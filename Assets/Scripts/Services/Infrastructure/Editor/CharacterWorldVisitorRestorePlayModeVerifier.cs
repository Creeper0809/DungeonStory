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

public static class CharacterWorldVisitorRestorePlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/character-world-visitor-restore-playmode.txt";
    private const string PendingPath =
        "Temp/character-world-visitor-restore-playmode.flag";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";

    [MenuItem("DungeonStory/Debug/QA/Run Character World Visitor Restore Verification")]
    public static void RequestRun()
    {
        if (EditorApplication.isPlaying)
        {
            StartRunner(false);
            return;
        }

        Directory.CreateDirectory("Temp");
        File.WriteAllText(PendingPath, DateTime.UtcNow.ToString("O"));
        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!File.Exists(PendingPath))
        {
            return;
        }
        File.Delete(PendingPath);
        StartRunner(true);
    }

    private static void StartRunner(bool exitPlayMode)
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                CharacterWorldVisitorRestorePlayModeRunner>() != null)
        {
            return;
        }

        CharacterWorldVisitorRestorePlayModeRunner runner =
            new GameObject("Character World Visitor Restore PlayMode Runner")
                .AddComponent<CharacterWorldVisitorRestorePlayModeRunner>();
        runner.ExitPlayModeOnCompletion = exitPlayMode;
    }
}

public sealed class CharacterWorldVisitorRestorePlayModeRunner : MonoBehaviour
{
    private const float OverallTimeout = 240f;
    private const string Revision = "character-world-visitor-restore-v2";
    private readonly List<string> evidence = new List<string>();
    private readonly List<string> failures = new List<string>();
    private IDungeonSaveSectionRegistry saveRegistry;
    private List<DungeonSaveSectionEnvelope> baseline;
    private bool baselineRestored;

    public bool ExitPlayModeOnCompletion { get; set; }

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        WriteReport("RUNNING", "setup");
        float deadline = Time.realtimeSinceStartup + OverallTimeout;
        yield return RunGuarded(deadline);
        CompleteRun();
    }

    private IEnumerator RunGuarded(float deadline)
    {
        IEnumerator run = null;
        try
        {
            run = Run(deadline);
        }
        catch (Exception exception)
        {
            failures.Add(exception.ToString());
        }

        if (run == null)
        {
            yield break;
        }

        while (true)
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                failures.Add("overall timeout");
                yield break;
            }

            bool moved;
            object current = null;
            try
            {
                moved = run.MoveNext();
                if (moved)
                {
                    current = run.Current;
                }
            }
            catch (Exception exception)
            {
                failures.Add(exception.ToString());
                yield break;
            }

            if (!moved)
            {
                yield break;
            }
            yield return current;
        }
    }

    private void CompleteRun()
    {
        if (!baselineRestored && saveRegistry != null && baseline != null)
        {
            try
            {
                DungeonGameRestoreReport cleanup = new DungeonGameRestoreReport();
                if (!saveRegistry.RestoreAll(baseline, cleanup))
                {
                    failures.Add("cleanup restore failed: "
                        + string.Join(" | ", cleanup.Errors));
                }
            }
            catch (Exception exception)
            {
                failures.Add("cleanup restore exception: " + exception);
            }
        }

        WriteReport(failures.Count == 0 ? "PASS" : "FAIL", "complete");
        if (failures.Count == 0)
        {
            Debug.Log("[CharacterWorldVisitorRestore] PASS");
        }
        else
        {
            Debug.LogError("[CharacterWorldVisitorRestore] "
                + string.Join(" | ", failures));
        }
        Destroy(gameObject);
        if (ExitPlayModeOnCompletion)
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = false;
                }
            };
        }
    }

    private IEnumerator Run(float deadline)
    {
        Require(string.Equals(
                SceneManager.GetActiveScene().path,
                "Assets/Scenes/GameplayScene.unity",
                StringComparison.OrdinalIgnoreCase),
            "official GameplayScene is not active");

        DungeonRuntimeLifetimeScope scope = null;
        CharacterSpawner spawner = null;
        float setupDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 15f);
        while (Time.realtimeSinceStartup < setupDeadline)
        {
            scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
            spawner = FindFirstObjectByType<CharacterSpawner>(
                FindObjectsInactive.Include);
            if (scope?.Container != null && spawner != null)
            {
                break;
            }
            yield return null;
        }
        Require(scope?.Container != null, "production LifetimeScope missing");
        Require(spawner != null, "production CharacterSpawner missing");
        if (failures.Count > 0)
        {
            yield break;
        }

        saveRegistry = scope.Container.Resolve<IDungeonSaveSectionRegistry>();
        ICharacterWorldSaveService saveService =
            scope.Container.Resolve<ICharacterWorldSaveService>();
        CharacterWorldSaveService concreteSave =
            saveService as CharacterWorldSaveService;
        ICharacterLifetimeQuery lifetime =
            scope.Container.Resolve<ICharacterLifetimeQuery>();
        ICharacterProficiencyQuery proficiency =
            scope.Container.Resolve<ICharacterProficiencyQuery>();
        ICharacterPopulationService population =
            scope.Container.Resolve<ICharacterPopulationService>();
        IGameCalendar calendar = scope.Container.Resolve<IGameCalendar>();
        IEnvironmentalFieldQuery environment =
            scope.Container.Resolve<IEnvironmentalFieldQuery>();
        IFactionRuntime factions = scope.Container.Resolve<IFactionRuntime>();
        IGridSystemProvider grids = scope.Container.Resolve<IGridSystemProvider>();
        Require(concreteSave != null, "concrete character-world save service missing");
        Require(grids.TryGetGrid(out Grid grid) && grid != null,
            "production grid missing");
        if (failures.Count > 0)
        {
            yield break;
        }

        float environmentDeadline = Mathf.Min(
            deadline,
            Time.realtimeSinceStartup + 30f);
        while (!environment.IsInitialized
               && Time.realtimeSinceStartup < environmentDeadline)
        {
            yield return null;
        }
        Require(environment.IsInitialized,
            $"environmental field initialization timed out; "
            + $"version={environment.Version}");
        if (!environment.IsInitialized)
        {
            yield break;
        }
        evidence.Add($"environment-ready;version={environment.Version}");

        // Wait for every registered save authority first, then commit
        // synchronously and capture before the spawner's next 0.3 s production
        // retry. This makes every subsequently published customer a genuine
        // post-baseline actor without synthesizing a profile or actor.
        evidence.Add("start-party="
            + StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug());
        baseline = saveRegistry.CaptureAll();
        DungeonSaveSectionEnvelope characterEnvelope = baseline.Single(value =>
            string.Equals(
                value.sectionId,
                CharacterWorldSaveSection.Id,
                StringComparison.Ordinal));
        DungeonCharacterWorldSaveData characterBaseline =
            JsonUtility.FromJson<DungeonCharacterWorldSaveData>(
                characterEnvelope.payloadJson);
        DungeonSaveSectionEnvelope bodyHealthEnvelope = baseline.Single(value =>
            string.Equals(
                value.sectionId,
                CharacterBodyHealthSaveSection.Id,
                StringComparison.Ordinal));
        string baselineBodyHealthPayload = bodyHealthEnvelope.payloadJson
            ?? string.Empty;
        HashSet<string> baselinePopulationIds = new HashSet<string>(
            (characterBaseline.populationProfiles
                ?? new List<WorldCharacterProfile>())
            .Where(value => value != null)
            .Select(value => value.persistentId),
            StringComparer.Ordinal);
        evidence.Add("baseline-population=" + baselinePopulationIds.Count);

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
            "no authored faction customer is available");
        if (visitorDefinition == null)
        {
            yield break;
        }

        string factionId = visitorDefinition.species.homeFactionId;
        int trustCommands = 0;
        while (!factions.IsContractUnlocked(
                factionId,
                FactionContractKind.Recruitment)
            && trustCommands++ < 4)
        {
            Require(factions.TryAdjustTrust(
                    factionId,
                    100,
                    "playmode:visitor-restore",
                    out string trustMessage),
                "faction trust command failed: " + trustMessage);
        }
        Require(factions.IsContractUnlocked(
                factionId,
                FactionContractKind.Recruitment),
            "visitor recruitment contract did not unlock");
        Shop shop = EnsureProductionShop(grid, out string shopDetail);
        Require(shop != null, shopDetail);
        if (failures.Count > 0)
        {
            yield break;
        }

        CharacterActor visitor = null;
        CharacterSpawnRejection lastRejection = CharacterSpawnRejection.None;
        int attempts = 0;
        float spawnDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 180f);
        while (visitor == null && Time.realtimeSinceStartup < spawnDeadline)
        {
            attempts++;
            spawner.TrySpawnCharacter(visitorDefinition.id, out lastRejection);
            visitor = lifetime.AllCharacters
                .Where(value => value != null
                    && value.gameObject.activeInHierarchy
                    && value.Identity?.CharacterType == CharacterType.Customer)
                .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
                .FirstOrDefault(value => !baselinePopulationIds.Contains(
                    value.Identity.PersistentId));
            if (visitor == null)
            {
                yield return null;
            }
        }
        Require(visitor != null,
            $"post-baseline production visitor missing; attempts={attempts};"
            + $"last={lastRejection}");
        if (visitor == null)
        {
            yield break;
        }

        string visitorId = visitor.Identity.PersistentId;
        Require(proficiency.TryGetProficiency(
                new CharacterId(visitorId),
                BuiltInCharacterProficiencyIds.Fieldwork,
                calendar.AbsoluteHour,
                out _),
            "visitor did not own fieldwork before restore");
        evidence.Add($"visitor={visitorId};attempts={attempts};shop={shopDetail}");

        concreteSave.BeginRestoreCandidate();
        CharacterWorldRestoreCandidate rollbackCandidate =
            concreteSave.PrepareRestoreCandidate(grid, characterBaseline);
        concreteSave.StageRestoreCandidate(rollbackCandidate);
        concreteSave.PublishRestoreCandidate();
        Require(visitor.gameObject.activeInHierarchy,
            "visitor retired before restore commit");
        concreteSave.RollbackPublishedRestoreCandidate();
        Require(visitor.gameObject.activeInHierarchy,
            "visitor was not preserved by restore rollback");
        evidence.Add("rollback-preserved-active-visitor");

        DungeonGameRestoreReport restoreReport = new DungeonGameRestoreReport();
        bool restored = saveRegistry.RestoreAll(baseline, restoreReport);
        Require(restored,
            "full baseline restore failed: "
            + string.Join(" | ", restoreReport.Errors));
        baselineRestored = restored;
        if (!restored)
        {
            yield break;
        }

        IReadOnlyList<DungeonSaveSectionEnvelope> immediateCapture =
            saveRegistry.CaptureAll();
        string immediateBodyHealthPayload = immediateCapture.Single(value =>
            string.Equals(
                value.sectionId,
                CharacterBodyHealthSaveSection.Id,
                StringComparison.Ordinal)).payloadJson ?? string.Empty;
        Require(string.Equals(
                baselineBodyHealthPayload,
                immediateBodyHealthPayload,
                StringComparison.Ordinal),
            "committed restore recreated or removed body-health state before "
            + "the immediate capture boundary");
        CharacterAiRuntimeGateSnapshot retiredGate =
            visitor.Brain?.CaptureRuntimeGateSnapshot()
            ?? default;
        Require(!visitor.gameObject.activeInHierarchy
                && visitor.CurrentLifecycleState
                    == CharacterLifecycleState.Despawned,
            "committed restore did not synchronously return visitor to pool");
        Require(retiredGate.LivePathRequests == 0
                && retiredGate.LiveReservations == 0,
            $"retired visitor leaked paths/reservations: "
            + $"{retiredGate.LivePathRequests}/{retiredGate.LiveReservations}");
        Require(visitor.CarryInventory == null
                || visitor.CarryInventory.Items.Count == 0,
            "retired visitor retained physical carry ownership");
        Require(spawner.RetireVisitorForWorldRestore(
                visitor,
                out string idempotentFailure),
            "synchronous idempotent retirement failed: "
            + idempotentFailure);

        DungeonGameRestoreReport secondRestoreReport =
            new DungeonGameRestoreReport();
        bool secondRestore = saveRegistry.RestoreAll(
            baseline,
            secondRestoreReport);
        Require(secondRestore,
            "second full baseline restore failed: "
            + string.Join(" | ", secondRestoreReport.Errors));
        if (!secondRestore)
        {
            yield break;
        }

        IReadOnlyList<DungeonSaveSectionEnvelope> secondImmediateCapture =
            saveRegistry.CaptureAll();
        string secondBodyHealthPayload = secondImmediateCapture.Single(value =>
            string.Equals(
                value.sectionId,
                CharacterBodyHealthSaveSection.Id,
                StringComparison.Ordinal)).payloadJson ?? string.Empty;
        Require(string.Equals(
                baselineBodyHealthPayload,
                secondBodyHealthPayload,
                StringComparison.Ordinal),
            "second restore was not body-health byte-idempotent at the "
            + "immediate capture boundary");
        evidence.Add("body-health-immediate-restore-exact=true;double-restore=true");

        // RestoreAll is synchronous. Assert exact narrative replacement at its
        // commit boundary, before the production spawner gets another frame to
        // replenish an intentionally empty baseline pool. Population serials
        // are restored from the saved profiles, so a later replenishment may
        // legitimately issue the same deterministic ID to a new individual.
        Require(!proficiency.TryGetProficiency(
                new CharacterId(visitorId),
                BuiltInCharacterProficiencyIds.Fieldwork,
                calendar.AbsoluteHour,
                out _),
            "post-baseline visitor narrative survived the restore commit boundary");
        evidence.Add("commit-narrative-exact-replacement=true");

        yield return null;
        yield return null;

        bool deterministicIdReissued = population.Profiles.Any(profile =>
            profile != null
            && string.Equals(
                profile.persistentId,
                visitorId,
                StringComparison.Ordinal));
        evidence.Add("post-commit-pool-replenishment-id-reissued="
            + deterministicIdReissued);
        evidence.Add("commit-retired=pool+path+reservation+carry;idempotent=true");
    }

    private static Shop EnsureProductionShop(Grid grid, out string detail)
    {
        Shop existing = FindObjectsByType<Shop>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(value => value != null
                && !value.isDestroy
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
            "SO/Building/P1/P1_GeneralStore");
        if (controller == null || definition == null || grid == null)
        {
            detail = "production shop prerequisites missing";
            return null;
        }

        foreach (GridCell cell in grid.GetCells()
                     .Where(value => value != null)
                     .OrderBy(value => value.Position.y)
                     .ThenBy(value => value.Position.x))
        {
            Vector2Int position = cell.Position;
            IReadOnlyList<Vector2Int> footprint =
                definition.GetGridPosList(position);
            if (footprint.Any(value => !grid.IsValidGridPos(value))
                || footprint.Any(value =>
                    grid.GetGridCell(value)?.CanOccupy(GridLayer.Building)
                        != true))
            {
                continue;
            }
            if (!controller.TryPlaceInitialBuildings(
                    new[]
                    {
                        new InitialBuildInfo
                        {
                            Position = position,
                            Building = definition
                        }
                    },
                    out string message))
            {
                continue;
            }

            Shop created = FindObjectsByType<Shop>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(value => value != null
                    && !value.isDestroy
                    && ReferenceEquals(value.BuildingData, definition));
            if (created != null)
            {
                detail = "production-shop:" + message;
                return created;
            }
        }

        detail = "official initial-building command could not place shop";
        return null;
    }

    private void Require(bool condition, string failure)
    {
        if (!condition)
        {
            failures.Add(failure);
        }
        WriteReport(failures.Count == 0 ? "RUNNING" : "FAIL", failure);
    }

    private void WriteReport(string result, string phase)
    {
        List<string> lines = new List<string>
        {
            "# Character World Visitor Restore PlayMode Verification",
            "result=" + result,
            "scope=official-GameplayScene+CharacterSpawner+full-save-restore",
            "utc=" + DateTime.UtcNow.ToString("O"),
            "verifierRevision=" + Revision,
            "phase=" + phase
        };
        lines.AddRange(evidence.Select(value => "PASS\t" + value));
        lines.AddRange(failures.Select(value => "FAIL\t" + value));
        File.WriteAllLines(
            CharacterWorldVisitorRestorePlayModeVerifier.ReportPath,
            lines);
    }
}
#endif
