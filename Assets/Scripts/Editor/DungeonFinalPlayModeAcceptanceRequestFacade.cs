#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class DungeonFinalPlayModeAcceptanceRequestFacade
{
    public const string RequestPath =
        "Temp/final-playmode-acceptance.request";
    public const string StatePath =
        "Library/final-playmode-acceptance.state";
    public const string ProgressPath =
        "Artifacts/QA/final-playmode-acceptance-progress.txt";
    public const string ReportPath =
        "Artifacts/QA/final-playmode-acceptance-report.txt";
    private const string PendingFinishPath =
        "Library/final-playmode-acceptance.finish-pending";
    private const string PersistenceRestoreStatusPath =
        "Library/final-playmode-acceptance.persistence-restore";
    private const string ResolutionRequestPath =
        "Temp/final-resolution-matrix.request";
    private const string PersistenceSnapshotId =
        "final-playmode-acceptance";

    private const string TitleScenePath =
        "Assets/Scenes/TitleScene.unity";
    private const string GameplayScenePath =
        "Assets/Scenes/GameplayScene.unity";
    // GameplayScene integration takes roughly seven minutes in this project
    // under the editor, and editor callbacks cannot observe progress while
    // Unity owns the main thread for scene activation.
    private const double TargetTimeoutSeconds = 900d;

    private static readonly AcceptanceTarget[] Targets =
    {
        new(
            "ResolutionMatrix",
            ResolutionRequestPath,
            DungeonResolutionPlayModeVerifier.ReportPath,
            TitleScenePath,
            RequestResolutionMatrix,
            DestroyRunners<DungeonResolutionVerificationRunner>,
            GetResolutionMatrixCapturePaths()),
        new(
            "FullWorldRoundTrip",
            DungeonFullWorldRoundTripPlayModeFacade.RequestPath,
            DungeonFullWorldRoundTripPlayModeFacade.ReportPath,
            GameplayScenePath,
            DungeonFullWorldRoundTripPlayModeFacade.RequestRunFromMenu,
            DestroyRunners<DungeonFullWorldRoundTripPlayModeRunner>,
            Array.Empty<string>(),
            DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts),
        new(
            "ResearchTree",
            ResearchTreePlayModeVerifier.RequestPath,
            ResearchTreePlayModeVerifier.ReportPath,
            GameplayScenePath,
            ResearchTreePlayModeVerifier.RequestRunFromMenu,
            DestroyRunners<ResearchTreeVerificationRunner>,
            new[]
            {
                ResearchTreePlayModeVerifier.DesktopCapturePath,
                ResearchTreePlayModeVerifier.PortraitDetailCapturePath,
                ResearchTreePlayModeVerifier.PortraitQueueCapturePath
            }),
        new(
            "Production",
            ProductionBuildingPlayModeVerifier.RequestPath,
            ProductionBuildingPlayModeVerifier.ReportPath,
            GameplayScenePath,
            ProductionBuildingPlayModeVerifier.RequestRunFromMenu,
            DestroyRunners<ProductionBuildingPlayModeVerificationRunner>,
            new[]
            {
                ProductionBuildingPlayModeVerifier.DesktopCapturePath,
                ProductionBuildingPlayModeVerifier.PortraitCapturePath
            }),
        new(
            "ServiceRoom",
            ServiceRoomVisualValidationFacade.RequestPath,
            ServiceRoomVisualValidationFacade.ReportPath,
            GameplayScenePath,
            ServiceRoomVisualValidationFacade.RequestRunFromMenu,
            DestroyRunners<ServiceRoomVisualCaptureRunner>,
            new[]
            {
                ServiceRoomVisualValidationFacade.DesktopCapturePath,
                ServiceRoomVisualValidationFacade.PortraitCapturePath
            }),
        new(
            "CharacterSummaryMedical",
            CharacterSummaryMedicalUiMatrixPlayModeVerifier.RequestPath,
            CharacterSummaryMedicalUiMatrixPlayModeVerifier.ReportPath,
            GameplayScenePath,
            CharacterSummaryMedicalUiMatrixPlayModeVerifier.RequestRunFromMenu,
            DestroyRunners<CharacterSummaryMedicalUiMatrixRunner>,
            GetCharacterSummaryMedicalCapturePaths()),
        new(
            "EquipmentExpeditionUiMatrix",
            EquipmentExpeditionUiMatrixPlayModeVerifier.RequestPath,
            EquipmentExpeditionUiMatrixPlayModeVerifier.ReportPath,
            GameplayScenePath,
            EquipmentExpeditionUiMatrixPlayModeVerifier.RequestRunFromMenu,
            DestroyRunners<EquipmentExpeditionUiMatrixRunner>,
            EquipmentExpeditionUiMatrixPlayModeVerifier.GetCapturePaths(),
            requiredReportMarkers: new[]
            {
                EquipmentExpeditionUiMatrixPlayModeVerifier.FacilityFlowMarker
            })
    };

    static DungeonFinalPlayModeAcceptanceRequestFacade()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    internal static bool IsPersistenceCoordinatorActive =>
        File.Exists(StatePath) || File.Exists(PendingFinishPath);

    [MenuItem("DungeonStory/QA/Request Final PlayMode Acceptance")]
    public static void RequestRunFromMenu()
    {
        if (File.Exists(StatePath) || File.Exists(PendingFinishPath))
        {
            Debug.LogWarning(GetStatusForMcp());
            return;
        }

        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Library");
        Directory.CreateDirectory("Artifacts/QA");
        CleanupAllKnownMarkers();
        File.Delete(PendingFinishPath);
        File.Delete(PersistenceRestoreStatusPath);
        File.Delete(ReportPath);
        File.Delete(ProgressPath);
        File.WriteAllText(
            RequestPath,
            DateTime.UtcNow.Ticks.ToString());
        Debug.Log("Final PlayMode acceptance request queued.");
    }

    [MenuItem("DungeonStory/QA/Log Final PlayMode Acceptance Status")]
    public static void LogStatusFromMenu()
    {
        string status = GetStatusForMcp();
        if (status.Contains("RESULT=FAIL", StringComparison.Ordinal))
        {
            Debug.LogError(status);
        }
        else
        {
            Debug.Log(status);
        }
    }

    public static string GetStatusForMcp()
    {
        if (File.Exists(PendingFinishPath))
        {
            return "FINAL_PLAYMODE_ACCEPTANCE EXITING_PLAYMODE; "
                + "pending=" + PendingFinishPath;
        }
        if (File.Exists(StatePath))
        {
            AcceptanceState state = ReadState();
            double elapsed = new TimeSpan(
                DateTime.UtcNow.Ticks - state.TargetStartedUtcTicks)
                .TotalSeconds;
            return "FINAL_PLAYMODE_ACCEPTANCE RUNNING "
                + $"target={state.CurrentTarget.Name}; "
                + $"index={state.CurrentIndex + 1}/{Targets.Length}; "
                + $"elapsed={elapsed:0.0}s; runId={state.RunId}";
        }
        if (File.Exists(ReportPath))
        {
            string first = File.ReadLines(ReportPath)
                .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))
                ?? "FINAL_PLAYMODE_ACCEPTANCE UNKNOWN";
            return first + "; report=" + ReportPath;
        }
        if (File.Exists(RequestPath))
        {
            return "FINAL_PLAYMODE_ACCEPTANCE QUEUED; request=" + RequestPath;
        }
        return "FINAL_PLAYMODE_ACCEPTANCE NOT_REQUESTED";
    }

    private static void OnEditorUpdate()
    {
        if (File.Exists(PendingFinishPath))
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.ExitPlaymode();
                }
                return;
            }

            CompletePendingFinish();
            return;
        }

        if (!File.Exists(StatePath))
        {
            try
            {
                TryStartRequestedRun();
            }
            catch (Exception exception)
            {
                FailAndCleanup(null, "Request startup failed: " + exception);
            }
            return;
        }

        AcceptanceState state;
        try
        {
            state = ReadState();
        }
        catch (Exception exception)
        {
            FailAndCleanup(null, "State read failed: " + exception);
            return;
        }

        FinalAcceptanceReportPolicy.CoordinatorAction action =
            FinalAcceptanceReportPolicy.ResolveCoordinatorAction(
                EditorApplication.isPlayingOrWillChangePlaymode,
                File.Exists(state.CurrentTarget.ReportPath),
                DateTime.UtcNow.Ticks,
                state.TargetStartedUtcTicks,
                TargetTimeoutSeconds);
        if (action == FinalAcceptanceReportPolicy.CoordinatorAction.Timeout)
        {
            double elapsed = new TimeSpan(
                DateTime.UtcNow.Ticks - state.TargetStartedUtcTicks)
                .TotalSeconds;
            FailAndCleanup(
                state,
                $"Target {state.CurrentTarget.Name} timed out after "
                + $"{elapsed:0.0}s.");
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (state.CurrentTarget.Name == "ResolutionMatrix"
                && EditorApplication.isPlaying)
            {
                EnsureResolutionRunner();
            }
            return;
        }

        if (action == FinalAcceptanceReportPolicy.CoordinatorAction.EvaluateReport)
        {
            try
            {
                EvaluateCompletedTarget(state, state.CurrentTarget);
            }
            catch (Exception exception)
            {
                FailAndCleanup(
                    state,
                    "Target report evaluation failed: " + exception);
            }
            return;
        }

        if (state.CurrentTarget.Name == "ResolutionMatrix"
            && File.Exists(ResolutionRequestPath))
        {
            EnsureResolutionPlayModeRequested();
        }
    }

    private static void TryStartRequestedRun()
    {
        if (!File.Exists(RequestPath)
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        try
        {
            EnsureSuiteCanRunWithoutPrompt();
        }
        catch (Exception exception)
        {
            CompleteFinish(
                false,
                "Suite scene preflight failed before persistence capture: "
                    + exception,
                false);
            return;
        }

        long requestedTicks = ReadRequestedTicks();
        File.Delete(RequestPath);
        AcceptanceState state = new(
            Guid.NewGuid().ToString("N"),
            requestedTicks,
            0,
            DateTime.UtcNow.Ticks);
        WriteState(state);
        try
        {
            StartCurrentTarget(state);
        }
        catch (Exception exception)
        {
            FailAndCleanup(state, "First target request failed: " + exception);
        }
    }

    private static void EvaluateCompletedTarget(
        AcceptanceState state,
        AcceptanceTarget target)
    {
        long reportTicks = File.GetLastWriteTimeUtc(target.ReportPath).Ticks;
        string report = File.ReadAllText(target.ReportPath);
        target.CleanupRequest();
        bool fresh = reportTicks >= state.TargetStartedUtcTicks;
        bool passed = FinalAcceptanceReportPolicy.IsFreshPass(
            report,
            reportTicks,
            state.TargetStartedUtcTicks);
        bool capturesFresh = FinalAcceptanceReportPolicy.AreFreshArtifacts(
            target.CapturePaths,
            state.TargetStartedUtcTicks,
            out string[] captureFailures);
        string[] missingReportMarkers = target.RequiredReportMarkers
            .Where(marker => !report.Contains(marker, StringComparison.Ordinal))
            .ToArray();
        bool reportMarkersPresent = missingReportMarkers.Length == 0;
        bool persistenceRestored = IsFreshPersistenceRestore(
            state.TargetStartedUtcTicks,
            out string persistenceFailure);
        passed &= capturesFresh && reportMarkersPresent && persistenceRestored;
        AppendProgress(
            $"[{(passed ? "PASS" : "FAIL")}] {target.Name}; "
            + $"fresh={fresh}; capturesFresh={capturesFresh}; "
            + $"reportMarkersPresent={reportMarkersPresent}; "
            + $"persistenceRestored={persistenceRestored}; "
            + $"reportUtcTicks={reportTicks}; "
            + $"startedUtcTicks={state.TargetStartedUtcTicks}; "
            + $"report={target.ReportPath}");
        if (!passed)
        {
            string reason = !fresh
                ? "Verifier report predates the target request."
                : !FinalAcceptanceReportPolicy.IsFreshPass(
                    report,
                    reportTicks,
                    state.TargetStartedUtcTicks)
                    ? "Verifier report did not declare RESULT=PASS."
                    : !reportMarkersPresent
                        ? "Verifier report omitted required contract markers: "
                            + string.Join(" | ", missingReportMarkers)
                    : !persistenceRestored
                        ? persistenceFailure
                    : "Required capture evidence was missing, empty, or stale: "
                        + string.Join(" | ", captureFailures);
            FailAndCleanup(
                state,
                $"Target {target.Name} failed. {reason}\n{Preview(report)}");
            return;
        }

        if (state.CurrentIndex + 1 >= Targets.Length)
        {
            CompleteFinish(
                true,
                "All verifier reports were fresh and passed.");
            return;
        }

        AcceptanceState next = state.WithTarget(
            state.CurrentIndex + 1,
            DateTime.UtcNow.Ticks);
        WriteState(next);
        try
        {
            StartCurrentTarget(next);
        }
        catch (Exception exception)
        {
            FailAndCleanup(next, "Next target request failed: " + exception);
        }
    }

    private static void StartCurrentTarget(AcceptanceState state)
    {
        AcceptanceTarget target = state.CurrentTarget;
        EnsureSceneCanOpenWithoutPrompt(target.ScenePath);
        File.Delete(PersistenceRestoreStatusPath);
        PlayModeVerificationPersistenceSnapshot.CaptureCurrent(
            PersistenceSnapshotId);
        target.CleanupRequest();
        File.Delete(target.ReportPath);
        FinalAcceptanceReportPolicy.DeleteFiles(target.CapturePaths);
        OpenScene(target.ScenePath);
        WriteState(state.WithTarget(
            state.CurrentIndex,
            DateTime.UtcNow.Ticks));
        target.Request();
        Debug.Log(
            $"Final PlayMode acceptance requested {target.Name}: "
            + target.ReportPath);
    }

    private static void RequestResolutionMatrix()
    {
        File.WriteAllText(
            ResolutionRequestPath,
            DateTime.UtcNow.Ticks.ToString());
        EnsureResolutionPlayModeRequested();
    }

    private static void EnsureResolutionPlayModeRequested()
    {
        if (!File.Exists(StatePath)
            || !File.Exists(ResolutionRequestPath)
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EditorApplication.EnterPlaymode();
    }

    private static void EnsureResolutionRunner()
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                DungeonResolutionVerificationRunner>() == null)
        {
            DungeonResolutionPlayModeVerifier.RunFromMenu();
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingPlayMode
            || !File.Exists(StatePath))
        {
            return;
        }

        try
        {
            bool restored = PlayModeVerificationPersistenceSnapshot.Restore(
                PersistenceSnapshotId);
            File.WriteAllLines(
                PersistenceRestoreStatusPath,
                new[]
                {
                    restored ? "RESULT=PASS" : "RESULT=FAIL",
                    "completedUtc=" + DateTime.UtcNow.ToString("O"),
                    restored
                        ? "detail=Target persistence snapshot restored before EditMode."
                        : "detail=Target persistence snapshot was missing at PlayMode exit."
                });
        }
        catch (Exception exception)
        {
            File.WriteAllLines(
                PersistenceRestoreStatusPath,
                new[]
                {
                    "RESULT=FAIL",
                    "completedUtc=" + DateTime.UtcNow.ToString("O"),
                    "detail=" + exception
                });
        }
    }

    private static void OpenScene(string scenePath)
    {
        EnsureSceneCanOpenWithoutPrompt(scenePath);
        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                scenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
    }

    private static void EnsureSuiteCanRunWithoutPrompt()
    {
        foreach (string scenePath in Targets
            .Select(target => target.ScenePath)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            EnsureSceneCanOpenWithoutPrompt(scenePath);
        }
    }

    private static void EnsureSceneCanOpenWithoutPrompt(string scenePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? Application.dataPath;
        string fullPath = Path.Combine(
            projectRoot,
            scenePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "Final PlayMode acceptance scene is missing.",
                fullPath);
        }

        if (string.Equals(
            SceneManager.GetActiveScene().path,
            scenePath,
            StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        List<string> dirtyScenes = new();
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (!scene.IsValid() || !scene.isLoaded || !scene.isDirty)
            {
                continue;
            }

            string path = string.IsNullOrWhiteSpace(scene.path)
                ? "<unsaved>"
                : scene.path;
            dirtyScenes.Add($"{scene.name} ({path})");
        }

        if (dirtyScenes.Count > 0)
        {
            throw new InvalidOperationException(
                $"Final PlayMode acceptance cannot switch to '{scenePath}' "
                + "because OpenSceneMode.Single would unload dirty scenes: "
                + string.Join(", ", dirtyScenes)
                + ". Save or revert those scene changes explicitly, then "
                + "request the run again.");
        }
    }

    private static void FailAndCleanup(
        AcceptanceState? state,
        string detail)
    {
        CleanupAllKnownMarkers();
        if (state.HasValue)
        {
            state.Value.CurrentTarget.StopRunner();
        }
        else
        {
            StopAllRunners();
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.WriteAllText(PendingFinishPath, detail ?? string.Empty);
            if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }
            return;
        }

        CompleteFinish(false, detail);
    }

    private static void CompletePendingFinish()
    {
        string detail;
        try
        {
            detail = File.ReadAllText(PendingFinishPath);
        }
        catch (Exception exception)
        {
            detail = "Pending failure state could not be read: " + exception;
        }
        File.Delete(PendingFinishPath);
        CompleteFinish(false, detail);
    }

    private static void CompleteFinish(
        bool passed,
        string detail,
        bool persistenceSnapshotCaptured = true)
    {
        CleanupAllKnownMarkers();
        long restoreRequiredAfterUtcTicks = 0L;
        if (File.Exists(StatePath))
        {
            try
            {
                restoreRequiredAfterUtcTicks = ReadState().TargetStartedUtcTicks;
            }
            catch
            {
                // A malformed state already fails the run; direct restore remains available.
            }
        }

        bool persistenceRestoredNow = !persistenceSnapshotCaptured;
        if (persistenceSnapshotCaptured)
        {
            persistenceRestoredNow = IsFreshPersistenceRestore(
                restoreRequiredAfterUtcTicks,
                out _);
            if (!persistenceRestoredNow)
            {
                try
                {
                    persistenceRestoredNow =
                        PlayModeVerificationPersistenceSnapshot.Restore(
                            PersistenceSnapshotId);
                }
                catch (Exception exception)
                {
                    detail += "\nFinal persistence restore failed: " + exception;
                }
            }
        }
        if (!persistenceRestoredNow)
        {
            passed = false;
            detail += "\nThe original persistent state could not be proven restored.";
        }

        string[] progress = File.Exists(ProgressPath)
            ? File.ReadAllLines(ProgressPath)
            : Array.Empty<string>();
        List<string> lines = new()
        {
            $"FINAL_PLAYMODE_ACCEPTANCE RESULT={(passed ? "PASS" : "FAIL")}",
            "requiredResolutions=1600x900,900x1600",
            "inputBoundary=Unity EventSystem and automation capability only",
            "capturePolicy=required captures must exist, be non-empty, and be fresh; "
                + "verifier report freshness and pass state required",
            "persistenceRestoreRequired=" + persistenceSnapshotCaptured,
            "persistenceRestoredNow=" + persistenceRestoredNow,
            "detail=" + detail,
            "completedUtc=" + DateTime.UtcNow.ToString("O"),
            "targets:"
        };
        lines.AddRange(progress);
        File.WriteAllText(ReportPath, string.Join("\n", lines));
        File.Delete(StatePath);
        File.Delete(PendingFinishPath);
        File.Delete(PersistenceRestoreStatusPath);

        string summary = lines[0] + "; report=" + ReportPath;
        if (passed)
        {
            Debug.Log(summary);
        }
        else
        {
            Debug.LogError(summary + "\n" + detail);
        }
    }

    private static void CleanupAllKnownMarkers()
    {
        FinalAcceptanceReportPolicy.DeleteFiles(new[] { RequestPath });
        foreach (AcceptanceTarget target in Targets)
        {
            target.CleanupRequest();
        }
    }

    private static void StopAllRunners()
    {
        foreach (AcceptanceTarget target in Targets)
        {
            target.StopRunner();
        }
    }

    private static string[] GetResolutionMatrixCapturePaths()
    {
        int[,] resolutions =
        {
            { 1280, 720 },
            { 1600, 900 },
            { 1920, 1080 },
            { 2560, 1440 },
            { 900, 1600 }
        };
        string[] surfaces = { "title", "settings", "game" };
        List<string> paths = new();
        for (int index = 0; index < resolutions.GetLength(0); index++)
        {
            foreach (string surface in surfaces)
            {
                paths.Add(
                    $"Temp/resolution-{resolutions[index, 0]}x"
                    + $"{resolutions[index, 1]}-{surface}.png");
            }
        }
        return paths.ToArray();
    }

    private static string[] GetCharacterSummaryMedicalCapturePaths()
    {
        Vector2Int[] resolutions =
        {
            new(1600, 900),
            new(900, 1600)
        };
        string[] surfaces = { "summary-health", "surgery-modal" };
        return resolutions
            .SelectMany(resolution => surfaces.Select(surface =>
                CharacterSummaryMedicalUiMatrixPlayModeVerifier
                    .GetCapturePath(resolution, surface)))
            .ToArray();
    }

    private static void DestroyRunners<T>()
        where T : MonoBehaviour
    {
        foreach (T runner in UnityEngine.Object.FindObjectsByType<T>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (runner == null)
            {
                continue;
            }
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(runner.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(runner.gameObject);
            }
        }
    }

    private static void AppendProgress(string line)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(ProgressPath) ?? "Artifacts/QA");
        File.AppendAllText(ProgressPath, line + "\n");
    }

    private static bool IsFreshPersistenceRestore(
        long targetStartedUtcTicks,
        out string failure)
    {
        if (!File.Exists(PersistenceRestoreStatusPath))
        {
            failure = "Persistence restore evidence is missing: "
                + PersistenceRestoreStatusPath;
            return false;
        }

        long reportTicks = File.GetLastWriteTimeUtc(
            PersistenceRestoreStatusPath).Ticks;
        string report = File.ReadAllText(PersistenceRestoreStatusPath);
        bool passed = FinalAcceptanceReportPolicy.IsFreshPass(
            report,
            reportTicks,
            targetStartedUtcTicks);
        failure = passed
            ? string.Empty
            : "Persistence restore evidence was stale or failed: "
                + Preview(report);
        return passed;
    }

    private static long ReadRequestedTicks()
    {
        string value = File.ReadAllText(RequestPath).Trim();
        return long.TryParse(value, out long ticks)
            ? ticks
            : File.GetLastWriteTimeUtc(RequestPath).Ticks;
    }

    private static string Preview(string report)
    {
        return string.Join(
            "\n",
            (report ?? string.Empty)
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Take(40));
    }

    private static AcceptanceState ReadState()
    {
        string[] lines = File.ReadAllLines(StatePath);
        if (lines.Length != 4)
        {
            throw new InvalidOperationException(
                "Final PlayMode acceptance state is incomplete.");
        }
        return new AcceptanceState(
            lines[0],
            long.Parse(lines[1]),
            int.Parse(lines[2]),
            long.Parse(lines[3]));
    }

    private static void WriteState(AcceptanceState state)
    {
        File.WriteAllLines(
            StatePath,
            new[]
            {
                state.RunId,
                state.RequestedUtcTicks.ToString(),
                state.CurrentIndex.ToString(),
                state.TargetStartedUtcTicks.ToString()
            });
    }

    private readonly struct AcceptanceTarget
    {
        public AcceptanceTarget(
            string name,
            string requestPath,
            string reportPath,
            string scenePath,
            Action request,
            Action stopRunner,
            IEnumerable<string> capturePaths,
            Action cleanupRequest = null,
            IEnumerable<string> requiredReportMarkers = null)
        {
            Name = name;
            RequestPath = requestPath;
            ReportPath = reportPath;
            ScenePath = scenePath;
            Request = request ?? throw new ArgumentNullException(nameof(request));
            StopRunnerAction = stopRunner
                ?? throw new ArgumentNullException(nameof(stopRunner));
            CapturePaths = (capturePaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            RequiredReportMarkers = (requiredReportMarkers
                    ?? Array.Empty<string>())
                .Where(marker => !string.IsNullOrWhiteSpace(marker))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            CleanupRequestAction = cleanupRequest
                ?? (() => File.Delete(requestPath));
        }

        public string Name { get; }
        public string RequestPath { get; }
        public string ReportPath { get; }
        public string ScenePath { get; }
        public Action Request { get; }
        public IReadOnlyList<string> CapturePaths { get; }
        public IReadOnlyList<string> RequiredReportMarkers { get; }
        private Action StopRunnerAction { get; }
        private Action CleanupRequestAction { get; }

        public void CleanupRequest() => CleanupRequestAction();

        public void StopRunner() => StopRunnerAction();
    }

    private readonly struct AcceptanceState
    {
        public AcceptanceState(
            string runId,
            long requestedUtcTicks,
            int currentIndex,
            long targetStartedUtcTicks)
        {
            if (currentIndex < 0 || currentIndex >= Targets.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(currentIndex));
            }
            RunId = string.IsNullOrWhiteSpace(runId)
                ? throw new ArgumentException(
                    "A final acceptance run ID is required.",
                    nameof(runId))
                : runId;
            RequestedUtcTicks = requestedUtcTicks;
            CurrentIndex = currentIndex;
            TargetStartedUtcTicks = targetStartedUtcTicks;
        }

        public string RunId { get; }
        public long RequestedUtcTicks { get; }
        public int CurrentIndex { get; }
        public long TargetStartedUtcTicks { get; }
        public AcceptanceTarget CurrentTarget => Targets[CurrentIndex];

        public AcceptanceState WithTarget(int index, long startedUtcTicks) =>
            new(RunId, RequestedUtcTicks, index, startedUtcTicks);
    }
}
#endif
