#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
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
    public const string PreflightReportPath =
        "Artifacts/QA/final-playmode-acceptance-preflight-report.txt";
    private const string PendingFinishPath =
        "Library/final-playmode-acceptance.finish-pending";
    private const string PersistenceRestoreStatusPath =
        "Library/final-playmode-acceptance.persistence-restore";
    private const string ResolutionRequestPath =
        "Temp/final-resolution-matrix.request";
    private const string PersistenceSnapshotId =
        "final-playmode-acceptance";

    private const string ConsoleBufferPath =
        "Library/final-playmode-console.log";
    private const string ConsoleActiveMarker =
        "Library/final-playmode-console-active";
    private const string ConsoleWriteFailurePath =
        "Library/final-playmode-console-write-failure";
    private const string ConsoleWriteFailureSessionKey =
        "DungeonStory.FinalPlayMode.ConsoleWriteFailure";
    private const int ConsolePreviewLineLimit = 12;
    private const int ConsolePreviewLineCharacterLimit = 320;
    private const int ReportDetailCharacterLimit = 4096;
    private const int ConsoleCallbackDrainTimeoutMilliseconds = 5000;
    private static readonly object ConsoleIoSync = new();
    private static int consoleCaptureEnabled;
    private static int activeConsoleCallbacks;
    private static string consoleWriteFailureInMemory = string.Empty;

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
            GetResolutionMatrixCaptureContracts()),
        new(
            "FullWorldRoundTrip",
            DungeonFullWorldRoundTripPlayModeFacade.RequestPath,
            DungeonFullWorldRoundTripPlayModeFacade.ReportPath,
            GameplayScenePath,
            DungeonFullWorldRoundTripPlayModeFacade.RequestRunFromMenu,
            DestroyRunners<DungeonFullWorldRoundTripPlayModeRunner>,
            Array.Empty<CaptureArtifactContract>(),
            DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts,
            new[]
            {
                "registeredSections=54",
                "capturedSections=54",
                "postRoundTripSections=54",
                "baselineRestored=True",
                "canonicalBaselineMatched=True"
            }),
        new(
            "ResearchTree",
            ResearchTreePlayModeVerifier.RequestPath,
            ResearchTreePlayModeVerifier.ReportPath,
            GameplayScenePath,
            ResearchTreePlayModeVerifier.RequestRunFromMenu,
            DestroyRunners<ResearchTreeVerificationRunner>,
            new[]
            {
                Capture(
                    ResearchTreePlayModeVerifier.DesktopCapturePath,
                    1600,
                    900),
                Capture(
                    ResearchTreePlayModeVerifier.PortraitDetailCapturePath,
                    900,
                    1600),
                Capture(
                    ResearchTreePlayModeVerifier.PortraitQueueCapturePath,
                    900,
                    1600)
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
                Capture(
                    ProductionBuildingPlayModeVerifier.DesktopCapturePath,
                    1600,
                    900),
                Capture(
                    ProductionBuildingPlayModeVerifier.PortraitCapturePath,
                    900,
                    1600)
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
                Capture(
                    ServiceRoomVisualValidationFacade.DesktopCapturePath,
                    1600,
                    900),
                Capture(
                    ServiceRoomVisualValidationFacade.PortraitCapturePath,
                    900,
                    1600)
            }),
        new(
            "CharacterSummaryMedical",
            CharacterSummaryMedicalUiMatrixPlayModeVerifier.RequestPath,
            CharacterSummaryMedicalUiMatrixPlayModeVerifier.ReportPath,
            GameplayScenePath,
            CharacterSummaryMedicalUiMatrixPlayModeVerifier.RequestRunFromMenu,
            DestroyRunners<CharacterSummaryMedicalUiMatrixRunner>,
            GetCharacterSummaryMedicalCaptureContracts()),
        new(
            "EquipmentExpeditionUiMatrix",
            EquipmentExpeditionUiMatrixPlayModeVerifier.RequestPath,
            EquipmentExpeditionUiMatrixPlayModeVerifier.ReportPath,
            GameplayScenePath,
            EquipmentExpeditionUiMatrixPlayModeVerifier.RequestRunFromMenu,
            DestroyRunners<EquipmentExpeditionUiMatrixRunner>,
            GetEquipmentExpeditionCaptureContracts(),
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
        // The threaded event receives both main-thread and worker-thread logs.
        // Remove the legacy main-thread subscription as well so a domain reload
        // cannot leave the same entry registered on both channels.
        Application.logMessageReceived -= OnLogMessage;
        Application.logMessageReceivedThreaded -= OnLogMessage;
        Application.logMessageReceivedThreaded += OnLogMessage;
        Volatile.Write(
            ref consoleCaptureEnabled,
            File.Exists(ConsoleActiveMarker) ? 1 : 0);
    }

    private static void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Warning
            && type != LogType.Error
            && type != LogType.Exception
            && type != LogType.Assert)
        {
            return;
        }

        if (Volatile.Read(ref consoleCaptureEnabled) == 0)
        {
            return;
        }

        Interlocked.Increment(ref activeConsoleCallbacks);
        try
        {
            if (Volatile.Read(ref consoleCaptureEnabled) == 0)
            {
                return;
            }

            lock (ConsoleIoSync)
            {
                if (Volatile.Read(ref consoleCaptureEnabled) == 0
                    || !File.Exists(ConsoleActiveMarker)
                    || File.Exists(ConsoleWriteFailurePath))
                {
                    return;
                }
                if (!File.Exists(ConsoleBufferPath))
                {
                    throw new IOException(
                        "Console buffer disappeared while capture was active.");
                }
                string message = (condition ?? string.Empty)
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n");
                string line = $"[{type}] {message}";
                File.AppendAllText(
                    ConsoleBufferPath,
                    line + Environment.NewLine);
            }
        }
        catch (Exception exception)
        {
            RecordConsoleWriteFailure(exception);
        }
        finally
        {
            Interlocked.Decrement(ref activeConsoleCallbacks);
        }
    }

    private static void RecordConsoleWriteFailure(Exception exception)
    {
        string failure = exception?.ToString() ?? "Unknown console write failure.";
        lock (ConsoleIoSync)
        {
            consoleWriteFailureInMemory = failure;
            try
            {
                File.WriteAllText(
                    ConsoleWriteFailurePath,
                    DateTime.UtcNow.ToString("O") + " " + failure);
            }
            catch
            {
                // The in-memory channel remains visible until the next domain
                // reload; missing durable evidence also fails inspection closed.
            }
        }
    }

    private static bool DisableConsoleCallbacks(out string failure)
    {
        Volatile.Write(ref consoleCaptureEnabled, 0);
        if (SpinWait.SpinUntil(
                () => Volatile.Read(ref activeConsoleCallbacks) == 0,
                ConsoleCallbackDrainTimeoutMilliseconds))
        {
            failure = string.Empty;
            return true;
        }

        failure = "Timed out waiting for threaded Console callbacks to drain.";
        return false;
    }

    private static bool TryBeginConsoleCapture(out string failure)
    {
        failure = string.Empty;
        try
        {
            if (!DisableConsoleCallbacks(out string drainFailure))
            {
                throw new IOException(drainFailure);
            }
            lock (ConsoleIoSync)
            {
                File.Delete(ConsoleActiveMarker);
                File.Delete(ConsoleWriteFailurePath);
                File.Delete(ConsoleBufferPath);
                SessionState.EraseString(ConsoleWriteFailureSessionKey);
                consoleWriteFailureInMemory = string.Empty;
                File.WriteAllText(ConsoleBufferPath, string.Empty);
                File.WriteAllText(
                    ConsoleActiveMarker,
                    DateTime.UtcNow.ToString("O"));
                if (!File.Exists(ConsoleBufferPath)
                    || !File.Exists(ConsoleActiveMarker))
                {
                    throw new IOException(
                        "Console evidence files were not created successfully.");
                }
                Volatile.Write(ref consoleCaptureEnabled, 1);
            }
            return true;
        }
        catch (Exception exception)
        {
            failure = exception.ToString();
            try
            {
                SessionState.SetString(
                    ConsoleWriteFailureSessionKey,
                    failure);
            }
            catch
            {
                // The durable file below is the second failure channel.
            }
            try
            {
                File.WriteAllText(
                    ConsoleWriteFailurePath,
                    DateTime.UtcNow.ToString("O") + " " + failure);
            }
            catch
            {
                // ReadConsoleEvidence also fails closed on missing files.
            }
            return false;
        }
    }

    private static bool TryEndConsoleCapture(out string failure)
    {
        failure = string.Empty;
        try
        {
            if (!DisableConsoleCallbacks(out string drainFailure))
            {
                throw new IOException(drainFailure);
            }
            lock (ConsoleIoSync)
            {
                File.Delete(ConsoleActiveMarker);
                if (File.Exists(ConsoleActiveMarker))
                {
                    throw new IOException(
                        "Console active marker still exists after deletion.");
                }
            }
            return true;
        }
        catch (Exception exception)
        {
            failure = "Console capture deactivation failed: " + exception;
            try
            {
                SessionState.SetString(
                    ConsoleWriteFailureSessionKey,
                    failure);
            }
            catch
            {
                // Any later inspection still observes the active marker.
            }
            return false;
        }
    }

    private static ConsoleEvidence ReadConsoleEvidence(
        bool requireActiveMarker = true)
    {
        int errors = 0;
        int warnings = 0;
        int exceptions = 0;
        int asserts = 0;
        bool healthy = true;
        var failures = new List<string>();
        var offendingLogs = new List<string>();
        try
        {
            lock (ConsoleIoSync)
            {
                string sessionWriteFailure = SessionState.GetString(
                    ConsoleWriteFailureSessionKey,
                    string.Empty);
                string inMemoryFailure = consoleWriteFailureInMemory;
                if (!string.IsNullOrEmpty(sessionWriteFailure))
                {
                    healthy = false;
                    failures.Add(
                        "Console write failed in session state: "
                        + BoundConsolePreviewLine(sessionWriteFailure));
                }
                if (!string.IsNullOrEmpty(inMemoryFailure))
                {
                    healthy = false;
                    failures.Add(
                        "Console write failed in memory: "
                        + BoundConsolePreviewLine(inMemoryFailure));
                }
                bool markerExists = File.Exists(ConsoleActiveMarker);
                if (requireActiveMarker && !markerExists)
                {
                    healthy = false;
                    failures.Add("Console active marker is missing.");
                }
                else if (!requireActiveMarker && markerExists)
                {
                    healthy = false;
                    failures.Add(
                        "Console active marker remains after deactivation.");
                }
                if (File.Exists(ConsoleWriteFailurePath))
                {
                    healthy = false;
                    failures.Add(
                        "Console write failed: "
                        + BoundConsolePreviewLine(
                            File.ReadAllText(ConsoleWriteFailurePath)));
                }
                if (!File.Exists(ConsoleBufferPath))
                {
                    healthy = false;
                    failures.Add("Console buffer is missing.");
                }
                else
                {
                    foreach (string line in File.ReadLines(ConsoleBufferPath))
                    {
                        if (line.StartsWith("[Error]", StringComparison.Ordinal))
                        {
                            errors++;
                        }
                        else if (line.StartsWith(
                                     "[Exception]",
                                     StringComparison.Ordinal))
                        {
                            exceptions++;
                        }
                        else if (line.StartsWith(
                                     "[Warning]",
                                     StringComparison.Ordinal))
                        {
                            warnings++;
                        }
                        else if (line.StartsWith(
                                     "[Assert]",
                                     StringComparison.Ordinal))
                        {
                            asserts++;
                        }
                        else
                        {
                            continue;
                        }

                        if (offendingLogs.Count < ConsolePreviewLineLimit)
                        {
                            offendingLogs.Add(BoundConsolePreviewLine(line));
                        }
                    }
                }
            }
        }
        catch (Exception exception)
        {
            healthy = false;
            failures.Add(
                "Console buffer inspection failed: "
                + BoundConsolePreviewLine(exception.ToString()));
        }

        return new ConsoleEvidence(
            healthy,
            warnings,
            errors,
            exceptions,
            asserts,
            failures,
            offendingLogs);
    }

    private static string BoundConsolePreviewLine(string value)
    {
        string singleLine = (value ?? string.Empty)
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
        return singleLine.Length <= ConsolePreviewLineCharacterLimit
            ? singleLine
            : singleLine.Substring(0, ConsolePreviewLineCharacterLimit)
                + "...[truncated]";
    }

    private static string BoundReportDetail(string value)
    {
        string singleLine = (value ?? string.Empty)
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
        return singleLine.Length <= ReportDetailCharacterLimit
            ? singleLine
            : singleLine.Substring(0, ReportDetailCharacterLimit)
                + "...[truncated]";
    }

    private static bool TryWritePreflightReport(
        bool passed,
        string gateDetail,
        string targetDetail,
        ConsoleEvidence evidence,
        out string failure)
    {
        failure = string.Empty;
        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(PreflightReportPath)
                    ?? "Artifacts/QA");
            var lines = new List<string>
            {
                $"FINAL_PLAYMODE_PREFLIGHT RESULT={(passed ? "PASS" : "FAIL")}",
                "targetCount=" + Targets.Length,
                "captureCount=" + Targets.Sum(target => target.CapturePaths.Count),
                "consoleCaptureHealthy=" + evidence.Healthy,
                "consoleWarnings=" + evidence.Warnings,
                "consoleErrors=" + evidence.Errors,
                "consoleExceptions=" + evidence.Exceptions,
                "consoleAsserts=" + evidence.Asserts,
                "gateDetail=" + BoundReportDetail(gateDetail),
                "targetDetail=" + BoundReportDetail(targetDetail),
                "completedUtc=" + DateTime.UtcNow.ToString("O"),
                "offendingLogPreview:"
            };
            lines.AddRange(evidence.OffendingLogPreview);
            if (evidence.FailureReasons.Count > 0)
            {
                lines.Add("consoleEvidenceFailures:");
                lines.AddRange(evidence.FailureReasons);
            }
            File.WriteAllText(
                PreflightReportPath,
                string.Join("\n", lines));
            return true;
        }
        catch (Exception exception)
        {
            failure = BoundReportDetail(exception.ToString());
            return false;
        }
    }

    internal static bool IsPersistenceCoordinatorActive =>
        File.Exists(StatePath) || File.Exists(PendingFinishPath);

    private static bool requestQueuedForMcp;

    public static bool QueueRequestForMcp()
    {
        if (requestQueuedForMcp
            || File.Exists(StatePath)
            || File.Exists(PendingFinishPath))
        {
            return false;
        }

        requestQueuedForMcp = true;
        return true;
    }

    public static bool TryClearActiveSceneDirtinessForMcp(
        out string failure)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            failure = "Scene dirtiness cannot be cleared during a PlayMode transition.";
            return false;
        }

        return TryClearProvenSpuriousSceneDirtiness(
            SceneManager.GetActiveScene(),
            out failure);
    }

    private static void RunQueuedRequestForMcp()
    {
        requestQueuedForMcp = false;
        RequestRunFromMenu();
    }

    [MenuItem("DungeonStory/QA/Request Final PlayMode Acceptance")]
    public static void RequestRunFromMenu()
    {
        if (File.Exists(StatePath) || File.Exists(PendingFinishPath))
        {
            Debug.LogWarning(GetStatusForMcp());
            return;
        }

        try
        {
            Directory.CreateDirectory("Temp");
            Directory.CreateDirectory("Library");
            Directory.CreateDirectory("Artifacts/QA");
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Final PlayMode request preparation failed closed: "
                + exception);
            return;
        }

        // Capture starts before request cleanup or any synchronous gate so
        // warnings and errors share one fail-closed evidence stream.
        if (!TryBeginConsoleCapture(out string initializationFailure))
        {
            string detail = "Console capture initialization failed: "
                + initializationFailure;
            if (!TryWritePreflightReport(
                false,
                detail,
                string.Empty,
                ReadConsoleEvidence(),
                out string reportFailure))
            {
                detail += " | Preflight report write failed: "
                    + reportFailure;
            }
            CompleteFinish(false, detail, false);
            return;
        }

        try
        {
            CleanupAllKnownMarkers();
            File.Delete(PendingFinishPath);
            File.Delete(PersistenceRestoreStatusPath);
            File.Delete(ReportPath);
            File.Delete(PreflightReportPath);
            File.Delete(ProgressPath);
        }
        catch (Exception exception)
        {
            string detail = "Final PlayMode request cleanup failed: "
                + exception;
            if (!TryWritePreflightReport(
                false,
                detail,
                string.Empty,
                ReadConsoleEvidence(),
                out string reportFailure))
            {
                detail += " | Preflight report write failed: "
                    + reportFailure;
            }
            CompleteFinish(false, detail, false);
            return;
        }

        try
        {
            if (!RunSynchronousPreflightForMcp(out string detail))
            {
                CompleteFinish(
                    false,
                    "Final PlayMode preflight failed: " + detail,
                    false);
                return;
            }
        }
        catch (Exception e)
        {
            string detail = "Final PlayMode preflight threw: " + e;
            if (!TryWritePreflightReport(
                false,
                detail,
                string.Empty,
                ReadConsoleEvidence(),
                out string reportFailure))
            {
                detail += " | Preflight report write failed: "
                    + reportFailure;
            }
            CompleteFinish(false, detail, false);
            return;
        }

        try
        {
            File.WriteAllText(
                RequestPath,
                DateTime.UtcNow.Ticks.ToString());
        }
        catch (Exception e)
        {
            CompleteFinish(
                false,
                "Final PlayMode request could not be queued: " + e,
                false);
            return;
        }
        Debug.Log("Final PlayMode acceptance request queued.");
    }

    public static bool RunSynchronousPreflightForMcp(out string detail)
    {
        try
        {
            Directory.CreateDirectory("Library");
            Directory.CreateDirectory("Artifacts/QA");
        }
        catch (Exception exception)
        {
            detail = "Preflight evidence directory initialization failed: "
                + exception;
            return false;
        }
        bool ownsConsoleCapture = !File.Exists(ConsoleActiveMarker);
        if (ownsConsoleCapture
            && !TryBeginConsoleCapture(out string initializationFailure))
        {
            bool deactivated = TryEndConsoleCapture(
                out string deactivationFailure);
            ConsoleEvidence failedEvidence = ReadConsoleEvidence(
                requireActiveMarker: false);
            if (!deactivated)
            {
                failedEvidence = failedEvidence.WithFailure(
                    deactivationFailure);
            }
            detail = "Console capture initialization failed: "
                + initializationFailure;
            if (!TryWritePreflightReport(
                false,
                detail,
                string.Empty,
                failedEvidence,
                out string reportFailure))
            {
                detail += " | Preflight report write failed: "
                    + reportFailure;
            }
            return false;
        }

        string gateDetail = string.Empty;
        string targetDetail = string.Empty;
        try
        {
            string targetContractDetail = ValidateTargetsAndCaptures();
            string sceneSafetyDetail = ValidateSuiteSceneSafety();
            if (string.IsNullOrEmpty(targetContractDetail)
                && string.IsNullOrEmpty(sceneSafetyDetail))
            {
                gateDetail = RunPreflightChecksInIsolatedScene();
            }

            string postGateSceneSafetyDetail = string.Empty;
            if (string.IsNullOrEmpty(sceneSafetyDetail))
            {
                postGateSceneSafetyDetail = ValidateSuiteSceneSafety();
            }
            targetDetail = string.Join(
                " | ",
                new[]
                {
                    targetContractDetail,
                    sceneSafetyDetail,
                    postGateSceneSafetyDetail
                }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            ConsoleEvidence evidence;
            if (ownsConsoleCapture)
            {
                bool deactivated = TryEndConsoleCapture(
                    out string deactivationFailure);
                evidence = ReadConsoleEvidence(requireActiveMarker: false);
                if (!deactivated)
                {
                    evidence = evidence.WithFailure(deactivationFailure);
                }
            }
            else
            {
                evidence = ReadConsoleEvidence();
            }
            string consoleDetail = evidence.GetFailureDetail();
            detail = string.Join(
                " | ",
                new[] { gateDetail, targetDetail, consoleDetail }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            bool passed = string.IsNullOrEmpty(detail);
            if (!TryWritePreflightReport(
                passed,
                gateDetail,
                targetDetail,
                evidence,
                out string reportFailure))
            {
                detail = string.Join(
                    " | ",
                    new[]
                    {
                        detail,
                        "Preflight report write failed: " + reportFailure
                    }.Where(value => !string.IsNullOrWhiteSpace(value)));
                passed = false;
            }
            return passed;
        }
        catch (Exception exception)
        {
            ConsoleEvidence evidence;
            if (ownsConsoleCapture)
            {
                bool deactivated = TryEndConsoleCapture(
                    out string deactivationFailure);
                evidence = ReadConsoleEvidence(requireActiveMarker: false);
                if (!deactivated)
                {
                    evidence = evidence.WithFailure(deactivationFailure);
                }
            }
            else
            {
                evidence = ReadConsoleEvidence();
            }
            detail = "Synchronous preflight threw: " + exception;
            if (!TryWritePreflightReport(
                false,
                detail,
                targetDetail,
                evidence,
                out string reportFailure))
            {
                detail += " | Preflight report write failed: "
                    + reportFailure;
            }
            return false;
        }
    }

    private static string ValidateSuiteSceneSafety()
    {
        try
        {
            EnsureSuiteCanRunWithoutPrompt();
            return string.Empty;
        }
        catch (Exception exception)
        {
            return "Suite scene preflight failed: " + exception.Message;
        }
    }

    private static string RunPreflightChecksInIsolatedScene(
        bool includeFinalAcceptance = true)
    {
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        if (originalSetup == null
            || originalSetup.Length == 0
            || originalSetup.Any(entry => string.IsNullOrWhiteSpace(entry.path)))
        {
            return "Synchronous preflight requires a restorable saved scene setup.";
        }

        Scene scratchScene = default;
        string scratchScenePath = string.Empty;
        string gateDetail = string.Empty;
        string restorationDetail = string.Empty;
        try
        {
            // The synchronous regressions create and destroy Unity objects. Run
            // them in an owned scratch scene so their cleanup cannot leave a
            // clean user scene dirty and block the subsequent scene matrix.
            scratchScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            scratchScenePath = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/DungeonStoryFinalPreflightScratch.unity");
            if (!EditorSceneManager.SaveScene(
                    scratchScene,
                    scratchScenePath,
                    false))
            {
                throw new InvalidOperationException(
                    "The owned synchronous-preflight scene could not be saved.");
            }
            gateDetail = RunPreflightChecks(includeFinalAcceptance);
        }
        catch (Exception exception)
        {
            gateDetail = "Isolated synchronous preflight threw: "
                + exception.GetBaseException().Message;
        }
        finally
        {
            try
            {
                if (scratchScene.IsValid()
                    && scratchScene.isLoaded
                    && !string.IsNullOrEmpty(scratchScenePath))
                {
                    // Regressions may dirty the owned scene. Saving it prevents
                    // a prompt while restoring the user's already-clean setup.
                    EditorSceneManager.SaveScene(
                        scratchScene,
                        scratchScenePath,
                        false);
                }
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                if (!string.IsNullOrEmpty(scratchScenePath)
                    && AssetDatabase.LoadAssetAtPath<SceneAsset>(
                        scratchScenePath) != null
                    && !AssetDatabase.DeleteAsset(scratchScenePath))
                {
                    throw new IOException(
                        "The owned synchronous-preflight scene asset could not be removed: "
                        + scratchScenePath);
                }
            }
            catch (Exception exception)
            {
                restorationDetail =
                    "Synchronous preflight scene restoration failed: "
                    + exception.GetBaseException().Message;
            }
        }

        return string.Join(
            " | ",
            new[] { gateDetail, restorationDetail }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    public static bool RunEditModeGatesForMcp(out string detail)
    {
        detail = RunPreflightChecks(includeFinalAcceptance: false);
        return string.IsNullOrEmpty(detail);
    }

    private static bool editModeGatesQueuedForMcp;

    public static bool QueueEditModeGatesForMcp()
    {
        if (editModeGatesQueuedForMcp)
        {
            return false;
        }

        editModeGatesQueuedForMcp = true;
        return true;
    }

    private static void RunQueuedEditModeGatesForMcp()
    {
        try
        {
            _ = RunEditModeGatesForMcp(out _);
        }
        finally
        {
            editModeGatesQueuedForMcp = false;
        }
    }

    public static bool RunArchitectureTestForMcp(
        string methodName,
        out string detail)
    {
        const string typeName =
            "DungeonStory.Tests.Architecture.GameplayArchitectureRatchetTests";
        Type testType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName, false, false))
            .FirstOrDefault(type => type != null);
        MethodInfo method = testType?.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
        {
            detail = $"Architecture test was not found: {typeName}.{methodName}";
            return false;
        }

        try
        {
            method.Invoke(Activator.CreateInstance(testType), null);
            detail = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            Exception cause = exception.GetBaseException();
            detail = $"{cause.GetType().Name}: {cause.Message}";
            return false;
        }
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
        if (requestQueuedForMcp
            && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            RunQueuedRequestForMcp();
            return;
        }

        if (editModeGatesQueuedForMcp
            && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            RunQueuedEditModeGatesForMcp();
            return;
        }

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
        bool capturesFresh = AreFreshPngArtifacts(
            target.CaptureArtifacts,
            state.TargetStartedUtcTicks,
            out string[] captureFailures);
        HashSet<string> reportLines = report
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        string[] missingReportMarkers = target.RequiredReportMarkers
            .Where(marker => !reportLines.Contains(marker))
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

    private static string RunPreflightChecks(bool includeFinalAcceptance = true)
    {
        var failures = new List<string>();
        var checks = new List<(string typeName, string methodName, bool hasOut)>
        {
            ("DungeonStory.Tests.Architecture.ArchitectureTestBatchRunner", "RunForFinalGate", true),
            ("DungeonStory.Tests.Architecture.TransactionalRestoreTestRunner", "RunForFinalGate", true)
        };
        if (includeFinalAcceptance)
        {
            checks.Add(("DungeonStoryFinalAcceptanceRunner", "RunAll", false));
        }

        foreach (var check in checks)
        {
            Type found = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(check.typeName, throwOnError: false, ignoreCase: false); }
                catch { }
                if (t != null)
                {
                    found = t;
                    break;
                }
                // Some generated/test assemblies require enumerating their types.
                try
                {
                    t = asm.GetTypes().FirstOrDefault(x => string.Equals(
                        x.FullName,
                        check.typeName,
                        StringComparison.Ordinal));
                }
                catch { }
                if (t != null) { found = t; break; }
            }

            if (found == null)
            {
                failures.Add($"Missing assembly/type: {check.typeName}");
                continue;
            }

            try
            {
                if (check.hasOut)
                {
                    MethodInfo mi = found.GetMethod(check.methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (mi == null)
                    {
                        failures.Add($"Missing method: {check.methodName} on {check.typeName}");
                        continue;
                    }
                    object[] args = new object[] { null };
                    bool ok = (bool)mi.Invoke(null, args);
                    string detail = args[0] as string ?? string.Empty;
                    if (!ok) failures.Add($"{check.typeName}.{check.methodName} returned false: {detail}");
                }
                else
                {
                    MethodInfo mi = found.GetMethod(check.methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(bool) }, null);
                    if (mi == null)
                    {
                        failures.Add($"Missing method: {check.methodName}(bool) on {check.typeName}");
                        continue;
                    }
                    bool ok = (bool)mi.Invoke(null, new object[] { true });
                    if (!ok) failures.Add($"{check.typeName}.{check.methodName}(true) returned false");
                }
            }
            catch (Exception e)
            {
                failures.Add($"{check.typeName}.{check.methodName} threw: {e.GetBaseException().Message}");
            }
        }

        return failures.Count == 0 ? string.Empty : string.Join(" | ", failures);
    }

    private static string ValidateTargetsAndCaptures()
    {
        var expected = new Dictionary<string, int[]>(StringComparer.Ordinal)
        {
            // total captures, 1600x900 captures, 900x1600 captures
            { "ResolutionMatrix", new[] { 15, 3, 3 } },
            { "FullWorldRoundTrip", new[] { 0, 0, 0 } },
            { "ResearchTree", new[] { 3, 1, 2 } },
            { "Production", new[] { 2, 1, 1 } },
            { "ServiceRoom", new[] { 2, 1, 1 } },
            { "CharacterSummaryMedical", new[] { 4, 2, 2 } },
            { "EquipmentExpeditionUiMatrix", new[] { 4, 2, 2 } }
        };

        string[] names = Targets.Select(target => target.Name).ToArray();
        if (Targets.Length != expected.Count
            || names.Any(string.IsNullOrWhiteSpace))
        {
            return $"Expected exactly {expected.Count} named targets; found {Targets.Length}.";
        }
        string[] duplicateNames = names
            .GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        string[] missingNames = expected.Keys.Except(names, StringComparer.Ordinal).ToArray();
        string[] unexpectedNames = names.Except(expected.Keys, StringComparer.Ordinal).ToArray();
        if (duplicateNames.Length > 0
            || missingNames.Length > 0
            || unexpectedNames.Length > 0)
        {
            return "Target identity contract failed. duplicates="
                + string.Join(",", duplicateNames)
                + "; missing=" + string.Join(",", missingNames)
                + "; unexpected=" + string.Join(",", unexpectedNames);
        }

        int totalCaptures = 0;
        var allPaths = new List<string>();
        foreach (var target in Targets)
        {
            int count = target.CaptureArtifacts?.Count ?? 0;
            totalCaptures += count;
            allPaths.AddRange(target.CapturePaths ?? Array.Empty<string>());
            int[] contract = expected[target.Name];
            if (count != contract[0])
            {
                return $"Target '{target.Name}' expected {contract[0]} captures but has {count}.";
            }
            int desktop = target.CaptureArtifacts.Count(capture =>
                capture.Width == 1600 && capture.Height == 900);
            int portrait = target.CaptureArtifacts.Count(capture =>
                capture.Width == 900 && capture.Height == 1600);
            if (desktop != contract[1] || portrait != contract[2])
            {
                return $"Target '{target.Name}' resolution contract failed: "
                    + $"1600x900={desktop}/{contract[1]}, "
                    + $"900x1600={portrait}/{contract[2]}.";
            }
        }

        if (totalCaptures != 30)
        {
            return $"Expected exactly 30 total captures; found {totalCaptures}.";
        }

        if (allPaths.Any(string.IsNullOrWhiteSpace))
        {
            return "One or more capture paths are empty or whitespace.";
        }

        if (Targets.SelectMany(target => target.CaptureArtifacts)
            .Any(capture => capture.Width <= 0 || capture.Height <= 0))
        {
            return "One or more capture contracts have invalid dimensions.";
        }

        var dup = allPaths
            .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (dup.Length > 0)
        {
            return "Duplicate capture paths found: " + string.Join(",", dup);
        }

        return string.Empty;
    }

    private static bool AreFreshPngArtifacts(
        IReadOnlyList<CaptureArtifactContract> captures,
        long targetStartedUtcTicks,
        out string[] failures)
    {
        CaptureArtifactContract[] contracts = (captures
                ?? Array.Empty<CaptureArtifactContract>())
            .ToArray();
        FinalAcceptanceReportPolicy.AreFreshArtifacts(
            contracts.Select(capture => capture.Path),
            targetStartedUtcTicks,
            out string[] freshnessFailures);
        var allFailures = new List<string>(freshnessFailures);
        foreach (CaptureArtifactContract capture in contracts)
        {
            if (string.IsNullOrWhiteSpace(capture.Path)
                || !File.Exists(capture.Path)
                || new FileInfo(capture.Path).Length <= 0)
            {
                continue;
            }

            if (!TryReadPngDimensions(
                    capture.Path,
                    out int width,
                    out int height,
                    out string failure))
            {
                allFailures.Add(
                    $"invalidPng={capture.Path}; detail={failure}");
                continue;
            }
            if (width != capture.Width || height != capture.Height)
            {
                allFailures.Add(
                    $"wrongDimensions={capture.Path}; actual={width}x{height}; "
                    + $"expected={capture.Width}x{capture.Height}");
            }
        }

        failures = allFailures.ToArray();
        return failures.Length == 0;
    }

    private static bool TryReadPngDimensions(
        string path,
        out int width,
        out int height,
        out string failure)
    {
        width = 0;
        height = 0;
        failure = string.Empty;
        try
        {
            byte[] header = new byte[24];
            using FileStream stream = File.Open(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            int offset = 0;
            while (offset < header.Length)
            {
                int read = stream.Read(
                    header,
                    offset,
                    header.Length - offset);
                if (read <= 0)
                {
                    failure = "PNG header ended before IHDR dimensions.";
                    return false;
                }
                offset += read;
            }

            byte[] signature =
            {
                0x89, 0x50, 0x4E, 0x47,
                0x0D, 0x0A, 0x1A, 0x0A
            };
            if (!signature.SequenceEqual(header.Take(signature.Length))
                || ReadBigEndianUInt32(header, 8) != 13u
                || header[12] != (byte)'I'
                || header[13] != (byte)'H'
                || header[14] != (byte)'D'
                || header[15] != (byte)'R')
            {
                failure = "PNG signature or IHDR chunk is invalid.";
                return false;
            }

            uint rawWidth = ReadBigEndianUInt32(header, 16);
            uint rawHeight = ReadBigEndianUInt32(header, 20);
            if (rawWidth == 0
                || rawHeight == 0
                || rawWidth > int.MaxValue
                || rawHeight > int.MaxValue)
            {
                failure = "PNG IHDR dimensions are outside the supported range.";
                return false;
            }

            width = (int)rawWidth;
            height = (int)rawHeight;
            return true;
        }
        catch (Exception exception)
        {
            failure = BoundReportDetail(exception.Message);
            return false;
        }
    }

    private static uint ReadBigEndianUInt32(byte[] source, int offset)
    {
        return ((uint)source[offset] << 24)
            | ((uint)source[offset + 1] << 16)
            | ((uint)source[offset + 2] << 8)
            | source[offset + 3];
    }

    private static bool TryClearProvenSpuriousSceneDirtiness(
        Scene scene,
        out string failure)
    {
        failure = string.Empty;
        if (!scene.IsValid() || !scene.isLoaded || !scene.isDirty)
        {
            return true;
        }
        if (string.IsNullOrWhiteSpace(scene.path))
        {
            failure = "The dirty scene is unsaved.";
            return false;
        }

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? Application.dataPath;
        string sourcePath = Path.Combine(
            projectRoot,
            scene.path.Replace('/', Path.DirectorySeparatorChar));
        string diagnosticDirectory = Path.Combine(
            projectRoot,
            "Temp",
            "SceneDiagnostics");
        string copyPath = Path.Combine(
            diagnosticDirectory,
            $"{scene.name}.{Guid.NewGuid():N}.unity");
        try
        {
            Directory.CreateDirectory(diagnosticDirectory);
            if (!EditorSceneManager.SaveScene(scene, copyPath, true))
            {
                failure = "Unity could not serialize the dirty scene as a diagnostic copy.";
                return false;
            }
            if (!FilesAreIdentical(sourcePath, copyPath))
            {
                failure = "The dirty scene has serialized changes.";
                return false;
            }

            MethodInfo clearMethod = typeof(EditorSceneManager)
                .GetMethods(BindingFlags.Static
                    | BindingFlags.Public
                    | BindingFlags.NonPublic)
                .SingleOrDefault(method =>
                {
                    if (!string.Equals(
                            method.Name,
                            "ClearSceneDirtiness",
                            StringComparison.Ordinal))
                    {
                        return false;
                    }
                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 1
                        && parameters[0].ParameterType == typeof(Scene);
                });
            if (clearMethod == null)
            {
                failure = "Unity does not expose its internal scene-dirtiness clear operation.";
                return false;
            }

            clearMethod.Invoke(null, new object[] { scene });
            if (scene.isDirty)
            {
                failure = "Unity retained the dirty flag after the byte-identical check.";
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            failure = "Spurious scene-dirtiness verification failed: "
                + exception.GetBaseException().Message;
            return false;
        }
        finally
        {
            try
            {
                File.Delete(copyPath);
            }
            catch
            {
                // A diagnostic-copy cleanup failure cannot alter the scene.
            }
        }
    }

    private static bool FilesAreIdentical(string leftPath, string rightPath)
    {
        FileInfo left = new FileInfo(leftPath);
        FileInfo right = new FileInfo(rightPath);
        if (!left.Exists || !right.Exists || left.Length != right.Length)
        {
            return false;
        }

        const int BufferSize = 64 * 1024;
        byte[] leftBuffer = new byte[BufferSize];
        byte[] rightBuffer = new byte[BufferSize];
        using FileStream leftStream = File.OpenRead(leftPath);
        using FileStream rightStream = File.OpenRead(rightPath);
        while (true)
        {
            int leftRead = leftStream.Read(leftBuffer, 0, leftBuffer.Length);
            int rightRead = rightStream.Read(rightBuffer, 0, rightBuffer.Length);
            if (leftRead != rightRead)
            {
                return false;
            }
            if (leftRead == 0)
            {
                return true;
            }
            for (int index = 0; index < leftRead; index++)
            {
                if (leftBuffer[index] != rightBuffer[index])
                {
                    return false;
                }
            }
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

            if (TryClearProvenSpuriousSceneDirtiness(
                    scene,
                    out _))
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

        bool consoleCaptureEnded = TryEndConsoleCapture(
            out string deactivationFailure);
        ConsoleEvidence consoleEvidence = ReadConsoleEvidence(
            requireActiveMarker: false);
        bool consoleCaptureHealthy = consoleEvidence.Healthy;
        if (!consoleCaptureHealthy || consoleEvidence.TotalOffendingEntries > 0)
        {
            passed = false;
            detail += "\n" + consoleEvidence.GetFailureDetail();
        }
        if (!consoleCaptureEnded)
        {
            consoleCaptureHealthy = false;
            passed = false;
            detail += "\n" + BoundReportDetail(deactivationFailure);
        }
        SessionState.EraseString(ConsoleWriteFailureSessionKey);

        string[] progress = File.Exists(ProgressPath)
            ? File.ReadAllLines(ProgressPath)
            : Array.Empty<string>();
        int completedTargetCount = progress.Count(line =>
            (line ?? string.Empty).TrimStart().StartsWith(
                "[PASS] ",
                StringComparison.Ordinal));
        if (passed
            && !ValidateCompletedTargetProgress(progress, out string progressFailure))
        {
            passed = false;
            detail += "\n" + progressFailure;
        }
        List<string> lines = new()
        {
            $"FINAL_PLAYMODE_ACCEPTANCE RESULT={(passed ? "PASS" : "FAIL")}",
            "targetCount=" + Targets.Length,
            "captureCount=" + Targets.Sum(target => target.CapturePaths.Count),
            "completedTargetCount=" + completedTargetCount,
            "requiredResolutions=1600x900,900x1600",
            "inputBoundary=Unity EventSystem and automation capability only",
            "capturePolicy=required captures must exist, be non-empty, and be fresh; "
                + "verifier report freshness and pass state required",
            "persistenceRestoreRequired=" + persistenceSnapshotCaptured,
            "persistenceRestoredNow=" + persistenceRestoredNow,
            "consoleCaptureHealthy=" + consoleCaptureHealthy,
            "consoleWarnings=" + consoleEvidence.Warnings,
            "consoleErrors=" + consoleEvidence.Errors,
            "consoleExceptions=" + consoleEvidence.Exceptions,
            "consoleAsserts=" + consoleEvidence.Asserts,
            "detail=" + detail,
            "completedUtc=" + DateTime.UtcNow.ToString("O"),
            "offendingLogPreview:"
        };
        lines.AddRange(consoleEvidence.OffendingLogPreview);
        lines.Add(
            "targets:"
        );
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

    private static bool ValidateCompletedTargetProgress(
        IReadOnlyList<string> progress,
        out string failure)
    {
        string[] lines = (progress ?? Array.Empty<string>())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .ToArray();
        if (lines.Length != Targets.Length)
        {
            failure = "Final target progress is incomplete: expected exactly "
                + Targets.Length + "; actual=" + lines.Length + ".";
            return false;
        }

        foreach (AcceptanceTarget target in Targets)
        {
            string prefix = "[PASS] " + target.Name + ";";
            int matches = lines.Count(line => line.StartsWith(
                prefix,
                StringComparison.Ordinal));
            if (matches != 1)
            {
                failure = "Final target progress must contain exactly one PASS "
                    + "entry for " + target.Name + "; actual=" + matches + ".";
                return false;
            }
        }

        failure = string.Empty;
        return true;
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

    private static CaptureArtifactContract[]
        GetResolutionMatrixCaptureContracts()
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
        List<CaptureArtifactContract> captures = new();
        for (int index = 0; index < resolutions.GetLength(0); index++)
        {
            foreach (string surface in surfaces)
            {
                int width = resolutions[index, 0];
                int height = resolutions[index, 1];
                captures.Add(Capture(
                    $"Temp/resolution-{width}x{height}-{surface}.png",
                    width,
                    height));
            }
        }
        return captures.ToArray();
    }

    private static CaptureArtifactContract[]
        GetCharacterSummaryMedicalCaptureContracts()
    {
        Vector2Int[] resolutions =
        {
            new(1600, 900),
            new(900, 1600)
        };
        string[] surfaces = { "summary-health", "surgery-modal" };
        return resolutions
            .SelectMany(resolution => surfaces.Select(surface =>
                Capture(
                    CharacterSummaryMedicalUiMatrixPlayModeVerifier
                        .GetCapturePath(resolution, surface),
                    resolution.x,
                    resolution.y)))
            .ToArray();
    }

    private static CaptureArtifactContract[]
        GetEquipmentExpeditionCaptureContracts()
    {
        Vector2Int[] resolutions =
        {
            new(1600, 900),
            new(900, 1600)
        };
        string[] surfaces = { "equipment", "expedition" };
        return resolutions
            .SelectMany(resolution => surfaces.Select(surface =>
                Capture(
                    EquipmentExpeditionUiMatrixPlayModeVerifier
                        .GetCapturePath(resolution, surface),
                    resolution.x,
                    resolution.y)))
            .ToArray();
    }

    private static CaptureArtifactContract Capture(
        string path,
        int width,
        int height) => new(path, width, height);

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
            IEnumerable<CaptureArtifactContract> captureArtifacts,
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
            CaptureArtifacts = (captureArtifacts
                    ?? Array.Empty<CaptureArtifactContract>())
                .ToArray();
            CapturePaths = CaptureArtifacts
                .Select(capture => capture.Path)
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
        public IReadOnlyList<CaptureArtifactContract> CaptureArtifacts { get; }
        public IReadOnlyList<string> CapturePaths { get; }
        public IReadOnlyList<string> RequiredReportMarkers { get; }
        private Action StopRunnerAction { get; }
        private Action CleanupRequestAction { get; }

        public void CleanupRequest() => CleanupRequestAction();

        public void StopRunner() => StopRunnerAction();
    }

    private readonly struct CaptureArtifactContract
    {
        public CaptureArtifactContract(string path, int width, int height)
        {
            Path = path ?? string.Empty;
            Width = width;
            Height = height;
        }

        public string Path { get; }
        public int Width { get; }
        public int Height { get; }
    }

    private readonly struct ConsoleEvidence
    {
        public ConsoleEvidence(
            bool healthy,
            int warnings,
            int errors,
            int exceptions,
            int asserts,
            IEnumerable<string> failureReasons,
            IEnumerable<string> offendingLogPreview)
        {
            Healthy = healthy;
            Warnings = warnings;
            Errors = errors;
            Exceptions = exceptions;
            Asserts = asserts;
            FailureReasons = (failureReasons ?? Array.Empty<string>())
                .Take(ConsolePreviewLineLimit)
                .ToArray();
            OffendingLogPreview = (offendingLogPreview
                    ?? Array.Empty<string>())
                .Take(ConsolePreviewLineLimit)
                .ToArray();
        }

        public bool Healthy { get; }
        public int Warnings { get; }
        public int Errors { get; }
        public int Exceptions { get; }
        public int Asserts { get; }
        public IReadOnlyList<string> FailureReasons { get; }
        public IReadOnlyList<string> OffendingLogPreview { get; }
        public int TotalOffendingEntries =>
            Warnings + Errors + Exceptions + Asserts;

        public string GetFailureDetail()
        {
            if (Healthy && TotalOffendingEntries == 0)
            {
                return string.Empty;
            }

            string reason = FailureReasons.Count > 0
                ? string.Join(" | ", FailureReasons)
                : "warnings/errors/exceptions/asserts were recorded.";
            string preview = OffendingLogPreview.Count > 0
                ? " Offending log preview: "
                    + string.Join(" || ", OffendingLogPreview)
                : string.Empty;
            return "Console acceptance failed: " + reason + preview;
        }

        public ConsoleEvidence WithFailure(string failure)
        {
            return new ConsoleEvidence(
                false,
                Warnings,
                Errors,
                Exceptions,
                Asserts,
                FailureReasons.Concat(new[]
                {
                    BoundConsolePreviewLine(failure)
                }),
                OffendingLogPreview);
        }
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
