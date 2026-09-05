#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the durable EditMode-to-PlayMode boundary for the two V27 primitive
/// survival gates. The JSON request remains authoritative until the runner has
/// published its terminal report, so a domain reload or editor restart cannot
/// silently turn a queued verification into an untracked PlayMode session.
/// </summary>
[InitializeOnLoad]
public static class PrimitiveStartSurvivalPlayModeRequestRunner
{
    public const string RequestPath =
        "Temp/v27-primitive-survival-playmode.request.json";
    internal const string FocusedMode = "focused";
    internal const string SixAdultOutageMode = "six-adult-outage";

    private const int RequestSchemaVersion = 1;
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private const string StartSceneLeaseOwnerId =
        "qa:v27-primitive-survival";
    private const string PersistenceSnapshotId =
        "v27-primitive-survival";
    private const string PersistenceOwnedKey =
        "DungeonStory.V27PrimitiveSurvival.PersistenceOwned";
    private static readonly UTF8Encoding StrictUtf8NoBom =
        new(false, true);

    private static bool runnerCreated;

    static PrimitiveStartSurvivalPlayModeRequestRunner()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall -= RecoverOrphanedOwnership;
        EditorApplication.delayCall += RecoverOrphanedOwnership;
    }

    internal static bool HasPendingDurableRun =>
        File.Exists(RequestPath)
        || SessionState.GetBool(PersistenceOwnedKey, false)
        || PlayModeVerificationStartSceneLease.IsOwnedBy(
            StartSceneLeaseOwnerId)
        || UnityEngine.Object.FindFirstObjectByType<
            PrimitiveStartSurvivalPlayModeRunner>() != null;

    internal static void QueueFocused() => Queue(FocusedMode);

    internal static void QueueSixAdultOutage() => Queue(SixAdultOutageMode);

    private static void Queue(string mode)
    {
        RequireMode(mode);
        if (HasPendingDurableRun)
        {
            throw new InvalidOperationException(
                "A primitive-survival verification is already pending or running.");
        }
        if (File.Exists(
                PrimitiveStartSurvivalPlayModeVerifier.PopulationStageRequestPath))
        {
            throw new InvalidOperationException(
                "The population-stage survival verification is already pending.");
        }
        if (EditorApplication.isCompiling
            || EditorUtility.scriptCompilationFailed
            || EditorApplication.isPlayingOrWillChangePlaymode
                && !EditorApplication.isPlaying)
        {
            throw new InvalidOperationException(
                "Primitive-survival verification requires stable compiled EditMode or an already-entered PlayMode.");
        }

        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ReportPath(mode));
        PrimitiveSurvivalPlayModeRequest request = new()
        {
            schemaVersion = RequestSchemaVersion,
            mode = mode,
            allScriptsDigest =
                V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest(),
            gameplaySceneSha256 =
                V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest()
        };
        RequireOfficialScene(request.gameplaySceneSha256);
        WriteRequestAtomic(request);

        if (EditorApplication.isPlaying)
        {
            TryStartPendingOrFail();
        }
        else
        {
            OnEditorUpdate();
        }
    }

    private static void OnEditorUpdate()
    {
        if (!File.Exists(RequestPath)
            || EditorApplication.isCompiling
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        try
        {
            ReadRequiredCurrentRequest();
            AcquirePersistenceIfRequired();
            PlayModeVerificationStartSceneLease.Acquire(
                StartSceneLeaseOwnerId,
                GameplayScenePath);
            EditorApplication.EnterPlaymode();
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "The primitive-survival PlayMode transition was rejected.");
            }
        }
        catch (Exception exception)
        {
            FailBeforePlay("EDITOR_BOOT_PREPARE_FAILED: " + exception);
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            TryStartPendingOrFail();
            return;
        }
        if (change != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        bool ownedReturn = File.Exists(RequestPath)
            || SessionState.GetBool(PersistenceOwnedKey, false)
            || PlayModeVerificationStartSceneLease.IsOwnedBy(
                StartSceneLeaseOwnerId);
        runnerCreated = false;
        if (!ownedReturn)
        {
            return;
        }

        string cleanupFailure = CleanupEditorOwnership();
        if (File.Exists(RequestPath))
        {
            string mode = TryReadModeForDiagnostics();
            PublishFailure(
                mode,
                "PLAYMODE_ABORTED verifier returned to EditMode before completion"
                + (cleanupFailure.Length == 0
                    ? string.Empty
                    : " | " + cleanupFailure));
            File.Delete(RequestPath);
        }
        else if (cleanupFailure.Length > 0)
        {
            Debug.LogError(cleanupFailure);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapPendingRun()
    {
        TryStartPendingOrFail();
    }

    private static void TryStartPendingOrFail()
    {
        if (!EditorApplication.isPlaying || !File.Exists(RequestPath))
        {
            return;
        }
        PrimitiveStartSurvivalPlayModeRunner existing =
            UnityEngine.Object.FindFirstObjectByType<
                PrimitiveStartSurvivalPlayModeRunner>();
        if (existing != null)
        {
            runnerCreated = true;
            return;
        }
        if (runnerCreated)
        {
            // Enter Play Mode Options may preserve static fields while the
            // previous scene object was destroyed. The durable file, not the
            // static latch, owns whether a run still needs a runner.
            runnerCreated = false;
        }

        try
        {
            if (PlayModeVerificationStartSceneLease.IsOwnedBy(
                    StartSceneLeaseOwnerId))
            {
                PlayModeVerificationStartSceneLease.RestoreOwned(
                    StartSceneLeaseOwnerId);
            }
            PrimitiveSurvivalPlayModeRequest request =
                ReadRequiredCurrentRequest();
            string activeScene = SceneManager.GetActiveScene().path;
            if (!string.Equals(
                    activeScene,
                    GameplayScenePath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "BOOT_GAMEPLAY_SCENE_MISMATCH: expected="
                    + GameplayScenePath + "; actual=" + activeScene);
            }

            runnerCreated = true;
            PrimitiveStartSurvivalPlayModeRunner runner = new GameObject(
                    request.mode == FocusedMode
                        ? "V27 Primitive Survival Focused Verification"
                        : "V27 Six-Adult Service Outage Verification")
                .AddComponent<PrimitiveStartSurvivalPlayModeRunner>();
            runner.FocusedOnly = request.mode == FocusedMode;
            runner.SixAdultOutage = request.mode == SixAdultOutageMode;
            runner.DurableRequestMode = request.mode;
        }
        catch (Exception exception)
        {
            FailBeforePlay("PLAYMODE_BOOT_FAILED: " + exception);
            RequestExitPlayMode();
        }
    }

    internal static bool TryValidateCompletion(
        string expectedMode,
        out string sourceDigest,
        out string sceneDigest,
        out string failure)
    {
        sourceDigest = string.Empty;
        sceneDigest = string.Empty;
        failure = string.Empty;
        try
        {
            RequireMode(expectedMode);
            PrimitiveSurvivalPlayModeRequest request =
                ReadRequiredCurrentRequest();
            sourceDigest = request.allScriptsDigest;
            sceneDigest = request.gameplaySceneSha256;
            if (!string.Equals(
                    request.mode,
                    expectedMode,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The durable request mode does not match its runner. expected="
                    + expectedMode + "; actual=" + request.mode);
            }
            return true;
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ": " + exception.Message;
            return false;
        }
    }

    internal static void CompleteRun(string expectedMode, string reportPath)
    {
        if (!File.Exists(reportPath))
        {
            PublishFailure(
                expectedMode,
                "TERMINAL_REPORT_MISSING after verifier completion: "
                + reportPath);
        }
        File.Delete(RequestPath);
        runnerCreated = false;
        RequestExitPlayMode();
    }

    private static PrimitiveSurvivalPlayModeRequest ReadRequiredCurrentRequest()
    {
        if (!File.Exists(RequestPath))
        {
            throw new FileNotFoundException(
                "The durable primitive-survival request is missing.",
                RequestPath);
        }

        string json = File.ReadAllText(RequestPath, StrictUtf8NoBom);
        PrimitiveSurvivalPlayModeRequest request;
        try
        {
            request = JsonUtility.FromJson<PrimitiveSurvivalPlayModeRequest>(
                json);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "The durable primitive-survival request JSON is malformed.",
                exception);
        }
        if (request == null
            || !string.Equals(
                JsonUtility.ToJson(request) + "\n",
                json,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The durable primitive-survival request is not canonical JSON.");
        }
        if (request.schemaVersion != RequestSchemaVersion)
        {
            throw new InvalidDataException(
                "Unsupported primitive-survival request schema: "
                + request.schemaVersion);
        }
        RequireMode(request.mode);
        RequireLowercaseSha256(
            request.allScriptsDigest,
            nameof(request.allScriptsDigest));
        RequireLowercaseSha256(
            request.gameplaySceneSha256,
            nameof(request.gameplaySceneSha256));
        string currentSource =
            V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest();
        string currentScene =
            V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest();
        RequireOfficialScene(currentScene);
        if (!string.Equals(
                request.allScriptsDigest,
                currentSource,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "CURRENT_SOURCE_CHANGED_DURING_PRIMITIVE_SURVIVAL_REQUEST");
        }
        if (!string.Equals(
                request.gameplaySceneSha256,
                currentScene,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "GAMEPLAY_SCENE_CHANGED_DURING_PRIMITIVE_SURVIVAL_REQUEST");
        }
        return request;
    }

    private static void WriteRequestAtomic(
        PrimitiveSurvivalPlayModeRequest request)
    {
        string temporary = RequestPath + ".tmp";
        File.Delete(temporary);
        try
        {
            File.WriteAllText(
                temporary,
                JsonUtility.ToJson(request) + "\n",
                StrictUtf8NoBom);
            File.Move(temporary, RequestPath);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }

    private static void AcquirePersistenceIfRequired()
    {
        if (SessionState.GetBool(PersistenceOwnedKey, false)
            && !PlayModeVerificationPersistenceSnapshot.Exists(
                PersistenceSnapshotId))
        {
            SessionState.EraseBool(PersistenceOwnedKey);
        }
        if (DungeonFinalPlayModeAcceptanceRequestFacade
                .IsPersistenceCoordinatorActive
            || SessionState.GetBool(PersistenceOwnedKey, false))
        {
            return;
        }

        PlayModeVerificationPersistenceSnapshot.CaptureCurrent(
            PersistenceSnapshotId);
        SessionState.SetBool(PersistenceOwnedKey, true);
    }

    private static void RestoreOwnedPersistence()
    {
        if (!SessionState.GetBool(PersistenceOwnedKey, false))
        {
            return;
        }
        PlayModeVerificationPersistenceSnapshot.Restore(
            PersistenceSnapshotId);
        SessionState.EraseBool(PersistenceOwnedKey);
    }

    private static string CleanupEditorOwnership()
    {
        string failure = string.Empty;
        try
        {
            if (PlayModeVerificationStartSceneLease.IsOwnedBy(
                    StartSceneLeaseOwnerId))
            {
                PlayModeVerificationStartSceneLease.RestoreOwned(
                    StartSceneLeaseOwnerId);
            }
        }
        catch (Exception exception)
        {
            failure = "START_SCENE_RESTORE_FAILED: " + exception;
        }
        try
        {
            RestoreOwnedPersistence();
        }
        catch (Exception exception)
        {
            failure += (failure.Length == 0 ? string.Empty : " | ")
                + "PERSISTENCE_RESTORE_FAILED: " + exception;
        }
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        return failure;
    }

    private static void FailBeforePlay(string detail)
    {
        string mode = TryReadModeForDiagnostics();
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            string cleanupFailure = CleanupEditorOwnership();
            if (cleanupFailure.Length > 0)
            {
                detail += " | " + cleanupFailure;
            }
        }
        else
        {
            try
            {
                if (PlayModeVerificationStartSceneLease.IsOwnedBy(
                        StartSceneLeaseOwnerId))
                {
                    PlayModeVerificationStartSceneLease.RestoreOwned(
                        StartSceneLeaseOwnerId);
                }
            }
            catch (Exception exception)
            {
                detail += " | START_SCENE_RESTORE_FAILED: " + exception;
            }
        }

        PublishFailure(mode, detail);
        File.Delete(RequestPath);
        runnerCreated = false;
        Debug.LogError(
            "Primitive-survival durable request failed: " + detail);
    }

    private static void PublishFailure(string mode, string detail)
    {
        Directory.CreateDirectory("Artifacts/QA");
        File.WriteAllText(
            ReportPath(mode),
            "FAIL\n"
            + "FAILURE EDITOR_REQUEST_LIFECYCLE: "
            + CanonicalSingleLine(detail) + "\n",
            StrictUtf8NoBom);
    }

    private static string TryReadModeForDiagnostics()
    {
        try
        {
            if (!File.Exists(RequestPath))
            {
                return FocusedMode;
            }
            PrimitiveSurvivalPlayModeRequest request = JsonUtility.FromJson<
                PrimitiveSurvivalPlayModeRequest>(
                File.ReadAllText(RequestPath, StrictUtf8NoBom));
            return request != null && IsSupportedMode(request.mode)
                ? request.mode
                : FocusedMode;
        }
        catch
        {
            return FocusedMode;
        }
    }

    private static string ReportPath(string mode) =>
        string.Equals(mode, SixAdultOutageMode, StringComparison.Ordinal)
            ? PrimitiveStartSurvivalPlayModeVerifier.SixAdultOutageReportPath
            : PrimitiveStartSurvivalPlayModeVerifier.FocusedReportPath;

    private static void RecoverOrphanedOwnership()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || File.Exists(RequestPath)
            || !SessionState.GetBool(PersistenceOwnedKey, false)
                && !PlayModeVerificationStartSceneLease.IsOwnedBy(
                    StartSceneLeaseOwnerId))
        {
            return;
        }

        string failure = CleanupEditorOwnership();
        if (failure.Length > 0)
        {
            Debug.LogError(
                "Primitive-survival orphaned ownership cleanup failed: "
                + failure);
        }
    }

    private static void RequestExitPlayMode()
    {
        EditorApplication.delayCall -= ExitPlayModeIfNeeded;
        EditorApplication.delayCall += ExitPlayModeIfNeeded;
    }

    private static void ExitPlayModeIfNeeded()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
        }
    }

    private static void RequireOfficialScene(string digest)
    {
        if (!string.Equals(
                digest,
                V27CurrentSourceEvidenceDigest.OfficialGameplaySceneSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "OFFICIAL_GAMEPLAY_SCENE_DIGEST_MISMATCH: " + digest);
        }
    }

    private static void RequireMode(string mode)
    {
        if (!IsSupportedMode(mode))
        {
            throw new ArgumentException(
                "Unsupported primitive-survival verification mode: " + mode,
                nameof(mode));
        }
    }

    private static bool IsSupportedMode(string mode) =>
        string.Equals(mode, FocusedMode, StringComparison.Ordinal)
        || string.Equals(mode, SixAdultOutageMode, StringComparison.Ordinal);

    private static void RequireLowercaseSha256(string value, string field)
    {
        if (value == null || value.Length != 64)
        {
            throw new InvalidDataException(field + " must be SHA-256 hex.");
        }
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character is not (>= '0' and <= '9')
                && character is not (>= 'a' and <= 'f'))
            {
                throw new InvalidDataException(
                    field + " must be lowercase SHA-256 hex.");
            }
        }
    }

    private static string CanonicalSingleLine(string value) =>
        (value ?? string.Empty)
        .Replace('\r', ' ')
        .Replace('\n', ' ')
        .Trim();
}

[Serializable]
internal sealed class PrimitiveSurvivalPlayModeRequest
{
    public int schemaVersion;
    public string mode = string.Empty;
    public string allScriptsDigest = string.Empty;
    public string gameplaySceneSha256 = string.Empty;
}
#endif
