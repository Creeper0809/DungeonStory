#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Balance;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

[InitializeOnLoad]
public static class PrimitiveStartSurvivalPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/primitive-start-survival-5day-report.txt";
    public const string FocusedReportPath =
        "Artifacts/QA/primitive-survival-focused-report.txt";
    public const string SixAdultOutageReportPath =
        "Artifacts/QA/v27-six-adult-service-outage-playmode.txt";
    public const string PopulationStageReportPath =
        "Artifacts/QA/v27-balance-population-stage-playmode.txt";
    internal const string PopulationStageRequestPath =
        "Temp/v27-balance-population-stage.flag";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";

    static PrimitiveStartSurvivalPlayModeVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall -= TryStartPopulationStagePending;
        EditorApplication.delayCall += TryStartPopulationStagePending;
    }

    [MenuItem("DungeonStory/Debug/QA/Run Primitive Start Survival 5-Day Verification")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("Primitive start survival verification requires PlayMode.");
            return;
        }
        if (UnityEngine.Object.FindFirstObjectByType<PrimitiveStartSurvivalPlayModeRunner>()
            != null)
        {
            Debug.LogWarning("Primitive start survival verification is already running.");
            return;
        }
        CreateRunner(focusedOnly: false);
    }

    [MenuItem("DungeonStory/Debug/QA/Run Primitive Survival Focused Verification")]
    public static void RunFocusedFromMenu()
    {
        PrimitiveStartSurvivalPlayModeRequestRunner.QueueFocused();
    }

    [MenuItem("DungeonStory/V27/Run Six-Adult Service Outage Verification")]
    public static void RunSixAdultOutageFromMenu()
    {
        PrimitiveStartSurvivalPlayModeRequestRunner.QueueSixAdultOutage();
    }

    [MenuItem("DungeonStory/V27/Run Population-Stage Physical Capacity Verification")]
    public static void RunPopulationStagesFromMenu()
    {
        if (PrimitiveStartSurvivalPlayModeRequestRunner.HasPendingDurableRun)
        {
            throw new InvalidOperationException(
                "A focused or six-adult primitive-survival verification is already pending.");
        }
        Directory.CreateDirectory("Temp");
        File.WriteAllText(PopulationStageRequestPath, "run");
        if (!EditorApplication.isPlaying)
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.isDirty && !string.Equals(
                    active.path,
                    GameplayScenePath,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Population-stage verifier refuses to replace a dirty scene.");
            if (!string.Equals(
                    active.path,
                    GameplayScenePath,
                    StringComparison.OrdinalIgnoreCase))
                EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
            return;
        }
        TryStartPopulationStagePending();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapPopulationStage() =>
        TryStartPopulationStagePending();

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode)
            TryStartPopulationStagePending();
    }

    private static void TryStartPopulationStagePending()
    {
        if (!EditorApplication.isPlaying
            || !File.Exists(PopulationStageRequestPath))
            return;
        File.Delete(PopulationStageRequestPath);
        if (UnityEngine.Object.FindFirstObjectByType<PrimitiveStartSurvivalPlayModeRunner>()
            != null)
        {
            Debug.LogWarning("Primitive survival verification is already running.");
            return;
        }

        PrimitiveStartSurvivalPlayModeRunner runner =
            new GameObject("V27 Population-Stage Physical Capacity Verification")
                .AddComponent<PrimitiveStartSurvivalPlayModeRunner>();
        runner.PopulationStageCapacity = true;
    }

    private static void CreateRunner(bool focusedOnly)
    {
        PrimitiveStartSurvivalPlayModeRunner runner =
            new GameObject(focusedOnly
                    ? "Primitive Survival Focused Verification"
                    : "Primitive Start Survival 5-Day Verification")
                .AddComponent<PrimitiveStartSurvivalPlayModeRunner>();
        runner.FocusedOnly = focusedOnly;
    }
}

public sealed class PrimitiveStartSurvivalPlayModeRunner : MonoBehaviour
{
    private const float DaySeconds = 180f;
    private const int Days = 5;
    private const float VerificationTimeScale = 20f;
    private const float MovementVerificationTimeScale = 10f;
    private const float RecoveryMealDeliveryGameDeadlineSeconds = DaySeconds * 2f;
    private const float RecoveryMealDeliveryRealtimeDeadlineSeconds = 60f;
    private static readonly FacilityRole[] RequiredPrimaryRoles =
    {
        FacilityRole.Meal,
        FacilityRole.Rest,
        FacilityRole.Toilet,
        FacilityRole.Hygiene
    };

    private readonly List<string> report = new();
    private readonly List<string> failures = new();
    private readonly Dictionary<string, int> primitiveCounts =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> primitivePhysicalItemCounts =
        new(StringComparer.Ordinal);
    private readonly List<string> deathEvents = new();
    private readonly List<UnityEngine.Object> outageTemporaryObjects = new();
    private float originalTimeScale;
    private IDisposable primitiveSubscription;
    private IDisposable mealSubscription;
    private IDisposable deathSubscription;
    private CharacterActor focusedVerificationActor;
    private CharacterId focusedVerificationActorId;
    private int physicalMeals;
    private int physicalFieldMeals;
    public bool FocusedOnly { get; set; }
    public bool SixAdultOutage { get; set; }
    public bool PopulationStageCapacity { get; set; }
    public string DurableRequestMode { get; set; } = string.Empty;
    private string ActiveReportPath => PopulationStageCapacity
        ? PrimitiveStartSurvivalPlayModeVerifier.PopulationStageReportPath
        : SixAdultOutage
        ? PrimitiveStartSurvivalPlayModeVerifier.SixAdultOutageReportPath
        : FocusedOnly
            ? PrimitiveStartSurvivalPlayModeVerifier.FocusedReportPath
            : PrimitiveStartSurvivalPlayModeVerifier.ReportPath;

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory("Artifacts/QA");
        originalTimeScale = Time.timeScale;
        yield return new WaitForSecondsRealtime(1f);
        yield return RunVerificationGuarded();
        CompleteVerification();
    }

    private IEnumerator RunVerificationGuarded()
    {
        Stack<IEnumerator> pending = new();
        pending.Push(RunVerification());
        Exception failure = null;
        while (pending.Count > 0 && failure == null)
        {
            IEnumerator currentRoutine = pending.Peek();
            bool moved = false;
            object yielded = null;
            try
            {
                moved = currentRoutine.MoveNext();
                if (moved)
                {
                    yielded = currentRoutine.Current;
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            if (failure != null)
            {
                break;
            }
            if (!moved)
            {
                pending.Pop();
                (currentRoutine as IDisposable)?.Dispose();
                continue;
            }
            if (yielded is IEnumerator nested)
            {
                pending.Push(nested);
                continue;
            }

            yield return yielded;
        }

        while (pending.Count > 0)
        {
            (pending.Pop() as IDisposable)?.Dispose();
        }
        if (failure != null)
        {
            failures.Add(
                "UNHANDLED_VERIFIER_EXCEPTION: "
                + failure.GetType().Name + ": " + failure.Message);
            Debug.LogException(failure);
        }
    }

    private IEnumerator RunVerification()
    {
        DungeonRuntimeLifetimeScope scope = FindRuntimeScope();
        float containerWaitDeadline = Time.realtimeSinceStartup + 10f;
        while (scope == null && Time.realtimeSinceStartup < containerWaitDeadline)
        {
            yield return null;
            scope = FindRuntimeScope();
        }
        Check(scope != null, "RUNTIME_SCOPE", "runtime scope resolved");
        Check(scope?.Container != null, "RUNTIME_CONTAINER", "runtime container built");
        if (scope?.Container == null)
        {
            yield break;
        }

        CharacterActor[] party = ResolveParty();
        if (party.Length == 3)
        {
            Check(true,
                "START_COMMIT",
                "GameplayScene launch flow already committed the starting party.");
        }
        else
        {
            string commit = StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            Check(commit.Contains("committed=True", StringComparison.OrdinalIgnoreCase),
                "START_COMMIT",
                commit);
            yield return null;
            party = ResolveParty();
        }

        // Committing the starting party replaces the preparation scene and its
        // LifetimeScope. Never retain services or event subscriptions resolved
        // before that transition: they belong to a disposed world and silently
        // miss every event produced by the gameplay world.
        float gameplayScopeDeadline = Time.realtimeSinceStartup + 10f;
        scope = FindRuntimeScopeForParty(party);
        while ((scope == null || scope.Container == null)
            && Time.realtimeSinceStartup < gameplayScopeDeadline)
        {
            yield return null;
            party = ResolveParty();
            scope = FindRuntimeScopeForParty(party);
        }
        Check(scope?.Container != null,
            "GAMEPLAY_RUNTIME_SCOPE",
            scope != null
                ? $"scene={scope.gameObject.scene.name}; party={party.Length}"
                : $"missing gameplay scope; party={party.Length}");
        if (scope?.Container == null)
        {
            yield break;
        }

        bool tierZeroReady = StartPartyPreparationPlayModeVerifier
            .TryReconcileTierZeroForDirectGameplayFixture(
                scope,
                out string tierZeroDetail);
        Check(
            tierZeroReady,
            "DIRECT_ENTRY_TIER_ZERO_READY",
            tierZeroDetail);
        if (!tierZeroReady)
        {
            yield break;
        }

        IGameEventBus events = scope.Container.Resolve<IGameEventBus>();
        primitiveSubscription = events.Subscribe<CharacterPrimitiveSurvivalCompletedEvent>(
            completed =>
            {
                if (FocusedOnly
                    && (!focusedVerificationActorId.IsValid
                        || !completed.CharacterId.Equals(
                            focusedVerificationActorId)))
                {
                    return;
                }

                primitiveCounts.TryGetValue(completed.ActionId, out int count);
                primitiveCounts[completed.ActionId] = count + 1;
                primitivePhysicalItemCounts.TryGetValue(
                    completed.ActionId,
                    out int physicalCount);
                primitivePhysicalItemCounts[completed.ActionId] =
                    physicalCount + completed.PhysicalItemCount;
                if (FocusedOnly
                    && focusedVerificationActor?.Brain != null)
                {
                    // Completion is published before the primitive runner's
                    // finally block ends its external intent. Fence new
                    // scheduler admission in the same production callback so
                    // a still-routine-eligible action cannot start and consume
                    // another item before the verifier's coroutine resumes.
                    focusedVerificationActor.Brain.availableActions =
                        Array.Empty<AIAction>();
                    focusedVerificationActor.SetAiPaused(true);
                }
            });
        mealSubscription = events.Subscribe<PhysicalMealConsumedEvent>(consumed =>
        {
            if (!consumed.Result.Success
                || (FocusedOnly
                    && !ReferenceEquals(
                        consumed.Actor,
                        focusedVerificationActor)))
            {
                return;
            }

            physicalMeals++;
            if (consumed.Facility == null)
            {
                physicalFieldMeals++;
            }
        });
        deathSubscription = events.Subscribe<CharacterDeathEvent>(died =>
        {
            deathEvents.Add(
                $"character={died.CharacterId.Value};cause={died.Cause};"
                + $"day={died.AbsoluteDay};location={died.Location.X},{died.Location.Y}");
        });

        IWorldItemStackRuntime items =
            scope.Container.Resolve<IWorldItemStackRuntime>();
        IGridSystemProvider grids = scope.Container.Resolve<IGridSystemProvider>();
        IFacilityCandidateCache facilities =
            scope.Container.Resolve<IFacilityCandidateCache>();
        ICharacterDeprivationQuery deprivation =
            scope.Container.Resolve<ICharacterDeprivationQuery>();
        ICharacterDeprivationCommand deprivationCommands =
            scope.Container.Resolve<ICharacterDeprivationCommand>();
        IEnvironmentalFieldQuery environment =
            scope.Container.Resolve<IEnvironmentalFieldQuery>();
        ICharacterSpeciesEnvironmentCatalog speciesEnvironment =
            scope.Container.Resolve<ICharacterSpeciesEnvironmentCatalog>();
        IGameClock clock = scope.Container.Resolve<IGameClock>();

        if (FocusedOnly)
        {
            ICharacterAiWorldRegistry characterWorld =
                scope.Container.Resolve<ICharacterAiWorldRegistry>();
            IRestoreWorldCandidateQuery restoreCandidates =
                scope.Container.Resolve<IRestoreWorldCandidateQuery>();
            bool hasCharacterCandidate = restoreCandidates.TryGetCharacters(
                out IReadOnlyList<CharacterActor> candidateCharacters);
            report.Add(
                "FOCUSED_CONSUMABLE_ACTOR_PROJECTION "
                + $"scene=[{DescribeCharacterIds(party)}];"
                + $"active=[{DescribeCharacterIds(characterWorld.Characters)}];"
                + $"lifetime=[{DescribeCharacterIds(characterWorld.AllCharacters)}];"
                + $"candidateActive={hasCharacterCandidate};"
                + $"candidate=[{DescribeCharacterIds(candidateCharacters)}];"
                + $"candidateRevision={restoreCandidates.Revision}");
        }

        Check(party.Length == 3, "PARTY_SIZE", $"party={party.Length}");
        report.Add("start-environment=" + string.Join(" | ", party.Select(actor =>
            DescribeEnvironment(actor, environment, speciesEnvironment))));

        if (FocusedOnly)
        {
            float environmentDeadline = Time.realtimeSinceStartup + 10f;
            while (!environment.IsInitialized
                && Time.realtimeSinceStartup < environmentDeadline)
            {
                yield return null;
            }
            Check(environment.IsInitialized,
                "FOCUSED_SAVE_AUTHORITY_READY",
                $"environmentInitialized={environment.IsInitialized};"
                + $"version={environment.Version}");
            if (!environment.IsInitialized)
            {
                yield break;
            }
        }

        VerifyStarterSupplies(items);
        int initialRations = CountItem(items, "food:preserved-ration");
        int initialWater = CountItem(items, "resource:clean-water");
        int maximumRations = initialRations;
        int maximumWater = initialWater;

        if (PopulationStageCapacity)
        {
            yield return RunPopulationStageCapacityVerification(
                party,
                scope,
                items,
                clock,
                scope.Container.Resolve<IDungeonGameSaveService>(),
                events);
            yield break;
        }

        if (SixAdultOutage)
        {
            yield return RunSixAdultOutageVerification(
                party,
                scope,
                items,
                clock,
                facilities,
                deprivation,
                deprivationCommands,
                scope.Container.Resolve<IDungeonGameSaveService>(),
                scope.Container.Resolve<IDungeonGridBuildingControllerProvider>(),
                events);
            yield break;
        }

        if (grids.TryGetGrid(out Grid grid))
        {
            if (FocusedOnly)
            {
                yield return DrainFacilityCandidateIndex(
                    facilities,
                    "FOCUSED_FOUNDATION_INDEX_READY");
            }
            int meal = facilities.GetCandidates(grid, FacilityRole.Meal).Count;
            int rest = facilities.GetCandidates(grid, FacilityRole.Rest).Count;
            int toilet = facilities.GetCandidates(grid, FacilityRole.Toilet).Count;
            int hygiene = facilities.GetCandidates(grid, FacilityRole.Hygiene).Count;
            if (FocusedOnly)
            {
                Check(true,
                    "FOCUSED_SERVICE_FOUNDATION_SNAPSHOT",
                    $"meal/rest/toilet/hygiene={meal}/{rest}/{toilet}/{hygiene};"
                    + $"indexVersion={facilities.CandidateIndexVersion};"
                    + $"pending={facilities.HasPendingIndexBuild}");
            }
            else
            {
                Check(meal + rest + toilet + hygiene == 0,
                    "NO_SERVICE_FOUNDATION",
                    $"meal/rest/toilet/hygiene={meal}/{rest}/{toilet}/{hygiene}");
            }
        }
        else
        {
            Check(false, "GRID", "grid unavailable");
        }

        if (FocusedOnly)
        {
            yield return RunFocusedVerification(
                party,
                deprivation,
                deprivationCommands,
                items,
                clock,
                facilities,
                scope.Container.Resolve<IDungeonGameSaveService>(),
                scope.Container.Resolve<IDungeonGridBuildingControllerProvider>());
            yield break;
        }

        foreach (CharacterActor actor in party)
        {
            ResetNeeds(actor);
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
        }

        Dictionary<string, float> initialHealthByCharacter = party
            .Where(actor => actor != null)
            .ToDictionary(
                actor => actor.Identity?.PersistentId
                    ?? $"instance:{actor.GetInstanceID()}",
                actor => actor.CurrentHealth,
                StringComparer.Ordinal);

        float startedAt = clock.Time;
        float nextDayAt = startedAt + DaySeconds;
        int sampledDay = 0;
        Time.timeScale = VerificationTimeScale;
        float targetEndAt = startedAt + DaySeconds * Days;
        while (clock.Time < targetEndAt
            && party.All(actor => actor != null && !actor.IsDead))
        {
            maximumRations = Mathf.Max(
                maximumRations,
                CountItem(items, "food:preserved-ration"));
            maximumWater = Mathf.Max(
                maximumWater,
                CountItem(items, "resource:clean-water"));
            if (clock.Time >= nextDayAt)
            {
                sampledDay++;
                report.Add(DescribeDay(sampledDay, party, items, deprivation));
                File.WriteAllLines(
                    ActiveReportPath,
                    report);
                nextDayAt += DaySeconds;
            }
            yield return null;
        }

        // The exact five-day frame can publish a deprivation transition before
        // its production self-care/breakdown owner has received a scheduler
        // frame. Freezing time immediately turns that right-censored in-flight
        // state into a false permanent-breakdown failure. Keep the five-day
        // target unchanged and observe only a bounded terminal grace window.
        float settlementStartedAt = clock.Time;
        float settlementGameDeadline = clock.Time + 60f;
        float settlementRealtimeDeadline = Time.realtimeSinceStartup + 5f;
        int settlementStableFrames = 0;
        while (settlementStableFrames < 2
            && clock.Time < settlementGameDeadline
            && Time.realtimeSinceStartup < settlementRealtimeDeadline
            && party.All(actor => actor != null && !actor.IsDead))
        {
            Time.timeScale = VerificationTimeScale;
            bool settled = party.All(actor =>
                !deprivation.HasActiveBreakdown(actor)
                && actor?.Brain?.IsExternallyDrivenActionActive != true);
            settlementStableFrames = settled
                ? settlementStableFrames + 1
                : 0;
            yield return null;
        }
        string breakdownSettlement = string.Join(",", party.Select(actor =>
            $"{actor?.Identity?.PersistentId}:{deprivation.HasActiveBreakdown(actor)}"));
        string externalSettlement = string.Join(",", party.Select(actor =>
            $"{actor?.Identity?.PersistentId}:{actor?.Brain?.IsExternallyDrivenActionActive}"));
        report.Add(
            $"post-five-day-terminal-settlement="
            + $"gameSeconds={clock.Time - settlementStartedAt:0.###};"
            + $"stableFrames={settlementStableFrames};"
            + $"breakdowns={breakdownSettlement};"
            + $"external={externalSettlement}");
        Time.timeScale = 0f;

        bool elapsedFiveDays = clock.Time >= targetEndAt;
        bool allActorsAlive = party.All(actor => actor != null && !actor.IsDead);
        Check(elapsedFiveDays,
            "FIVE_DAY_ELAPSED",
            $"clock={clock.Time:0.###}; start={startedAt:0.###}; target={targetEndAt:0.###}; "
                + $"elapsed={clock.Time - startedAt:0.###}");
        Check(allActorsAlive,
            "FIVE_DAY_SURVIVAL",
            string.Join(", ", party.Select(actor =>
                $"{DescribeActor(actor)}:dead={actor == null || actor.IsDead}:"
                + $"brainExternal={actor?.Brain?.IsExternallyDrivenActionActive}:"
                + $"intent={actor?.Brain?.ExternalIntentOwnerId}:"
                + $"epoch={actor?.Brain?.ExternalIntentEpoch}")));
        report.Add("death-events=" + (deathEvents.Count == 0
            ? "none"
            : string.Join(" | ", deathEvents)));
        foreach (CharacterActor actor in party)
        {
            report.Add("damage-activities=" + DescribeDamageActivities(actor));
            report.Add("ai-failures=" + DescribeAiFailures(actor));
            report.Add("ai-arbitration=" + DescribeAiArbitration(actor));
        }
        Check(party.All(actor => actor.CurrentHealth > 0f),
            "POSITIVE_HEALTH",
            string.Join(", ", party.Select(DescribeActor)));
        Check(party.All(actor =>
            {
                if (actor == null)
                {
                    return false;
                }

                string characterId = actor.Identity?.PersistentId
                    ?? $"instance:{actor.GetInstanceID()}";
                return initialHealthByCharacter.TryGetValue(
                        characterId,
                        out float initialHealth)
                    && actor.CurrentHealth >= initialHealth - 0.001f;
            }),
            "NO_SURVIVAL_DAMAGE",
            string.Join(", ", party.Select(actor =>
            {
                if (actor == null)
                {
                    return "missing";
                }

                string characterId = actor.Identity?.PersistentId
                    ?? $"instance:{actor.GetInstanceID()}";
                initialHealthByCharacter.TryGetValue(characterId, out float initialHealth);
                return $"{characterId}:health={initialHealth:0.###}->{actor.CurrentHealth:0.###}";
            })));
        Check(party.All(actor => !deprivation.HasActiveBreakdown(actor)),
            "NO_ACTIVE_BREAKDOWN",
            string.Join(", ", party.Select(actor =>
                $"{actor.Identity?.PersistentId}:{deprivation.HasActiveBreakdown(actor)}")));
        Check(party.All(actor => actor?.Brain != null
                && actor.Brain.ExternalIntentStaleCompletionCount == 0),
            "NO_STALE_AI_COMPLETION",
            string.Join(", ", party.Select(DescribeAiArbitration)));
        int naturalFinalRations = CountItem(items, "food:preserved-ration");
        int naturalFinalWater = CountItem(items, "resource:clean-water");
        Check(maximumRations <= initialRations && maximumWater <= initialWater,
            "NATURAL_ITEM_CONSERVATION",
            $"ration initial/max/final={initialRations}/{maximumRations}/{naturalFinalRations}; "
                + $"water initial/max/final={initialWater}/{maximumWater}/{naturalFinalWater}");
        Check(physicalMeals > 0 && naturalFinalRations < initialRations,
            "NATURAL_PHYSICAL_MEALS",
            $"events={physicalMeals}; ration={initialRations}->{naturalFinalRations}");
        Check(naturalFinalWater < initialWater,
            "NATURAL_PHYSICAL_WATER",
            $"water={initialWater}->{naturalFinalWater}");
        if (party.Any(actor => actor == null || actor.IsDead))
        {
            yield break;
        }
    }

    private IEnumerator RunFocusedVerification(
        CharacterActor[] party,
        ICharacterDeprivationQuery deprivation,
        ICharacterDeprivationCommand deprivationCommands,
        IWorldItemStackRuntime items,
        IGameClock clock,
        IFacilityCandidateCache facilities,
        IDungeonGameSaveService saves,
        IDungeonGridBuildingControllerProvider buildingControllerProvider)
    {
        CharacterActor focusedActor = party.FirstOrDefault(actor =>
            actor != null
            && !actor.IsDead
            && actor.Role == CharacterRole.Owner)
            ?? party.FirstOrDefault(actor => actor != null && !actor.IsDead);
        Check(focusedActor?.Brain != null && focusedActor.Stats != null,
            "FOCUSED_SETUP",
            focusedActor != null
                ? $"actor={focusedActor.Identity?.PersistentId}; external={focusedActor.Brain?.IsExternallyDrivenActionActive}"
                : "no live focused actor");
        if (focusedActor?.Brain == null || focusedActor.Stats == null)
        {
            yield break;
        }

        focusedVerificationActor = focusedActor;
        focusedVerificationActorId =
            CharacterPersistentIdentity.Require(focusedActor);
        Check(
            focusedVerificationActorId.IsValid
                && primitiveSubscription != null
                && mealSubscription != null,
            "FOCUSED_EVENT_IDENTITY",
            $"actor={focusedVerificationActorId.Value}; subscriptions="
            + $"primitive={primitiveSubscription != null}; meal={mealSubscription != null}");

        foreach (CharacterActor actor in party)
        {
            if (actor == null || actor.IsDead)
            {
                continue;
            }
            deprivationCommands.DebugClearBreakdown(actor);
            ResetNeeds(actor);
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
        }

        Dictionary<string, bool> focusedPauseStates = new(StringComparer.Ordinal);
        foreach (CharacterActor actor in party)
        {
            if (actor == null || actor.IsDead
                || !CharacterPersistentIdentity.TryGet(actor, out CharacterId actorId))
            {
                continue;
            }

            focusedPauseStates[actorId.Value] = actor.IsAiPaused();
            actor.SetAiPaused(true);
        }
        try
        {
            yield return VerifyFocusedPrimitive<AIPrimitiveFieldMeal>(
                focusedActor,
                deprivation,
                CharacterCondition.HUNGER,
                FacilityRole.Meal,
                "survival:field-meal",
                "food:preserved-ration",
                1,
                items,
                clock);
            yield return VerifyFocusedPrimitive<AIPrimitiveLatrine>(
                focusedActor,
                deprivation,
                CharacterCondition.EXCRETION,
                FacilityRole.Toilet,
                "survival:primitive-latrine",
                string.Empty,
                0,
                items,
                clock);
            yield return VerifyFocusedPrimitive<AIPrimitiveBucketWash>(
                focusedActor,
                deprivation,
                CharacterCondition.HYGIENE,
                FacilityRole.Hygiene,
                "survival:bucket-wash",
                "resource:clean-water",
                1,
                items,
                clock);
            yield return VerifyFocusedFloorRestWithFacilityTeardown(
                focusedActor,
                party,
                deprivation,
                items,
                clock,
                facilities,
                saves,
                buildingControllerProvider);

            CharacterActor restoredFocusedActor = FindActiveActor(
                focusedVerificationActorId);
            Check(restoredFocusedActor != null,
                "FOCUSED_FLOOR_REST_RESTORED_ACTOR",
                restoredFocusedActor != null
                    ? $"actor={focusedVerificationActorId.Value};lifecycle={restoredFocusedActor.CurrentLifecycleState}"
                    : $"missing={focusedVerificationActorId.Value}");
            if (restoredFocusedActor != null)
            {
                focusedActor = restoredFocusedActor;
                focusedVerificationActor = restoredFocusedActor;
            }

            Check(physicalFieldMeals == GetCount("survival:field-meal"),
                "FOCUSED_FIELD_MEAL_AUTHORITY",
                $"primitive={GetCount("survival:field-meal")}; physical={physicalFieldMeals}");
        }
        finally
        {
            if (focusedActor == null)
            {
                focusedActor = FindActiveActor(focusedVerificationActorId);
            }
            if (focusedActor != null)
            {
                focusedActor.SetAiPaused(true);
                EndFocusedExternalIntent(focusedActor.Brain);
                focusedActor.Brain?.StopCurrentActionForReplan(
                    "primitive-focused-verifier-finalize");
                focusedActor.GetAbility<AbilityMove>()?.CancelActiveMovement();
            }
            RestorePausedStates(focusedPauseStates);
            focusedVerificationActor = null;
            focusedVerificationActorId = default;
            Time.timeScale = 0f;
        }
    }

    private IEnumerator RunPopulationStageCapacityVerification(
        CharacterActor[] startingParty,
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime items,
        IGameClock clock,
        IDungeonGameSaveService saves,
        IGameEventBus events)
    {
        const string prefix = "V27_POPULATION_STAGE";
        DungeonGameSaveData baseline = saves?.Capture();
        Check(baseline != null,
            prefix + "_BASELINE_CAPTURED",
            $"sections={baseline?.sections?.Count ?? 0}");
        if (baseline == null || scope?.Container == null)
            yield break;

        IGridSystemProvider grids = scope.Container.Resolve<IGridSystemProvider>();
        IFloorClutterDiagnosticsQuery clutter =
            scope.Container.Resolve<IFloorClutterDiagnosticsQuery>();
        ProgressionSceneRuntimeReferences progression =
            scope.Container.Resolve<ProgressionSceneRuntimeReferences>();
        IFacilityShopCatalog facilityCatalog =
            scope.Container.Resolve<IFacilityShopCatalog>();
        BuildingSO storageAsset = Resources.LoadAll<BuildingSO>("SO/Building")
            .Where(value => value != null
                && !value.IsDeprecatedCompatibilityAsset
                && value.GetStorageMassCapacityGrams() > 0L
                && value.StoresAllCategories())
            .OrderByDescending(value => (decimal)value.GetStorageMassCapacityGrams()
                / Math.Max(1, value.width * value.height))
            .ThenBy(value => value.GetFacilityCode(), StringComparer.Ordinal)
            .FirstOrDefault();
        Check(storageAsset != null,
            prefix + "_STORAGE_AUTHORITY",
            storageAsset == null
                ? "missing"
                : $"asset={storageAsset.name};capacityGrams={storageAsset.GetStorageMassCapacityGrams()}");
        if (storageAsset == null)
            yield break;

        try
        {
            foreach (int population in PopulationStagePortfolioCatalog.PopulationStages)
            {
                if (!saves.TryRestore(CloneSave(baseline), out DungeonGameRestoreReport restore))
                {
                    Check(false,
                        prefix + "_RESTORE_" + population,
                        restore == null
                            ? "restore report missing"
                            : string.Join(" | ", restore.Errors));
                    yield break;
                }
                yield return null;
                yield return null;

                CharacterActor[] stageParty = ResolveParty();
                scope = FindRuntimeScopeForParty(stageParty);
                if (scope?.Container == null)
                {
                    Check(false,
                        prefix + "_SCOPE_" + population,
                        "runtime scope missing after restore");
                    yield break;
                }
                grids = scope.Container.Resolve<IGridSystemProvider>();
                clutter = scope.Container.Resolve<IFloorClutterDiagnosticsQuery>();
                progression = scope.Container.Resolve<ProgressionSceneRuntimeReferences>();
                facilityCatalog = scope.Container.Resolve<IFacilityShopCatalog>();
                events = scope.Container.Resolve<IGameEventBus>();
                items = scope.Container.Resolve<IWorldItemStackRuntime>();
                clock = scope.Container.Resolve<IGameClock>();
                IWorldResourceRuntime worldResources =
                    scope.Container.Resolve<IWorldResourceRuntime>();

                int tier = PopulationStagePortfolioCatalog.TierForPopulation(population);
                for (int tierIndex = 0; tierIndex < tier; tierIndex++)
                {
                    DungeonSpaceExpansionDefinition definition =
                        DungeonSpaceExpansionCatalog.All[tierIndex];
                    ResearchProjectSO project = Resources
                        .LoadAll<ResearchProjectSO>("SO/Research/Projects")
                        .Single(value => value != null
                            && string.Equals(
                                value.ProjectId.Value,
                                definition.ResearchProjectId,
                                StringComparison.Ordinal));
                    BlueprintResearchUnlockResult unlock =
                        BlueprintResearchService.ApplyCompletion(
                            project,
                            progression.BlueprintResearch.State,
                            progression.BlueprintResearch.ShopUnlockState,
                            facilityCatalog);
                    events.Publish(new BlueprintResearchCompletedEvent(project, unlock));
                    yield return null;
                }

                Grid grid = null;
                DungeonInteriorLayoutSnapshot interior = default;
                string gridFailure = "grid unavailable";
                int minimumRequiredColumns =
                    PopulationStagePortfolioCatalog.InteriorColumnsForPopulation(
                        population);
                int expectedAuthoredColumns = tier == 0
                    ? DungeonSpaceExpansionCatalog.InitialInteriorColumns
                    : DungeonSpaceExpansionCatalog.All[tier - 1]
                        .TargetInteriorColumns;
                bool gridReady = grids.TryGetGrid(out grid)
                    && DungeonSpaceGridLayout.TryCapture(
                        grid,
                        out interior,
                        out gridFailure)
                    && interior.ColumnCount == expectedAuthoredColumns
                    && interior.ColumnCount >= minimumRequiredColumns;
                Check(gridReady,
                    prefix + "_RESEARCH_SPACE_" + population,
                    gridReady
                        ? $"columns={interior.ColumnCount};developerKeyUsed=False"
                        : $"failure={gridFailure}");
                if (!gridReady)
                    yield break;

                foreach (CharacterActor unused in stageParty.Skip(population))
                {
                    unused.SetAiPaused(true);
                    unused.Brain?.StopCurrentActionForReplan(
                        "v27-population-stage-disabled");
                    unused.GetAbility<AbilityMove>()?.CancelActiveMovement();
                    unused.SetLifecycleState(
                        CharacterLifecycleState.PreparingExpedition);
                }
                List<CharacterActor> cohort = CreatePopulationCohort(
                    stageParty,
                    scope,
                    grid,
                    $"stage-{population}",
                    population);
                foreach (CharacterActor actor in cohort)
                {
                    actor.SetAiPaused(true);
                    actor.Brain?.StopCurrentActionForReplan(
                        "v27-population-stage-capture");
                    actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
                }
                yield return null;
                bool liveCohort = IsExactLiveCohort(cohort, population)
                    && cohort.Select(actor =>
                            CharacterPersistentIdentity.Require(actor).Value)
                        .Distinct(StringComparer.Ordinal)
                        .Count() == population;
                Check(liveCohort,
                    prefix + "_LIVE_COHORT_" + population,
                    DescribeCohort(cohort));
                if (!liveCohort)
                    yield break;

                SurvivalClosedLoopAssessment stage =
                    V27SixAdultSurvivalLoopDebugScenarios
                        .CapturePopulationStage(population);
                HashSet<string> beforeStackIds = items.GetAllStacks()
                    .Where(value => value != null)
                    .Select(value => value.StackId)
                    .ToHashSet(StringComparer.Ordinal);
                Vector2Int origin = cohort[0].GetNowXY();
                Vector2Int[] reserveCells = grid.SearchPath(origin)
                    .GetReachablePositions()
                    .Where(grid.IsWalkable)
                    .Where(cell => grid.GetGridCell(cell)?
                        .GetOccupant(GridLayer.Building) == null)
                    .Where(cell => !items.GetAllStacks().Any(stack =>
                        stack != null && stack.Quantity > 0 && stack.Position == cell))
                    .Distinct()
                    .OrderBy(cell => Mathf.Abs(cell.x - origin.x)
                        + Mathf.Abs(cell.y - origin.y))
                    .ThenBy(cell => cell.y)
                    .ThenBy(cell => cell.x)
                    .Take(3)
                    .ToArray();
                Check(reserveCells.Length == 3,
                    prefix + "_RESERVE_CELLS_" + population,
                    $"cells={reserveCells.Length}");
                if (reserveCells.Length != 3)
                    yield break;

                bool mealSpawned = items.SpawnItemAt(
                    "food:grain-porridge",
                    stage.ImmediateMealUnits,
                    reserveCells[0],
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int meals);
                bool grainSpawned = items.SpawnItemAt(
                    "resource:twilight-grain",
                    stage.SevenDayGrainUnits,
                    reserveCells[1],
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int grain);
                bool waterSpawned = items.SpawnItemAt(
                    "resource:clean-water",
                    stage.SevenDayWaterUnits,
                    reserveCells[2],
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int water);
                WorldItemStackSnapshot[] stageStacks = items.GetAllStacks()
                    .Where(value => value != null
                        && !beforeStackIds.Contains(value.StackId))
                    .ToArray();
                bool exactPhysical = mealSpawned && grainSpawned && waterSpawned
                    && meals == stage.ImmediateMealUnits
                    && grain == stage.SevenDayGrainUnits
                    && water == stage.SevenDayWaterUnits
                    && stageStacks.Where(value => string.Equals(
                            value.ItemId, "food:grain-porridge", StringComparison.Ordinal))
                        .Sum(value => value.Quantity) == stage.ImmediateMealUnits
                    && stageStacks.Where(value => string.Equals(
                            value.ItemId, "resource:twilight-grain", StringComparison.Ordinal))
                        .Sum(value => value.Quantity) == stage.SevenDayGrainUnits
                    && stageStacks.Where(value => string.Equals(
                            value.ItemId, "resource:clean-water", StringComparison.Ordinal))
                        .Sum(value => value.Quantity) == stage.SevenDayWaterUnits;
                Check(exactPhysical,
                    prefix + "_SEVEN_DAY_PHYSICAL_RESERVE_" + population,
                    $"meal={meals}/{stage.ImmediateMealUnits};"
                    + $"grain={grain}/{stage.SevenDayGrainUnits};"
                    + $"water={water}/{stage.SevenDayWaterUnits};"
                    + $"stacks={stageStacks.Length}");

                long liveReserveMassGrams = checked(
                    items.MassQuery.GetDefinitionUnitMass(
                        (ItemDefinitionId)"food:grain-porridge").Value
                        * stage.ImmediateMealUnits
                    + items.MassQuery.GetDefinitionUnitMass(
                        (ItemDefinitionId)"resource:twilight-grain").Value
                        * stage.SevenDayGrainUnits
                    + items.MassQuery.GetDefinitionUnitMass(
                        (ItemDefinitionId)"resource:clean-water").Value
                        * stage.SevenDayWaterUnits);
                Check(liveReserveMassGrams == stage.RequiredStorageMassGrams,
                    prefix + "_RESERVE_MASS_AUTHORITY_" + population,
                    $"live={liveReserveMassGrams};static={stage.RequiredStorageMassGrams}");
                long requiredCapacityGrams = checked(
                    (stage.RequiredStorageMassGrams * 1000L + 699L) / 700L);
                long unitStorageCapacityGrams =
                    storageAsset.GetStorageMassCapacityGrams();
                int storageCount = checked((int)(
                    (requiredCapacityGrams + unitStorageCapacityGrams - 1L)
                    / unitStorageCapacityGrams));
                long authoredCapacityGrams = checked(
                    storageCount * unitStorageCapacityGrams);
                int normalStoragePermille = checked((int)(
                    (stage.RequiredStorageMassGrams * 1000L
                        + authoredCapacityGrams - 1L)
                    / authoredCapacityGrams));
                long burstMassGrams = Math.Max(
                    stage.MaximumRelevantStackMassGrams,
                    Math.Max(
                        stage.GrossGrainMassGramsPerDay,
                        stage.GrossMealMassGramsPerDay));
                int overflowCells = population switch
                {
                    1 or 3 => 1,
                    6 => 2,
                    12 => 4,
                    18 => 5,
                    24 => 6,
                    _ => throw new ArgumentOutOfRangeException(nameof(population))
                };
                long overflowCapacityGrams = checked(
                    overflowCells * stage.MaximumRelevantStackMassGrams);
                int faultStoragePermille = checked((int)(
                    ((stage.RequiredStorageMassGrams + burstMassGrams) * 1000L
                        + authoredCapacityGrams + overflowCapacityGrams - 1L)
                    / (authoredCapacityGrams + overflowCapacityGrams)));
                Check(normalStoragePermille <= 700 && faultStoragePermille <= 900,
                    prefix + "_AUTHORED_STORAGE_" + population,
                    $"asset={storageAsset.name};count={storageCount};"
                    + $"capacityGrams={authoredCapacityGrams};reserveMassGrams={stage.RequiredStorageMassGrams};"
                    + $"normal={normalStoragePermille};fault={faultStoragePermille};"
                    + $"overflowMassGrams={overflowCapacityGrams};overflowCells={overflowCells}");

                List<KeyValuePair<Vector2Int, SpatialCellRole>> roles = items
                    .GetAllStacks()
                    .Where(value => value != null && value.Quantity > 0)
                    .Select(value => new KeyValuePair<Vector2Int, SpatialCellRole>(
                        value.Position,
                        beforeStackIds.Contains(value.StackId)
                            ? SpatialCellRole.AuthorizedLooseSource
                            : SpatialCellRole.StorageBuffer))
                    .ToList();
                DungeonSpaceLayoutSnapshot layout = new(
                    roles,
                    Array.Empty<Vector2Int>(),
                    cleanRunP95HaulDispatchAndDeliverySeconds: 15f,
                    gameDaySeconds: DaySeconds);
                FloorClutterAssessment floor = clutter.Capture(
                    grid,
                    layout,
                    clock.Time);
                Vector2Int[] fixedResourceCells = worldResources.Nodes
                    .Where(value => value != null)
                    .Select(value => value.GetComponent<Facility>())
                    .Where(value => value != null)
                    .Select(value => value.centerPos)
                    .Where(value => grid.GetGridCell(value)?.AreaType
                        == GridCellAreaType.DungeonInterior)
                    .Distinct()
                    .OrderBy(value => value.y)
                    .ThenBy(value => value.x)
                    .ToArray();
                bool fixedResourcesExact = fixedResourceCells.All(value =>
                    grid.GetGridCell(value)?.GetOccupant(GridLayer.Building)
                        is IWorldResourceNodeHost)
                    && fixedResourceCells.Length
                        == V27PopulationStageSpatialBaseline
                            .FixedWorldFeatureCells(population);
                Check(fixedResourcesExact,
                    prefix + "_FIXED_WORLD_FEATURES_" + population,
                    $"interiorNodes={fixedResourceCells.Length};"
                    + $"reserved={V27PopulationStageSpatialBaseline.FixedWorldFeatureCells(population)};cells="
                    + string.Join("|", fixedResourceCells));
                int runtimeHeadroom =
                    V27PopulationStageSpatialBaseline.RuntimeHeadroomPermille(
                        population,
                        floor.OutsideContainment
                            .Select(value => value.Position)
                            .Distinct()
                            .Count());
                Check(floor.OutsideContainment.Count == 0
                        && runtimeHeadroom >= 300,
                    prefix + "_RUNTIME_HEADROOM_" + population,
                    $"outside={floor.OutsideContainment.Count};"
                    + $"fixedWorldFeatures={fixedResourceCells.Length};"
                    + $"headroomPermille={runtimeHeadroom};"
                    + $"grid={grid.width}x{grid.height}");
                int growthAvailablePermille = checked(
                    1000 - stage.RecurringSharePermille - 150 - 100);
                bool minimumPlotGranularityWarning = population == 1
                    && stage.RecurringSharePermille > 350
                    && stage.RecurringSharePermille <= 400
                    && growthAvailablePermille >= 350;
                Check(stage.GrossFoodCoveragePermille >= 1250
                        && stage.NetFoodCoveragePermille >= 1100
                        && (stage.RecurringSharePermille <= 350
                            || minimumPlotGranularityWarning)
                        && growthAvailablePermille >= 350,
                    prefix + "_CLOSED_LOOP_" + population,
                    $"gross={stage.GrossFoodCoveragePermille};"
                    + $"net={stage.NetFoodCoveragePermille};"
                    + $"recurring={stage.RecurringSharePermille};"
                    + $"growth={growthAvailablePermille};emergency=100;"
                    + $"disposition={(minimumPlotGranularityWarning ? "minimum-plot-warning" : "normal")}");

                report.Add($"population-stage={population};tier={tier};"
                    + $"actors={cohort.Count};reserveMassGrams={stage.RequiredStorageMassGrams};"
                    + $"storageCapacityGrams={authoredCapacityGrams};"
                    + $"fixedWorldFeatures={fixedResourceCells.Length};"
                    + $"runtimeHeadroomPermille={runtimeHeadroom}");
                DestroyOutageTemporaryObjects();
                yield return null;
            }

            Check(true,
                prefix + "_ALL_STAGES_EXACT",
                "populations=1,3,6,12,18,24;physicalReserve=true;"
                + "researchExpansion=true;developerE=false");
        }
        finally
        {
            saves.TryRestore(CloneSave(baseline), out _);
            DestroyOutageTemporaryObjects();
            Time.timeScale = 0f;
        }
    }

    private IEnumerator RunSixAdultOutageVerification(
        CharacterActor[] startingParty,
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime items,
        IGameClock clock,
        IFacilityCandidateCache facilities,
        ICharacterDeprivationQuery deprivation,
        ICharacterDeprivationCommand deprivationCommands,
        IDungeonGameSaveService saves,
        IDungeonGridBuildingControllerProvider buildingControllerProvider,
        IGameEventBus events)
    {
        const string prefix = "V27_SIX_ADULT_OUTAGE";
        Grid grid = null;
        if (startingParty == null
            || startingParty.Length != 3
            || scope?.Container == null
            || items == null
            || clock == null
            || facilities == null
            || deprivation == null
            || deprivationCommands == null
            || saves == null
            || buildingControllerProvider?.Controller == null
            || events == null
            || !scope.Container.Resolve<IGridSystemProvider>()
                .TryGetGrid(out grid))
        {
            Check(false,
                prefix + "_FIXTURE_READY",
                $"party={startingParty?.Length ?? 0};scope={scope?.Container != null};"
                + $"items={items != null};clock={clock != null};facilities={facilities != null};"
                + $"deprivation={deprivation != null};commands={deprivationCommands != null};"
                + $"saves={saves != null};controller={buildingControllerProvider?.Controller != null};"
                + $"events={events != null};grid={grid != null}");
            yield break;
        }

        DungeonGameSaveData baseline = null;
        IDisposable startedSubscription = null;
        IDisposable completedSubscription = null;
        IDisposable waterSubscription = null;
        IDisposable primaryMealSubscription = null;
        HashSet<string> observedActorIds = new(StringComparer.Ordinal);
        Dictionary<string, int> primitiveStarted = new(StringComparer.Ordinal);
        Dictionary<string, int> primitiveCompleted = new(StringComparer.Ordinal);
        Dictionary<string, int> primitivePhysical = new(StringComparer.Ordinal);
        Dictionary<string, int> waterConsumed = new(StringComparer.Ordinal);
        List<string> primitiveStartDetails = new();
        List<string> primaryMealDetails = new();
        Dictionary<string, float> primaryMealNutritionByActor =
            new(StringComparer.Ordinal);
        int primaryMealEvents = 0;
        float savedTimeScale = Time.timeScale;
        CharacterSpawner ambientSpawner =
            UnityEngine.Object.FindFirstObjectByType<CharacterSpawner>();
        bool ambientSpawnerWasPaused = ambientSpawner != null
            && ambientSpawner.DeterministicSimulationPausedForDiagnostics;
        ambientSpawner?.ConfigureDeterministicSimulationForDiagnostics(true);

        try
        {
            yield return DrainFacilityCandidateIndex(
                facilities,
                prefix + "_BASELINE_INDEX_READY");
            List<string> primarySetup = EnsurePrimaryServiceFixture(
                startingParty,
                scope,
                grid,
                facilities,
                buildingControllerProvider.Controller);
            yield return DrainFacilityCandidateIndex(
                facilities,
                prefix + "_PRIMARY_FIXTURE_INDEX_READY");

            // A normal-service baseline owns physical food in its service
            // buffer. Trigger the authored proactive delivery with one
            // routine-hungry consumer, then fence that consumer as soon as the
            // routed stack exists so setup cannot consume the evidence before
            // the hauler deposits it.
            CharacterActor provisioningActor = startingParty[0];
            foreach (CharacterActor actor in startingParty)
            {
                ResetNeeds(actor);
            }
            CharacterNeedResponseProfile provisioningResponse =
                provisioningActor.Stats.GetNeedResponse(CharacterCondition.HUNGER);
            SetNeed(
                provisioningActor,
                CharacterCondition.HUNGER,
                provisioningResponse.routineStart);
            foreach (CharacterActor actor in startingParty)
            {
                actor.SetAiPaused(false);
                actor.Brain?.RequestImmediateReplan(clearFailures: true);
            }
            bool provisioningActorFenced = false;
            float primaryWarmupStartedAt = clock.Time;
            float primaryWarmupGameDeadline = clock.Time + DaySeconds;
            float primaryWarmupRealtimeDeadline = Time.realtimeSinceStartup + 30f;
            while (!HasUsablePrimaryAuthority(
                       startingParty[0], facilities, grid)
                   && clock.Time < primaryWarmupGameDeadline
                   && Time.realtimeSinceStartup < primaryWarmupRealtimeDeadline)
            {
                if (!provisioningActorFenced
                    && items.GetAllStacks().Any(stack => stack != null
                        && stack.Quantity > 0
                        && !string.IsNullOrWhiteSpace(stack.DestinationId)
                        && CharacterConsumablesInputDestinationIdentity
                            .IsDestinationForKind(
                                stack.DestinationId,
                                CharacterConsumablesInputKind.Meal)))
                {
                    provisioningActorFenced = true;
                    provisioningActor.SetAiPaused(true);
                    provisioningActor.Brain?.StopCurrentActionForReplan(
                        "v27-six-adult-baseline-meal-provisioning");
                    provisioningActor.GetAbility<AbilityMove>()?
                        .CancelActiveMovement();
                    ResetNeeds(provisioningActor);
                }
                foreach (CharacterActor actor in startingParty)
                {
                    if (actor != provisioningActor || !provisioningActorFenced)
                    {
                        actor.SetAiPaused(false);
                        actor.Brain?.RequestImmediateReplan(clearFailures: true);
                    }
                }
                Time.timeScale = MovementVerificationTimeScale;
                yield return null;
            }
            foreach (CharacterActor actor in startingParty)
            {
                actor.SetAiPaused(true);
                actor.Brain?.StopCurrentActionForReplan(
                    "v27-six-adult-baseline-provisioned");
                actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
                ResetNeeds(actor);
            }
            yield return null;
            Dictionary<FacilityRole, List<string>> baselineRoles =
                CapturePrimaryRoleIdentity(facilities, grid);
            List<string> baselineWaterProducers = CaptureWaterProducerIdentity();
            bool primaryPresent = HasUsablePrimaryAuthority(
                startingParty[0],
                facilities,
                grid);
            Check(primaryPresent,
                prefix + "_PRIMARY_AUTHORITY_PRESENT",
                DescribePrimaryAuthority(baselineRoles, baselineWaterProducers)
                + ";usability=" + DescribePrimaryUsability(
                    startingParty[0], facilities, grid)
                + ";mealDelivery=" + DescribeMealDeliveryState(
                    scope.Container,
                    items,
                    startingParty)
                + $";warmupSeconds={clock.Time - primaryWarmupStartedAt:0.###}"
                + ";setup=[" + string.Join(" | ", primarySetup) + "]");
            if (!primaryPresent)
            {
                yield break;
            }

            baseline = saves.Capture();
            Check(baseline != null,
                prefix + "_BASELINE_CAPTURED",
                baseline != null
                    ? $"sections={baseline.sections?.Count ?? 0};facilities="
                        + baselineRoles.Sum(pair => pair.Value.Count)
                    : "capture returned null");
            if (baseline == null)
            {
                yield break;
            }

            foreach (CharacterActor actor in startingParty)
            {
                actor.SetAiPaused(true);
                actor.Brain?.StopCurrentActionForReplan(
                    "v27-six-adult-outage-baseline-fence");
                actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
            }
            yield return null;

            startedSubscription = events.Subscribe<CharacterPrimitiveSurvivalStartedEvent>(
                started =>
                {
                    if (!observedActorIds.Contains(started.CharacterId.Value))
                    {
                        return;
                    }

                    string key = OutageEventKey(started.CharacterId, started.ActionId);
                    primitiveStarted.TryGetValue(key, out int count);
                    primitiveStarted[key] = count + 1;
                    primitiveStartDetails.Add(
                        $"actor={started.CharacterId.Value}:action={started.ActionId}:"
                        + $"emergency={started.Emergency}:need={started.NeedValue:0.###}:"
                        + $"clock={clock.Time:0.###}:frame={Time.frameCount}");
                });
            completedSubscription = events.Subscribe<CharacterPrimitiveSurvivalCompletedEvent>(
                completed =>
                {
                    if (!observedActorIds.Contains(completed.CharacterId.Value))
                    {
                        return;
                    }

                    string key = OutageEventKey(completed.CharacterId, completed.ActionId);
                    primitiveCompleted.TryGetValue(key, out int count);
                    primitiveCompleted[key] = count + 1;
                    primitivePhysical.TryGetValue(key, out int physical);
                    primitivePhysical[key] = physical + completed.PhysicalItemCount;
                });
            waterSubscription = events.Subscribe<CharacterWaterConsumedEvent>(consumed =>
            {
                if (!observedActorIds.Contains(consumed.CharacterId.Value))
                {
                    return;
                }

                waterConsumed.TryGetValue(consumed.CharacterId.Value, out int count);
                waterConsumed[consumed.CharacterId.Value] = count + 1;
            });
            primaryMealSubscription = events.Subscribe<PhysicalMealConsumedEvent>(consumed =>
            {
                if (consumed.Actor != null
                    && CharacterPersistentIdentity.TryGet(
                        consumed.Actor,
                        out CharacterId actorId)
                    && observedActorIds.Contains(actorId.Value)
                    && consumed.Result.Success
                    && consumed.Facility != null)
                {
                    primaryMealEvents++;
                    primaryMealNutritionByActor.TryGetValue(
                        actorId.Value,
                        out float consumedNutrition);
                    primaryMealNutritionByActor[actorId.Value] =
                        consumedNutrition + consumed.Result.Nutrition;
                    primaryMealDetails.Add(
                        $"actor={actorId.Value}:facility="
                        + consumed.Facility.RequirePersistentInstanceId().Value
                        + $":nutrition={consumed.Result.Nutrition:0.###}:"
                        + $"hunger={GetNeed(consumed.Actor, CharacterCondition.HUNGER):0.###}:"
                        + $"clock={clock.Time:0.###}:frame={Time.frameCount}");
                }
            });

            HashSet<BuildableObject> primaryFacilities = new();
            foreach (FacilityRole role in RequiredPrimaryRoles)
            {
                foreach (BuildableObject candidate in facilities.GetCandidates(grid, role))
                {
                    if (candidate != null && !candidate.isDestroy)
                    {
                        primaryFacilities.Add(candidate);
                    }
                }
            }
            foreach (BuildableObject candidate in FindLiveWaterProducers())
            {
                primaryFacilities.Add(candidate);
            }

            bool teardownSucceeded = true;
            List<string> teardown = new(primaryFacilities.Count);
            foreach (BuildableObject candidate in primaryFacilities
                         .OrderBy(value => value.RequirePersistentInstanceId().Value,
                             StringComparer.Ordinal))
            {
                string id = candidate.RequirePersistentInstanceId().Value;
                bool destroyed = buildingControllerProvider.Controller.TryDestroyBuilding(
                    candidate,
                    out string message);
                teardownSucceeded &= destroyed;
                teardown.Add($"{id}:destroyed={destroyed}:message={message}");
            }
            Check(teardownSucceeded,
                prefix + "_PRIMARY_TEARDOWN",
                string.Join(" | ", teardown));
            yield return DrainFacilityCandidateIndex(
                facilities,
                prefix + "_OUTAGE_INDEX_READY");

            Dictionary<FacilityRole, List<string>> outageRoles =
                CapturePrimaryRoleIdentity(facilities, grid);
            List<string> outageWaterProducers = CaptureWaterProducerIdentity();
            bool primaryUnavailable = RequiredPrimaryRoles.All(role =>
                    !outageRoles.TryGetValue(role, out List<string> ids)
                    || ids.Count == 0)
                && outageWaterProducers.Count == 0;
            Check(teardownSucceeded && primaryUnavailable,
                prefix + "_PRIMARY_UNAVAILABLE_EXACT",
                DescribePrimaryAuthority(outageRoles, outageWaterProducers));
            if (!teardownSucceeded || !primaryUnavailable)
            {
                yield break;
            }

            List<CharacterActor> outageActors = CreateSixAdultCohort(
                startingParty,
                scope,
                grid,
                "outage");
            yield return DrainCohortPathSearches(
                outageActors,
                prefix + "_OUTAGE_PATH_AUTHORITY_READY");
            RefreshObservedActorIds(observedActorIds, outageActors);
            bool sixLive = IsExactLiveCohort(outageActors, 6);
            Check(sixLive,
                prefix + "_SIX_LIVE_ADULTS",
                DescribeCohort(outageActors));
            Check(sixLive && outageActors.All(HasCompleteSurvivalCatalog),
                prefix + "_FULL_PRODUCTION_ACTION_CATALOG",
                string.Join(" | ", outageActors.Select(DescribeSurvivalCatalog)));
            if (!sixLive || outageActors.Any(actor => !HasCompleteSurvivalCatalog(actor)))
            {
                yield break;
            }

            // The additional three actors contribute live population/need
            // load only. Service execution is owned by the three actors that
            // were composed through the production start-party pipeline.
            foreach (CharacterActor actor in outageActors.Take(3))
            {
                deprivationCommands.DebugClearBreakdown(actor);
                ResetNeeds(actor);
                actor.SetAiPaused(false);
                actor.Brain?.RequestImmediateReplan(clearFailures: true);
            }

            CharacterActor cancellationActor = outageActors[0];
            CharacterId cancellationId = CharacterPersistentIdentity.Require(cancellationActor);
            int cancellationRationsBefore = CountItem(items, "food:preserved-ration");
            int cancellationCompletionsBefore = GetEventCount(
                primitiveCompleted,
                cancellationId,
                "survival:field-meal");
            SetEmergencyNeed(cancellationActor, CharacterCondition.HUNGER);
            float cancellationNeedBefore = GetNeed(
                cancellationActor,
                CharacterCondition.HUNGER);
            cancellationActor.Brain.RequestImmediateReplan(clearFailures: true);
            float cancellationStartDeadline = Time.realtimeSinceStartup + 8f;
            Time.timeScale = VerificationTimeScale;
            while (GetEventCount(
                       primitiveStarted,
                       cancellationId,
                       "survival:field-meal") == 0
                   && Time.realtimeSinceStartup < cancellationStartDeadline)
            {
                Time.timeScale = VerificationTimeScale;
                yield return null;
            }

            bool cancellationStarted =
                GetEventCount(
                    primitiveStarted,
                    cancellationId,
                    "survival:field-meal") == 1
                && cancellationActor.Brain.IsExternallyDrivenActionActive;
            cancellationActor.SetAiPaused(true);
            CharacterActionIntentLease cancellationLease = new(
                cancellationActor.Brain.ExternalIntentOwnerId,
                cancellationActor.Brain.ExternalIntentKind,
                cancellationActor.Brain.ExternalIntentEpoch);
            bool cancellationAccepted = cancellationStarted
                && cancellationActor.Brain.CancelExternallyDrivenAction(cancellationLease);
            cancellationActor.GetAbility<AbilityMove>()?.CancelActiveMovement();
            yield return null;
            yield return null;
            bool cancellationConserved = cancellationAccepted
                && !cancellationActor.Brain.IsExternallyDrivenActionActive
                && GetEventCount(
                    primitiveCompleted,
                    cancellationId,
                    "survival:field-meal") == cancellationCompletionsBefore
                && CountItem(items, "food:preserved-ration")
                    == cancellationRationsBefore
                && GetNeed(cancellationActor, CharacterCondition.HUNGER)
                    <= cancellationNeedBefore + 0.001f;
            Check(cancellationConserved,
                prefix + "_CANCEL_NO_RECOVERY_NO_CONSUME",
                $"started={cancellationStarted};accepted={cancellationAccepted};"
                + $"completion={cancellationCompletionsBefore}->"
                + GetEventCount(
                    primitiveCompleted,
                    cancellationId,
                    "survival:field-meal")
                + $";ration={cancellationRationsBefore}->"
                + CountItem(items, "food:preserved-ration")
                + $";hunger={cancellationNeedBefore:0.###}->"
                + GetNeed(cancellationActor, CharacterCondition.HUNGER).ToString("0.###"));
            ResetNeeds(cancellationActor);

            primitiveStarted.Clear();
            primitiveCompleted.Clear();
            primitivePhysical.Clear();
            waterConsumed.Clear();
            ConfigureOutageNeeds(outageActors);
            foreach (CharacterActor actor in outageActors)
            {
                actor.SetAiPaused(false);
                actor.Brain.RequestImmediateReplan(clearFailures: true);
            }

            int rationBefore = CountItem(items, "food:preserved-ration");
            int waterBefore = CountItem(items, "resource:clean-water");
            int rationMaximum = rationBefore;
            int waterMaximum = waterBefore;
            float outageStartedAt = clock.Time;
            float outageGameDeadline = outageStartedAt + DaySeconds;
            float outageRealtimeDeadline = Time.realtimeSinceStartup + 40f;
            while (clock.Time < outageGameDeadline
                && Time.realtimeSinceStartup < outageRealtimeDeadline
                && IsExactLiveCohort(outageActors, 6))
            {
                Time.timeScale = VerificationTimeScale;
                rationMaximum = Mathf.Max(
                    rationMaximum,
                    CountItem(items, "food:preserved-ration"));
                waterMaximum = Mathf.Max(
                    waterMaximum,
                    CountItem(items, "resource:clean-water"));
                yield return null;
            }
            foreach (CharacterActor actor in outageActors)
            {
                actor.SetAiPaused(true);
            }

            int rationAfter = CountItem(items, "food:preserved-ration");
            int waterAfter = CountItem(items, "resource:clean-water");
            int fieldMealPhysical = CountCompletedPhysicalActions(
                primitivePhysical,
                outageActors,
                "survival:field-meal");
            int bucketWashPhysical = CountCompletedPhysicalActions(
                primitivePhysical,
                outageActors,
                "survival:bucket-wash");
            int waterDrinkCount = waterConsumed.Values.Sum();
            bool fullDay = clock.Time >= outageGameDeadline;
            Check(fullDay,
                prefix + "_ONE_GAME_DAY_ELAPSED",
                $"start={outageStartedAt:0.###};end={clock.Time:0.###};"
                + $"target={outageGameDeadline:0.###}");
            Check(OutageTargetsCompleted(outageActors, primitiveCompleted, waterConsumed),
                prefix + "_ALL_FIVE_FALLBACKS_COMPLETED",
                DescribeOutageEvents(outageActors, primitiveStarted, primitiveCompleted, waterConsumed));
            Check(rationMaximum == rationBefore
                    && waterMaximum == waterBefore
                    && rationBefore - rationAfter == fieldMealPhysical
                    && waterBefore - waterAfter
                        == bucketWashPhysical + waterDrinkCount,
                prefix + "_PHYSICAL_EXACT_NO_MINT",
                $"ration={rationBefore}/{rationMaximum}/{rationAfter};"
                + $"fieldMeal={fieldMealPhysical};water={waterBefore}/{waterMaximum}/{waterAfter};"
                + $"bucketWash={bucketWashPhysical};drink={waterDrinkCount}");
            Check(IsExactLiveCohort(outageActors, 6)
                    && outageActors.All(actor => !deprivation.HasActiveBreakdown(actor)),
                prefix + "_NO_DEATH_DOWN_BREAKDOWN",
                DescribeCohort(outageActors) + ";breakdown="
                + string.Join(",", outageActors.Select(actor =>
                    CharacterPersistentIdentity.Require(actor).Value + ":"
                    + deprivation.HasActiveBreakdown(actor))));

            foreach (CharacterActor actor in outageActors)
            {
                actor.SetAiPaused(true);
                actor.Brain?.StopCurrentActionForReplan("v27-six-adult-outage-restore");
                actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
            }

            bool restored = saves.TryRestore(
                CloneSave(baseline),
                out DungeonGameRestoreReport restoreReport);
            Check(restored,
                prefix + "_PRIMARY_RESTORE_TRANSACTION",
                restoreReport != null
                    ? $"success={restoreReport.Success};buildings="
                        + $"{restoreReport.RestoredBuildingCount};characters="
                        + $"{restoreReport.RestoredCharacterCount};warnings=["
                        + string.Join(" | ", restoreReport.Warnings)
                        + "];errors=["
                        + string.Join(" | ", restoreReport.Errors)
                        + "]"
                    : "restore not attempted");
            if (!restored)
            {
                yield break;
            }
            yield return null;
            yield return DrainFacilityCandidateIndex(
                facilities,
                prefix + "_RESTORED_INDEX_READY");
            bool restoredGridReady = scope.Container
                .Resolve<IGridSystemProvider>()
                .TryGetGrid(out Grid restoredGrid);
            Dictionary<FacilityRole, List<string>> restoredRoles =
                restoredGridReady
                    ? CapturePrimaryRoleIdentity(facilities, restoredGrid)
                    : RequiredPrimaryRoles.ToDictionary(
                        role => role,
                        _ => new List<string>());
            List<string> restoredWaterProducers = CaptureWaterProducerIdentity();
            bool exactFacilityRestore = restoredGridReady
                && RequiredPrimaryRoles.All(role =>
                    baselineRoles[role].SequenceEqual(
                        restoredRoles[role],
                        StringComparer.Ordinal))
                && baselineWaterProducers.SequenceEqual(
                    restoredWaterProducers,
                    StringComparer.Ordinal);
            Check(exactFacilityRestore,
                prefix + "_PRIMARY_FACILITY_IDENTITY_EXACT",
                "before=" + DescribePrimaryAuthority(baselineRoles, baselineWaterProducers)
                + ";after=" + DescribePrimaryAuthority(restoredRoles, restoredWaterProducers));
            if (!exactFacilityRestore)
            {
                yield break;
            }
            grid = restoredGrid;

            CharacterActor[] restoredParty = ResolveParty();
            List<CharacterActor> recoveryActors = CreateSixAdultCohort(
                restoredParty,
                scope,
                grid,
                "recovery");
            yield return DrainCohortPathSearches(
                recoveryActors,
                prefix + "_RECOVERY_PATH_AUTHORITY_READY");
            RefreshObservedActorIds(observedActorIds, recoveryActors);
            primitiveStarted.Clear();
            primitiveCompleted.Clear();
            primitivePhysical.Clear();
            waterConsumed.Clear();
            primitiveStartDetails.Clear();
            primaryMealDetails.Clear();
            primaryMealNutritionByActor.Clear();
            primaryMealEvents = 0;
            CharacterCondition[] recoveryTargets =
            {
                CharacterCondition.HUNGER,
                CharacterCondition.THIRST,
                CharacterCondition.SLEEP,
                CharacterCondition.EXCRETION,
                CharacterCondition.HYGIENE
            };
            int recoveryTargetCount = 0;
            List<string> recoveryDetails = new(recoveryTargets.Length);
            for (int serviceIndex = 0; serviceIndex < recoveryTargets.Length; serviceIndex++)
            {
                foreach (CharacterActor actor in recoveryActors)
                {
                    // The 24-hour outage survival assertion is complete before
                    // these independent recovery rows begin. Restore health so
                    // several intentionally long service deadlines do not
                    // accumulate unrelated environmental damage across rows and
                    // despawn the next row's consumer or logistics helper.
                    actor.Heal(actor.MaxHealth);
                    ResetNeeds(actor);
                    actor.SetAiPaused(true);
                }

                CharacterCondition target = recoveryTargets[serviceIndex];
                CharacterActor targetActor = GetServiceTargetActor(
                    recoveryActors,
                    serviceIndex);
                CharacterNeedResponseProfile response =
                    targetActor.Stats.GetNeedResponse(target);
                // This phase proves normal-operation primary dominance after
                // the one-day outage has ended. Emergency fallback was already
                // exercised above, so every restored service starts at its
                // authored routine boundary instead of manufacturing a second
                // emergency that is allowed to select the primitive path.
                float demandValue = response.routineStart;
                SetNeed(targetActor, target, demandValue);
                if (target == CharacterCondition.HUNGER)
                {
                    bool routinePrecondition = !CharacterNeedAiThresholds
                        .IsEmergencyOrImminentPhysicalHarm(
                            targetActor,
                            CharacterCondition.HUNGER);
                    Check(
                        routinePrecondition,
                        prefix + "_RECOVERY_HUNGER_ROUTINE_PRECONDITION",
                        $"need={GetNeed(targetActor, target):0.###};"
                        + $"routineStart={response.routineStart:0.###};"
                        + $"emergencyStart={response.emergencyStart:0.###}");
                    if (!routinePrecondition)
                    {
                        break;
                    }
                }
                yield return DrainActorPathSearch(
                    targetActor,
                    prefix + "_RECOVERY_" + target + "_PATH_READY");
                float original = GetNeed(targetActor, target);
                int primitiveBefore = primitiveStarted.Values.Sum();
                int mealBefore = primaryMealEvents;
                string targetActorId =
                    CharacterPersistentIdentity.Require(targetActor).Value;
                primaryMealNutritionByActor.TryGetValue(
                    targetActorId,
                    out float mealNutritionBefore);
                bool mealBufferReady = target != CharacterCondition.HUNGER;
                int stagingPrimitiveDelta = 0;
                string mealDeliveryDetail = target == CharacterCondition.HUNGER
                    ? "not-observed"
                    : "n/a";

                if (target == CharacterCondition.HUNGER)
                {
                    // A primary meal has a real physical delivery leg. Stage it
                    // at routine hunger through the complete production AI path.
                    // The consumer must remain awake because its actor-owned
                    // full-grid route snapshot is the authority used by the
                    // proactive delivery probe. Primitive fallback remains
                    // unavailable while that route is pending and while a
                    // reachable queueable meal facility exists.
                    int stagingPrimitiveBefore = primitiveStarted.Values.Sum();
                    // Keep the third production actor clean for the following
                    // bed-service row. Two authored workers are sufficient to
                    // prove request -> pickup -> delivery, while waking all
                    // three can leave the future sleep consumer owning an
                    // unrelated in-flight haul when the buffer becomes ready.
                    foreach (CharacterActor hauler in recoveryActors.Take(2))
                    {
                        deprivationCommands.DebugClearBreakdown(hauler);
                        hauler.SetAiPaused(false);
                        hauler.Brain?.RequestImmediateReplan(clearFailures: true);
                    }
                    // Recovery dominance begins only after the restored primary
                    // service owns a real physical meal.  A complete warehouse
                    // pickup plus the two-leg source-to-facility route can exceed
                    // one game day in a lawful wide dungeon; the outage contract
                    // does not define a one-day replenishment SLA.  Keep this
                    // bounded at two days and still require the production
                    // request, reservation, pickup, movement and deposit path.
                    float bufferGameDeadline = clock.Time
                        + RecoveryMealDeliveryGameDeadlineSeconds;
                    float bufferRealtimeDeadline = Time.realtimeSinceStartup
                        + RecoveryMealDeliveryRealtimeDeadlineSeconds;
                    bool consumerRemovedFromHaulPool = false;
                    while (clock.Time < bufferGameDeadline
                        && Time.realtimeSinceStartup < bufferRealtimeDeadline
                        && !items.GetAllStacks().Any(stack => stack != null
                            && stack.State == WorldItemStackState.FacilityBuffer
                            && stack.Quantity > 0
                            && !string.IsNullOrWhiteSpace(stack.DestinationId)
                            && CharacterConsumablesInputDestinationIdentity
                                .IsDestinationForKind(
                                    stack.DestinationId,
                                    CharacterConsumablesInputKind.Meal)))
                    {
                        // Isolate the delivery SLA from the fallback whose
                        // dominance is measured immediately afterwards.  The
                        // hunger probe remains at authored routine demand so
                        // CharacterConsumablesRuntime keeps the exact routed
                        // delivery alive, while unrelated needs cannot turn a
                        // hauler into another emergency consumer.  This does
                        // not create food or bypass hauling: the buffer must
                        // still be populated by the production request,
                        // reservation, pickup, movement and deposit path.
                        foreach (CharacterActor hauler in recoveryActors.Take(2))
                        {
                            ResetNeeds(hauler);
                            if (hauler == targetActor)
                            {
                                SetNeed(hauler, CharacterCondition.HUNGER, demandValue);
                            }
                        }

                        // The consumer must be runnable long enough for the
                        // production consumables probe to author its physical
                        // destination. Once that route exists, keep the
                        // consumer out of the haul pool so this focused row
                        // measures another worker delivering food followed by
                        // the consumer using the restored primary service.
                        // Otherwise the consumer can pick up a second stock
                        // move, and stopping all staging hauls at the first
                        // successful buffer leaves carry cleanup in front of
                        // the service action being measured.
                        if (!consumerRemovedFromHaulPool
                            && items.GetAllStacks().Any(stack => stack != null
                                && stack.Quantity > 0
                                && !string.IsNullOrWhiteSpace(stack.DestinationId)
                                && CharacterConsumablesInputDestinationIdentity
                                    .IsDestinationForKind(
                                        stack.DestinationId,
                                        CharacterConsumablesInputKind.Meal)))
                        {
                            targetActor.SetAiPaused(true);
                            targetActor.Brain?.StopCurrentActionForReplan(
                                "v27-six-adult-meal-request-authored");
                            consumerRemovedFromHaulPool = true;
                        }
                        Time.timeScale = MovementVerificationTimeScale;
                        yield return null;
                    }
                    mealBufferReady = items.GetAllStacks().Any(stack => stack != null
                        && stack.State == WorldItemStackState.FacilityBuffer
                        && stack.Quantity > 0
                        && !string.IsNullOrWhiteSpace(stack.DestinationId)
                            && CharacterConsumablesInputDestinationIdentity
                                .IsDestinationForKind(
                                    stack.DestinationId,
                                    CharacterConsumablesInputKind.Meal));
                    mealDeliveryDetail = DescribeMealDeliveryState(
                        scope.Container,
                        items,
                        recoveryActors);
                    stagingPrimitiveDelta = primitiveStarted.Values.Sum()
                        - stagingPrimitiveBefore;
                    foreach (CharacterActor hauler in recoveryActors.Take(2))
                    {
                        hauler.SetAiPaused(true);
                        hauler.Brain?.StopCurrentActionForReplan(
                            "v27-six-adult-meal-staging-complete");
                    }
                    yield return null;

                    // Measure normal-operation dominance from an authored,
                    // physically supplied primary service at routine demand.
                    // Emergency demand intentionally permits the primitive
                    // fallback when a just-consumed meal buffer is waiting on
                    // its next physical delivery, so using it here would test
                    // the outage policy a second time instead of recovery
                    // priority. Staging activity is reported separately and
                    // cannot satisfy the assertion.
                    ResetNeeds(targetActor);
                    SetNeed(targetActor, target, demandValue);
                    original = GetNeed(targetActor, target);
                    primitiveBefore = primitiveStarted.Values.Sum();
                    mealBefore = primaryMealEvents;
                    primaryMealNutritionByActor.TryGetValue(
                        targetActorId,
                        out mealNutritionBefore);
                }

                // The meal buffer has already been populated by production AI.
                // From this boundary onward the row measures only whether the
                // selected consumer chooses the restored primary service over
                // its primitive fallback. Keep unrelated workers paused so a
                // priority haul cannot leak ownership into a later row. The
                // authored sink is the one exception: its first dry attempt
                // publishes a physical manual-water delivery, so healthy
                // adults must remain available to execute that real logistics
                // dependency. Pausing every other adult makes a valid primary
                // service impossible and is not representative of a six-adult
                // settlement.
                CharacterActor[] serviceHelpers = target == CharacterCondition.HYGIENE
                    ? recoveryActors.Take(3)
                        .Where(actor => actor != targetActor)
                        .ToArray()
                    : Array.Empty<CharacterActor>();
                deprivationCommands.DebugClearBreakdown(targetActor);
                targetActor.SetAiPaused(false);
                targetActor.Brain?.RequestImmediateReplan(clearFailures: true);
                foreach (CharacterActor serviceHelper in serviceHelpers)
                {
                    deprivationCommands.DebugClearBreakdown(serviceHelper);
                    serviceHelper.SetAiPaused(false);
                    serviceHelper.Brain?.RequestImmediateReplan(clearFailures: true);
                }

                // A restored consumer can lawfully begin this row while still
                // carrying outage cleanup stock. The outage itself is exactly
                // one day, but that duration is not also a recovery-completion
                // deadline. Sleep is work-depleted rather than wall-clock
                // depleted, so a busy hauler can select the bed late in the
                // window and still need its exact route plus rest duration.
                // Give those already-live primary actions another half day.
                // Hygiene starts at the authored routine boundary, so normal
                // work may lawfully run until it becomes the selected need; its
                // bound then includes that routine delay, one failed dry
                // attempt, a full physical manual-water delivery and the second
                // facility visit.
                float serviceGameWindow = target switch
                {
                    CharacterCondition.HUNGER =>
                        RecoveryMealDeliveryGameDeadlineSeconds,
                    CharacterCondition.SLEEP => DaySeconds * 2.5f,
                    CharacterCondition.HYGIENE => DaySeconds * 3f,
                    _ => DaySeconds
                };
                float serviceRealtimeWindow = target switch
                {
                    CharacterCondition.HUNGER =>
                        RecoveryMealDeliveryRealtimeDeadlineSeconds,
                    CharacterCondition.SLEEP => 65f,
                    CharacterCondition.HYGIENE => 80f,
                    _ => 30f
                };
                float serviceGameDeadline = clock.Time
                    + serviceGameWindow;
                float serviceRealtimeDeadline = Time.realtimeSinceStartup
                    + serviceRealtimeWindow;
                bool hygieneDeliveryWindowStarted = false;
                bool hygienePrimaryActionWindowStarted = false;
                bool hygieneConsumerWaitingForDelivery = false;
                BuildableObject hygieneManualWaterFixture = null;
                int hygienePrimaryActionPathSteps = 0;
                float hygienePrimaryActionMoveSpeed = 0f;
                float hygienePrimaryActionRealtimeWindow = 0f;
                while (clock.Time < serviceGameDeadline
                    && Time.realtimeSinceStartup < serviceRealtimeDeadline)
                {
                    if (target == CharacterCondition.HYGIENE
                        && !hygieneDeliveryWindowStarted
                        && HasRoutedManualWater(
                            items,
                            targetActor.Brain?.bestAction?.destination))
                    {
                        // Routine demand can lawfully run normal work before it
                        // selects hygiene. Once that first facility attempt has
                        // authored a real physical water route, start a separate
                        // one-shot logistics-and-retry SLA instead of charging
                        // its travel time against the demand-selection window.
                        hygieneDeliveryWindowStarted = true;
                        hygieneManualWaterFixture =
                            targetActor.Brain?.bestAction?.destination;
                        hygieneConsumerWaitingForDelivery = true;
                        targetActor.SetAiPaused(true);
                        targetActor.Brain?.StopCurrentActionForReplan(
                            "v27-six-adult-hygiene-await-manual-water");
                        serviceGameDeadline = clock.Time + DaySeconds * 2.5f;
                        serviceRealtimeDeadline = Time.realtimeSinceStartup + 65f;
                    }

                    if (target == CharacterCondition.HYGIENE
                        && hygieneConsumerWaitingForDelivery
                        && HasBufferedManualWater(
                            items,
                            hygieneManualWaterFixture))
                    {
                        // The consumer authored the exact physical dependency,
                        // but must not join the haul pool for that same request.
                        // Keep it parked while the other healthy adults execute
                        // reservation, pickup, movement and deposit. Once the
                        // authored fixture buffer is physically populated, wake
                        // the consumer through its normal decision tree.
                        hygieneConsumerWaitingForDelivery = false;
                        targetActor.SetAiPaused(false);
                        targetActor.Brain?.RequestImmediateReplan(
                            clearFailures: true);
                        serviceGameDeadline = Mathf.Max(
                            serviceGameDeadline,
                            clock.Time + DaySeconds * 0.5f);
                        serviceRealtimeDeadline = Mathf.Max(
                            serviceRealtimeDeadline,
                            Time.realtimeSinceStartup + 45f);
                    }

                    if (target == CharacterCondition.HYGIENE
                        && hygieneDeliveryWindowStarted
                        && !hygienePrimaryActionWindowStarted
                        && string.Equals(
                            targetActor.Brain?.CurrentActionDebugLabel,
                            "위생",
                            StringComparison.Ordinal))
                    {
                        // The authored sink can be on another connected floor.
                        // Start the consumer SLA when the primary action owns
                        // its exact route, and derive the bound from that route
                        // and the actor's production movement speed. A fixed
                        // wall-clock timeout turns a lawful wide-dungeon route
                        // into a verifier false negative; this bound still
                        // remains finite and topology-sensitive.
                        hygienePrimaryActionWindowStarted = true;
                        hygienePrimaryActionPathSteps =
                            targetActor.Brain.bestAction?.pathSteps?.Count ?? 0;
                        hygienePrimaryActionMoveSpeed =
                            CharacterMovementKinematics.GetMoveSpeed(
                                targetActor,
                                1f);
                        float projectedRouteSeconds =
                            hygienePrimaryActionPathSteps
                            / Mathf.Max(0.1f, hygienePrimaryActionMoveSpeed);
                        hygienePrimaryActionRealtimeWindow = Mathf.Clamp(
                            projectedRouteSeconds * 1.5f + 30f,
                            45f,
                            180f);
                        serviceRealtimeDeadline = Mathf.Max(
                            serviceRealtimeDeadline,
                            Time.realtimeSinceStartup
                                + hygienePrimaryActionRealtimeWindow);
                        serviceGameDeadline = Mathf.Max(
                            serviceGameDeadline,
                            clock.Time + hygienePrimaryActionRealtimeWindow);
                    }

                    primaryMealNutritionByActor.TryGetValue(
                        targetActorId,
                        out float currentMealNutrition);
                    bool serviceRecovered = target == CharacterCondition.HUNGER
                        ? currentMealNutrition > mealNutritionBefore + 0.5f
                        : GetNeed(targetActor, target) > original + 0.5f;
                    if (serviceRecovered)
                    {
                        break;
                    }

                    foreach (CharacterActor actor in recoveryActors.Take(3))
                    {
                        // Environmental survivability was asserted during the
                        // preceding 24-hour outage. These recovery rows are
                        // independent service-liveness probes and may each run
                        // for more than a game day, so prevent damage accrued in
                        // one probe from despawning the next probe's consumer or
                        // its logistics helpers. This does not alter needs,
                        // inventory, routing, movement or facility effects.
                        actor.Heal(actor.MaxHealth);
                        if (actor == targetActor)
                        {
                            ResetUnrelatedNeeds(actor, target);
                        }
                        else
                        {
                            ResetNeeds(actor);
                        }
                    }

                    // Grid path brokerage is frame-budgeted, while need decay
                    // and this SLA use the game clock. Running the routed-water
                    // phase at 10x would consume 2.5 game days with one tenth of
                    // the production frames and manufacture broker starvation.
                    // Once the physical route exists, preserve the normal
                    // frame/game-time ratio and let the realtime bound guard the
                    // focused probe.
                    Time.timeScale = target == CharacterCondition.HYGIENE
                        && hygieneDeliveryWindowStarted
                            ? 1f
                            : MovementVerificationTimeScale;
                    yield return null;
                }

                if (target == CharacterCondition.HYGIENE
                    && hygieneDeliveryWindowStarted
                    && GetNeed(targetActor, target) <= original + 0.5f
                    && (string.Equals(
                            targetActor.Brain?.CurrentActionDebugLabel,
                            "위생",
                            StringComparison.Ordinal)
                        || serviceHelpers.Any(actor =>
                            actor.GetComponent<AbilityHaul>()?.IsHauling == true)))
                {
                    float terminalGameDeadline = clock.Time + DaySeconds * 0.25f;
                    float terminalRealtimeDeadline = Time.realtimeSinceStartup + 10f;
                    while (clock.Time < terminalGameDeadline
                        && Time.realtimeSinceStartup < terminalRealtimeDeadline
                        && GetNeed(targetActor, target) <= original + 0.5f)
                    {
                        foreach (CharacterActor actor in recoveryActors.Take(3))
                        {
                            actor.Heal(actor.MaxHealth);
                            if (actor == targetActor)
                            {
                                ResetUnrelatedNeeds(actor, target);
                            }
                            else
                            {
                                ResetNeeds(actor);
                            }
                        }
                        Time.timeScale = 1f;
                        yield return null;
                    }
                }

                string actionAtDeadline =
                    targetActor.Brain?.CurrentActionDebugLabel ?? "<none>";
                string serviceDiagnostics = target == CharacterCondition.HYGIENE
                    ? DescribeHygieneRecoveryDiagnostics(
                        targetActor,
                        serviceHelpers.FirstOrDefault(),
                        items)
                    : "n/a";

                foreach (CharacterActor actor in recoveryActors.Take(3))
                {
                    actor.SetAiPaused(true);
                    actor.Brain?.StopCurrentActionForReplan(
                        "v27-six-adult-primary-service-isolation");
                }
                yield return null;

                primaryMealNutritionByActor.TryGetValue(
                    targetActorId,
                    out float mealNutritionAfter);
                float mealNutritionDelta = mealNutritionAfter - mealNutritionBefore;
                bool recovered = target == CharacterCondition.HUNGER
                    ? mealNutritionDelta > 0.5f
                    : GetNeed(targetActor, target) > original + 0.5f;
                int primitiveDelta = primitiveStarted.Values.Sum() - primitiveBefore;
                bool primaryMeal = target != CharacterCondition.HUNGER
                    || primaryMealEvents > mealBefore;
                bool passedService = recovered
                    && primitiveDelta == 0
                    && stagingPrimitiveDelta == 0
                    && primaryMeal
                    && mealBufferReady;
                if (passedService)
                {
                    recoveryTargetCount++;
                }
                recoveryDetails.Add(
                    $"{target}:{targetActor.name}:before={original:0.###}:"
                    + $"after={GetNeed(targetActor, target):0.###}:"
                    + $"primitiveDelta={primitiveDelta}:"
                    + $"primaryMealDelta={primaryMealEvents - mealBefore}:"
                    + $"primaryMealNutritionDelta={mealNutritionDelta:0.###}:"
                    + $"mealBufferReady={mealBufferReady}:"
                    + $"stagingPrimitiveDelta={stagingPrimitiveDelta}:"
                    + $"mealDelivery={mealDeliveryDetail}:"
                    + $"serviceHelpers={string.Join(",", serviceHelpers.Select(actor => actor.Identity?.PersistentId ?? "missing"))}:"
                    + $"hygieneDeliveryWindowStarted={hygieneDeliveryWindowStarted}:"
                    + $"hygienePrimaryActionWindowStarted={hygienePrimaryActionWindowStarted}:"
                    + $"hygieneConsumerWaitingForDelivery={hygieneConsumerWaitingForDelivery}:"
                    + $"hygieneManualWaterFixture={hygieneManualWaterFixture?.PersistentInstanceId.Value ?? string.Empty}:"
                    + $"hygienePrimaryPathSteps={hygienePrimaryActionPathSteps}:"
                    + $"hygienePrimaryMoveSpeed={hygienePrimaryActionMoveSpeed:0.###}:"
                    + $"hygienePrimaryRealtimeWindow={hygienePrimaryActionRealtimeWindow:0.###}:"
                    + $"actionAtDeadline={actionAtDeadline}:"
                    + $"serviceDiagnostics={serviceDiagnostics}:"
                    + $"passed={passedService}");
                if (!passedService)
                {
                    // Preserve the first incorrect production boundary. Letting a
                    // failed service consume another full day would turn the
                    // remaining independent rows into lifecycle cascades.
                    break;
                }
            }

            foreach (CharacterActor actor in recoveryActors)
            {
                actor.SetAiPaused(true);
            }

            int recoveryPrimitiveStarts = primitiveStarted.Values.Sum();
            bool primaryDominance = IsExactLiveCohort(recoveryActors, 6)
                && recoveryTargetCount == 5
                && primaryMealEvents > 0
                && recoveryPrimitiveStarts == 0
                && recoveryActors.All(actor =>
                    !deprivation.HasActiveBreakdown(actor));
            CharacterDeprivationDiagnosticsSnapshot deprivationDiagnostics =
                deprivation.GetDiagnostics();
            Check(primaryDominance,
                prefix + "_RECOVERY_PRIMARY_DOMINANCE",
                $"recovered={recoveryTargetCount}/5;primaryMealEvents={primaryMealEvents};"
                + $"primitiveStarts={recoveryPrimitiveStarts};"
                + $"primitiveRatePermille={(recoveryTargetCount > 0 ? recoveryPrimitiveStarts * 1000 / recoveryTargetCount : 1000)};"
                + $"primitiveStartDetails=[{string.Join(" | ", primitiveStartDetails)}];"
                + $"primaryMealDetails=[{string.Join(" | ", primaryMealDetails)}];"
                + $"services=[{string.Join(" | ", recoveryDetails)}];"
                + DescribeCohort(recoveryActors)
                + $";safeDrink=requests={deprivationDiagnostics.SafeReliefRequests}:"
                + $"pending={deprivationDiagnostics.SafeReliefPlanSearchPending}:"
                + $"noSource={deprivationDiagnostics.SafeReliefPlanNoSource}:"
                + $"reservationRejected={deprivationDiagnostics.SafeReliefPlanReservationRejected}:"
                + $"started={deprivationDiagnostics.SafeReliefActionsStarted}:"
                + $"success={deprivationDiagnostics.SafeReliefSuccesses}:"
                + $"moveFailures={deprivationDiagnostics.SafeReliefMoveFailures}:"
                + $"last={deprivationDiagnostics.LastSafeReliefPlanFailureDetail};"
                + ";mealDelivery=" + DescribeMealDeliveryState(
                    scope.Container,
                    items,
                    recoveryActors));
            Check(recoveryPrimitiveStarts * 20 <= Math.Max(1, recoveryTargetCount),
                prefix + "_RECOVERY_PRIMITIVE_STARTS_LE_5_PERCENT",
                $"starts={recoveryPrimitiveStarts};serviceCompletions={recoveryTargetCount}");
            Check(primaryDominance,
                prefix + "_RESULT",
                "liveAdults=6;outageHours=24;fallbacks=5;restore=exact;"
                + "primitiveRecoveryPermille=0;consoleIssues=deferred-to-final-gate");
        }
        finally
        {
            startedSubscription?.Dispose();
            completedSubscription?.Dispose();
            waterSubscription?.Dispose();
            primaryMealSubscription?.Dispose();
            if (baseline != null)
            {
                saves.TryRestore(
                    CloneSave(baseline),
                    out _);
            }
            DestroyOutageTemporaryObjects();
            if (ambientSpawner != null)
            {
                ambientSpawner.ConfigureDeterministicSimulationForDiagnostics(
                    ambientSpawnerWasPaused);
            }
            Time.timeScale = savedTimeScale;
        }
    }

    private static string DescribeMealDeliveryState(
        IObjectResolver container,
        IWorldItemStackRuntime items,
        IReadOnlyList<CharacterActor> actors)
    {
        string stacks = string.Join(",", items.GetAllStacks()
            .Where(stack => stack != null
                && !string.IsNullOrWhiteSpace(stack.DestinationId)
                && CharacterConsumablesInputDestinationIdentity
                    .IsDestinationForKind(
                        stack.DestinationId,
                        CharacterConsumablesInputKind.Meal))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .Select(stack => $"{stack.StackId}:{stack.ItemId}:q{stack.Quantity}:"
                + $"r{stack.ReservedQuantity}:{stack.State}@{stack.Position}->"
                + $"{stack.DestinationPosition}:dest={stack.DestinationId}"));
        IWorldItemHaulPlanningService planning =
            container.Resolve<IWorldItemHaulPlanningService>();
        CharacterConsumablesRuntime consumables =
            container.Resolve<CharacterConsumablesRuntime>();
        ICharacterConsumablesInventoryPort consumableInventory =
            container.Resolve<ICharacterConsumablesInventoryPort>();
        ICharacterConsumablesWorldPort consumableWorld =
            container.Resolve<ICharacterConsumablesWorldPort>();
        string sourceMeals = string.Join(",", consumableInventory.GetAllStacks()
            .Where(stack => string.Equals(
                stack.ItemId.Value,
                "food:preserved-ration",
                StringComparison.Ordinal))
            .OrderBy(stack => stack.StackId.Value, StringComparer.Ordinal)
            .Select(stack =>
            {
                bool resolved = consumableInventory.TryGetMeal(
                    stack.ItemId,
                    out CharacterConsumablesMealDefinitionSnapshot meal);
                string policies = resolved
                    ? string.Join("/", actors
                        .Where(actor => actor != null)
                        .Select(actor =>
                        {
                            CharacterId actorId = CharacterPersistentIdentity.Require(actor);
                            return actorId.Value + "="
                                + consumables.IsMealAllowed(actorId, meal);
                        }))
                    : "unresolved";
                return $"{stack.StackId.Value}:q{stack.Quantity}:"
                    + $"r{stack.ReservedQuantity}:a{stack.AvailableQuantity}:"
                    + $"{stack.State}@{stack.Position}:dest={stack.DestinationId}:"
                    + $"forbidden={stack.Forbidden}:contamination={stack.Contamination:0.###}:"
                    + $"fresh={stack.RemainingFreshnessSeconds:0.###}:"
                    + (resolved
                        ? $"meal={meal.Id.Value}:quality={meal.QualityBand}:role={meal.ServingRole}:"
                            + $"nutrition={meal.Nutrition:0.###}:policies={policies}"
                        : "meal=unresolved");
            }));
        string previews = string.Join(",", actors
            .Where(actor => actor != null)
            .Select(actor =>
            {
                AbilityHaul haul = actor.GetComponent<AbilityHaul>();
                AbilityMove move = actor.GetAbility<AbilityMove>();
                string runtime = $":position={actor.GetNowXY()}:"
                    + $"clock={move?.GameClockTimeForDiagnostics:0.###}/"
                    + $"{move?.GameClockDeltaTimeForDiagnostics:0.###}:"
                    + $"timeScale={Time.timeScale:0.###}:"
                    + $"action={actor.Brain?.CurrentActionDebugLabel}:"
                    + $"phase={actor.Brain?.CurrentActionPhase}:"
                    + $"phaseDetail={actor.Brain?.CurrentActionPhaseDetail}:"
                    + $"actionDestination={actor.Brain?.CurrentDestinationDebugLabel}:"
                    + $"preferred={actor.Brain?.RuntimePreferredActionDisposition}/"
                    + $"{actor.Brain?.RuntimePreferredActionDispositionBranch}:"
                    + $"preferredFailure={actor.Brain?.FirstPreferredActionHardFailure}:"
                    + $"lastFailure={actor.Brain?.LastActionFailure}:"
                    + $"haul={haul?.IsHauling}:plan={haul?.CurrentPlanSummary}:"
                    + $"haulStage={haul?.CurrentExecutionStage}:"
                    + $"haulHeartbeat={haul?.RoutineHeartbeat}:"
                    + $"haulPath={haul?.ActivePathDebug}:"
                    + $"haulUnload={haul?.CurrentUnloadReason}:"
                    + $"haulFailure={haul?.LastFailureReason}:"
                    + $"move={move?.IsSystemMoveInProgress}:"
                    + $"moveDestination={move?.ActiveSystemMoveDestinationForDiagnostics}:"
                    + $"moveOwner={move?.ActiveMovementOperationOwnerForDiagnostics}:"
                    + $"moveBlocked={move?.LastGridMoveWasBlocked}/"
                    + $"{move?.LastGridMoveFailureReason}:"
                    + $"moveCancel={move?.LastMovementCancellationSourceForDiagnostics}:"
                    + $"movePreempt={move?.LastMovementOperationPreemptionForDiagnostics}:"
                    + $"moveRejected={move?.LastRejectedMovementOperationOwnerForDiagnostics}";
                return planning.TryPreviewBestPlan(
                        actor,
                        out WorldItemHaulPlan plan,
                        out string reason)
                    ? $"{actor.name}:preview={plan.PrimaryDestinationId}:"
                        + $"legs={plan.PickupLegs.Count}/{plan.DeliveryLegs.Count}"
                        + runtime
                    : $"{actor.name}:none={reason}" + runtime;
            }));
        CharacterActor routeActor = actors.FirstOrDefault(actor => actor != null);
        string routes = routeActor == null
            ? "actor-missing"
            : string.Join(",", consumableWorld.FacilityIds
                .Where(id => consumableWorld.TryGetFacility(
                    id,
                    out CharacterConsumablesFacilitySnapshot facility)
                    && facility.MealFacility)
                .OrderBy(id => id.Value, StringComparer.Ordinal)
                .Select(id =>
                {
                    consumableWorld.TryGetFacility(
                        id,
                        out CharacterConsumablesFacilitySnapshot facility);
                    CharacterMealRouteStatus status =
                        consumableWorld.GetMealRouteStatus(
                            CharacterPersistentIdentity.Require(routeActor),
                            routeActor.GetNowXY(),
                            facility.Position,
                            out float travelSeconds);
                    return $"{id.Value}@{facility.Position}:{status}:"
                        + $"travel={travelSeconds:0.###}";
                }));
        bool wakePort = container.Resolve<ICharacterConsumablesWorkforcePort>() != null;
        DungeonWorkforceReplanService workforce =
            container.Resolve<IWorkforceReplanService>() as DungeonWorkforceReplanService;
        return $"wakePort={wakePort};probeCount={consumables.MealDeliveryProbeCount};"
            + $"probe={consumables.LastMealDeliveryProbeDetail};"
            + $"requestFailure={consumables.LastMealDeliveryRequestFailure};"
            + $"haulWake={workforce?.LastHaulReplanDetail ?? "unavailable"};"
            + $"stacks=[{stacks}];sourceMeals=[{sourceMeals}];"
            + $"previews=[{previews}];routes=[{routes}]";
    }

    private static bool IsPrimaryAuthorityReadyForDemand(
        CharacterActor actor,
        BuildableObject candidate,
        FacilityRole role)
    {
        if (FacilityCandidateScorer.IsCandidate(
                actor,
                candidate,
                role,
                out string reason))
        {
            return true;
        }

        // Meal buffers are demand-routed.  A valid meal facility is therefore
        // allowed to be empty before the first hungry actor exists.  Recovery
        // later in this verifier lowers a real actor's hunger and requires the
        // production runtime to request, haul, consume, and emit the primary
        // meal event; accepting this typed pre-demand state does not weaken the
        // end-to-end liveness proof.
        return role == FacilityRole.Meal
            && reason != null
            && reason.StartsWith(
                "meal unavailable: DeliveryPending",
                StringComparison.Ordinal);
    }

    private IEnumerator VerifyFocusedFloorRestWithFacilityTeardown(
        CharacterActor actor,
        IReadOnlyList<CharacterActor> party,
        ICharacterDeprivationQuery deprivation,
        IWorldItemStackRuntime items,
        IGameClock clock,
        IFacilityCandidateCache facilities,
        IDungeonGameSaveService saves,
        IDungeonGridBuildingControllerProvider buildingControllerProvider)
    {
        const string prefix = "FOCUSED_FLOOR_REST";
        Grid grid = null;
        bool hasGrid = actor?.Brain != null
            && actor.Brain.TryGetRuntimeGrid(out grid);
        if (actor?.Brain == null
            || facilities == null
            || saves == null
            || buildingControllerProvider?.Controller == null
            || !hasGrid)
        {
            Check(false,
                prefix + "_ROW_FIXTURE_READY",
                $"actor={actor != null};brain={actor?.Brain != null};"
                + $"facilities={facilities != null};saves={saves != null};"
                + $"controller={buildingControllerProvider?.Controller != null};grid={grid != null}");
            yield break;
        }

        Dictionary<string, bool> pausedByCharacterId = new(StringComparer.Ordinal);
        foreach (CharacterActor member in party ?? Array.Empty<CharacterActor>())
        {
            if (member == null
                || !CharacterPersistentIdentity.TryGet(member, out CharacterId memberId))
            {
                continue;
            }

            pausedByCharacterId[memberId.Value] = member.IsAiPaused();
            member.SetAiPaused(true);
            EndFocusedExternalIntent(member.Brain);
            member.Brain?.StopCurrentActionForReplan(
                "primitive-floor-rest-fixture-quiesce");
            member.GetAbility<AbilityMove>()?.CancelActiveMovement();
        }

        float settleDeadline = Time.realtimeSinceStartup + 4f;
        int stableFrames = 0;
        while (Time.realtimeSinceStartup < settleDeadline && stableFrames < 2)
        {
            bool settled = pausedByCharacterId.Keys.All(characterId =>
            {
                CharacterActor member = FindActiveActor(new CharacterId(characterId));
                return member?.Brain != null
                    && !member.Brain.HasRunningAction
                    && !member.Brain.IsExternallyDrivenActionActive
                    && member.GetAbility<AbilityMove>()
                        ?.HasActiveMovementRoutineForDiagnostics != true;
            });
            stableFrames = settled ? stableFrames + 1 : 0;
            yield return null;
        }
        Check(stableFrames >= 2,
            prefix + "_PARTY_QUIESCED",
            $"stableFrames={stableFrames};party={pausedByCharacterId.Count}");

        yield return DrainFacilityCandidateIndex(
            facilities,
            prefix + "_BASELINE_INDEX_READY");
        List<string> baselineRestFacilities = SnapshotFacilityIdentity(
            facilities.GetCandidates(grid, FacilityRole.Rest));
        Check(baselineRestFacilities.Count > 0,
            prefix + "_AUTHORED_REST_PRESENT",
            baselineRestFacilities.Count > 0
                ? string.Join(" | ", baselineRestFacilities)
                : "no indexed Rest facility to isolate");
        if (baselineRestFacilities.Count == 0 || stableFrames < 2)
        {
            RestorePausedStates(pausedByCharacterId);
            yield break;
        }

        DungeonGameSaveData baseline = null;
        DungeonGameRestoreReport restoreReport = null;
        bool restored = false;
        bool teardownSucceeded = false;
        try
        {
            baseline = saves.Capture();
            Check(baseline != null,
                prefix + "_BASELINE_CAPTURED",
                baseline != null
                    ? $"sections={baseline.sections?.Count ?? 0};facilities={baselineRestFacilities.Count}"
                    : "save capture returned null");
            if (baseline != null)
            {
                List<BuildableObject> restFacilities = facilities
                    .GetCandidates(grid, FacilityRole.Rest)
                    .Where(candidate => candidate != null && !candidate.isDestroy)
                    .Distinct()
                    .OrderBy(candidate => candidate.RequirePersistentInstanceId().Value,
                        StringComparer.Ordinal)
                    .ToList();
                List<string> teardownResults = new(restFacilities.Count);
                teardownSucceeded = true;
                foreach (BuildableObject restFacility in restFacilities)
                {
                    string facilityId = restFacility.RequirePersistentInstanceId().Value;
                    Vector2Int position = restFacility.centerPos;
                    bool destroyed = buildingControllerProvider.Controller.TryDestroyBuilding(
                        restFacility,
                        out string message);
                    teardownResults.Add(
                        $"{facilityId}@{position}:destroyed={destroyed}:message={message}");
                    teardownSucceeded &= destroyed;
                }
                Check(teardownSucceeded,
                    prefix + "_AUTHORED_REST_TEARDOWN",
                    string.Join(" | ", teardownResults));

                yield return DrainFacilityCandidateIndex(
                    facilities,
                    prefix + "_TEARDOWN_INDEX_READY");
                IReadOnlyList<BuildableObject> remainingRest =
                    facilities.GetCandidates(grid, FacilityRole.Rest);
                bool noRestRemains = remainingRest.All(candidate =>
                    candidate == null || candidate.isDestroy);
                Check(teardownSucceeded && noRestRemains,
                    prefix + "_NO_REST_AFTER_TEARDOWN",
                    $"teardown={teardownSucceeded};remaining="
                    + string.Join(" | ", SnapshotFacilityIdentity(remainingRest)));

                if (teardownSucceeded && noRestRemains)
                {
                    yield return VerifyFocusedPrimitive<AIPrimitiveFloorRest>(
                        actor,
                        deprivation,
                        CharacterCondition.SLEEP,
                        FacilityRole.Rest,
                        "survival:floor-rest",
                        string.Empty,
                        0,
                        items,
                        clock);
                }
            }
        }
        finally
        {
            try
            {
                if (baseline != null)
                {
                    restored = saves.TryRestore(
                        CloneSave(baseline),
                        out restoreReport);
                }
            }
            finally
            {
                // This finally also covers capture/teardown/action exceptions.
                // Full restore can replace CharacterActor instances, so resolve
                // every pause owner again by persistent identity.
                RestorePausedStates(pausedByCharacterId);
            }
        }

        Check(restored,
            prefix + "_BASELINE_RESTORE",
            restoreReport?.ToString() ?? "restore was not attempted");
        if (!restored)
        {
            RestorePausedStates(pausedByCharacterId);
            yield break;
        }

        CharacterActor restoredActor = FindActiveActor(focusedVerificationActorId);
        focusedVerificationActor = restoredActor;
        foreach (string characterId in pausedByCharacterId.Keys)
        {
            FindActiveActor(new CharacterId(characterId))?.SetAiPaused(true);
        }

        yield return null;
        yield return DrainFacilityCandidateIndex(
            facilities,
            prefix + "_RESTORED_INDEX_READY");
        if (restoredActor?.Brain != null
            && restoredActor.Brain.TryGetRuntimeGrid(out Grid restoredGrid))
        {
            List<string> restoredRestFacilities = SnapshotFacilityIdentity(
                facilities.GetCandidates(restoredGrid, FacilityRole.Rest));
            Check(baselineRestFacilities.SequenceEqual(
                    restoredRestFacilities,
                    StringComparer.Ordinal),
                prefix + "_EXACT_FACILITY_RESTORE",
                $"before=[{string.Join(" | ", baselineRestFacilities)}];"
                + $"after=[{string.Join(" | ", restoredRestFacilities)}]");
        }
        else
        {
            Check(false,
                prefix + "_EXACT_FACILITY_RESTORE",
                $"restoredActor={restoredActor != null};grid=False");
        }

        RestorePausedStates(pausedByCharacterId);
        focusedVerificationActor = restoredActor;
    }

    private IEnumerator VerifyFocusedPrimitive<TAction>(
        CharacterActor actor,
        ICharacterDeprivationQuery deprivation,
        CharacterCondition targetNeed,
        FacilityRole authoredFacilityRole,
        string actionId,
        string consumedItemId,
        int expectedItemCost,
        IWorldItemStackRuntime items,
        IGameClock clock)
        where TAction : AIPrimitiveSurvivalAction
    {
        string checkPrefix = "FOCUSED_" + actionId
            .Replace("survival:", string.Empty)
            .Replace('-', '_')
            .ToUpperInvariant();
        if (actor?.Brain == null || actor.Stats == null)
        {
            Check(false, checkPrefix + "_SETUP", "focused actor or brain is missing");
            yield break;
        }

        AIAction[] originalActions = actor.Brain.availableActions;
        bool actorWasPaused = actor.IsAiPaused();
        AbilityMove move = actor.GetAbility<AbilityMove>();
        AIAction focusedAction = originalActions?
            .FirstOrDefault(candidate => candidate?.actionset is TAction);
        Check(focusedAction != null,
            checkPrefix + "_REGISTERED",
            focusedAction != null ? focusedAction.actionset.GetType().Name : "missing");
        if (focusedAction == null)
        {
            yield break;
        }
        actor.SetAiPaused(true);
        try
        {
            yield return SettleFocusedActor(
                actor,
                move,
                checkPrefix + "_PREVIOUS_ACTION_SETTLED");
            if (actor.Brain.HasRunningAction
                || actor.Brain.IsExternallyDrivenActionActive
                || move?.HasActiveMovementRoutineForDiagnostics == true)
            {
                yield break;
            }

            ResetNeeds(actor);
            CharacterNeedResponseProfile response =
                actor.Stats.GetNeedResponse(targetNeed);
            // Meal facilities with a pending material-delivery pipeline remain
            // the authored plan at ordinary emergency urgency. Exercise the
            // production primitive-meal bypass only at the critical wait
            // boundary where that queue can no longer complete safely.
            float focusedNeed = typeof(TAction) == typeof(AIPrimitiveFieldMeal)
                ? 0.5f
                : Mathf.Max(1f, response.emergencyStart - 1f);
            SetNeed(
                actor,
                targetNeed,
                focusedNeed);
            float needBefore = GetNeed(actor, targetNeed);
            int countBefore = GetCount(actionId);
            int physicalItemCountBefore =
                GetPrimitivePhysicalItemCount(actionId);

            actor.Brain.availableActions = new[] { focusedAction };
            GridPathSearchResult authoredFacilitySearch = null;
            float pathDeadline = Time.realtimeSinceStartup + 2f;
            while (authoredFacilitySearch == null
                && Time.realtimeSinceStartup < pathDeadline)
            {
                authoredFacilitySearch = actor.Brain.GetPathSearch(actor);
                if (authoredFacilitySearch == null)
                    yield return null;
            }

            if (typeof(TAction) == typeof(AIPrimitiveFieldMeal))
            {
                bool spawned = items.SpawnItemAt(
                    consumedItemId,
                    expectedItemCost,
                    actor.GetNowXY(),
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawnedQuantity);
                Check(
                    spawned && spawnedQuantity == expectedItemCost,
                    checkPrefix + "_PHYSICAL_SOURCE",
                    $"spawned={spawned};quantity={spawnedQuantity};"
                    + $"position={actor.GetNowXY()}");

            }

            int itemBefore = string.IsNullOrWhiteSpace(consumedItemId)
                ? 0
                : CountItem(items, consumedItemId);
            string primitiveMealReason = string.Empty;
            bool needsPrimitiveMeal = false;
            bool primitiveFallbackAllowed = false;
            bool canStart = false;
            bool preferred = false;

            // The fixture pauses the actor to make the authored single-action
            // catalog atomic. Consumables deliberately projects CanRunAi as
            // actor availability, so evaluating while paused manufactures a
            // CharacterMissing false-negative. Open one synchronous admission
            // window with no yield, then close it before the scheduler gets a
            // frame. The later unpause remains the sole production execution
            // wake-up.
            actor.SetAiPaused(false);
            try
            {
                needsPrimitiveMeal =
                    typeof(TAction) != typeof(AIPrimitiveFieldMeal)
                    || deprivation.NeedsPrimitiveMeal(
                        actor,
                        out primitiveMealReason);
                primitiveFallbackAllowed =
                    AIPrimitiveSurvivalAction.CanUsePrimitiveFallback(
                        actor,
                        authoredFacilityRole,
                        targetNeed);
                canStart = focusedAction.actionset.CanStart(actor);
                preferred =
                    actor.Brain.PreferActionOnNextDecision<TAction>(180f);
            }
            finally
            {
                actor.SetAiPaused(true);
            }
            bool authoredFacilityPresent = authoredFacilitySearch != null
                && FacilityCandidateScorer.HasCandidate(
                    actor,
                    authoredFacilitySearch,
                    authoredFacilityRole);
            if (authoredFacilityPresent)
            {
                Check(!canStart,
                    checkPrefix + "_SUPPRESSED_BY_FACILITY",
                    $"canStart={canStart}; role={authoredFacilityRole}; need={needBefore:0.##}");
                yield break;
            }

            Check(canStart && preferred,
                checkPrefix + "_AI_ELIGIBLE",
                $"canStart={canStart}; preferred={preferred}; need={needBefore:0.##}; "
                    + $"path={(authoredFacilitySearch != null ? "ready" : "missing")};"
                    + $"needsPrimitiveMeal={needsPrimitiveMeal};"
                    + $"mealReason={primitiveMealReason};"
                    + $"fallbackAllowed={primitiveFallbackAllowed}");
            if (!canStart || !preferred)
                yield break;

            long startCountBefore = actor.Brain.RuntimeActionStartCount;
            int externalTransitionsBefore =
                actor.Brain.ExternalIntentTransitionCount;
            // SetAiPaused(false) is the sole scheduler wake. The action list,
            // need state and preference were all committed atomically while
            // paused, so no unrelated action can win an intermediate tick.
            actor.SetAiPaused(false);

            Time.timeScale = VerificationTimeScale;
            float deadline = clock.Time + 240f;
            float realtimeDeadline = Time.realtimeSinceStartup + 15f;
            while (GetCount(actionId) == countBefore
                && clock.Time < deadline
                && Time.realtimeSinceStartup < realtimeDeadline
                && actor != null
                && !actor.IsDead)
            {
                Time.timeScale = VerificationTimeScale;
                yield return null;
            }

            // The completion callback pauses new scheduler admission in the
            // same frame. The primitive runner still owns its coroutine and
            // executes its finally block, so give that production owner one
            // frame to end the external intent before clearing the completed
            // Brain action and any movement presentation it left behind.
            yield return null;
            bool externalEndedNaturally =
                !actor.Brain.IsExternallyDrivenActionActive;
            actor.Brain.StopCurrentActionForReplan(
                "primitive-focused-completed-event");
            move?.CancelActiveMovement();
            yield return WaitForFocusedTerminal(
                actor,
                move,
                checkPrefix + "_TERMINAL_SETTLED");
            // The primitive completion event is published before the owning
            // scheduler action consumes its terminal frame. Pausing in that
            // event frame can freeze HasRunningAction after the external
            // intent has already ended. Keep the isolated single-action
            // catalog running until the production terminal is observed, then
            // fence the actor before inspecting conservation state.
            actor.SetAiPaused(true);

            bool productionStarted =
                actor.Brain.RuntimeActionStartCount > startCountBefore
                && actor.Brain.ExternalIntentTransitionCount
                    > externalTransitionsBefore;
            Check(
                productionStarted && externalEndedNaturally,
                checkPrefix + "_PRODUCTION_STARTED",
                $"actionStarts={startCountBefore}->{actor.Brain.RuntimeActionStartCount}; "
                + $"externalTransitions={externalTransitionsBefore}->"
                + actor.Brain.ExternalIntentTransitionCount
                + $"; externalEndedNaturally={externalEndedNaturally}");

            float needAfter = GetNeed(actor, targetNeed);
            int itemAfter = string.IsNullOrWhiteSpace(consumedItemId)
                ? 0
                : CountItem(items, consumedItemId);
            bool completed = GetCount(actionId) == countBefore + 1;
            int eventPhysicalItemCost =
                GetPrimitivePhysicalItemCount(actionId)
                - physicalItemCountBefore;
            bool itemConserved = eventPhysicalItemCost == expectedItemCost
                && (expectedItemCost == 0
                    || itemAfter <= itemBefore - expectedItemCost);
            Check(completed && needAfter > needBefore && itemConserved,
                checkPrefix + "_COMPLETED",
                $"events={countBefore}->{GetCount(actionId)}; "
                    + $"need={needBefore:0.##}->{needAfter:0.##}; "
                    + $"item={itemBefore}->{itemAfter}; expectedCost={expectedItemCost}; "
                    + $"eventPhysicalCost={eventPhysicalItemCost}; "
                    + $"action={actor.Brain.CurrentActionDebugLabel}");
        }
        finally
        {
            actor.SetAiPaused(true);
            EndFocusedExternalIntent(actor.Brain);
            actor.Brain.StopCurrentActionForReplan(
                "primitive-focused-verifier-row-cleanup");
            move?.CancelActiveMovement();
            actor.Brain.availableActions = originalActions;
            actor.SetAiPaused(actorWasPaused);
        }
    }

    private IEnumerator SettleFocusedActor(
        CharacterActor actor,
        AbilityMove move,
        string checkId)
    {
        actor.SetAiPaused(true);
        EndFocusedExternalIntent(actor.Brain);
        actor.Brain.StopCurrentActionForReplan(
            "primitive-focused-verifier-row-setup");
        move?.CancelActiveMovement();

        float deadline = Time.realtimeSinceStartup + 3f;
        int stableFrames = 0;
        while (Time.realtimeSinceStartup < deadline && stableFrames < 2)
        {
            bool settled = !actor.Brain.HasRunningAction
                && !actor.Brain.IsExternallyDrivenActionActive
                && move?.HasActiveMovementRoutineForDiagnostics != true;
            stableFrames = settled ? stableFrames + 1 : 0;
            yield return null;
        }

        bool finalSettled = !actor.Brain.HasRunningAction
            && !actor.Brain.IsExternallyDrivenActionActive
            && move?.HasActiveMovementRoutineForDiagnostics != true;
        Check(
            finalSettled && stableFrames >= 2,
            checkId,
            $"running={actor.Brain.HasRunningAction}; "
            + $"external={actor.Brain.IsExternallyDrivenActionActive}; "
            + $"movement={move?.HasActiveMovementRoutineForDiagnostics == true}; "
            + $"stableFrames={stableFrames}; action={actor.Brain.CurrentActionDebugLabel}");
    }

    private IEnumerator WaitForFocusedTerminal(
        CharacterActor actor,
        AbilityMove move,
        string checkId)
    {
        float deadline = Time.realtimeSinceStartup + 3f;
        int stableFrames = 0;
        while (Time.realtimeSinceStartup < deadline && stableFrames < 2)
        {
            bool settled = !actor.Brain.HasRunningAction
                && !actor.Brain.IsExternallyDrivenActionActive
                && move?.HasActiveMovementRoutineForDiagnostics != true;
            stableFrames = settled ? stableFrames + 1 : 0;
            yield return null;
        }

        bool finalSettled = !actor.Brain.HasRunningAction
            && !actor.Brain.IsExternallyDrivenActionActive
            && move?.HasActiveMovementRoutineForDiagnostics != true;
        Check(
            finalSettled && stableFrames >= 2,
            checkId,
            $"running={actor.Brain.HasRunningAction}; "
            + $"external={actor.Brain.IsExternallyDrivenActionActive}; "
            + $"movement={move?.HasActiveMovementRoutineForDiagnostics == true}; "
            + $"stableFrames={stableFrames}; action={actor.Brain.CurrentActionDebugLabel}");
    }

    private static void EndFocusedExternalIntent(AIBrain brain)
    {
        if (brain?.IsExternallyDrivenActionActive != true)
            return;

        string ownerId = brain.ExternalIntentOwnerId;
        if (!string.IsNullOrWhiteSpace(ownerId))
            brain.EndExternallyDrivenAction(ownerId, clearFailures: false);
    }

    private IEnumerator DrainFacilityCandidateIndex(
        IFacilityCandidateCache facilities,
        string checkId)
    {
        if (facilities == null)
        {
            Check(false, checkId, "facility candidate cache missing");
            yield break;
        }

        int versionBefore = facilities.CandidateIndexVersion;
        int processed = 0;
        int passes = 0;
        int stableFrames = 0;
        int observedVersion = versionBefore;
        float deadline = Time.realtimeSinceStartup + 5f;
        while (stableFrames < 2 && Time.realtimeSinceStartup < deadline)
        {
            if (facilities.HasPendingIndexBuild)
            {
                processed += facilities.AdvanceIndex(1.0);
                passes++;
            }

            int currentVersion = facilities.CandidateIndexVersion;
            bool stable = !facilities.HasPendingIndexBuild
                && currentVersion == observedVersion;
            stableFrames = stable ? stableFrames + 1 : 0;
            observedVersion = currentVersion;
            yield return null;
        }

        Check(!facilities.HasPendingIndexBuild && stableFrames >= 2,
            checkId,
            $"pending={facilities.HasPendingIndexBuild};passes={passes};"
            + $"processed={processed};stableFrames={stableFrames};"
            + $"version={versionBefore}->"
            + facilities.CandidateIndexVersion);
    }

    private static List<string> SnapshotFacilityIdentity(
        IEnumerable<BuildableObject> facilities)
    {
        return (facilities ?? Array.Empty<BuildableObject>())
            .Where(candidate => candidate != null && !candidate.isDestroy)
            .Select(candidate =>
            {
                string persistentId = candidate
                    .RequirePersistentInstanceId().Value;
                int definitionId = candidate.BuildingData != null
                    ? candidate.BuildingData.id
                    : -1;
                return $"{persistentId}:definition={definitionId}:"
                    + $"position={candidate.centerPos.x},{candidate.centerPos.y}";
            })
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
    }

    private static CharacterActor FindActiveActor(CharacterId characterId)
    {
        if (!characterId.IsValid)
        {
            return null;
        }

        return UnityEngine.Object.FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate != null
                && !candidate.IsDead
                && candidate.CurrentLifecycleState == CharacterLifecycleState.Active
                && CharacterPersistentIdentity.TryGet(candidate, out CharacterId candidateId)
                && candidateId.Equals(characterId));
    }

    private static void RestorePausedStates(
        IReadOnlyDictionary<string, bool> pausedByCharacterId)
    {
        if (pausedByCharacterId == null)
        {
            return;
        }

        foreach (KeyValuePair<string, bool> pair in pausedByCharacterId)
        {
            CharacterActor actor = FindActiveActor(new CharacterId(pair.Key));
            actor?.SetAiPaused(pair.Value);
        }
    }

    private static DungeonGameSaveData CloneSave(DungeonGameSaveData source)
    {
        return source != null
            ? JsonUtility.FromJson<DungeonGameSaveData>(JsonUtility.ToJson(source))
            : null;
    }

    private void CompleteVerification()
    {
        primitiveSubscription?.Dispose();
        mealSubscription?.Dispose();
        deathSubscription?.Dispose();
        Time.timeScale = originalTimeScale;
        string sourceDigest = string.Empty;
        string sceneDigest = string.Empty;
        if (!string.IsNullOrEmpty(DurableRequestMode)
            && !PrimitiveStartSurvivalPlayModeRequestRunner
                .TryValidateCompletion(
                    DurableRequestMode,
                    out sourceDigest,
                    out sceneDigest,
                    out string requestFailure))
        {
            failures.Add("DURABLE_REQUEST_INVALID: " + requestFailure);
        }
        report.Insert(0, failures.Count == 0 ? "PASS" : "FAIL");
        if (!string.IsNullOrEmpty(DurableRequestMode))
        {
            report.Add("durableRequestMode=" + DurableRequestMode);
            report.Add("currentSourceDigest=" + sourceDigest);
            report.Add("gameplaySceneSha256=" + sceneDigest);
        }
        report.Add("primitive-counts=" + string.Join(", ", primitiveCounts
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}:{pair.Value}")));
        report.Add($"physical-field-meals={physicalFieldMeals}");
        report.Add($"physical-meals={physicalMeals}");
        report.AddRange(failures.Select(value => "FAILURE " + value));
        File.WriteAllLines(ActiveReportPath, report);
        if (failures.Count == 0)
        {
            Debug.Log("[PrimitiveStartSurvivalPlayModeVerifier] PASS "
                + string.Join(" | ", report));
        }
        else
        {
            Debug.LogError("[PrimitiveStartSurvivalPlayModeVerifier] FAIL "
                + string.Join(" | ", failures));
        }
        if (!string.IsNullOrEmpty(DurableRequestMode))
        {
            PrimitiveStartSurvivalPlayModeRequestRunner.CompleteRun(
                DurableRequestMode,
                ActiveReportPath);
        }
        Destroy(gameObject);
    }

    private void VerifyStarterSupplies(IWorldItemStackRuntime items)
    {
        Check(CountItem(items, "food:preserved-ration") >= 24,
            "START_RATIONS",
            $"count={CountItem(items, "food:preserved-ration")}");
        Check(CountItem(items, "resource:clean-water") >= 30,
            "START_WATER",
            $"count={CountItem(items, "resource:clean-water")}");
        Check(CountItem(items, "material:lumber") >= 15,
            "START_LUMBER",
            $"count={CountItem(items, "material:lumber")}");
        Check(CountItem(items, "material:cloth") >= 9,
            "START_CLOTH",
            $"count={CountItem(items, "material:cloth")}");
        Check(CountItem(items, "craft:fermented-vinegar") == 0,
            "NO_VINEGAR_AS_FOOD",
            $"count={CountItem(items, "craft:fermented-vinegar")}");
        Check(CountItem(items, "captivity:restraints") == 0,
            "NO_RESTRAINTS_AS_GENERAL",
            $"count={CountItem(items, "captivity:restraints")}");
    }

    private static DungeonRuntimeLifetimeScope FindRuntimeScope() =>
        Resources.FindObjectsOfTypeAll<DungeonRuntimeLifetimeScope>()
            .Where(candidate => candidate != null
                && candidate.gameObject.scene.IsValid()
                && candidate.Container != null)
            .OrderBy(candidate => candidate.gameObject.scene.buildIndex)
            .ThenBy(candidate => candidate.GetInstanceID())
            .FirstOrDefault();

    private static DungeonRuntimeLifetimeScope FindRuntimeScopeForParty(
        IReadOnlyList<CharacterActor> party)
    {
        CharacterActor anchor = party?
            .FirstOrDefault(actor => actor != null && actor.gameObject.scene.IsValid());
        if (anchor == null)
        {
            return null;
        }

        int sceneHandle = anchor.gameObject.scene.handle;
        return Resources.FindObjectsOfTypeAll<DungeonRuntimeLifetimeScope>()
            .Where(candidate => candidate != null
                && candidate.gameObject.scene.IsValid()
                && candidate.gameObject.scene.handle == sceneHandle
                && candidate.Container != null)
            .OrderBy(candidate => candidate.GetInstanceID())
            .FirstOrDefault();
    }

    private static CharacterActor[] ResolveParty()
    {
        return CharacterActorCollection.DistinctByGameObject(
                UnityEngine.Object.FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None))
            .Where(actor => actor != null
                && !actor.IsDead
                && actor.CurrentLifecycleState == CharacterLifecycleState.Active
                && (actor.Role == CharacterRole.Owner
                    || actor.Identity?.PersistentId?.StartsWith(
                        "character:staff:",
                        StringComparison.Ordinal) == true))
            .OrderBy(actor => actor.Identity?.PersistentId, StringComparer.Ordinal)
            .Take(3)
            .ToArray();
    }

    private static string DescribeCharacterIds(
        IEnumerable<CharacterActor> actors)
    {
        return actors == null
            ? "<null>"
            : string.Join(",", actors
                .Where(actor => actor != null)
                .Select(actor =>
                    $"{actor.Identity?.PersistentId ?? "<missing-id>"}:"
                    + $"active={actor.isActiveAndEnabled}:"
                    + $"published={!actor.IsUnpublishedComposition}:"
                    + $"detached={actor.IsDetachedRestoreCandidate}")
                .OrderBy(value => value, StringComparer.Ordinal));
    }

    private static void ResetNeeds(CharacterActor actor)
    {
        if (actor?.Stats == null)
        {
            return;
        }
        CharacterCondition[] needs =
        {
            CharacterCondition.HUNGER,
            CharacterCondition.THIRST,
            CharacterCondition.SLEEP,
            CharacterCondition.EXCRETION,
            CharacterCondition.HYGIENE,
            CharacterCondition.FUN
        };
        foreach (CharacterCondition need in needs)
        {
            if (actor.Stats.TryGetConditionValue(need, out float current))
            {
                actor.ChangesStat(need, 100f - current);
            }
        }
    }

    private static void SetNeed(
        CharacterActor actor,
        CharacterCondition condition,
        float target)
    {
        if (actor?.Stats != null
            && actor.Stats.TryGetConditionValue(condition, out float current))
        {
            actor.ChangesStat(condition, target - current);
        }
    }

    private static void ResetUnrelatedNeeds(
        CharacterActor actor,
        CharacterCondition preservedCondition)
    {
        if (actor?.Stats == null)
        {
            return;
        }

        CharacterCondition[] needs =
        {
            CharacterCondition.HUNGER,
            CharacterCondition.THIRST,
            CharacterCondition.SLEEP,
            CharacterCondition.EXCRETION,
            CharacterCondition.HYGIENE,
            CharacterCondition.FUN
        };
        foreach (CharacterCondition need in needs)
        {
            if (need != preservedCondition
                && actor.Stats.TryGetConditionValue(need, out float current))
            {
                actor.ChangesStat(need, 100f - current);
            }
        }
    }

    private static Dictionary<FacilityRole, List<string>> CapturePrimaryRoleIdentity(
        IFacilityCandidateCache facilities,
        Grid grid)
    {
        Dictionary<FacilityRole, List<string>> result = new();
        foreach (FacilityRole role in RequiredPrimaryRoles)
        {
            result[role] = facilities.GetCandidates(grid, role)
                .Where(candidate => candidate != null && !candidate.isDestroy)
                .Select(candidate => candidate.RequirePersistentInstanceId().Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }

        return result;
    }

    private List<string> EnsurePrimaryServiceFixture(
        IReadOnlyList<CharacterActor> startingParty,
        DungeonRuntimeLifetimeScope scope,
        Grid grid,
        IFacilityCandidateCache facilities,
        DungeonStoryGridBuildingController controller)
    {
        List<string> rows = new();
        IReadOnlyDictionary<int, BuildingSO> catalog =
            scope.Container.Resolve<IDataCatalog>().GetData<BuildingSO>();
        Vector2Int origin = startingParty[0].GetNowXY();
        foreach (FacilityRole role in RequiredPrimaryRoles)
        {
            if (facilities.GetCandidates(grid, role)
                .Any(candidate => candidate != null && !candidate.isDestroy))
            {
                rows.Add($"{role}:already-present");
                continue;
            }

            BuildingSO[] definitions = catalog.Values
                .Where(definition => definition != null
                    && definition.Facility != null
                    && (definition.Facility.roles & role) != 0)
                .OrderBy(definition => definition.width * definition.height)
                .ThenBy(definition => definition.id)
                .ToArray();
            bool placed = false;
            string lastMessage = definitions.Length == 0
                ? "authored definition missing"
                : "no valid placement cell";
            foreach (BuildingSO definition in definitions)
            {
                Vector2Int[] candidates = grid.SearchPath(origin)
                    .GetReachablePositions()
                    .Where(position => definition.GetGridPosList(position)
                        .All(cell => grid.IsValidGridPos(cell)
                            && grid.GetGridCell(cell)
                                ?.GetOccupant(definition.layer) == null))
                    .OrderBy(position =>
                        Mathf.Abs(position.x - origin.x)
                        + Mathf.Abs(position.y - origin.y))
                    .ThenBy(position => position.y)
                    .ThenBy(position => position.x)
                    .ToArray();
                foreach (Vector2Int position in candidates)
                {
                    bool accepted = controller.TryPlaceInitialBuildings(
                        new[]
                        {
                            new InitialBuildInfo
                            {
                                Position = position,
                                Building = definition
                            }
                        },
                        out lastMessage);
                    BuildableObject placedBuilding =
                        UnityEngine.Object.FindObjectsByType<BuildableObject>(
                                FindObjectsInactive.Exclude,
                                FindObjectsSortMode.None)
                            .FirstOrDefault(candidate => candidate != null
                                && !candidate.isDestroy
                                && candidate.id == definition.id
                                && candidate.centerPos == position);
                    if (placedBuilding == null)
                    {
                        continue;
                    }

                    bool usable = FacilityCandidateScorer.IsCandidate(
                        startingParty[0],
                        placedBuilding,
                        role,
                        out string rejectReason);
                    if (!usable)
                    {
                        controller.TryDestroyBuilding(
                            placedBuilding,
                            out string destroyMessage);
                        lastMessage = $"unusable={rejectReason};destroy={destroyMessage}";
                        continue;
                    }

                    rows.Add($"{role}:definition={definition.id}:"
                        + $"position={position}:accepted={accepted}:"
                        + $"persistentId={placedBuilding.RequirePersistentInstanceId().Value}:"
                        + $"message={lastMessage}");
                    placed = true;
                    break;
                }

                if (placed)
                {
                    break;
                }
            }

            if (!placed)
            {
                rows.Add($"{role}:FAILED:{lastMessage}");
            }
        }

        return rows;
    }

    private static List<BuildableObject> FindLiveWaterProducers() =>
        UnityEngine.Object.FindObjectsByType<BuildableObject>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where(candidate => candidate != null
                && !candidate.isDestroy
                && candidate.BuildingData != null
                && (candidate.BuildingData.GetAbility<BuildingWaterSourceAbility>() != null
                    || candidate.BuildingData.GetAbility<BuildingWaterProducerAbility>() != null))
            .OrderBy(candidate =>
                    candidate.RequirePersistentInstanceId().Value,
                StringComparer.Ordinal)
            .ToList();

    private static List<string> CaptureWaterProducerIdentity() =>
        FindLiveWaterProducers()
            .Select(candidate => candidate.RequirePersistentInstanceId().Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();

    private static string DescribePrimaryAuthority(
        IReadOnlyDictionary<FacilityRole, List<string>> roles,
        IReadOnlyList<string> waterProducers)
    {
        string roleText = string.Join(";", RequiredPrimaryRoles.Select(role =>
            role + "=[" + string.Join(",", roles != null
                && roles.TryGetValue(role, out List<string> values)
                    ? values
                    : Array.Empty<string>()) + "]"));
        return roleText + ";water=["
            + string.Join(",", waterProducers ?? Array.Empty<string>()) + "]";
    }

    private static string DescribePrimaryUsability(
        CharacterActor actor,
        IFacilityCandidateCache facilities,
        Grid grid) =>
        string.Join(";", RequiredPrimaryRoles.Select(role =>
            role + "=[" + string.Join(",", facilities
                .GetCandidates(grid, role)
                .Where(candidate => candidate != null && !candidate.isDestroy)
                .Select(candidate =>
                {
                    bool usable = FacilityCandidateScorer.IsCandidate(
                        actor,
                        candidate,
                        role,
                        out string reason);
                    return candidate.RequirePersistentInstanceId().Value
                        + $":usable={usable}:reason={reason}";
                })) + "]"));

    private static bool HasUsablePrimaryAuthority(
        CharacterActor actor,
        IFacilityCandidateCache facilities,
        Grid grid) =>
        RequiredPrimaryRoles.All(role => facilities
            .GetCandidates(grid, role)
            .Any(candidate => candidate != null
                && !candidate.isDestroy
                && IsPrimaryAuthorityReadyForDemand(
                    actor,
                    candidate,
                    role)))
        && CaptureWaterProducerIdentity().Count > 0;

    private List<CharacterActor> CreateSixAdultCohort(
        IReadOnlyList<CharacterActor> startingParty,
        DungeonRuntimeLifetimeScope scope,
        Grid grid,
        string phase) => CreatePopulationCohort(
            startingParty,
            scope,
            grid,
            phase,
            6);

    private List<CharacterActor> CreatePopulationCohort(
        IReadOnlyList<CharacterActor> startingParty,
        DungeonRuntimeLifetimeScope scope,
        Grid grid,
        string phase,
        int targetPopulation)
    {
        if (targetPopulation <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetPopulation));
        List<CharacterActor> cohort = (startingParty ?? Array.Empty<CharacterActor>())
            .Where(actor => actor != null)
            .Take(targetPopulation)
            .ToList();
        int authoredPartyCount = Math.Min(
            targetPopulation,
            (startingParty ?? Array.Empty<CharacterActor>()).Count(actor => actor != null));
        if (cohort.Count != authoredPartyCount
            || scope?.Container == null
            || grid == null)
        {
            return cohort;
        }

        ICharacterAiWorldRegistry world =
            scope.Container.Resolve<ICharacterAiWorldRegistry>();
        ICharacterAiSchedulingService scheduling =
            scope.Container.Resolve<ICharacterAiSchedulingService>();
        ICharacterNarrativeQuery narrative =
            scope.Container.Resolve<ICharacterNarrativeQuery>();
        ICharacterNarrativeCommand narrativeCommands =
            scope.Container.Resolve<ICharacterNarrativeCommand>();

        IWorldItemStackRuntime physicalItems =
            scope.Container.Resolve<IWorldItemStackRuntime>();
        HashSet<Vector2Int> protectedLooseSourceCells = physicalItems.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.Loose
                && stack.Quantity > 0)
            .SelectMany(stack => new[]
            {
                stack.Position,
                stack.Position + Vector2Int.left,
                stack.Position + Vector2Int.right,
                stack.Position + Vector2Int.up,
                stack.Position + Vector2Int.down
            })
            .ToHashSet();

        Vector2Int origin = cohort[0].GetNowXY();
        Vector2Int[] positions = grid.SearchPath(origin)
            .GetReachablePositions()
            .Where(grid.IsWalkable)
            // The cohort is a workload fixture, not an obstacle fixture. Never
            // park a paused synthetic adult on a live loose stack or one of its
            // cardinal pickup stands; doing so can make a later production
            // delivery appear unavailable even though the settlement has a
            // lawful route.
            .Where(position => !protectedLooseSourceCells.Contains(position))
            .Distinct()
            .OrderBy(position =>
                Mathf.Abs(position.x - origin.x) + Mathf.Abs(position.y - origin.y))
            .ThenBy(position => position.y)
            .ThenBy(position => position.x)
            .Take(targetPopulation)
            .ToArray();
        if (positions.Length < targetPopulation)
        {
            return cohort;
        }

        for (int index = 0; index < cohort.Count; index++)
        {
            CharacterActor actor = cohort[index];
            actor.SetAiPaused(true);
            actor.Brain?.StopCurrentActionForReplan(
                "v27-six-adult-cohort-position");
            actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
            actor.transform.position = grid.GetWorldPos(positions[index]);
        }

        CharacterActor source = cohort.FirstOrDefault(HasCompleteSurvivalCatalog)
            ?? cohort[0];
        AIActionSet[] authoredActions = source.Brain?.availableActions?
            .Where(action => action?.actionset != null)
            .Select(action => action.actionset)
            .ToArray()
            ?? Array.Empty<AIActionSet>();
        for (int index = cohort.Count; index < targetPopulation; index++)
        {
            string displayName = $"V27 Population {phase} {index + 1}";
            CharacterSO data = source.data;

            GameObject actorObject =
                CharacterAiPlanDebugFixtures.CreatePlayActorObject(displayName);
            outageTemporaryObjects.Add(actorObject);
            actorObject.SetActive(false);
            scope.Container.InjectGameObject(actorObject);
            CharacterActor created = actorObject.GetComponent<CharacterActor>();
            created.RefreshAbilityCache();
            created.Initialization(data);
            created.EnsureRuntimeState();
            scope.Container.InjectGameObject(actorObject);
            created.RefreshAbilityCache();

            scheduling.Unregister(created);
            world.UnregisterCharacter(created);
            world.UnregisterCharacterLifetime(created);
            if (source.Progression != null && created.Progression != null)
            {
                created.Progression.RestorePersistentState(
                    source.Progression.CapturePersistentState());
            }

            AbilityWork work = actorObject.GetComponent<AbilityWork>()
                ?? actorObject.AddComponent<AbilityWork>();
            scope.Container.InjectGameObject(actorObject);
            created.RefreshAbilityCache();
            created.Brain.availableActions = authoredActions
                .Select(actionSet => new AIAction { actionset = actionSet })
                .ToArray();
            created.transform.position = grid.GetWorldPos(positions[index]);
            created.SetLifecycleState(CharacterLifecycleState.Active);
            world.RegisterCharacterLifetime(created);
            world.RegisterCharacter(created);
            CharacterId createdId = CharacterPersistentIdentity.Require(created);
            if (!narrative.TryGet(createdId, out _))
            {
                narrativeCommands.Register(
                    createdId,
                    new CharacterSpeciesId(created.SpeciesTag),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    created.Progression?.GrowthState?.startingProficiencies);
            }
            work.Initializtion(data);
            actorObject.SetActive(true);
            if (created.Brain != null)
            {
                created.Brain.enabled = true;
            }
            if (created.BehaviorTree != null)
            {
                created.BehaviorTree.enabled = true;
            }
            scheduling.Unregister(created);
            scheduling.Register(created);
            created.SetAiPaused(true);
            cohort.Add(created);
        }

        return cohort;
    }

    private IEnumerator DrainCohortPathSearches(
        IReadOnlyList<CharacterActor> actors,
        string marker)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        int stableFrames = 0;
        while (Time.realtimeSinceStartup < deadline && stableFrames < 2)
        {
            bool ready = actors != null
                && actors.Count == 6
                && actors.All(actor => actor?.Brain?.GetPathSearch(actor) != null);
            stableFrames = ready ? stableFrames + 1 : 0;
            if (!ready)
            {
                foreach (CharacterActor actor in actors ?? Array.Empty<CharacterActor>())
                {
                    actor?.Brain?.RequestImmediateReplan(clearFailures: true);
                }
            }
            yield return null;
        }

        bool complete = stableFrames >= 2;
        Check(complete,
            marker,
            $"stableFrames={stableFrames};actors="
            + string.Join(",", (actors ?? Array.Empty<CharacterActor>())
                .Select(actor => actor == null
                    ? "null"
                    : CharacterPersistentIdentity.Require(actor).Value
                        + ":path=" + (actor.Brain?.GetPathSearch(actor) != null)
                        + ":deferred=" + (actor.Brain?.IsPathSearchDeferred ?? false))));
    }

    private IEnumerator DrainActorPathSearch(
        CharacterActor actor,
        string marker)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        int stableFrames = 0;
        while (Time.realtimeSinceStartup < deadline && stableFrames < 2)
        {
            bool ready = actor?.Brain?.GetPathSearch(actor) != null
                && !actor.Brain.IsPathSearchDeferred;
            stableFrames = ready ? stableFrames + 1 : 0;
            if (!ready)
            {
                actor?.Brain?.RequestImmediateReplan(clearFailures: true);
            }
            yield return null;
        }

        bool complete = stableFrames >= 2;
        Check(complete,
            marker,
            actor == null
                ? "actor=null"
                : CharacterPersistentIdentity.Require(actor).Value
                    + $":stableFrames={stableFrames}:path="
                    + (actor.Brain?.GetPathSearch(actor) != null)
                    + ":deferred=" + (actor.Brain?.IsPathSearchDeferred ?? false));
    }

    private static void RefreshObservedActorIds(
        ISet<string> observedActorIds,
        IEnumerable<CharacterActor> actors)
    {
        observedActorIds.Clear();
        foreach (CharacterActor actor in actors ?? Array.Empty<CharacterActor>())
        {
            if (CharacterPersistentIdentity.TryGet(actor, out CharacterId id))
            {
                observedActorIds.Add(id.Value);
            }
        }
    }

    private static bool IsExactLiveCohort(
        IReadOnlyCollection<CharacterActor> actors,
        int expectedCount) =>
        actors != null
        && actors.Count == expectedCount
        && actors.All(actor => actor != null
            && !actor.IsDead
            && actor.CurrentLifecycleState == CharacterLifecycleState.Active
            && actor.CurrentHealth > 0f);

    private static bool HasCompleteSurvivalCatalog(CharacterActor actor)
    {
        AIActionSet[] actions = actor?.Brain?.availableActions?
            .Where(action => action?.actionset != null)
            .Select(action => action.actionset)
            .ToArray()
            ?? Array.Empty<AIActionSet>();
        return actions.Any(action => action is AIPrimitiveFieldMeal)
            && actions.Any(action => action is AIDrink)
            && actions.Any(action => action is AIPrimitiveFloorRest)
            && actions.Any(action => action is AIPrimitiveLatrine)
            && actions.Any(action => action is AIPrimitiveBucketWash);
    }

    private static string DescribeSurvivalCatalog(CharacterActor actor)
    {
        string id = CharacterPersistentIdentity.TryGet(actor, out CharacterId actorId)
            ? actorId.Value
            : "missing";
        string[] actionNames = actor?.Brain?.availableActions?
            .Where(action => action?.actionset != null)
            .Select(action => action.actionset.GetType().Name)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray()
            ?? Array.Empty<string>();
        return $"{id}:[{string.Join(",", actionNames)}]";
    }

    private static string DescribeCohort(IEnumerable<CharacterActor> actors) =>
        string.Join(" | ", (actors ?? Array.Empty<CharacterActor>())
            .Select(actor => DescribeActor(actor)
                + $":lifecycle={actor?.CurrentLifecycleState}:"
                + $"paused={actor?.IsAiPaused()}:"
                + $"action={actor?.Brain?.CurrentActionDebugLabel}"));

    private static string OutageEventKey(CharacterId actorId, string actionId) =>
        actorId.Value + "\u001F" + (actionId ?? string.Empty);

    private static int GetEventCount(
        IReadOnlyDictionary<string, int> counts,
        CharacterId actorId,
        string actionId) =>
        counts != null
        && counts.TryGetValue(OutageEventKey(actorId, actionId), out int count)
            ? count
            : 0;

    private static void SetEmergencyNeed(
        CharacterActor actor,
        CharacterCondition condition)
    {
        CharacterNeedResponseProfile response = actor.Stats.GetNeedResponse(condition);
        SetNeed(actor, condition, Mathf.Max(1f, response.emergencyStart - 1f));
    }

    private static void ConfigureOutageNeeds(IReadOnlyList<CharacterActor> actors)
    {
        foreach (CharacterActor actor in actors)
        {
            ResetNeeds(actor);
        }
        SetEmergencyNeed(GetServiceTargetActor(actors, 0), CharacterCondition.HUNGER);
        SetEmergencyNeed(GetServiceTargetActor(actors, 1), CharacterCondition.THIRST);
        SetEmergencyNeed(GetServiceTargetActor(actors, 2), CharacterCondition.SLEEP);
        SetEmergencyNeed(GetServiceTargetActor(actors, 3), CharacterCondition.EXCRETION);
        SetEmergencyNeed(GetServiceTargetActor(actors, 4), CharacterCondition.HYGIENE);
    }

    private static int CountCompletedPhysicalActions(
        IReadOnlyDictionary<string, int> physicalCounts,
        IEnumerable<CharacterActor> actors,
        string actionId) =>
        (actors ?? Array.Empty<CharacterActor>())
            .Where(actor => CharacterPersistentIdentity.TryGet(actor, out _))
            .Sum(actor => GetEventCount(
                physicalCounts,
                CharacterPersistentIdentity.Require(actor),
                actionId));

    private static bool OutageTargetsCompleted(
        IReadOnlyList<CharacterActor> actors,
        IReadOnlyDictionary<string, int> primitiveCompleted,
        IReadOnlyDictionary<string, int> waterConsumed)
    {
        CharacterId hunger = CharacterPersistentIdentity.Require(GetServiceTargetActor(actors, 0));
        CharacterId thirst = CharacterPersistentIdentity.Require(GetServiceTargetActor(actors, 1));
        CharacterId sleep = CharacterPersistentIdentity.Require(GetServiceTargetActor(actors, 2));
        CharacterId excretion = CharacterPersistentIdentity.Require(GetServiceTargetActor(actors, 3));
        CharacterId hygiene = CharacterPersistentIdentity.Require(GetServiceTargetActor(actors, 4));
        return GetEventCount(
                primitiveCompleted,
                hunger,
                "survival:field-meal") >= 1
            && waterConsumed.TryGetValue(thirst.Value, out int drinks)
            && drinks >= 1
            && GetEventCount(
                primitiveCompleted,
                sleep,
                "survival:floor-rest") >= 1
            && GetEventCount(
                primitiveCompleted,
                excretion,
                "survival:primitive-latrine") >= 1
            && GetEventCount(
                primitiveCompleted,
                hygiene,
                "survival:bucket-wash") >= 1;
    }

    private static string DescribeOutageEvents(
        IReadOnlyList<CharacterActor> actors,
        IReadOnlyDictionary<string, int> primitiveStarted,
        IReadOnlyDictionary<string, int> primitiveCompleted,
        IReadOnlyDictionary<string, int> waterConsumed)
    {
        string[] actionIds =
        {
            "survival:field-meal",
            "drink:physical-water",
            "survival:floor-rest",
            "survival:primitive-latrine",
            "survival:bucket-wash"
        };
        return string.Join(" | ", actionIds.Select((actionId, index) =>
        {
            CharacterActor actor = GetServiceTargetActor(actors, index);
            CharacterId id = CharacterPersistentIdentity.Require(actor);
            int started = index == 1
                ? 0
                : GetEventCount(primitiveStarted, id, actionIds[index]);
            int completed = index == 1
                ? waterConsumed.TryGetValue(id.Value, out int count) ? count : 0
                : GetEventCount(primitiveCompleted, id, actionIds[index]);
            return $"{id.Value}:{actionId}:started={started}:completed={completed}:"
                + DescribeTargetAction(actor, index);
        }));
    }

    private static string DescribeTargetAction(CharacterActor actor, int index)
    {
        Type targetType = index switch
        {
            0 => typeof(AIPrimitiveFieldMeal),
            1 => typeof(AIDrink),
            2 => typeof(AIPrimitiveFloorRest),
            3 => typeof(AIPrimitiveLatrine),
            4 => typeof(AIPrimitiveBucketWash),
            _ => null
        };
        AIAction action = actor?.Brain?.availableActions?
            .FirstOrDefault(candidate => candidate?.actionset != null
                && candidate.actionset.GetType() == targetType);
        return action?.actionset == null
            ? "action=missing"
            : $"canStart={action.actionset.CanStart(actor)};"
                + $"path={actor.Brain.GetPathSearch(actor) != null};"
                + $"lastFailure={actor.Brain.LastActionFailure.Kind}:"
                + actor.Brain.LastActionFailure;
    }

    private static Dictionary<string, float> ConfigureRecoveryNeeds(
        IReadOnlyList<CharacterActor> actors)
    {
        CharacterCondition[] targets =
        {
            CharacterCondition.HUNGER,
            CharacterCondition.THIRST,
            CharacterCondition.SLEEP,
            CharacterCondition.EXCRETION,
            CharacterCondition.HYGIENE
        };
        foreach (CharacterActor actor in actors)
        {
            ResetNeeds(actor);
        }

        Dictionary<string, float> before = new(StringComparer.Ordinal);
        for (int index = 0; index < targets.Length; index++)
        {
            CharacterActor actor = GetServiceTargetActor(actors, index);
            CharacterNeedResponseProfile response =
                actor.Stats.GetNeedResponse(targets[index]);
            float demandValue = Mathf.Max(
                response.emergencyStart + 5f,
                Mathf.Min(response.routineStart, 40f));
            SetNeed(actor, targets[index], demandValue);
            before[OutageNeedKey(
                CharacterPersistentIdentity.Require(actor),
                targets[index])] = GetNeed(actor, targets[index]);
        }
        return before;
    }

    private static int CountRecoveredTargets(
        IReadOnlyList<CharacterActor> actors,
        IReadOnlyDictionary<string, float> before)
    {
        CharacterCondition[] targets =
        {
            CharacterCondition.HUNGER,
            CharacterCondition.THIRST,
            CharacterCondition.SLEEP,
            CharacterCondition.EXCRETION,
            CharacterCondition.HYGIENE
        };
        int recovered = 0;
        for (int index = 0; index < targets.Length; index++)
        {
            CharacterActor actor = GetServiceTargetActor(actors, index);
            string key = OutageNeedKey(
                CharacterPersistentIdentity.Require(actor),
                targets[index]);
            if (before.TryGetValue(key, out float original)
                && GetNeed(actor, targets[index]) > original + 0.5f)
            {
                recovered++;
            }
        }
        return recovered;
    }

    private static CharacterActor GetServiceTargetActor(
        IReadOnlyList<CharacterActor> actors,
        int serviceIndex)
    {
        if (actors == null || actors.Count < 3)
        {
            return null;
        }

        // The extra three live adults intentionally exercise scheduler and
        // world-registry population load. Their objects originate from an
        // editor fixture, so authoritative deprivation execution is proven by
        // the three production-composed starting actors instead.
        return actors[serviceIndex % 3];
    }

    private static string OutageNeedKey(
        CharacterId actorId,
        CharacterCondition condition) =>
        actorId.Value + "\u001F" + condition;

    private void DestroyOutageTemporaryObjects()
    {
        for (int index = outageTemporaryObjects.Count - 1; index >= 0; index--)
        {
            UnityEngine.Object target = outageTemporaryObjects[index];
            if (target == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
        outageTemporaryObjects.Clear();
    }

    private static string DescribeDay(
        int day,
        IEnumerable<CharacterActor> party,
        IWorldItemStackRuntime items,
        ICharacterDeprivationQuery deprivation)
    {
        string actors = string.Join(" | ", party.Select(actor =>
            DescribeActor(actor) + ":ai=" + DescribePrimitiveAi(actor, deprivation)));
        string stacks = string.Join(" | ", items.GetAllStacks()
            .Where(stack => stack != null
                && (stack.ItemId == "food:preserved-ration"
                    || stack.ItemId == "resource:clean-water"))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .Select(stack => $"{stack.StackId}:{stack.ItemId}:q={stack.Quantity}:"
                + $"available={stack.AvailableQuantity}:state={stack.State}:"
                + $"destination={stack.DestinationId}:pos={stack.Position}"));
        return $"day={day}; actors=[{actors}]; "
            + $"ration={CountItem(items, "food:preserved-ration")}; "
            + $"water={CountItem(items, "resource:clean-water")}; "
            + $"survival-stacks=[{stacks}]";
    }

    private static string DescribePrimitiveAi(
        CharacterActor actor,
        ICharacterDeprivationQuery deprivation)
    {
        if (actor == null)
        {
            return "missing";
        }

        bool meal = deprivation.NeedsPrimitiveMeal(actor, out string mealReason);
        bool rest = deprivation.NeedsPrimitiveRest(actor, out string restReason);
        bool relief = deprivation.NeedsPrimitiveRelief(actor, out string reliefReason);
        bool wash = deprivation.NeedsPrimitiveWash(actor, out string washReason);
        string registered = string.Join(",", actor.Brain?.availableActions?
            .Where(action => action?.actionset is AIPrimitiveSurvivalAction)
            .Select(action => $"{action.actionset.GetType().Name}:"
                + $"can={action.actionset.CanStart(actor)}:"
                + $"score={action.CalculateScore(actor):0.##}")
            ?? Array.Empty<string>());
        string candidates = string.Join(",", actor.Brain?.LastCandidateScores?
            .Select(candidate => $"{candidate.ActionLabel}:{candidate.Score:0.##}:"
                + candidate.Failure.Kind)
            ?? Array.Empty<string>());
        FacilityRole availableRoles = CharacterAiJobGiver.ResolveAvailableFacilityRoles(actor);
        AIBrain brain = actor.Brain;
        string facilityAuthority = DescribeFacilityAuthority(actor, brain);
        return $"canRun={actor.CanRunAi}:current={actor.Brain?.CurrentActionDebugLabel}:"
            + $"availableRoles={availableRoles}:registered={registered}:"
            + $"meal={meal}({mealReason}):"
            + $"rest={rest}({restReason}):relief={relief}({reliefReason}):"
            + $"wash={wash}({washReason}):candidates={candidates}:"
            + $"immediateDecisions={brain?.ImmediateDecisionRequestCount ?? 0}:"
            + $"lastImmediate={brain?.LastImmediateDecisionReason}:"
            + $"facilityAuthority={facilityAuthority}";
    }

    private static string DescribeFacilityAuthority(
        CharacterActor actor,
        AIBrain brain)
    {
        if (brain == null || !brain.TryGetRuntimeGrid(out Grid activeGrid))
        {
            return "active-grid-missing";
        }

        IFacilityCandidateCache cache = brain.RequireFacilityCandidateCache();
        GridPathSearchResult search = brain.GetPathSearch(actor);
        Grid searchGrid = search?.sourceGrid;
        IReadOnlyList<BuildableObject> activeMeals =
            cache.GetCandidates(activeGrid, FacilityRole.Meal);
        IReadOnlyList<BuildableObject> searchMeals = searchGrid != null
            ? cache.GetCandidates(searchGrid, FacilityRole.Meal)
            : Array.Empty<BuildableObject>();
        string rows = string.Join(",", searchMeals.Select(building =>
        {
            bool usable = FacilityCandidateScorer.IsCandidate(
                actor,
                building,
                FacilityRole.Meal,
                out string rejectReason);
            bool reachable = search != null
                && search.ContainsVisitableOccupant(building);
            return $"{building?.name}:usable={usable}:reachable={reachable}:"
                + $"reject={rejectReason}";
        }));
        return $"sameGrid={ReferenceEquals(activeGrid, searchGrid)}:"
            + $"activeMeal={activeMeals.Count}:searchMeal={searchMeals.Count}:"
            + $"rows=[{rows}]";
    }

    private static string DescribeActor(CharacterActor actor)
    {
        if (actor == null)
        {
            return "missing";
        }
        return $"{actor.Identity?.PersistentId}:hp={actor.CurrentHealth:0.##}:"
            + $"h={GetNeed(actor, CharacterCondition.HUNGER):0.##}:"
            + $"t={GetNeed(actor, CharacterCondition.THIRST):0.##}:"
            + $"s={GetNeed(actor, CharacterCondition.SLEEP):0.##}:"
            + $"e={GetNeed(actor, CharacterCondition.EXCRETION):0.##}:"
            + $"y={GetNeed(actor, CharacterCondition.HYGIENE):0.##}";
    }

    private static string DescribeDamageActivities(CharacterActor actor)
    {
        string id = actor?.Identity?.PersistentId ?? "missing";
        IReadOnlyList<CharacterActivityEvent> activities =
            actor?.LogComponent?.ActivityEntries;
        if (activities == null)
        {
            return id + ":log-unavailable";
        }

        string[] damage = activities
            .Where(value => value != null
                && string.Equals(
                    value.ActionId,
                    "health:damage",
                    StringComparison.Ordinal))
            .TakeLast(20)
            .Select(value =>
                $"value={value.Value:0.###},reason={value.ReasonCode},fact={value.FactText}")
            .ToArray();
        return id + ":" + (damage.Length == 0
            ? "none"
            : string.Join(" || ", damage));
    }

    private static string DescribeAiFailures(CharacterActor actor)
    {
        string id = actor?.Identity?.PersistentId ?? "missing";
        IReadOnlyList<CharacterActivityEvent> activities =
            actor?.LogComponent?.ActivityEntries;
        if (activities == null)
        {
            return id + ":log-unavailable";
        }

        string[] failures = activities
            .Where(value => value != null
                && string.Equals(
                    value.KindId,
                    CharacterActivityKinds.AiDecision,
                    StringComparison.Ordinal)
                && (string.Equals(
                        value.OutcomeId,
                        CharacterActivityOutcomes.Failed,
                        StringComparison.Ordinal)
                    || string.Equals(
                        value.OutcomeId,
                        CharacterActivityOutcomes.Blocked,
                        StringComparison.Ordinal)))
            .TakeLast(20)
            .Select(value => $"outcome={value.OutcomeId},reason={value.ReasonCode},fact={value.FactText}")
            .ToArray();
        return id + ":" + (failures.Length == 0
            ? "none"
            : string.Join(" || ", failures));
    }

    private static string DescribeHygieneRecoveryDiagnostics(
        CharacterActor target,
        CharacterActor helper,
        IWorldItemStackRuntime items)
    {
        const string ManualWaterPrefix = "plumbing:manual-water:";

        string stacks = items == null
            ? "items-unavailable"
            : string.Join(",", items.GetAllStacks()
                .Where(stack => stack != null
                    && !string.IsNullOrWhiteSpace(stack.DestinationId)
                    && stack.DestinationId.StartsWith(
                        ManualWaterPrefix,
                        StringComparison.Ordinal))
                .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
                .Select(stack => $"{stack.StackId}:{stack.ItemId}:"
                    + $"q={stack.Quantity}:r={stack.ReservedQuantity}:"
                    + $"state={stack.State}:pos={stack.Position}:"
                    + $"dest={stack.DestinationId}"));
        if (stacks.Length == 0)
        {
            stacks = "none";
        }

        return $"target=[{DescribeServiceActorRuntime(target)}];"
            + $"helper=[{DescribeServiceActorRuntime(helper)}];"
            + $"manualWater=[{stacks}]";
    }

    private static bool HasRoutedManualWater(
        IWorldItemStackRuntime items,
        BuildableObject fixture)
    {
        const string ManualWaterPrefix = "plumbing:manual-water:";
        if (fixture == null || !fixture.PersistentInstanceId.IsValid)
        {
            return false;
        }

        string expectedDestination = ManualWaterPrefix
            + fixture.PersistentInstanceId.Value;
        return items?.GetAllStacks().Any(stack => stack != null
            && stack.Quantity > 0
            && string.Equals(
                stack.DestinationId,
                expectedDestination,
                StringComparison.Ordinal)) == true;
    }

    private static bool HasBufferedManualWater(
        IWorldItemStackRuntime items,
        BuildableObject fixture)
    {
        const string ManualWaterPrefix = "plumbing:manual-water:";
        if (fixture == null || !fixture.PersistentInstanceId.IsValid)
        {
            return false;
        }

        string expectedDestination = ManualWaterPrefix
            + fixture.PersistentInstanceId.Value;
        return items?.GetAllStacks().Any(stack => stack != null
            && stack.State == WorldItemStackState.FacilityBuffer
            && stack.Quantity > 0
            && string.Equals(
                stack.DestinationId,
                expectedDestination,
                StringComparison.Ordinal)) == true;
    }

    private static string DescribeServiceActorRuntime(CharacterActor actor)
    {
        if (actor == null)
        {
            return "missing";
        }

        AIBrain brain = actor.Brain;
        AbilityHaul haul = actor.GetComponent<AbilityHaul>();
        AbilityMove move = actor.GetAbility<AbilityMove>();
        IReadOnlyList<CharacterActivityEvent> activities =
            actor.LogComponent?.ActivityEntries;
        string recent = activities == null
            ? "log-unavailable"
            : string.Join(" || ", activities
                .Where(value => value != null
                    && (string.Equals(
                            value.KindId,
                            BuildingActivityKinds.FacilityUse,
                            StringComparison.Ordinal)
                        || string.Equals(
                            value.KindId,
                            CharacterActivityKinds.AiDecision,
                            StringComparison.Ordinal)))
                .TakeLast(12)
                .Select(value => $"kind={value.KindId},outcome={value.OutcomeId},"
                    + $"reason={value.ReasonCode},fact={value.FactText}"));
        if (recent.Length == 0)
        {
            recent = "none";
        }

        return $"id={actor.Identity?.PersistentId}:pos={actor.GetNowXY()}:"
            + $"canRun={actor.CanRunAi}:paused={actor.IsAiPaused()}:"
            + $"action={brain?.CurrentActionDebugLabel}:"
            + $"phase={brain?.CurrentActionPhase}:"
            + $"detail={brain?.CurrentActionPhaseDetail}:"
            + $"destination={brain?.CurrentDestinationDebugLabel}:"
            + $"preferred={brain?.RuntimePreferredActionDisposition}/"
            + $"{brain?.RuntimePreferredActionDispositionBranch}:"
            + $"preferredFailure={brain?.FirstPreferredActionHardFailure}:"
            + $"lastFailure={brain?.LastActionFailure}:"
            + $"haulActive={haul?.IsHauling}:haulPlan={haul?.CurrentPlanSummary}:"
            + $"haulStage={haul?.CurrentExecutionStage}:"
            + $"haulHeartbeat={haul?.RoutineHeartbeat}:"
            + $"haulPath={haul?.ActivePathDebug}:"
            + $"haulFailure={haul?.LastFailureReason}:"
            + $"moveActive={move?.HasActiveMovementRoutineForDiagnostics}:"
            + $"moveDestination={move?.ActiveSystemMoveDestinationForDiagnostics}:"
            + $"moveOwner={move?.ActiveMovementOperationOwnerForDiagnostics}:"
            + $"moveReplans={move?.RuntimeActionPathReplanCount}:"
            + $"moveFailures={move?.RuntimeActionPathFailureCount}:"
            + $"recent={recent}";
    }

    private static string DescribeAiArbitration(CharacterActor actor)
    {
        string id = actor?.Identity?.PersistentId ?? "missing";
        AIBrain brain = actor?.Brain;
        return brain == null
            ? id + ":brain-missing"
            : $"{id}:transitions={brain.ExternalIntentTransitionCount},"
                + $"preemptions={brain.ExternalIntentPreemptionCount},"
                + $"rejections={brain.ExternalIntentRejectedCount},"
                + $"staleCompletions={brain.ExternalIntentStaleCompletionCount},"
                + $"immediateDecisions={brain.ImmediateDecisionRequestCount},"
                + $"lastImmediate={brain.LastImmediateDecisionReason}";
    }

    private static string DescribeEnvironment(
        CharacterActor actor,
        IEnvironmentalFieldQuery environment,
        ICharacterSpeciesEnvironmentCatalog speciesEnvironment)
    {
        if (actor == null
            || !environment.TryGetCell(
                actor.GetNowXY(),
                out EnvironmentalCellSnapshot cell))
        {
            return $"{actor?.Identity?.PersistentId}:missing";
        }
        SpeciesThermalProfile thermal = speciesEnvironment.GetRequiredThermalProfile(
            new CharacterSpeciesId(actor.SpeciesTag));
        return $"{actor.Identity?.PersistentId}@{actor.GetNowXY()}:"
            + $"species={actor.SpeciesTag}:temp={cell.TemperatureC:0.##}:"
            + $"air={cell.AirQuality:0.##}:light={cell.LightLevel:0.##}:"
            + $"lethal={thermal.LethalMinimum:0.##}..{thermal.LethalMaximum:0.##}";
    }

    private static float GetNeed(CharacterActor actor, CharacterCondition condition) =>
        actor?.Stats != null
        && actor.Stats.TryGetConditionValue(condition, out float value)
            ? value
            : -1f;

    private static int CountItem(IWorldItemStackRuntime items, string itemId) =>
        items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);

    private int GetCount(string actionId) =>
        primitiveCounts.TryGetValue(actionId, out int count) ? count : 0;

    private int GetPrimitivePhysicalItemCount(string actionId) =>
        primitivePhysicalItemCounts.TryGetValue(actionId, out int count) ? count : 0;

    private void Check(bool condition, string id, string detail)
    {
        report.Add($"{(condition ? "PASS" : "FAIL")} {id}: {detail}");
        if (!condition)
        {
            failures.Add(id + ": " + detail);
        }
    }
}
#endif
