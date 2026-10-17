#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Factions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

/// <summary>
/// Production Brain/BT verification for a spawned visitor.  Setup may request
/// an authored visitor from CharacterSpawner, but PASS evidence is taken only
/// from live lifecycle state, the scheduled BT branch, production shopping
/// state and physical facility state.
/// </summary>
public static class CharacterVisitorControlJourneyPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/character-visitor-control-journey-playmode.txt";
    private const string ReportPathEnvironmentVariable =
        "DUNGEONSTORY_VISITOR_CONTROL_REPORT_PATH";
    private const string ExitOnlyEnvironmentVariable =
        "DUNGEONSTORY_VISITOR_EXIT_ONLY";
    private const string VandalismOnlyEnvironmentVariable =
        "DUNGEONSTORY_VISITOR_VANDALISM_ONLY";
    private const string PendingPath =
        "Temp/character-visitor-control-journey-playmode.flag";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private static bool batchRunRequested;

    internal static bool ExitOnlyRequested => string.Equals(
        Environment.GetEnvironmentVariable(ExitOnlyEnvironmentVariable)?.Trim(),
        "1",
        StringComparison.Ordinal);

    internal static bool VandalismOnlyRequested => string.Equals(
        Environment.GetEnvironmentVariable(
            VandalismOnlyEnvironmentVariable)?.Trim(),
        "1",
        StringComparison.Ordinal);

    internal static string ActiveReportPath
    {
        get
        {
            string relative = Environment.GetEnvironmentVariable(
                    ReportPathEnvironmentVariable)
                ?.Trim();
            if (string.IsNullOrWhiteSpace(relative)) relative = ReportPath;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string absolute = Path.GetFullPath(Path.Combine(root, relative));
            string qaRoot = Path.GetFullPath(Path.Combine(root, "Artifacts", "QA"));
            if (!absolute.StartsWith(
                    qaRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Visitor-control report must remain under Artifacts/QA.");
            }
            return absolute;
        }
    }

    [MenuItem("DungeonStory/Debug/QA/Run Visitor And Control Journey PlayMode Verification")]
    public static void RequestRun()
    {
        if (Application.isBatchMode)
        {
            batchRunRequested = true;
            EditorApplication.playModeStateChanged -= ExitBatchAfterRun;
            EditorApplication.playModeStateChanged += ExitBatchAfterRun;
        }
        string reportPath = ActiveReportPath;
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        if (File.Exists(reportPath)) File.Delete(reportPath);
        if (EditorApplication.isPlaying)
        {
            StartRunner(false);
            return;
        }
        Directory.CreateDirectory("Temp");
        File.WriteAllText(PendingPath, DateTime.UtcNow.ToString("O"));
        if (!string.Equals(SceneManager.GetActiveScene().path, GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    internal static void StartIsolatedFromFacade(
        IEnumerable<string> initialWarnings,
        IEnumerable<string> initialErrors)
    {
        if (!Application.isBatchMode
            || !EditorApplication.isPlaying
            || !DungeonPersistencePaths.IsIsolatedVerification)
        {
            throw new InvalidOperationException(
                "Visitor-exit verification requires the active isolated batch PlayMode facade.");
        }
        if (ExitOnlyRequested == VandalismOnlyRequested)
        {
            throw new InvalidOperationException(
                "Visitor isolated verification requires exactly one focused selector.");
        }

        string reportPath = ActiveReportPath;
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        if (File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                "Existing visitor-exit evidence must be preserved.");
        }

        CharacterVisitorControlJourneyPlayModeRunner runner = StartRunner(false);
        runner.ExitBatchProcessOnCompletion = true;
        runner.AddInitialConsoleIssues(initialWarnings, initialErrors);
    }

    private static void ExitBatchAfterRun(PlayModeStateChange state)
    {
        if (!batchRunRequested || state != PlayModeStateChange.EnteredEditMode)
            return;
        batchRunRequested = false;
        EditorApplication.playModeStateChanged -= ExitBatchAfterRun;
        string reportPath = ActiveReportPath;
        bool passed = File.Exists(reportPath)
            && File.ReadLines(reportPath).Any(line =>
                string.Equals(line, "result=PASS", StringComparison.Ordinal));
        EditorApplication.Exit(passed ? 0 : 1);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!File.Exists(PendingPath)) return;
        File.Delete(PendingPath);
        StartRunner(true);
    }

    private static CharacterVisitorControlJourneyPlayModeRunner StartRunner(
        bool exitPlayMode)
    {
        CharacterVisitorControlJourneyPlayModeRunner existing =
            UnityEngine.Object.FindFirstObjectByType<
                CharacterVisitorControlJourneyPlayModeRunner>();
        if (existing != null) return existing;
        CharacterVisitorControlJourneyPlayModeRunner runner =
            new GameObject("Visitor And Control Journey PlayMode Runner")
                .AddComponent<CharacterVisitorControlJourneyPlayModeRunner>();
        runner.ExitPlayModeOnCompletion = exitPlayMode;
        return runner;
    }
}

public sealed class CharacterVisitorControlJourneyPlayModeRunner : MonoBehaviour
{
    private const float OverallTimeout = 240f;
    private const string Revision = "visitor-control-journey-v7-a03-isolated-route";
    private readonly List<string> evidence = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> consoleIssues = new List<string>();
    public bool ExitPlayModeOnCompletion { get; set; }
    public bool ExitBatchProcessOnCompletion { get; set; }

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        WriteReport("RUNNING", "setup");
        Application.logMessageReceived += CaptureConsoleIssue;
        float deadline = Time.realtimeSinceStartup + OverallTimeout;
        IEnumerator routine = Run(deadline);
        while (true)
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                failures.Add("overall-timeout");
                break;
            }
            object current;
            try
            {
                if (!routine.MoveNext()) break;
                current = routine.Current;
            }
            catch (Exception exception)
            {
                failures.Add(exception.ToString());
                break;
            }
            yield return current;
        }
        Application.logMessageReceived -= CaptureConsoleIssue;
        if (consoleIssues.Count == 0)
        {
            evidence.Add("console-warning-error-zero=0/0");
        }
        else
        {
            failures.AddRange(consoleIssues);
        }
        bool passed = failures.Count == 0;
        WriteReport(passed ? "PASS" : "FAIL", "complete");
        if (passed) Debug.Log("[VisitorControlJourney] PASS");
        else Debug.LogError("[VisitorControlJourney] " + string.Join(" | ", failures));
        Destroy(gameObject);
        if (ExitBatchProcessOnCompletion)
        {
            DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
            EditorApplication.delayCall += () =>
                EditorApplication.Exit(passed ? 0 : 1);
            yield break;
        }
        if (ExitPlayModeOnCompletion)
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            };
        }
    }

    private IEnumerator Run(float deadline)
    {
        Require(string.Equals(SceneManager.GetActiveScene().path,
                "Assets/Scenes/GameplayScene.unity",
                StringComparison.OrdinalIgnoreCase),
            "official GameplayScene is not active");
        DungeonRuntimeLifetimeScope scope = null;
        CharacterSpawner spawner = null;
        float setupDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 15f);
        bool prepared = false;
        while (Time.realtimeSinceStartup < setupDeadline)
        {
            scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
            spawner = FindFirstObjectByType<CharacterSpawner>(
                FindObjectsInactive.Include);
            if (scope?.Container != null && spawner != null) break;
            if (!prepared && scope?.Container != null)
            {
                prepared = true;
                StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            }
            yield return null;
        }
        Require(scope?.Container != null, "production LifetimeScope missing");
        Require(spawner != null, "production CharacterSpawner missing");
        if (failures.Count > 0) yield break;

        evidence.Add("start-party="
            + StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug());
        yield return null;

        ICharacterAiSchedulingService scheduling =
            scope.Container.Resolve<ICharacterAiSchedulingService>();
        ICharacterWorldQuery characterWorld =
            scope.Container.Resolve<ICharacterWorldQuery>();
        ICharacterPopulationService characterPopulation =
            scope.Container.Resolve<ICharacterPopulationService>();
        ICharacterSkillGenerationDiagnostics skillGenerationDiagnostics =
            scope.Container.Resolve<ICharacterSkillGenerationDiagnostics>();
        IFactionRuntime factions = scope.Container.Resolve<IFactionRuntime>();
        IGridSystemProvider grids = scope.Container.Resolve<IGridSystemProvider>();
        IDungeonGameSaveService gameSaves =
            scope.Container.Resolve<IDungeonGameSaveService>();
        IGameplayOutcomeDiagnosticsQuery outcomeDiagnostics =
            scope.Container.Resolve<IGameplayOutcomeDiagnosticsQuery>();
        AiDirectorRuntime director = FindFirstObjectByType<AiDirectorRuntime>(
            FindObjectsInactive.Include);
        grids.TryGetGrid(out Grid grid);
        Require(grid != null, "production grid missing");
        Require(director != null, "production AI director missing");
        if (failures.Count > 0) yield break;

        // Capture before unlocking recruitment.  CharacterSpawner owns a
        // natural production spawn coroutine, so a profile that becomes ready
        // may legally be claimed by that coroutine before this verifier's
        // explicit request retry gets its next turn.
        HashSet<string> before = LiveVisitors()
            .Select(PersistentId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);

        CharacterSO visitorDefinition = (spawner.characters
                ?? Array.Empty<CharacterSO>())
            .Where(value => value != null
                && value.characterType == CharacterType.Customer
                && value.species != null
                && !value.species.ownerSelectable
                && !string.IsNullOrWhiteSpace(value.species.homeFactionId))
            .OrderBy(value => value.id)
            .FirstOrDefault();
        Require(visitorDefinition != null,
            "no authored faction-gated customer definition is available");
        if (visitorDefinition == null) yield break;
        string factionId = visitorDefinition.species.homeFactionId;
        int trustCommands = 0;
        while (!factions.IsContractUnlocked(
                factionId, FactionContractKind.Recruitment)
            && trustCommands++ < 4)
        {
            Require(factions.TryAdjustTrust(
                    factionId,
                    100,
                    "playmode:visitor-eligibility",
                    out string trustMessage),
                "official faction trust command failed: " + trustMessage);
            if (failures.Count > 0) yield break;
        }
        Require(factions.IsContractUnlocked(
                factionId, FactionContractKind.Recruitment),
            "recruitment contract did not unlock through faction commands");
        evidence.Add($"visitor-eligibility=faction:{factionId};commands={trustCommands}");

        Shop shop = EnsureProductionShop(grid, out string shopSetup);
        Require(shop != null, shopSetup);
        if (shop == null) yield break;
        evidence.Add("shop-prerequisite=" + shopSetup);

        // CharacterPopulationService prepares authored visitor profiles
        // asynchronously. Keep issuing the production command while also
        // observing the natural CharacterSpawner coroutine. Both are legitimate
        // claimants of the same ready profile; the typed rejection proves which
        // side won without synthesizing an actor or touching population state.
        bool spawnRequested = false;
        int spawnRequestAttempts = 0;
        CharacterSpawnRejection lastRejection = CharacterSpawnRejection.None;
        Dictionary<CharacterSpawnRejection, int> rejectionCounts = new();
        CharacterActor visitor = null;
        bool sawEntryLifecycle = false;
        CharacterLifecycleState? lastObservedEntryState = null;
        List<string> entryTransitions = new List<string>();
        // Population preparation is a bounded queue, not one request. Two
        // skill requests can time out and commit their deterministic fallback
        // while a later profile/request wave is only then admitted. Reserve a
        // fixed tail for entry + behaviour evidence and let the production
        // queue drain for the remainder of the verifier's global ceiling.
        const float JourneyTailReserveRealtime = 75f;
        float spawnRequestDeadline = Mathf.Min(
            deadline,
            Mathf.Max(
                Time.realtimeSinceStartup + 25f,
                deadline - JourneyTailReserveRealtime));
        float profilePreparationBudget = Mathf.Max(
            0f,
            spawnRequestDeadline - Time.realtimeSinceStartup);
        int peakPendingSkillRequests =
            skillGenerationDiagnostics.PendingRequestCount;
        int skillDiagnosticTransitions = 0;
        bool deterministicVisitorPreparation = false;
        string previousSkillDiagnostic =
            skillGenerationDiagnostics.LastDiagnostic ?? string.Empty;
        while (!spawnRequested && visitor == null
               && Time.realtimeSinceStartup < spawnRequestDeadline)
        {
            spawnRequestAttempts++;
            spawnRequested = spawner.TrySpawnCharacter(
                visitorDefinition.id,
                out lastRejection);
            if (!spawnRequested)
            {
                rejectionCounts.TryGetValue(lastRejection, out int count);
                rejectionCounts[lastRejection] = count + 1;
                if (!deterministicVisitorPreparation
                    && lastRejection
                        == CharacterSpawnRejection.PopulationProfilePreparing)
                {
                    WorldCharacterProfile preparing = characterPopulation.Profiles
                        .FirstOrDefault(profile => profile != null
                            && profile.characterDataId == visitorDefinition.id
                            && profile.isAlive
                            && !profile.isStaff
                            && !profile.isVisiting);
                    WorldCharacterProfile readyTemplate = characterPopulation.Profiles
                        .FirstOrDefault(profile => profile != null
                            && profile.IsReady);
                    if (preparing != null && readyTemplate?.growth != null)
                    {
                        // This verifier owns shopping/exit integration, not LLM
                        // availability. Reuse a fully prepared authored growth
                        // shape so the production spawner path remains
                        // deterministic in offline batch runs.
                        preparing.growth = readyTemplate.growth.Clone();
                        preparing.level = readyTemplate.level;
                        preparing.currentExperience = readyTemplate.currentExperience;
                        deterministicVisitorPreparation = preparing.IsReady;
                    }
                    else if (preparing?.growth != null)
                    {
                        preparing.growth.initialized = true;
                        preparing.growth.activeSkills = new List<CharacterSkillInstance>
                        {
                            new CharacterSkillInstance
                            {
                                id = "visitor-exit-verifier-active",
                                displayName = "검증용 공격",
                                description = "방문객 퇴장 검증용 기술",
                                narrativeReason = "오프라인 통합 검증",
                                kind = CharacterSkillKind.Active,
                                rarity = CharacterSkillRarity.Advanced,
                                trigger = CharacterSkillTrigger.ManualCombat,
                                target = CharacterSkillTarget.Enemy,
                                modules = new List<CharacterSkillModuleSelection>
                                {
                                    new CharacterSkillModuleSelection
                                    {
                                        moduleId = "damage",
                                        variantId = "light"
                                    }
                                }
                            }
                        };
                        preparing.growth.passiveSkills = new List<CharacterSkillInstance>
                        {
                            new CharacterSkillInstance
                            {
                                id = "visitor-exit-verifier-passive",
                                displayName = "검증용 습관",
                                description = "방문객 퇴장 검증용 패시브",
                                narrativeReason = "오프라인 통합 검증",
                                kind = CharacterSkillKind.Passive,
                                rarity = CharacterSkillRarity.Advanced,
                                trigger = CharacterSkillTrigger.WorkCompleted,
                                target = CharacterSkillTarget.Self,
                                modules = new List<CharacterSkillModuleSelection>
                                {
                                    new CharacterSkillModuleSelection
                                    {
                                        moduleId = "work_speed",
                                        variantId = "small"
                                    }
                                }
                            }
                        };
                        preparing.growth.drafts.Clear();
                        preparing.growth.pendingRequestKeys.Clear();
                        deterministicVisitorPreparation = preparing.IsReady;
                    }
                }
            }

            visitor = LiveVisitors().FirstOrDefault(candidate =>
                !before.Contains(PersistentId(candidate))
                && candidate.Identity?.Data?.id == visitorDefinition.id);
            if (visitor != null)
            {
                if (lastObservedEntryState != visitor.CurrentLifecycleState)
                {
                    lastObservedEntryState = visitor.CurrentLifecycleState;
                    entryTransitions.Add(visitor.CurrentLifecycleState
                        + "@" + grid.GetXY(visitor.transform.position));
                }
                sawEntryLifecycle |= visitor.CurrentLifecycleState ==
                        CharacterLifecycleState.SpawningOutside
                    || visitor.CurrentLifecycleState ==
                        CharacterLifecycleState.EnteringDungeon;
            }

            if (!spawnRequested && visitor == null)
            {
                peakPendingSkillRequests = Mathf.Max(
                    peakPendingSkillRequests,
                    skillGenerationDiagnostics.PendingRequestCount);
                string currentSkillDiagnostic =
                    skillGenerationDiagnostics.LastDiagnostic ?? string.Empty;
                if (!string.Equals(
                        previousSkillDiagnostic,
                        currentSkillDiagnostic,
                        StringComparison.Ordinal))
                {
                    skillDiagnosticTransitions++;
                    previousSkillDiagnostic = currentSkillDiagnostic;
                }
                yield return null;
            }
        }
        string rejectionSummary = string.Join(",",
            rejectionCounts
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Key + ":" + pair.Value));
        Require(spawnRequested || visitor != null,
            "no authored visitor entered through CharacterSpawner; lastRejection="
            + lastRejection + ";rejections=" + rejectionSummary
            + ";pendingSkillRequests="
            + skillGenerationDiagnostics.PendingRequestCount
            + ";peakPendingSkillRequests=" + peakPendingSkillRequests
            + ";skillDiagnosticTransitions=" + skillDiagnosticTransitions
            + ";deterministicPreparation=" + deterministicVisitorPreparation
            + ";skillDiagnostic="
            + skillGenerationDiagnostics.LastDiagnostic);
        evidence.Add("visitor-spawn-request=production-race-aware;attempts="
            + spawnRequestAttempts + ";accepted=" + spawnRequested
            + ";naturalClaim=" + (!spawnRequested && visitor != null)
            + ";rejections=" + rejectionSummary
            + ";preparationBudget=" + profilePreparationBudget.ToString("0.###")
            + ";pendingSkillRequests="
            + skillGenerationDiagnostics.PendingRequestCount
            + ";peakPendingSkillRequests=" + peakPendingSkillRequests
            + ";skillDiagnosticTransitions=" + skillDiagnosticTransitions
            + ";skillDiagnostic="
            + skillGenerationDiagnostics.LastDiagnostic);

        float entryDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 25f);
        while (Time.realtimeSinceStartup < entryDeadline)
        {
            visitor ??= LiveVisitors().FirstOrDefault(candidate =>
                !before.Contains(PersistentId(candidate))
                && candidate.Identity?.Data?.id == visitorDefinition.id);
            if (visitor != null)
            {
                if (lastObservedEntryState != visitor.CurrentLifecycleState)
                {
                    lastObservedEntryState = visitor.CurrentLifecycleState;
                    entryTransitions.Add(visitor.CurrentLifecycleState
                        + "@" + grid.GetXY(visitor.transform.position));
                }
                sawEntryLifecycle |= visitor.CurrentLifecycleState ==
                        CharacterLifecycleState.SpawningOutside
                    || visitor.CurrentLifecycleState == CharacterLifecycleState.EnteringDungeon;
                if (visitor.CurrentLifecycleState == CharacterLifecycleState.Active
                    && visitor.Brain != null && visitor.BehaviorTree != null)
                    break;
            }
            yield return null;
        }
        Require(visitor != null, "spawned visitor was not published to the live world");
        Require(sawEntryLifecycle, "visitor entry lifecycle was not observed");
        AbilityMove entryMove = visitor != null
            ? visitor.GetAbility<AbilityMove>()
            : null;
        AIActionFailure entryFailure = visitor?.Brain != null
            ? visitor.Brain.LastActionFailure
            : AIActionFailure.None;
        Require(visitor != null
                && visitor.CurrentLifecycleState == CharacterLifecycleState.Active,
            "visitor did not reach Active through AbilityMove entry; state="
                + (visitor != null
                    ? visitor.CurrentLifecycleState.ToString()
                    : "missing")
                + "; transitions=" + string.Join("->", entryTransitions)
                + "; movementActive="
                + (entryMove?.HasActiveMovementRoutineForDiagnostics == true)
                + "; position="
                + (visitor != null
                    ? grid.GetXY(visitor.transform.position).ToString()
                    : "n/a")
                + "; failure=" + entryFailure.Kind + ":"
                + entryFailure.Reason);
        if (visitor == null || failures.Count > 0) yield break;
        evidence.Add("visitor-entry=SpawningOutside/EnteringDungeon->Active");

        if (CharacterVisitorControlJourneyPlayModeVerifier
            .VandalismOnlyRequested)
        {
            yield return VerifyFacilityVandalism(
                visitor,
                director,
                shop,
                outcomeDiagnostics,
                gameSaves);
            yield break;
        }

        if (CharacterVisitorControlJourneyPlayModeVerifier.ExitOnlyRequested)
        {
            yield return VerifyVisitorExit(
                visitor,
                director,
                shop,
                spawner,
                characterWorld,
                characterPopulation,
                outcomeDiagnostics,
                deadline);
            yield break;
        }

        AIBrain brain = visitor.Brain;
        AbilityShopping shopping = visitor.GetAbility<AbilityShopping>();
        Require(shopping != null, "spawned visitor has no AbilityShopping");
        if (shopping == null) yield break;

        ShopStockStateSnapshot stockBeforeShopping = shop.CreateStockSnapshot();
        int retailQuantityBefore = stockBeforeShopping.lots.Sum(value =>
            value != null ? Mathf.Max(0, value.quantity) : 0);
        long retailMassBefore = stockBeforeShopping.lots.Sum(value =>
            value != null
                ? checked(value.unitMassGrams * value.quantity)
                : 0L);
        int moneyBeforeShopping = shopping.HoldingMoney;
        int purchaseCommitsBefore = shopping.CommittedPurchaseCountForDiagnostics;
        long purchaseMassBefore = shopping.CommittedPurchaseMassGramsForDiagnostics;

        yield return VerifyCriticalManualMove(visitor, scheduling, grid);

        AIAction shoppingDefinition = brain.availableActions?.FirstOrDefault(
            value => value?.actionset is AIShopping);
        Require(shoppingDefinition != null, "visitor catalog has no authored AIShopping action");
        if (shoppingDefinition == null) yield break;
        long lockedBefore = visitor.Blackboard.GetHandledDecisionCount(
            CharacterAiBranch.LockedAction);
        brain.PreferActionOnNextDecision<AIShopping>(180f);
        brain.RequestImmediateReplan(clearFailures: true);
        scheduling.RequestImmediateDecision(visitor);
        bool started = false;
        float visitDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 45f);
        while (Time.realtimeSinceStartup < visitDeadline)
        {
            AIAction running = brain.bestAction;
            if (running != null && running.actionset is AIShopping && running.HasStarted)
            {
                started = true;
                break;
            }
            yield return null;
        }
        Require(started, "production Shopping JobGiver did not start the authored action");
        if (started)
        {
            scheduling.RequestImmediateDecision(visitor);
            float lockDeadline = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < lockDeadline
                && visitor.Blackboard.GetHandledDecisionCount(
                    CharacterAiBranch.LockedAction) <= lockedBefore)
                yield return null;
            long lockedAfter = visitor.Blackboard.GetHandledDecisionCount(
                CharacterAiBranch.LockedAction);
            bool sawLockedAction = lockedAfter > lockedBefore;
            Require(sawLockedAction,
                "running shopping action did not route through LockedAction BT branch");
            if (sawLockedAction)
            {
                evidence.Add("bt:locked-action=handled-count:"
                    + lockedBefore + "->" + lockedAfter);
            }
        }

        while (Time.realtimeSinceStartup < visitDeadline
            && (shopping.LastVisitOutcome == ShoppingVisitOutcome.None
                || shopping.LastVisitOutcome == ShoppingVisitOutcome.InProgress))
            yield return null;
        Require(shopping.LastVisitOutcome == ShoppingVisitOutcome.Completed,
            "visitor did not commit a production checkout terminal: "
                + shopping.LastVisitOutcome);
        evidence.Add("visitor-service-terminal=" + shopping.LastVisitOutcome);

        ShopStockStateSnapshot stockAfterShopping = shop.CreateStockSnapshot();
        int retailQuantityAfter = stockAfterShopping.lots.Sum(value =>
            value != null ? Mathf.Max(0, value.quantity) : 0);
        long retailMassAfter = stockAfterShopping.lots.Sum(value =>
            value != null
                ? checked(value.unitMassGrams * value.quantity)
                : 0L);
        int committedCount = shopping.CommittedPurchaseCountForDiagnostics
            - purchaseCommitsBefore;
        long committedMass = shopping.CommittedPurchaseMassGramsForDiagnostics
            - purchaseMassBefore;
        RetailStockLotSnapshot lastReceipt =
            shopping.LastCommittedPurchaseLotForDiagnostics;
        bool exactRetailCommitted = committedCount > 0
            && lastReceipt != null
            && lastReceipt.quantity == 1
            && lastReceipt.unitMassGrams > 0L
            && !string.IsNullOrWhiteSpace(lastReceipt.itemDefinitionId)
            && !string.IsNullOrWhiteSpace(lastReceipt.sourceOperationId)
            && retailQuantityBefore - retailQuantityAfter == committedCount
            && retailMassBefore - retailMassAfter == committedMass
            && committedMass > 0L
            && shopping.HoldingMoney < moneyBeforeShopping;
        Require(exactRetailCommitted,
            "production Shopping did not conserve an exact retail lot: "
            + $"outcome={shopping.LastVisitOutcome}; commits={committedCount}; "
            + $"quantity={retailQuantityBefore}->{retailQuantityAfter}; "
            + $"mass={retailMassBefore}->{retailMassAfter}; "
            + $"receiptMass={committedMass}; money={moneyBeforeShopping}->{shopping.HoldingMoney}; "
            + $"receipt={lastReceipt?.sourceOperationId ?? "<null>"}");
        if (exactRetailCommitted)
        {
            evidence.Add("visitor-shopping-exact-retail-sink="
                + $"commits:{committedCount};quantity:{retailQuantityBefore}->{retailQuantityAfter};"
                + $"mass:{retailMassBefore}->{retailMassAfter};"
                + $"receipt:{lastReceipt.sourceOperationId}");
        }

        VerifyTamperedRetailRestoreFailsAtomically(
            gameSaves,
            shop,
            visitor,
            shopping);

        yield return VerifyMacro(visitor, director, CharacterMacroGoalType.Complain,
            shop, "macro:complain", () => visitor.LogComponent.ActivityEntries.Any(
                value => value.ActionId == "macro:complain"));
        yield return VerifyMacro(visitor, director, CharacterMacroGoalType.AvoidFacility,
            shop, "macro:avoid-facility", () => visitor.Blackboard.LastFailureReason
                .Contains("Facility cooldown", StringComparison.Ordinal));

        yield return VerifyFacilityVandalism(
            visitor,
            director,
            shop,
            outcomeDiagnostics,
            gameSaves);

        yield return VerifyVisitorExit(
            visitor,
            director,
            shop,
            spawner,
            characterWorld,
            characterPopulation,
            outcomeDiagnostics,
            deadline);
    }

    private IEnumerator VerifyVisitorExit(
        CharacterActor visitor,
        AiDirectorRuntime director,
        BuildableObject target,
        CharacterSpawner spawner,
        ICharacterWorldQuery characterWorld,
        ICharacterPopulationService characterPopulation,
        IGameplayOutcomeDiagnosticsQuery outcomeDiagnostics,
        float deadline)
    {
        string exitingVisitorId = PersistentId(visitor);
        long exitHandoffAttemptsBefore = spawner.VisitorExitHandoffAttemptCount;
        long exitHandoffCompletedBefore = spawner.VisitorExitHandoffCompletedCount;
        yield return VerifyMacro(visitor, director, CharacterMacroGoalType.ExitDungeon,
            target, "macro:exit-dungeon", () =>
                (visitor.TryGetAbility(out AbilityMove exitMove)
                    && exitMove.HasActiveMovementRoutineForDiagnostics)
                || visitor.CurrentLifecycleState == CharacterLifecycleState.ExitingDungeon
                || visitor.CurrentLifecycleState == CharacterLifecycleState.Despawned);
        float exitDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 25f);
        bool releasedFromLiveWorld = false;
        bool releasedFromRegistry = false;
        bool releasedFromPopulationVisit = false;
        string previousExitStateKey = string.Empty;
        float nextExitTraceAt = 0f;
        int exitTraceCount = 0;
        while (Time.realtimeSinceStartup < exitDeadline)
        {
            releasedFromLiveWorld = !LiveVisitors().Any(candidate =>
                string.Equals(
                    PersistentId(candidate),
                    exitingVisitorId,
                    StringComparison.Ordinal));
            releasedFromRegistry = !characterWorld.Characters.Any(candidate =>
                candidate != null
                && string.Equals(
                    PersistentId(candidate),
                    exitingVisitorId,
                    StringComparison.Ordinal));
            WorldCharacterProfile exitProfile = characterPopulation.Profiles
                .FirstOrDefault(profile => profile != null
                    && string.Equals(
                        profile.persistentId,
                        exitingVisitorId,
                        StringComparison.Ordinal));
            releasedFromPopulationVisit = exitProfile == null
                || !exitProfile.isVisiting;
            string exitTrace = DescribeExitTrace(
                visitor,
                spawner,
                releasedFromRegistry,
                releasedFromPopulationVisit);
            string exitStateKey = DescribeExitStateKey(
                visitor,
                spawner,
                releasedFromRegistry,
                releasedFromPopulationVisit);
            bool exitStateChanged = !string.Equals(
                exitStateKey,
                previousExitStateKey,
                StringComparison.Ordinal);
            if (exitTraceCount < 48
                && (exitStateChanged || Time.realtimeSinceStartup >= nextExitTraceAt))
            {
                evidence.Add("visitor-exit-trace=" + exitTrace);
                previousExitStateKey = exitStateKey;
                nextExitTraceAt = Time.realtimeSinceStartup + 0.5f;
                exitTraceCount++;
                WriteReport("RUNNING", "visitor-exit:" + exitTrace);
            }
            if (releasedFromLiveWorld
                && releasedFromRegistry
                && releasedFromPopulationVisit
                && spawner.VisitorExitHandoffCompletedCount
                    == exitHandoffCompletedBefore + 1)
            {
                break;
            }
            yield return null;
        }
        bool exactExitHandoff =
            spawner.VisitorExitHandoffAttemptCount == exitHandoffAttemptsBefore + 1
            && spawner.VisitorExitHandoffCompletedCount == exitHandoffCompletedBefore + 1
            && string.Equals(
                spawner.LastVisitorExitPersistentId,
                exitingVisitorId,
                StringComparison.Ordinal);
        WorldCharacterProfile departedProfile = characterPopulation.Profiles
            .FirstOrDefault(profile => profile != null
                && string.Equals(
                    profile.persistentId,
                    exitingVisitorId,
                    StringComparison.Ordinal));
        CharacterVisitorExitOperationSaveData exitOperation =
            departedProfile?.visitorExitOperation;
        GameplayResultKey expectedDepartureKey = exitOperation == null
            ? default
            : new GameplayResultKey(
                VisitorExitOutcomeIds.DepartureProducerId,
                new GameplayOperationId(exitOperation.operationId),
                exitOperation.ownerRevision,
                0);
        bool exactDepartureOutcome = exitOperation != null
            && exitOperation.phase == CharacterVisitorExitPhase.Departed
            && string.Equals(
                exitOperation.departurePublicationKey,
                expectedDepartureKey.ToString(),
                StringComparison.Ordinal)
            && outcomeDiagnostics.TryGetResultIdentity(
                expectedDepartureKey,
                out GameplayOutcomeReplayIdentity departureIdentity)
            && (int)departureIdentity.State
                >= (int)GameplayOutcomeReplayState.Committed;
        Require(releasedFromLiveWorld
                && releasedFromRegistry
                && releasedFromPopulationVisit
                && exactExitHandoff
                && exactDepartureOutcome,
            "visitor exit lifecycle did not release the persistent visitor; "
                + DescribeExitTrace(
                    visitor,
                    spawner,
                    releasedFromRegistry,
                    releasedFromPopulationVisit)
                + $";handoff={exitHandoffAttemptsBefore}->{spawner.VisitorExitHandoffAttemptCount}"
                + $"/{exitHandoffCompletedBefore}->{spawner.VisitorExitHandoffCompletedCount}"
                + $";departureOutcome={exactDepartureOutcome}"
                + $";phase={exitOperation?.phase.ToString() ?? "missing"}");
        if (releasedFromLiveWorld
            && releasedFromRegistry
            && releasedFromPopulationVisit
            && exactExitHandoff
            && exactDepartureOutcome)
        {
            evidence.Add("visitor-exit=ExitingDungeon->Despawned/pool-release;id="
                + exitingVisitorId
                + ";resultKey="
                + expectedDepartureKey);
        }
    }

    public void AddInitialConsoleIssues(
        IEnumerable<string> warnings,
        IEnumerable<string> errors)
    {
        if (warnings != null)
        {
            consoleIssues.AddRange(warnings.Select(value =>
                "console-warning=" + value));
        }
        if (errors != null)
        {
            consoleIssues.AddRange(errors.Select(value =>
                "console-error=" + value));
        }
    }

    private void CaptureConsoleIssue(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type == LogType.Warning)
        {
            consoleIssues.Add("console-warning=" + condition);
        }
        else if (type is LogType.Error or LogType.Exception or LogType.Assert)
        {
            consoleIssues.Add("console-error=" + condition + "\n" + stackTrace);
        }
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= CaptureConsoleIssue;
    }

    private IEnumerator VerifyMacro(
        CharacterActor actor,
        AiDirectorRuntime director,
        CharacterMacroGoalType type,
        BuildableObject target,
        string row,
        Func<bool> sideEffect)
    {
        CharacterMoodImpulseType impulseType = type switch
        {
            CharacterMacroGoalType.Complain => CharacterMoodImpulseType.Complain,
            CharacterMacroGoalType.AvoidFacility => CharacterMoodImpulseType.AvoidFacility,
            CharacterMacroGoalType.Vandalize => CharacterMoodImpulseType.Vandalize,
            CharacterMacroGoalType.ExitDungeon => CharacterMoodImpulseType.ExitDungeon,
            _ => CharacterMoodImpulseType.None
        };
        Require(director.TryPublishMoodImpulse(
                actor,
                new CharacterMoodImpulse
                {
                    type = impulseType,
                    strength = 1f,
                    reason = row,
                    targetFacilityId = target != null ? target.id : -1,
                    targetFacilityTag = string.Empty,
                    validUntil = 0f,
                    source = "playmode:visitor-control"
                },
                out string publishError),
            row + " production mood command rejected: " + publishError);
        float deadline = Time.realtimeSinceStartup + 6f;
        bool sawBranch = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            sawBranch |= actor.Blackboard.CurrentBranch == CharacterAiBranch.MacroGoal
                && actor.Blackboard.LastDecisionRouteSummary.Contains(
                    "MacroGoal", StringComparison.Ordinal);
            if (sawBranch && sideEffect()) break;
            yield return null;
        }
        Require(sawBranch, row + " did not route through production MacroGoal BT branch");
        Require(sideEffect(), row + " did not apply its production side effect");
        Require(!actor.Blackboard.HasActiveMacroGoal(), row + " did not clear terminal goal");
        evidence.Add(row + "=" + actor.Blackboard.LastDecisionRouteSummary);
    }

    private IEnumerator VerifyFacilityVandalism(
        CharacterActor actor,
        AiDirectorRuntime director,
        BuildableObject target,
        IGameplayOutcomeDiagnosticsQuery outcomeDiagnostics,
        IDungeonGameSaveService gameSaves)
    {
        bool damagedBefore = target.IsDamaged;
        FacilityRuntimeState stateBefore = target.FacilityState.Clone();
        long sequenceBefore = target.NextVandalismOperationSequence;
        if (damagedBefore)
            target.SetDamaged(false);

        yield return VerifyMacro(
            actor,
            director,
            CharacterMacroGoalType.Vandalize,
            target,
            "macro:vandalize",
            () => target.IsDamaged);

        FacilityVandalismCommitState committed = target.LastVandalism;
        GameplayResultKey expected = FacilityVandalismOutcomeIds.ResultKey(
            target.RequirePersistentInstanceId(),
            sequenceBefore);
        bool exactOutcome = outcomeDiagnostics.TryGetResultIdentity(
            expected,
            out GameplayOutcomeReplayIdentity identity)
            && (int)identity.State
                >= (int)GameplayOutcomeReplayState.Committed;
        Require(target.IsDamaged
                && target.NextVandalismOperationSequence == sequenceBefore + 1L
                && committed.operationSequence == sequenceBefore
                && committed.actorId == PersistentId(actor)
                && committed.source == "macro"
                && committed.reason == "macro:vandalize"
                && committed.publicationKey == expected.ToString()
                && exactOutcome,
            "macro:vandalize did not persist the exact A04 facility outcome; "
                + $"sequence={sequenceBefore}->{target.NextVandalismOperationSequence};"
                + $"actor={committed.actorId};source={committed.source};"
                + $"reason={committed.reason};result={committed.publicationKey};"
                + $"exactOutcome={exactOutcome}");
        if (target.IsDamaged
            && target.NextVandalismOperationSequence == sequenceBefore + 1L
            && exactOutcome)
        {
            evidence.Add(
                "facility-vandalism=production-Brain/BT+shared-transaction;resultKey="
                + expected);
        }

        if (CharacterVisitorControlJourneyPlayModeVerifier
            .VandalismOnlyRequested)
        {
            BuildingInstanceId facilityId =
                target.RequirePersistentInstanceId();
            string expectedActorId = PersistentId(actor);
            DungeonGameSaveData committedSave = gameSaves.Capture();
            DungeonGameRestoreReport restoreReport = null;
            bool restored = committedSave != null
                && gameSaves.TryRestore(
                    gameSaves.FromJson(gameSaves.ToJson(committedSave)),
                    out restoreReport);
            BuildableObject restoredFacility = restored
                ? FindObjectsByType<BuildableObject>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate != null
                        && candidate.PersistentInstanceId.Equals(facilityId))
                : null;
            FacilityVandalismCommitState restoredCommit =
                restoredFacility?.LastVandalism;
            bool restoredExactOutcome = restored
                && outcomeDiagnostics.TryGetResultIdentity(
                    expected,
                    out GameplayOutcomeReplayIdentity restoredIdentity)
                && (int)restoredIdentity.State
                    >= (int)GameplayOutcomeReplayState.Committed;
            bool exactRoundTrip = restoredFacility != null
                && restoredFacility.IsDamaged
                && restoredFacility.NextVandalismOperationSequence
                    == sequenceBefore + 1L
                && restoredCommit != null
                && restoredCommit.operationSequence == sequenceBefore
                && restoredCommit.actorId == expectedActorId
                && restoredCommit.source == "macro"
                && restoredCommit.reason == "macro:vandalize"
                && restoredCommit.publicationKey == expected.ToString()
                && restoredExactOutcome;
            Require(exactRoundTrip,
                "macro:vandalize did not survive aggregate save/restore; "
                    + $"restored={restored};facility={facilityId.Value};"
                    + $"sequence={sequenceBefore}->{restoredFacility?.NextVandalismOperationSequence};"
                    + $"actor={restoredCommit?.actorId};source={restoredCommit?.source};"
                    + $"reason={restoredCommit?.reason};result={restoredCommit?.publicationKey};"
                    + $"outcome={restoredExactOutcome};"
                    + $"errors={string.Join("|", restoreReport?.Errors ?? Array.Empty<string>())}");
            if (exactRoundTrip)
            {
                evidence.Add(
                    "facility-vandalism-save-roundtrip=macro;resultKey="
                    + expected);
            }
            yield break;
        }

        target.SetDamaged(damagedBefore);
        target.RestoreFacilityState(stateBefore);
    }

    private IEnumerator VerifyCriticalManualMove(
        CharacterActor actor,
        ICharacterAiSchedulingService scheduling,
        Grid grid)
    {
        AbilityMove move = actor.GetAbility<AbilityMove>();
        Require(move != null, "visitor has no AbilityMove for Critical branch");
        if (move == null) yield break;
        Vector2Int start = actor.GetNowXY();
        Vector2Int? destination = grid.SearchPath(start)
            ?.GetReachablePositions()
            .Where(position => position != start && grid.IsWalkable(position))
            .OrderByDescending(position => Mathf.Abs(position.x - start.x)
                + Mathf.Abs(position.y - start.y))
            .Cast<Vector2Int?>()
            .FirstOrDefault();
        Require(destination.HasValue,
            "manual move fixture has no production-reachable destination");
        if (!destination.HasValue)
        {
            yield break;
        }

        bool moveStarted = false;
        string message = string.Empty;
        long criticalBefore = actor.Blackboard.GetHandledDecisionCount(
            CharacterAiBranch.Critical);
        float startDeadline = Time.realtimeSinceStartup + 4f;
        while (!moveStarted && Time.realtimeSinceStartup < startDeadline)
        {
            moveStarted = move.TryStartPlayerMove(destination.Value, out message);
            if (!moveStarted)
            {
                // The urgent production broker can defer while another path
                // slice owns this frame's budget. Retry the public command on
                // later frames; do not inject a path or clear scheduler state.
                yield return null;
            }
        }
        Require(moveStarted,
            "manual move could not start: " + message);
        if (!moveStarted)
        {
            yield break;
        }
        scheduling.RequestImmediateDecision(actor);
        float deadline = Time.realtimeSinceStartup + 4f;
        while (Time.realtimeSinceStartup < deadline
            && actor.Blackboard.GetHandledDecisionCount(
                CharacterAiBranch.Critical) <= criticalBefore)
            yield return null;
        long criticalAfter = actor.Blackboard.GetHandledDecisionCount(
            CharacterAiBranch.Critical);
        bool sawCritical = criticalAfter > criticalBefore;
        Require(sawCritical,
            "manual command did not route through Critical BT branch");
        if (sawCritical)
        {
            evidence.Add("bt:critical=handled-count:"
                + criticalBefore + "->" + criticalAfter);
        }
        move.CancelActiveMovement();
        yield return null;
    }

    private static IEnumerable<CharacterActor> LiveVisitors() =>
        FindObjectsByType<CharacterActor>(FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(value => value != null && value.characterType == CharacterType.Customer
                && value.CurrentLifecycleState != CharacterLifecycleState.Despawned);

    private static string PersistentId(CharacterActor actor) =>
        actor?.Identity?.PersistentId ?? string.Empty;

    private void VerifyTamperedRetailRestoreFailsAtomically(
        IDungeonGameSaveService gameSaves,
        Shop shop,
        CharacterActor visitor,
        AbilityShopping shopping)
    {
        Require(gameSaves != null, "production game-save service missing");
        Require(shop != null && visitor != null && shopping != null,
            "retail restore fixture ownership missing");
        if (gameSaves == null || shop == null || visitor == null || shopping == null)
            return;

        string shopId = shop.PersistentInstanceId.IsValid
            ? shop.PersistentInstanceId.Value
            : string.Empty;
        Require(shopId.Length > 0, "retail restore fixture shop has no persistent id");
        if (shopId.Length == 0) return;

        string stockBefore = JsonUtility.ToJson(shop.CreateStockSnapshot());
        string visitorIdBefore = PersistentId(visitor);
        int moneyBefore = shopping.HoldingMoney;
        int commitsBefore = shopping.CommittedPurchaseCountForDiagnostics;
        long massBefore = shopping.CommittedPurchaseMassGramsForDiagnostics;

        DungeonGameSaveData canonical = gameSaves.Capture();
        DungeonGameSaveData tampered = gameSaves.FromJson(
            gameSaves.ToJson(canonical));
        ModularFacilityWorldSaveData world =
            DungeonSaveSectionPayload.ReadOrNew<ModularFacilityWorldSaveData>(
                tampered,
                ModularFacilityWorldSaveSection.Id);
        ModularFacilityBuildingSaveData savedShop = world.buildings?
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.persistentInstanceId,
                    shopId,
                    StringComparison.Ordinal));
        BuildingStateModuleSaveData stockModule = savedShop?.stateModules?
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.moduleId,
                    BuildingStateModuleIds.ShopStock,
                    StringComparison.Ordinal));
        ShopStockStateSnapshot stock = stockModule == null
            ? null
            : JsonUtility.FromJson<ShopStockStateSnapshot>(
                stockModule.payload ?? string.Empty);
        int activatedSource = stock?.activatedAuthoredSaleItemIds?.FirstOrDefault()
            ?? int.MinValue;
        bool fixtureReady = savedShop != null
            && stockModule != null
            && stock != null
            && (stock.activatedAuthoredSaleItemIds?.Count ?? 0) > 0;
        Require(fixtureReady,
            "retail restore tamper fixture could not locate an activated authored source");
        if (!fixtureReady) return;

        stock.activatedAuthoredSaleItemIds.Add(activatedSource);
        stockModule.payload = JsonUtility.ToJson(stock);
        DungeonSaveSectionPayload.Write(
            tampered,
            ModularFacilityWorldSaveSection.Id,
            1,
            DungeonSaveRestorePhase.World,
            world);
        tampered.manifest = DungeonSaveManifest.Capture(tampered.sections);

        bool accepted = gameSaves.TryRestore(
            tampered,
            out DungeonGameRestoreReport report);
        string errors = string.Join(" | ", report?.Errors
            ?? Array.Empty<string>());
        bool rejectedByRetailAuthority = !accepted
            && report != null
            && !report.Success
            && errors.IndexOf(
                "duplicate authored-source activation authority",
                StringComparison.OrdinalIgnoreCase) >= 0;
        Require(rejectedByRetailAuthority,
            "tampered retail whole restore was not rejected by exact-source authority: "
                + errors);

        string stockAfter = JsonUtility.ToJson(shop.CreateStockSnapshot());
        bool unchanged = ReferenceEquals(shop,
                FindObjectsByType<Shop>(FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .SingleOrDefault(value => value != null
                        && value.PersistentInstanceId.IsValid
                        && string.Equals(
                            value.PersistentInstanceId.Value,
                            shopId,
                            StringComparison.Ordinal)))
            && string.Equals(stockBefore, stockAfter, StringComparison.Ordinal)
            && string.Equals(visitorIdBefore, PersistentId(visitor),
                StringComparison.Ordinal)
            && moneyBefore == shopping.HoldingMoney
            && commitsBefore == shopping.CommittedPurchaseCountForDiagnostics
            && massBefore == shopping.CommittedPurchaseMassGramsForDiagnostics;
        Require(unchanged,
            "rejected retail restore changed live shop or visitor ownership");
        if (rejectedByRetailAuthority && unchanged)
        {
            evidence.Add("visitor-shopping-retail-save-tamper-rejected-atomically="
                + $"shop:{shopId};activation:{activatedSource};"
                + $"quantity:{shop.CreateStockSnapshot().lots.Sum(value => value.quantity)};"
                + $"money:{moneyBefore};commits:{commitsBefore};mass:{massBefore}");
        }
    }

    private static string DescribeExitTrace(
        CharacterActor actor,
        CharacterSpawner spawner,
        bool registryReleased,
        bool populationVisitReleased)
    {
        AbilityMove move = actor != null ? actor.GetAbility<AbilityMove>() : null;
        AIBrain brain = actor != null ? actor.Brain : null;
        string position = actor != null
            ? $"({actor.transform.position.x:F2},{actor.transform.position.y:F2})"
            : "destroyed";
        string lifecycle = actor != null
            ? actor.CurrentLifecycleState.ToString()
            : "destroyed";
        string active = actor != null
            ? actor.gameObject.activeInHierarchy.ToString()
            : "false";
        string failure = brain != null
            ? brain.LastActionFailure.Kind.ToString()
            : string.Empty;
        Vector3 entryTarget = spawner != null
            ? spawner.GetEntryDoorWorldPosition()
            : Vector3.zero;
        Vector3 outsideTarget = spawner != null
            ? spawner.GetOutsideSpawnWorldPosition()
            : Vector3.zero;
        return $"state={lifecycle};active={active};pos={position};"
            + $"entry=({entryTarget.x:F2},{entryTarget.y:F2});"
            + $"outside=({outsideTarget.x:F2},{outsideTarget.y:F2});"
            + $"moveRoutine={move != null && move.HasActiveMovementRoutineForDiagnostics};"
            + $"action={brain?.CurrentActionDebugLabel ?? string.Empty};"
            + $"phase={brain?.CurrentActionPhase ?? string.Empty};"
            + $"failure={failure};registryReleased={registryReleased};"
            + $"populationVisitReleased={populationVisitReleased};"
            + $"spawnerStage={spawner?.LastVisitorExitHandoffStage ?? string.Empty};"
            + $"handoffFailure={spawner?.LastVisitorExitHandoffFailure ?? string.Empty}";
    }

    private static string DescribeExitStateKey(
        CharacterActor actor,
        CharacterSpawner spawner,
        bool registryReleased,
        bool populationVisitReleased)
    {
        AbilityMove move = actor != null ? actor.GetAbility<AbilityMove>() : null;
        return $"{(actor != null ? actor.CurrentLifecycleState.ToString() : "destroyed")}|"
            + $"{(actor != null && actor.gameObject.activeInHierarchy)}|"
            + $"{(move != null && move.HasActiveMovementRoutineForDiagnostics)}|"
            + $"{registryReleased}|{populationVisitReleased}|"
            + (spawner?.LastVisitorExitHandoffStage ?? string.Empty) + "|"
            + (spawner?.LastVisitorExitHandoffFailure ?? string.Empty);
    }

    private static Shop EnsureProductionShop(Grid grid, out string detail)
    {
        Shop existing = FindObjectsByType<Shop>(FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(value => value != null && !value.isDestroy
                && value.Facility != null);
        if (existing != null)
        {
            detail = "existing-authored-shop:" + existing.name;
            return existing;
        }

        DungeonStoryGridBuildingController controller =
            FindFirstObjectByType<DungeonStoryGridBuildingController>(
                FindObjectsInactive.Include);
        BuildingSO definition = Resources.Load<BuildingSO>(
            "SO/Building/Modular/S01_판매카운터");
        if (controller == null || definition == null || grid == null)
        {
            detail = "production initial-building command prerequisites missing";
            return null;
        }

        foreach (GridCell cell in grid.GetCells()
            .Where(value => value != null)
            .OrderBy(value => value.Position.y)
            .ThenBy(value => value.Position.x))
        {
            Vector2Int position = cell.Position;
            IReadOnlyList<Vector2Int> footprint = definition.GetGridPosList(position);
            if (footprint.Any(value => !grid.IsValidGridPos(value)))
                continue;
            if (footprint.Any(value =>
                grid.GetGridCell(value)?.CanOccupy(GridLayer.Building) != true))
                continue;
            if (!controller.TryPlaceInitialBuildings(
                    new[] { new InitialBuildInfo
                    {
                        Position = position,
                        Building = definition
                    } },
                    out string message))
                continue;

            Shop created = FindObjectsByType<Shop>(FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(value => value != null && !value.isDestroy
                    && ReferenceEquals(value.BuildingData, definition));
            if (created != null)
            {
                detail = "production-initial-building-command:" + message;
                return created;
            }
        }

        detail = "official initial-building command could not place authored shop";
        return null;
    }

    private void Require(bool condition, string failure)
    {
        if (!condition) failures.Add(failure);
    }

    private void WriteReport(string result, string phase)
    {
        List<string> lines = new List<string>
        {
            "# Character Visitor And Control Journey PlayMode Verification",
            "result=" + result,
            "scope=" + (CharacterVisitorControlJourneyPlayModeVerifier.ExitOnlyRequested
                ? "official-GameplayScene+CharacterSpawner+production-Brain/BT+visitor-exit-A03"
                : CharacterVisitorControlJourneyPlayModeVerifier
                    .VandalismOnlyRequested
                    ? "official-GameplayScene+CharacterSpawner+production-Brain/BT+facility-vandalism-A04"
                : "official-GameplayScene+CharacterSpawner+production-Brain/BT+production-shopping"),
            "utc=" + DateTime.UtcNow.ToString("O"),
            "verifierRevision=" + Revision,
            "fullAuditClosure=false",
            "humanApprovalClaimed=false",
            "trainingEligible=false",
            "phase=" + phase
        };
        lines.AddRange(evidence.Select(value => "PASS\t" + value));
        lines.AddRange(failures.Select(value => "FAIL\t" + value));
        string reportPath = CharacterVisitorControlJourneyPlayModeVerifier
            .ActiveReportPath;
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        File.WriteAllLines(reportPath, lines);
    }
}
#endif
