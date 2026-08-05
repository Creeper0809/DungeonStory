#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using VContainer;

[InitializeOnLoad]
public static class DarkSurvivalPlayModeVerifier
{
    public const string RequestPath = "Temp/dark-survival-playmode.request";
    public const string ReportPath = "Artifacts/QA/dark-survival-playmode-report.txt";
    public const string CapturePath = "Artifacts/QA/dark-survival-health-and-filth.png";
    public const string WorldCapturePath = "Artifacts/QA/dark-survival-world-water-and-filth.png";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";

    private static bool runnerCreated;

    static DarkSurvivalPlayModeVerifier()
    {
        InstallHooks();
    }

    private static void InstallHooks()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/QA/Request Dark Survival Verification")]
    public static void RequestRunFromMenu()
    {
        runnerCreated = false;
        InstallHooks();
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ReportPath);
        File.Delete(CapturePath);
        File.Delete(WorldCapturePath);
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
        EditorApplication.delayCall -= BeginRequestedPlayMode;
        EditorApplication.delayCall += BeginRequestedPlayMode;
    }

    private static void BeginRequestedPlayMode()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!string.Equals(SceneManager.GetActiveScene().path, GameplayScenePath, StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }

        EditorApplication.EnterPlaymode();
    }

    private static void OnEditorUpdate()
    {
        if (!File.Exists(RequestPath))
        {
            return;
        }

        if (EditorApplication.isPlaying)
        {
            EnsureRunnerCreated();
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!string.Equals(SceneManager.GetActiveScene().path, GameplayScenePath, StringComparison.OrdinalIgnoreCase))
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

        if (change != PlayModeStateChange.EnteredPlayMode || !File.Exists(RequestPath))
        {
            return;
        }

        EnsureRunnerCreated();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapRequestedRunAfterSceneLoad()
    {
        if (!File.Exists(RequestPath)
            || UnityEngine.Object.FindFirstObjectByType<DarkSurvivalPlayModeVerificationRunner>() != null)
        {
            return;
        }

        EnsureRunnerCreated();
    }

    private static void EnsureRunnerCreated()
    {
        if (runnerCreated
            && UnityEngine.Object.FindFirstObjectByType<
                DarkSurvivalPlayModeVerificationRunner>() != null)
        {
            return;
        }

        runnerCreated = true;
        new GameObject("Dark Survival PlayMode Verification Runner")
            .AddComponent<DarkSurvivalPlayModeVerificationRunner>();
    }
}

public sealed class DarkSurvivalPlayModeVerificationRunner : MonoBehaviour
{
    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> capturedErrors = new List<string>();
    private readonly List<string> capturedWarnings = new List<string>();
    private float originalTimeScale;

    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLogMessageReceived;
        originalTimeScale = Time.timeScale;
        Time.timeScale = 5f;
        Screen.SetResolution(1600, 900, false);

        yield return null;
        yield return null;
        yield return EnsurePlayableRun();

        ICharacterDeprivationRuntime deprivation = ResolveService<ICharacterDeprivationRuntime>();
        IWorldFilthQuery filth = ResolveService<IWorldFilthQuery>();
        IWorldWaterQuery water = ResolveService<IWorldWaterQuery>();
        CharacterActor actor = FindTestActor();
        Check(deprivation != null, "DEPRIVATION_RUNTIME", deprivation != null ? "ready" : "missing");
        Check(filth != null, "FILTH_RUNTIME", filth != null ? "ready" : "missing");
        Check(water != null, "WATER_RUNTIME", water != null ? "ready" : "missing");
        Check(actor != null, "TEST_ACTOR", actor != null ? GetActorLabel(actor) : "missing");
        if (deprivation == null || filth == null || water == null || actor == null)
        {
            Finish();
            yield break;
        }

        VerifyWaterWorld(water);
        yield return VerifyReliefBreakdown(deprivation, filth, actor);
        yield return VerifyDesperateDrink(deprivation, water, actor);
        FocusWorldCamera(actor);
        yield return CaptureScreen(DarkSurvivalPlayModeVerifier.WorldCapturePath, "WORLD_SCREEN_CAPTURE_NONBLANK");
        yield return VerifyHealthUi(actor, deprivation);
        yield return CaptureScreen(DarkSurvivalPlayModeVerifier.CapturePath, "HEALTH_SCREEN_CAPTURE_NONBLANK");
        VerifyNonlethalSuppression(actor, deprivation);
        Finish();
    }

    private IEnumerator EnsurePlayableRun()
    {
        OwnerRunManager ownerManager = FindFirstObjectByType<OwnerRunManager>();
        if (ownerManager == null || ownerManager.CurrentOwnerActor == null)
        {
            string commit = StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            report.Add("[INFO] FAST_PARTY_COMMIT " + commit);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }
        }

        ownerManager = FindFirstObjectByType<OwnerRunManager>();
        Check(ownerManager != null && ownerManager.CurrentOwnerActor != null,
            "RUN_READY",
            ownerManager != null && ownerManager.CurrentOwnerActor != null
                ? GetActorLabel(ownerManager.CurrentOwnerActor)
                : "owner missing");
    }

    private void VerifyWaterWorld(IWorldWaterQuery water)
    {
        IReadOnlyList<WorldWaterSourceSnapshot> sources = water.GetAllSources();
        Tilemap tilemap = Resources.FindObjectsOfTypeAll<Tilemap>()
            .FirstOrDefault(candidate => candidate != null
                && candidate.gameObject.scene.IsValid()
                && candidate.name == "Water"
                && candidate.transform.parent != null
                && candidate.transform.parent.name == "World Water Tilemap");
        Check(sources.Count >= 2,
            "WORLD_WATER_SOURCES",
            string.Join(", ", sources.Select(source => $"{source.SourceId}:{source.TerrainType}:{source.Quality}@{source.Position}")));
        Check(sources.Any(source => source.TerrainType == GridCellTerrainType.ShallowWater)
                && sources.Any(source => source.TerrainType == GridCellTerrainType.DeepWater),
            "WATER_TERRAIN_TYPES",
            $"shallow={sources.Count(source => source.TerrainType == GridCellTerrainType.ShallowWater)}; deep={sources.Count(source => source.TerrainType == GridCellTerrainType.DeepWater)}");
        int visibleSourceCells = tilemap != null
            ? sources.Count(source => tilemap.HasTile(new Vector3Int(-source.Position.x, source.Position.y, 0)))
            : 0;
        Check(tilemap != null && visibleSourceCells == sources.Count,
            "WATER_TILEMAP_VISIBLE",
            tilemap != null
                ? $"sourceCells={visibleSourceCells}/{sources.Count}; tileAssets={tilemap.GetUsedTilesCount()}"
                : "tilemap missing");
    }

    private IEnumerator VerifyReliefBreakdown(
        ICharacterDeprivationRuntime deprivation,
        IWorldFilthQuery filth,
        CharacterActor actor)
    {
        actor.ChangesStat(CharacterCondition.EXCRETION, -200f);
        int before = filth.GetAll().Count;
        SetBreakdown(deprivation, actor, DeprivationKind.Bladder, CharacterBreakdownKind.DesperateRelief);
        bool started = deprivation.TryRunActiveBreakdown(actor, out string status);
        Check(started && !string.IsNullOrWhiteSpace(status), "RELIEF_BREAKDOWN_STARTED", status);

        float timeout = Time.realtimeSinceStartup + 14f;
        while (Time.realtimeSinceStartup < timeout && filth.GetAll().Count < before + 2)
        {
            yield return null;
        }

        IReadOnlyList<WorldFilthSnapshot> added = filth.GetAll().Skip(before).ToArray();
        float excretion = GetNeed(actor, CharacterCondition.EXCRETION);
        Check(added.Any(entry => entry.Type == WorldFilthType.Waste)
                && added.Any(entry => entry.WallStain),
            "RELIEF_CREATED_FILTH",
            $"before={before}; after={filth.GetAll().Count}; types={string.Join(",", added.Select(entry => entry.Type + (entry.WallStain ? ":wall" : string.Empty)))}");
        Check(excretion >= 50f, "RELIEF_RESTORED_BLADDER", $"excretion={excretion:0.#}");
        WorldFilthWorkTarget workTarget = FindObjectsByType<WorldFilthWorkTarget>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault();
        Check(workTarget != null,
            "FILTH_CLEAN_WORK_TARGET",
            $"targets={FindObjectsByType<WorldFilthWorkTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}");
        if (workTarget != null)
        {
            ResolveService<DungeonStory.Foundation.IGameEventBus>()?.ShowInfo(
                new BuildingInfoTarget(workTarget));
            yield return null;
            BuildingSummaryInfo panel = FindFirstObjectByType<BuildingSummaryInfo>(FindObjectsInactive.Include);
            Button priorityButton = panel != null
                ? panel.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(button => button != null && button.name == "CleanPriorityButton")
                : null;
            Check(panel != null
                    && panel.UI != null
                    && panel.UI.activeInHierarchy
                    && panel.stock != null
                    && !string.IsNullOrEmpty(panel.stock.text)
                    && panel.stock.text.Contains("감염도", StringComparison.Ordinal)
                    && panel.stock.text.Contains("청소 작업량", StringComparison.Ordinal),
                "FILTH_INFO_VISIBLE",
                panel?.stock != null ? panel.stock.text.Replace('\n', ' ') : "panel missing");
            Check(priorityButton != null, "FILTH_PRIORITY_BUTTON", priorityButton != null ? "ready" : "missing");
            priorityButton?.onClick.Invoke();
            yield return null;
            Check(workTarget.IsPriorityCleaning
                    && Mathf.Approximately(workTarget.GetWorkUrgency(BuiltInWorkTypeIds.Clean), 100f),
                "FILTH_PRIORITY_COMMAND",
                $"priority={workTarget.IsPriorityCleaning}; urgency={workTarget.GetWorkUrgency(BuiltInWorkTypeIds.Clean):0.#}");
        }
    }

    private IEnumerator VerifyDesperateDrink(
        ICharacterDeprivationRuntime deprivation,
        IWorldWaterQuery water,
        CharacterActor actor)
    {
        actor.ChangesStat(CharacterCondition.THIRST, -200f);
        float before = water.GetAllSources().Sum(source => source.Remaining);
        SetBreakdown(deprivation, actor, DeprivationKind.Thirst, CharacterBreakdownKind.DesperateDrink);
        bool started = deprivation.TryRunActiveBreakdown(actor, out string status);
        Check(started && !string.IsNullOrWhiteSpace(status), "DRINK_BREAKDOWN_STARTED", status);

        float timeout = Time.realtimeSinceStartup + 20f;
        while (Time.realtimeSinceStartup < timeout && GetNeed(actor, CharacterCondition.THIRST) < 20f)
        {
            yield return null;
        }

        float afterPreferred = water.GetAllSources().Sum(source => source.Remaining);
        float preferredThirst = GetNeed(actor, CharacterCondition.THIRST);
        Check(preferredThirst >= 20f,
            "SAFE_WATER_PRIORITY",
            $"water={before:0.##}->{afterPreferred:0.##}; thirst={preferredThirst:0.#}; pos={actor.GetNowXY()}");

        // The need is recovered before the coroutine's finally block releases
        // its per-actor execution slot. Keep the next scenario independent.
        yield return null;
        yield return null;

        BuildableObject[] waterFacilities = UnityEngine.Object
            .FindObjectsByType<BuildableObject>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where(building => building != null
                && building.gameObject.activeInHierarchy
                && building.BuildingData?.GetAbility<BuildingWaterSourceAbility>() != null)
            .ToArray();
        foreach (BuildableObject facility in waterFacilities)
        {
            facility.gameObject.SetActive(false);
        }

        IWorldItemStackRuntime itemStacks = ResolveService<IWorldItemStackRuntime>();
        List<WorldItemStockCandidate> safeWaterStacks = new();
        itemStacks?.CopyAvailableStockCandidates(
            StockCategory.Water,
            safeWaterStacks);
        int removedSafeWater = 0;
        foreach (WorldItemStockCandidate candidate in safeWaterStacks)
        {
            if (candidate.IsValid
                && itemStacks.TryConsumeStackQuantity(
                    candidate.StackId,
                    candidate.Quantity,
                    out _))
            {
                removedSafeWater += candidate.Quantity;
            }
        }
        safeWaterStacks.Clear();
        itemStacks?.CopyAvailableStockCandidates(
            StockCategory.Water,
            safeWaterStacks);
        Check(
            itemStacks != null && safeWaterStacks.Count == 0,
            "EXTERIOR_SAFE_WATER_EXHAUSTED",
            $"removed={removedSafeWater}; remaining={safeWaterStacks.Count}");

        bool scenarioSourceCreated = water.DebugCreateSource(
            actor.GetNowXY(),
            WorldWaterQuality.Unsafe,
            12f,
            GridCellTerrainType.ShallowWater,
            out string scenarioSourceId);
        Check(
            scenarioSourceCreated,
            "EXTERIOR_WATER_SCENARIO_SOURCE",
            scenarioSourceCreated
                ? $"{scenarioSourceId}@{actor.GetNowXY()}"
                : $"failed@{actor.GetNowXY()}");

        float infectionBefore = deprivation.TryGetSnapshot(actor, out CharacterDeprivationSnapshot beforeSnapshot)
            ? beforeSnapshot.InfectionBurden
            : 0f;
        actor.ChangesStat(CharacterCondition.THIRST, -200f);
        float externalBefore = water.GetAllSources().Sum(source => source.Remaining);
        SetBreakdown(deprivation, actor, DeprivationKind.Thirst, CharacterBreakdownKind.DesperateDrink);
        started = deprivation.TryRunActiveBreakdown(actor, out status);
        Check(started, "EXTERIOR_DRINK_BREAKDOWN_STARTED", status);
        timeout = Time.realtimeSinceStartup + 20f;
        while (Time.realtimeSinceStartup < timeout
            && GetNeed(actor, CharacterCondition.THIRST) < 20f
            && water.GetAllSources().Sum(source => source.Remaining) >= externalBefore - 0.01f)
        {
            yield return null;
        }

        foreach (BuildableObject facility in waterFacilities)
        {
            if (facility != null)
            {
                facility.gameObject.SetActive(true);
            }
        }

        float externalAfter = water.GetAllSources().Sum(source => source.Remaining);
        float thirst = GetNeed(actor, CharacterCondition.THIRST);
        Check(externalAfter < externalBefore && thirst >= 20f,
            "EXTERIOR_WATER_CONSUMED",
            $"water={externalBefore:0.##}->{externalAfter:0.##}; thirst={thirst:0.#}; pos={actor.GetNowXY()}");
        Check(deprivation.TryGetSnapshot(actor, out CharacterDeprivationSnapshot snapshot)
                && snapshot.InfectionBurden > infectionBefore,
            "UNSAFE_WATER_HEALTH_COST",
            deprivation.TryGetSnapshot(actor, out snapshot)
                ? $"infection={infectionBefore:0.#}->{snapshot.InfectionBurden:0.#}"
                : "snapshot missing");
    }

    private IEnumerator VerifyHealthUi(CharacterActor actor, ICharacterDeprivationRuntime deprivation)
    {
        ResolveService<DungeonStory.Foundation.IGameEventBus>()?.ShowInfo(actor);
        yield return null;
        CharacterSummaryInfo summary = FindFirstObjectByType<CharacterSummaryInfo>(FindObjectsInactive.Include);
        summary?.ShowHealthTab();
        yield return new WaitForSecondsRealtime(0.35f);

        TMP_Text healthText = Resources.FindObjectsOfTypeAll<TMP_Text>()
            .FirstOrDefault(text => text != null
                && text.gameObject.scene.IsValid()
                && text.gameObject.activeInHierarchy
                && !string.IsNullOrEmpty(text.text)
                && text.text.Contains("붕괴 확률", StringComparison.Ordinal));
        TMP_Text warning = actor.GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(text => text != null && text.name == "DeprivationWarning");
        Check(summary != null && summary.UI != null && summary.UI.activeInHierarchy,
            "HEALTH_PANEL_OPEN",
            summary != null ? $"active={summary.UI != null && summary.UI.activeInHierarchy}" : "summary missing");
        Check(healthText != null && healthText.text.Contains("감염 부담", StringComparison.Ordinal),
            "HEALTH_DETAILS_VISIBLE",
            healthText != null ? healthText.text.Replace('\n', ' ') : "health text missing");
        Check(warning != null,
            "DEPRIVATION_WARNING_LABEL",
            warning != null ? $"active={warning.gameObject.activeSelf}; text={warning.text}" : "warning missing");
        Check(deprivation.TryGetSnapshot(actor, out CharacterDeprivationSnapshot snapshot)
                && snapshot.Burdens.ContainsKey(DeprivationKind.Thirst),
            "HEALTH_SNAPSHOT_BOUND",
            deprivation.TryGetSnapshot(actor, out snapshot) ? $"highest={snapshot.HighestBurden:0.#}" : "snapshot missing");
    }

    private void VerifyNonlethalSuppression(
        CharacterActor actor,
        ICharacterDeprivationRuntime deprivation)
    {
        SetBreakdown(
            deprivation,
            actor,
            DeprivationKind.MentalInstability,
            CharacterBreakdownKind.ViolentImpulse);
        float healthBefore = actor.CurrentHealth;
        bool applied = deprivation.ApplySuppression(actor, 100f, out bool ended);
        float healthAfter = actor.CurrentHealth;
        Check(applied
                && ended
                && !deprivation.HasActiveBreakdown(actor)
                && !actor.IsDead
                && healthBefore - healthAfter <= 2.6f,
            "NONLETHAL_SUPPRESSION",
            $"applied={applied}; ended={ended}; alive={!actor.IsDead}; health={healthBefore:0.#}->{healthAfter:0.#}");
    }

    private static void SetBreakdown(
        ICharacterDeprivationRuntime runtime,
        CharacterActor actor,
        DeprivationKind cause,
        CharacterBreakdownKind kind)
    {
        DungeonDarkSurvivalSaveData save = runtime.Capture();
        string id = GetPersistentId(actor);
        CharacterDeprivationState state = save.characters.FirstOrDefault(
            entry => entry.characterId == id);
        if (state == null)
        {
            state = new CharacterDeprivationState { characterId = id };
            save.characters.Add(state);
        }

        state.burdens = Enum.GetValues(typeof(DeprivationKind))
            .Cast<DeprivationKind>()
            .Select(entry => new DeprivationBurdenSaveData
            {
                kind = entry,
                burden = entry == cause ? 100f : 0f,
                maximumHeldSeconds = entry == cause ? 30f : 0f,
                nextBreakdownCheckAt = Time.time + 30f,
                nextDamageAt = Time.time + 30f
            })
            .ToList();
        state.breakdown = new CharacterBreakdownState
        {
            active = true,
            cause = cause,
            kind = kind,
            startedAt = Time.time,
            suppressionResistance = 35f,
            lastReplanReason = "PlayMode 검증"
        };
        state.breakdownGeneration = Math.Max(1, state.breakdownGeneration + 1);
        state.dispatchedBreakdownGeneration = state.breakdownGeneration;
        runtime.PublishRestoreCandidate(
            runtime.BuildRestoreCandidate(save));
    }

    private static void FocusWorldCamera(CharacterActor actor)
    {
        Camera camera = Camera.main;
        if (camera == null || actor == null)
        {
            return;
        }

        Vector3 position = camera.transform.position;
        position.x = actor.transform.position.x;
        camera.transform.position = position;
    }

    private IEnumerator CaptureScreen(string path, string checkKey)
    {
        yield return PlayModeVerificationFrameWait.CaptureReady();
        Texture2D capture = PlayModeVerificationFrameWait.CaptureScreenshotAsTexture();
        if (capture == null)
        {
            Check(false, checkKey, "capture returned null");
            yield break;
        }

        byte[] bytes = capture.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        Check(bytes.Length > 1000, checkKey, $"{path}; bytes={bytes.Length}");
        Destroy(capture);
    }

    private static CharacterActor FindTestActor()
    {
        return CharacterActorCollection.DistinctByGameObject(FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None))
            .Where(actor => actor != null && !actor.IsDead && actor.TryGetAbility(out AbilityMove _))
            .OrderBy(actor => actor.Identity != null && actor.Identity.Role == CharacterRole.Owner ? 1 : 0)
            .FirstOrDefault();
    }

    private static TService ResolveService<TService>()
        where TService : class
    {
        DungeonRuntimeLifetimeScope scope =
            FindFirstObjectByType<DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include);
        return scope?.Container != null
            ? scope.Container.Resolve<TService>()
            : null;
    }

    private static float GetNeed(CharacterActor actor, CharacterCondition condition)
    {
        return actor != null
            && actor.Stats != null
            && actor.Stats.Stats.TryGetValue(condition, out float value)
                ? value
                : 0f;
    }

    private static string GetPersistentId(CharacterActor actor)
    {
        return !string.IsNullOrWhiteSpace(actor?.Identity?.PersistentId)
            ? actor.Identity.PersistentId
            : actor != null ? $"character:{actor.GetInstanceID()}" : string.Empty;
    }

    private static string GetActorLabel(CharacterActor actor)
    {
        return actor?.Identity != null ? actor.Identity.DisplayName : actor != null ? actor.name : "<none>";
    }

    private bool Check(bool condition, string key, string detail)
    {
        report.Add($"[{(condition ? "PASS" : "FAIL")}] {key} {detail}");
        if (!condition)
        {
            failures.Add($"{key}: {detail}");
        }
        return condition;
    }

    private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning)
        {
            capturedWarnings.Add(condition);
        }
        else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            capturedErrors.Add(string.IsNullOrWhiteSpace(stackTrace) ? condition : condition + "\n" + stackTrace);
        }
    }

    private void Finish()
    {
        Application.logMessageReceived -= OnLogMessageReceived;
        Time.timeScale = originalTimeScale;
        report.Add($"capturedErrors={capturedErrors.Count}; {Compact(capturedErrors)}");
        report.Add($"capturedWarnings={capturedWarnings.Count}; {Compact(capturedWarnings)}");
        bool passed = failures.Count == 0 && capturedErrors.Count == 0 && capturedWarnings.Count == 0;
        report.Add($"RESULT={(passed ? "PASS" : "FAIL")}; failures={failures.Count}; {Compact(failures)}");
        File.WriteAllText(DarkSurvivalPlayModeVerifier.ReportPath, string.Join("\n", report));
        File.Delete(DarkSurvivalPlayModeVerifier.RequestPath);
        if (passed)
        {
            Debug.Log("Dark survival PlayMode verification passed. " + DarkSurvivalPlayModeVerifier.ReportPath);
        }
        else
        {
            Debug.LogError("Dark survival PlayMode verification failed. " + DarkSurvivalPlayModeVerifier.ReportPath);
        }

        EditorApplication.ExitPlaymode();
        Destroy(gameObject);
    }

    private static string Compact(IEnumerable<string> values)
    {
        string value = string.Join(" | ", values ?? Array.Empty<string>());
        return string.IsNullOrWhiteSpace(value) ? "<none>" : value.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }
}
#endif
