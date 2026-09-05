#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class AbilityHaulLifecycleRecoveryPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/ability-haul-lifecycle-recovery-playmode.txt";
    private const string RequestPath =
        "Temp/ability-haul-lifecycle-recovery-playmode.request";
    private const string DispatchRequestPath =
        "Temp/ability-haul-lifecycle-recovery-playmode.dispatch.request";
    private const string SceneLeaseOwnerPath =
        "Temp/ability-haul-lifecycle-recovery-scene-lease.flag";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private const string SceneLeaseOwnerToken =
        "ability-haul-lifecycle-recovery|Assets/Scenes/GameplayScene.unity";

    static AbilityHaulLifecycleRecoveryPlayModeVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update -= DispatchPendingRun;
        EditorApplication.update += DispatchPendingRun;
        EditorApplication.delayCall -= RecoverOwnedSceneLeaseIfOrphaned;
        EditorApplication.delayCall += RecoverOwnedSceneLeaseIfOrphaned;
    }

    public static void QueueRunFromEditorCommand()
    {
        Directory.CreateDirectory("Temp");
        if (File.Exists(DispatchRequestPath)
            || File.Exists(RequestPath)
            || File.Exists(SceneLeaseOwnerPath))
        {
            throw new InvalidOperationException(
                "An AbilityHaul lifecycle recovery run is already pending.");
        }
        File.WriteAllText(
            DispatchRequestPath,
            "run",
            new UTF8Encoding(false));
    }

    internal static bool HasPendingDurableRun =>
        File.Exists(DispatchRequestPath)
        || File.Exists(RequestPath)
        || File.Exists(SceneLeaseOwnerPath);

    private static void DispatchPendingRun()
    {
        if (!File.Exists(DispatchRequestPath)
            || EditorApplication.isCompiling
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid() || active.isDirty)
            return;

        string request;
        try
        {
            request = File.ReadAllText(DispatchRequestPath).Trim();
        }
        catch (IOException)
        {
            return;
        }
        if (!string.Equals(request, "run", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "AbilityHaul lifecycle recovery dispatch request must contain the exact token 'run'.");
        }

        File.Delete(DispatchRequestPath);
        RunFromMenu();
    }

    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify AbilityHaul Lifecycle Recovery")]
    public static void RunFromMenu()
    {
        if (EditorApplication.isPlaying)
        {
            StartRunner();
            return;
        }

        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        if (EditorApplication.isCompiling)
            throw new InvalidOperationException(
                "AbilityHaul lifecycle recovery cannot enter Play Mode while scripts compile.");
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "AbilityHaul lifecycle recovery cannot dispatch during a Play Mode transition.");
        if (EditorUtility.scriptCompilationFailed)
            throw new InvalidOperationException(
                "AbilityHaul lifecycle recovery cannot enter Play Mode because the latest script compilation failed.");

        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid() || active.isDirty)
            throw new InvalidOperationException(
                "AbilityHaul lifecycle recovery requires a valid clean EditMode scene.");
        if (SyntheticPreparedOutputCanaryGameplaySceneLease.IsActive
            || File.Exists(SceneLeaseOwnerPath))
        {
            throw new InvalidOperationException(
                "AbilityHaul lifecycle recovery cannot acquire its sanitized GameplayScene because another verification lease is active.");
        }

        bool sceneLeaseAcquired = false;
        try
        {
            SyntheticPreparedOutputCanaryGameplaySceneLease.Acquire();
            sceneLeaseAcquired = true;
            File.WriteAllText(SceneLeaseOwnerPath, SceneLeaseOwnerToken);
            EditorSceneManager.OpenScene(
                SyntheticPreparedOutputCanaryGameplaySceneLease
                    .ExpectedRuntimeScenePath,
                OpenSceneMode.Single);
            File.WriteAllText(RequestPath, "requested");
            EditorApplication.EnterPlaymode();
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "AbilityHaul lifecycle recovery Play Mode transition was rejected.");
            }
        }
        catch (Exception requestFailure)
        {
            File.Delete(RequestPath);
            try
            {
                if (File.Exists(SceneLeaseOwnerPath))
                {
                    ReleaseOwnedSceneLease();
                }
                else if (sceneLeaseAcquired)
                {
                    SyntheticPreparedOutputCanaryGameplaySceneLease
                        .RestoreOwned();
                }
            }
            catch (Exception rollbackFailure)
            {
                throw new AggregateException(
                    "AbilityHaul lifecycle recovery request failed and its scene lease rollback also failed.",
                    requestFailure,
                    rollbackFailure);
            }
            throw;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapPendingRun()
    {
        if (!File.Exists(RequestPath))
            return;
        File.Delete(RequestPath);
        StartRunner();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode
            && File.Exists(RequestPath))
        {
            File.Delete(RequestPath);
            StartRunner();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            File.Delete(RequestPath);
            try
            {
                ReleaseOwnedSceneLease();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "AbilityHaul lifecycle recovery scene lease cleanup failed: "
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
            RequireOwnedSceneLeaseMarker();
            File.Delete(RequestPath);
            ReleaseOwnedSceneLease();
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "AbilityHaul lifecycle recovery orphaned scene lease recovery failed: "
                + exception);
        }
    }

    private static void ReleaseOwnedSceneLease()
    {
        if (!File.Exists(SceneLeaseOwnerPath))
            return;
        RequireOwnedSceneLeaseMarker();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException(
                "AbilityHaul lifecycle recovery scene lease cannot be released during a Play Mode transition.");
        }

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
            // PlayMode teardown may dirty only the disposable scene. Persisting
            // it at its owned temporary path avoids a close prompt; the official
            // GameplayScene and its meta are never save targets here.
            string temporaryDirectory = Path.GetDirectoryName(
                    temporaryScenePath)
                ?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(temporaryDirectory))
            {
                throw new InvalidOperationException(
                    "AbilityHaul lifecycle recovery temporary scene directory is invalid.");
            }
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
                        "AbilityHaul lifecycle recovery could not recreate its disposable scene directory.");
                }
            }
            if (ownedScene.isDirty
                && !EditorSceneManager.SaveScene(
                    ownedScene,
                    temporaryScenePath,
                    saveAsCopy: false))
            {
                throw new InvalidOperationException(
                    "AbilityHaul lifecycle recovery could not persist its disposable scene before cleanup.");
            }

            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
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
                    "AbilityHaul lifecycle recovery could not delete its orphaned disposable scene directory.");
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
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
                || official.isDirty
                || !string.Equals(
                    official.path,
                    GameplayScenePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "AbilityHaul lifecycle recovery could not restore a clean official GameplayScene.");
            }
        }
        File.Delete(SceneLeaseOwnerPath);
    }

    private static void RequireOwnedSceneLeaseMarker()
    {
        string marker = File.ReadAllText(SceneLeaseOwnerPath);
        if (!string.Equals(
                marker,
                SceneLeaseOwnerToken,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "AbilityHaul lifecycle recovery scene lease marker is not exact-owned.");
        }
    }

    private static void StartRunner()
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                AbilityHaulLifecycleRecoveryPlayModeRunner>() != null)
        {
            return;
        }

        new GameObject("AbilityHaul Lifecycle Recovery Verifier")
            .AddComponent<AbilityHaulLifecycleRecoveryPlayModeRunner>();
    }
}

public sealed class AbilityHaulLifecycleRecoveryPlayModeRunner : MonoBehaviour
{
    private readonly List<string> evidence = new();
    private readonly List<string> failures = new();

    private IEnumerator Start()
    {
        try
        {
            RestoredCarrySubsetReleaseDebugScenarios
                .VerifyAdditionalHaulFaultRows(evidence);
        }
        catch (Exception exception)
        {
            failures.Add(exception.ToString());
        }

        if (failures.Count > 0)
        {
            WriteReport();
            Destroy(gameObject);
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlaying)
                    EditorApplication.isPlaying = false;
            };
            yield break;
        }

        IEnumerator verification = RestoredCarrySubsetReleaseDebugScenarios
            .VerifyLifecycleRecoveryFaultRows(evidence);
        while (true)
        {
            bool moved;
            object current = null;
            try
            {
                moved = verification.MoveNext();
                if (moved)
                    current = verification.Current;
            }
            catch (Exception exception)
            {
                failures.Add(exception.ToString());
                break;
            }

            if (!moved)
                break;
            yield return current;
        }

        if (failures.Count == 0)
        {
            IEnumerator wholePickup = HaulPlanConstructionSafetyDebugScenarios
                .RunWholePickupAndMidHaulRestoreFocused(evidence);
            while (true)
            {
                bool moved;
                object current = null;
                try
                {
                    moved = wholePickup.MoveNext();
                    if (moved)
                        current = wholePickup.Current;
                }
                catch (Exception exception)
                {
                    failures.Add(exception.ToString());
                    break;
                }

                if (!moved)
                    break;
                yield return current;
            }
        }

        WriteReport();
        Destroy(gameObject);
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
        };
    }

    private void WriteReport()
    {
        Directory.CreateDirectory("Artifacts/QA");
        StringBuilder report = new();
        report.AppendLine("# AbilityHaul Lifecycle Recovery PlayMode");
        report.AppendLine("authority=production-ability-haul-lifecycle");
        report.AppendLine("currentSourceDigest="
            + V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest());
        report.AppendLine("gameplaySceneSha256="
            + V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest());
        foreach (string line in evidence)
            report.AppendLine(line);
        report.AppendLine("RESULT=" + (failures.Count == 0 ? "PASS" : "FAIL"));
        if (failures.Count > 0)
            report.AppendLine("failures=" + string.Join(" | ", failures));
        File.WriteAllText(
            AbilityHaulLifecycleRecoveryPlayModeVerifier.ReportPath,
            report.ToString());

        if (failures.Count == 0)
            Debug.Log("ABILITY_HAUL_LIFECYCLE_RECOVERY=PASS");
        else
            Debug.LogError("ABILITY_HAUL_LIFECYCLE_RECOVERY=FAIL: "
                + string.Join(" | ", failures));
    }
}
#endif
