#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

[InitializeOnLoad]
public static class DailyRoutineWuPlayModeVerifier
{
    public const string RequestPath = "Temp/phase157-daily-routine-wu.request";
    public const string ReportPath = "Artifacts/QA/phase157-daily-routine-wu-playmode.txt";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private static bool runnerCreated;
    private const string RequestedSeedSessionKey =
        "DungeonStory.DailyRoutineWu.RequestedSeed";

    static DailyRoutineWuPlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/QA/Request Phase 157 Daily Routine WU")]
    public static void RequestRunFromMenu()
    {
        RequestRun(157_180);
    }

    public static void RequestRun(int runSeed)
    {
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ReportPath);
        int normalizedSeed = runSeed != 0 ? runSeed : 1;
        SessionState.SetInt(RequestedSeedSessionKey, normalizedSeed);
        File.WriteAllText(
            RequestPath,
            $"seed={normalizedSeed};requestedUtc={DateTime.UtcNow:O}");
    }

    private static void OnEditorUpdate()
    {
        if (!File.Exists(RequestPath)
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            runnerCreated = false;
            return;
        }
        if (change != PlayModeStateChange.EnteredPlayMode
            || runnerCreated
            || !File.Exists(RequestPath))
        {
            return;
        }

        runnerCreated = true;
        File.Delete(RequestPath);
        new GameObject("Phase 157 Daily Routine WU Runner")
            .AddComponent<DailyRoutineWuPlayModeRunner>();
    }
}

public sealed class DailyRoutineWuPlayModeRunner : MonoBehaviour
{
    public const string DailyCadenceContractVersion = "cadence-v2";
    public const float ToiletCadenceMinimum = 0.6f;
    public const float ToiletCadenceMaximum = 1.0f;
    public const float HygieneCadenceMinimum = 0.6f;
    public const float HygieneCadenceMaximum = 1.0f;
    private const int ObservationDays = 5;
    private const float ObservationGameSeconds =
        SettlementLaborBalanceRules.SecondsPerDay * ObservationDays;
    private const float SteadyStateWarmupGameSeconds = 130f;
    private const int ObservationGameSpeed = 5;
    private const float WarmupRealtimeSeconds = 1.5f;
    private const string RequestedSeedSessionKey =
        "DungeonStory.DailyRoutineWu.RequestedSeed";

    private readonly List<string> failures = new();
    private readonly List<string> warnings = new();
    private readonly List<string> capturedIssues = new();
    private readonly Dictionary<CharacterActor, ActorRoutineObservation> observations = new();
    private readonly List<GameObject> temporaryObjects = new();
    private readonly List<BuildingSO> temporaryDefinitions = new();
    private float originalTimeScale;
    private int originalGameSpeed = 1;
    private ISettlementLaborAccountingService labor;
    private IGameClock gameClock;
    private IGameSpeedController gameSpeed;
    private IDungeonDebugModeService debugMode;
    private IWorkOrderRuntime workOrders;
    private IWorldItemStackRuntime itemStacks;
    private ICharacterCarryInventoryRegistry carryInventories;
    private IEnvironmentalFieldPersistence environmentalField;
    private ICharacterEnvironmentPersistence characterEnvironment;
    private IItemDefinitionCatalog itemDefinitions;
    private ICharacterAiWorldRegistry worldRegistry;
    private ICharacterConsumablesPersistence consumablesPersistence;
    private ICharacterDeprivationRuntime deprivationRuntime;
    private StaffDiscontentRuntime staffDiscontent;
    private IGameEventBus gameEvents;
    private IExteriorIncidentRuntime exteriorIncidents;
    private IDisposable mealConsumedSubscription;
    private IDisposable waterConsumedSubscription;
    private IDisposable primitiveSurvivalSubscription;
    private IDisposable primitiveSurvivalStartedSubscription;
    private IDisposable facilityVisitSubscription;
    private readonly List<string> consumedEventTrace = new();
    private readonly Dictionary<string, int> primitiveSurvivalCounts = new(
        StringComparer.Ordinal);
    private readonly List<string> primitiveSurvivalTrace = new();
    private readonly Dictionary<FacilityRole, int> facilityVisitCounts = new();
    private int funRecoveryFacilityVisits;
    private bool observationActive;
    private int observedMealConsumptions;
    private int observedWaterConsumptions;
    private string fixtureFoodId = string.Empty;
    private string fixtureWaterId = string.Empty;
    private float fixtureFoodNutrition;
    private int fixtureFoodStartQuantity;
    private int fixtureWaterStartQuantity;
    private DungeonEnvironmentalFieldSaveData neutralEnvironmentalFieldState;
    private float nextNeutralEnvironmentRefreshAt;
    private Grid fixtureGrid;
    private Vector2Int[] fixtureActorPositions = Array.Empty<Vector2Int>();
    private Facility fixtureMealFacility;
    private ConstructionSite fixtureConstructionSite;
    private BuildingSO fixtureConstructionDefinition;
    private string fixtureConstructionOrderId = string.Empty;
    private float fixtureMeasuredStartWork;
    private Vector2Int fixtureConstructionAccessPosition;
    private Vector2Int fixtureWaterPosition;
    private bool originalFriendlyInvincible;
    private SettlementLaborAccountingSnapshot previousLabor;
    private long observedActualLaborMilliWu;
    private long observedOutputEquivalentMilliWu;
    private long observedConvertedMilliWu;
    private long observedAutomationMilliWu;
    private long observedLossMilliWu;
    private long observedEssentialMilliWu;
    private long observedFacilityMaintenanceMilliWu;
    private int observedLaborDayRollovers;
    private bool capturedFixtureWorkforce;
    private int peakFixtureActiveWorkers;
    private float peakFixtureEffectiveWorkers;
    private ProjectWorkforceSnapshot latestFixtureWorkforce;
    private CharacterActor[] fixtureActors = Array.Empty<CharacterActor>();
    private int quarantinedNonFixtureActorCount;
    private int finalActiveActorCount;
    private int requestedRunSeed;
    private AiDirectorRuntime suspendedAiDirector;
    private LocalLlmRequestQueue suspendedLlmQueue;

    [SerializeField]
    private string diagnosticStage = "not-started";
    [SerializeField]
    private float diagnosticStageEnteredRealtime;
    [SerializeField]
    private float diagnosticObservedGameSeconds;
    [SerializeField]
    private float diagnosticLastProgressRealtime;

    private void SetDiagnosticStage(string stage)
    {
        diagnosticStage = stage;
        diagnosticStageEnteredRealtime = Time.realtimeSinceStartup;
        diagnosticLastProgressRealtime = diagnosticStageEnteredRealtime;
    }

    private IEnumerator Start()
    {
        SetDiagnosticStage("initializing");
        Directory.CreateDirectory("Artifacts/QA");
        Application.logMessageReceived += OnLogMessageReceived;
        originalTimeScale = Time.timeScale;
        requestedRunSeed = SessionState.GetInt(
            RequestedSeedSessionKey,
            157_180);

        yield return null;
        yield return null;
        DungeonRuntimeLifetimeScope scope =
            UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        if (scope == null || scope.Container == null)
        {
            failures.Add("Gameplay lifetime scope is unavailable.");
            Finish(0f);
            yield break;
        }

        try
        {
            labor = scope.Container.Resolve<ISettlementLaborAccountingService>();
            gameClock = scope.Container.Resolve<IGameClock>();
            gameSpeed = scope.Container.Resolve<IGameSpeedController>();
            debugMode = scope.Container.Resolve<IDungeonDebugModeService>();
            workOrders = scope.Container.Resolve<IWorkOrderRuntime>();
            itemStacks = scope.Container.Resolve<IWorldItemStackRuntime>();
            carryInventories = scope.Container.Resolve<ICharacterCarryInventoryRegistry>();
            environmentalField = scope.Container.Resolve<IEnvironmentalFieldPersistence>();
            characterEnvironment = scope.Container.Resolve<ICharacterEnvironmentPersistence>();
            itemDefinitions = scope.Container.Resolve<IItemDefinitionCatalog>();
            worldRegistry = scope.Container.Resolve<ICharacterAiWorldRegistry>();
            consumablesPersistence = scope.Container.Resolve<ICharacterConsumablesPersistence>();
            deprivationRuntime = scope.Container.Resolve<ICharacterDeprivationRuntime>();
            staffDiscontent = UnityEngine.Object.FindFirstObjectByType<StaffDiscontentRuntime>();
            gameEvents = scope.Container.Resolve<IGameEventBus>();
            exteriorIncidents = scope.Container.Resolve<IExteriorIncidentRuntime>();
            exteriorIncidents.SetAutomaticIncidentChecksSuspended(true);
            mealConsumedSubscription = gameEvents.Subscribe<PhysicalMealConsumedEvent>(gameEvent =>
            {
                observedMealConsumptions++;
                consumedEventTrace.Add(
                    $"meal actor={gameEvent.Actor?.name ?? "missing"}; operation={gameEvent.OperationId.Value}; item={gameEvent.Result.ItemId}; nutrition={gameEvent.Result.Nutrition:0.###}");
            });
            waterConsumedSubscription = gameEvents.Subscribe<CharacterWaterConsumedEvent>(gameEvent =>
            {
                observedWaterConsumptions++;
                consumedEventTrace.Add(
                    $"water actor={gameEvent.CharacterId.Value}; source={gameEvent.SourceId}; amount={gameEvent.Amount:0.###}; quality={gameEvent.Quality}");
            });
            primitiveSurvivalSubscription = gameEvents.Subscribe<
                CharacterPrimitiveSurvivalCompletedEvent>(completed =>
            {
                if (!observationActive)
                {
                    return;
                }
                primitiveSurvivalCounts.TryGetValue(
                    completed.ActionId,
                    out int count);
                primitiveSurvivalCounts[completed.ActionId] = count + 1;
                primitiveSurvivalTrace.Add(
                    DescribePrimitiveSurvivalCompletion(completed));
            });
            primitiveSurvivalStartedSubscription = gameEvents.Subscribe<
                CharacterPrimitiveSurvivalStartedEvent>(started =>
            {
                if (observationActive)
                {
                    primitiveSurvivalTrace.Add(
                        DescribePrimitiveSurvivalStart(started));
                }
            });
            facilityVisitSubscription = gameEvents.Subscribe<FacilityVisitEvent>(
                visit =>
                {
                    if (!observationActive
                        || !TryResolveObservedFacilityRole(
                            visit,
                            out FacilityRole observedRole))
                    {
                        return;
                    }
                    facilityVisitCounts.TryGetValue(
                        observedRole,
                        out int count);
                    facilityVisitCounts[observedRole] = count + 1;
                    if (visit.facility?.BuildingData.GetNeedRecovery().fun > 0f)
                    {
                        funRecoveryFacilityVisits++;
                    }
                });
        }
        catch (Exception exception)
        {
            failures.Add("Settlement labor accounting resolve failed: "
                + Compact(exception.Message));
        }

        SetDiagnosticStage("ensuring-actors");
        yield return EnsureGameplayActors(scope);
        suspendedAiDirector =
            UnityEngine.Object.FindFirstObjectByType<AiDirectorRuntime>();
        if (suspendedAiDirector != null)
        {
            suspendedAiDirector.enabled = false;
        }
        suspendedLlmQueue =
            UnityEngine.Object.FindFirstObjectByType<LocalLlmRequestQueue>();
        if (suspendedLlmQueue != null)
        {
            suspendedLlmQueue.AbortAllForDebug();
            suspendedLlmQueue.enabled = false;
        }
        CharacterSpawner sceneSpawner =
            UnityEngine.Object.FindFirstObjectByType<CharacterSpawner>();
        if (sceneSpawner != null)
        {
            sceneSpawner.StopAllCoroutines();
            sceneSpawner.enabled = false;
        }
        originalGameSpeed = gameSpeed?.Speed ?? 1;
        gameSpeed?.SetSpeed(ObservationGameSpeed);
        if (debugMode != null)
        {
            originalFriendlyInvincible = debugMode.IsCheatEnabled(
                DungeonDebugCheat.FriendlyInvincible);
            debugMode.SetCheat(DungeonDebugCheat.FriendlyInvincible, true);
        }
        SetDiagnosticStage("startup-wait");
        yield return new WaitForSecondsRealtime(WarmupRealtimeSeconds);

        CharacterActor[] discoveredActors = FindActors();
        CharacterActor[] actors = discoveredActors
            .Where(actor => actor != null
                && !actor.IsDead
                && actor.GetComponent<AbilityWork>() != null)
            .OrderBy(
                actor => actor.Identity?.PersistentId ?? actor.name,
                StringComparer.Ordinal)
            .Take(3)
            .ToArray();
        if (actors.Length < 3)
        {
            failures.Add($"Expected at least three live founders, found {actors.Length}.");
            Finish(0f);
            yield break;
        }
        fixtureActors = actors;
        HashSet<int> fixtureActorObjects = actors
            .Select(actor => actor.gameObject.GetInstanceID())
            .ToHashSet();
        foreach (CharacterActor extraActor in discoveredActors)
        {
            if (extraActor == null
                || fixtureActorObjects.Contains(
                    extraActor.gameObject.GetInstanceID()))
            {
                continue;
            }

            extraActor.gameObject.SetActive(false);
            quarantinedNonFixtureActorCount++;
        }
        bool fixtureCreated;
        string fixtureMessage;
        try
        {
            fixtureCreated = TryCreateNeutralRoutineFixture(
                scope,
                actors,
                out fixtureMessage);
        }
        catch (Exception exception)
        {
            fixtureCreated = false;
            fixtureMessage = Compact(exception.Message);
        }
        if (!fixtureCreated)
        {
            failures.Add("Neutral daily-routine fixture failed: " + fixtureMessage);
            Finish(0f);
            yield break;
        }
        else
        {
            warnings.Add("Fixture: " + fixtureMessage);
            foreach (CharacterActor actor in actors)
            {
                actor.Brain?.RequestImmediateReplan(clearFailures: true);
            }
            yield return new WaitForSecondsRealtime(.5f);
            SetDiagnosticStage("recreation-preflight");
            yield return VerifyRecreationSelectionPreflight(actors);
        }

        SetDiagnosticStage("steady-state-warmup");
        RestoreFixtureActorPositions(actors);
        yield return new WaitForSecondsRealtime(.25f);
        foreach (CharacterActor actor in actors)
        {
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
        }
        yield return WarmUpToSteadyState(actors);
        PrepareFixtureWorkOrderReset();
        yield return null;
        string measuredConsumables;
        try
        {
            measuredConsumables = SeedAccessibleStarterConsumables(
                fixtureWaterPosition);
        }
        catch (Exception exception)
        {
            failures.Add("Measured-day consumable seeding failed: "
                + Compact(exception.Message));
            Finish(0f);
            yield break;
        }
        warnings.Add("Measured-day supplies: " + measuredConsumables + ".");
        ResetNeutralNeedsMoodAndDiscontent(actors);
        RestoreFixtureActorPositions(actors);
        if (!ResetFixtureWorkOrderForMeasuredDay(out string resetFailure))
        {
            failures.Add("Measured-day work-order reset failed: " + resetFailure);
        }
        fixtureFoodStartQuantity = CountFixtureItemQuantity(fixtureFoodId);
        fixtureWaterStartQuantity = CountFixtureItemQuantity(fixtureWaterId);
        if (workOrders == null
            || fixtureConstructionSite == null
            || !workOrders.TryGetOrderFor(
                fixtureConstructionSite,
                BuiltInWorkTypeIds.Construct,
                out WorkOrderProgressState measuredStartOrder))
        {
            failures.Add("Measured-day construction progress baseline is unavailable.");
            fixtureMeasuredStartWork = 0f;
        }
        else
        {
            fixtureMeasuredStartWork = measuredStartOrder.CompletedWork;
        }
        consumedEventTrace.Clear();
        observedMealConsumptions = 0;
        observedWaterConsumptions = 0;
        primitiveSurvivalCounts.Clear();
        primitiveSurvivalTrace.Clear();
        facilityVisitCounts.Clear();
        funRecoveryFacilityVisits = 0;
        foreach (CharacterActor actor in actors)
        {
            observations[actor] = new ActorRoutineObservation(actor);
        }
        observationActive = true;
        SetDiagnosticStage("observing-five-days");

        previousLabor = labor?.Capture() ?? default;
        float observedGameSeconds = 0f;
        float previousGameTime = gameClock?.Time ?? Time.time;
        float realtimeDeadline = Time.realtimeSinceStartup + 600f;
        while (observedGameSeconds < ObservationGameSeconds
            && Time.realtimeSinceStartup < realtimeDeadline)
        {
            float currentGameTime = gameClock?.Time ?? Time.time;
            float step = Mathf.Min(
                Mathf.Max(0f, currentGameTime - previousGameTime),
                ObservationGameSeconds - observedGameSeconds);
            previousGameTime = currentGameTime;
            if (step > 0f)
            {
                MaintainNeutralEnvironment(actors, currentGameTime);
                foreach (ActorRoutineObservation observation in observations.Values)
                {
                    observation.StabilizeNeutralHealth();
                    observation.Observe(step);
                }
                ObserveFixtureWorkforce();
                ObserveLabor();
                observedGameSeconds += step;
                diagnosticObservedGameSeconds = observedGameSeconds;
                diagnosticLastProgressRealtime = Time.realtimeSinceStartup;
            }
            if (observedGameSeconds >= ObservationGameSeconds)
            {
                break;
            }
            yield return null;
        }

        finalActiveActorCount = FindActors().Length;
        if (finalActiveActorCount != fixtureActors.Length)
        {
            failures.Add(
                $"Three-founder fixture population changed during observation: expected {fixtureActors.Length}, active {finalActiveActorCount}.");
        }

        if (observedGameSeconds < ObservationGameSeconds - .25f)
        {
            failures.Add($"Observation timed out at {observedGameSeconds:0.###} game seconds.");
        }
        if (observedActualLaborMilliWu <= 0L)
        {
            failures.Add("Central labor accounting received no approved work WU during the observed day.");
        }
        if (observations.Values.All(value => value.WorkActiveSeconds <= 0f))
        {
            failures.Add("No actor entered actual work execution during the observed day.");
        }
        if (observations.Values.All(value => value.PersonalNeedSeconds <= 0f))
        {
            warnings.Add("No personal-need action was observed; the sample may have started immediately after recovery.");
        }
        Finish(observedGameSeconds);
    }

    private void ObserveLabor()
    {
        if (labor == null)
        {
            return;
        }
        SettlementLaborAccountingSnapshot current = labor.Capture();
        observedActualLaborMilliWu += DeltaActualLaborAcrossDay(
            previousLabor,
            current,
            out int completedDayDelta);
        observedOutputEquivalentMilliWu += DeltaOutputEquivalentAcrossDay(
            previousLabor,
            current,
            completedDayDelta);
        observedLaborDayRollovers += completedDayDelta;
        observedConvertedMilliWu += DeltaAcrossDay(
            previousLabor.ConvertedProcessOutputMilliWu,
            current.ConvertedProcessOutputMilliWu);
        observedAutomationMilliWu += DeltaAcrossDay(
            previousLabor.DomainAutomationMilliWu,
            current.DomainAutomationMilliWu);
        observedLossMilliWu += DeltaAcrossDay(
            previousLabor.LossMilliWu,
            current.LossMilliWu);
        observedEssentialMilliWu += DeltaAcrossDay(
            previousLabor.EssentialMaintenanceMilliWu,
            current.EssentialMaintenanceMilliWu);
        observedFacilityMaintenanceMilliWu += DeltaAcrossDay(
            previousLabor.EquipmentFacilityMaintenanceMilliWu,
            current.EquipmentFacilityMaintenanceMilliWu);
        previousLabor = current;
    }

    private static long DeltaAcrossDay(long previous, long current) =>
        current >= previous ? current - previous : current;

    private static long DeltaActualLaborAcrossDay(
        SettlementLaborAccountingSnapshot previous,
        SettlementLaborAccountingSnapshot current,
        out int completedDayDelta)
    {
        completedDayDelta = current.CompletedDayCount - previous.CompletedDayCount;
        if (completedDayDelta <= 0)
        {
            completedDayDelta = 0;
            return Math.Max(
                0L,
                current.ActualLaborMilliWu - previous.ActualLaborMilliWu);
        }

        if (completedDayDelta == 1)
        {
            long previousDayRemainder = Math.Max(
                0L,
                current.LatestDay.ActualLaborMilliWu
                    - previous.ActualLaborMilliWu);
            return checked(previousDayRemainder + current.ActualLaborMilliWu);
        }

        throw new InvalidOperationException(
            $"Daily labor verifier skipped {completedDayDelta} completed days between observations.");
    }

    private static long DeltaOutputEquivalentAcrossDay(
        SettlementLaborAccountingSnapshot previous,
        SettlementLaborAccountingSnapshot current,
        int completedDayDelta)
    {
        long previousCurrent = GetCurrentOutputEquivalent(previous);
        long currentCurrent = GetCurrentOutputEquivalent(current);
        if (completedDayDelta == 0)
        {
            return Math.Max(0L, currentCurrent - previousCurrent);
        }

        if (completedDayDelta == 1)
        {
            long previousDayRemainder = Math.Max(
                0L,
                current.LatestDay.OutputEquivalentMilliWu - previousCurrent);
            return checked(previousDayRemainder + currentCurrent);
        }

        throw new InvalidOperationException(
            $"Daily output-equivalent verifier skipped {completedDayDelta} completed days between observations.");
    }

    private static long GetCurrentOutputEquivalent(
        SettlementLaborAccountingSnapshot snapshot) =>
        Math.Max(
            0L,
            checked(snapshot.ActualLaborMilliWu
                + snapshot.ConvertedProcessOutputMilliWu
                + snapshot.DomainAutomationMilliWu
                - snapshot.LossMilliWu));

    private IEnumerator WarmUpToSteadyState(
        IReadOnlyList<CharacterActor> actors)
    {
        float elapsed = 0f;
        float previous = gameClock?.Time ?? Time.time;
        float deadline = Time.realtimeSinceStartup + 55f;
        while (elapsed < SteadyStateWarmupGameSeconds
            && Time.realtimeSinceStartup < deadline)
        {
            MaintainNeutralEnvironment(
                actors,
                gameClock?.Time ?? Time.time);
            foreach (CharacterActor actor in actors)
            {
                if (actor != null
                    && !actor.IsDead
                    && actor.CurrentHealth < actor.MaxHealth)
                {
                    actor.Heal(actor.MaxHealth - actor.CurrentHealth);
                }
            }
            float current = gameClock?.Time ?? Time.time;
            elapsed += Mathf.Min(
                Mathf.Max(0f, current - previous),
                SteadyStateWarmupGameSeconds - elapsed);
            previous = current;
            yield return null;
        }
        if (elapsed < SteadyStateWarmupGameSeconds - .25f)
        {
            failures.Add($"Steady-state warmup timed out at {elapsed:0.###} game seconds.");
        }
        else
        {
            warnings.Add($"Steady-state warmup completed for {elapsed:0.###} game seconds before the measured day.");
        }
    }

    private IEnumerator EnsureGameplayActors(DungeonRuntimeLifetimeScope scope)
    {
        if (FindActors().Length > 0)
        {
            yield break;
        }

        IStartPartyPreparationService preparation;
        IOwnerRunManagerProvider ownerProvider;
        IPreparedStartPartyGameplayApplier applier;
        try
        {
            preparation = scope.Container.Resolve<IStartPartyPreparationService>();
            ownerProvider = scope.Container.Resolve<IOwnerRunManagerProvider>();
            applier = scope.Container.Resolve<IPreparedStartPartyGameplayApplier>();
        }
        catch (Exception exception)
        {
            failures.Add("Start-party dependency resolve failed: "
                + Compact(exception.Message));
            yield break;
        }

        if (!ownerProvider.TryGetManager(out OwnerRunManager manager)
            || manager == null)
        {
            failures.Add("Owner manager is unavailable.");
            yield break;
        }

        CharacterSO owner = manager.OwnerCandidates?
            .FirstOrDefault(value => value != null);
        string beginMessage = string.Empty;
        if (owner == null
            || !preparation.Begin(owner, out beginMessage))
        {
            failures.Add("Start-party preparation failed: " + beginMessage);
            yield break;
        }

        bool created = preparation.TryCreatePreparedSnapshot(
            DungeonDifficulty.Normal,
            requestedRunSeed,
            out PreparedStartPartySnapshot snapshot,
            out string snapshotMessage);
        preparation.Cancel();
        string applyMessage = string.Empty;
        if (!created || !applier.TryApply(snapshot, out applyMessage))
        {
            failures.Add("Start-party application failed: "
                + (created ? applyMessage : snapshotMessage));
            yield break;
        }

        float deadline = Time.realtimeSinceStartup + 10f;
        while (FindActors().Length == 0 && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
    }

    private void Finish(float observedGameSeconds)
    {
        diagnosticObservedGameSeconds = observedGameSeconds;
        SetDiagnosticStage("writing-report");
        observationActive = false;
        Application.logMessageReceived -= OnLogMessageReceived;
        mealConsumedSubscription?.Dispose();
        waterConsumedSubscription?.Dispose();
        primitiveSurvivalSubscription?.Dispose();
        primitiveSurvivalStartedSubscription?.Dispose();
        facilityVisitSubscription?.Dispose();
        mealConsumedSubscription = null;
        waterConsumedSubscription = null;
        primitiveSurvivalSubscription = null;
        primitiveSurvivalStartedSubscription = null;
        facilityVisitSubscription = null;
        if (debugMode != null)
        {
            debugMode.SetCheat(
                DungeonDebugCheat.FriendlyInvincible,
                originalFriendlyInvincible);
        }
        if (gameSpeed != null)
        {
            gameSpeed.SetSpeed(originalGameSpeed);
        }
        exteriorIncidents?.SetAutomaticIncidentChecksSuspended(false);
        if (suspendedLlmQueue != null)
        {
            suspendedLlmQueue.enabled = true;
        }
        if (suspendedAiDirector != null)
        {
            suspendedAiDirector.enabled = true;
        }
        Time.timeScale = originalTimeScale;
        File.Delete(DailyRoutineWuPlayModeVerifier.RequestPath);

        StringBuilder report = new(16384);
        report.AppendLine("# Phase 157 daily-routine live WU trace");
        report.AppendLine($"scene={SceneManager.GetActiveScene().path}");
        report.AppendLine($"observedGameSeconds={observedGameSeconds:0.###}");
        report.AppendLine($"observedDays={ObservationDays}");
        report.AppendLine($"runSeed={requestedRunSeed}");
        report.AppendLine($"dailyCadenceContract={DailyCadenceContractVersion}; toilet={ToiletCadenceMinimum:0.###}~{ToiletCadenceMaximum:0.###}; hygiene={HygieneCadenceMinimum:0.###}~{HygieneCadenceMaximum:0.###}");
        report.AppendLine($"actors={observations.Count}");
        report.AppendLine($"activeActorsAtEnd={finalActiveActorCount}");
        report.AppendLine($"quarantinedNonFixtureActors={quarantinedNonFixtureActorCount}");
        report.AppendLine($"capturedIssues={capturedIssues.Count}");
        report.AppendLine();
        report.AppendLine("## Central labor channels observed during the window");
        report.AppendLine($"actualLaborWU={observedActualLaborMilliWu / 1000f:0.###}");
        report.AppendLine($"outputEquivalentWU={observedOutputEquivalentMilliWu / 1000f:0.###}");
        report.AppendLine($"laborDayRollovers={observedLaborDayRollovers}");
        report.AppendLine($"convertedProcessWU={observedConvertedMilliWu / 1000f:0.###}");
        report.AppendLine($"domainAutomationWU={observedAutomationMilliWu / 1000f:0.###}");
        report.AppendLine($"lossWU={observedLossMilliWu / 1000f:0.###}");
        report.AppendLine($"essentialMaintenanceWU={observedEssentialMilliWu / 1000f:0.###}");
        report.AppendLine($"equipmentFacilityMaintenanceWU={observedFacilityMaintenanceMilliWu / 1000f:0.###}");
        report.AppendLine();
        int foodEndQuantity = CountFixtureItemQuantity(fixtureFoodId);
        int waterEndQuantity = CountFixtureItemQuantity(fixtureWaterId);
        int foodConsumed = fixtureFoodStartQuantity - foodEndQuantity;
        int waterConsumed = fixtureWaterStartQuantity - waterEndQuantity;
        int actorDays = Math.Max(1, observations.Count * ObservationDays);
        float mealsPerActorDay = observedMealConsumptions / (float)actorDays;
        float drinksPerActorDay = observedWaterConsumptions / (float)actorDays;
        float initialMealDelayDays = (85f - 50f) / 50f;
        float expectedMealsPerActor = fixtureFoodNutrition > 0f
            ? 1f + Mathf.Floor(Mathf.Max(
                0f,
                (ObservationDays - initialMealDelayDays)
                / (fixtureFoodNutrition / 50f)))
            : 0f;
        float expectedMealsPerActorDay = expectedMealsPerActor
            / ObservationDays;
        float minimumMealsPerActorDay = Mathf.Max(
            .1f,
            expectedMealsPerActorDay - .2f);
        float maximumMealsPerActorDay = expectedMealsPerActorDay + .2f;
        if (mealsPerActorDay < minimumMealsPerActorDay
            || mealsPerActorDay > maximumMealsPerActorDay)
        {
            failures.Add(
                $"Physical meals averaged {mealsPerActorDay:0.###} per actor-day; expected {minimumMealsPerActorDay:0.###}~{maximumMealsPerActorDay:0.###} for {fixtureFoodNutrition:0.###} nutrition.");
        }
        if (drinksPerActorDay < .75f || drinksPerActorDay > 1.25f)
        {
            failures.Add(
                $"Physical drinks averaged {drinksPerActorDay:0.###} per actor-day; expected 0.75~1.25.");
        }
        DungeonCharacterConsumablesSaveData consumables = consumablesPersistence?.Capture();
        report.AppendLine("## Physical consumable authority");
        report.AppendLine($"food item={fixtureFoodId}; start={fixtureFoodStartQuantity}; end={foodEndQuantity}; stockDepletion={foodConsumed}; consumedEvents={observedMealConsumptions}");
        report.AppendLine($"water item={fixtureWaterId}; start={fixtureWaterStartQuantity}; end={waterEndQuantity}; stockDepletion={waterConsumed}; consumedEvents={observedWaterConsumptions}");
        report.AppendLine($"mealsPerActorDay={mealsPerActorDay:0.###}; expectedMealsPerActorDay={expectedMealsPerActorDay:0.###}; drinksPerActorDay={drinksPerActorDay:0.###}");
        report.AppendLine($"actualLaborWuPerActorDay={observedActualLaborMilliWu / 1000f / actorDays:0.###}; outputEquivalentWuPerActorDay={observedOutputEquivalentMilliWu / 1000f / actorDays:0.###}");
        report.AppendLine($"activeMealPlans={consumables?.activeMealPlans?.Count ?? -1}; completedMealOperations={consumables?.completedOperations?.Count(value => value != null && value.meal) ?? -1}");
        CharacterDeprivationDiagnosticsSnapshot deprivation =
            deprivationRuntime?.GetDiagnostics() ?? default;
        report.AppendLine(
            $"safeDrink requests={deprivation.SafeReliefRequests}; planFailures={deprivation.SafeReliefPlanFailures}; started={deprivation.SafeReliefActionsStarted}; running={deprivation.SafeReliefRunningActions}; arrivals={deprivation.SafeReliefArrivals}; interactions={deprivation.SafeReliefInteractionAttempts}; successes={deprivation.SafeReliefSuccesses}; finished={deprivation.SafeReliefActionsFinished}; moveFailures={deprivation.SafeReliefMoveFailures}; missingPath={deprivation.SafeReliefMissingPathFailures}; cancelled={deprivation.SafeReliefCancelledMoveFailures}; maxPath={deprivation.SafeReliefMaximumPlannedPathSteps}; maxDuration={deprivation.SafeReliefMaximumDurationSeconds:0.###}");
        foreach (string eventTrace in consumedEventTrace)
        {
            report.AppendLine("event=" + eventTrace);
        }
        report.AppendLine();
        report.AppendLine("## Per-actor sampled game seconds");
        report.AppendLine("| actor | work active | work transit | work queue | meal service | drink service | sleep service | toilet service | hygiene service | recreation service | need travel | need queue | other travel | idle/other | total | visits M/D/S/T/H/R | need start -> end |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---|");
        foreach (ActorRoutineObservation observation in observations.Values
                     .OrderBy(value => value.ActorName, StringComparer.Ordinal))
        {
            observation.Append(report);
        }

        float sampledActorSeconds = observations.Values.Sum(value => value.TotalSeconds);
        float sampledActorDays = Math.Max(1f, actorDays);
        report.AppendLine();
        report.AppendLine("## Mean time channels per actor-day");
        report.AppendLine($"workActive={observations.Values.Sum(value => value.WorkActiveSeconds) / sampledActorDays:0.###}");
        report.AppendLine($"workTransit={observations.Values.Sum(value => value.WorkTransitSeconds) / sampledActorDays:0.###}");
        report.AppendLine($"workQueue={observations.Values.Sum(value => value.WorkQueueSeconds) / sampledActorDays:0.###}");
        report.AppendLine($"needService={observations.Values.Sum(value => value.PersonalServiceSeconds) / sampledActorDays:0.###}");
        report.AppendLine($"needTravel={observations.Values.Sum(value => value.NeedTravelSeconds) / sampledActorDays:0.###}");
        report.AppendLine($"needQueue={observations.Values.Sum(value => value.NeedQueueSeconds) / sampledActorDays:0.###}");
        report.AppendLine($"otherTravel={observations.Values.Sum(value => value.OtherTravelSeconds) / sampledActorDays:0.###}");
        report.AppendLine($"idleOther={observations.Values.Sum(value => value.IdleOtherSeconds) / sampledActorDays:0.###}");
        report.AppendLine($"sampledSeconds={sampledActorSeconds:0.###}; expected={observedGameSeconds * observations.Count:0.###}; delta={sampledActorSeconds - observedGameSeconds * observations.Count:0.###}");

        report.AppendLine();
        report.AppendLine("## Need travel attribution");
        foreach (ActorRoutineObservation observation in observations.Values
                     .OrderBy(value => value.ActorName, StringComparer.Ordinal))
        {
            report.AppendLine(observation.DescribeNeedTravelAttribution());
            foreach (string evidence in observation.EnumerateNeedTravelEvidence())
            {
                report.AppendLine($"needTravelSegment actor={observation.ActorName}; {evidence}");
            }
        }

        report.AppendLine();
        report.AppendLine("## Cumulative AI lifecycle and harmful-stall diagnostics");
        foreach (ActorRoutineObservation observation in observations.Values
                     .OrderBy(value => value.ActorName, StringComparer.Ordinal))
        {
            report.AppendLine(observation.DescribeCumulativeDiagnostics());
            foreach (string evidence in observation.EnumerateStallEvidence())
            {
                report.AppendLine($"stall actor={observation.ActorName}; {evidence}");
            }
            if (observation.HarmfulStallEpisodes > 0)
            {
                failures.Add(
                    $"{observation.ActorName} encountered {observation.HarmfulStallEpisodes} harmful no-progress episode(s); inspect cumulative stall evidence.");
            }
            if (observation.InteractionActionReplacementDelta > 0)
            {
                failures.Add(
                    $"{observation.ActorName} lost ownership of {observation.InteractionActionReplacementDelta} active facility interaction(s); inspect cumulative AI lifecycle diagnostics.");
            }
            if (observation.OrphanWorkActionRecoveryDelta > 0)
            {
                failures.Add(
                    $"{observation.ActorName} required {observation.OrphanWorkActionRecoveryDelta} orphan Work action lifecycle recovery event(s); inspect cumulative AI lifecycle diagnostics.");
            }
        }

        int toiletVisits = GetFacilityVisitCount(FacilityRole.Toilet);
        int hygieneVisits = GetFacilityVisitCount(FacilityRole.Hygiene);
        int recreationVisits = GetFacilityVisitCount(
            FacilityRole.Entertainment);
        int restVisits = GetFacilityVisitCount(FacilityRole.Rest);
        float toiletVisitsPerActorDay = toiletVisits / sampledActorDays;
        float hygieneVisitsPerActorDay = hygieneVisits / sampledActorDays;
        float recreationVisitsPerActorDay = recreationVisits / sampledActorDays;
        float funRecoveryVisitsPerActorDay =
            funRecoveryFacilityVisits / sampledActorDays;
        report.AppendLine();
        report.AppendLine("## Completed authored facility uses");
        report.AppendLine(
            $"toilet={toiletVisits}; hygiene={hygieneVisits}; recreationBranch={recreationVisits}; funRecovery={funRecoveryFacilityVisits}; rest={restVisits}");
        report.AppendLine(
            $"perActorDay toilet={toiletVisitsPerActorDay:0.###}; hygiene={hygieneVisitsPerActorDay:0.###}; recreationBranch={recreationVisitsPerActorDay:0.###}; funRecovery={funRecoveryVisitsPerActorDay:0.###}");
        report.AppendLine(
            "primitive=" + (primitiveSurvivalCounts.Count == 0
                ? "none"
                : string.Join(", ", primitiveSurvivalCounts
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .Select(value => $"{value.Key}:{value.Value}"))));
        foreach (string trace in primitiveSurvivalTrace)
        {
            report.AppendLine("primitiveTrace " + trace);
        }

        ValidateDailyFacilityCadence(
            "toilet",
            toiletVisitsPerActorDay,
            ToiletCadenceMinimum,
            ToiletCadenceMaximum);
        ValidateDailyFacilityCadence(
            "hygiene",
            hygieneVisitsPerActorDay,
            HygieneCadenceMinimum,
            HygieneCadenceMaximum);
        ValidateDailyFacilityCadence(
            "recreation",
            funRecoveryVisitsPerActorDay,
            .6f,
            1.4f);
        if (primitiveSurvivalCounts.Values.Sum() > 0)
        {
            failures.Add(
                "Primitive survival actions executed while the neutral fixture supplied meal, rest, toilet, and hygiene facilities: "
                + string.Join(", ", primitiveSurvivalCounts
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .Select(value => $"{value.Key}={value.Value}")));
        }

        report.AppendLine();
        report.AppendLine("## Final live actor diagnostics");
        CharacterAiScheduler scheduler = FindFirstObjectByType<CharacterAiScheduler>();
        if (scheduler != null)
        {
            report.AppendLine(
                $"scheduler enabled={scheduler.enabled}; driving={scheduler.IsDrivingAi}; registered={scheduler.RegisteredCharacterCount}; lastProcessed={scheduler.LastProcessedDecisionCount}; cumulativeProcessed={scheduler.CumulativeProcessedDecisionCount}; starved={scheduler.CumulativeStarvedDecisionCount}; skipped={scheduler.CumulativeSkippedDecisionCount}; budget={scheduler.CurrentDecisionBudget}; pathBudget={scheduler.CurrentPathSearchBudget}; pathUsed={scheduler.LastPathSearchCount}; brokerPathUsed={scheduler.LastBrokerPathSearchCount}; brokerDeferrals={scheduler.LastBrokerPathBudgetDeferralCount}; exhausted={scheduler.LastBudgetExhausted}; processingMs={scheduler.LastProcessingMilliseconds:0.###}");
        }
        foreach (ActorRoutineObservation observation in observations.Values
                     .OrderBy(value => value.ActorName, StringComparer.Ordinal))
        {
            float nextDecisionDelay = scheduler != null
                ? scheduler.GetNextDecisionDelayForDebug(observation.Actor)
                : -1f;
            report.AppendLine(
                observation.DescribeRuntimeState()
                + $"; nextDecisionDelay={nextDecisionDelay:0.###}");
            report.AppendLine(
                $"aiTrace actor={observation.ActorName}; "
                + (observation.Actor?.Brain?.CaptureRuntimeDiagnostics()
                    .FormatRecentTrace() ?? "none"));
            CharacterLog log = observation.Actor?.LogComponent;
            if (log?.ActivityEntries == null)
            {
                continue;
            }
            foreach (CharacterActivityEvent activity in log.ActivityEntries
                         .Where(value => value != null)
                         .TakeLast(12))
            {
                report.AppendLine(
                    $"activity actor={observation.ActorName}; kind={activity.KindId}; outcome={activity.OutcomeId}; action={activity.ActionId}; target={activity.TargetId}; reason={activity.ReasonCode}; fact={activity.FactText}");
            }
        }
        if (fixtureMealFacility != null)
        {
            report.AppendLine(
                $"mealFacility id={fixtureMealFacility.PersistentInstanceId.Value}; users={fixtureMealFacility.CurrentUserCount}; reservations={fixtureMealFacility.ActiveVisitReservationCount}; capacity={fixtureMealFacility.EffectiveCapacity}; pos={fixtureMealFacility.centerPos}");
            report.AppendLine("## Fixture meal-candidate diagnostics");
            foreach (ActorRoutineObservation observation in observations.Values
                         .OrderBy(value => value.ActorName, StringComparer.Ordinal))
            {
                GridPathSearchResult search = observation.Actor?.Brain?
                    .GetPathSearch(observation.Actor);
                report.AppendLine(
                    $"actor={observation.ActorName}; "
                    + FacilityCandidateScorer.DescribeCandidateForDiagnostics(
                        observation.Actor,
                        fixtureMealFacility,
                        FacilityRole.Meal,
                        search));
            }
        }
        if (capturedFixtureWorkforce)
        {
            report.AppendLine(
                $"fixtureWorkforce project={latestFixtureWorkforce.ProjectId}; scale={latestFixtureWorkforce.Scale}; activeNow={latestFixtureWorkforce.ActiveWorkers}; peakActive={peakFixtureActiveWorkers}; peakEffective={peakFixtureEffectiveWorkers:0.###}; max={latestFixtureWorkforce.MaximumWorkers}; automaticLimit={latestFixtureWorkforce.DefaultAutomaticWorkerLimit}; nextContribution={latestFixtureWorkforce.NextWorkerContribution:0.###}");
            if (latestFixtureWorkforce.Scale != ProjectScale.IndustrialFacility
                || latestFixtureWorkforce.MaximumWorkers != 4)
            {
                failures.Add(
                    $"Fixture construction workforce contract is {latestFixtureWorkforce.Scale}/{latestFixtureWorkforce.MaximumWorkers}; expected IndustrialFacility/4.");
            }
            if (peakFixtureActiveWorkers < 2)
            {
                failures.Add(
                    $"Fixture construction never demonstrated parallel execution; peak active workers={peakFixtureActiveWorkers}.");
            }
        }
        else
        {
            failures.Add(
                "Fixture construction workforce snapshot was never available during the measured window.");
        }
        if (workOrders != null
            && fixtureConstructionSite != null
            && workOrders.TryGetOrderFor(
                fixtureConstructionSite,
                BuiltInWorkTypeIds.Construct,
                out WorkOrderProgressState fixtureOrder))
        {
            float measuredProjectDelta = Mathf.Max(
                0f,
                fixtureOrder.CompletedWork - fixtureMeasuredStartWork);
            float measuredOutputEquivalent =
                observedOutputEquivalentMilliWu / 1000f;
            // Project progress is accumulated as float while central labor is
            // booked in milli-WU. Five simulated days can differ by a few
            // hundredths solely from the two quantization domains.
            const float projectCausalToleranceWu = .05f;
            if (Mathf.Abs(measuredProjectDelta - measuredOutputEquivalent)
                > projectCausalToleranceWu)
            {
                failures.Add(
                    $"Measured construction delta {measuredProjectDelta:0.###} WU does not match central output-equivalent labor {measuredOutputEquivalent:0.###} WU within {projectCausalToleranceWu:0.###} WU.");
            }
            report.AppendLine($"fixtureOrder status={fixtureOrder.Status}; measuredStart={fixtureMeasuredStartWork:0.###}; measuredEnd={fixtureOrder.CompletedWork:0.###}; measuredDelta={measuredProjectDelta:0.###}; required={fixtureOrder.RequiredWork:0.###}; destination={fixtureOrder.MaterialDestinationId}; reservedWorker={fixtureOrder.ReservedWorkerPersistentId}");
        }

        report.AppendLine();
        report.AppendLine("## Diagnostics");
        foreach (string warning in warnings)
        {
            report.AppendLine("[WARN] " + warning);
        }
        foreach (string failure in failures)
        {
            report.AppendLine("[FAIL] " + failure);
        }
        foreach (string issue in capturedIssues.Take(30))
        {
            report.AppendLine("[CONSOLE] " + issue);
        }
        report.AppendLine($"RESULT={(failures.Count == 0 && capturedIssues.Count == 0 ? "PASS" : "FAIL")}; failures={failures.Count}; warnings={warnings.Count}; capturedIssues={capturedIssues.Count}");
        string seededReportPath =
            $"Artifacts/QA/phase157-daily-routine-wu-seed-{requestedRunSeed}.txt";
        File.WriteAllText(
            seededReportPath,
            report.ToString(),
            Encoding.UTF8);
        File.WriteAllText(
            DailyRoutineWuPlayModeVerifier.ReportPath,
            report.ToString(),
            Encoding.UTF8);
        CleanupFixture();
        Debug.Log($"PHASE157_DAILY_ROUTINE_WU={(failures.Count == 0 && capturedIssues.Count == 0 ? "PASS" : "FAIL")}; "
            + $"actors={observations.Count}; gameSeconds={observedGameSeconds:0.###}; "
            + $"actualWU={observedActualLaborMilliWu / 1000f:0.###}; failures={failures.Count}");
        EditorApplication.delayCall += EditorApplication.ExitPlaymode;
    }

    private static bool TryResolveObservedFacilityRole(
        FacilityVisitEvent visit,
        out FacilityRole role)
    {
        CharacterActor actor = visit.visitorActor;
        CharacterAiBranch branch = actor?.Brain?.bestAction?.actionset?.Branch
            ?? actor?.Blackboard?.CurrentBranch
            ?? CharacterAiBranch.None;
        role = branch switch
        {
            CharacterAiBranch.Rest => FacilityRole.Rest,
            CharacterAiBranch.Toilet => FacilityRole.Toilet,
            CharacterAiBranch.Hygiene => FacilityRole.Hygiene,
            CharacterAiBranch.LeisureVisit => FacilityRole.Entertainment,
            _ => FacilityRole.None
        };
        if (role != FacilityRole.None)
        {
            return true;
        }

        FacilityRole authored = visit.facility?.Facility?.roles
            ?? FacilityRole.None;
        if (authored == FacilityRole.Toilet
            || authored == FacilityRole.Hygiene
            || authored == FacilityRole.Rest
            || authored == FacilityRole.Entertainment)
        {
            role = authored;
            return true;
        }
        return false;
    }

    private int GetFacilityVisitCount(FacilityRole role) =>
        facilityVisitCounts.TryGetValue(role, out int count) ? count : 0;

    private string DescribePrimitiveSurvivalCompletion(
        CharacterPrimitiveSurvivalCompletedEvent completed)
    {
        CharacterActor actor = observations.Keys.FirstOrDefault(candidate =>
            candidate != null
            && CharacterPersistentIdentity.Require(candidate)
                .Equals(completed.CharacterId));
        FacilityRole role = completed.ActionId switch
        {
            "survival:primitive-latrine" => FacilityRole.Toilet,
            "survival:bucket-wash" => FacilityRole.Hygiene,
            "survival:floor-rest" => FacilityRole.Rest,
            "survival:field-meal" => FacilityRole.Meal,
            _ => FacilityRole.None
        };
        string needs = actor?.Stats == null
            ? "missing"
            : string.Join(
                "/",
                Enum.GetValues(typeof(CharacterCondition))
                    .Cast<CharacterCondition>()
                    .Select(condition => actor.Stats.TryGetConditionValue(
                            condition,
                            out float value)
                        ? $"{condition}:{value:0.##}"
                        : $"{condition}:N/A"));
        string facilities = role == FacilityRole.None
            ? "not-applicable"
            : string.Join(
                " | ",
                UnityEngine.Object.FindObjectsByType<BuildableObject>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.InstanceID)
                    .Where(building => building != null
                        && building.SupportsFacilityRole(role))
                    .Select(building =>
                    {
                        bool canVisit = building.CanVisit(
                            actor?.BuildingVisitor,
                            out string visitReason);
                        bool canQueue = building.CanQueueVisit(
                            actor?.BuildingVisitor,
                            out string queueReason);
                        return $"{building.name}@{building.centerPos}"
                            + $":users={building.CurrentUserCount}"
                            + $":reservations={building.ActiveVisitReservationCount}"
                            + $":capacity={building.EffectiveCapacity}"
                            + $":visit={canVisit}({Compact(visitReason)})"
                            + $":queue={canQueue}({Compact(queueReason)})";
                    }));
        AIBrain brain = actor?.Brain;
        return $"time={gameClock?.Time ?? Time.time:0.###}; "
            + $"actor={actor?.name ?? completed.CharacterId.Value}; "
            + $"action={completed.ActionId}; recovery={completed.NeedRecovery:0.###}; "
            + $"branch={brain?.bestAction?.actionset?.Branch ?? actor?.Blackboard?.CurrentBranch}; "
            + $"phase={brain?.CurrentActionPhase}; pathDeferred={brain?.IsPathSearchDeferred}; "
            + $"needs=[{needs}]; facilities=[{facilities}]";
    }

    private string DescribePrimitiveSurvivalStart(
        CharacterPrimitiveSurvivalStartedEvent started)
    {
        CharacterActor actor = observations.Keys.FirstOrDefault(candidate =>
            candidate != null
            && CharacterPersistentIdentity.Require(candidate)
                .Equals(started.CharacterId));
        FacilityRole role = GetPrimitiveFacilityRole(started.ActionId);
        GridPathSearchResult search = actor?.Brain?.GetPathSearch(actor);
        bool immediate = role != FacilityRole.None
            && search != null
            && FacilityCandidateScorer.HasCandidate(actor, search, role);
        bool queueable = role != FacilityRole.None
            && search != null
            && FacilityCandidateScorer.HasReachableQueueableCandidate(
                actor,
                search,
                role);
        return $"START time={gameClock?.Time ?? Time.time:0.###}; "
            + $"actor={actor?.name ?? started.CharacterId.Value}; "
            + $"action={started.ActionId}; emergency={started.Emergency}; "
            + $"need={started.NeedValue:0.###}; position={actor?.GetNowXY()}; "
            + $"pathNull={search == null}; pathDeferred={actor?.Brain?.IsPathSearchDeferred}; "
            + $"immediate={immediate}; queueable={queueable}; "
            + DescribeFacilities(actor, role);
    }

    private static FacilityRole GetPrimitiveFacilityRole(string actionId) =>
        actionId switch
        {
            "survival:primitive-latrine" => FacilityRole.Toilet,
            "survival:bucket-wash" => FacilityRole.Hygiene,
            "survival:floor-rest" => FacilityRole.Rest,
            "survival:field-meal" => FacilityRole.Meal,
            _ => FacilityRole.None
        };

    private static string DescribeFacilities(
        CharacterActor actor,
        FacilityRole role)
    {
        if (role == FacilityRole.None)
        {
            return "facilities=not-applicable";
        }

        return "facilities=[" + string.Join(
            " | ",
            UnityEngine.Object.FindObjectsByType<BuildableObject>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.InstanceID)
                .Where(building => building != null
                    && building.SupportsFacilityRole(role))
                .Select(building =>
                {
                    bool canVisit = building.CanVisit(
                        actor?.BuildingVisitor,
                        out string visitReason);
                    bool canQueue = building.CanQueueVisit(
                        actor?.BuildingVisitor,
                        out string queueReason);
                    return $"{building.name}@{building.centerPos}"
                        + $":users={building.CurrentUserCount}"
                        + $":reservations={building.ActiveVisitReservationCount}"
                        + $":capacity={building.EffectiveCapacity}"
                        + $":visit={canVisit}({Compact(visitReason)})"
                        + $":queue={canQueue}({Compact(queueReason)})";
                })) + "]";
    }

    private void ValidateDailyFacilityCadence(
        string label,
        float visitsPerActorDay,
        float minimum,
        float maximum)
    {
        if (visitsPerActorDay < minimum || visitsPerActorDay > maximum)
        {
            failures.Add(
                $"Completed {label} facility uses averaged {visitsPerActorDay:0.###} per actor-day; expected {minimum:0.###}~{maximum:0.###}.");
        }
    }

    private bool TryCreateNeutralRoutineFixture(
        DungeonRuntimeLifetimeScope scope,
        IReadOnlyList<CharacterActor> actors,
        out string message)
    {
        message = string.Empty;
        CharacterActor anchor = actors?.FirstOrDefault(value => value != null);
        GridSystemManager gridSystem = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>();
        fixtureGrid = gridSystem != null ? gridSystem.grid : null;
        if (anchor == null || fixtureGrid == null || workOrders == null)
        {
            message = "actor, grid, or work-order runtime unavailable";
            return false;
        }

        NormalizeEnvironmentalField(actors);

        Vector2Int origin = anchor.GetNowXY();
        Vector2Int[] reachableInterior = fixtureGrid.SearchPath(origin)
            .GetReachablePositions()
            .Where(value => fixtureGrid.IsValidGridPos(value)
                && fixtureGrid.IsWalkable(value)
                && fixtureGrid.GetGridCell(value)?.AreaType
                    == GridCellAreaType.DungeonInterior)
            .Distinct()
            .ToArray();
        if (!TryResolveCompactTwoRowLayout(
                reachableInterior,
                origin,
                fixtureGrid,
                out Vector2Int[] actorPositions,
                out Vector2Int[] facilityPositions,
                out Vector2Int constructionPosition))
        {
            message = "no same-floor compact interior fixture layout; "
                + DescribeFloorPlacementCapacity(reachableInterior, fixtureGrid);
            return false;
        }
        fixtureActorPositions = actorPositions.ToArray();
        fixtureConstructionAccessPosition = reachableInterior
            .Where(value => value.y == constructionPosition.y
                && Mathf.Abs(value.x - constructionPosition.x)
                + Mathf.Abs(value.y - constructionPosition.y) == 1)
            .OrderBy(value => Mathf.Abs(value.x - origin.x)
                + Mathf.Abs(value.y - origin.y))
            .First();
        if (fixtureActorPositions.Length != 3
            || fixtureActorPositions.Distinct().Count() != 3)
        {
            message = "compact fixture did not provide three distinct founder cells";
            return false;
        }
        fixtureWaterPosition = actorPositions.Length > 1
            ? actorPositions[1]
            : actorPositions[0];

        for (int index = 0; index < actors.Count; index++)
        {
            CharacterActor actor = actors[index];
            if (actor == null)
            {
                continue;
            }
            actor.transform.position = fixtureGrid.GetWorldPos(actorPositions[index]);
            actor.GetComponent<AbilityHaul>()?.StopHauling(
                "neutral daily-routine fixture excludes onboarding redistribution");
            AbilityWork work = actor.GetComponent<AbilityWork>();
            if (work != null)
            {
                work.SetDutyState(AbilityWork.DutyState.OnDuty);
                foreach (WorkTypeDefinition definition in WorkTypeCatalog.All)
                {
                    work.SetWorkPriority(
                        definition.WorkTypeId,
                        WorkPriorityLevel.Off);
                }
            }
        }

        string[] facilityCodes =
        {
            "D04",
            "R01",
            "H01",
            "H03",
            "R04"
        };
        GridLayer[] fixtureFacilityLayers =
        {
            GridLayer.Building,
            GridLayer.WallFixture,
            GridLayer.CeilingFixture,
            GridLayer.FloorOverlay,
            GridLayer.Utility
        };
        Facility mealFacility = null;
        for (int index = 0; index < facilityCodes.Length; index++)
        {
            BuildingSO definition = FindFacilityDefinition(facilityCodes[index]);
            if (definition == null)
            {
                message = "missing facility definition: " + facilityCodes[index];
                return false;
            }
            BuildingSO runtimeDefinition = UnityEngine.Object.Instantiate(definition);
            runtimeDefinition.width = 1;
            runtimeDefinition.height = 1;
            // The live starting dungeon has very few empty lateral building
            // cells on any single floor. Test-only visitor fixtures share one
            // walkable service tile across independent occupancy layers so the
            // cadence run never smuggles cross-floor travel into daily needs.
            runtimeDefinition.layer = fixtureFacilityLayers[index];
            // This verifier measures service cadence in a compact neutral layout,
            // not room-construction validity. Production assets keep their room
            // requirement; the isolated clone removes it so every authored service
            // channel can be observed without a second room-system fixture.
            runtimeDefinition.AbilityModules.Remove<BuildingRoomRequirementAbility>();
            if (index == 0 && runtimeDefinition.Facility != null)
            {
                runtimeDefinition.Facility.capacity = 3;
                BuildingServiceHubAbility serviceHub =
                    runtimeDefinition.GetAbility<BuildingServiceHubAbility>();
                if (serviceHub != null)
                {
                    serviceHub.baseCapacity = 3;
                }
            }
            temporaryDefinitions.Add(runtimeDefinition);

            GameObject facilityObject = new($"QA_DailyRoutine_Facility_{index + 1}");
            temporaryObjects.Add(facilityObject);
            Facility facility = facilityObject.AddComponent<Facility>();
            InjectGameObject(scope, facilityObject);
            // Runtime-created verifier fixtures do not pass through the scene
            // factory that normally performs this base-class injection.  A
            // deprivation breakdown may legitimately damage a fixture, so the
            // test object must carry the same rule port as production objects.
            facility.ConstructDebugRules(DisabledDungeonDebugRuleQuery.Instance);
            facility.SetGrid(fixtureGrid);
            Vector2Int facilityPosition = facilityPositions[index];
            facility.Initialization(runtimeDefinition, facilityPosition);
            facilityObject.transform.position = fixtureGrid.GetWorldPos(facilityPosition);
            if (!fixtureGrid.RegisterOccupant(
                    facility,
                    runtimeDefinition.layer,
                    runtimeDefinition.GetGridPosList(facilityPosition),
                    false))
            {
                message = "facility grid registration failed: " + facilityCodes[index];
                return false;
            }
            if (index == 0)
            {
                mealFacility = facility;
                fixtureMealFacility = facility;
            }
        }

        if (mealFacility == null)
        {
            message = "meal facility was not created";
            return false;
        }
        if (facilityPositions.Any(position => !fixtureGrid.IsWalkable(position)))
        {
            message = "stacked same-floor visitor fixture blocked its service tile";
            return false;
        }
        fixtureConstructionDefinition = ScriptableObject.CreateInstance<BuildingSO>();
        fixtureConstructionDefinition.id = 9201;
        fixtureConstructionDefinition.objectName = "QA neutral routine long construction";
        fixtureConstructionDefinition.width = 1;
        fixtureConstructionDefinition.height = 1;
        fixtureConstructionDefinition.layer = GridLayer.Building;
        fixtureConstructionDefinition.category = BuildingCategory.Shop;
        fixtureConstructionDefinition.unlocked = true;
        fixtureConstructionDefinition.ConfigureGameplayExecution(
            FacilityUseClassification.Logistics,
            ResearchFacilityCommandKind.None);
        BuildingWorkAmountAbility workAmount = new()
        {
            constructionWorkRequired = 100000f,
            repairWorkRequired = 3f,
            cleanWorkRequired = 2f,
            researchWorkRequired = 6f
        };
        workAmount.SetConstructionProjectScale(ProjectScale.IndustrialFacility);
        workAmount.SetConstructionMaterials(new[]
        {
            new ItemAmountDefinition("material:lumber", 1)
        });
        fixtureConstructionDefinition.AbilityModules.Add(workAmount);

        GameObject siteObject = new("QA_DailyRoutine_LongConstructionSite");
        temporaryObjects.Add(siteObject);
        fixtureConstructionSite = siteObject.AddComponent<ConstructionSite>();
        InjectGameObject(scope, siteObject);
        fixtureConstructionSite.ConstructDebugRules(
            DisabledDungeonDebugRuleQuery.Instance);
        fixtureConstructionSite.SetGrid(fixtureGrid);
        fixtureConstructionSite.Initialization(
            fixtureConstructionDefinition,
            constructionPosition);
        siteObject.transform.position = fixtureGrid.GetWorldPos(constructionPosition);
        bool registered = fixtureGrid.RegisterOccupant(
            fixtureConstructionSite,
            GridLayer.Construction,
            fixtureConstructionDefinition.GetGridPosList(constructionPosition),
            false);
        string failureReason = string.Empty;
        bool orderCreated = false;
        if (registered)
        {
            fixtureConstructionDefinition.width = 3;
            fixtureConstructionDefinition.height = 3;
            try
            {
                orderCreated = workOrders.TryCreateConstructionOrder(
                    fixtureConstructionSite,
                    fixtureConstructionDefinition,
                    constructionPosition,
                    out fixtureConstructionOrderId,
                    out failureReason);
            }
            finally
            {
                fixtureConstructionDefinition.width = 1;
                fixtureConstructionDefinition.height = 1;
            }
        }
        if (!registered || !orderCreated)
        {
            message = registered ? failureReason : "construction-site registration failed";
            return false;
        }
        fixtureConstructionSite.ConfigureSite(
            fixtureConstructionOrderId,
            () => true,
            () => { });

        message = "neutral 22C/clean/lit environmental field, three founders placed on distinct interior hallway cells, three local meal slots plus four other live need facilities, "
            + "measured-day consumables deferred until after warmup, onboarding redistribution disabled, and a physically supplied industrial construction order";
        return true;
    }

    private IEnumerator VerifyRecreationSelectionPreflight(
        IReadOnlyList<CharacterActor> actors)
    {
        CharacterActor actor = actors?.FirstOrDefault(value =>
            value != null && !value.IsDead && value.Brain != null
            && value.Stats != null);
        if (actor == null)
        {
            failures.Add("Recreation preflight has no live actor.");
            yield break;
        }

        float originalFun = actor.Stats.GetConditionValue(
            CharacterCondition.FUN,
            100f);
        try
        {
            actor.stats[CharacterCondition.FUN] = 50f;
            actor.Brain.ClearPathSearchCache();
            RecreationJobGiver giver = new();
            bool evaluated = false;
            CharacterAiJobCandidate candidate = default;
            float deadline = Time.realtimeSinceStartup + 2f;
            do
            {
                evaluated = giver.TryEvaluate(actor, out candidate);
                bool facilityLookupDeferred = !evaluated
                    && string.Equals(
                        candidate.Reason,
                        "required facility role unavailable",
                        StringComparison.Ordinal);
                if (evaluated
                    || (!candidate.ActionCandidate.Failure.IsDeferred
                        && !facilityLookupDeferred))
                {
                    break;
                }
                if (facilityLookupDeferred)
                {
                    // The authored-role cache and the actor-specific candidate
                    // shortlist rebuild on bounded frame budgets. Drive the
                    // same lookup the live AI will retry instead of declaring a
                    // transient cache rebuild to be a missing facility.
                    FacilityCandidateScorer.HasUsableCandidate(
                        actor,
                        FacilityRole.Entertainment);
                }
                yield return null;
            }
            while (Time.realtimeSinceStartup < deadline);
            bool rolePresent = FacilityCandidateScorer.HasCandidate(
                actor,
                null,
                FacilityRole.Entertainment);
            bool usable = FacilityCandidateScorer.HasUsableCandidate(
                actor,
                FacilityRole.Entertainment);
            AIAction[] recreationActions = actor.Brain.availableActions?
                .Where(action => action?.actionset?.Branch
                    == CharacterAiBranch.LeisureVisit)
                .ToArray()
                ?? Array.Empty<AIAction>();
            bool preparedDirectly = false;
            BuildableObject directlyPreparedDestination = null;
            AIActionFailure directPreparationFailure = AIActionFailure.None;
            if (recreationActions.Length > 0)
            {
                preparedDirectly = recreationActions[0].actionset
                    .TryPrepareCandidate(
                        actor,
                        null,
                        out directlyPreparedDestination,
                        out directPreparationFailure);
            }
            string actionDiagnostics = recreationActions.Length == 0
                ? "none"
                : string.Join(",", recreationActions.Select(action =>
                {
                    AIActionSet actionSet = action.actionset;
                    float considerationScore = actionSet.considerations == null
                        || actionSet.considerations.Length == 0
                        ? 1f
                        : actionSet.considerations
                            .Where(value => value != null)
                            .Select(value => value.ScoreConsideration(actor))
                            .DefaultIfEmpty(0f)
                            .Aggregate(1f, (total, value) => total * value);
                    float finalActionScore = action.CalculateScore(
                        actor,
                        CharacterAiDecisionContext.Capture(
                            actor,
                            CharacterAiBranch.LeisureVisit));
                    float personality = CharacterAiPersonalityUtility
                        .GetActionScoreMultiplier(actor, actionSet);
                    return $"{actionSet.GetType().Name}:canStart={actionSet.CanStart(actor)}:consideration={considerationScore:0.###}:personality={personality:0.###}:final={finalActionScore:0.###}:role={(actionSet is AIFacilityRoleAction facilityAction ? facilityAction.Role : FacilityRole.None)}";
                }));
            bool valid = evaluated
                && candidate.IsValid
                && candidate.ActionCandidate.ActionSet != null
                && candidate.ActionCandidate.ActionSet.Branch
                    == CharacterAiBranch.LeisureVisit
                && candidate.ActionCandidate.Destination != null
                && candidate.ActionCandidate.Destination.SupportsFacilityRole(
                    FacilityRole.Entertainment);
            string diagnostic =
                $"actor={actor.name}; rolePresent={rolePresent}; usable={usable}; "
                + $"evaluated={evaluated}; valid={candidate.IsValid}; "
                + $"domain={candidate.DomainScore:0.###}; "
                + $"action={candidate.ActionCandidate.Score:0.###}; "
                + $"utility={candidate.Utility:0.###}; "
                + $"destination={candidate.ActionCandidate.Destination?.name ?? "none"}; "
                + $"directPrepared={preparedDirectly}; "
                + $"directDestination={directlyPreparedDestination?.name ?? "none"}; "
                + $"directFailure={directPreparationFailure.Kind}:{Compact(directPreparationFailure.ToString())}; "
                + $"selectorFailure={candidate.ActionCandidate.Failure.Kind}:{Compact(candidate.ActionCandidate.Failure.ToString())}; "
                + $"pending={actor.Brain.IsActionScoringPending}; "
                + $"available={recreationActions.Length}[{actionDiagnostics}]; "
                + $"reason={Compact(candidate.Reason)}";
            if (!valid)
            {
                failures.Add("Recreation selection preflight failed: "
                    + diagnostic);
            }
            else
            {
                warnings.Add("Recreation selection preflight: " + diagnostic);
            }
            Debug.Log("[DailyRoutineWu] Recreation preflight " + diagnostic);
        }
        finally
        {
            actor.stats[CharacterCondition.FUN] = originalFun;
            actor.Brain.ClearPathSearchCache();
            actor.Brain.RequestImmediateReplan(clearFailures: true);
        }
    }

    private static bool TryResolveCompactTwoRowLayout(
        IReadOnlyList<Vector2Int> reachable,
        Vector2Int origin,
        Grid grid,
        out Vector2Int[] actorPositions,
        out Vector2Int[] facilityPositions,
        out Vector2Int constructionPosition)
    {
        actorPositions = Array.Empty<Vector2Int>();
        facilityPositions = Array.Empty<Vector2Int>();
        constructionPosition = default;
        int[] candidateFloors = (reachable ?? Array.Empty<Vector2Int>())
            .Select(value => value.y)
            .Distinct()
            .OrderBy(value => Mathf.Abs(value - origin.y))
            .ThenBy(value => value)
            .ToArray();
        foreach (int floor in candidateFloors)
        {
            if (TryResolveCompactLayoutOnFloor(
                    reachable,
                    origin,
                    floor,
                    grid,
                    out actorPositions,
                    out facilityPositions,
                    out constructionPosition))
            {
                return true;
            }
        }
        return false;
    }

    private static string DescribeFloorPlacementCapacity(
        IReadOnlyList<Vector2Int> reachable,
        Grid grid)
    {
        return string.Join(
            "; ",
            (reachable ?? Array.Empty<Vector2Int>())
                .GroupBy(value => value.y)
                .OrderBy(group => group.Key)
                .Select(group =>
                {
                    Vector2Int[] lateral = group
                        .SelectMany(value => new[]
                        {
                            value,
                            value + Vector2Int.left,
                            value + Vector2Int.right
                        })
                        .Distinct()
                        .ToArray();
                    int placeable = lateral.Count(value =>
                        grid.GetGridCell(value)?.AreaType
                            == GridCellAreaType.DungeonInterior
                        && grid.GetGridCell(value)?.CanOccupy(
                            GridLayer.Building) == true);
                    return $"floor={group.Key}:reachable={group.Count()}:lateralPlaceable={placeable}";
                }));
    }

    private static bool TryResolveCompactLayoutOnFloor(
        IReadOnlyList<Vector2Int> reachable,
        Vector2Int origin,
        int floor,
        Grid grid,
        out Vector2Int[] actorPositions,
        out Vector2Int[] facilityPositions,
        out Vector2Int constructionPosition)
    {
        actorPositions = Array.Empty<Vector2Int>();
        facilityPositions = Array.Empty<Vector2Int>();
        constructionPosition = default;
        Vector2Int[] directions =
        {
            Vector2Int.zero,
            Vector2Int.left,
            Vector2Int.right
        };
        List<(Vector2Int access, Vector2Int placement)> candidates = new();
        foreach (Vector2Int access in reachable ?? Array.Empty<Vector2Int>())
        {
            if (access.y != floor)
            {
                continue;
            }
            foreach (Vector2Int direction in directions)
            {
                Vector2Int placement = access + direction;
                GridCell cell = grid.GetGridCell(placement);
                if (placement.y != floor
                    || cell?.AreaType != GridCellAreaType.DungeonInterior
                    || !cell.CanOccupy(GridLayer.Building)
                    || candidates.Any(value => value.placement == placement))
                {
                    continue;
                }
                candidates.Add((access, placement));
            }
        }
        candidates = candidates
            .OrderBy(value => Mathf.Abs(value.access.x - origin.x)
                + Mathf.Abs(value.access.y - origin.y))
            .ThenBy(value => value.placement.y)
            .ThenBy(value => value.placement.x)
            .ToList();
        if (candidates.Count < 2)
        {
            return false;
        }

        GridLayer[] requiredFacilityLayers =
        {
            GridLayer.Building,
            GridLayer.WallFixture,
            GridLayer.CeilingFixture,
            GridLayer.FloorOverlay,
            GridLayer.Utility
        };
        (Vector2Int access, Vector2Int placement) facility = candidates
            .Where(value => value.access == value.placement)
            .FirstOrDefault(value => requiredFacilityLayers.All(layer =>
                grid.GetGridCell(value.placement)?.CanOccupy(layer) == true));
        if (facility == default)
        {
            facility = candidates.FirstOrDefault(value =>
                requiredFacilityLayers.All(layer =>
                    grid.GetGridCell(value.placement)?.CanOccupy(layer) == true));
        }
        if (facility == default)
        {
            return false;
        }

        (Vector2Int access, Vector2Int placement) construction = candidates
            .FirstOrDefault(value =>
                value.access != value.placement
                && value.placement != facility.placement
                && grid.GetGridCell(value.placement)?
                    .CanOccupy(GridLayer.Construction) == true);
        if (construction == default)
        {
            return false;
        }
        Vector2Int[] distinctAccess = (reachable ?? Array.Empty<Vector2Int>())
            .Where(value => value.y == floor)
            .OrderBy(value => Mathf.Abs(value.x - facility.access.x))
            .ThenBy(value => value.x)
            .Distinct()
            .Take(3)
            .ToArray();
        if (distinctAccess.Length < 3)
        {
            return false;
        }
        actorPositions = distinctAccess;
        facilityPositions = Enumerable.Repeat(facility.placement, 5).ToArray();
        constructionPosition = construction.placement;
        return actorPositions.All(value => value.y == floor)
            && facilityPositions.All(value => value.y == floor)
            && constructionPosition.y == floor;
    }

    private bool ActivateFixtureWorkOrder(out string failureReason)
    {
        failureReason = string.Empty;
        if (fixtureConstructionSite == null
            || workOrders == null
            || itemStacks == null
            || !workOrders.TryGetOrderFor(
                fixtureConstructionSite,
                BuiltInWorkTypeIds.Construct,
                out WorkOrderProgressState order))
        {
            failureReason = "fixture construction order is unavailable";
            return false;
        }
        if (!itemStacks.SpawnItemAt(
                "material:lumber",
                1,
                fixtureConstructionSite.centerPos,
                WorldItemStackState.FacilityBuffer,
                order.MaterialDestinationId,
                out int spawned)
            || spawned != 1
            || !workOrders.RefreshMaterialsReady(fixtureConstructionSite))
        {
            failureReason = "construction material could not be committed to the live site buffer";
            return false;
        }
        int assignedCount = 0;
        List<string> assignmentFailures = new();
        CharacterActor[] eligibleActors = fixtureActors
            .Where(actor => actor != null
                && actor.gameObject.activeInHierarchy
                && actor.GetComponent<AbilityWork>() != null)
            .OrderBy(
                actor => actor.Identity?.PersistentId ?? actor.name,
                StringComparer.Ordinal)
            .ToArray();
        foreach (CharacterActor actor in eligibleActors)
        {
            AbilityWork work = actor.GetComponent<AbilityWork>();
            work.SetWorkPriority(
                BuiltInWorkTypeIds.Construct,
                WorkPriorityLevel.Priority1);
            GridPathSearchResult search = fixtureGrid.SearchPath(actor.GetNowXY());
            if (work.TrySetPriorityWorkTarget(
                    fixtureConstructionSite,
                    BuiltInWorkTypeIds.Construct,
                    search,
                    out string assignmentFailure)
                || work.TrySetPriorityWorkTarget(
                    fixtureConstructionSite,
                    BuiltInWorkTypeIds.Construct,
                    search,
                    out assignmentFailure))
            {
                actor.Brain?.RequestImmediateReplan(clearFailures: true);
                assignedCount++;
                continue;
            }
            assignmentFailures.Add(
                $"priority construction assignment failed for {actor.name}: {assignmentFailure}");
        }
        if (assignedCount == eligibleActors.Length && assignedCount >= 3)
        {
            return true;
        }

        failureReason = eligibleActors.Length == 0
            ? "no eligible founder could receive the construction order"
            : $"assigned {assignedCount}/{eligibleActors.Length}: "
                + string.Join(" | ", assignmentFailures);
        return false;
    }

    private bool ResetFixtureWorkOrderForMeasuredDay(out string failureReason)
    {
        failureReason = string.Empty;
        if (workOrders == null
            || fixtureConstructionSite == null
            || fixtureConstructionDefinition == null)
        {
            failureReason = "fixture construction authority is unavailable";
            return false;
        }

        if (!workOrders.TryGetOrderFor(
                fixtureConstructionSite,
                BuiltInWorkTypeIds.Construct,
                out WorkOrderProgressState existingOrder)
            || existingOrder.Status is WorkOrderStatus.Completed
                or WorkOrderStatus.Cancelled)
        {
            failureReason = "warm-up construction order was not preserved for measured-day activation";
            return false;
        }
        return ActivateFixtureWorkOrder(out failureReason);
    }

    private void ObserveFixtureWorkforce()
    {
        if (fixtureConstructionSite == null
            || workOrders is not IConstructionProjectWorkforceRuntime workforce
            || !workforce.TryCaptureConstructionProject(
                fixtureConstructionSite,
                out ProjectWorkforceSnapshot snapshot))
        {
            return;
        }

        capturedFixtureWorkforce = true;
        latestFixtureWorkforce = snapshot;
        peakFixtureActiveWorkers = Math.Max(
            peakFixtureActiveWorkers,
            snapshot.ActiveWorkers);
        peakFixtureEffectiveWorkers = Mathf.Max(
            peakFixtureEffectiveWorkers,
            snapshot.EffectiveWorkerCount);
    }

    private void PrepareFixtureWorkOrderReset()
    {
        foreach (CharacterActor actor in fixtureActors)
        {
            AbilityWork work = actor?.GetComponent<AbilityWork>();
            if (work == null)
            {
                continue;
            }

            work.ClearPriorityWorkTarget();
            actor.Brain?.StopCurrentActionForReplan(
                "measured-day-project-reset");
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
        }
    }

    private void RestoreFixtureActorPositions(
        IReadOnlyList<CharacterActor> actors)
    {
        if (fixtureGrid == null || fixtureActorPositions.Length == 0)
        {
            return;
        }

        int index = 0;
        foreach (CharacterActor actor in actors
                     .Where(value => value != null && !value.IsDead)
                     .OrderBy(
                         value => value.Identity?.PersistentId ?? value.name,
                         StringComparer.Ordinal))
        {
            Vector2Int position = fixtureActorPositions[
                index % fixtureActorPositions.Length];
            actor.transform.position = fixtureGrid.GetWorldPos(position);
            actor.Brain?.ClearPathSearchCache();
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
            index++;
        }
    }

    private string SeedAccessibleStarterConsumables(
        Vector2Int waterPosition)
    {
        if (itemStacks == null)
        {
            throw new InvalidOperationException("World-item runtime is unavailable.");
        }

        string foodId = itemDefinitions?.All
            .Where(value => value != null
                && value.TryGetFeature(out FoodItemFeature food)
                && food.preserved
                && food.freshnessSeconds >= ObservationGameSeconds + 60f
                && food.servingRole is MealServingRole.FullMeal
                    or MealServingRole.FieldRation
                && food.nutrition >= 35f)
            .OrderByDescending(value => value.GetFeatureOrDefault<FoodItemFeature>()?.nutrition)
            .ThenBy(value => value.ItemId, StringComparer.Ordinal)
            .Select(value => value.ItemId)
            .FirstOrDefault();
        string waterId = itemStacks.GetAllStacks()
            .Where(value => value != null
                && value.AvailableQuantity > 0
                && value.StockCategory == StockCategory.Water)
            .Select(value => value.ItemId)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(foodId)
            || string.IsNullOrWhiteSpace(waterId))
        {
            throw new InvalidOperationException(
                $"Starter consumables missing: food='{foodId}', water='{waterId}'.");
        }

        foreach (WorldItemStackSnapshot starterConsumable in itemStacks.GetAllStacks()
                     .Where(value => value != null
                         && value.StockCategory is StockCategory.Food
                             or StockCategory.Water)
                     .ToArray())
        {
            itemStacks.DeleteStack(starterConsumable.StackId);
        }

        int foodSpawned = 0;
        int waterSpawned = 0;
        BuildableObject[] mealFacilities = (worldRegistry?.Buildings
                ?? Array.Empty<BuildableObject>())
            .Where(value => value != null
                && !value.isDestroy
                && value.SupportsFacilityRole(FacilityRole.Meal))
            .OrderBy(value => value.RequirePersistentInstanceId().Value,
                StringComparer.Ordinal)
            .ToArray();
        foreach (BuildableObject facility in mealFacilities)
        {
            if (!itemStacks.SpawnItemAt(
                    foodId,
                    30,
                    facility.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    $"facility-input:meal:{facility.RequirePersistentInstanceId().Value}",
                    out int spawned))
            {
                throw new InvalidOperationException(
                    $"Meal buffer seeding failed for '{facility.name}'.");
            }
            foodSpawned += spawned;
        }
        if (mealFacilities.Length == 0
            || foodSpawned != mealFacilities.Length * 30
            || !itemStacks.SpawnItemAt(
                waterId,
                30,
                waterPosition,
                WorldItemStackState.Loose,
                string.Empty,
                out waterSpawned)
            || waterSpawned != 30)
        {
            throw new InvalidOperationException(
                $"Accessible consumable seeding failed: food={foodSpawned}, water={waterSpawned}.");
        }

        fixtureFoodId = foodId;
        fixtureWaterId = waterId;
        fixtureFoodNutrition = itemDefinitions.GetRequired(
                new ItemDefinitionId(foodId))
            .GetFeatureOrDefault<FoodItemFeature>()?.nutrition ?? 0f;
        fixtureFoodStartQuantity = CountFixtureItemQuantity(foodId);
        fixtureWaterStartQuantity = CountFixtureItemQuantity(waterId);

        return $"accessible physical starter food '{foodId}' in {mealFacilities.Length} meal buffers and water '{waterId}'";
    }

    private int CountFixtureItemQuantity(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || itemStacks == null)
        {
            return 0;
        }

        int worldQuantity = itemStacks.GetAllStacks()
                .Where(value => value != null
                    && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
                .Sum(value => value.Quantity);
        int carriedQuantity = carryInventories?.All
            .Where(value => value != null)
            .Sum(value => value.CountItem(itemId)) ?? 0;
        return checked(worldQuantity + carriedQuantity);
    }

    private void NormalizeEnvironmentalField(
        IReadOnlyList<CharacterActor> actors)
    {
        if (environmentalField == null
            || characterEnvironment == null
            || fixtureGrid == null)
        {
            throw new InvalidOperationException(
                "Environmental-field, character-environment persistence, or fixture grid is unavailable.");
        }

        neutralEnvironmentalFieldState = new DungeonEnvironmentalFieldSaveData
        {
            width = fixtureGrid.width,
            height = fixtureGrid.height
        };
        for (int y = 0; y < fixtureGrid.height; y++)
        {
            for (int x = 0; x < fixtureGrid.width; x++)
            {
                neutralEnvironmentalFieldState.cells.Add(new EnvironmentalCellSaveData
                {
                    x = x,
                    y = y,
                    temperatureC = 22f,
                    airQuality = 100f,
                    lightLevel = 100f
                });
            }
        }
        environmentalField.Restore(
            environmentalField.PrepareRestore(neutralEnvironmentalFieldState));
        ResetCharacterEnvironment(actors);
        nextNeutralEnvironmentRefreshAt = (gameClock?.Time ?? Time.time) + 5f;
    }

    private void ResetNeutralNeedsMoodAndDiscontent(
        IReadOnlyList<CharacterActor> actors)
    {
        foreach (CharacterActor actor in actors)
        {
            if (actor?.Stats == null)
            {
                continue;
            }

            Dictionary<CharacterCondition, float> restoredStats =
                actor.Stats.StatSnapshot.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value);
            restoredStats[CharacterCondition.HUNGER] = 85f;
            restoredStats[CharacterCondition.THIRST] = 100f;
            restoredStats[CharacterCondition.SLEEP] = 100f;
            restoredStats[CharacterCondition.FUN] = 80f;
            restoredStats[CharacterCondition.EXCRETION] = 100f;
            restoredStats[CharacterCondition.HYGIENE] = 100f;
            restoredStats[CharacterCondition.MOOD] = 75f;
            actor.Stats.RestorePersistentState(
                restoredStats,
                actor.CurrentHealth,
                actor.InjurySeverity,
                75f,
                Array.Empty<CharacterMoodFactorSnapshot>());

            if (deprivationRuntime == null
                || !deprivationRuntime.DebugResetForDeterministicScenario(actor))
            {
                failures.Add(
                    $"Measured-day deprivation reset failed: actor={actor.Identity?.PersistentId ?? actor.name}.");
            }
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
        }

        staffDiscontent?.RestoreSnapshots(Array.Empty<StaffDiscontentSnapshot>());
    }

    private void MaintainNeutralEnvironment(
        IReadOnlyList<CharacterActor> actors,
        float gameTime)
    {
        if (neutralEnvironmentalFieldState == null
            || gameTime < nextNeutralEnvironmentRefreshAt)
        {
            return;
        }
        environmentalField.Restore(
            environmentalField.PrepareRestore(neutralEnvironmentalFieldState));
        ResetCharacterEnvironment(actors);
        nextNeutralEnvironmentRefreshAt = gameTime + 5f;
    }

    private void ResetCharacterEnvironment(
        IReadOnlyList<CharacterActor> actors)
    {
        DungeonCharacterEnvironmentSaveData characterState =
            characterEnvironment.Capture();
        characterState.exposures = actors
            .Where(actor => actor?.Identity != null)
            .OrderBy(
                actor => actor.Identity.PersistentId,
                StringComparer.Ordinal)
            .Select(actor => new CharacterEnvironmentExposure
            {
                characterId = actor.Identity.PersistentId,
                coldExposure = 0f,
                heatExposure = 0f,
                airborneExposure = 0f,
                visualStrain = 0f,
                physiologicalBand = EnvironmentalExposureBand.Stable,
                visualBand = EnvironmentalExposureBand.Stable,
                criticalDamageTimer = 0f,
                coldWorkCooldownActive = false
            })
            .ToArray();
        characterEnvironment.PublishRestoreCandidate(
            characterEnvironment.BuildRestoreCandidate(characterState));
    }

    private static BuildingSO FindFacilityDefinition(string code)
    {
        foreach (string guid in AssetDatabase.FindAssets(
                     "t:BuildingSO",
                     new[] { "Assets/Resources/SO/Building" }))
        {
            BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (definition != null
                && string.Equals(
                    definition.GetAbility<BuildingFacilityPartAbility>()?.code,
                    code,
                    StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }

    private static void InjectGameObject(
        DungeonRuntimeLifetimeScope scope,
        GameObject target)
    {
        if (scope?.Container == null || target == null)
        {
            return;
        }

        foreach (MonoBehaviour component in target.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component != null)
            {
                scope.Container.Inject(component);
            }
        }
    }

    private void CleanupFixture()
    {
        if (workOrders != null && !string.IsNullOrWhiteSpace(fixtureConstructionOrderId))
        {
            workOrders.CancelOrder(
                fixtureConstructionOrderId,
                refundDeliveredMaterials: false);
        }
        if (fixtureGrid != null
            && fixtureConstructionSite != null
            && fixtureConstructionDefinition != null)
        {
            fixtureGrid.RemoveOccupant(
                fixtureConstructionSite,
                GridLayer.Construction,
                fixtureConstructionDefinition.GetGridPosList(
                    fixtureConstructionSite.centerPos),
                false);
        }
        foreach (GameObject temporaryObject in temporaryObjects)
        {
            if (temporaryObject != null)
            {
                Destroy(temporaryObject);
            }
        }
        temporaryObjects.Clear();
        fixtureActorPositions = Array.Empty<Vector2Int>();
        fixtureMealFacility = null;
        foreach (BuildingSO temporaryDefinition in temporaryDefinitions)
        {
            if (temporaryDefinition != null)
            {
                Destroy(temporaryDefinition);
            }
        }
        temporaryDefinitions.Clear();
        if (fixtureConstructionDefinition != null)
        {
            Destroy(fixtureConstructionDefinition);
        }
    }

    private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type is LogType.Error or LogType.Exception or LogType.Assert)
        {
            capturedIssues.Add($"{type}: {Compact(condition)}");
        }
    }

    private static CharacterActor[] FindActors() =>
        CharacterActorCollection.DistinctByGameObject(
                UnityEngine.Object.FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None))
            .Where(value => value != null
                && value.gameObject.activeInHierarchy)
            .ToArray();

    private static string Compact(string value, int limit = 180)
    {
        string normalized = (value ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        return normalized.Length <= limit
            ? normalized
            : normalized.Substring(0, limit) + "...";
    }

    private sealed class ActorRoutineObservation
    {
        private enum RoutineNeedKind
        {
            None,
            Meal,
            Drink,
            Sleep,
            Toilet,
            Hygiene,
            Recreation
        }

        private readonly CharacterActor actor;
        private readonly NeedSnapshot start;
        private readonly CharacterAiRuntimeDiagnosticsSnapshot diagnosticsStart;
        private readonly Dictionary<CharacterAiBranch, float> branchSeconds =
            new Dictionary<CharacterAiBranch, float>();
        private readonly List<string> stallEvidence = new List<string>(8);
        private readonly Dictionary<RoutineNeedKind, float> needTravelSecondsByKind =
            new Dictionary<RoutineNeedKind, float>();
        private readonly Dictionary<RoutineNeedKind, float> needTravelDistanceByKind =
            new Dictionary<RoutineNeedKind, float>();
        private readonly Dictionary<RoutineNeedKind, int> needTravelSegmentsByKind =
            new Dictionary<RoutineNeedKind, int>();
        private readonly List<string> needTravelEvidence = new List<string>(12);
        private Vector3 lastPosition;
        private bool hasPosition;
        private RoutineNeedKind previousNeedKind;
        private float urgentUnservedSeconds;
        private float movementStallSeconds;
        private float activePhaseStallSeconds;
        private float orphanWorkActionSeconds;
        private float workProgressStallSeconds;
        private float needQueueStallSeconds;
        private float needServiceStallSeconds;
        private float longestHarmfulStallSeconds;
        private int harmfulStallEpisodes;
        private string previousActiveSignature = string.Empty;
        private bool urgentStallReported;
        private bool movementStallReported;
        private bool activePhaseStallReported;
        private bool orphanWorkActionReported;
        private bool workProgressStallReported;
        private bool needQueueStallReported;
        private bool needServiceStallReported;
        private long lastWorkProgressRevision;
        private bool hasWorkProgressRevision;
        private RoutineNeedKind activeTravelKind;
        private float activeTravelSeconds;
        private float activeTravelDistance;
        private Vector2Int activeTravelStart;
        private string activeTravelDestination = string.Empty;

        public ActorRoutineObservation(CharacterActor actor)
        {
            this.actor = actor;
            ActorName = actor != null ? actor.name : "missing";
            start = NeedSnapshot.Capture(actor);
            diagnosticsStart = actor?.Brain?.CaptureRuntimeDiagnostics() ?? default;
        }

        public string ActorName { get; }
        public CharacterActor Actor => actor;
        public float WorkActiveSeconds { get; private set; }
        public float WorkTransitSeconds { get; private set; }
        public float WorkQueueSeconds { get; private set; }
        public float MealSeconds { get; private set; }
        public float DrinkSeconds { get; private set; }
        public float SleepSeconds { get; private set; }
        public float ToiletSeconds { get; private set; }
        public float HygieneSeconds { get; private set; }
        public float RecreationSeconds { get; private set; }
        public float NeedTravelSeconds { get; private set; }
        public float NeedQueueSeconds { get; private set; }
        public float OtherTravelSeconds { get; private set; }
        public float IdleOtherSeconds { get; private set; }
        public float PersonalServiceSeconds => MealSeconds + DrinkSeconds
            + SleepSeconds + ToiletSeconds + HygieneSeconds + RecreationSeconds;
        public float PersonalNeedSeconds => PersonalServiceSeconds
            + NeedTravelSeconds + NeedQueueSeconds;
        public float TotalSeconds => WorkActiveSeconds + WorkTransitSeconds
            + WorkQueueSeconds + PersonalNeedSeconds + OtherTravelSeconds
            + IdleOtherSeconds;
        public int MealVisits { get; private set; }
        public int DrinkVisits { get; private set; }
        public int SleepVisits { get; private set; }
        public int ToiletVisits { get; private set; }
        public int HygieneVisits { get; private set; }
        public int RecreationVisits { get; private set; }
        public long InteractionActionReplacementDelta
        {
            get
            {
                CharacterAiRuntimeDiagnosticsSnapshot end =
                    actor?.Brain?.CaptureRuntimeDiagnostics() ?? default;
                return end.InteractionActionReplacements
                    - diagnosticsStart.InteractionActionReplacements;
            }
        }
        public long OrphanWorkActionRecoveryDelta
        {
            get
            {
                CharacterAiRuntimeDiagnosticsSnapshot end =
                    actor?.Brain?.CaptureRuntimeDiagnostics() ?? default;
                return end.OrphanWorkActionRecoveries
                    - diagnosticsStart.OrphanWorkActionRecoveries;
            }
        }

        public void StabilizeNeutralHealth()
        {
            if (actor != null
                && !actor.IsDead
                && actor.CurrentHealth < actor.MaxHealth)
            {
                actor.Heal(actor.MaxHealth - actor.CurrentHealth);
            }
        }

        public void Observe(float seconds)
        {
            if (actor == null)
            {
                return;
            }
            Vector3 position = actor.transform.position;
            float movedDistance = hasPosition
                ? Vector3.Distance(position, lastPosition)
                : 0f;
            bool moving = movedDistance > .001f;
            hasPosition = true;
            lastPosition = position;

            AbilityWork work = actor.GetComponent<AbilityWork>();
            if (work != null && work.isWorking)
            {
                EndNeedTravelSegment(actor.GetNowXY());
                ResetNoProgressWindows();
                previousNeedKind = RoutineNeedKind.None;
                string workPhase = actor.Brain?.CurrentActionPhase ?? string.Empty;
                ObserveApprovedWorkProgress(
                    seconds,
                    moving,
                    workPhase,
                    work,
                    actor.Brain);
                if (moving || IsMovementPhase(workPhase)
                    || workPhase.Contains("접근", StringComparison.Ordinal)
                    || workPhase.Contains("이탈", StringComparison.Ordinal))
                {
                    WorkTransitSeconds += seconds;
                }
                else if (workPhase.Contains("대기", StringComparison.Ordinal)
                    || workPhase.Contains("예약", StringComparison.Ordinal))
                {
                    WorkQueueSeconds += seconds;
                }
                else
                {
                    WorkActiveSeconds += seconds;
                }
                return;
            }

            ResetWorkProgressWindow();

            AIBrain brain = actor.Brain;
            CharacterAiBranch branch = brain?.bestAction?.actionset?.Branch
                ?? actor.Blackboard?.CurrentBranch
                ?? CharacterAiBranch.None;
            string actionLabel = brain?.CurrentActionDebugLabel ?? string.Empty;
            string actionPhase = brain?.CurrentActionPhase ?? string.Empty;
            AddBranchSeconds(branch, seconds);
            RoutineNeedKind needKind = ResolveNeedKind(branch, actionLabel);
            ObserveHarmfulNoProgress(
                seconds,
                moving,
                work,
                brain,
                branch,
                actionLabel,
                actionPhase,
                needKind);
            if (needKind != RoutineNeedKind.None)
            {
                if (previousNeedKind != needKind)
                {
                    IncrementVisit(needKind);
                }
                previousNeedKind = needKind;
                if (moving || IsMovementPhase(actionPhase))
                {
                    NeedTravelSeconds += seconds;
                    RecordNeedTravel(
                        needKind,
                        seconds,
                        movedDistance,
                        brain?.CurrentDestinationDebugLabel,
                        actor.GetNowXY());
                    return;
                }
                EndNeedTravelSegment(actor.GetNowXY());
                if (IsQueuePhase(actionPhase))
                {
                    NeedQueueSeconds += seconds;
                    return;
                }
                AddNeedService(needKind, seconds);
                return;
            }
            EndNeedTravelSegment(actor.GetNowXY());
            previousNeedKind = RoutineNeedKind.None;
            switch (branch)
            {
                case CharacterAiBranch.Work:
                case CharacterAiBranch.DutyWork:
                    if (moving || IsMovementPhase(actionPhase))
                    {
                        WorkTransitSeconds += seconds;
                    }
                    else
                    {
                        WorkQueueSeconds += seconds;
                    }
                    break;
                default:
                    if (moving)
                    {
                        OtherTravelSeconds += seconds;
                    }
                    else
                    {
                        IdleOtherSeconds += seconds;
                    }
                    break;
            }
        }

        private static RoutineNeedKind ResolveNeedKind(
            CharacterAiBranch branch,
            string actionLabel)
        {
            switch (branch)
            {
                case CharacterAiBranch.Eat:
                    return RoutineNeedKind.Meal;
                case CharacterAiBranch.Drink:
                    return RoutineNeedKind.Drink;
                case CharacterAiBranch.Rest:
                    return RoutineNeedKind.Sleep;
                case CharacterAiBranch.Toilet:
                    return RoutineNeedKind.Toilet;
                case CharacterAiBranch.Hygiene:
                    return RoutineNeedKind.Hygiene;
                case CharacterAiBranch.LeisureVisit:
                case CharacterAiBranch.Shopping:
                case CharacterAiBranch.LookAround:
                    return RoutineNeedKind.Recreation;
                case CharacterAiBranch.RoutineUtility:
                    if (actionLabel.Contains("식수", StringComparison.Ordinal))
                    {
                        return RoutineNeedKind.Drink;
                    }
                    if (actionLabel.Contains("식사", StringComparison.Ordinal)
                        || actionLabel.Contains("야전식", StringComparison.Ordinal))
                    {
                        return RoutineNeedKind.Meal;
                    }
                    if (actionLabel.Contains("수면", StringComparison.Ordinal)
                        || actionLabel.Contains("휴식", StringComparison.Ordinal)
                        || actionLabel.Contains("취침", StringComparison.Ordinal))
                    {
                        return RoutineNeedKind.Sleep;
                    }
                    if (actionLabel.Contains("화장실", StringComparison.Ordinal)
                        || actionLabel.Contains("배변", StringComparison.Ordinal)
                        || actionLabel.Contains("변소", StringComparison.Ordinal))
                    {
                        return RoutineNeedKind.Toilet;
                    }
                    if (actionLabel.Contains("위생", StringComparison.Ordinal)
                        || actionLabel.Contains("세면", StringComparison.Ordinal)
                        || actionLabel.Contains("세척", StringComparison.Ordinal))
                    {
                        return RoutineNeedKind.Hygiene;
                    }
                    return RoutineNeedKind.None;
                default:
                    return RoutineNeedKind.None;
            }
        }

        private void RecordNeedTravel(
            RoutineNeedKind kind,
            float seconds,
            float distance,
            string destination,
            Vector2Int currentPosition)
        {
            if (kind == RoutineNeedKind.None)
            {
                return;
            }

            if (activeTravelKind != kind)
            {
                EndNeedTravelSegment(currentPosition);
                activeTravelKind = kind;
                activeTravelStart = currentPosition;
                activeTravelDestination = OneLine(destination);
                needTravelSegmentsByKind.TryGetValue(kind, out int segments);
                needTravelSegmentsByKind[kind] = segments + 1;
            }

            activeTravelSeconds += Mathf.Max(0f, seconds);
            activeTravelDistance += Mathf.Max(0f, distance);
            needTravelSecondsByKind.TryGetValue(kind, out float priorSeconds);
            needTravelSecondsByKind[kind] = priorSeconds + Mathf.Max(0f, seconds);
            needTravelDistanceByKind.TryGetValue(kind, out float priorDistance);
            needTravelDistanceByKind[kind] = priorDistance + Mathf.Max(0f, distance);
            if (string.IsNullOrWhiteSpace(activeTravelDestination))
            {
                activeTravelDestination = OneLine(destination);
            }
        }

        private void EndNeedTravelSegment(Vector2Int endPosition)
        {
            if (activeTravelKind == RoutineNeedKind.None)
            {
                return;
            }

            if (needTravelEvidence.Count < 12)
            {
                int manhattan = Mathf.Abs(endPosition.x - activeTravelStart.x)
                    + Mathf.Abs(endPosition.y - activeTravelStart.y);
                needTravelEvidence.Add(
                    $"kind={activeTravelKind}; seconds={activeTravelSeconds:0.###}; "
                    + $"worldDistance={activeTravelDistance:0.###}; displacementCells={manhattan}; "
                    + $"start={activeTravelStart}; end={endPosition}; destination={activeTravelDestination}");
            }

            activeTravelKind = RoutineNeedKind.None;
            activeTravelSeconds = 0f;
            activeTravelDistance = 0f;
            activeTravelDestination = string.Empty;
        }

        private static bool IsMovementPhase(string phase) =>
            phase.Contains("이동", StringComparison.Ordinal)
            || phase.Contains("찾는 중", StringComparison.Ordinal);

        private static bool IsQueuePhase(string phase) =>
            phase.Contains("대기", StringComparison.Ordinal)
            || phase.Contains("예약", StringComparison.Ordinal)
            || phase.Contains("확인", StringComparison.Ordinal)
            || phase.Contains("결정", StringComparison.Ordinal);

        private void AddBranchSeconds(CharacterAiBranch branch, float seconds)
        {
            branchSeconds.TryGetValue(branch, out float current);
            branchSeconds[branch] = current + seconds;
        }

        private void ObserveHarmfulNoProgress(
            float seconds,
            bool moving,
            AbilityWork work,
            AIBrain brain,
            CharacterAiBranch branch,
            string actionLabel,
            string actionPhase,
            RoutineNeedKind needKind)
        {
            if (brain == null || work?.isWorking == true)
            {
                ResetNoProgressWindows();
                return;
            }

            // A routine-need interruption is a deliberate one-frame ownership
            // handoff: the executor has stopped applying work but still owns
            // the action until accounting, worker leases and resume intent are
            // finalized. It is not an inactive Work action and must not be
            // classified as a no-progress stall.
            if (branch == CharacterAiBranch.Work
                && work?.IsRoutineNeedInterruptionFinalizing == true)
            {
                ResetNoProgressWindows();
                return;
            }

            bool activeAction = brain.IsExternallyDrivenActionActive
                || (brain.bestAction != null && !brain.isBestActionEnd);
            bool orphanWorkAction = branch == CharacterAiBranch.Work
                && activeAction
                && brain.isExecuted
                && work != null
                && !work.isWorking
                && !work.HasActiveWorkRoutineForDiagnostics
                && !work.HasRoutineNeedWorkBlockForDiagnostics;
            if (orphanWorkAction)
            {
                orphanWorkActionSeconds += seconds;
                longestHarmfulStallSeconds = Mathf.Max(
                    longestHarmfulStallSeconds,
                    orphanWorkActionSeconds);
                if (orphanWorkActionSeconds >= 2f
                    && !orphanWorkActionReported)
                {
                    RecordStall(
                        "orphan-work-action",
                        orphanWorkActionSeconds,
                        "none",
                        CharacterAiBranch.None,
                        branch,
                        actionLabel,
                        actionPhase,
                        brain);
                    orphanWorkActionReported = true;
                }

                // Do not also classify the same ownership defect as a generic
                // inactive phase or movement stall. The dedicated evidence has
                // the work-run and coroutine state needed to find the leak.
                urgentUnservedSeconds = 0f;
                movementStallSeconds = 0f;
                activePhaseStallSeconds = 0f;
                urgentStallReported = false;
                movementStallReported = false;
                activePhaseStallReported = false;
                previousActiveSignature = string.Empty;
                return;
            }

            orphanWorkActionSeconds = 0f;
            orphanWorkActionReported = false;

            bool urgent = NeedSnapshot.Capture(actor).TryGetMostUrgent(
                actor,
                out string urgentNeed,
                out CharacterAiBranch urgentBranch);
            bool servicingNeed = needKind != RoutineNeedKind.None
                && !IsMovementPhase(actionPhase)
                && !IsQueuePhase(actionPhase);
            if (urgent && needKind == RoutineNeedKind.None && !moving)
            {
                urgentUnservedSeconds += seconds;
                longestHarmfulStallSeconds = Mathf.Max(
                    longestHarmfulStallSeconds,
                    urgentUnservedSeconds);
                if (urgentUnservedSeconds >= 12f && !urgentStallReported)
                {
                    RecordStall("urgent-unserved", urgentUnservedSeconds,
                        urgentNeed, urgentBranch, branch, actionLabel,
                        actionPhase, brain);
                    urgentStallReported = true;
                }
            }
            else
            {
                urgentUnservedSeconds = 0f;
                urgentStallReported = false;
            }

            bool expectsPhysicalMovement = IsMovementPhase(actionPhase)
                && branch != CharacterAiBranch.Wait
                && !actionPhase.Contains("갈 곳 찾는 중", StringComparison.Ordinal);
            if (expectsPhysicalMovement && !moving)
            {
                movementStallSeconds += seconds;
                longestHarmfulStallSeconds = Mathf.Max(
                    longestHarmfulStallSeconds,
                    movementStallSeconds);
                if (movementStallSeconds >= 6f && !movementStallReported)
                {
                    RecordStall("movement-not-advancing", movementStallSeconds,
                        urgentNeed, urgentBranch, branch, actionLabel,
                        actionPhase, brain);
                    movementStallReported = true;
                }
            }
            else
            {
                movementStallSeconds = 0f;
                movementStallReported = false;
            }

            string activeSignature = string.Concat(
                ((int)branch).ToString(), "|", actionLabel, "|", actionPhase,
                "|", brain.CurrentDestinationDebugLabel);

            bool waitingForNeedFacility = activeAction
                && needKind != RoutineNeedKind.None
                && IsQueuePhase(actionPhase)
                && !moving;
            if (waitingForNeedFacility
                && string.Equals(activeSignature, previousActiveSignature,
                    StringComparison.Ordinal))
            {
                needQueueStallSeconds += seconds;
                longestHarmfulStallSeconds = Mathf.Max(
                    longestHarmfulStallSeconds,
                    needQueueStallSeconds);
                if (needQueueStallSeconds >= 30f && !needQueueStallReported)
                {
                    RecordStall("need-queue-not-advancing", needQueueStallSeconds,
                        urgentNeed, urgentBranch, branch, actionLabel,
                        actionPhase, brain);
                    needQueueStallReported = true;
                }
            }
            else
            {
                needQueueStallSeconds = 0f;
                needQueueStallReported = false;
            }

            if (activeAction
                && servicingNeed
                && string.Equals(activeSignature, previousActiveSignature,
                    StringComparison.Ordinal))
            {
                needServiceStallSeconds += seconds;
                longestHarmfulStallSeconds = Mathf.Max(
                    longestHarmfulStallSeconds,
                    needServiceStallSeconds);
                float serviceDeadline = GetNeedServiceStallDeadline(needKind);
                if (needServiceStallSeconds >= serviceDeadline
                    && !needServiceStallReported)
                {
                    RecordStall("need-service-not-completing", needServiceStallSeconds,
                        urgentNeed, urgentBranch, branch, actionLabel,
                        actionPhase, brain);
                    needServiceStallReported = true;
                }
            }
            else
            {
                needServiceStallSeconds = 0f;
                needServiceStallReported = false;
            }

            bool activePhaseNotProgressing = activeAction
                && !servicingNeed
                && !IsQueuePhase(actionPhase)
                && !IsMovementPhase(actionPhase)
                && !moving
                && string.Equals(activeSignature, previousActiveSignature,
                    StringComparison.Ordinal);
            if (activePhaseNotProgressing)
            {
                activePhaseStallSeconds += seconds;
                longestHarmfulStallSeconds = Mathf.Max(
                    longestHarmfulStallSeconds,
                    activePhaseStallSeconds);
                if (activePhaseStallSeconds >= 15f && !activePhaseStallReported)
                {
                    RecordStall("active-phase-not-advancing", activePhaseStallSeconds,
                        urgentNeed, urgentBranch, branch, actionLabel,
                        actionPhase, brain);
                    activePhaseStallReported = true;
                }
            }
            else
            {
                activePhaseStallSeconds = 0f;
                activePhaseStallReported = false;
            }
            previousActiveSignature = activeSignature;
        }

        private void ObserveApprovedWorkProgress(
            float seconds,
            bool moving,
            string actionPhase,
            AbilityWork work,
            AIBrain brain)
        {
            long revision = work.ApprovedWorkProgressRevisionForDiagnostics;
            bool phaseCanApplyWork = !moving
                && !IsMovementPhase(actionPhase)
                && !IsQueuePhase(actionPhase)
                && brain?.HasRunningWorkAction == true;
            if (!hasWorkProgressRevision || revision != lastWorkProgressRevision)
            {
                lastWorkProgressRevision = revision;
                hasWorkProgressRevision = true;
                workProgressStallSeconds = 0f;
                workProgressStallReported = false;
                return;
            }

            if (!phaseCanApplyWork)
            {
                workProgressStallSeconds = 0f;
                workProgressStallReported = false;
                return;
            }

            workProgressStallSeconds += seconds;
            longestHarmfulStallSeconds = Mathf.Max(
                longestHarmfulStallSeconds,
                workProgressStallSeconds);
            if (workProgressStallSeconds >= 15f && !workProgressStallReported)
            {
                RecordStall(
                    "work-approved-wu-not-advancing",
                    workProgressStallSeconds,
                    "none",
                    CharacterAiBranch.None,
                    CharacterAiBranch.Work,
                    brain.CurrentActionDebugLabel,
                    actionPhase,
                    brain);
                workProgressStallReported = true;
            }
        }

        private void ResetWorkProgressWindow()
        {
            workProgressStallSeconds = 0f;
            workProgressStallReported = false;
            hasWorkProgressRevision = false;
        }

        private static float GetNeedServiceStallDeadline(RoutineNeedKind kind) =>
            kind switch
            {
                RoutineNeedKind.Sleep => 65f,
                RoutineNeedKind.Recreation => 25f,
                RoutineNeedKind.Toilet => 15f,
                RoutineNeedKind.Hygiene => 15f,
                RoutineNeedKind.Meal => 12f,
                RoutineNeedKind.Drink => 12f,
                _ => 15f
            };

        private void ResetNoProgressWindows()
        {
            urgentUnservedSeconds = 0f;
            movementStallSeconds = 0f;
            activePhaseStallSeconds = 0f;
            orphanWorkActionSeconds = 0f;
            needQueueStallSeconds = 0f;
            needServiceStallSeconds = 0f;
            urgentStallReported = false;
            movementStallReported = false;
            activePhaseStallReported = false;
            orphanWorkActionReported = false;
            needQueueStallReported = false;
            needServiceStallReported = false;
            previousActiveSignature = string.Empty;
        }

        private void RecordStall(
            string kind,
            float duration,
            string urgentNeed,
            CharacterAiBranch urgentBranch,
            CharacterAiBranch branch,
            string actionLabel,
            string actionPhase,
            AIBrain brain)
        {
            harmfulStallEpisodes++;
            if (stallEvidence.Count >= 8) return;
            CharacterBlackboard blackboard = actor?.Blackboard;
            string macro = blackboard?.HasActiveMacroGoal() == true
                ? blackboard.ActiveMacroGoal?.type.ToString()
                : "None";
            string impulse = blackboard?.HasActiveMoodImpulse() == true
                ? $"{blackboard.ActiveMoodImpulse?.type}/{blackboard.ActiveMoodImpulse?.strength:0.###}"
                : "None";
            AbilityWork work = actor?.GetComponent<AbilityWork>();
            string urgentRejection = urgentBranch == CharacterAiBranch.None
                ? "none"
                : brain?.GetCurrentJobGiverEvaluationRejectionSummary(
                    urgentBranch);
            stallEvidence.Add(
                $"kind={kind}; at={duration:0.###}s; urgent={urgentNeed}; urgentJobGiverRejected={OneLine(urgentRejection)}; branch={branch}; action={OneLine(actionLabel)}; phase={OneLine(actionPhase)}; phaseDetail={OneLine(brain?.CurrentActionPhaseDetail)}; destination={OneLine(brain?.CurrentDestinationDebugLabel)}; failure={brain?.LastActionFailure.Kind}; lastJobGiverRejected={OneLine(brain?.CurrentJobGiverEvaluationRejectionSummary)}; selected={OneLine(blackboard?.SelectedJobGiverUtilitySummary)}; route={OneLine(blackboard?.LastDecisionRouteSummary)}; macro={macro}; impulse={impulse}; brainExecuted={brain?.isExecuted}; workActive={work?.isWorking}; workRoutine={work?.HasActiveWorkRoutineForDiagnostics}; workRun={work?.ActiveWorkRunIdForDiagnostics}; routineBlock={work?.HasRoutineNeedWorkBlockForDiagnostics}; routineFinalizing={work?.IsRoutineNeedInterruptionFinalizing}");
        }

        private void AddNeedService(RoutineNeedKind kind, float seconds)
        {
            switch (kind)
            {
                case RoutineNeedKind.Meal:
                    MealSeconds += seconds;
                    break;
                case RoutineNeedKind.Drink:
                    DrinkSeconds += seconds;
                    break;
                case RoutineNeedKind.Sleep:
                    SleepSeconds += seconds;
                    break;
                case RoutineNeedKind.Toilet:
                    ToiletSeconds += seconds;
                    break;
                case RoutineNeedKind.Hygiene:
                    HygieneSeconds += seconds;
                    break;
                case RoutineNeedKind.Recreation:
                    RecreationSeconds += seconds;
                    break;
            }
        }

        private void IncrementVisit(RoutineNeedKind kind)
        {
            switch (kind)
            {
                case RoutineNeedKind.Meal:
                    MealVisits++;
                    break;
                case RoutineNeedKind.Drink:
                    DrinkVisits++;
                    break;
                case RoutineNeedKind.Sleep:
                    SleepVisits++;
                    break;
                case RoutineNeedKind.Toilet:
                    ToiletVisits++;
                    break;
                case RoutineNeedKind.Hygiene:
                    HygieneVisits++;
                    break;
                case RoutineNeedKind.Recreation:
                    RecreationVisits++;
                    break;
            }
        }

        public void Append(StringBuilder report)
        {
            NeedSnapshot end = NeedSnapshot.Capture(actor);
            report.Append("| ").Append(ActorName)
                .Append(" | ").Append(F(WorkActiveSeconds))
                .Append(" | ").Append(F(WorkTransitSeconds))
                .Append(" | ").Append(F(WorkQueueSeconds))
                .Append(" | ").Append(F(MealSeconds))
                .Append(" | ").Append(F(DrinkSeconds))
                .Append(" | ").Append(F(SleepSeconds))
                .Append(" | ").Append(F(ToiletSeconds))
                .Append(" | ").Append(F(HygieneSeconds))
                .Append(" | ").Append(F(RecreationSeconds))
                .Append(" | ").Append(F(NeedTravelSeconds))
                .Append(" | ").Append(F(NeedQueueSeconds))
                .Append(" | ").Append(F(OtherTravelSeconds))
                .Append(" | ").Append(F(IdleOtherSeconds))
                .Append(" | ").Append(F(TotalSeconds))
                .Append(" | ").Append(MealVisits).Append('/')
                .Append(DrinkVisits).Append('/')
                .Append(SleepVisits).Append('/')
                .Append(ToiletVisits).Append('/')
                .Append(HygieneVisits).Append('/')
                .Append(RecreationVisits)
                .Append(" | ").Append(start.Format()).Append(" -> ").Append(end.Format())
                .AppendLine(" |");
        }

        public string DescribeRuntimeState()
        {
            if (actor == null)
            {
                return $"actor={ActorName}; destroyed=true";
            }

            AIBrain brain = actor.Brain;
            CharacterBlackboard blackboard = actor.Blackboard;
            AbilityWork work = actor.GetComponent<AbilityWork>();
            string persistentId = actor.Identity?.PersistentId ?? string.Empty;
            string target = work?.assignedShop != null
                ? $"{work.assignedShop.name}@{work.assignedShop.centerPos}"
                : "none";
            GridSystemManager gridSystem = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>();
            GridCell cell = gridSystem?.grid?.GetGridCell(actor.GetNowXY());
            return $"actor={ActorName}; id={persistentId}; species={actor.SpeciesTag}; role={actor.Role}; dead={actor.IsDead}; health={actor.CurrentHealth:0.###}/{actor.MaxHealth:0.###}; pos={actor.GetNowXY()}; "
                + $"area={cell?.AreaType}; "
                + $"duty={work?.CurrentDutyState}; offDuty={work?.IsOffDuty}; canWork={work?.CanStartWorkAction()}; constructPriority={work?.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Construct)}; rejected={work?.LastRejectedWorkCandidate.FailureReason}; "
                + $"decisionPending={actor.IsAiDecisionPending}; actionEnded={brain?.isBestActionEnd}; external={brain?.IsExternallyDrivenActionActive}; externalOwner={brain?.ExternalIntentOwnerId}; externalKind={brain?.ExternalIntentKind}; externalEpoch={brain?.ExternalIntentEpoch}; externalTransitions={brain?.ExternalIntentTransitionCount}; externalPreemptions={brain?.ExternalIntentPreemptionCount}; externalRejected={brain?.ExternalIntentRejectedCount}; staleCompletions={brain?.ExternalIntentStaleCompletionCount}; branch={brain?.bestAction?.actionset?.Branch ?? actor.Blackboard?.CurrentBranch ?? CharacterAiBranch.None}; action={brain?.CurrentActionDebugLabel}; phase={brain?.CurrentActionPhase}; detail={brain?.CurrentActionPhaseDetail}; destination={brain?.CurrentDestinationDebugLabel}; failure={brain?.LastActionFailure}; "
                + $"workActive={work?.isWorking}; workRoutine={work?.HasActiveWorkRoutineForDiagnostics}; workRun={work?.ActiveWorkRunIdForDiagnostics}; routineBlock={work?.HasRoutineNeedWorkBlockForDiagnostics}; routineFinalizing={work?.IsRoutineNeedInterruptionFinalizing}; brainExecuted={brain?.isExecuted}; workType={work?.AssignedWorkTypeId}; workTarget={target}; "
                + $"decisionRoute={OneLine(blackboard?.LastDecisionRouteSummary)}; selectedUtility={OneLine(blackboard?.SelectedJobGiverUtilitySummary)}; blackboardFailure={OneLine(blackboard?.LastFailureReason)}; trace={OneLine(blackboard?.LastDecisionTrace)}";
        }

        public string DescribeCumulativeDiagnostics()
        {
            CharacterAiRuntimeDiagnosticsSnapshot end =
                actor?.Brain?.CaptureRuntimeDiagnostics() ?? default;
            string branches = string.Join(", ", branchSeconds
                .Where(value => value.Value >= .05f)
                .OrderByDescending(value => value.Value)
                .ThenBy(value => value.Key)
                .Take(10)
                .Select(value => $"{value.Key}:{value.Value:0.###}"));
            return $"actor={ActorName}; {end.FormatDeltaFrom(diagnosticsStart)}; harmfulStalls={harmfulStallEpisodes}; longestHarmfulStall={longestHarmfulStallSeconds:0.###}; branches={branches}";
        }

        public IEnumerable<string> EnumerateStallEvidence() => stallEvidence;

        public string DescribeNeedTravelAttribution()
        {
            EndNeedTravelSegment(actor != null ? actor.GetNowXY() : default);
            string values = string.Join(
                ", ",
                needTravelSecondsByKind
                    .OrderByDescending(value => value.Value)
                    .ThenBy(value => value.Key)
                    .Select(value =>
                    {
                        needTravelDistanceByKind.TryGetValue(
                            value.Key,
                            out float distance);
                        needTravelSegmentsByKind.TryGetValue(
                            value.Key,
                            out int segments);
                        return $"{value.Key}:seconds={value.Value:0.###}:distance={distance:0.###}:segments={segments}";
                    }));
            return $"actor={ActorName}; {values}";
        }

        public IEnumerable<string> EnumerateNeedTravelEvidence() =>
            needTravelEvidence;

        public int HarmfulStallEpisodes => harmfulStallEpisodes;

        private static string F(float value) => value.ToString("0.0");

        private static string OneLine(string value) =>
            string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace('\r', ' ').Replace('\n', ' ');
    }

    private readonly struct NeedSnapshot
    {
        private NeedSnapshot(float hunger, float thirst, float sleep, float fun, float excretion, float hygiene, float mood)
        {
            Hunger = hunger;
            Thirst = thirst;
            Sleep = sleep;
            Fun = fun;
            Excretion = excretion;
            Hygiene = hygiene;
            Mood = mood;
        }

        private float Hunger { get; }
        private float Thirst { get; }
        private float Sleep { get; }
        private float Fun { get; }
        private float Excretion { get; }
        private float Hygiene { get; }
        private float Mood { get; }

        public static NeedSnapshot Capture(CharacterActor actor) => new(
            actor?.Stats?.GetConditionValue(CharacterCondition.HUNGER, 0f) ?? 0f,
            actor?.Stats?.GetConditionValue(CharacterCondition.THIRST, 0f) ?? 0f,
            actor?.Stats?.GetConditionValue(CharacterCondition.SLEEP, 0f) ?? 0f,
            actor?.Stats?.GetConditionValue(CharacterCondition.FUN, 0f) ?? 0f,
            actor?.Stats?.GetConditionValue(CharacterCondition.EXCRETION, 0f) ?? 0f,
            actor?.Stats?.GetConditionValue(CharacterCondition.HYGIENE, 0f) ?? 0f,
            actor?.Stats?.GetConditionValue(CharacterCondition.MOOD, 0f) ?? 0f);

        public string Format() =>
            $"H{Hunger:0}/T{Thirst:0}/S{Sleep:0}/F{Fun:0}/E{Excretion:0}/Y{Hygiene:0}/M{Mood:0}";

        public bool TryGetMostUrgent(
            CharacterActor actor,
            out string summary,
            out CharacterAiBranch branch)
        {
            string selectedSummary = string.Empty;
            CharacterAiBranch selectedBranch = CharacterAiBranch.None;
            if (actor?.Stats == null)
            {
                summary = selectedSummary;
                branch = selectedBranch;
                return false;
            }

            float worstMargin = float.PositiveInfinity;
            bool found = false;
            Evaluate(
                CharacterCondition.HUNGER,
                CharacterAiBranch.Eat,
                Hunger);
            Evaluate(
                CharacterCondition.THIRST,
                CharacterAiBranch.Drink,
                Thirst);
            Evaluate(
                CharacterCondition.SLEEP,
                CharacterAiBranch.Rest,
                Sleep);
            AbilityWork work = actor.GetComponent<AbilityWork>();
            if (work == null || !work.HasUrgentPriorityWork)
            {
                // Priority work intentionally suppresses routine recreation;
                // physiological survival needs remain strict above. Treating
                // this authored policy as an unserved emergency creates a
                // false stall while the actor is productively working.
                Evaluate(
                    CharacterCondition.FUN,
                    CharacterAiBranch.LeisureVisit,
                    Fun);
            }
            Evaluate(
                CharacterCondition.EXCRETION,
                CharacterAiBranch.Toilet,
                Excretion);
            Evaluate(
                CharacterCondition.HYGIENE,
                CharacterAiBranch.Hygiene,
                Hygiene);
            summary = selectedSummary;
            branch = selectedBranch;
            return found;

            void Evaluate(
                CharacterCondition condition,
                CharacterAiBranch conditionBranch,
                float value)
            {
                CharacterNeedResponseProfile response =
                    actor.Stats.GetNeedResponse(condition);
                float margin = value - response.routineStart;
                if (margin > 0f || margin >= worstMargin) return;
                worstMargin = margin;
                selectedSummary =
                    $"{condition}:{value:0.###}/{response.routineStart:0.###}";
                selectedBranch = conditionBranch;
                found = true;
            }
        }
    }
}
#endif
