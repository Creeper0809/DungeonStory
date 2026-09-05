#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Balance;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

[InitializeOnLoad]
public static class V27PairedClutterPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/v27-balance-paired-run-rng.txt";
    public const string PairedCsvPath =
        "Artifacts/QA/v27-balance-paired-run-rng.csv";
    public const string ClutterCsvPath =
        "Artifacts/QA/v27-balance-floor-clutter.csv";
    public const string FocusedReportPath =
        "Temp/v27-balance-paired-clutter-focused.txt";
    public const string FocusedPairedCsvPath =
        "Temp/v27-balance-paired-run-rng-focused.csv";
    public const string FocusedClutterCsvPath =
        "Temp/v27-balance-floor-clutter-focused.csv";
    public const string ProgressPath =
        "Temp/v27-balance-paired-clutter-progress.txt";
    private const string RequestPath = "Temp/v27-balance-paired-clutter.flag";
    private const string SceneLeaseOwnerPath =
        "Temp/v27-balance-paired-clutter-scene-lease.flag";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private const string DirtySceneProbeDirectory =
        "Assets/__V27DirtySceneProbe";
    private const string DirtySceneProbePath =
        DirtySceneProbeDirectory + "/GameplayScene.unity";
    private const string ExternalDispatchRequestPath =
        "Temp/v27-unity-editor-dispatch.request";
    private const string ExternalDispatchResultPath =
        "Temp/v27-unity-editor-dispatch.result";
    private const string QueuedDispatchRequestPath =
        "Temp/v27-paired-clutter-queued-dispatch.request";
    private const string AssemblyReloadInterruptionPath =
        "Temp/v27-balance-paired-clutter-assembly-reload.flag";
    private const string AssemblyReloadInterruptionSessionKey =
        "DungeonStory.V27.PairedClutter.AssemblyReloadInterrupted";
    private static bool queuedDispatch;
    private static int queuedSeedCount;
    private static int queuedFocusedSeed;
    private static double queuedDispatchAfter;
    private static int interruptionCleanupAttempts;
    private static double nextInterruptionCleanupAttempt;
    public static IReadOnlyList<string> EvidenceSourcePaths { get; } =
        Array.AsReadOnly(new[]
        {
            "Assets/Scripts/Services/Economy/Editor/V27PairedClutterPlayModeVerifier.cs",
            "Assets/Scripts/Services/Economy/V27PopulationCapacityModels.cs",
            "Assets/Scripts/Services/Infrastructure/DungeonSpaceExpansionRuntime.cs",
            "Assets/Scripts/Services/Items/PhysicalStockQuery.cs",
            "Assets/Scripts/Services/Foundation/Random/RandomStreamProvider.cs",
             "Assets/Scripts/Services/Character/AI/CharacterAiScheduler.cs",
             "Assets/Scripts/Services/Character/AI/AIBrain.cs",
             "Assets/Scripts/Services/Character/AI/Action/AIHaul.cs",
             "Assets/Scripts/Services/Character/Ability/AbilityMove.cs",
             "Assets/Scripts/Services/Items/AbilityHaul.cs",
             "Assets/Scripts/Services/Items/WarehouseMassAdmissionService.cs",
             "Assets/Scripts/Services/Items/WorldItemWarehouseService.cs",
             "Assets/Scripts/Services/Items/WorldItemHaulPlanningService.cs",
             "Assets/Scripts/Services/Items/Editor/SyntheticPreparedOutputCanaryGameplaySceneLease.cs"
         });
    static V27PairedClutterPlayModeVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall -= RecoverOwnedSceneLeaseIfOrphaned;
        EditorApplication.delayCall += RecoverOwnedSceneLeaseIfOrphaned;
        EditorApplication.update -= ProcessExternalDispatch;
        EditorApplication.update += ProcessExternalDispatch;
        EditorApplication.update -= DispatchQueuedRun;
        EditorApplication.update += DispatchQueuedRun;
        EditorApplication.update -= RecoverInterruptedRunAfterReload;
        EditorApplication.update += RecoverInterruptedRunAfterReload;
        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
    }

    private static void ProcessExternalDispatch()
    {
        if (!File.Exists(ExternalDispatchRequestPath)
            || EditorApplication.isCompiling
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        try
        {
            string request;
            // The external writer publishes this tiny command through the file
            // system.  Never observe a partially written request and never let
            // a transient sharing violation escape an EditorApplication.update
            // callback: an escaped exception is retried every frame and can
            // make Unity's main-thread watchdog report the MCP bridge as busy.
            using (FileStream stream = new(
                       ExternalDispatchRequestPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.None))
            using (StreamReader reader = new(
                       stream,
                       Encoding.UTF8,
                       detectEncodingFromByteOrderMarks: true,
                       bufferSize: 256,
                       leaveOpen: false))
            {
                request = reader.ReadToEnd().Trim();
            }
            File.Delete(ExternalDispatchRequestPath);

            string[] tokens = request.Split('|');
            string command = tokens[0];
            if (string.Equals(command, "refresh", StringComparison.Ordinal)
                && tokens.Length == 1)
            {
                File.WriteAllText(
                    ExternalDispatchResultPath,
                    "ACCEPTED|refresh");
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                return;
            }
            if (string.Equals(
                    command,
                    "paired-focused",
                    StringComparison.Ordinal)
                && tokens.Length == 2
                && int.TryParse(tokens[1], out int focusedSeed)
                && focusedSeed > 0)
            {
                QueueFocusedRunFromEditorCommand(focusedSeed);
                File.WriteAllText(
                    ExternalDispatchResultPath,
                    $"ACCEPTED|paired-focused|{focusedSeed}");
                return;
            }
            if (string.Equals(
                    command,
                    "paired-full",
                    StringComparison.Ordinal)
                && tokens.Length == 2
                && int.TryParse(tokens[1], out int seedCount)
                && seedCount is >= 32 and <= 64)
            {
                QueueRunFromEditorCommand(seedCount, 1);
                File.WriteAllText(
                    ExternalDispatchResultPath,
                    $"ACCEPTED|paired-full|{seedCount}");
                return;
            }
            if (string.Equals(
                    command,
                    "haul-reachability",
                    StringComparison.Ordinal)
                && tokens.Length == 1)
            {
                EditorApplication.delayCall += () =>
                {
                    bool passed = HaulPlanConstructionSafetyDebugScenarios
                        .RunAll(logSuccess: false);
                    File.WriteAllText(
                        ExternalDispatchResultPath,
                        passed
                            ? "RESULT|haul-reachability|PASS"
                            : "RESULT|haul-reachability|FAIL");
                };
                File.WriteAllText(
                    ExternalDispatchResultPath,
                    "ACCEPTED|haul-reachability");
                return;
            }

            File.WriteAllText(
                ExternalDispatchResultPath,
                "REJECTED|unsupported-request|" + request);
        }
        catch (IOException)
        {
            // The producer still owns the file.  It remains the durable retry
            // token and will be consumed by a later editor frame.
        }
        catch (Exception exception)
        {
            File.WriteAllText(
                ExternalDispatchResultPath,
                "ERROR|" + exception.GetType().Name + "|" + exception.Message);
        }
    }

    [MenuItem("DungeonStory/V27/Run Paired Clutter 4-Arm PlayMode (32 Seeds)")]
    public static void RequestRun() => RequestRun(32, 1);

    [MenuItem("DungeonStory/V27/Run Paired Clutter Focused (1 Seed)")]
    public static void RequestFocusedRun() => RequestRun(1, 1);

    [MenuItem("DungeonStory/V27/Run Paired Clutter Focused - Crop Harvest (Seed 2)")]
    public static void RequestFocusedCropHarvestRun() => RequestRun(1, 2);

    [MenuItem("DungeonStory/V27/Run Paired Clutter Focused - Mining Burst (Seed 3)")]
    public static void RequestFocusedMiningRun() => RequestRun(1, 3);

    public static void RequestFocusedRun(int seed) => RequestRun(1, seed);

    public static void RequestRun(int seedCount) => RequestRun(seedCount, 1);

    /// <summary>
    /// Queues a focused run after the current editor command has returned. MCP
    /// commands must use this entry point so EnterPlaymode/domain reload cannot
    /// keep the bridge's synchronous ProcessCommands progress modal open.
    /// </summary>
    public static void QueueFocusedRunFromEditorCommand(int seed = 1) =>
        QueueRunFromEditorCommand(1, seed);

    public static void QueueRunFromEditorCommand(
        int seedCount,
        int focusedSeed = 1)
    {
        if (queuedDispatch || File.Exists(QueuedDispatchRequestPath))
            throw new InvalidOperationException(
                "A V27 paired clutter editor-command dispatch is already queued.");
        if (seedCount != 1 && seedCount is (< 32 or > 64))
            throw new ArgumentOutOfRangeException(nameof(seedCount));
        if (focusedSeed < 1)
            throw new ArgumentOutOfRangeException(nameof(focusedSeed));

        Directory.CreateDirectory("Temp");
        File.Delete(ProgressPath);
        if (!TryClearAssemblyReloadInterruption())
            throw new IOException(
                "The previous V27 paired clutter interruption marker could not be cleared.");
        File.WriteAllText(
            QueuedDispatchRequestPath,
            seedCount + "|" + focusedSeed,
            new UTF8Encoding(false));
    }

    internal static bool HasPendingDurableRun =>
        File.Exists(QueuedDispatchRequestPath)
        || File.Exists(RequestPath)
        || File.Exists(SceneLeaseOwnerPath);

    internal static bool HasDurableInterruption =>
        SessionState.GetBool(AssemblyReloadInterruptionSessionKey, false)
        || File.Exists(AssemblyReloadInterruptionPath);

    public static void RecoverOwnedSceneLeaseForDiagnostics() =>
        RecoverOwnedSceneLeaseIfOrphaned();

    internal static void PublishProgress(
        string result,
        string phase,
        int seedCount,
        int startSeed,
        bool focused,
        int completedWindows,
        int failures,
        string currentSourceDigest)
    {
        string text = "RESULT=" + result + "\n"
            + "phase=" + (phase ?? string.Empty) + "\n"
            + "seedCount=" + seedCount + "\n"
            + "startSeed=" + startSeed + "\n"
            + "focused=" + (focused ? "true" : "false") + "\n"
            + "completedWindows=" + completedWindows + "\n"
            + "failures=" + failures + "\n"
            + "currentSourceDigest=" + (currentSourceDigest ?? string.Empty) + "\n";
        try
        {
            V27BalanceArtifactWriter.WriteIfDifferent(ProgressPath, stream =>
            {
                byte[] bytes = new UTF8Encoding(false, true).GetBytes(text);
                stream.Write(bytes, 0, bytes.Length);
            });
        }
        catch (IOException)
        {
            // Progress is advisory and may be read by an external monitor on
            // Windows at the exact instant the atomic replacement is published.
            // A transient sharing violation must never abort the authoritative
            // PlayMode run; the next phase/window publishes a fresh snapshot.
        }
    }

    private static void OnBeforeAssemblyReload()
    {
        if (!File.Exists(SceneLeaseOwnerPath) || !EditorApplication.isPlaying)
            return;

        // Publish a control marker before any fallible diagnostic lookup. The
        // SessionState copy survives a domain reload even if the file system is
        // temporarily unavailable.
        SessionState.SetBool(AssemblyReloadInterruptionSessionKey, true);
        TryWriteAssemblyReloadInterruption(
            "PAIRED_RUN_ASSEMBLY_RELOAD_INTERRUPTED|runner-unavailable|0");

        V27PairedClutterPlayModeRunner runner = null;
        try
        {
            runner = UnityEngine.Object.FindFirstObjectByType<
                V27PairedClutterPlayModeRunner>(FindObjectsInactive.Include);
        }
        catch
        {
            // Keep the generic durable marker. Recovery after reload remains
            // authoritative even when the diagnostic lookup itself fails.
        }

        if (runner == null
            || string.Equals(runner.CurrentPhase, "finished", StringComparison.Ordinal))
        {
            TryClearAssemblyReloadInterruption();
            return;
        }

        string phase = runner?.CurrentPhase ?? "runner-unavailable";
        int completedWindows = runner?.CompletedWindowCount ?? 0;
        int failures = runner?.FailureCount ?? 0;
        try
        {
            PublishProgress(
                "INTERRUPTED",
                phase,
                runner.SeedCount,
                runner.StartSeed,
                runner.Focused,
                completedWindows,
                failures + 1,
                runner.CurrentSourceDigestAtStart);
        }
        catch
        {
            // The control marker above owns recovery. Progress is diagnostic.
        }
        TryWriteAssemblyReloadInterruption(
            "PAIRED_RUN_ASSEMBLY_RELOAD_INTERRUPTED|" + phase + "|"
                + completedWindows);
        try
        {
            Debug.LogError(
                "PAIRED_RUN_ASSEMBLY_RELOAD_INTERRUPTED: phase=" + phase
                + "; completedWindows=" + completedWindows + ".");
        }
        catch
        {
            // Never let a diagnostic logger suppress the durable marker.
        }
    }

    private static void RecoverInterruptedRunAfterReload()
    {
        if (!HasDurableInterruption || EditorApplication.isCompiling)
        {
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (EditorApplication.isPlaying)
                EditorApplication.ExitPlaymode();
            return;
        }

        if (interruptionCleanupAttempts >= 3
            || EditorApplication.timeSinceStartup < nextInterruptionCleanupAttempt)
        {
            return;
        }

        File.Delete(RequestPath);
        try
        {
            ReleaseOwnedSceneLease();
            if (!TryClearAssemblyReloadInterruption())
                throw new IOException(
                    "The interrupted-run marker could not be cleared after lease release.");
            interruptionCleanupAttempts = 0;
            nextInterruptionCleanupAttempt = 0d;
        }
        catch (Exception exception)
        {
            interruptionCleanupAttempts++;
            nextInterruptionCleanupAttempt =
                EditorApplication.timeSinceStartup + interruptionCleanupAttempts;
            if (interruptionCleanupAttempts == 1)
            {
                Debug.LogError(
                    "V27 paired clutter interrupted-run cleanup failed: "
                    + exception);
            }
        }
    }

    private static void TryWriteAssemblyReloadInterruption(string text)
    {
        try
        {
            Directory.CreateDirectory("Temp");
            using FileStream stream = new(
                AssemblyReloadInterruptionPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read | FileShare.Delete);
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(text ?? string.Empty);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }
        catch
        {
            // SessionState is the independent durable recovery authority.
        }
    }

    private static bool TryClearAssemblyReloadInterruption()
    {
        try
        {
            if (File.Exists(AssemblyReloadInterruptionPath))
                File.Delete(AssemblyReloadInterruptionPath);
            SessionState.SetBool(AssemblyReloadInterruptionSessionKey, false);
            interruptionCleanupAttempts = 0;
            nextInterruptionCleanupAttempt = 0d;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void DispatchQueuedRun()
    {
        if (!queuedDispatch)
        {
            if (!File.Exists(QueuedDispatchRequestPath)
                || EditorApplication.isCompiling
                || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            string[] tokens;
            try
            {
                tokens = File.ReadAllText(QueuedDispatchRequestPath)
                    .Trim()
                    .Split('|');
            }
            catch (IOException)
            {
                return;
            }
            if (tokens.Length != 2
                || !int.TryParse(tokens[0], out queuedSeedCount)
                || !int.TryParse(tokens[1], out queuedFocusedSeed)
                || queuedSeedCount != 1
                    && queuedSeedCount is (< 32 or > 64)
                || queuedFocusedSeed < 1)
            {
                File.Delete(QueuedDispatchRequestPath);
                queuedSeedCount = 0;
                queuedFocusedSeed = 0;
                Debug.LogError(
                    "The durable V27 paired clutter request is malformed.");
                return;
            }
            queuedDispatchAfter = EditorApplication.timeSinceStartup + 0.25d;
            queuedDispatch = true;
            return;
        }

        if (EditorApplication.timeSinceStartup < queuedDispatchAfter
            || EditorApplication.isCompiling
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        int seedCount = queuedSeedCount;
        int focusedSeed = queuedFocusedSeed;
        queuedDispatch = false;
        queuedSeedCount = 0;
        queuedFocusedSeed = 0;
        queuedDispatchAfter = 0d;
        try
        {
            RequestRun(seedCount, focusedSeed);
            File.Delete(QueuedDispatchRequestPath);
        }
        catch (Exception exception)
        {
            File.Delete(QueuedDispatchRequestPath);
            Debug.LogError(
                "V27 paired clutter queued dispatch failed: " + exception);
        }
    }

    public static string ComputeEvidenceSourceDigest()
    {
        StringBuilder builder = new();
        foreach (string path in EvidenceSourcePaths)
        {
            builder.Append(path).Append('\t')
                .Append(V27BalanceArtifactWriter.ComputeSha256(path))
                .Append('\n');
        }

        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            new UTF8Encoding(false, true).GetBytes(builder.ToString()));
        const string hex = "0123456789abcdef";
        char[] result = new char[digest.Length * 2];
        for (int index = 0; index < digest.Length; index++)
        {
            result[index * 2] = hex[digest[index] >> 4];
            result[index * 2 + 1] = hex[digest[index] & 15];
        }
        return new string(result);
    }

    private static void RequestRun(int seedCount, int focusedSeed)
    {
        if (seedCount != 1 && seedCount is (< 32 or > 64))
            throw new ArgumentOutOfRangeException(nameof(seedCount));
        if (focusedSeed < 1)
            throw new ArgumentOutOfRangeException(nameof(focusedSeed));
        if (!TryClearAssemblyReloadInterruption())
            throw new IOException(
                "The previous V27 paired clutter interruption marker could not be cleared.");
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        if (EditorApplication.isPlaying)
        {
            // A live runner does not cross an Edit -> Play boundary.  Leaving a
            // request file here would replay the same run on the next Play entry.
            StartRunner(seedCount, focusedSeed);
            return;
        }

        if (EditorApplication.isCompiling)
            throw new InvalidOperationException(
                "V27 paired clutter cannot enter Play Mode while scripts compile.");
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "V27 paired clutter cannot dispatch during a Play Mode transition.");
        if (EditorUtility.scriptCompilationFailed)
        {
            if (File.Exists(RequestPath))
                File.Delete(RequestPath);
            throw new InvalidOperationException(
                "V27 paired clutter cannot enter Play Mode because the latest "
                + "script compilation failed. Fix compiler errors and retry.");
        }

        Scene active = SceneManager.GetActiveScene();
        if (!CanDiscardByteIdenticalDirtyScene(active, out string dirtyFailure))
            throw new InvalidOperationException(
                "V27 paired clutter refuses to replace a dirty scene: "
                + dirtyFailure);

        if (SyntheticPreparedOutputCanaryGameplaySceneLease.IsActive
            || File.Exists(SceneLeaseOwnerPath))
        {
            throw new InvalidOperationException(
                "V27 paired clutter cannot acquire its sanitized GameplayScene "
                + "because another verification lease is active.");
        }

        try
        {
            SyntheticPreparedOutputCanaryGameplaySceneLease.Acquire();
            File.WriteAllText(SceneLeaseOwnerPath, GameplayScenePath);
            EditorSceneManager.OpenScene(
                SyntheticPreparedOutputCanaryGameplaySceneLease
                    .ExpectedRuntimeScenePath,
                OpenSceneMode.Single);
            File.WriteAllText(RequestPath, $"{seedCount}|{focusedSeed}");
            EditorApplication.EnterPlaymode();
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "V27 paired clutter Play Mode transition was rejected.");
            }
        }
        catch
        {
            if (File.Exists(RequestPath))
                File.Delete(RequestPath);
            ReleaseOwnedSceneLease();
            throw;
        }
    }

    private static bool CanDiscardByteIdenticalDirtyScene(
        Scene active,
        out string failure)
    {
        failure = string.Empty;
        if (!active.IsValid())
        {
            failure = "active scene is invalid";
            return false;
        }
        if (!active.isDirty)
            return true;
        if (!string.Equals(
                active.path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            failure = $"non-official dirty scene path={active.path}";
            return false;
        }

        bool byteIdentical = false;
        try
        {
            if (AssetDatabase.IsValidFolder(DirtySceneProbeDirectory)
                && !AssetDatabase.DeleteAsset(DirtySceneProbeDirectory))
            {
                failure = "stale dirty-scene probe could not be removed";
            }
            else
            {
                AssetDatabase.CreateFolder("Assets", "__V27DirtySceneProbe");
                if (!EditorSceneManager.SaveScene(
                        active,
                        DirtySceneProbePath,
                        saveAsCopy: true))
                {
                    failure = "dirty scene could not be serialized as a probe copy";
                }
                else
                {
                    string officialHash =
                        V27BalanceArtifactWriter.ComputeSha256(GameplayScenePath);
                    string probeHash =
                        V27BalanceArtifactWriter.ComputeSha256(DirtySceneProbePath);
                    byteIdentical = string.Equals(
                        officialHash,
                        probeHash,
                        StringComparison.OrdinalIgnoreCase);
                    if (!byteIdentical)
                    {
                        failure = $"serialized dirty scene differs from authority;"
                            + $"official={officialHash};probe={probeHash}";
                    }
                }
            }
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ":" + exception.Message;
            byteIdentical = false;
        }
        finally
        {
            if (AssetDatabase.IsValidFolder(DirtySceneProbeDirectory)
                && !AssetDatabase.DeleteAsset(DirtySceneProbeDirectory))
            {
                if (string.IsNullOrWhiteSpace(failure))
                    failure = "byte-identical dirty-scene probe cleanup failed";
                byteIdentical = false;
            }
        }
        return byteIdentical;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() => TryStartPending();

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode)
            TryStartPending();
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            try
            {
                ReleaseOwnedSceneLease();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "V27 paired clutter scene lease cleanup failed: "
                    + exception);
            }
        }
    }

    private static void RecoverOwnedSceneLeaseIfOrphaned()
    {
        if (!File.Exists(SceneLeaseOwnerPath)
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        try
        {
            ReleaseOwnedSceneLease();
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "V27 paired clutter orphaned scene lease recovery failed: "
                + exception);
        }
    }

    private static void ReleaseOwnedSceneLease()
    {
        if (!File.Exists(SceneLeaseOwnerPath))
            return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "V27 paired clutter scene lease cannot be released during a Play Mode transition.");

        string temporaryScenePath =
            SyntheticPreparedOutputCanaryGameplaySceneLease
                .ExpectedRuntimeScenePath;
        Scene ownedScene = default;
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene loaded = SceneManager.GetSceneAt(index);
            if (loaded.IsValid()
                && string.Equals(
                    loaded.path,
                    temporaryScenePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                ownedScene = loaded;
                break;
            }
        }

        if (ownedScene.IsValid())
        {
            // PlayMode teardown can dirty the verifier-owned scene. Unity will
            // refuse to close that scene without a save prompt, so persist only
            // this disposable copy to its own staging path before replacing it.
            // The official GameplayScene is never a save target here.
            string temporaryDirectory = Path.GetDirectoryName(
                    temporaryScenePath)
                ?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(temporaryDirectory))
                throw new InvalidOperationException(
                    "V27 paired clutter temporary scene directory is invalid.");
            if (!AssetDatabase.IsValidFolder(temporaryDirectory))
            {
                string parent = Path.GetDirectoryName(temporaryDirectory)
                    ?.Replace('\\', '/');
                string folder = Path.GetFileName(temporaryDirectory);
                if (string.IsNullOrWhiteSpace(parent)
                    || string.IsNullOrWhiteSpace(folder)
                    || string.IsNullOrWhiteSpace(
                        AssetDatabase.CreateFolder(parent, folder)))
                {
                    throw new InvalidOperationException(
                        "V27 paired clutter could not recreate its disposable scene directory.");
                }
            }
            if (ownedScene.isDirty
                && !EditorSceneManager.SaveScene(
                    ownedScene,
                    temporaryScenePath,
                    saveAsCopy: false))
            {
                throw new InvalidOperationException(
                    "V27 paired clutter could not persist its disposable scene before cleanup.");
            }
        }

        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid()
            || !string.Equals(
                active.path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase)
            || SceneManager.sceneCount != 1)
        {
            Scene official = EditorSceneManager.OpenScene(
                GameplayScenePath,
                OpenSceneMode.Single);
            if (!official.IsValid()
                || !string.Equals(
                    official.path,
                    GameplayScenePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "V27 paired clutter could not restore the official GameplayScene.");
            }
        }

        bool restoredLease =
            SyntheticPreparedOutputCanaryGameplaySceneLease.RestoreOwned();
        if (!restoredLease)
        {
            string temporaryDirectory = Path.GetDirectoryName(
                    temporaryScenePath)
                ?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(temporaryDirectory)
                && AssetDatabase.IsValidFolder(temporaryDirectory)
                && !AssetDatabase.DeleteAsset(temporaryDirectory))
            {
                throw new InvalidOperationException(
                    "V27 paired clutter could not delete its orphaned disposable scene directory.");
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        File.Delete(SceneLeaseOwnerPath);
        if (!TryClearAssemblyReloadInterruption())
            throw new IOException(
                "The V27 paired clutter interruption marker could not be cleared after lease release.");
    }

    private static void TryStartPending()
    {
        if (!File.Exists(RequestPath))
            return;
        int seedCount = 32;
        int focusedSeed = 1;
        string[] tokens = File.ReadAllText(RequestPath).Trim().Split('|');
        int.TryParse(tokens[0], out seedCount);
        if (tokens.Length > 1)
            int.TryParse(tokens[1], out focusedSeed);
        File.Delete(RequestPath);
        StartRunner(
            seedCount == 1 ? 1 : Mathf.Clamp(seedCount, 32, 64),
            Mathf.Max(1, focusedSeed));
    }

    private static void StartRunner(int seedCount, int focusedSeed)
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                V27PairedClutterPlayModeRunner>() != null)
            return;
        // Enter Play Mode Options may keep static fields while destroying every
        // scene object. The live runner object is the only duplicate authority.
        V27PairedClutterPlayModeRunner runner =
            new GameObject("V27 Paired Clutter PlayMode Runner")
                .AddComponent<V27PairedClutterPlayModeRunner>();
        runner.SeedCount = seedCount;
        runner.Focused = seedCount == 1;
        runner.StartSeed = focusedSeed;
    }
}

public sealed class V27PairedClutterPlayModeRunner : MonoBehaviour
{
    private const string FacilityBurstItemId = "survival:cooked_meal";
    private const string CropId = "crop:twilight-grain";
    private const float GameDaySeconds = 180f;
    private const float WarmupSeconds = 90f;
    private const float WindowSeconds = 45f;
    private const float PickupSearchAndSchedulingHeadroomSeconds = GameDaySeconds;
    private const float PickupCaptureDeltaTime = 1f / 120f;
    private const float RecoverySeconds = 90f;
    private const float WorkMilliWuPerGameSecond = 50000f / GameDaySeconds;
    private const float VerificationTimeScale = 32f;
    private const float ClockProgressTimeoutRealtimeSeconds = 10f;
    private const string ScenarioId = "v27.floor-clutter.paired";

    private readonly List<PairedRunWindowResult> rows = new();
    private readonly List<FloorRow> floorRows = new();
    private readonly List<string> failures = new();
    private readonly List<string> focusedDeferredFailures = new();
    private readonly List<string> consoleIssues = new();
    private readonly Dictionary<string, IReadOnlyList<RandomStreamDiagnosticSnapshot>>
        randomByArmWindow = new(StringComparer.Ordinal);
    private readonly Dictionary<int, HashSet<string>> affectedActorsBySeed = new();
    private readonly Dictionary<string, string> armStartRandomHashes =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> armStartSemanticHashes =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> armStartSemanticTexts =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> focusedFrameTraces =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> measuredActorIds =
        new(StringComparer.Ordinal);

    private DungeonRuntimeLifetimeScope scope;
    private IDungeonGameSaveService saves;
    private IWorldItemStackRuntime items;
    private IDungeonItemCatalogProvider itemCatalog;
    private IWarehousePhysicalMassQueryPort warehouseMassQuery;
    private IItemTransferService itemTransfers;
    private IFloorClutterDiagnosticsQuery clutter;
    private IRandomStreamProvider randomProvider;
    private IRandomStreamDiagnosticsQuery randomDiagnostics;
    private ICharacterAiWorldRegistry world;
    private IWorldDropZoneQuery dropZones;
    private IWorldItemHaulPlanningService haulPlanning;
    private ISurvivalFoodQuery survivalFoodQuery;
    private ISurvivalFoodCommand survivalFood;
    private CropPlotRuntime cropPlots;
    private IResourceEconomyContentCatalog economyCatalog;
    private IWorldResourceRuntime worldResources;
    private ProgressionSceneRuntimeReferences progression;
    private IGameClock clock;
    private IGameClockDiagnosticsControl clockDiagnostics;
    private IGameSpeedController gameSpeed;
    private IDungeonDebugModeService debugMode;
    private IDungeonUserSettingsService userSettings;
    private ICharacterDeprivationRuntime deprivation;
    private CharacterAiScheduler scheduler;
    private CharacterSpawner characterSpawner;
    private GridBuildingPlacementService livePlacementService;
    private Grid grid;
    private DungeonGameSaveData originalSave;
    private string originalSaveJson = string.Empty;
    private string commonCheckpointJson = string.Empty;
    private float commonCheckpointTime;
    private int commonCheckpointFrame;
    private string warehouseId = string.Empty;
    private string overflowWarehouseId = string.Empty;
    private string productionInputWarehouseId = string.Empty;
    private string producerFacilityId = string.Empty;
    private string cropPlotId = string.Empty;
    private string miningRecipeId = string.Empty;
    private string miningNodeId = string.Empty;
    private string cropBurstItemId = string.Empty;
    private string miningBurstItemId = string.Empty;
    private string faultActorId = string.Empty;
    private Vector2Int burstCell;
    private Vector2Int cropBurstCell;
    private Vector2Int miningBurstCell;
    private Vector2Int overflowCell;
    private DungeonSpaceLayoutSnapshot layout;
    private Facility fixtureWarehouse;
    private Facility fixtureOverflowWarehouse;
    private Facility fixtureProducerFacility;
    private Facility fixtureCropPlot;
    private BuildingSO producerFacilityAsset;
    private float originalTimeScale;
    private bool originalRunInBackground;
    private bool originalFreezeNeeds;
    private bool originalFriendlyInvincible;
    private bool originalPauseWildlifeAi;
    private bool originalDeveloperMode;
    private int originalGameSpeed;
    private bool originalGamePause;
    private bool gameSpeedConfigured;
    private bool developerModeConfigured;
    private bool debugModeConfigured;
    private bool schedulerDiagnosticsConfigured;
    private bool spawnerDiagnosticsConfigured;
    private bool originalSchedulerDeterministicMode;
    private bool originalSpawnerDiagnosticsPaused;
    private float originalCaptureDeltaTime;
    private bool finished;
    private bool runCompleted;
    private int requiredSeedCount;
    private int productionBurstArmCount;
    private int facilityBurstArmCount;
    private int cropHarvestBurstArmCount;
    private int miningBurstArmCount;
    private int productionPriorityArmCount;
    private int postPickupFaultArmCount;
    private int lastRuntimeHeadroomErosionCount;
    private string lastRuntimeHeadroomErosionDetail = string.Empty;
    private PairedRunAttributionAssessment finalAssessment;
    private ArmBurstProbe currentBurstProbe;
    private string currentSourceDigestAtStart = string.Empty;

    public int SeedCount { get; set; } = 32;
    public int StartSeed { get; set; } = 1;
    public bool Focused { get; set; }
    public string CurrentPhase { get; private set; } = "created";
    public int CompletedWindowCount => rows.Count;
    public int FailureCount => failures.Count;
    internal string CurrentSourceDigestAtStart => currentSourceDigestAtStart;

    private IEnumerator Start()
    {
        currentSourceDigestAtStart =
            V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest();
        SetPhase("starting");
        originalTimeScale = Time.timeScale;
        originalCaptureDeltaTime = Time.captureDeltaTime;
        originalRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        // Three exact game seconds per rendered frame divides the 90/45/90
        // measurement windows without a partial final tick.
        Time.captureDeltaTime = 3f / VerificationTimeScale;
        Time.timeScale = VerificationTimeScale;
        Application.logMessageReceived += CaptureIssue;
        yield return ExecuteGuarded(RunAll());
        Finish();
    }

    private IEnumerator RunAll()
    {
        yield return ResolveWorld();
        if (failures.Count > 0)
            yield break;
        yield return CreateFixtureAndCheckpoint();
        if (failures.Count > 0)
            yield break;

        int targetSeeds = SeedCount;
        requiredSeedCount = targetSeeds;
        int lastSeed = checked(StartSeed + targetSeeds - 1);
        for (int seed = StartSeed; seed <= lastSeed; seed++)
        {
            yield return RunSeed(seed);
            if (failures.Count > 0 && !Focused)
                yield break;
        }

        if (Focused)
        {
            if (failures.Count > 0)
            {
                runCompleted = true;
                yield break;
            }
            Check(rows.Count == 16, "PAIRED_FOCUSED_FOUR_ARMS",
                $"rows={rows.Count};seeds={rows.Select(value => value.Seed).Distinct().Count()}");
            ValidateProductionInterventionEvidence();
            ValidateFocusedCleanRepeatability();
            ValidateFocusedClutterDelta();
            Check(floorRows.All(value => value.RuntimeHeadroomPermille >= 300),
                "PAIRED_RUNTIME_HEADROOM_AT_LEAST_30_PERCENT",
                $"rows={floorRows.Count};minimumPermille="
                + $"{floorRows.Min(value => value.RuntimeHeadroomPermille)}");
            failures.AddRange(focusedDeferredFailures);
            runCompleted = true;
            yield break;
        }

        PairedRunAttributionAssessment assessment =
            PairedRunAttributionEvaluator.Evaluate(rows);
        if (assessment.RequiresExpandedSample && targetSeeds == 32)
        {
            targetSeeds = 64;
            requiredSeedCount = targetSeeds;
            for (int seed = 33; seed <= targetSeeds; seed++)
            {
                yield return RunSeed(seed);
                if (failures.Count > 0)
                    yield break;
            }
            assessment = PairedRunAttributionEvaluator.Evaluate(rows);
        }

        ValidateProductionInterventionEvidence();
        Check(assessment.Passed, "PAIRED_CLUTTER_ATTRIBUTION",
            $"samples={assessment.SampleCount};medianPermille={assessment.MedianClutterDeltaPermille};"
            + $"p95Permille={assessment.P95ClutterDeltaPermille};maxPermille={assessment.MaximumClutterDeltaPermille};"
            + $"madPermille={assessment.MadPermille};failure={assessment.FailureCode}");
        Check(floorRows.All(value => value.ImmediateFailures == 0),
            "FLOOR_CLUTTER_ACCESS_EGRESS_ZERO",
            $"rows={floorRows.Count};immediate={floorRows.Sum(value => value.ImmediateFailures)}");
        Check(floorRows.Where(value => value.IsRecovery)
                .All(value => value.Persistent == 0),
            "FLOOR_CLUTTER_RECOVERY_ZERO",
            $"recoveryRows={floorRows.Count(value => value.IsRecovery)};"
            + $"persistent={floorRows.Where(value => value.IsRecovery).Sum(value => value.Persistent)}");
        Check(floorRows.All(value => value.RuntimeHeadroomPermille >= 300),
            "PAIRED_RUNTIME_HEADROOM_AT_LEAST_30_PERCENT",
            $"rows={floorRows.Count};minimumPermille="
            + $"{floorRows.Min(value => value.RuntimeHeadroomPermille)}");
        finalAssessment = assessment;
        runCompleted = true;
    }

    private IEnumerator ResolveWorld()
    {
        SetPhase("resolve-world");
        float deadline = Time.realtimeSinceStartup + 30f;
        bool prepared = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = FindObjectsByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(value => value?.Container != null);
            if (scope?.Container != null && LiveActors().Length < 3 && !prepared)
            {
                prepared = true;
                _ = StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            }
            if (scope?.Container != null && LiveActors().Length >= 3)
                break;
            yield return null;
        }
        if (prepared)
        {
            for (int frame = 0; frame < 8; frame++)
                yield return null;
            Time.timeScale = VerificationTimeScale;
        }

        foreach (CharacterActor actor in FindObjectsByType<CharacterActor>(
                     FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                 .Select(CharacterActorCollection.GetCanonical)
                 .Where(value => value != null && value.CurrentLifecycleState is
                     CharacterLifecycleState.EnteringDungeon
                     or CharacterLifecycleState.SpawningOutside)
                 .Distinct())
        {
            actor.GetComponent<AbilityMove>()?.CancelActiveMovement(
                "v27-paired-fixture-settle");
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
        }

        float settlementDeadline = Time.realtimeSinceStartup + 5f;
        int stableFrames = 0;
        int previousCount = -1;
        while (Time.realtimeSinceStartup < settlementDeadline)
        {
            EnsureVerificationTimeScale();
            CharacterActor[] all = FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Select(CharacterActorCollection.GetCanonical)
                .Where(value => value != null && !value.IsDead
                    && value.characterType is not CharacterType.Customer
                        and not CharacterType.Intruder)
                .Distinct()
                .ToArray();
            bool transition = all.Any(value => value.CurrentLifecycleState is
                CharacterLifecycleState.EnteringDungeon
                or CharacterLifecycleState.SpawningOutside);
            int activeCount = all.Count(value =>
                value.CurrentLifecycleState == CharacterLifecycleState.Active);
            stableFrames = !transition && activeCount >= 3 && activeCount == previousCount
                ? stableFrames + 1
                : 0;
            previousCount = activeCount;
            if (stableFrames >= 2)
                break;
            yield return null;
        }

        saves = Resolve<IDungeonGameSaveService>();
        items = Resolve<IWorldItemStackRuntime>();
        itemCatalog = items?.CatalogProvider;
        warehouseMassQuery = Resolve<IStockQuery>() as IWarehousePhysicalMassQueryPort;
        itemTransfers = Resolve<IItemTransferService>();
        clutter = Resolve<IFloorClutterDiagnosticsQuery>();
        randomProvider = Resolve<IRandomStreamProvider>();
        randomDiagnostics = Resolve<IRandomStreamDiagnosticsQuery>();
        world = Resolve<ICharacterAiWorldRegistry>();
        dropZones = Resolve<IWorldDropZoneQuery>();
        haulPlanning = Resolve<IWorldItemHaulPlanningService>();
        survivalFoodQuery = Resolve<ISurvivalFoodQuery>();
        survivalFood = Resolve<ISurvivalFoodCommand>();
        cropPlots = Resolve<CropPlotRuntime>();
        economyCatalog = Resolve<IResourceEconomyContentCatalog>();
        worldResources = Resolve<IWorldResourceRuntime>();
        progression = Resolve<ProgressionSceneRuntimeReferences>();
        clock = Resolve<IGameClock>();
        clockDiagnostics = clock as IGameClockDiagnosticsControl;
        gameSpeed = Resolve<IGameSpeedController>();
        debugMode = Resolve<IDungeonDebugModeService>();
        userSettings = Resolve<IDungeonUserSettingsService>();
        deprivation = Resolve<ICharacterDeprivationRuntime>();
        scheduler = FindFirstObjectByType<CharacterAiScheduler>(
            FindObjectsInactive.Include);
        characterSpawner = FindFirstObjectByType<CharacterSpawner>(
            FindObjectsInactive.Include);
        DungeonStoryGridBuildingController buildingController =
            FindFirstObjectByType<DungeonStoryGridBuildingController>(
                FindObjectsInactive.Include);
        livePlacementService = buildingController != null
            ? typeof(DungeonStoryGridBuildingController)
                .GetField(
                    "placementService",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(buildingController) as GridBuildingPlacementService
            : null;
        world?.TryGetGrid(out grid);
        bool ready = saves != null && items != null && itemCatalog != null
            && warehouseMassQuery != null && itemTransfers != null
            && clutter != null
            && randomProvider != null && randomDiagnostics != null
            && world != null && dropZones != null && haulPlanning != null
            && survivalFoodQuery != null && survivalFood != null
            && cropPlots != null && economyCatalog != null
            && worldResources != null && progression?.BlueprintResearch != null
            && clock != null && gameSpeed != null
            && clockDiagnostics != null
            && debugMode != null && userSettings != null
            && deprivation != null
            && scheduler != null
            && characterSpawner != null
            && livePlacementService != null
            && grid != null
            && LiveActors().Length >= 3;
        CharacterActor[] unresolvedTransitions = FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Select(CharacterActorCollection.GetCanonical)
            .Where(value => value != null && value.CurrentLifecycleState is
                CharacterLifecycleState.EnteringDungeon
                or CharacterLifecycleState.SpawningOutside)
            .Distinct()
            .ToArray();
        ready &= unresolvedTransitions.Length == 0;
        Check(ready, "PAIRED_AUTHORITIES_READY",
            $"save={saves != null};items={items != null};"
            + $"itemCatalog={itemCatalog != null};"
            + $"warehouseMass={warehouseMassQuery != null};"
            + $"transfers={itemTransfers != null};clutter={clutter != null};"
            + $"random={randomProvider != null}/{randomDiagnostics != null};"
            + $"clockDiagnostics={clockDiagnostics != null};"
            + $"world={world != null};dropZones={dropZones != null};"
            + $"haulPlanning={haulPlanning != null};speed={gameSpeed != null};"
            + $"survivalFood={survivalFoodQuery != null}/{survivalFood != null};"
            + $"crop={cropPlots != null};catalog={economyCatalog != null};"
            + $"worldResources={worldResources != null};"
            + $"research={progression?.BlueprintResearch != null};"
            + $"debug={debugMode != null};"
            + $"settings={userSettings != null};"
            + $"deprivation={deprivation != null};grid={grid != null};"
            + $"scheduler={scheduler != null};"
            + $"spawner={characterSpawner != null};"
            + $"placementService={livePlacementService != null};"
            + $"actors={LiveActors().Length};transitions={unresolvedTransitions.Length}");
        if (!ready)
            yield break;
        foreach (CharacterActor actor in EligibleActors()
                     .OrderBy(ActorId, StringComparer.Ordinal)
                     .Take(3))
        {
            measuredActorIds.Add(ActorId(actor));
        }
        Check(measuredActorIds.Count == 3,
            "PAIRED_MEASURED_ACTOR_SET_EXACT",
            $"count={measuredActorIds.Count};ids="
            + string.Join(",", measuredActorIds.OrderBy(value => value,
                StringComparer.Ordinal)));
        if (measuredActorIds.Count != 3)
            yield break;
        originalGameSpeed = gameSpeed.Speed;
        originalGamePause = gameSpeed.IsPaused;
        gameSpeedConfigured = true;
        originalSchedulerDeterministicMode =
            scheduler.DeterministicSimulationForDiagnostics;
        scheduler.ConfigureDeterministicSimulationForDiagnostics(true);
        schedulerDiagnosticsConfigured = true;
        originalSpawnerDiagnosticsPaused =
            characterSpawner.DeterministicSimulationPausedForDiagnostics;
        characterSpawner.ConfigureDeterministicSimulationForDiagnostics(true);
        spawnerDiagnosticsConfigured = true;
        if (!TryReconcileTierZeroForDirectPlayModeEntry())
            yield break;
        originalSave = saves.Capture();
        originalSaveJson = saves.ToJson(originalSave);
        Check(!string.IsNullOrWhiteSpace(originalSaveJson),
            "PAIRED_ORIGINAL_WORLD_CHECKPOINT",
            $"bytes={originalSaveJson.Length}");
        if (string.IsNullOrWhiteSpace(originalSaveJson))
            yield break;

        originalDeveloperMode = userSettings.Current.developerMode;
        if (!originalDeveloperMode)
            userSettings.Update(value => value.developerMode = true);
        developerModeConfigured = true;
        originalFreezeNeeds = debugMode.IsCheatEnabled(DungeonDebugCheat.FreezeNeeds);
        originalFriendlyInvincible = debugMode.IsCheatEnabled(
            DungeonDebugCheat.FriendlyInvincible);
        originalPauseWildlifeAi = debugMode.IsCheatEnabled(
            DungeonDebugCheat.PauseWildlifeAi);
        debugModeConfigured = true;
        ApplyMeasurementIsolation();
        scheduler.ResetDeterministicSimulationCheckpointForDiagnostics();
        for (int frame = 0; frame < 4; frame++)
            yield return null;
    }

    private bool TryReconcileTierZeroForDirectPlayModeEntry()
    {
        IDungeonSpaceExpansionQuery expansionQuery =
            Resolve<IDungeonSpaceExpansionQuery>();
        IDungeonSpaceExpansionCommand expansionCommand =
            Resolve<IDungeonSpaceExpansionCommand>();
        DungeonInteriorLayoutSnapshot before = default;
        string beforeFailure = "expansion-query-missing";
        bool beforeCaptured = expansionQuery != null
            && expansionQuery.TryCaptureLayout(
                out before,
                out beforeFailure);
        if (!Check(
                beforeCaptured,
                "PAIRED_TIER_ZERO_LAYOUT_CAPTURED_BEFORE_RECONCILE",
                beforeCaptured
                    ? $"columns={before.ColumnCount}"
                    : beforeFailure ?? "expansion-query-missing"))
        {
            return false;
        }

        bool canonicalBefore = before.ColumnCount
            is DungeonSpaceExpansionCatalog.SceneSeedInteriorColumns
            or DungeonSpaceExpansionCatalog.InitialInteriorColumns;
        if (!Check(
                canonicalBefore,
                "PAIRED_TIER_ZERO_CANONICAL_PRE_LAYOUT",
                $"columns={before.ColumnCount}"))
        {
            return false;
        }

        DungeonSpaceExpansionResult result = default;
        string reconcileFailure = "expansion-command-missing";
        bool reconciled = expansionCommand != null
            && expansionCommand.TryReconcileNewRunTierZero(
                out result,
                out reconcileFailure);
        if (!Check(
                reconciled,
                "PAIRED_TIER_ZERO_PRODUCTION_RECONCILE",
                reconciled
                    ? $"changed={result.Changed};columns={result.PreviousInteriorColumns}->{result.CurrentInteriorColumns}"
                    : reconcileFailure ?? "expansion-command-missing"))
        {
            return false;
        }

        bool expectedChanged = before.ColumnCount
            == DungeonSpaceExpansionCatalog.SceneSeedInteriorColumns;
        bool exactResult = string.Equals(
                result.ResearchProjectId,
                DungeonSpaceExpansionCatalog.TierZeroInitializationId,
                StringComparison.Ordinal)
            && result.Tier == 0
            && result.PreviousInteriorColumns == before.ColumnCount
            && result.CurrentInteriorColumns
                == DungeonSpaceExpansionCatalog.InitialInteriorColumns
            && result.Changed == expectedChanged;
        if (!Check(
                exactResult,
                "PAIRED_TIER_ZERO_EXACT_TRANSITION",
                $"id={result.ResearchProjectId};tier={result.Tier};"
                + $"changed={result.Changed}/{expectedChanged};"
                + $"columns={result.PreviousInteriorColumns}->{result.CurrentInteriorColumns}"))
        {
            return false;
        }

        bool gridPublished = world.TryGetGrid(out Grid publishedGrid)
            && publishedGrid != null;
        grid = publishedGrid;
        DungeonInteriorLayoutSnapshot after = default;
        string afterFailure = "published-grid-missing";
        bool afterCaptured = gridPublished
            && DungeonSpaceGridLayout.TryCapture(
                grid,
                out after,
                out afterFailure);
        bool exactPublishedLayout = afterCaptured
            && after.ColumnCount
                == DungeonSpaceExpansionCatalog.InitialInteriorColumns
            && after.StartX == before.StartX
            && after.EntrancePosition == before.EntrancePosition;
        return Check(
            exactPublishedLayout,
            "PAIRED_TIER_ZERO_LIVE_GRID_RECAPTURED",
            !gridPublished
                ? "published-grid-missing"
                : afterCaptured
                    ? $"columns={after.ColumnCount};startX={after.StartX};entrance={after.EntrancePosition}"
                    : afterFailure);
    }

    private IEnumerator CreateFixtureAndCheckpoint()
    {
        SetPhase("create-fixture");
        CharacterActor anchor = LiveActors()
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .First();
        CharacterActor fault = LiveActors()
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .Skip(1).First();
        faultActorId = fault.Identity.PersistentId;

        string[] expansionResearchIds =
        {
            DungeonSpaceExpansionCatalog.QuarryResearchId,
            DungeonSpaceExpansionCatalog.StonecuttingResearchId,
            DungeonSpaceExpansionCatalog.DeepMiningResearchId
        };
        Dictionary<string, ResearchProjectSO> expansionProjects = Resources
            .LoadAll<ResearchProjectSO>("SO/Research/Projects")
            .Where(value => value != null
                && expansionResearchIds.Contains(
                    value.ProjectId.Value,
                    StringComparer.Ordinal))
            .ToDictionary(
                value => value.ProjectId.Value,
                StringComparer.Ordinal);
        IFacilityShopCatalog facilityCatalog = Resolve<IFacilityShopCatalog>();
        IGameEventBus gameEvents = Resolve<IGameEventBus>();
        bool expansionAuthorityReady = expansionResearchIds.All(
                expansionProjects.ContainsKey)
            && facilityCatalog != null
            && gameEvents != null;
        Check(expansionAuthorityReady,
            "PAIRED_MINING_EXPANSION_RESEARCH_AUTHORITY",
            $"projects={string.Join(",", expansionProjects.Keys.OrderBy(value => value, StringComparer.Ordinal))};"
            + $"catalog={facilityCatalog != null};events={gameEvents != null}");
        if (!expansionAuthorityReady)
            yield break;

        foreach (string expansionResearchId in expansionResearchIds)
        {
            ResearchProjectSO expansionProject =
                expansionProjects[expansionResearchId];
            BlueprintResearchUnlockResult expansionUnlock =
                BlueprintResearchService.ApplyCompletion(
                    expansionProject,
                    progression.BlueprintResearch.State,
                    progression.BlueprintResearch.ShopUnlockState,
                    facilityCatalog);
            gameEvents.Publish(new BlueprintResearchCompletedEvent(
                expansionProject,
                expansionUnlock));
            for (int frame = 0; frame < 4; frame++)
                yield return null;
        }
        world.TryGetGrid(out grid);
        IDungeonSpaceExpansionQuery expansion = Resolve<IDungeonSpaceExpansionQuery>();
        DungeonInteriorLayoutSnapshot expandedLayout = default;
        string expansionFailure = "expansion authority missing";
        bool expansionApplied = expansion != null
            && expansion.TryCaptureLayout(
                out expandedLayout,
                out expansionFailure)
            && expandedLayout.ColumnCount
                >= DungeonSpaceExpansionCatalog.DeepSectorTargetColumns
            && string.Equals(
                expansion.LastResult.ResearchProjectId,
                DungeonSpaceExpansionCatalog.DeepMiningResearchId,
                StringComparison.Ordinal);
        Check(expansionApplied,
            "PAIRED_MINING_EXPANSION_RESEARCH_APPLIED",
            $"projects={string.Join(",", expansionResearchIds)};"
            + $"columns={(expansionApplied ? expandedLayout.ColumnCount : 0)};"
            + $"failure={(expansionApplied ? string.Empty : expansionFailure)};"
            + $"developerKeyUsed=False");
        if (!expansionApplied || grid == null)
            yield break;

        BuildingSO warehouseAsset = FindWarehouseAsset();
        // Keep the control and overflow facilities on the same all-category
        // authority so the paired run changes pressure, not category/research
        // eligibility or footprint semantics.
        BuildingSO overflowWarehouseAsset = warehouseAsset;
        BuildingSO producerAsset = FindCookFacilityAsset();
        producerFacilityAsset = producerAsset;
        Vector2Int[] reachable = grid.SearchPath(anchor.GetNowXY())
            .GetReachablePositions()
            .Where(value => grid.IsValidGridPos(value) && grid.IsWalkable(value))
            .Where(value => grid.GetGridCell(value)?.GetOccupant(GridLayer.Building) == null)
            .Where(value => !items.GetAllStacks().Any(stack =>
                stack != null && stack.Quantity > 0 && stack.Position == value))
            .Distinct()
            .OrderBy(value => Mathf.Abs(value.x - anchor.GetNowXY().x)
                + Mathf.Abs(value.y - anchor.GetNowXY().y))
            .ThenBy(value => value.x)
            .ThenBy(value => value.y)
            .Skip(2)
            .ToArray();
        Check(warehouseAsset != null && overflowWarehouseAsset != null
                && producerAsset != null && reachable.Length >= 8,
            "PAIRED_FIXTURE_CELLS",
            $"warehouse={warehouseAsset != null};overflow={overflowWarehouseAsset != null};"
            + $"producer={producerAsset != null};cells={reachable.Length}");
        if (warehouseAsset == null || overflowWarehouseAsset == null
            || producerAsset == null || reachable.Length < 8)
            yield break;

        IGameSessionStateProvider sessionState = Resolve<IGameSessionStateProvider>();
        IDungeonDebugRuleQuery debugRules = Resolve<IDungeonDebugRuleQuery>();
        BuildingPlacementValidator placement = new(
            new GridPlacementValidator(),
            () =>
            {
                GameSessionState gameData = null;
                sessionState?.TryGetSessionState(out gameData);
                return new BuildingConditionContext(
                    gameData,
                    progression.BlueprintResearch.State,
                    null,
                    debugRules ?? DisabledDungeonDebugRuleQuery.Instance);
            });
        fixtureWarehouse = world.Warehouses
            .OfType<Facility>()
            .Where(value => value != null
                && value.BuildingData == warehouseAsset
                && value.Inventory != null
                && value.Inventory.MaxMassGrams > 0L
                && value.BuildingData.StoresAllCategories())
            .OrderBy(value => value.RequirePersistentInstanceId().Value,
                StringComparer.Ordinal)
            .FirstOrDefault();
        fixtureProducerFacility = world.Buildings
            .OfType<Facility>()
            .Where(value => value != null
                && value.BuildingData == producerAsset
                && value.BuildingData.Facility.SupportsWork(
                    BuiltInWorkTypeIds.Cook))
            .OrderBy(value => value.RequirePersistentInstanceId().Value,
                StringComparer.Ordinal)
            .FirstOrDefault();
        bool existingAuthoritiesReady = fixtureWarehouse != null
            && fixtureProducerFacility != null;
        Check(existingAuthoritiesReady,
            "PAIRED_FIXTURE_EXISTING_AUTHORITIES",
            $"warehouse={fixtureWarehouse?.RequirePersistentInstanceId().Value ?? "missing"}:"
            + $"{fixtureWarehouse?.centerPos.ToString() ?? "missing"};"
            + $"producer={fixtureProducerFacility?.RequirePersistentInstanceId().Value ?? "missing"}:"
            + $"{fixtureProducerFacility?.centerPos.ToString() ?? "missing"};"
            + $"authorities={DescribeExistingFixtureAuthorities()}");
        if (!existingAuthoritiesReady)
            yield break;

        warehouseId = fixtureWarehouse.RequirePersistentInstanceId().Value;
        Vector2Int warehouseCell = fixtureWarehouse.centerPos;
        producerFacilityId = fixtureProducerFacility
            .RequirePersistentInstanceId().Value;
        burstCell = fixtureProducerFacility.centerPos;
        Vector2Int[] overflowCandidates = reachable
            .Where(value => placement.CanBuild(
                grid, overflowWarehouseAsset, value, out _))
            .OrderBy(value => checked(
                Manhattan(value, warehouseCell)
                + Manhattan(value, burstCell)))
            .ThenBy(value => value.x)
            .ThenBy(value => value.y)
            .ToArray();
        bool fixturePlacementFound = overflowCandidates.Length > 0;
        Vector2Int overflowPlacementCell = fixturePlacementFound
            ? overflowCandidates[0]
            : default;
        Check(fixturePlacementFound,
            "PAIRED_FIXTURE_PRODUCER_RESERVATION",
            $"asset={producerAsset.name};anchor={burstCell};"
            + $"warehouse={warehouseCell};overflow="
            + $"{(fixturePlacementFound ? overflowPlacementCell.ToString() : "missing")};"
            + $"distance={Manhattan(warehouseCell, burstCell)}");
        if (!fixturePlacementFound)
        {
            Fail("PAIRED_FIXTURE_WAREHOUSE_PAIR_PLACEMENT",
                $"no legal overflow placement for existing warehouse/producer authorities;"
                + $"overflowCandidates={overflowCandidates.Length};"
                + $"overflow={DescribePlacementCandidates(overflowWarehouseAsset, overflowCandidates)};"
                + $"existing={DescribeExistingFixtureAuthorities()};"
                + $"overflowAsset={overflowWarehouseAsset.name}");
            yield break;
        }

        overflowCell = overflowPlacementCell;
        bool overflowPlaced = livePlacementService.TryPlaceBuildingImmediateUnchecked(
            overflowWarehouseAsset,
            overflowCell,
            chargeCost: false,
            out string overflowPlacementFailure);
        Check(overflowPlaced,
            "PAIRED_FIXTURE_OVERFLOW_PRODUCTION_PLACEMENT",
            $"asset={overflowWarehouseAsset.name};anchor={overflowCell};"
            + $"failure={overflowPlacementFailure}");
        if (!overflowPlaced)
            yield break;
        for (int frame = 0; frame < 4; frame++)
            yield return null;
        fixtureOverflowWarehouse = world.Warehouses
            .OfType<Facility>()
            .SingleOrDefault(value => value != null
                && value.BuildingData == overflowWarehouseAsset
                && value.centerPos == overflowCell);
        Check(fixtureOverflowWarehouse != null,
            "PAIRED_FIXTURE_OVERFLOW_LIVE_REGISTRATION",
            $"asset={overflowWarehouseAsset.name};anchor={overflowCell};"
            + $"registered={fixtureOverflowWarehouse != null}");
        if (fixtureOverflowWarehouse == null)
            yield break;
        overflowWarehouseId = fixtureOverflowWarehouse
            .RequirePersistentInstanceId().Value;

        bool producerPublished = world.Buildings.Any(value => value != null
            && value.PersistentInstanceId.Value == producerFacilityId);
        Check(grid.IsValidGridPos(burstCell)
                && producerPublished
                && fixtureProducerFacility.BuildingData == producerAsset,
            "PAIRED_FIXTURE_PRODUCTION_CELL",
            $"cell={burstCell};asset={producerAsset.id};"
            + $"facility={producerFacilityId};published={producerPublished};cook="
            + producerAsset.Facility.SupportsWork(BuiltInWorkTypeIds.Cook));
        if (!grid.IsValidGridPos(burstCell)
            || !producerPublished
            || fixtureProducerFacility.BuildingData != producerAsset)
            yield break;

        yield return PrepareCropAndMiningFixtures(reachable, placement);
        if (failures.Count > 0)
            yield break;

        string[] burstItemIds =
        {
            FacilityBurstItemId,
            cropBurstItemId,
            miningBurstItemId
        };
        List<string> overflowAdmission = new(burstItemIds.Length);
        bool overflowAcceptsAllBursts = true;
        foreach (string burstItemId in burstItemIds)
        {
            bool defined = itemCatalog.TryGetDefinition(
                burstItemId,
                out DungeonItemDefinition definition);
            bool accepts = defined
                && fixtureOverflowWarehouse.Inventory.Accepts(
                    definition.StockCategory)
                && fixtureOverflowWarehouse.Inventory.GetAcceptableQuantity(
                    burstItemId,
                    1) == 1;
            overflowAcceptsAllBursts &= accepts;
            overflowAdmission.Add(
                $"{burstItemId}:{(defined ? definition.StockCategory.ToString() : "missing")}:{accepts}");
        }
        Check(overflowAcceptsAllBursts,
            "PAIRED_OVERFLOW_ACCEPTS_ALL_BURST_CATEGORIES",
            $"warehouse={overflowWarehouseId};asset={overflowWarehouseAsset.id};"
            + $"maxMassGrams={fixtureOverflowWarehouse.Inventory.MaxMassGrams};"
            + string.Join(",", overflowAdmission));
        if (!overflowAcceptsAllBursts)
            yield break;

        bool published = world.Warehouses.Any(value => value != null
            && value.PersistentInstanceId.Value == warehouseId);
        string overflowId = overflowWarehouseId;
        bool overflowPublished = world.Warehouses.Any(value => value != null
            && value.PersistentInstanceId.Value == overflowId);
        IWarehouseFacility productionInputWarehouse = world.Warehouses
            .Where(value => value?.Inventory != null
                && value.PersistentInstanceId.Value != warehouseId
                && value.PersistentInstanceId.Value != overflowWarehouseId
                && value.Inventory.Accepts(StockCategory.Food)
                && value.Inventory.RemainingMassGrams > 0L)
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        productionInputWarehouseId =
            productionInputWarehouse?.PersistentInstanceId.Value ?? string.Empty;
        Check(published && overflowPublished
                && !string.IsNullOrWhiteSpace(productionInputWarehouseId)
                && fixtureWarehouse.Inventory?.MaxMassGrams > 0L
                && fixtureOverflowWarehouse.Inventory?.MaxMassGrams > 0L,
            "PAIRED_WAREHOUSE_LIVE",
            $"id={warehouseId};published={published};capacityGrams={fixtureWarehouse.Inventory?.MaxMassGrams ?? -1L};"
            + $"overflowId={overflowId};overflowPublished={overflowPublished};"
            + $"overflowCapacityGrams={fixtureOverflowWarehouse.Inventory?.MaxMassGrams ?? -1L};"
            + $"producerAsset={producerAsset.id};producerCell={burstCell};"
            + $"inputWarehouse={productionInputWarehouseId}");
        if (!published || !overflowPublished
            || string.IsNullOrWhiteSpace(productionInputWarehouseId)
            || fixtureWarehouse.Inventory?.MaxMassGrams <= 0L
            || fixtureOverflowWarehouse.Inventory?.MaxMassGrams <= 0L)
            yield break;
        bool anyPlan = false;
        List<string> planDetails = new();
        foreach (CharacterActor actor in LiveActors().OrderBy(ActorId, StringComparer.Ordinal))
        {
            bool preview = haulPlanning.TryPreviewBestPlan(
                actor, out WorldItemHaulPlan plan, out string reason);
            anyPlan |= preview;
            planDetails.Add($"{ActorId(actor)}:{preview}:{plan?.PrimaryDestinationId}:{reason}");
        }
        Check(anyPlan, "PAIRED_INITIAL_HAUL_PLAN",
            string.Join(";", planDetails));
        if (!anyPlan)
            yield break;

        QuiesceActorsForCheckpoint();
        IsolatePreexistingLogistics();
        for (int frame = 0; frame < 2; frame++)
            yield return null;
        if (!DungeonSpaceGridLayout.TryCapture(
                grid,
                out DungeonInteriorLayoutSnapshot interior,
                out string interiorFailure))
        {
            Check(false, "PAIRED_FLOOR_CLUTTER_LAYOUT_CAPTURE",
                interiorFailure);
            yield break;
        }
        HashSet<Vector2Int> emergencyEgress = new()
        {
            interior.EntrancePosition
        };
        Vector2Int entranceLanding = interior.EntrancePosition + Vector2Int.right;
        if (grid.IsValidGridPos(entranceLanding) && grid.IsWalkable(entranceLanding))
            emergencyEgress.Add(entranceLanding);

        HashSet<Vector2Int> operationalAccess = new();
        HashSet<Vector2Int> criticalAccess = new();
        foreach (BuildableObject building in world.Buildings
                     .OfType<BuildableObject>()
                     .Where(value => value != null && value.BuildingData != null)
                     .OrderBy(value => value.PersistentInstanceId.Value,
                         StringComparer.Ordinal))
        {
            Vector2Int[] candidates = BuildingWorkAccessRules
                .EnumerateCandidates(
                    building.buildPoses,
                    building.BuildingData.IsGridMovement)
                .Where(value => grid.IsValidGridPos(value)
                    && grid.IsWalkable(value))
                .Distinct()
                .OrderBy(value => value.x)
                .ThenBy(value => value.y)
                .ToArray();
            foreach (Vector2Int candidate in candidates)
            {
                operationalAccess.Add(candidate);
            }
            if (candidates.Length == 1 && IsCriticalServiceFacility(building))
                criticalAccess.Add(candidates[0]);
        }
        HashSet<Vector2Int> protectedCells = new(emergencyEgress);
        protectedCells.UnionWith(criticalAccess);
        Check(emergencyEgress.Count > 0
                && operationalAccess.Count > 0
                && criticalAccess.Count > 0,
            "PAIRED_FLOOR_CLUTTER_PROTECTED_LAYOUT",
            $"egress={emergencyEgress.Count};operationalAccess={operationalAccess.Count};"
            + $"criticalAccess={criticalAccess.Count};"
            + $"entrance={interior.EntrancePosition}");
        if (emergencyEgress.Count == 0
            || operationalAccess.Count == 0
            || criticalAccess.Count == 0)
            yield break;
        bool burstSourcesProtected = protectedCells.Contains(burstCell)
            || protectedCells.Contains(cropBurstCell)
            || protectedCells.Contains(miningBurstCell);
        Check(!burstSourcesProtected,
            "PAIRED_BURST_SOURCES_OUTSIDE_PROTECTED_CELLS",
            $"facility={burstCell};crop={cropBurstCell};mining={miningBurstCell}");
        if (burstSourcesProtected)
            yield break;

        HashSet<Vector2Int> authorized = items.GetAllStacks()
            .Where(value => value != null && value.Quantity > 0)
            .Select(value => value.Position)
            .Where(value => !protectedCells.Contains(value))
            .ToHashSet();
        List<KeyValuePair<Vector2Int, SpatialCellRole>> roles = authorized
            .Select(value => new KeyValuePair<Vector2Int, SpatialCellRole>(
                value, SpatialCellRole.AuthorizedLooseSource))
            .ToList();
        foreach (GridCell dropZone in grid.GetCells()
                     .Where(value => value != null
                         && value.AreaType == GridCellAreaType.DropZone))
        {
            if (!protectedCells.Contains(dropZone.Position))
            {
                roles.Add(new KeyValuePair<Vector2Int, SpatialCellRole>(
                    dropZone.Position, SpatialCellRole.AuthorizedLooseSource));
            }
        }
        if (dropZones.TryGetDeliveryDropoff(out Vector2Int deliveryDropoff)
            && !protectedCells.Contains(deliveryDropoff))
        {
            roles.Add(new KeyValuePair<Vector2Int, SpatialCellRole>(
                deliveryDropoff, SpatialCellRole.AuthorizedLooseSource));
        }
        foreach (IWarehouseFacility warehouse in world.Warehouses
                     .Where(value => value != null)
                     .OrderBy(value => value.PersistentInstanceId.Value,
                         StringComparer.Ordinal))
        {
            if (warehouse is not BuildableObject building)
                continue;
            foreach (Vector2Int cell in building.buildPoses)
            {
                roles.Add(new KeyValuePair<Vector2Int, SpatialCellRole>(
                    cell, SpatialCellRole.StorageBuffer));
            }
        }
        if (fixtureOverflowWarehouse is BuildableObject overflowBuilding)
        {
            foreach (Vector2Int cell in overflowBuilding.buildPoses
                         .OrderBy(value => value.x)
                         .ThenBy(value => value.y))
            {
                roles.Add(new KeyValuePair<Vector2Int, SpatialCellRole>(
                    cell, SpatialCellRole.OverflowContainment));
            }
        }
        roles.Add(new KeyValuePair<Vector2Int, SpatialCellRole>(
            burstCell, SpatialCellRole.AuthorizedLooseSource));
        roles.Add(new KeyValuePair<Vector2Int, SpatialCellRole>(
            cropBurstCell, SpatialCellRole.AuthorizedLooseSource));
        roles.Add(new KeyValuePair<Vector2Int, SpatialCellRole>(
            miningBurstCell, SpatialCellRole.AuthorizedLooseSource));
        foreach (Vector2Int cell in operationalAccess
                     .OrderBy(value => value.x)
                     .ThenBy(value => value.y))
        {
            roles.Add(new KeyValuePair<Vector2Int, SpatialCellRole>(
                cell,
                SpatialCellRole.OperationalAccess
                | SpatialCellRole.SharedCorridor));
        }
        foreach (Vector2Int cell in emergencyEgress
                     .OrderBy(value => value.x)
                     .ThenBy(value => value.y))
        {
            roles.Add(new KeyValuePair<Vector2Int, SpatialCellRole>(
                cell,
                SpatialCellRole.EmergencyEgress
                | SpatialCellRole.SharedCorridor));
        }
        layout = new DungeonSpaceLayoutSnapshot(
            roles,
            criticalAccess,
            cleanRunP95HaulDispatchAndDeliverySeconds: 15f,
            gameDaySeconds: GameDaySeconds);
        if (!VerifyFloorClutterProtectedCellContract(
                emergencyEgress,
                operationalAccess,
                criticalAccess))
        {
            yield break;
        }
        commonCheckpointJson = saves.ToJson(saves.Capture());
        commonCheckpointTime = clock.Time;
        commonCheckpointFrame = clock.FrameCount;
        Check(!string.IsNullOrWhiteSpace(commonCheckpointJson),
            "PAIRED_COMMON_CHECKPOINT", $"bytes={commonCheckpointJson.Length}");
    }

    private bool VerifyFloorClutterProtectedCellContract(
        IReadOnlyCollection<Vector2Int> emergencyEgress,
        IReadOnlyCollection<Vector2Int> operationalAccess,
        IReadOnlyCollection<Vector2Int> criticalAccess)
    {
        Vector2Int? protectedProbeCell = emergencyEgress
            .Concat(criticalAccess)
            .Distinct()
            .OrderBy(value => value.x)
            .ThenBy(value => value.y)
            .Where(value => (layout.GetRoles(value) & (
                SpatialCellRole.StorageBuffer
                | SpatialCellRole.OverflowContainment
                | SpatialCellRole.AuthorizedLooseSource)) == 0)
            .Where(value => items.GetStacksAt(value, includeStored: true).Count == 0)
            .Select(value => (Vector2Int?)value)
            .FirstOrDefault();
        Check(protectedProbeCell.HasValue,
            "PAIRED_FLOOR_CLUTTER_PROTECTED_PROBE_CELL",
            $"egress={emergencyEgress.Count};operationalAccess={operationalAccess.Count};"
            + $"criticalAccess={criticalAccess.Count}");
        if (!protectedProbeCell.HasValue)
            return false;

        if (!TryProbeFloorClutterCell(
                protectedProbeCell.Value,
                expectImmediateFailure: true,
                "PAIRED_FLOOR_CLUTTER_PROTECTED_CELL_POSITIVE"))
        {
            return false;
        }
        if (!TryProbeFloorClutterCell(
                burstCell,
                expectImmediateFailure: false,
                "PAIRED_FLOOR_CLUTTER_AUTHORIZED_SOURCE_NEGATIVE"))
        {
            return false;
        }

        FloorClutterAssessment cleanup = clutter.Capture(grid, layout, 0f);
        Check(cleanup.OutsideContainment.Count == 0,
            "PAIRED_FLOOR_CLUTTER_PROBE_CLEANUP",
            $"outside={cleanup.OutsideContainment.Count}");
        return cleanup.OutsideContainment.Count == 0;
    }

    private static bool IsCriticalServiceFacility(BuildableObject building)
    {
        FacilityData facility = building?.BuildingData?.Facility;
        return facility != null
            && (facility.SupportsWork(BuiltInWorkTypeIds.DrawWater)
                || facility.SupportsWork(BuiltInWorkTypeIds.Cook)
                || facility.SupportsWork(BuiltInWorkTypeIds.Rest)
                || facility.SupportsWork(BuiltInWorkTypeIds.Treat)
                || facility.SupportsWork(BuiltInWorkTypeIds.Surgery));
    }

    private bool TryProbeFloorClutterCell(
        Vector2Int cell,
        bool expectImmediateFailure,
        string evidenceToken)
    {
        if (items.GetStacksAt(cell, includeStored: true).Count != 0)
        {
            Check(false, evidenceToken, $"cell={cell};occupied-before-spawn");
            return false;
        }
        bool spawned = items.SpawnItemAt(
            FacilityBurstItemId,
            1,
            cell,
            WorldItemStackState.Loose,
            string.Empty,
            out int spawnedQuantity);
        WorldItemStackSnapshot probe = items.GetStacksAt(cell, includeStored: true)
            .SingleOrDefault(value => value != null
                && value.State == WorldItemStackState.Loose
                && string.Equals(
                    value.ItemId,
                    FacilityBurstItemId,
                    StringComparison.Ordinal));
        if (!spawned || spawnedQuantity != 1 || probe == null)
        {
            Check(false, evidenceToken,
                $"cell={cell};spawned={spawned};quantity={spawnedQuantity};"
                + $"probe={(probe == null ? "missing" : probe.StackId)}");
            if (probe != null)
                items.DeleteStack(probe.StackId);
            return false;
        }

        FloorClutterAssessment assessment = clutter.Capture(grid, layout, 0f);
        FloorClutterStackAssessment row = assessment.OutsideContainment
            .SingleOrDefault(value => string.Equals(
                value.StackId,
                probe.StackId,
                StringComparison.Ordinal));
        bool detected = expectImmediateFailure
            ? row != null && row.ImmediateFailure && row.Persistent
            : row == null && assessment.ImmediateFailureCount == 0;
        bool deleted = items.DeleteStack(probe.StackId);
        Check(detected && deleted,
            evidenceToken,
            $"cell={cell};roles={layout.GetRoles(cell)};"
            + $"expectImmediate={expectImmediateFailure};"
            + $"detected={(row != null)};"
            + $"immediate={row?.ImmediateFailure ?? false};"
            + $"persistent={row?.Persistent ?? false};deleted={deleted}");
        return detected && deleted;
    }

    private IEnumerator PrepareCropAndMiningFixtures(
        IReadOnlyList<Vector2Int> reachable,
        BuildingPlacementValidator placement)
    {
        SetPhase("prepare-production-burst-authorities");
        BlueprintResearchRuntime research = progression.BlueprintResearch;
        research.State.Projects.Complete(
            new ResearchProjectId("research:agriculture:field"));
        research.State.Projects.Complete(
            new ResearchProjectId("research:agriculture:gathering"));
        research.State.Projects.Complete(
            new ResearchProjectId("research:mining:surface"));

        BuildingSO cropAsset = FindCropPlotAsset();
        bool cropBuildingUnlocked = cropAsset != null
            && research.State.UnlockBuilding(cropAsset.id);
        Check(cropAsset != null
                && (cropBuildingUnlocked
                    || research.State.IsBuildingUnlocked(cropAsset.id)),
            "PAIRED_CROP_BUILDING_RESEARCH_UNLOCKED",
            $"asset={cropAsset?.name ?? "missing"};"
            + $"buildingId={cropAsset?.id ?? -1};"
            + $"unlocked={cropAsset != null && research.State.IsBuildingUnlocked(cropAsset.id)}");
        HashSet<Vector2Int> reservedProducerCells = producerFacilityAsset
            .GetGridPosList(burstCell)
            .ToHashSet();
        List<Vector2Int> legalCropAnchors = new();
        Dictionary<string, int> cropPlacementFailures =
            new(StringComparer.Ordinal);
        int cropCandidates = 0;
        if (cropAsset != null)
        {
            foreach (Vector2Int candidate in reachable)
            {
                if (cropAsset.GetGridPosList(candidate)
                    .Any(cell => reservedProducerCells.Contains(cell)))
                    continue;
                cropCandidates++;
                if (placement.CanBuild(
                        grid, cropAsset, candidate, out string placementFailure))
                {
                    legalCropAnchors.Add(candidate);
                    continue;
                }

                string reason = string.IsNullOrWhiteSpace(placementFailure)
                    ? "unspecified"
                    : placementFailure.Trim();
                cropPlacementFailures[reason] =
                    cropPlacementFailures.TryGetValue(reason, out int count)
                        ? count + 1
                        : 1;
            }
        }
        Vector2Int? cropAnchor = legalCropAnchors
            .OrderByDescending(value => Mathf.Min(
                Manhattan(value, burstCell),
                Manhattan(value, overflowCell)))
            .ThenBy(value => value.x)
            .ThenBy(value => value.y)
            .Select(value => (Vector2Int?)value)
            .FirstOrDefault();
        string cropPlacementDetail = string.Join(
            "|",
            cropPlacementFailures
                .OrderBy(value => value.Key, StringComparer.Ordinal)
                .Select(value => $"{value.Key}:{value.Value}"));
        Check(cropAsset != null && cropAnchor.HasValue,
            "PAIRED_CROP_PLOT_PLACEMENT",
            $"asset={cropAsset?.name ?? "missing"};anchor={cropAnchor};"
            + $"reachable={reachable.Count};candidates={cropCandidates};"
            + $"legal={legalCropAnchors.Count};rejections={cropPlacementDetail}");
        if (cropAsset == null || !cropAnchor.HasValue)
            yield break;

        bool cropPlotPlaced = livePlacementService.TryPlaceBuildingImmediateUnchecked(
            cropAsset,
            cropAnchor.Value,
            chargeCost: false,
            out string cropPlacementFailure);
        Check(cropPlotPlaced,
            "PAIRED_CROP_PLOT_PRODUCTION_PLACEMENT",
            $"asset={cropAsset.name};anchor={cropAnchor.Value};"
            + $"failure={cropPlacementFailure}");
        if (!cropPlotPlaced)
            yield break;
        for (int frame = 0; frame < 2; frame++)
            yield return null;
        fixtureCropPlot = world.Buildings
            .OfType<Facility>()
            .SingleOrDefault(value => value != null
                && value.BuildingData == cropAsset
                && value.centerPos == cropAnchor.Value);
        Check(fixtureCropPlot != null,
            "PAIRED_CROP_PLOT_LIVE_REGISTRATION",
            $"asset={cropAsset.name};anchor={cropAnchor.Value};"
            + $"registered={fixtureCropPlot != null}");
        if (fixtureCropPlot == null)
            yield break;
        cropPlotId = fixtureCropPlot.RequirePersistentInstanceId().Value;
        cropBurstCell = fixtureCropPlot.centerPos;
        for (int frame = 0; frame < 4; frame++)
            yield return null;

        cropPlots.Restore(cropPlots.BuildRestore(cropPlots.Capture()));
        bool cropSelected = cropPlots.TrySetCrop(
            fixtureCropPlot, CropId, out string cropMessage);
        cropPlots.Tick();
        CropPlotSnapshot waiting = cropPlots.Plots.FirstOrDefault(value =>
            string.Equals(value.PlotId, cropPlotId, StringComparison.Ordinal));
        Check(cropSelected && waiting != null
                && waiting.Phase == CropPlotPhase.WaitingForMaterials
                && waiting.RequiredMaterials.Count > 0,
            "PAIRED_CROP_CYCLE_SELECTED",
            $"selected={cropSelected};message={cropMessage};plot={cropPlotId};"
            + $"phase={waiting?.Phase};materials={waiting?.RequiredMaterials.Count ?? 0}");
        if (!cropSelected || waiting == null
            || waiting.Phase != CropPlotPhase.WaitingForMaterials
            || waiting.RequiredMaterials.Count == 0)
            yield break;

        bool cropDefined = economyCatalog.TryGetCrop(
            CropId, out CropDefinitionSO crop);
        SeedLotState fixtureSeedLot = cropDefined
            ? FindSeedLot(crop.SeedItemId, CropId)
            : null;
        Check(cropDefined && fixtureSeedLot != null,
            "PAIRED_CROP_SEED_LOT_AUTHORITY",
            $"crop={CropId};seedItem={crop?.SeedItemId ?? "missing"};"
            + $"seedCrop={fixtureSeedLot?.cropId ?? "missing"};"
            + $"genome={fixtureSeedLot?.cultivarGenomeId ?? "missing"}");
        if (!cropDefined || fixtureSeedLot == null)
            yield break;

        int releasedRequests = itemTransfers.ReleaseDestination(
            waiting.MaterialDestinationId,
            fixtureCropPlot.centerPos);
        foreach (KeyValuePair<string, int> material in waiting.RequiredMaterials
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            bool isSeedLot = string.Equals(
                material.Key,
                crop.SeedItemId,
                StringComparison.Ordinal);
            int spawnedQuantity;
            bool spawned = isSeedLot
                ? itemTransfers.TrySpawnItemWithComponents(
                    material.Key,
                    material.Value,
                    fixtureCropPlot.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    waiting.MaterialDestinationId,
                    new[] { SeedLotItemStateCodec.Encode(fixtureSeedLot) },
                    out spawnedQuantity)
                : items.SpawnItemAt(
                    material.Key,
                    material.Value,
                    fixtureCropPlot.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    waiting.MaterialDestinationId,
                    out spawnedQuantity);
            Check(spawned && spawnedQuantity == material.Value,
                "PAIRED_CROP_INPUT_PHYSICAL",
                $"item={material.Key};required={material.Value};"
                + $"spawned={spawnedQuantity};seedLot={isSeedLot};"
                + $"releasedRequests={releasedRequests}");
            if (!spawned || spawnedQuantity != material.Value)
                yield break;
        }

        cropPlots.Tick();
        bool sowAvailable = cropPlots.TryGetWork(
            fixtureCropPlot,
            BuiltInWorkTypeIds.Sow,
            out CropPlotWorkSnapshot sow) && sow.Available;
        bool sowed = sowAvailable && cropPlots.ApplyWork(
            fixtureCropPlot,
            BuiltInWorkTypeIds.Sow,
            sow.RequiredWork,
            out bool sowCompleted) && sowCompleted;
        Check(sowed, "PAIRED_CROP_SOW_PRODUCTION_COMMAND",
            $"available={sowAvailable};reason={sow.UnavailableReason};completed={sowed}");
        if (!sowed)
            yield break;

        DungeonCropPlotSaveData growingSave = cropPlots.Capture();
        CropPlotSaveData growing = growingSave.plots.FirstOrDefault(value =>
            string.Equals(value.buildingInstanceId, cropPlotId, StringComparison.Ordinal));
        Check(growing != null && growing.phase == CropPlotPhase.Growing,
            "PAIRED_CROP_GROWING_AUTHORITY",
            $"plot={cropPlotId};phase={growing?.phase}");
        if (growing == null || growing.phase != CropPlotPhase.Growing)
            yield break;
        growing.growthHours = crop.GrowthHours;
        cropPlots.Restore(cropPlots.BuildRestore(growingSave));
        cropPlots.Tick();

        bool harvestReady = cropPlots.TryGetWork(
            ResolveCropPlot(),
            BuiltInWorkTypeIds.Harvest,
            out CropPlotWorkSnapshot harvest) && harvest.Available;
        cropBurstItemId = crop?.HarvestItemId ?? string.Empty;
        Check(harvestReady && cropDefined
                && !string.IsNullOrWhiteSpace(cropBurstItemId),
            "PAIRED_CROP_HARVEST_READY_CHECKPOINT",
            $"ready={harvestReady};reason={harvest.UnavailableReason};"
            + $"cropDefined={cropDefined};item={cropBurstItemId};cell={cropBurstCell}");
        if (!harvestReady || !cropDefined
            || string.IsNullOrWhiteSpace(cropBurstItemId))
            yield break;

        HashSet<Vector2Int> reachablePositions = reachable.ToHashSet();
        var miningCandidate = worldResources.Nodes
            .Where(value => value != null)
            .Select(value =>
            {
                bool available = worldResources.TryGetWork(
                    value,
                    BuiltInWorkTypeIds.Quarry,
                    out WorldResourceWorkSnapshot snapshot) && snapshot.Available;
                return new
                {
                    Node = value,
                    Host = value.GetComponent<BuildableObject>(),
                    Snapshot = snapshot,
                    Available = available
                };
            })
            .Where(value => value.Available
                && value.Host != null
                && HasReachablePickupStand(
                    value.Host.centerPos,
                    reachablePositions))
            .OrderBy(value => value.Snapshot.RecipeId, StringComparer.Ordinal)
            .ThenBy(value => value.Node.NodeId, StringComparer.Ordinal)
            .FirstOrDefault();
        WorldResourceNode miningNode = miningCandidate?.Node;
        BuildableObject miningHost = miningCandidate?.Host;
        miningRecipeId = miningCandidate?.Snapshot.RecipeId ?? string.Empty;
        bool miningRecipeDefined = economyCatalog.TryGetRecipe(
            miningRecipeId, out ProductionRecipeSO miningRecipe);
        ProductionOutputDefinition deterministicMiningOutput = miningRecipe?.Outputs
            .Where(value => value != null
                && value.Probability >= 1f
                && value.Amount > 0)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .FirstOrDefault();
        miningNodeId = miningNode?.NodeId ?? string.Empty;
        miningBurstItemId = deterministicMiningOutput?.ItemId ?? string.Empty;
        miningBurstCell = miningHost?.centerPos ?? default;
        Check(miningNode != null && miningHost != null && miningRecipeDefined
                && deterministicMiningOutput != null
                && !string.IsNullOrWhiteSpace(miningBurstItemId),
            "PAIRED_MINING_BURST_READY_CHECKPOINT",
            $"node={miningNodeId};host={miningHost != null};recipe={miningRecipeId}:"
            + $"{miningRecipeDefined};"
            + $"item={miningBurstItemId};cell={miningBurstCell};candidates="
            + string.Join(",", worldResources.Nodes
                .Where(value => value != null)
                .Select(value => value.GetComponent<BuildableObject>())
                .Where(value => value != null)
                .OrderBy(value => value.centerPos.x)
                .ThenBy(value => value.centerPos.y)
                .Select(value => value.centerPos + ":"
                    + HasReachablePickupStand(value.centerPos, reachablePositions))));
    }

    private static bool HasReachablePickupStand(
        Vector2Int itemPosition,
        ISet<Vector2Int> reachable)
    {
        return reachable != null
            && (reachable.Contains(itemPosition)
                || reachable.Contains(itemPosition + Vector2Int.left)
                || reachable.Contains(itemPosition + Vector2Int.right));
    }

    private void IsolatePreexistingLogistics()
    {
        SetPhase("isolate-preexisting-logistics");
        WorldItemStackSnapshot[] isolated = items.GetAllStacks()
            .Where(value => value != null
                && value.Quantity > 0
                && !value.Forbidden
                && (value.State is WorldItemStackState.Loose
                        or WorldItemStackState.FacilityOutputBuffer
                    || value.State == WorldItemStackState.Stored
                        && value.HasDestinationPosition
                        && !string.IsNullOrWhiteSpace(value.DestinationId)
                        && !string.IsNullOrWhiteSpace(
                            value.SourceStorageDestinationId)))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        int clearedReservations = 0;
        int forbidden = 0;
        foreach (WorldItemStackSnapshot stack in isolated)
        {
            if (stack.ReservedQuantity > 0 && items.TryClearReservation(stack.StackId))
                clearedReservations++;
            if (items.SetForbidden(stack.StackId, true))
                forbidden++;
        }
        int remainingReservations = items.GetAllStacks().Sum(value =>
            value?.ReservedQuantity ?? 0);
        Check(forbidden == isolated.Length && remainingReservations == 0,
            "PAIRED_PREEXISTING_LOGISTICS_ISOLATED",
            $"candidates={isolated.Length};forbidden={forbidden};"
            + $"clearedReservations={clearedReservations};"
            + $"remainingReservations={remainingReservations}");
    }

    private IEnumerator RunSeed(int seed)
    {
        SetPhase($"seed-{seed}-checkpoint");
        yield return Restore(
            commonCheckpointJson,
            commonCheckpointTime,
            commonCheckpointFrame);
        if (failures.Count > 0)
            yield break;
        randomProvider.Reseed(seed);
        string seedCheckpoint = saves.ToJson(saves.Capture());
        float seedCheckpointTime = clock.Time;
        int seedCheckpointFrame = clock.FrameCount;
        foreach (string arm in new[]
                 {
                     "cleanRepeatA", "cleanRepeatB", "faultControl", "clutterStress"
                 })
        {
            int failuresBeforeRestore = failures.Count;
            yield return Restore(
                seedCheckpoint,
                seedCheckpointTime,
                seedCheckpointFrame);
            if (failures.Count > failuresBeforeRestore)
                yield break;
            string startKey = $"{seed}|{arm}";
            armStartRandomHashes[startKey] = CaptureRandomHash(
                randomDiagnostics.Capture());
            string startSemanticText = CaptureSemanticText();
            armStartSemanticTexts[startKey] = startSemanticText;
            armStartSemanticHashes[startKey] = HashText(startSemanticText);
            int failuresBeforeArm = failures.Count;
            yield return RunArm(seed, arm);
            if (failures.Count > failuresBeforeArm)
                yield break;
        }
        ValidateExogenousEventsExact(seed);
        if (failures.Count > 0)
            yield break;
        ValidateCausalCone(seed);
    }

    private IEnumerator RunArm(int seed, string arm)
    {
        currentBurstProbe = null;
        SetPhase($"seed-{seed}-{arm}-warmup");
        Time.timeScale = VerificationTimeScale;
        yield return ObserveDuration(seed, arm, -1, WarmupSeconds, false);
        if (failures.Count > 0)
            yield break;

        bool faultArm = arm is "faultControl" or "clutterStress";
        SetPhase($"seed-{seed}-{arm}-measurement-setup");
        PrepareActorsForArmMeasurementBoundary();
        if (failures.Count > 0)
            yield break;
        if (!faultArm)
            ResumeAllMeasuredActors();
        string eventHash = $"clean:{seed}:none";
        if (faultArm)
        {
            SetPhase($"seed-{seed}-{arm}-fault-setup");
            IWarehouseFacility warehouse = ResolveWarehouse();
            CharacterActor faultActor = ResolveActor(faultActorId);
            CounterfactualRandomKey key = new(
                seed, ScenarioId, "haul-burst-and-downed", faultActorId, 0, 0);
            DeterministicRandomSequence sequence = key.CreateSequence();
            BurstProducerKind producerKind = SelectBurstProducer(seed);
            int burstQuantity = producerKind == BurstProducerKind.FacilityOutput
                ? 6 + sequence.NextInt(0, 3)
                : 0;
            eventHash = HashText(
                $"{seed}|{ScenarioId}|{faultActorId}|{producerKind}|"
                + $"{burstQuantity}|{sequence.State}");
            Vector2Int interventionSourceCell = producerKind switch
            {
                BurstProducerKind.CropHarvest => cropBurstCell,
                BurstProducerKind.Mining => miningBurstCell,
                _ => burstCell
            };
            HashSet<string> interventionStackIdsBefore = items.GetAllStacks()
                .Where(value => value != null
                    && value.Quantity > 0
                    && value.Position == interventionSourceCell)
                .Select(value => value.StackId)
                .ToHashSet(StringComparer.Ordinal);
            if (arm == "clutterStress")
            {
                DungeonItemDefinition fillDefinition = itemCatalog.All
                    .Where(candidate => candidate != null
                        && candidate.StockCategory == StockCategory.General
                        && candidate.MaxStack > 1)
                    .OrderBy(candidate => candidate.ItemId, StringComparer.Ordinal)
                    .FirstOrDefault()
                    ?? throw new InvalidOperationException(
                        "Paired clutter requires one stackable General item.");
                long unitMassGrams = warehouseMassQuery
                    .GetDefinitionUnitMassGrams(fillDefinition.ItemId);
                long targetMassGrams = warehouse.Inventory.MaxMassGrams * 9L / 10L;
                long missingMassGrams = Math.Max(
                    0L,
                    targetMassGrams - warehouse.Inventory.StoredMassGrams);
                int missingQuantity = missingMassGrams == 0L
                    ? 0
                    : checked((int)((missingMassGrams + unitMassGrams - 1L)
                        / unitMassGrams));
                if (missingQuantity > 0)
                {
                    bool filled = items.SpawnStockInWarehouse(
                        warehouse,
                        StockCategory.General,
                        missingQuantity,
                        out int spawned);
                    long storedMassGrams = warehouse.Inventory.StoredMassGrams;
                    Check(filled
                            && spawned == missingQuantity
                            && storedMassGrams >= targetMassGrams
                            && storedMassGrams <= warehouse.Inventory.MaxMassGrams,
                        "PAIRED_STORAGE_NINETY_PERCENT",
                        $"seed={seed};targetMassGrams={targetMassGrams};"
                        + $"unitMassGrams={unitMassGrams};"
                        + $"requested={missingQuantity};spawned={spawned};"
                        + $"storedMassGrams={storedMassGrams};"
                        + $"maxMassGrams={warehouse.Inventory.MaxMassGrams};"
                        + $"totalQuantity={warehouse.Inventory.TotalStock}");
                }
            }

            if (producerKind == BurstProducerKind.FacilityOutput)
            {
            fixtureProducerFacility = ResolveProducerFacility() as Facility;
            bool producerPublished = world.Buildings.Any(value => value != null
                && value.PersistentInstanceId.Value == producerFacilityId);
            bool producerAuthorityExact = fixtureProducerFacility != null
                && fixtureProducerFacility.BuildingData == producerFacilityAsset
                && fixtureProducerFacility.centerPos == burstCell;
            Check(producerAuthorityExact && producerPublished,
                "PAIRED_INTERVENTION_PRODUCER_PUBLISHED",
                $"seed={seed};arm={arm};facility={producerFacilityId};cell={burstCell};"
                + $"asset={fixtureProducerFacility?.BuildingData?.id ?? -1};"
                + $"expectedAsset={producerFacilityAsset?.id ?? -1}");
            if (!producerAuthorityExact || !producerPublished)
                yield break;

            IWarehouseFacility inputWarehouse = ResolveProductionInputWarehouse();
            Check(inputWarehouse?.Inventory != null
                    && inputWarehouse.Inventory.RemainingMassGrams > 0L,
                "PAIRED_PRODUCTION_INPUT_CAPACITY",
                $"seed={seed};arm={arm};quantity={burstQuantity};"
                + $"warehouse={productionInputWarehouseId};stock={inputWarehouse?.Inventory?.TotalStock ?? -1}");
            if (inputWarehouse?.Inventory == null
                || inputWarehouse.Inventory.RemainingMassGrams <= 0L)
                yield break;
            bool inputSeeded = items.SpawnStockInWarehouse(
                inputWarehouse,
                StockCategory.Food,
                burstQuantity,
                out int seededInput);
            Check(inputSeeded && seededInput == burstQuantity,
                "PAIRED_PRODUCTION_INPUT_PHYSICAL",
                $"seed={seed};arm={arm};requested={burstQuantity};seeded={seededInput}");
            if (!inputSeeded || seededInput != burstQuantity)
                yield break;

            for (int publicationFrame = 0; publicationFrame < 2; publicationFrame++)
                yield return null;
            BuildableObject producer = ResolveProducerFacility();
            bool productionReady = survivalFoodQuery.HasSurvivalWorkAvailable(
                producer,
                BuiltInWorkTypeIds.Cook);
            Check(productionReady,
                "PAIRED_PRODUCTION_INPUT_PUBLISHED",
                $"seed={seed};arm={arm};producer={producerFacilityId};"
                + $"inputWarehouse={productionInputWarehouseId};quantity={burstQuantity}");
            if (!productionReady)
                yield break;

            currentBurstProbe = new ArmBurstProbe(
                BurstProducerKind.FacilityOutput,
                FacilityBurstItemId,
                burstCell,
                burstQuantity,
                CountItemQuantity(FacilityBurstItemId),
                CountStoredItemQuantity(FacilityBurstItemId),
                CountCarriedItemQuantity(FacilityBurstItemId));
            int produced = 0;
            DomainFailure productionFailure = default;
            for (int unit = 0; unit < burstQuantity; unit++)
            {
                if (!survivalFood.TryApplySurvivalWork(
                        faultActor.BuildingVisitor,
                        producer,
                        BuiltInWorkTypeIds.Cook,
                        out int cooked,
                        out productionFailure))
                    break;
                produced = checked(produced + cooked);
            }
            int looseProduced = items.GetAllStacks()
                .Where(value => value != null
                    && value.Position == burstCell
                    && value.State == WorldItemStackState.Loose
                    && string.Equals(value.ItemId, FacilityBurstItemId, StringComparison.Ordinal))
                .Sum(value => value.Quantity);
            bool productionBurstExact = produced == burstQuantity
                    && CountItemQuantity(FacilityBurstItemId) - currentBurstProbe.TotalBefore
                        == burstQuantity
                    && looseProduced >= burstQuantity;
            Check(productionBurstExact,
                "PAIRED_KEYED_PRODUCTION_BURST_APPLIED",
                $"seed={seed};arm={arm};requested={burstQuantity};produced={produced};"
                + $"looseAtProducer={looseProduced};cell={burstCell};"
                + $"failure={productionFailure.Code}:"
                + string.Join(",", productionFailure.Parameters.ToArray()));
            if (!productionBurstExact)
                yield break;
            productionBurstArmCount++;
            facilityBurstArmCount++;
            }
            else
            {
                string itemId = producerKind == BurstProducerKind.CropHarvest
                    ? cropBurstItemId
                    : miningBurstItemId;
                Vector2Int sourceCell = producerKind == BurstProducerKind.CropHarvest
                    ? cropBurstCell
                    : miningBurstCell;
                int totalBefore = CountItemQuantity(itemId);
                int storedBefore = CountStoredItemQuantity(itemId);
                int carriedBefore = CountCarriedItemQuantity(itemId);
                int sourceLooseBefore = CountLooseAt(itemId, sourceCell);
                bool commandApplied;
                bool cycleCompleted = false;
                string commandDetail;
                if (producerKind == BurstProducerKind.CropHarvest)
                {
                    BuildableObject plot = ResolveCropPlot();
                    CropPlotWorkSnapshot harvest = default;
                    bool available = plot != null && cropPlots.TryGetWork(
                        plot,
                        BuiltInWorkTypeIds.Harvest,
                        out harvest) && harvest.Available;
                    commandApplied = available && cropPlots.ApplyWork(
                        plot,
                        BuiltInWorkTypeIds.Harvest,
                        harvest.RequiredWork,
                        faultActor,
                        out cycleCompleted);
                    commandDetail = $"plot={cropPlotId};available={available};"
                        + $"completed={cycleCompleted};reason={harvest.UnavailableReason}";
                }
                else
                {
                    WorldResourceNode node = ResolveMiningNode();
                    WorldResourceWorkSnapshot quarry = default;
                    bool available = node != null && worldResources.TryGetWork(
                        node,
                        BuiltInWorkTypeIds.Quarry,
                        out quarry) && quarry.Available;
                    commandApplied = available && worldResources.ApplyWork(
                        node,
                        BuiltInWorkTypeIds.Quarry,
                        quarry.RequiredWork,
                        out cycleCompleted);
                    commandDetail = $"node={miningNodeId};available={available};"
                        + $"completed={cycleCompleted};reason={quarry.UnavailableReason}";
                }

                int produced = CountItemQuantity(itemId) - totalBefore;
                int sourceLooseDelta = CountLooseAt(itemId, sourceCell)
                    - sourceLooseBefore;
                currentBurstProbe = new ArmBurstProbe(
                    producerKind,
                    itemId,
                    sourceCell,
                    produced,
                    totalBefore,
                    storedBefore,
                    carriedBefore);
                bool productionBurstExact = commandApplied && cycleCompleted
                    && produced > 0 && sourceLooseDelta == produced;
                Check(productionBurstExact,
                    producerKind == BurstProducerKind.CropHarvest
                        ? "PAIRED_CROP_HARVEST_BURST_PRODUCTION"
                        : "PAIRED_MINING_BURST_PRODUCTION",
                    $"seed={seed};arm={arm};item={itemId};produced={produced};"
                    + $"sourceLooseDelta={sourceLooseDelta};cell={sourceCell};"
                    + commandDetail);
                if (!productionBurstExact)
                    yield break;
                eventHash = HashText(
                    $"{seed}|{ScenarioId}|{faultActorId}|{producerKind}|"
                    + $"{produced}|{sequence.State}");
                productionBurstArmCount++;
                if (producerKind == BurstProducerKind.CropHarvest)
                    cropHarvestBurstArmCount++;
                else
                    miningBurstArmCount++;
            }

            WorldItemStackSnapshot[] producedStacks = items.GetAllStacks()
                .Where(value => value != null
                    && !interventionStackIdsBefore.Contains(value.StackId)
                    && value.Position == currentBurstProbe.SourceCell
                    && value.State == WorldItemStackState.Loose
                    && string.Equals(value.ItemId, currentBurstProbe.ItemId, StringComparison.Ordinal))
                .OrderBy(value => value.StackId, StringComparer.Ordinal)
                .ToArray();
            WorldItemStackSnapshot[] ancillaryProducedStacks = items.GetAllStacks()
                .Where(value => value != null
                    && !interventionStackIdsBefore.Contains(value.StackId)
                    && value.Position == currentBurstProbe.SourceCell
                    && value.State == WorldItemStackState.Loose
                    && !string.Equals(
                        value.ItemId,
                        currentBurstProbe.ItemId,
                        StringComparison.Ordinal))
                .OrderBy(value => value.StackId, StringComparer.Ordinal)
                .ToArray();
            bool ancillaryIsolated = true;
            foreach (WorldItemStackSnapshot ancillary in ancillaryProducedStacks)
                ancillaryIsolated &= items.SetForbidden(ancillary.StackId, true);
            Check(ancillaryIsolated,
                "PAIRED_PRODUCTION_ANCILLARY_OUTPUT_ISOLATED",
                $"seed={seed};arm={arm};producer={currentBurstProbe.ProducerKind};"
                + $"count={ancillaryProducedStacks.Length};"
                + string.Join(",", ancillaryProducedStacks.Select(value =>
                    $"{value.StackId}:{value.ItemId}:{value.Quantity}")));
            if (!ancillaryIsolated)
                yield break;
            bool prioritized = producedStacks.Length > 0;
            foreach (WorldItemStackSnapshot stack in producedStacks)
                prioritized &= items.PrioritizeHaul(stack.StackId);
            Check(prioritized,
                "PAIRED_PRODUCTION_BURST_HAUL_PRIORITY",
                $"seed={seed};arm={arm};stacks={producedStacks.Length};"
                + string.Join(",", producedStacks.Select(value => value.StackId)));
            if (!prioritized)
                yield break;
            productionPriorityArmCount++;

            HashSet<string> producedStackIds = producedStacks
                .Select(value => value.StackId)
                .ToHashSet(StringComparer.Ordinal);
            bool previewAvailable = haulPlanning.TryPreviewBestPlan(
                faultActor,
                out WorldItemHaulPlan previewPlan,
                out string previewFailure);
            bool previewSelectsBurst = previewAvailable
                && previewPlan?.ReservedStackQuantities.Any(value =>
                    producedStackIds.Contains(value.StackId)) == true;
            IEnumerable<string> previewStackIds = previewPlan?.ReservedStackQuantities
                .Select(value => value.StackId)
                ?? Array.Empty<string>();
            Check(previewSelectsBurst,
                "PAIRED_PRODUCTION_BURST_HAUL_PLAN_PREFLIGHT",
                $"seed={seed};arm={arm};producer={currentBurstProbe.ProducerKind};"
                + $"available={previewAvailable};failure={previewFailure};"
                + $"planDestination={previewPlan?.PrimaryDestinationId};"
                + $"planStacks={string.Join(",", previewStackIds)};"
                + $"burstStacks={string.Join(",", producedStackIds.OrderBy(value => value, StringComparer.Ordinal))}");
            if (!previewSelectsBurst)
                yield break;

            faultActor.SetAiPaused(false);
            faultActor.Brain?.RequestImmediateReplan(clearFailures: true);
            scheduler.ResetDecisionQueueForDiagnostics();
            WorldItemHaulPlanLeg firstPickupLeg = previewPlan.PickupLegs
                .First(value => value.IsValid);
            float pickupMoveSpeed = Mathf.Max(0.1f, faultActor.GetMoveSpeed());
            float directPickupTravelSeconds = Vector3.Distance(
                    grid.GetWorldPos(faultActor.GetNowXY()),
                    grid.GetWorldPos(firstPickupLeg.PickupStandPosition))
                / pickupMoveSpeed;
            float pickupGameBudget = PickupSearchAndSchedulingHeadroomSeconds
                + directPickupTravelSeconds * 2f;
            float pickupGameDeadline = clock.Time + pickupGameBudget;
            float pickupRealtimeDeadline = Time.realtimeSinceStartup + 15f;
            bool haulExecutionObserved = false;
            float acceleratedCaptureDeltaTime = Time.captureDeltaTime;
            // The production path broker advances an urgent exact search in
            // bounded slices over as many as 240 rendered frames. Keep the
            // 32x simulation rate, but provide enough rendered-frame density
            // for the real Brain -> AIHaul -> broker -> movement path to
            // complete before injecting the hauler fault.
            Time.captureDeltaTime = PickupCaptureDeltaTime;
            while (clock.Time < pickupGameDeadline
                && Time.realtimeSinceStartup < pickupRealtimeDeadline
                && CountActorCarriedQuantity(
                    faultActor, currentBurstProbe.ItemId) == 0)
            {
                AbilityMove pickupMove = faultActor.GetComponent<AbilityMove>();
                AbilityHaul pickupHaul = faultActor.GetComponent<AbilityHaul>();
                if (!haulExecutionObserved && pickupHaul?.IsHauling == true)
                {
                    // Candidate discovery and the committed haul each own an
                    // incremental path-search boundary.  A remote quarry may
                    // legitimately consume the scheduling headroom before the
                    // AIHaul epoch starts; begin the physical route allowance
                    // at that typed ownership transition instead of letting
                    // candidate-search time erase movement time.
                    haulExecutionObserved = true;
                    pickupGameDeadline = Mathf.Max(
                        pickupGameDeadline,
                        clock.Time
                            + PickupSearchAndSchedulingHeadroomSeconds
                            + directPickupTravelSeconds * 2f);
                    // Candidate discovery and committed movement are two
                    // independent production phases. The game-time budget was
                    // already phased above; give the committed movement its
                    // own bounded realtime watchdog as well. Otherwise a
                    // candidate selected on the final discovery frame is
                    // cancelled by the harness immediately after its first
                    // coroutine yield. The post-fault 90-game-second recovery
                    // SLA remains unchanged.
                    pickupRealtimeDeadline = Time.realtimeSinceStartup + 15f;
                }
                bool followingResolvedPath = string.Equals(
                        pickupMove?.ActiveMovementOperationOwnerForDiagnostics,
                        "raw-path",
                        StringComparison.Ordinal)
                    && pickupHaul?.CurrentExecutionStage.StartsWith(
                        "경로 이동 중",
                        StringComparison.Ordinal) == true;
                Time.captureDeltaTime = followingResolvedPath
                    ? acceleratedCaptureDeltaTime
                    : PickupCaptureDeltaTime;
                EnsureVerificationTimeScale();
                yield return null;
            }
            Time.captureDeltaTime = acceleratedCaptureDeltaTime;
            int carriedAtFault = CountActorCarriedQuantity(
                faultActor, currentBurstProbe.ItemId);
            Check(carriedAtFault > 0,
                "PAIRED_HAULER_FAULT_AFTER_PHYSICAL_PICKUP",
                $"seed={seed};arm={arm};actor={faultActorId};carried={carriedAtFault};"
                + $"position={faultActor.GetNowXY()};clock={clock.Time:0.###};"
                + $"pickupBudget={pickupGameBudget:0.###};"
                + $"haulObserved={haulExecutionObserved};"
                + $"directTravel={directPickupTravelSeconds:0.###};"
                + $"moveSpeed={pickupMoveSpeed:0.###};"
                + $"action={faultActor.Brain?.CurrentActionDebugLabel};"
                + $"actors={DescribeActors()};"
                + $"stacks=" + string.Join(",", items.GetAllStacks()
                    .Where(value => value != null
                        && string.Equals(
                            value.ItemId,
                            currentBurstProbe.ItemId,
                            StringComparison.Ordinal))
                    .OrderBy(value => value.StackId, StringComparer.Ordinal)
                    .Select(value => $"{value.StackId}:{value.Quantity}:{value.State}@{value.Position}")));
            if (carriedAtFault <= 0)
                yield break;
            postPickupFaultArmCount++;
            faultActor.SetLifecycleState(CharacterLifecycleState.Downed);
            foreach (CharacterActor actor in LiveActors())
            {
                actor.SetAiPaused(false);
                actor.Brain?.RequestImmediateReplan(clearFailures: true);
            }
        }

        for (int window = 0; window < 4; window++)
        {
            SetPhase($"seed-{seed}-{arm}-window-{window}");
            WindowAccumulator accumulator = new();
            yield return ObserveWindow(seed, arm, window, eventHash, accumulator);
            if (failures.Count > 0)
                yield break;
        }

        CharacterActor restoredFault = ResolveActor(faultActorId);
        if (restoredFault != null
            && restoredFault.CurrentLifecycleState == CharacterLifecycleState.Downed)
        {
            restoredFault.SetLifecycleState(CharacterLifecycleState.Active);
            restoredFault.Brain?.RequestImmediateReplan(clearFailures: true);
        }
        SetPhase($"seed-{seed}-{arm}-recovery");
        yield return ObserveDuration(seed, arm, 4, RecoverySeconds, true);
        if (currentBurstProbe != null)
        {
            BurstState finalBurst = CaptureBurstState(currentBurstProbe);
            Check(finalBurst.QuantityConserved,
                "PAIRED_BURST_RECOVERY_CONSERVED",
                $"seed={seed};arm={arm};expected={currentBurstProbe.Quantity};"
                + $"totalDelta={finalBurst.TotalDelta};delivered={finalBurst.Delivered};"
                + $"outstanding={finalBurst.Outstanding}");
            Check(finalBurst.Delivered >= currentBurstProbe.Quantity
                    && finalBurst.Outstanding == 0,
                "PAIRED_BURST_RECOVERY_COMPLETED",
                $"seed={seed};arm={arm};expected={currentBurstProbe.Quantity};"
                + $"delivered={finalBurst.Delivered};outstanding={finalBurst.Outstanding};"
                + $"sourceLoose={finalBurst.SourceLoose};sourceReserved={finalBurst.SourceReserved};"
                + $"carried={finalBurst.CarriedDelta};actors={DescribeActors()}");
        }
        FloorClutterAssessment recovered = clutter.Capture(
            grid, layout, WarmupSeconds + 4 * WindowSeconds + RecoverySeconds);
        int recoveredHeadroom = CaptureRuntimeHeadroomPermille();
        floorRows.Add(new FloorRow(
            seed,
            arm,
            4,
            recovered,
            clutterCellSeconds: 0,
            runtimeHeadroomPermille: recoveredHeadroom,
            runtimeErosionCells: lastRuntimeHeadroomErosionCount,
            runtimeErosionDetail: lastRuntimeHeadroomErosionDetail,
            isRecovery: true));
        bool recoveredClean = recovered.PersistentCount == 0
            && recovered.ImmediateFailureCount == 0;
        string recoveryDetail = recoveredClean
            ? $"seed={seed};arm={arm};persistent=0;immediate=0"
            : $"seed={seed};arm={arm};persistent={recovered.PersistentCount};"
              + $"immediate={recovered.ImmediateFailureCount};loose={recovered.LooseStackCount};"
              + $"outside={DescribeOutside(recovered)};actors={DescribeActors()}";
        if (Focused && !recoveredClean)
        {
            focusedDeferredFailures.Add(
                "PAIRED_ARM_RECOVERED:" + recoveryDetail);
        }
        else
        {
            Check(recoveredClean, "PAIRED_ARM_RECOVERED", recoveryDetail);
        }
    }

    private IEnumerator ObserveWindow(
        int seed,
        string arm,
        int window,
        string eventHash,
        WindowAccumulator accumulator)
    {
        float elapsed = 0f;
        float lastClockProgressRealtime = Time.realtimeSinceStartup;
        CharacterActor[] startActors = LiveActors();
        Dictionary<string, long> replanStart = startActors.ToDictionary(
            ActorId, value => value.Brain?.RuntimeImmediateReplanCount ?? 0L,
            StringComparer.Ordinal);
        Dictionary<string, long> pathStart = startActors.ToDictionary(
            ActorId,
            value => value.GetComponent<AbilityMove>()?.RuntimeActionPathReplanCount ?? 0L,
            StringComparer.Ordinal);
        Dictionary<string, bool> stepAsideLive = new(StringComparer.Ordinal);
        while (elapsed < WindowSeconds)
        {
            EnsureVerificationTimeScale();
            float delta = Mathf.Min(Mathf.Max(clock.DeltaTime, 0f), WindowSeconds - elapsed);
            if (delta > 0f)
            {
                lastClockProgressRealtime = Time.realtimeSinceStartup;
            }
            else if (Time.realtimeSinceStartup - lastClockProgressRealtime
                     >= ClockProgressTimeoutRealtimeSeconds)
            {
                FailOnce(
                    "PAIRED_CLOCK_NO_PROGRESS",
                    $"phase={CurrentPhase};seed={seed};arm={arm};window={window};"
                    + $"elapsed={elapsed:0.###};timeScale={Time.timeScale:0.###}");
                yield break;
            }
            elapsed += delta;
            SampleActors(seed, arm, delta, accumulator, stepAsideLive);
            FloorClutterAssessment current = clutter.Capture(
                grid, layout, WarmupSeconds + window * WindowSeconds + elapsed);
            accumulator.ClutterCellSeconds += Mathf.RoundToInt(
                current.OutsideContainment.Count * delta);
            if (current.ImmediateFailureCount > 0)
                accumulator.ImmediateFailures += current.ImmediateFailureCount;
            if (Focused && arm is "cleanRepeatA" or "cleanRepeatB")
            {
                string key = $"{seed}|{arm}|{window}";
                if (!focusedFrameTraces.TryGetValue(key, out List<string> trace))
                {
                    trace = new List<string>();
                    focusedFrameTraces.Add(key, trace);
                }
                trace.Add(CaptureFocusedFrameTrace(elapsed));
            }
            yield return null;
        }

        CharacterActor[] endActors = LiveActors();
        accumulator.Replans = checked((int)endActors.Sum(actor =>
            Math.Max(0L, (actor.Brain?.RuntimeImmediateReplanCount ?? 0L)
                - (replanStart.TryGetValue(ActorId(actor), out long start) ? start : 0L))));
        accumulator.Replans += checked((int)endActors.Sum(actor =>
            Math.Max(0L,
                (actor.GetComponent<AbilityMove>()?.RuntimeActionPathReplanCount ?? 0L)
                - (pathStart.TryGetValue(ActorId(actor), out long start) ? start : 0L))));
        string semantic = CaptureSemanticHash();
        IReadOnlyList<RandomStreamDiagnosticSnapshot> random = randomDiagnostics.Capture();
        string randomHash = CaptureRandomHash(random);
        randomByArmWindow[$"{seed}|{arm}|{window}"] = random;
        PairedRunWindowResult row = new(
            seed,
            arm,
            window,
            accumulator.TravelMilliWu,
            accumulator.WaitMilliWu,
            accumulator.Replans,
            accumulator.StepAsideCount,
            accumulator.ClutterCellSeconds,
            semantic,
            randomHash,
            eventHash,
            accumulator.DispatchWaitMilliWu,
            accumulator.ReservationWaitMilliWu,
            accumulator.FacilityAccessWaitMilliWu,
            accumulator.NoPathMilliWu,
            accumulator.BurstDeliveredQuantity,
            accumulator.BurstOutstandingQuantity,
            accumulator.BurstQuantityConserved);
        rows.Add(row);
        PublishCurrentProgress("RUNNING");
        FloorClutterAssessment end = clutter.Capture(
            grid, layout, WarmupSeconds + (window + 1) * WindowSeconds);
        int windowHeadroom = CaptureRuntimeHeadroomPermille();
        floorRows.Add(new FloorRow(
            seed,
            arm,
            window,
            end,
            accumulator.ClutterCellSeconds,
            windowHeadroom,
            lastRuntimeHeadroomErosionCount,
            lastRuntimeHeadroomErosionDetail,
            false));
        Check(accumulator.ImmediateFailures == 0,
            "PAIRED_WINDOW_ACCESS_CLEAR",
            $"seed={seed};arm={arm};window={window};immediate={accumulator.ImmediateFailures}");
    }

    private IEnumerator ObserveDuration(
        int seed,
        string arm,
        int phase,
        float duration,
        bool recovery)
    {
        float elapsed = 0f;
        float lastClockProgressRealtime = Time.realtimeSinceStartup;
        while (elapsed < duration)
        {
            EnsureVerificationTimeScale();
            float delta = Mathf.Min(Mathf.Max(clock.DeltaTime, 0f), duration - elapsed);
            if (delta > 0f)
            {
                lastClockProgressRealtime = Time.realtimeSinceStartup;
            }
            else if (Time.realtimeSinceStartup - lastClockProgressRealtime
                     >= ClockProgressTimeoutRealtimeSeconds)
            {
                FailOnce(
                    "PAIRED_CLOCK_NO_PROGRESS",
                    $"phase={CurrentPhase};seed={seed};arm={arm};window={phase};"
                    + $"elapsed={elapsed:0.###};timeScale={Time.timeScale:0.###}");
                yield break;
            }
            elapsed += delta;
            if (Focused && phase == -1
                && arm is "cleanRepeatA" or "cleanRepeatB")
            {
                string key = $"{seed}|{arm}|{phase}";
                if (!focusedFrameTraces.TryGetValue(key, out List<string> trace))
                {
                    trace = new List<string>();
                    focusedFrameTraces.Add(key, trace);
                }
                trace.Add(CaptureFocusedFrameTrace(elapsed));
            }
            yield return null;
        }
        _ = seed;
        _ = arm;
        _ = phase;
        _ = recovery;
    }

    private void SampleActors(
        int seed,
        string arm,
        float delta,
        WindowAccumulator accumulator,
        IDictionary<string, bool> stepAsideLive)
    {
        CharacterActor[] actors = LiveActors();
        foreach (CharacterActor actor in actors)
        {
            string id = ActorId(actor);
            AbilityMove move = actor.GetComponent<AbilityMove>();
            AbilityHaul haul = actor.GetComponent<AbilityHaul>();
            bool moving = move?.HasActiveMovementRoutineForDiagnostics == true;
            bool hauling = haul?.IsHauling == true;
            if (moving)
                accumulator.TravelMilliWu += Mathf.RoundToInt(delta * WorkMilliWuPerGameSecond);
            bool stepAside = string.Equals(
                actor.Brain?.CurrentActionDebugLabel,
                "길 비켜주기",
                StringComparison.Ordinal);
            bool wasStepAside = stepAsideLive.TryGetValue(id, out bool previous) && previous;
            if (stepAside && !wasStepAside)
                accumulator.StepAsideCount++;
            stepAsideLive[id] = stepAside;
            if (arm is "faultControl" or "clutterStress"
                && (hauling || moving && currentBurstProbe != null
                    && Manhattan(actor.GetNowXY(), currentBurstProbe.SourceCell) <= 3))
            {
                if (!affectedActorsBySeed.TryGetValue(seed, out HashSet<string> affected))
                {
                    affected = new HashSet<string>(StringComparer.Ordinal);
                    affectedActorsBySeed.Add(seed, affected);
                }
                affected.Add(id);
            }
        }
        SampleBurstWait(delta, accumulator, actors);
    }

    private void SampleBurstWait(
        float delta,
        WindowAccumulator accumulator,
        IReadOnlyList<CharacterActor> actors)
    {
        if (currentBurstProbe == null)
            return;
        BurstState state = CaptureBurstState(currentBurstProbe);
        accumulator.BurstDeliveredQuantity = state.Delivered;
        accumulator.BurstOutstandingQuantity = state.Outstanding;
        accumulator.BurstQuantityConserved &= state.QuantityConserved;
        if (state.Outstanding <= 0)
            return;

        long milliWu = Mathf.RoundToInt(delta * WorkMilliWuPerGameSecond);
        BurstHaulObservation[] observations = actors
            .Select(CaptureBurstHaulObservation)
            .Where(value => value.Phase != BurstHaulPhase.None)
            .ToArray();
        BurstHaulObservation[] invalid = observations
            .Where(value => value.Phase == BurstHaulPhase.Invalid)
            .ToArray();
        int joinedCarriedQuantity = observations.Sum(
            value => value.JoinedBurstCarriedQuantity);
        if (invalid.Length > 0 || joinedCarriedQuantity != state.CarriedDelta)
        {
            FailOnce(
                "PAIRED_BURST_HAUL_ATTRIBUTION_INVALID",
                $"item={currentBurstProbe.ItemId};"
                + $"stateCarried={state.CarriedDelta};joinedCarried={joinedCarriedQuantity};"
                + $"observations={string.Join("|", observations.Select(value => value.Detail))}");
            return;
        }

        if (observations.Any(value => value.Phase == BurstHaulPhase.DeliveryMoving))
            return;
        accumulator.WaitMilliWu = checked(accumulator.WaitMilliWu + milliWu);
        if (observations.Any(value => value.Phase == BurstHaulPhase.NoPath))
            accumulator.NoPathMilliWu = checked(accumulator.NoPathMilliWu + milliWu);
        else if (joinedCarriedQuantity > 0
            && observations
                .Where(value => value.JoinedBurstCarriedQuantity > 0)
                .All(value => value.Phase == BurstHaulPhase.DestinationAccessWait))
        {
            accumulator.FacilityAccessWaitMilliWu = checked(
                accumulator.FacilityAccessWaitMilliWu + milliWu);
        }
        else if (joinedCarriedQuantity == 0
            && observations.Any(value => value.Phase == BurstHaulPhase.SourceReserved))
        {
            accumulator.ReservationWaitMilliWu = checked(
                accumulator.ReservationWaitMilliWu + milliWu);
        }
        else
        {
            accumulator.DispatchWaitMilliWu = checked(
                accumulator.DispatchWaitMilliWu + milliWu);
        }
    }

    private BurstHaulObservation CaptureBurstHaulObservation(
        CharacterActor actor)
    {
        if (actor == null || currentBurstProbe == null)
            return BurstHaulObservation.None;

        string actorId = ActorId(actor);
        AbilityHaul haul = actor.GetComponent<AbilityHaul>();
        AbilityMove move = actor.GetComponent<AbilityMove>();
        CharacterCarriedItemSaveData[] burstCarried =
            (actor.CarryInventory?.Items
                ?? Array.Empty<CharacterCarriedItemSaveData>())
            .Where(value => value != null
                && value.quantity > 0
                && string.Equals(
                    value.itemId,
                    currentBurstProbe.ItemId,
                    StringComparison.Ordinal))
            .ToArray();
        WorldItemReservedStackQuantity[] burstReservations =
            (haul?.ActiveReservationsForDiagnostics
                ?? Array.Empty<WorldItemReservedStackQuantity>())
            .Where(value => value.IsValid
                && string.Equals(
                    value.ItemId,
                    currentBurstProbe.ItemId,
                    StringComparison.Ordinal)
                && value.Position == currentBurstProbe.SourceCell)
            .ToArray();

        if (burstCarried.Length == 0)
        {
            if (burstReservations.Length == 0)
                return BurstHaulObservation.None;
            if (haul == null || !haul.IsHauling)
            {
                return BurstHaulObservation.Invalid(
                    actorId,
                    "source reservation exists without active haul");
            }
            if (actor.Brain?.LastActionFailure.Kind == AIActionFailureKind.NoPath)
            {
                return new BurstHaulObservation(
                    BurstHaulPhase.NoPath,
                    actorId,
                    0,
                    $"{actorId}:NoPath:source-reserved={burstReservations.Sum(value => value.Quantity)}");
            }
            if (move?.HasActiveMovementRoutineForDiagnostics == true)
            {
                return new BurstHaulObservation(
                    BurstHaulPhase.DeliveryMoving,
                    actorId,
                    0,
                    $"{actorId}:PickupMoving:source-reserved={burstReservations.Sum(value => value.Quantity)}");
            }
            return new BurstHaulObservation(
                BurstHaulPhase.SourceReserved,
                actorId,
                0,
                $"{actorId}:SourceReserved:{burstReservations.Sum(value => value.Quantity)}");
        }

        if (haul == null || items == null)
        {
            return BurstHaulObservation.Invalid(
                actorId,
                "carried burst has no haul or delivery-intent query");
        }
        string[] operationIds = burstCarried
            .Select(value => value.ownerOperationId ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (operationIds.Length != 1
            || string.IsNullOrWhiteSpace(operationIds[0])
            || !haul.OwnsHaulOperation(operationIds[0]))
        {
            return BurstHaulObservation.Invalid(
                actorId,
                "carried burst operation is missing, mixed, or not owned");
        }

        string operationId = operationIds[0];
        if (!items.TryCaptureHaulDeliveryIntent(
                operationId,
                out HaulDeliveryIntentSaveData intent)
            || intent == null
            || !intent.HasCommittedPickup
            || !string.Equals(
                intent.ownerCharacterId,
                actorId,
                StringComparison.Ordinal))
        {
            return BurstHaulObservation.Invalid(
                actorId,
                "committed delivery intent is missing or owned by another actor");
        }

        CharacterCarriedItemSaveData[] operationCarried = actor.CarryInventory.Items
            .Where(value => value != null
                && value.quantity > 0
                && string.Equals(
                    value.ownerOperationId,
                    operationId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
            .ToArray();
        HaulDeliveryItemCommitmentSaveData[] commitments =
            (intent.commitments
                ?? new List<HaulDeliveryItemCommitmentSaveData>())
            .Where(value => value != null && value.quantity > 0)
            .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
            .ToArray();
        WorldItemReservedStackQuantity[] operationReservations =
            haul.ActiveReservationsForDiagnostics
                .Where(value => value.IsValid
                    && string.Equals(
                        value.OwnerOperationId,
                        operationId,
                        StringComparison.Ordinal))
                .ToArray();
        bool commitmentVectorExact = operationCarried.Length == commitments.Length
            && operationCarried
                .Select(value => value.carriedStackId)
                .Distinct(StringComparer.Ordinal)
                .Count() == operationCarried.Length
            && commitments
                .Select(value => value.carriedStackId)
                .Distinct(StringComparer.Ordinal)
                .Count() == commitments.Length
            && operationCarried.All(carried => commitments.Any(commitment =>
                string.Equals(
                    commitment.carriedStackId,
                    carried.carriedStackId,
                    StringComparison.Ordinal)
                && string.Equals(
                    commitment.sourceStackId,
                    carried.sourceStackId,
                    StringComparison.Ordinal)
                && string.Equals(
                    commitment.itemId,
                    carried.itemId,
                    StringComparison.Ordinal)
                && string.Equals(
                    commitment.expectedStackSignature,
                    ItemReservationSignature.Create(
                        carried.itemId,
                        carried.components),
                    StringComparison.Ordinal)
                && commitment.quantity == carried.quantity));
        bool reservationVectorExact = operationReservations.Length > 0
            && operationReservations.All(reservation =>
                reservation.DestinationKind == intent.destinationKind
                && string.Equals(
                    reservation.DestinationId,
                    intent.destinationId,
                    StringComparison.Ordinal))
            && commitments
                .GroupBy(
                    value => (value.sourceStackId, value.itemId),
                    value => value.quantity)
                .All(group => operationReservations
                    .Where(reservation => string.Equals(
                            reservation.StackId,
                            group.Key.sourceStackId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            reservation.ItemId,
                            group.Key.itemId,
                            StringComparison.Ordinal))
                    .Sum(reservation => reservation.Quantity) == group.Sum());
        if (!commitmentVectorExact || !reservationVectorExact)
        {
            return BurstHaulObservation.Invalid(
                actorId,
                $"delivery join mismatch;operation={operationId};"
                + $"carried={operationCarried.Length};commitments={commitments.Length};"
                + $"reservations={operationReservations.Length};"
                + $"commitmentExact={commitmentVectorExact};"
                + $"reservationExact={reservationVectorExact};"
                + "carriedVector=" + string.Join(",", operationCarried.Select(value =>
                    $"{value.carriedStackId}/{value.sourceStackId}/{value.itemId}/{value.quantity}"))
                + ";commitmentVector=" + string.Join(",", commitments.Select(value =>
                    $"{value.carriedStackId}/{value.sourceStackId}/{value.itemId}/{value.quantity}"))
                + ";reservationVector=" + string.Join(",", operationReservations.Select(value =>
                    $"{value.StackId}/{value.ItemId}/{value.Quantity}/{value.DestinationKind}/{value.DestinationId}")));
        }

        int joinedBurstQuantity = burstCarried.Sum(value => value.quantity);
        BurstHaulPhase phase;
        if (actor.IsDead
            || actor.CurrentLifecycleState is
                CharacterLifecycleState.Downed or CharacterLifecycleState.Despawned
            || !haul.IsHauling && haul.HasBoundDeliveryIntent)
        {
            phase = BurstHaulPhase.RecoveryPending;
        }
        else if (actor.Brain?.LastActionFailure.Kind == AIActionFailureKind.NoPath)
        {
            phase = BurstHaulPhase.NoPath;
        }
        else if (move?.HasActiveMovementRoutineForDiagnostics == true)
        {
            phase = BurstHaulPhase.DeliveryMoving;
        }
        else
        {
            Vector2Int deliveryCell = new(
                intent.deliveryGridX,
                intent.deliveryGridY);
            phase = actor.GetNowXY() == deliveryCell
                ? BurstHaulPhase.DestinationAccessWait
                : BurstHaulPhase.DeliveryRoutingWait;
        }
        return new BurstHaulObservation(
            phase,
            actorId,
            joinedBurstQuantity,
            $"{actorId}:{phase}:operation={operationId}:"
            + $"carried={joinedBurstQuantity}:destination={intent.destinationId}:"
            + $"position={actor.GetNowXY()}:"
            + $"delivery={intent.deliveryGridX},{intent.deliveryGridY}");
    }

    private BurstState CaptureBurstState(ArmBurstProbe probe)
    {
        WorldItemStackSnapshot[] stacks = items.GetAllStacks()
            .Where(value => value != null
                && string.Equals(
                    value.ItemId,
                    probe.ItemId,
                    StringComparison.Ordinal))
            .ToArray();
        int totalDelta = stacks.Sum(value => value.Quantity) - probe.TotalBefore;
        int storedDelta = stacks.Where(value => value.State == WorldItemStackState.Stored)
            .Sum(value => value.Quantity) - probe.StoredBefore;
        int carriedDelta = stacks.Where(value => value.State == WorldItemStackState.Carried)
            .Sum(value => value.Quantity) - probe.CarriedBefore;
        int sourceLoose = stacks.Where(value => value.Position == probe.SourceCell
                && value.State == WorldItemStackState.Loose)
            .Sum(value => value.Quantity);
        int sourceReserved = stacks.Where(value => value.Position == probe.SourceCell
                && value.State == WorldItemStackState.Loose)
            .Sum(value => value.ReservedQuantity);
        int delivered = Mathf.Clamp(storedDelta, 0, probe.Quantity);
        return new BurstState(
            totalDelta,
            sourceLoose,
            sourceReserved,
            Mathf.Max(0, carriedDelta),
            delivered,
            Mathf.Max(0, probe.Quantity - delivered),
            totalDelta == probe.Quantity);
    }

    private int CountItemQuantity(string itemId) => items.GetAllStacks()
        .Where(value => value != null
            && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
        .Sum(value => value.Quantity);

    private int CountStoredItemQuantity(string itemId) => items.GetAllStacks()
        .Where(value => value != null
            && value.State == WorldItemStackState.Stored
            && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
        .Sum(value => value.Quantity);

    private int CountLooseAt(string itemId, Vector2Int position) =>
        items.GetAllStacks()
            .Where(value => value != null
                && value.State == WorldItemStackState.Loose
                && value.Position == position
                && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
            .Sum(value => value.Quantity);

    private int CountCarriedItemQuantity(string itemId) => LiveActors()
        .Sum(actor => CountActorCarriedQuantity(actor, itemId));

    private static int CountActorCarriedQuantity(
        CharacterActor actor,
        string itemId) => actor?.GetComponent<CharacterCarryInventory>()?.Items
        .Where(value => value != null
            && string.Equals(value.itemId, itemId, StringComparison.Ordinal))
        .Sum(value => value.quantity) ?? 0;

    private void ValidateExogenousEventsExact(int seed)
    {
        PairedRunWindowResult[] control = rows
            .Where(value => value.Seed == seed
                && string.Equals(
                    value.Arm,
                    "faultControl",
                    StringComparison.Ordinal))
            .OrderBy(value => value.WindowIndex)
            .ToArray();
        PairedRunWindowResult[] stress = rows
            .Where(value => value.Seed == seed
                && string.Equals(
                    value.Arm,
                    "clutterStress",
                    StringComparison.Ordinal))
            .OrderBy(value => value.WindowIndex)
            .ToArray();
        bool exact = control.Length == 4 && stress.Length == 4;
        string mismatch = exact ? string.Empty
            : $"rows={control.Length}/{stress.Length}";
        for (int index = 0; exact && index < 4; index++)
        {
            if (control[index].WindowIndex == stress[index].WindowIndex
                && string.Equals(
                    control[index].ExogenousEventHash,
                    stress[index].ExogenousEventHash,
                    StringComparison.Ordinal))
            {
                continue;
            }

            exact = false;
            mismatch = $"window={control[index].WindowIndex}/"
                + $"{stress[index].WindowIndex};hash="
                + $"{control[index].ExogenousEventHash}/"
                + stress[index].ExogenousEventHash;
        }

        Check(exact, "PAIRED_RUN_EXOGENOUS_EVENTS_EXACT",
            exact ? $"seed={seed};windows=4" : $"seed={seed};{mismatch}");
    }

    private void ValidateCausalCone(int seed)
    {
        HashSet<string> affected = affectedActorsBySeed.TryGetValue(
            seed, out HashSet<string> found)
            ? found
            : new HashSet<string>(StringComparer.Ordinal);
        affected.Add(faultActorId);
        for (int window = 0; window < 4; window++)
        {
            IReadOnlyList<RandomStreamDiagnosticSnapshot> control =
                randomByArmWindow[$"{seed}|faultControl|{window}"];
            IReadOnlyList<RandomStreamDiagnosticSnapshot> stress =
                randomByArmWindow[$"{seed}|clutterStress|{window}"];
            Dictionary<string, RandomStreamDiagnosticSnapshot> left = control
                .ToDictionary(value => value.StreamId, StringComparer.Ordinal);
            Dictionary<string, RandomStreamDiagnosticSnapshot> right = stress
                .ToDictionary(value => value.StreamId, StringComparer.Ordinal);
            string[] streamIds = left.Keys
                .Concat(right.Keys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            foreach (string streamId in streamIds)
            {
                if (IsAffectedActorStream(streamId, affected))
                    continue;
                bool controlPresent = left.TryGetValue(
                    streamId,
                    out RandomStreamDiagnosticSnapshot controlValue);
                bool stressPresent = right.TryGetValue(
                    streamId,
                    out RandomStreamDiagnosticSnapshot stressValue);
                if (!controlPresent || !stressPresent)
                {
                    Fail("RNG_CROSS_TALK",
                        $"seed={seed};window={window};stream={streamId};"
                        + $"controlPresent={controlPresent};"
                        + $"stressPresent={stressPresent}");
                    return;
                }
                if (controlValue.State != stressValue.State
                    || controlValue.DrawCount != stressValue.DrawCount)
                {
                    Fail("RNG_CROSS_TALK",
                        $"seed={seed};window={window};stream={streamId};"
                        + $"control={controlValue.State}/{controlValue.DrawCount};"
                        + $"stress={stressValue.State}/{stressValue.DrawCount}");
                    return;
                }
            }
        }
        Check(true, "PAIRED_RNG_CAUSAL_CONE",
            $"seed={seed};affectedActors={string.Join(",", affected.OrderBy(value => value, StringComparer.Ordinal))}");
    }

    private IEnumerator Restore(
        string json,
        float checkpointTime,
        int checkpointFrame)
    {
        clockDiagnostics.RebaseDeterministicCheckpointTime(
            checkpointTime,
            checkpointFrame);
        DungeonGameSaveData candidate = saves.FromJson(json);
        bool restored = saves.TryRestore(candidate, out DungeonGameRestoreReport report);
        Check(restored, "PAIRED_CHECKPOINT_RESTORE",
            restored
                ? $"sections={candidate.sections.Count}"
                : report == null
                    ? "failed:report-null"
                    : $"errors={string.Join(" | ", report.Errors)};"
                    + $"warnings={string.Join(" | ", report.Warnings)}");
        if (!restored)
            yield break;
        ApplyMeasurementIsolation(activateMeasuredActors: false);
        for (int frame = 0; frame < 6; frame++)
            yield return null;
        clockDiagnostics.RebaseDeterministicCheckpointTime(
            checkpointTime,
            checkpointFrame);
        ApplyMeasurementIsolation(activateMeasuredActors: true);
        // A full-world restore republishes the runtime collaborators used by
        // the scheduler. Re-assert the diagnostics composition after that
        // publication so the paired arms cannot silently fall back to the
        // frame-budgeted path broker while the scheduler flag remains stale.
        scheduler.ConfigureDeterministicSimulationForDiagnostics(true);
        characterSpawner.ConfigureDeterministicSimulationForDiagnostics(true);
        scheduler.ResetDeterministicSimulationCheckpointForDiagnostics();
        Check(scheduler.DeterministicSimulationForDiagnostics,
            "PAIRED_DETERMINISTIC_SCHEDULER_REBOUND_AFTER_RESTORE",
            $"checkpoint={checkpointTime:0.###}/{checkpointFrame}");
        world.TryGetGrid(out grid);
    }

    private IWarehouseFacility ResolveWarehouse() => world.Warehouses
        .Single(value => value != null
            && value.PersistentInstanceId.Value == warehouseId);

    private IWarehouseFacility ResolveOverflowWarehouse() => world.Warehouses
        .Single(value => value != null
            && value.PersistentInstanceId.Value == overflowWarehouseId);

    private IWarehouseFacility ResolveProductionInputWarehouse() => world.Warehouses
        .Single(value => value != null
            && value.PersistentInstanceId.Value == productionInputWarehouseId);

    private BuildableObject ResolveProducerFacility() => world.Buildings
        .Single(value => value != null
            && value.PersistentInstanceId.Value == producerFacilityId);

    private CharacterActor ResolveActor(string id) => world.AllCharacters
        .Select(CharacterActorCollection.GetCanonical)
        .FirstOrDefault(value => value != null
            && string.Equals(ActorId(value), id, StringComparison.Ordinal));

    private string CaptureSemanticHash() => HashText(CaptureSemanticText());

    private string CaptureSemanticText()
    {
        StringBuilder builder = new();
        foreach (WorldItemStackSnapshot stack in items.GetAllStacks()
                     .Where(value => value != null)
                     .OrderBy(value => value.StackId, StringComparer.Ordinal))
        {
            builder.Append("S|").Append(stack.StackId).Append('|')
                .Append(stack.ItemId).Append('|').Append(stack.Quantity).Append('|')
                .Append((int)stack.State).Append('|').Append(stack.Position.x)
                .Append(',').Append(stack.Position.y).Append('|')
                .Append(stack.DestinationId).Append('\n');
        }
        foreach (CharacterActor actor in LiveActors()
                     .OrderBy(ActorId, StringComparer.Ordinal))
        {
            Vector2Int position = actor.GetNowXY();
            builder.Append("A|").Append(ActorId(actor)).Append('|')
                .Append((int)actor.CurrentLifecycleState).Append('|')
                .Append(position.x).Append(',').Append(position.y).Append('|')
                .Append(actor.Brain?.RuntimeActionStartCount ?? 0L).Append('|')
                .Append(actor.Brain?.RuntimeImmediateReplanCount ?? 0L).Append('\n');
        }
        return builder.ToString();
    }

    private static string CaptureRandomHash(
        IEnumerable<RandomStreamDiagnosticSnapshot> snapshots) =>
        HashText(string.Join("\n", snapshots
            .OrderBy(value => value.StreamId, StringComparer.Ordinal)
            .Select(value => $"{value.StreamId}|{value.State}|{value.DrawCount}")));

    private void Finish()
    {
        if (finished)
            return;
        finished = true;
        try
        {
            clockDiagnostics?.DisableDeterministicCheckpointTime();
            if (saves != null
                && originalSave != null
                && !string.IsNullOrWhiteSpace(originalSaveJson))
            {
                DungeonGameSaveData restoreCandidate = saves.FromJson(originalSaveJson);
                bool restored = saves.TryRestore(
                    restoreCandidate,
                    out DungeonGameRestoreReport report);
                if (!restored)
                    failures.Add("ORIGINAL_WORLD_RESTORE:errors="
                        + string.Join(" | ", report?.Errors ?? Array.Empty<string>())
                        + ";warnings="
                        + string.Join(" | ", report?.Warnings ?? Array.Empty<string>()));
            }
        }
        catch (Exception exception)
        {
            failures.Add("ORIGINAL_WORLD_RESTORE:" + exception.Message);
        }
        try
        {
            if (debugModeConfigured && debugMode != null)
            {
                debugMode.SetCheat(
                    DungeonDebugCheat.FreezeNeeds,
                    originalFreezeNeeds);
                debugMode.SetCheat(
                    DungeonDebugCheat.FriendlyInvincible,
                    originalFriendlyInvincible);
                debugMode.SetCheat(
                    DungeonDebugCheat.PauseWildlifeAi,
                    originalPauseWildlifeAi);
            }
            if (developerModeConfigured && userSettings != null
                && userSettings.Current.developerMode != originalDeveloperMode)
            {
                userSettings.Update(value => value.developerMode = originalDeveloperMode);
            }
            if (gameSpeedConfigured && gameSpeed != null)
            {
                gameSpeed.SetSpeed(originalGameSpeed);
                gameSpeed.SetPaused(originalGamePause);
            }
            if (schedulerDiagnosticsConfigured && scheduler != null)
            {
                foreach (CharacterActor actor in LiveActors())
                    actor.Brain?.ConfigureLogisticsMeasurementForDiagnostics(false);
                scheduler.ConfigureDeterministicSimulationForDiagnostics(
                    originalSchedulerDeterministicMode);
            }
            if (spawnerDiagnosticsConfigured && characterSpawner != null)
            {
                characterSpawner.ConfigureDeterministicSimulationForDiagnostics(
                    originalSpawnerDiagnosticsPaused);
            }
        }
        catch (Exception exception)
        {
            failures.Add("ORIGINAL_DEBUG_STATE_RESTORE:" + exception.Message);
        }
        Application.logMessageReceived -= CaptureIssue;
        CurrentPhase = "finished";
        Time.timeScale = originalTimeScale;
        Time.captureDeltaTime = originalCaptureDeltaTime;
        Application.runInBackground = originalRunInBackground;
        WriteArtifacts();
        PublishCurrentProgress("FINISHED");
        EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
    }

    private void SetPhase(string phase)
    {
        CurrentPhase = phase ?? string.Empty;
        PublishCurrentProgress("RUNNING");
    }

    private void PublishCurrentProgress(string result)
    {
        V27PairedClutterPlayModeVerifier.PublishProgress(
            result,
            CurrentPhase,
            SeedCount,
            StartSeed,
            Focused,
            rows.Count,
            failures.Count,
            currentSourceDigestAtStart);
    }

    private void WriteArtifacts()
    {
        int completedSeeds = rows.Select(value => value.Seed).Distinct().Count();
        if (!runCompleted)
        {
            failures.Add(
                $"PAIRED_RUN_INCOMPLETE:requiredSeeds={requiredSeedCount};"
                + $"completedSeeds={completedSeeds};windows={rows.Count};floorRows={floorRows.Count}");
        }
        bool passed = runCompleted && failures.Count == 0 && consoleIssues.Count == 0;
        string pairedCsv = BuildPairedCsv();
        string floorCsv = BuildFloorCsv();
        string sourceDigest = V27PairedClutterPlayModeVerifier
            .ComputeEvidenceSourceDigest();
        string currentSourceDigest =
            V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest();
        string gameplaySceneSha256 =
            V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest();
        string report = $"RESULT={(passed ? "PASS" : "FAIL")}; seeds={completedSeeds};"
            + $" windows={rows.Count}; floorRows={floorRows.Count}; failures={failures.Count};"
            + $" consoleIssues={consoleIssues.Count}; sourceDigest={sourceDigest};"
            + $" currentSourceDigest={currentSourceDigest};"
            + $" gameplaySceneSha256={gameplaySceneSha256};"
            + $" pairedCsvSha256={HashText(pairedCsv)};"
            + $" floorCsvSha256={HashText(floorCsv)};\n"
            + BuildSuccessEvidence(passed, completedSeeds)
            + string.Join("\n", failures.Select(value => "FAIL\t" + value))
            + (consoleIssues.Count == 0 ? string.Empty : "\n" + string.Join("\n",
                consoleIssues.Select(value => "CONSOLE\t" + value))) + "\n";
        if (Focused)
        {
            WriteText(V27PairedClutterPlayModeVerifier.FocusedReportPath, report);
            WriteText(V27PairedClutterPlayModeVerifier.FocusedClutterCsvPath, floorCsv);
            WriteText(V27PairedClutterPlayModeVerifier.FocusedPairedCsvPath, pairedCsv);
        }
        else
        {
            WriteText(V27PairedClutterPlayModeVerifier.ReportPath, report);
            WriteText(V27PairedClutterPlayModeVerifier.PairedCsvPath, pairedCsv);
            WriteText(V27PairedClutterPlayModeVerifier.ClutterCsvPath, floorCsv);
        }
        Debug.Log(report);
    }

    private string BuildSuccessEvidence(bool passed, int completedSeeds)
    {
        if (!passed)
            return string.Empty;
        if (Focused)
        {
            return "PASS\tPAIRED_FOCUSED_FOUR_ARMS\tseeds=1;windows=16\n"
                + "PASS\tPAIRED_TIER_ZERO_PRODUCTION_RECONCILE\tlayout=29;capturePreflight=PASS\n"
                + "PASS\tPAIRED_RUN_CLEAN_REPEATABILITY_EXACT\tseeds=1\n"
                + "PASS\tPAIRED_RUN_EXOGENOUS_EVENTS_EXACT\tallWindows=true\n"
                + "PASS\tPAIRED_FOCUSED_CLUTTER_DELTA_BELOW_10_PERCENT\tseedLocal=true\n"
                + "PASS\tPAIRED_FOCUSED_BURST_QUANTITY_CONSERVED\tallRows=true\n"
                + "PASS\tPAIRED_BURST_WAIT_TYPED_AUTHORITY_JOIN\tinvalid=0\n"
                + $"PASS\tPAIRED_KEYED_PRODUCTION_BURST_APPLIED\tarms={productionBurstArmCount}\n"
                + BuildProducerBurstEvidence()
                + $"PASS\tPAIRED_PRODUCTION_BURST_HAUL_PRIORITY\tarms={productionPriorityArmCount}\n"
                + $"PASS\tPAIRED_HAULER_FAULT_AFTER_PHYSICAL_PICKUP\tarms={postPickupFaultArmCount}\n"
                + "PASS\tFLOOR_CLUTTER_ACCESS_EGRESS_ZERO\timmediateFailures=0\n"
                + "PASS\tFLOOR_CLUTTER_RECOVERY_ZERO\tpersistent=0\n"
                + $"PASS\tPAIRED_RUNTIME_HEADROOM_AT_LEAST_30_PERCENT\tminimumPermille="
                + $"{floorRows.Min(value => value.RuntimeHeadroomPermille)}\n"
                + "PASS\tRNG_CAUSAL_CONE_NO_CROSS_TALK\toutsideConeDivergence=0\n";
        }

        PairedRunAttributionAssessment assessment = finalAssessment
            ?? PairedRunAttributionEvaluator.Evaluate(rows);
        return "PASS\tPAIRED_TIER_ZERO_PRODUCTION_RECONCILE\tlayout=29;capturePreflight=PASS\n"
            + $"PASS\tPAIRED_RUN_CLEAN_REPEATABILITY_EXACT\tseeds={completedSeeds}\n"
            + "PASS\tPAIRED_RUN_EXOGENOUS_EVENTS_EXACT\tallWindows=true\n"
            + $"PASS\tPAIRED_CLUTTER_ATTRIBUTION\tsamples={assessment.SampleCount};"
            + $"medianPermille={assessment.MedianClutterDeltaPermille};"
            + $"p95Permille={assessment.P95ClutterDeltaPermille};"
            + $"maxPermille={assessment.MaximumClutterDeltaPermille};"
            + $"madPermille={assessment.MadPermille}\n"
            + "PASS\tPAIRED_BURST_QUANTITY_CONSERVED\tallRows=true\n"
            + "PASS\tPAIRED_BURST_WAIT_TYPED_AUTHORITY_JOIN\tinvalid=0\n"
            + $"PASS\tPAIRED_KEYED_PRODUCTION_BURST_APPLIED\tarms={productionBurstArmCount}\n"
            + BuildProducerBurstEvidence()
            + $"PASS\tPAIRED_PRODUCTION_BURST_HAUL_PRIORITY\tarms={productionPriorityArmCount}\n"
            + $"PASS\tPAIRED_HAULER_FAULT_AFTER_PHYSICAL_PICKUP\tarms={postPickupFaultArmCount}\n"
            + "PASS\tFLOOR_CLUTTER_ACCESS_EGRESS_ZERO\timmediateFailures=0\n"
            + "PASS\tFLOOR_CLUTTER_RECOVERY_ZERO\tpersistent=0\n"
            + $"PASS\tPAIRED_RUNTIME_HEADROOM_AT_LEAST_30_PERCENT\tminimumPermille="
            + $"{floorRows.Min(value => value.RuntimeHeadroomPermille)}\n"
            + "PASS\tRNG_CAUSAL_CONE_NO_CROSS_TALK\toutsideConeDivergence=0\n";
    }

    private string BuildProducerBurstEvidence()
    {
        StringBuilder builder = new();
        if (facilityBurstArmCount > 0)
        {
            builder.Append("PASS\tPAIRED_FACILITY_OUTPUT_BURST_PRODUCTION\tarms=")
                .Append(facilityBurstArmCount).Append('\n');
        }
        if (cropHarvestBurstArmCount > 0)
        {
            builder.Append("PASS\tPAIRED_CROP_HARVEST_BURST_PRODUCTION\tarms=")
                .Append(cropHarvestBurstArmCount).Append('\n');
        }
        if (miningBurstArmCount > 0)
        {
            builder.Append("PASS\tPAIRED_MINING_BURST_PRODUCTION\tarms=")
                .Append(miningBurstArmCount).Append('\n');
        }
        return builder.ToString();
    }

    private void ValidateProductionInterventionEvidence()
    {
        int expectedFaultArms = checked(requiredSeedCount * 2);
        Check(productionBurstArmCount == expectedFaultArms,
            "PAIRED_KEYED_PRODUCTION_BURST_APPLIED",
            $"expectedArms={expectedFaultArms};actualArms={productionBurstArmCount}");
        if (Focused)
        {
            BurstProducerKind expected = SelectBurstProducer(StartSeed);
            int actual = expected switch
            {
                BurstProducerKind.FacilityOutput => facilityBurstArmCount,
                BurstProducerKind.CropHarvest => cropHarvestBurstArmCount,
                _ => miningBurstArmCount
            };
            Check(actual == expectedFaultArms,
                "PAIRED_FOCUSED_PRODUCER_KIND_EXACT",
                $"seed={StartSeed};producer={expected};"
                + $"expectedArms={expectedFaultArms};actualArms={actual}");
        }
        else
        {
            Check(facilityBurstArmCount > 0
                    && cropHarvestBurstArmCount > 0
                    && miningBurstArmCount > 0
                    && facilityBurstArmCount + cropHarvestBurstArmCount
                        + miningBurstArmCount == expectedFaultArms,
                "PAIRED_ALL_PRODUCTION_BURST_KINDS_EXACT",
                $"facility={facilityBurstArmCount};crop={cropHarvestBurstArmCount};"
                + $"mining={miningBurstArmCount};expected={expectedFaultArms}");
        }
        Check(productionPriorityArmCount == expectedFaultArms,
            "PAIRED_PRODUCTION_BURST_HAUL_PRIORITY",
            $"expectedArms={expectedFaultArms};actualArms={productionPriorityArmCount}");
        Check(postPickupFaultArmCount == expectedFaultArms,
            "PAIRED_HAULER_FAULT_AFTER_PHYSICAL_PICKUP",
            $"expectedArms={expectedFaultArms};actualArms={postPickupFaultArmCount}");
    }

    private string BuildPairedCsv()
    {
        StringBuilder builder = new(
            "seed,arm,window,travelMilliWu,waitMilliWu,dispatchWaitMilliWu,reservationWaitMilliWu,facilityAccessWaitMilliWu,noPathMilliWu,burstDeliveredQuantity,burstOutstandingQuantity,burstQuantityConserved,replanCount,stepAsideCount,clutterCellSeconds,semanticStateHash,randomStateHash,exogenousEventHash\r\n");
        foreach (PairedRunWindowResult row in rows
                     .OrderBy(value => value.Seed)
                     .ThenBy(value => value.Arm, StringComparer.Ordinal)
                     .ThenBy(value => value.WindowIndex))
        {
            builder.Append(row.Seed).Append(',').Append(row.Arm).Append(',')
                .Append(row.WindowIndex).Append(',').Append(row.TravelMilliWu).Append(',')
                .Append(row.WaitMilliWu).Append(',').Append(row.DispatchWaitMilliWu).Append(',')
                .Append(row.ReservationWaitMilliWu).Append(',')
                .Append(row.FacilityAccessWaitMilliWu).Append(',')
                .Append(row.NoPathMilliWu).Append(',')
                .Append(row.BurstDeliveredQuantity).Append(',')
                .Append(row.BurstOutstandingQuantity).Append(',')
                .Append(row.BurstQuantityConserved ? "true" : "false").Append(',')
                .Append(row.ReplanCount).Append(',')
                .Append(row.StepAsideCount).Append(',').Append(row.ClutterCellSeconds).Append(',')
                .Append(row.SemanticStateHash).Append(',').Append(row.RandomStateHash).Append(',')
                .Append(row.ExogenousEventHash).Append("\r\n");
        }
        return builder.ToString();
    }

    private string BuildFloorCsv()
    {
        StringBuilder builder = new(
            "seed,arm,window,isRecovery,graceSeconds,looseStacks,looseQuantity,outsideContainment,persistent,immediateFailures,clutterCellSeconds,runtimeHeadroomPermille,runtimeErosionCells,runtimeErosionDetail\r\n");
        foreach (FloorRow row in floorRows
                     .OrderBy(value => value.Seed)
                     .ThenBy(value => value.Arm, StringComparer.Ordinal)
                     .ThenBy(value => value.Window))
        {
            builder.Append(row.Seed).Append(',').Append(row.Arm).Append(',')
                .Append(row.Window).Append(',').Append(row.IsRecovery ? "true" : "false").Append(',')
                .Append(row.GraceSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(row.LooseStacks).Append(',').Append(row.LooseQuantity).Append(',')
                .Append(row.OutsideContainment).Append(',').Append(row.Persistent).Append(',')
                .Append(row.ImmediateFailures).Append(',').Append(row.ClutterCellSeconds)
                .Append(',').Append(row.RuntimeHeadroomPermille)
                .Append(',').Append(row.RuntimeErosionCells)
                .Append(',').Append(row.RuntimeErosionDetail)
                .Append("\r\n");
        }
        return builder.ToString();
    }

    private static void WriteText(string path, string text)
    {
        V27BalanceArtifactWriter.WriteIfDifferent(path, stream =>
        {
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        });
    }

    private IEnumerator ExecuteGuarded(IEnumerator routine)
    {
        Stack<IEnumerator> stack = new();
        stack.Push(routine);
        while (stack.Count > 0)
        {
            bool moved;
            object current;
            try
            {
                IEnumerator active = stack.Peek();
                moved = active.MoveNext();
                current = moved ? active.Current : null;
            }
            catch (Exception exception)
            {
                failures.Add(exception.GetType().Name + ":" + exception.Message);
                yield break;
            }
            if (!moved)
            {
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

    private void CaptureIssue(string condition, string stackTrace, LogType type)
    {
        if (type is LogType.Warning or LogType.Error or LogType.Exception or LogType.Assert)
            consoleIssues.Add(type + ":" + condition);
    }

    private bool Check(bool condition, string key, string detail)
    {
        if (!condition)
            failures.Add(key + ":" + detail);
        return condition;
    }

    private void Fail(string key, string detail) => failures.Add(key + ":" + detail);

    private void FailOnce(string key, string detail)
    {
        string prefix = key + ":";
        if (!failures.Any(value => value.StartsWith(
                prefix,
                StringComparison.Ordinal)))
        {
            failures.Add(prefix + detail);
        }
    }

    private T Resolve<T>() where T : class
    {
        try
        {
            return scope?.Container?.Resolve<T>();
        }
        catch
        {
            return null;
        }
    }

    private void Inject(GameObject target)
    {
        foreach (MonoBehaviour component in target.GetComponentsInChildren<MonoBehaviour>(true))
            scope.Container.Inject(component);
    }

    private static BuildingSO FindWarehouseAsset()
    {
        return AssetDatabase.FindAssets(
                "t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(value => value != null
                && value.GetStorageCapacity() > 0
                && value.StoresAllCategories())
            .OrderByDescending(value => value.GetStorageCapacity())
            .ThenBy(value => value.width * value.height)
            .ThenBy(value => value.name, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static string DescribePlacementCandidates(
        BuildingSO definition,
        IEnumerable<Vector2Int> candidates) => string.Join(
        "|",
        candidates.Select(anchor =>
            $"{anchor.x},{anchor.y}["
            + string.Join(
                ",",
                definition.GetGridPosList(anchor)
                    .OrderBy(value => value.x)
                    .ThenBy(value => value.y)
                    .Select(value => $"{value.x},{value.y}"))
            + "]"));

    private string DescribeExistingFixtureAuthorities()
    {
        string warehouses = string.Join(
            "|",
            world.Warehouses
                .Where(value => value?.Inventory != null)
                .OrderBy(value => value.PersistentInstanceId.Value,
                    StringComparer.Ordinal)
                .Select(value => value is BuildableObject building
                    ? $"{value.PersistentInstanceId.Value}:{building.BuildingData?.name}:"
                        + $"{building.centerPos}:all={building.BuildingData?.StoresAllCategories()}:"
                        + $"food={value.Inventory.Accepts(StockCategory.Food)}:"
                        + $"general={value.Inventory.Accepts(StockCategory.General)}:"
                        + $"mass={value.Inventory.MaxMassGrams}"
                    : $"{value.PersistentInstanceId.Value}:non-building"));
        string cooks = string.Join(
            "|",
            world.Buildings
                .OfType<Facility>()
                .Where(value => value?.BuildingData?.Facility != null
                    && value.BuildingData.Facility.SupportsWork(
                        BuiltInWorkTypeIds.Cook))
                .OrderBy(value => value.PersistentInstanceId.Value,
                    StringComparer.Ordinal)
                .Select(value =>
                    $"{value.PersistentInstanceId.Value}:{value.BuildingData.name}:"
                    + value.centerPos));
        return $"warehouses=[{warehouses}],cooks=[{cooks}]";
    }

    private static BuildingSO FindCookFacilityAsset()
    {
        return AssetDatabase.FindAssets(
                "t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(value => value?.Facility != null
                && value.Facility.SupportsWork(BuiltInWorkTypeIds.Cook)
                && value.GetAbility<BuildingCookingAbility>() is
                {
                    requiresFuel: false,
                    cookedMeals: 1
                })
            .OrderBy(value => value.width * value.height)
            .ThenBy(value => value.id)
            .ThenBy(value => value.name, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private SeedLotState FindSeedLot(string seedItemId, string cropId)
    {
        foreach (WorldItemStackSnapshot stack in items.GetAllStacks()
                     .Where(value => value != null
                         && value.Quantity > 0
                         && string.Equals(
                             value.ItemId,
                             seedItemId,
                             StringComparison.Ordinal))
                     .OrderBy(value => value.StackId, StringComparer.Ordinal))
        {
            SeedLotState seedLot = SeedLotItemStateCodec.Decode(stack.Components);
            if (string.Equals(seedLot.cropId, cropId, StringComparison.Ordinal))
                return seedLot.Clone();
        }

        return null;
    }

    private static BuildingSO FindCropPlotAsset()
    {
        return AssetDatabase.FindAssets(
                "t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(value => value?.GetAbility<BuildingFacilityPartAbility>()?.code == "P23"
                && value.GetAbility<BuildingCropPlotAbility>() is { Indoor: false }
                && value.Facility?.SupportsWork(BuiltInWorkTypeIds.Sow) == true
                && value.Facility.SupportsWork(BuiltInWorkTypeIds.Harvest))
            .SingleOrDefault();
    }

    private BuildableObject ResolveCropPlot() => world.Buildings
        .Where(value => value != null)
        .FirstOrDefault(value => string.Equals(
            value.PersistentInstanceId.Value,
            cropPlotId,
            StringComparison.Ordinal));

    private WorldResourceNode ResolveMiningNode() => worldResources.Nodes
        .Where(value => value != null)
        .FirstOrDefault(value => string.Equals(
            value.NodeId,
            miningNodeId,
            StringComparison.Ordinal));

    private CharacterActor[] EligibleActors() => world?.Characters
        .Select(CharacterActorCollection.GetCanonical)
        .Where(value => value != null && !value.IsDead
            && value.characterType is not CharacterType.Customer
                and not CharacterType.Intruder
            && value.CurrentLifecycleState == CharacterLifecycleState.Active)
        .Distinct()
        .ToArray() ?? FindObjectsByType<CharacterActor>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Select(CharacterActorCollection.GetCanonical)
            .Where(value => value != null && !value.IsDead
                && value.characterType is not CharacterType.Customer
                    and not CharacterType.Intruder
                && value.CurrentLifecycleState == CharacterLifecycleState.Active)
            .Distinct().ToArray();

    private CharacterActor[] LiveActors()
    {
        CharacterActor[] eligible = EligibleActors();
        return measuredActorIds.Count == 0
            ? eligible
            : eligible.Where(value => measuredActorIds.Contains(ActorId(value)))
                .ToArray();
    }

    private static string ActorId(CharacterActor actor) =>
        actor?.Identity?.PersistentId ?? string.Empty;

    private void ApplyMeasurementIsolation(bool activateMeasuredActors = true)
    {
        gameSpeed.SetSpeed(5);
        gameSpeed.SetPaused(!activateMeasuredActors);
        Time.timeScale = activateMeasuredActors ? VerificationTimeScale : 0f;
        debugMode.SetCheat(DungeonDebugCheat.FreezeNeeds, true);
        debugMode.SetCheat(DungeonDebugCheat.FriendlyInvincible, true);
        debugMode.SetCheat(DungeonDebugCheat.PauseWildlifeAi, true);
        bool isolated = gameSpeed.IsPaused == !activateMeasuredActors
            && debugMode.IsCheatEnabled(DungeonDebugCheat.FreezeNeeds)
            && debugMode.IsCheatEnabled(DungeonDebugCheat.FriendlyInvincible);
        isolated &= debugMode.IsCheatEnabled(DungeonDebugCheat.PauseWildlifeAi);
        Check(isolated, "PAIRED_DEBUG_ISOLATION",
            $"speed={gameSpeed.Speed};paused={gameSpeed.IsPaused};"
            + $"developer={debugMode.IsDeveloperModeEnabled};"
            + $"freeze={debugMode.IsCheatEnabled(DungeonDebugCheat.FreezeNeeds)};"
            + $"invincible={debugMode.IsCheatEnabled(DungeonDebugCheat.FriendlyInvincible)};"
            + $"wildlifePaused={debugMode.IsCheatEnabled(DungeonDebugCheat.PauseWildlifeAi)}");
        if (!isolated)
            return;

        CharacterActor[] actors = LiveActors()
            .OrderBy(ActorId, StringComparer.Ordinal)
            .ToArray();
        HashSet<CharacterActor> measuredActors = actors.ToHashSet();
        foreach (CharacterActor unrelated in world.Characters
                     .Select(CharacterActorCollection.GetCanonical)
                     .Where(value => value != null
                         && !measuredActors.Contains(value))
                     .Distinct())
        {
            unrelated.SetAiPaused(true);
            unrelated.Brain?.StopAllAiForLifecycleTransition(
                "v27-paired-unrelated-actor-isolation");
            unrelated.GetComponent<AbilityMove>()?.CancelActiveMovement(
                "v27-paired-unrelated-actor-isolation");
        }
        foreach (CharacterActor actor in actors)
        {
            actor.Brain?.ConfigureLogisticsMeasurementForDiagnostics(true);
            actor.SetAiPaused(true);
            actor.Brain?.StopAllAiForLifecycleTransition(
                "v27-paired-checkpoint-reset");
            actor.GetComponent<AbilityMove>()?.CancelActiveMovement(
                "v27-paired-checkpoint-reset");
            actor.GetComponent<AbilityShopping>()?.StopShopping(
                "v27-paired-checkpoint-reset");
            actor.GetComponent<AbilityHaul>()?.StopHauling(
                "v27-paired-checkpoint-reset");
        }
        foreach (CharacterActor actor in actors)
        {
            ResetActorForLogisticsMeasurement(actor);
            if (activateMeasuredActors)
            {
                actor.SetAiPaused(false);
                actor.Brain?.RequestImmediateReplan(clearFailures: true);
            }
        }

        Check(actors.All(actor =>
                actor.Brain?.LogisticsMeasurementOnlyForDiagnostics == true),
            "PAIRED_LOGISTICS_ONLY_CANDIDATE_SCOPE",
            $"actors={actors.Length};enabled="
            + string.Join(",", actors.Select(actor =>
                $"{ActorId(actor)}:{actor.Brain?.LogisticsMeasurementOnlyForDiagnostics}")));
    }

    private void QuiesceActorsForCheckpoint()
    {
        foreach (CharacterActor actor in LiveActors()
                     .OrderBy(ActorId, StringComparer.Ordinal))
        {
            actor.SetAiPaused(true);
            actor.Brain?.StopAllAiForLifecycleTransition(
                "v27-paired-checkpoint-capture");
            actor.GetComponent<AbilityMove>()?.CancelActiveMovement(
                "v27-paired-checkpoint-capture");
            actor.GetComponent<AbilityShopping>()?.StopShopping(
                "v27-paired-checkpoint-capture");
            actor.GetComponent<AbilityHaul>()?.StopHauling(
                "v27-paired-checkpoint-capture");
        }
    }

    private void PrepareActorsForArmMeasurementBoundary()
    {
        CharacterActor[] actors = LiveActors()
            .OrderBy(ActorId, StringComparer.Ordinal)
            .ToArray();
        foreach (CharacterActor actor in actors)
        {
            actor.SetAiPaused(true);
            actor.Brain?.StopAllAiForLifecycleTransition(
                "v27-paired-arm-measurement-boundary");
            actor.GetComponent<AbilityMove>()?.CancelActiveMovement(
                "v27-paired-arm-measurement-boundary");
            actor.GetComponent<AbilityShopping>()?.StopShopping(
                "v27-paired-arm-measurement-boundary");
            actor.GetComponent<AbilityHaul>()?.StopHauling(
                "v27-paired-arm-measurement-boundary");
        }

        bool isolated = actors.Length > 0
            && actors.All(actor => actor.IsAiPaused()
                && actor.Brain?.HasRunningAction != true
                && actor.GetComponent<AbilityMove>()
                    ?.HasActiveMovementRoutineForDiagnostics != true
                && actor.GetComponent<AbilityHaul>()?.IsHauling != true);
        Check(isolated,
            "PAIRED_ARM_MEASUREMENT_BOUNDARY_ISOLATED",
            $"actors={actors.Length};state={DescribeActors()}");
    }

    private void ResumeAllMeasuredActors()
    {
        foreach (CharacterActor actor in LiveActors()
                     .OrderBy(ActorId, StringComparer.Ordinal))
        {
            actor.SetAiPaused(false);
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
        }
        scheduler.ResetDecisionQueueForDiagnostics();
    }

    private void ValidateFocusedCleanRepeatability()
    {
        PairedRunWindowResult[] left = rows
            .Where(value => value.Arm == "cleanRepeatA")
            .OrderBy(value => value.WindowIndex)
            .ToArray();
        PairedRunWindowResult[] right = rows
            .Where(value => value.Arm == "cleanRepeatB")
            .OrderBy(value => value.WindowIndex)
            .ToArray();
        bool exact = left.Length == 4 && right.Length == 4;
        string mismatch = string.Empty;
        int seed = left.FirstOrDefault()?.Seed
            ?? right.FirstOrDefault()?.Seed
            ?? 1;
        string leftStartRandom = armStartRandomHashes.GetValueOrDefault(
            $"{seed}|cleanRepeatA", string.Empty);
        string rightStartRandom = armStartRandomHashes.GetValueOrDefault(
            $"{seed}|cleanRepeatB", string.Empty);
        string leftStartSemantic = armStartSemanticHashes.GetValueOrDefault(
            $"{seed}|cleanRepeatA", string.Empty);
        string rightStartSemantic = armStartSemanticHashes.GetValueOrDefault(
            $"{seed}|cleanRepeatB", string.Empty);
        string startDifference = FindFirstLineDifference(
            armStartSemanticTexts.GetValueOrDefault(
                $"{seed}|cleanRepeatA", string.Empty),
            armStartSemanticTexts.GetValueOrDefault(
                $"{seed}|cleanRepeatB", string.Empty));
        bool startExact = string.Equals(leftStartRandom, rightStartRandom,
                StringComparison.Ordinal)
            && string.Equals(leftStartSemantic, rightStartSemantic,
                StringComparison.Ordinal);
        exact &= startExact;
        if (!startExact)
        {
            mismatch = $"startRandom={leftStartRandom}/{rightStartRandom};"
                + $"startSemantic={leftStartSemantic}/{rightStartSemantic};"
                + $"startFirstDifference={startDifference};";
        }
        bool windowsExact = left.Length == 4 && right.Length == 4;
        for (int index = 0; windowsExact && index < left.Length; index++)
        {
            PairedRunWindowResult a = left[index];
            PairedRunWindowResult b = right[index];
            windowsExact = a.TravelMilliWu == b.TravelMilliWu
                && a.WaitMilliWu == b.WaitMilliWu
                && a.DispatchWaitMilliWu == b.DispatchWaitMilliWu
                && a.ReservationWaitMilliWu == b.ReservationWaitMilliWu
                && a.FacilityAccessWaitMilliWu == b.FacilityAccessWaitMilliWu
                && a.NoPathMilliWu == b.NoPathMilliWu
                && a.BurstDeliveredQuantity == b.BurstDeliveredQuantity
                && a.BurstOutstandingQuantity == b.BurstOutstandingQuantity
                && a.BurstQuantityConserved == b.BurstQuantityConserved
                && a.ReplanCount == b.ReplanCount
                && a.StepAsideCount == b.StepAsideCount
                && a.ClutterCellSeconds == b.ClutterCellSeconds
                && string.Equals(a.SemanticStateHash, b.SemanticStateHash,
                    StringComparison.Ordinal)
                && string.Equals(a.RandomStateHash, b.RandomStateHash,
                    StringComparison.Ordinal)
                && string.Equals(a.ExogenousEventHash, b.ExogenousEventHash,
                    StringComparison.Ordinal);
            if (!windowsExact)
            {
                mismatch += $"window={index};travel={a.TravelMilliWu}/{b.TravelMilliWu};"
                    + $"wait={a.WaitMilliWu}/{b.WaitMilliWu};replan={a.ReplanCount}/{b.ReplanCount};"
                    + $"stepAside={a.StepAsideCount}/{b.StepAsideCount};"
                    + $"clutter={a.ClutterCellSeconds}/{b.ClutterCellSeconds};"
                    + $"semantic={a.SemanticStateHash}/{b.SemanticStateHash};"
                    + $"random={a.RandomStateHash}/{b.RandomStateHash};"
                    + $"randomFirstDifference={FindFirstRandomDifference(a.Seed, index)};"
                    + $"warmupFirstDifference={FindFirstFrameDifference(a.Seed, -1)};"
                    + $"frameFirstDifference={FindFirstFrameDifference(a.Seed, index)};"
                    + $"event={a.ExogenousEventHash}/{b.ExogenousEventHash}";
            }
        }
        exact &= windowsExact;
        Check(exact, "PAIRED_RUN_CLEAN_REPEATABILITY", mismatch.Length == 0
            ? "windows=4;exact=true"
            : mismatch);
    }

    private void ValidateFocusedClutterDelta()
    {
        long controlWait = rows
            .Where(value => string.Equals(
                value.Arm,
                "faultControl",
                StringComparison.Ordinal))
            .Sum(value => value.WaitMilliWu);
        long stressWait = rows
            .Where(value => string.Equals(
                value.Arm,
                "clutterStress",
                StringComparison.Ordinal))
            .Sum(value => value.WaitMilliWu);
        long denominator = Math.Max(controlWait, 1L);
        long deltaPermille = checked(
            (stressWait - controlWait) * 1000L) / denominator;
        Check(deltaPermille < 100L,
            "PAIRED_FOCUSED_CLUTTER_DELTA_BELOW_10_PERCENT",
            $"controlWaitMilliWu={controlWait};stressWaitMilliWu={stressWait};"
            + $"deltaPermille={deltaPermille};limitExclusive=100");
    }

    private string FindFirstRandomDifference(int seed, int window)
    {
        IReadOnlyList<RandomStreamDiagnosticSnapshot> left =
            randomByArmWindow[$"{seed}|cleanRepeatA|{window}"];
        IReadOnlyList<RandomStreamDiagnosticSnapshot> right =
            randomByArmWindow[$"{seed}|cleanRepeatB|{window}"];
        Dictionary<string, RandomStreamDiagnosticSnapshot> rightById = right
            .ToDictionary(value => value.StreamId, StringComparer.Ordinal);
        foreach (RandomStreamDiagnosticSnapshot value in left)
        {
            if (!rightById.TryGetValue(value.StreamId, out RandomStreamDiagnosticSnapshot other))
                return value.StreamId + ":missing-right";
            if (value.State != other.State || value.DrawCount != other.DrawCount)
                return $"{value.StreamId}:{value.State}/{value.DrawCount}!={other.State}/{other.DrawCount}";
        }
        HashSet<string> leftIds = left.Select(value => value.StreamId)
            .ToHashSet(StringComparer.Ordinal);
        string onlyRight = right.Select(value => value.StreamId)
            .FirstOrDefault(value => !leftIds.Contains(value));
        return onlyRight ?? "none";
    }

    private string FindFirstFrameDifference(int seed, int window)
    {
        List<string> left = focusedFrameTraces.GetValueOrDefault(
            $"{seed}|cleanRepeatA|{window}", new List<string>());
        List<string> right = focusedFrameTraces.GetValueOrDefault(
            $"{seed}|cleanRepeatB|{window}", new List<string>());
        int count = Math.Max(left.Count, right.Count);
        for (int index = 0; index < count; index++)
        {
            string leftValue = index < left.Count ? left[index] : "<missing>";
            string rightValue = index < right.Count ? right[index] : "<missing>";
            if (!string.Equals(leftValue, rightValue, StringComparison.Ordinal))
                return $"frame={index}:{leftValue}!={rightValue}";
        }
        return "none";
    }

    private string CaptureFocusedFrameTrace(float elapsed)
    {
        StringBuilder builder = new();
        builder.Append("elapsed=").Append(elapsed.ToString("0.###",
                System.Globalization.CultureInfo.InvariantCulture))
            .Append("|clock=").Append(clock.Time.ToString("0.###",
                System.Globalization.CultureInfo.InvariantCulture))
            .Append('|').Append(clock.FrameCount)
            .Append("|scheduler=").Append(scheduler.LastProcessedDecisionCount)
            .Append('/').Append(scheduler.LastBehaviorTreeTickCount);
        foreach (CharacterActor actor in LiveActors()
                     .OrderBy(ActorId, StringComparer.Ordinal))
        {
            Vector2Int position = actor.GetNowXY();
            AbilityMove move = actor.GetComponent<AbilityMove>();
            AbilityHaul haul = actor.GetComponent<AbilityHaul>();
            CharacterAiDecisionTickResult decision =
                scheduler.GetLastDecisionResultForDiagnostics(actor);
            builder.Append("|A:").Append(ActorId(actor)).Append(':')
                .Append(position.x).Append(',').Append(position.y).Append(':')
                .Append(actor.Brain?.CurrentActionDebugLabel).Append(':')
                .Append(actor.Brain?.RuntimeActionStartCount ?? 0L).Append(':')
                .Append(actor.Brain?.RuntimeImmediateReplanCount ?? 0L).Append(':')
                .Append(actor.CanRunAi ? 'R' : '-')
                .Append(actor.IsAiPaused() ? 'P' : '-')
                .Append(actor.Brain?.HasResumableDecisionPipeline == true ? 'D' : '-')
                .Append(':').Append(decision.Handled ? 'H' : '-')
                .Append('/').Append(decision.Branch)
                .Append('/').Append(decision.Task)
                .Append('/').Append(decision.Status)
                .Append(':').Append(actor.Blackboard?.LastDecisionTrace)
                .Append(':')
                .Append(move?.HasActiveMovementRoutineForDiagnostics == true ? 'M' : '-')
                .Append(haul?.IsHauling == true ? 'H' : '-');
        }
        foreach (RandomStreamDiagnosticSnapshot value in randomDiagnostics.Capture()
                     .Where(value => value.StreamId.StartsWith(
                         "character-", StringComparison.Ordinal))
                     .OrderBy(value => value.StreamId, StringComparer.Ordinal))
        {
            builder.Append("|R:").Append(value.StreamId).Append(':')
                .Append(value.State).Append(':').Append(value.DrawCount);
        }
        builder.Append("|W:").Append(CaptureSemanticHash());
        return builder.ToString();
    }

    private static string FindFirstLineDifference(string left, string right)
    {
        string[] leftLines = (left ?? string.Empty).Split('\n');
        string[] rightLines = (right ?? string.Empty).Split('\n');
        int count = Math.Max(leftLines.Length, rightLines.Length);
        for (int index = 0; index < count; index++)
        {
            string leftLine = index < leftLines.Length ? leftLines[index] : "<missing>";
            string rightLine = index < rightLines.Length ? rightLines[index] : "<missing>";
            if (!string.Equals(leftLine, rightLine, StringComparison.Ordinal))
                return $"line={index}:{leftLine}!={rightLine}";
        }
        return "none";
    }

    private int CaptureRuntimeHeadroomPermille()
    {
        FloorClutterAssessment current = clutter.Capture(
            grid,
            layout,
            Math.Max(0f, clock.Time - commonCheckpointTime));
        HashSet<Vector2Int> dynamicErosion = current.OutsideContainment
            .Where(value => value.Quantity > 0)
            .Select(value => value.Position)
            .ToHashSet();

        CharacterActor[] actors = LiveActors();
        dynamicErosion.UnionWith(actors
            .Where(value => string.Equals(
                value.Brain?.CurrentActionDebugLabel,
                "길 비켜주기",
                StringComparison.Ordinal))
            .Select(value => value.GetNowXY())
            .Where(value => (layout.GetRoles(value) & (
                SpatialCellRole.OperationalAccess
                | SpatialCellRole.QueueAccess
                | SpatialCellRole.SharedCorridor)) == 0));
        dynamicErosion.UnionWith(actors
            .Select(value => value.GetComponent<AbilityMove>()
                ?.ActiveSystemMoveDestinationForDiagnostics)
            .Where(value => value.HasValue)
            .Select(value => value.Value)
            .GroupBy(value => value)
            .Where(value => value.Count() > 1)
            .Select(value => value.Key)
            .Where(value => (layout.GetRoles(value) & (
                SpatialCellRole.OperationalAccess
                | SpatialCellRole.QueueAccess
                | SpatialCellRole.SharedCorridor)) == 0));

        if (!DungeonSpaceGridLayout.TryCapture(
                grid,
                out DungeonInteriorLayoutSnapshot currentInterior,
                out string layoutFailure))
        {
            throw new InvalidOperationException(
                "RUNTIME_HEADROOM_LAYOUT_INVALID:" + layoutFailure);
        }
        int currentStagePopulation = PopulationStagePortfolioCatalog
            .PopulationStages
            .Where(population => PopulationStagePortfolioCatalog
                .InteriorColumnsForPopulation(population)
                <= currentInterior.ColumnCount)
            .DefaultIfEmpty(PopulationStagePortfolioCatalog.PopulationStages[0])
            .Max();
        int minimum = V27PopulationStageSpatialBaseline
            .RuntimeHeadroomPermille(
                currentStagePopulation,
                dynamicErosion.Count);
        lastRuntimeHeadroomErosionCount = dynamicErosion.Count;
        lastRuntimeHeadroomErosionDetail = string.Join("|", dynamicErosion
            .OrderBy(value => value.x)
            .ThenBy(value => value.y)
            .Select(value => $"{value}:{layout.GetRoles(value)}"));
        if (minimum < 0 || minimum > 1000)
            throw new InvalidOperationException("RUNTIME_HEADROOM_AUTHORITY_INVALID");
        return minimum;
    }

    private void ResetActorForLogisticsMeasurement(CharacterActor actor)
    {
        if (actor?.Stats == null)
        {
            Fail("PAIRED_ACTOR_NEUTRALIZATION", $"actor={ActorId(actor)};stats=false");
            return;
        }

        Dictionary<CharacterCondition, float> values = actor.Stats.StatSnapshot
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        values[CharacterCondition.HUNGER] = 85f;
        values[CharacterCondition.THIRST] = 100f;
        values[CharacterCondition.SLEEP] = 100f;
        values[CharacterCondition.FUN] = 80f;
        values[CharacterCondition.EXCRETION] = 100f;
        values[CharacterCondition.HYGIENE] = 100f;
        values[CharacterCondition.MOOD] = 75f;
        actor.Stats.RestorePersistentState(
            values,
            actor.CurrentHealth,
            actor.InjurySeverity,
            75f,
            Array.Empty<CharacterMoodFactorSnapshot>());
        bool reset = deprivation.DebugResetForDeterministicScenario(actor);
        Check(reset, "PAIRED_ACTOR_NEUTRALIZATION",
            $"actor={ActorId(actor)};deprivationReset={reset}");
    }

    private static int Manhattan(Vector2Int left, Vector2Int right) =>
        Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);

    private void EnsureVerificationTimeScale()
    {
        if (gameSpeed?.IsPaused == true)
            gameSpeed.SetPaused(false);
        if (Time.timeScale < VerificationTimeScale)
            Time.timeScale = VerificationTimeScale;
    }

    private static bool IsAffectedActorStream(
        string streamId,
        ISet<string> affected)
    {
        foreach (string actorId in affected)
        {
            if (string.Equals(streamId, "character-ai:" + actorId, StringComparison.Ordinal)
                || string.Equals(streamId, "character-movement:" + actorId, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private string DescribeOutside(FloorClutterAssessment assessment) =>
        string.Join(";", assessment.OutsideContainment.Select(value =>
            $"{value.StackId}/{items.GetAllStacks().FirstOrDefault(stack => stack.StackId == value.StackId)?.ItemId}"
            + $"@{value.Position}:q{value.Quantity}:age{value.AgeSeconds:0.##}:"
            + $"area={grid.GetGridCell(value.Position)?.AreaType}:"
            + $"roles={value.Roles}:persistent={value.Persistent}"));

    private string DescribeActors() => string.Join(";", LiveActors()
        .OrderBy(ActorId, StringComparer.Ordinal)
        .Select(actor =>
        {
            AbilityMove move = actor.GetComponent<AbilityMove>();
            AbilityHaul haul = actor.GetComponent<AbilityHaul>();
            WorldItemHaulPlan plan = null;
            string reason = string.Empty;
            bool preview = haul?.IsHauling != true && haulPlanning.TryPreviewBestPlan(
                actor, out plan, out reason);
            if (haul?.IsHauling == true)
            {
                plan = null;
                reason = "skipped-live-haul";
            }
            string continuation = string.Empty;
            string stopReason = string.Empty;
            bool canContinue = actor.Brain != null
                && actor.Brain.CanContinueCurrentAction(out continuation);
            bool shouldStop = actor.Brain != null
                && actor.Brain.ShouldStopCurrentActionForReplan(out stopReason);
            GridPathSearchBroker actorPaths =
                actor.PathSearchBroker as GridPathSearchBroker;
            bool samePathBroker = ReferenceEquals(
                actor.PathSearchBroker,
                scheduler?.PathSearchBrokerForDiagnostics);
            return $"{ActorId(actor)}:{actor.CurrentLifecycleState}:"
                + $"instance={actor.GetInstanceID()}:"
                + $"active={actor.gameObject.activeInHierarchy}/{actor.isActiveAndEnabled}:"
                + $"canRun={actor.CanRunAi}:published={actor.HasBeenPublished}:"
                + $"detached={actor.IsDetachedRestoreCandidate}:"
                + $"unpublished={actor.IsUnpublishedComposition}:"
                + $"pos={actor.GetNowXY()}:"
                + $"action={actor.Brain?.CurrentActionDebugLabel}:"
                + $"running={actor.Brain?.HasRunningAction}:"
                + $"ended={actor.Brain?.isBestActionEnd}:"
                + $"continue={canContinue}/{continuation}:"
                + $"stop={shouldStop}/{stopReason}:"
                + $"haul={haul?.IsHauling == true}:move={move?.HasActiveMovementRoutineForDiagnostics == true}:"
                + $"haulComponents={actor.GetComponents<AbilityHaul>().Length}:"
                + $"haulEnabled={haul?.enabled}/{haul?.isActiveAndEnabled}:"
                + $"haulRoutine={haul?.HasHaulingRoutineForDiagnostics}:"
                + $"haulUpdate={haul?.UpdateHeartbeatForDiagnostics}:"
                + $"haulStarts={haul?.RuntimeHaulStartCount}:"
                + $"haulTerminals={haul?.RuntimeHaulTerminalCount}:"
                + $"haulLastTerminal={haul?.LastTerminalDiagnostics}:"
                + $"haulStage={haul?.CurrentExecutionStage}:"
                + $"haulBeat={haul?.RoutineHeartbeat}:"
                + $"haulFailure={haul?.LastFailureReason}:"
                + $"haulPath={haul?.ActivePathDebug}:"
                + $"moveOwner={move?.ActiveMovementOperationOwnerForDiagnostics}:"
                + $"moveCancel={move?.LastMovementCancellationSourceForDiagnostics}:"
                + $"moveFailure={move?.LastGridMoveFailureReason}:"
                + $"movePreempt={move?.LastMovementOperationPreemptionForDiagnostics}:"
                + $"moveActionCancel={move?.LastActionMovementCancellationReasonForDiagnostics}:"
                + $"schedulerDeterministic={scheduler?.DeterministicSimulationForDiagnostics}:"
                + $"pathBrokerSame={samePathBroker}:"
                + $"pathDeterministic={actorPaths?.DeterministicSearchForDiagnostics}:"
                + $"pathFrame={actorPaths?.CacheFrameForDiagnostics}:"
                + $"pathSearches={actorPaths?.SearchesThisFrame}:"
                + $"pathDeferrals={actorPaths?.BudgetDeferralsThisFrame}:"
                + $"pathIncremental={actorPaths?.IncrementalExactSearchCountForDiagnostics}:"
                + $"preview={preview}/{plan?.PrimaryDestinationId}/{reason}";
        }));

    private static string HashText(string text)
    {
        using SHA256 sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(new UTF8Encoding(false, true).GetBytes(text));
        const string hex = "0123456789abcdef";
        char[] result = new char[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            result[index * 2] = hex[bytes[index] >> 4];
            result[index * 2 + 1] = hex[bytes[index] & 15];
        }
        return new string(result);
    }

    private sealed class WindowAccumulator
    {
        internal long TravelMilliWu;
        internal long WaitMilliWu;
        internal long DispatchWaitMilliWu;
        internal long ReservationWaitMilliWu;
        internal long FacilityAccessWaitMilliWu;
        internal long NoPathMilliWu;
        internal int BurstDeliveredQuantity;
        internal int BurstOutstandingQuantity;
        internal bool BurstQuantityConserved = true;
        internal int Replans;
        internal int StepAsideCount;
        internal int ClutterCellSeconds;
        internal int ImmediateFailures;
    }

    private enum BurstHaulPhase
    {
        None = 0,
        SourceReserved = 1,
        DeliveryMoving = 2,
        DeliveryRoutingWait = 3,
        DestinationAccessWait = 4,
        RecoveryPending = 5,
        NoPath = 6,
        Invalid = 7
    }

    private readonly struct BurstHaulObservation
    {
        internal BurstHaulObservation(
            BurstHaulPhase phase,
            string actorId,
            int joinedBurstCarriedQuantity,
            string detail)
        {
            Phase = phase;
            ActorId = actorId ?? string.Empty;
            JoinedBurstCarriedQuantity = Mathf.Max(
                0,
                joinedBurstCarriedQuantity);
            Detail = detail ?? string.Empty;
        }

        internal static BurstHaulObservation None => new(
            BurstHaulPhase.None,
            string.Empty,
            0,
            string.Empty);

        internal static BurstHaulObservation Invalid(
            string actorId,
            string detail) => new(
            BurstHaulPhase.Invalid,
            actorId,
            0,
            $"{actorId}:Invalid:{detail}");

        internal BurstHaulPhase Phase { get; }
        internal string ActorId { get; }
        internal int JoinedBurstCarriedQuantity { get; }
        internal string Detail { get; }
    }

    private enum BurstProducerKind
    {
        FacilityOutput = 0,
        CropHarvest = 1,
        Mining = 2
    }

    private static BurstProducerKind SelectBurstProducer(int seed) =>
        (BurstProducerKind)((Mathf.Max(1, seed) - 1) % 3);

    private sealed class ArmBurstProbe
    {
        internal ArmBurstProbe(
            BurstProducerKind producerKind,
            string itemId,
            Vector2Int sourceCell,
            int quantity,
            int totalBefore,
            int storedBefore,
            int carriedBefore)
        {
            ProducerKind = producerKind;
            ItemId = itemId ?? string.Empty;
            SourceCell = sourceCell;
            Quantity = quantity;
            TotalBefore = totalBefore;
            StoredBefore = storedBefore;
            CarriedBefore = carriedBefore;
        }

        internal BurstProducerKind ProducerKind { get; }
        internal string ItemId { get; }
        internal Vector2Int SourceCell { get; }
        internal int Quantity { get; }
        internal int TotalBefore { get; }
        internal int StoredBefore { get; }
        internal int CarriedBefore { get; }
    }

    private readonly struct BurstState
    {
        internal BurstState(
            int totalDelta,
            int sourceLoose,
            int sourceReserved,
            int carriedDelta,
            int delivered,
            int outstanding,
            bool quantityConserved)
        {
            TotalDelta = totalDelta;
            SourceLoose = sourceLoose;
            SourceReserved = sourceReserved;
            CarriedDelta = carriedDelta;
            Delivered = delivered;
            Outstanding = outstanding;
            QuantityConserved = quantityConserved;
        }

        internal int TotalDelta { get; }
        internal int SourceLoose { get; }
        internal int SourceReserved { get; }
        internal int CarriedDelta { get; }
        internal int Delivered { get; }
        internal int Outstanding { get; }
        internal bool QuantityConserved { get; }
    }

    private readonly struct FloorRow
    {
        internal FloorRow(
            int seed,
            string arm,
            int window,
            FloorClutterAssessment assessment,
            int clutterCellSeconds,
            int runtimeHeadroomPermille,
            int runtimeErosionCells,
            string runtimeErosionDetail,
            bool isRecovery)
        {
            Seed = seed;
            Arm = arm;
            Window = window;
            IsRecovery = isRecovery;
            GraceSeconds = assessment.GraceSeconds;
            LooseStacks = assessment.LooseStackCount;
            LooseQuantity = assessment.LooseQuantity;
            OutsideContainment = assessment.OutsideContainment.Count;
            Persistent = assessment.PersistentCount;
            ImmediateFailures = assessment.ImmediateFailureCount;
            ClutterCellSeconds = clutterCellSeconds;
            RuntimeHeadroomPermille = runtimeHeadroomPermille;
            RuntimeErosionCells = runtimeErosionCells;
            RuntimeErosionDetail = runtimeErosionDetail ?? string.Empty;
        }

        internal int Seed { get; }
        internal string Arm { get; }
        internal int Window { get; }
        internal bool IsRecovery { get; }
        internal float GraceSeconds { get; }
        internal int LooseStacks { get; }
        internal int LooseQuantity { get; }
        internal int OutsideContainment { get; }
        internal int Persistent { get; }
        internal int ImmediateFailures { get; }
        internal int ClutterCellSeconds { get; }
        internal int RuntimeHeadroomPermille { get; }
        internal int RuntimeErosionCells { get; }
        internal string RuntimeErosionDetail { get; }
    }
}
#endif
