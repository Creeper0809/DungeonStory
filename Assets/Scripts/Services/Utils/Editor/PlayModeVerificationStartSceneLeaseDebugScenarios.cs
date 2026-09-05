using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayModeVerificationStartSceneLeaseDebugScenarios
{
    private const string OwnerId = "qa:start-scene-lease";
    private const string OtherOwnerId = "qa:start-scene-lease-other";
    private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
    private const string PreparationScenePath =
        "Assets/Scenes/StartPreparationScene.unity";

    [MenuItem(
        "DungeonStory/Debug/QA/Run PlayMode Start Scene Lease Contracts")]
    public static void RunAll()
    {
        Scene activeBefore = SceneManager.GetActiveScene();
        string activePathBefore = activeBefore.path;
        bool activeDirtyBefore = activeBefore.IsValid() && activeBefore.isDirty;
        int rootCountBefore = activeBefore.IsValid()
            ? activeBefore.rootCount
            : -1;
        SceneAsset original = EditorSceneManager.playModeStartScene;
        try
        {
            VerifyNullOriginalRoundTrip();
            VerifyExistingOriginalRoundTrip();
            VerifySameOwnerIdempotenceAndForeignOwnerRejection();
            VerifyForeignRestoreAndSceneDriftRetainLease();
            VerifyMissingRequestedSceneFailsWithoutLease();
            VerifyLauncherSourceShape();

            Scene activeAfter = SceneManager.GetActiveScene();
            Require(
                string.Equals(
                    activePathBefore,
                    activeAfter.path,
                    StringComparison.Ordinal)
                && activeDirtyBefore
                    == (activeAfter.IsValid() && activeAfter.isDirty)
                && rootCountBefore
                    == (activeAfter.IsValid() ? activeAfter.rootCount : -1),
                "Start-scene lease changed the active scene, dirty state, or root topology.");
        }
        finally
        {
            if (PlayModeVerificationStartSceneLease.IsOwnedBy(OwnerId))
            {
                PlayModeVerificationStartSceneLease.RestoreOwned(OwnerId);
            }
            EditorSceneManager.playModeStartScene = original;
        }

        Debug.Log(
            "PlayMode start-scene lease contracts PASS: null/existing round-trip, "
            + "idempotence, ownership conflict, missing scene fail-loud, active scene mutation=0.");
    }

    private static void VerifyNullOriginalRoundTrip()
    {
        EditorSceneManager.playModeStartScene = null;
        PlayModeVerificationStartSceneLease.Acquire(OwnerId, TitleScenePath);
        Require(
            AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)
                == TitleScenePath,
            "The requested Title start scene was not applied.");
        Require(
            PlayModeVerificationStartSceneLease.RestoreOwned(OwnerId)
            && EditorSceneManager.playModeStartScene == null,
            "A null original start scene did not round-trip exactly.");
    }

    private static void VerifyExistingOriginalRoundTrip()
    {
        SceneAsset preparation = AssetDatabase.LoadAssetAtPath<SceneAsset>(
            PreparationScenePath);
        Require(preparation != null, "Preparation scene asset is missing.");
        EditorSceneManager.playModeStartScene = preparation;
        PlayModeVerificationStartSceneLease.Acquire(OwnerId, TitleScenePath);
        Require(
            PlayModeVerificationStartSceneLease.RestoreOwned(OwnerId)
            && ReferenceEquals(
                EditorSceneManager.playModeStartScene,
                preparation),
            "An existing original start scene did not round-trip exactly.");
    }

    private static void VerifySameOwnerIdempotenceAndForeignOwnerRejection()
    {
        PlayModeVerificationStartSceneLease.Acquire(OwnerId, TitleScenePath);
        PlayModeVerificationStartSceneLease.Acquire(OwnerId, TitleScenePath);
        bool rejected = false;
        try
        {
            PlayModeVerificationStartSceneLease.Acquire(
                OtherOwnerId,
                TitleScenePath);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(rejected, "A foreign start-scene lease owner was accepted.");
        Require(
            PlayModeVerificationStartSceneLease.RestoreOwned(OwnerId),
            "The original owner could not restore its lease after conflict rejection.");
    }

    private static void VerifyMissingRequestedSceneFailsWithoutLease()
    {
        bool rejected = false;
        try
        {
            PlayModeVerificationStartSceneLease.Acquire(
                OwnerId,
                "Assets/Scenes/QA_Missing_Start_Scene.unity");
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(rejected, "A missing requested start scene was accepted.");
        Require(
            !PlayModeVerificationStartSceneLease.IsOwnedBy(OwnerId),
            "A failed lease acquisition left active ownership behind.");
    }

    private static void VerifyForeignRestoreAndSceneDriftRetainLease()
    {
        SceneAsset title = AssetDatabase.LoadAssetAtPath<SceneAsset>(
            TitleScenePath);
        SceneAsset preparation = AssetDatabase.LoadAssetAtPath<SceneAsset>(
            PreparationScenePath);
        Require(title != null && preparation != null, "Lease test scenes are missing.");
        PlayModeVerificationStartSceneLease.Acquire(OwnerId, TitleScenePath);

        bool foreignRestoreRejected = false;
        try
        {
            PlayModeVerificationStartSceneLease.RestoreOwned(OtherOwnerId);
        }
        catch (InvalidOperationException)
        {
            foreignRestoreRejected = true;
        }
        Require(
            foreignRestoreRejected
            && PlayModeVerificationStartSceneLease.IsOwnedBy(OwnerId),
            "A foreign restore changed or released the active lease.");

        EditorSceneManager.playModeStartScene = preparation;
        bool driftRejected = false;
        try
        {
            PlayModeVerificationStartSceneLease.Acquire(
                OwnerId,
                TitleScenePath);
        }
        catch (InvalidOperationException)
        {
            driftRejected = true;
        }
        Require(
            driftRejected
            && PlayModeVerificationStartSceneLease.IsOwnedBy(OwnerId),
            "A drifted start scene was accepted or cleared its recovery lease.");

        EditorSceneManager.playModeStartScene = title;
        Require(
            PlayModeVerificationStartSceneLease.RestoreOwned(OwnerId),
            "The owner could not restore after the drift was repaired.");
    }

    private static void VerifyLauncherSourceShape()
    {
        string root = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string firstRun = File.ReadAllText(Path.Combine(
            root,
            "Assets/Scripts/Services/Run/Editor/FirstRunObjectivePlayModeVerifier.cs"));
        string physical = File.ReadAllText(Path.Combine(
            root,
            "Assets/Scripts/Services/Items/Editor/PhysicalItemLogisticsPlayModeVerifier.cs"));
        string synthetic = File.ReadAllText(Path.Combine(
            root,
            "Assets/Scripts/Services/Items/Editor/SyntheticPreparedOutputCanaryAssetTransaction.cs"));

        Require(
            firstRun.Contains(
                "PlayModeVerificationStartSceneLease.Acquire(",
                StringComparison.Ordinal)
            && !firstRun.Contains(
                "StartSceneOverrideActiveKey",
                StringComparison.Ordinal)
            && physical.Contains(
                "PlayModeVerificationStartSceneLease.Acquire(",
                StringComparison.Ordinal)
            && physical.Contains(
                "PLAYMODE_ABORTED verifier returned to EditMode",
                StringComparison.Ordinal)
            && !physical.Contains(
                "EditorSceneManager.OpenScene(",
                StringComparison.Ordinal)
            && !synthetic.Contains(
                "requiresSceneUnload",
                StringComparison.Ordinal),
            "A PlayMode verifier reintroduced direct scene unloading or deferred dirty-scene refusal.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
