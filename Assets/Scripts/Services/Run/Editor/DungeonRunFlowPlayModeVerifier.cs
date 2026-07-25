using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using VContainer;

[InitializeOnLoad]
public static class DungeonRunFlowPlayModeVerifier
{
    public const string RequestPath = "Temp/run-flow-verification.request";
    public const string ReportPath = "Temp/run-flow-verification-report.txt";
    public const string ScreenshotPath = "Temp/run-flow-verification.png";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private static bool runnerCreated;

    static DungeonRunFlowPlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/QA/Request Run Flow Verification")]
    public static void RequestRunFromMenu()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
    }

    private static void OnEditorUpdate()
    {
        if (File.Exists(RequestPath) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (!string.Equals(
                    SceneManager.GetActiveScene().path,
                    GameplayScenePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            }

            EditorApplication.EnterPlaymode();
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            runnerCreated = false;
            return;
        }

        if (change == PlayModeStateChange.EnteredPlayMode && !runnerCreated && File.Exists(RequestPath))
        {
            runnerCreated = true;
            GameObject runnerObject = new GameObject("Run Flow Verification Runner");
            UnityEngine.Object.DontDestroyOnLoad(runnerObject);
            runnerObject.AddComponent<DungeonRunFlowVerificationRunner>();
        }
    }
}

public sealed class DungeonRunFlowVerificationRunner : MonoBehaviour
{
    private sealed class FileBackup
    {
        public string Path;
        public byte[] Bytes;
    }

    private readonly List<string> report = new List<string>();
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<FileBackup> backups = new List<FileBackup>();
    private InputSettings.EditorInputBehaviorInPlayMode originalInputBehavior;
    private Mouse originalMouse;
    private Mouse verificationMouse;
    private string profilePath;

    private IEnumerator Start()
    {
        yield return Run();
    }

    private IEnumerator Run()
    {
        Directory.CreateDirectory("Temp");
        Application.logMessageReceived += CaptureLog;
        originalInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        originalMouse = Mouse.current;
        if (originalMouse != null)
        {
            InputSystem.DisableDevice(originalMouse);
        }

        verificationMouse = InputSystem.AddDevice<Mouse>("RunFlowVerificationMouse");
        verificationMouse.MakeCurrent();

        try
        {
            yield return new WaitForSecondsRealtime(2f);
            DungeonRuntimeLifetimeScope scope = FindScope();
            Check(scope != null, "DI_SCOPE", "runtime scope resolved");
            if (scope == null)
            {
                yield break;
            }

            IDungeonGameSaveSlotService slots = scope.Container.Resolve<IDungeonGameSaveSlotService>();
            IMetaProfileStore profileStore = scope.Container.Resolve<IMetaProfileStore>();
            profilePath = profileStore.ProfilePath;
            BackupFile(profilePath);
            foreach (DungeonSaveSlotInfo slot in slots.GetSlots())
            {
                BackupFile(slot.Path);
            }

            yield return StartFreshRun();

            scope = FindScope();
            IDungeonRunFlowRuntime flow = scope?.Container.Resolve<IDungeonRunFlowRuntime>();
            IMetaProgressionRuntimeProvider metaProvider = scope?.Container.Resolve<IMetaProgressionRuntimeProvider>();
            DungeonStory.Foundation.IGameEventBus gameEvents =
                scope?.Container.Resolve<DungeonStory.Foundation.IGameEventBus>();
            Check(flow != null, "FLOW_RESOLVE", "run flow resolved from the game container");
            if (flow == null || metaProvider == null)
            {
                yield break;
            }

            gameEvents?.Publish(new OperatingDayStartedEvent(4));
            Check(flow.Phase == DungeonRunPhase.Growth, "GROWTH", $"day 4 phase={flow.Phase}");
            gameEvents?.Publish(new OperatingDayStartedEvent(7));
            Check(flow.Phase == DungeonRunPhase.Escalation, "ESCALATION", $"day 7 phase={flow.Phase}");
            gameEvents?.Publish(new OperatingDayStartedEvent(10));
            yield return null;
            yield return null;
            Check(flow.Phase == DungeonRunPhase.EndlessDefense, "ENDLESS_DEFENSE_PHASE", $"day 10 phase={flow.Phase}");
            Check(flow.IsBossActive, "BOSS_SPAWN", "day 10 spawned the armed boss invasion");
            Check(flow.BossCycle == 1, "BOSS_CYCLE_1", $"day 10 cycle={flow.BossCycle}");
            IDungeonGameSaveService gameSave = scope.Container.Resolve<IDungeonGameSaveService>();
            DungeonGameSaveData activeBossSave = gameSave.Capture();
            DungeonGameSaveData activeBossRoundTrip = gameSave.FromJson(gameSave.ToJson(activeBossSave));
            DungeonRunFlowSaveData activeBossFlow =
                DungeonSaveSectionPayload.ReadOrNew<DungeonRunFlowSaveData>(
                    activeBossRoundTrip,
                    RunFlowSaveSection.Id);
            Check(activeBossFlow.phase == DungeonRunPhase.EndlessDefense
                    && activeBossFlow.bossActive
                    && activeBossFlow.bossCycle == 1,
                "BOSS_SAVE", "active recurring boss cycle survives save serialization");

            gameEvents?.Publish(new InvasionResolvedEvent(true, 0f));
            yield return null;
            yield return null;
            OwnerRunManager ownerManager = FindFirstObjectByType<OwnerRunManager>();
            RunResultPanel panel = FindFirstObjectByType<RunResultPanel>(FindObjectsInactive.Include);
            Check(ownerManager != null && !ownerManager.IsRunEnded,
                "DEFENSE_NONTERMINAL", "boss defense keeps the run active");
            Check(flow.Outcome == DungeonRunOutcome.None
                    && flow.Phase == DungeonRunPhase.EndlessDefense
                    && flow.BossCycle == 1
                    && !flow.IsBossActive,
                "ENDLESS_DEFENSE_CONTINUES",
                $"outcome={flow.Outcome}; phase={flow.Phase}; cycle={flow.BossCycle}; active={flow.IsBossActive}");
            Check(panel == null || !panel.gameObject.activeInHierarchy,
                "NO_DEFENSE_RESULT", "boss defense does not open the run result panel");

            DungeonGameSaveData defendedSave = gameSave.FromJson(gameSave.ToJson(gameSave.Capture()));
            DungeonRunFlowSaveData defendedFlow =
                DungeonSaveSectionPayload.ReadOrNew<DungeonRunFlowSaveData>(
                    defendedSave,
                    RunFlowSaveSection.Id);
            Check(defendedFlow.outcome == DungeonRunOutcome.None
                    && defendedFlow.phase == DungeonRunPhase.EndlessDefense
                    && defendedFlow.bossCycle == 1
                    && !defendedFlow.bossActive,
                "DEFENSE_SAVE", "post-defense endless cycle state survives serialization");

            gameEvents?.Publish(new OperatingDayStartedEvent(20));
            yield return null;
            yield return null;
            Check(flow.Outcome == DungeonRunOutcome.None
                    && flow.Phase == DungeonRunPhase.EndlessDefense
                    && flow.BossCycle == 2
                    && flow.IsBossActive,
                "BOSS_CYCLE_2",
                $"day 20 outcome={flow.Outcome}; phase={flow.Phase}; cycle={flow.BossCycle}; active={flow.IsBossActive}");
            gameEvents?.Publish(new InvasionResolvedEvent(true, 0f));
            yield return null;

            OffenseWorldMapRuntime worldMap = FindFirstObjectByType<OffenseWorldMapRuntime>();
            Check(worldMap != null, "OFFENSE_RUNTIME", "offense campaign runtime resolved");
            if (worldMap == null)
            {
                yield break;
            }

            while (worldMap.State.ReconLevel < OffenseWorldMapService.MaxReconLevel)
            {
                worldMap.TryUpgradeRecon(out _);
            }

            IReadOnlyList<OffenseTargetDefinition> route = worldMap.TargetDefinitions
                .OrderBy(target => target.campaignOrder)
                .ToList();
            Check(route.Count == 6
                    && route[route.Count - 1].id == OffenseWorldMapService.TruthTargetId
                    && route[route.Count - 1].revealsTruth,
                "OFFENSE_ROUTE", $"targets={route.Count}; terminal={route.LastOrDefault()?.id}");
            for (int i = 0; i < route.Count; i++)
            {
                bool advanced = worldMap.TryRecordSuccessfulExpedition(
                    route[i].id,
                    out OffenseTargetSnapshot completedTarget,
                    out string campaignMessage);
                Check(advanced && completedTarget != null && completedTarget.isCompleted,
                    $"OFFENSE_STAGE_{i + 1}",
                    $"target={route[i].id}; message={campaignMessage}");
                yield return null;

                if (i < route.Count - 1)
                {
                    Check(flow.Outcome == DungeonRunOutcome.None && !ownerManager.IsRunEnded,
                        $"NO_EARLY_VICTORY_{i + 1}",
                        $"completed={worldMap.State.CompletedTargetCount}; outcome={flow.Outcome}");
                }
            }

            yield return null;
            yield return null;
            Check(worldMap.State.TruthRevealed
                    && worldMap.State.RevealedTruthTargetId == OffenseWorldMapService.TruthTargetId,
                "TRUTH_REVEALED", $"target={worldMap.State.RevealedTruthTargetId}");
            Check(ownerManager != null && ownerManager.IsRunEnded,
                "OWNER_RUN_END", "terminal offense completion ends the owner run");
            Check(flow.Outcome == DungeonRunOutcome.Victory && flow.Phase == DungeonRunPhase.Finished,
                "OFFENSE_VICTORY", $"outcome={flow.Outcome}; phase={flow.Phase}");
            Check(metaProvider.TryGetRuntime(out MetaProgressionRuntime meta)
                    && meta.LatestResult != null
                    && meta.LatestResult.outcome == DungeonRunOutcome.Victory
                    && meta.LatestResult.endReason.Contains(OffenseWorldMapService.TruthTitle),
                "RESULT", "meta result records an offense-owned truth victory");

            panel = FindFirstObjectByType<RunResultPanel>(FindObjectsInactive.Include);
            Button nextRunButton = FindButton("NextRunButton");
            Check(panel != null && panel.gameObject.activeInHierarchy, "RESULT_PANEL", "truth victory result panel is visible");
            Check(nextRunButton != null && nextRunButton.interactable, "NEXT_BUTTON", "next-run command is available");
            Check(Mathf.Approximately(Time.timeScale, 0f), "RESULT_PAUSE", "result panel pauses simulation");
            Check(File.Exists(profilePath), "PROFILE_SAVE", "truth victory writes the durable meta profile");
            Check(slots.HasSave(DungeonGameSaveSlotService.AutoSaveSlot), "FINAL_AUTOSAVE", "truth victory writes an ended-run autosave");

            yield return CaptureScreen();
            yield return Click(nextRunButton);
            DungeonRuntimeLifetimeScope nextScope = null;
            float transitionDeadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < transitionDeadline)
            {
                nextScope = FindScope();
                if (nextScope != null && nextScope != scope)
                {
                    break;
                }

                yield return null;
            }

            OwnerRunManager nextOwner = FindFirstObjectByType<OwnerRunManager>();
            OwnerSelectionPanel selection = FindFirstObjectByType<OwnerSelectionPanel>(FindObjectsInactive.Include);
            Check(nextScope != null && nextScope != scope, "SCENE_RELOAD", "next-run click reloads the gameplay scene");
            Check(nextOwner != null && nextOwner.CurrentOwnerActor == null,
                "FRESH_OWNER", "next run waits for a new owner choice");
            Check(selection != null && selection.gameObject.activeInHierarchy,
                "OWNER_SELECTION", "owner selection is visible after the transition");

            IDungeonGameSaveSlotService nextSlots = nextScope?.Container.Resolve<IDungeonGameSaveSlotService>();
            Check(nextSlots != null
                    && !nextSlots.HasSave(DungeonGameSaveSlotService.AutoSaveSlot)
                    && !nextSlots.HasSave(DungeonGameSaveSlotService.QuickSaveSlot)
                    && !nextSlots.HasSave(DungeonGameSaveSlotService.ManualSaveSlot),
                "RUN_SAVES_CLEARED", "next run clears only run save slots");
            IMetaProgressionRuntimeProvider nextMetaProvider = nextScope?.Container.Resolve<IMetaProgressionRuntimeProvider>();
            Check(nextMetaProvider != null
                    && nextMetaProvider.TryGetRuntime(out MetaProgressionRuntime nextMeta)
                    && nextMeta.State.LifetimeEarnedCurrency > 0,
                "META_CARRY", "durable meta currency is loaded into the next run");

            yield return StartFreshRun();
            IDungeonRunFlowRuntime nextFlow = nextScope?.Container.Resolve<IDungeonRunFlowRuntime>();
            DungeonStory.Foundation.IGameEventBus nextGameEvents =
                nextScope?.Container.Resolve<DungeonStory.Foundation.IGameEventBus>();
            nextGameEvents?.Publish(new OperatingDayStartedEvent(10));
            yield return null;
            yield return null;
            Check(nextFlow != null && nextFlow.IsBossActive,
                "DEFEAT_BOSS_ACTIVE", "second run boss invasion is active before breach verification");
            nextGameEvents?.Publish(new InvasionResolvedEvent(false, 100f));
            yield return null;
            yield return null;

            Check(nextFlow != null
                    && nextFlow.Outcome == DungeonRunOutcome.Defeat
                    && nextFlow.Phase == DungeonRunPhase.Finished,
                "DEFEAT", $"invasion breach outcome={nextFlow?.Outcome}; phase={nextFlow?.Phase}");
            Check(nextMetaProvider != null
                    && nextMetaProvider.TryGetRuntime(out MetaProgressionRuntime defeatedMeta)
                    && defeatedMeta.LatestResult != null
                    && defeatedMeta.LatestResult.outcome == DungeonRunOutcome.Defeat,
                "DEFEAT_RESULT", "invasion breach produces an explicit defeat result");
        }
        finally
        {
            RestoreFiles();
            TeardownInput();
            Application.logMessageReceived -= CaptureLog;
            report.Add($"capturedErrors={errors.Count}; capturedWarnings={warnings.Count}");
            foreach (string error in errors) report.Add("[CONSOLE ERROR] " + error.Replace('\n', ' '));
            foreach (string warning in warnings) report.Add("[CONSOLE WARNING] " + warning.Replace('\n', ' '));
            bool passed = report.All(line => !line.StartsWith("[FAIL]", StringComparison.Ordinal))
                && errors.Count == 0
                && warnings.Count == 0;
            report.Insert(0, passed ? "RUN_FLOW PASS" : "RUN_FLOW FAIL");
            File.WriteAllLines(DungeonRunFlowPlayModeVerifier.ReportPath, report);
            File.Delete(DungeonRunFlowPlayModeVerifier.RequestPath);
            EditorApplication.ExitPlaymode();
        }
    }

    private IEnumerator StartFreshRun()
    {
        GameObject modal = FindObject("SaveModal");
        Button startNew = FindButton("StartNewRunButton");
        if (modal != null && modal.activeInHierarchy && startNew != null && startNew.gameObject.activeInHierarchy)
        {
            yield return Click(startNew);
            if (modal.activeInHierarchy)
            {
                yield return Click(startNew);
            }
        }

        OwnerRunManager ownerManager = null;
        float readyDeadline = Time.realtimeSinceStartup + 5f;
        while (Time.realtimeSinceStartup < readyDeadline)
        {
            ownerManager = FindFirstObjectByType<OwnerRunManager>();
            if (ownerManager != null)
            {
                break;
            }

            yield return null;
        }

        int ownerClickAttempts = 0;
        while (ownerManager != null
            && ownerManager.CurrentOwnerActor == null
            && ownerClickAttempts < 3)
        {
            Button ownerButton = Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(candidate => candidate != null
                    && candidate.gameObject.scene.IsValid()
                    && candidate.gameObject.scene == ownerManager.gameObject.scene
                    && candidate.gameObject.activeInHierarchy
                    && candidate.name.StartsWith("OwnerOption_", StringComparison.Ordinal));
            yield return Click(ownerButton);
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible();
            ownerClickAttempts++;
            yield return new WaitForSecondsRealtime(0.15f);
        }

        Check(ownerManager != null && ownerManager.CurrentOwnerActor != null,
            "OWNER_SELECTED", $"owner selected through pointer input; attempts={ownerClickAttempts}");
    }

    private IEnumerator Click(Button button)
    {
        Check(button != null && button.gameObject.activeInHierarchy && button.interactable,
            "CLICK_TARGET", button != null ? button.name : "<missing>");
        if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
        {
            yield break;
        }

        RectTransform rect = button.GetComponent<RectTransform>();
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        verificationMouse.MakeCurrent();
        InputSystem.QueueStateEvent(verificationMouse, new MouseState { position = point }.WithButton(MouseButton.Left, true));
        InputSystem.Update();
        yield return null;
        yield return null;
        verificationMouse.MakeCurrent();
        InputSystem.QueueStateEvent(verificationMouse, new MouseState { position = point });
        InputSystem.Update();
        yield return null;
        yield return null;
    }

    private IEnumerator CaptureScreen()
    {
        yield return new WaitForEndOfFrame();
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        Color32[] pixels = capture != null ? capture.GetPixels32() : Array.Empty<Color32>();
        Check(pixels.Any(pixel => pixel.a > 0 && (pixel.r > 8 || pixel.g > 8 || pixel.b > 8)),
            "CAPTURE", $"nonblank pixels={pixels.Length}");
        if (capture != null)
        {
            File.WriteAllBytes(DungeonRunFlowPlayModeVerifier.ScreenshotPath, capture.EncodeToPNG());
            Destroy(capture);
        }
    }

    private void BackupFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || backups.Any(backup => backup.Path == path))
        {
            return;
        }

        backups.Add(new FileBackup
        {
            Path = path,
            Bytes = File.Exists(path) ? File.ReadAllBytes(path) : null
        });
    }

    private void RestoreFiles()
    {
        foreach (FileBackup backup in backups)
        {
            if (backup.Bytes == null)
            {
                if (File.Exists(backup.Path)) File.Delete(backup.Path);
                continue;
            }

            string directory = Path.GetDirectoryName(backup.Path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllBytes(backup.Path, backup.Bytes);
        }
    }

    private void TeardownInput()
    {
        if (verificationMouse != null && verificationMouse.added) InputSystem.RemoveDevice(verificationMouse);
        if (originalMouse != null && originalMouse.added)
        {
            InputSystem.EnableDevice(originalMouse);
            originalMouse.MakeCurrent();
        }

        InputSystem.settings.editorInputBehaviorInPlayMode = originalInputBehavior;
    }

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors.Add(condition + "\n" + stackTrace);
        }
        else if (type == LogType.Warning)
        {
            warnings.Add(condition);
        }
    }

    private void Check(bool condition, string id, string detail)
    {
        report.Add($"[{(condition ? "PASS" : "FAIL")}] {id} {detail}");
    }

    private static DungeonRuntimeLifetimeScope FindScope()
    {
        return Resources.FindObjectsOfTypeAll<DungeonRuntimeLifetimeScope>()
            .FirstOrDefault(candidate => candidate != null
                && candidate.gameObject.scene.IsValid()
                && candidate.Container != null);
    }

    private static Button FindButton(string name)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(candidate => candidate != null
                && candidate.gameObject.scene.IsValid()
                && candidate.gameObject.name == name);
    }

    private static GameObject FindObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<Transform>()
            .Where(candidate => candidate != null && candidate.gameObject.scene.IsValid())
            .Select(candidate => candidate.gameObject)
            .FirstOrDefault(candidate => candidate.name == name);
    }
}
