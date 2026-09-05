using System;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PlayModeVerificationStartSceneLease
{
    private const string KeyPrefix =
        "DungeonStory.PlayModeVerification.StartSceneLease.";
    private const string ActiveKey = KeyPrefix + "Active";
    private const string OwnerKey = KeyPrefix + "Owner";
    private const string RequestedScenePathKey = KeyPrefix + "RequestedScenePath";
    private const string OriginalSceneWasNullKey = KeyPrefix + "OriginalSceneWasNull";
    private const string OriginalScenePathKey = KeyPrefix + "OriginalScenePath";

    public static void Acquire(string ownerId, string startScenePath)
    {
        ValidateOwnerId(ownerId);
        SceneAsset requested = RequireSceneAsset(startScenePath, "requested");

        if (SessionState.GetBool(ActiveKey, false))
        {
            RequireOwnedLease(ownerId, startScenePath);
            string currentPath = AssetDatabase.GetAssetPath(
                EditorSceneManager.playModeStartScene);
            if (!string.Equals(
                    currentPath,
                    startScenePath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The active PlayMode start-scene lease drifted from its requested scene. "
                    + $"owner={ownerId}; expected={startScenePath}; actual={currentPath}");
            }

            return;
        }

        SceneAsset original = EditorSceneManager.playModeStartScene;
        string originalPath = original != null
            ? AssetDatabase.GetAssetPath(original)
            : string.Empty;
        SessionState.SetString(OwnerKey, ownerId);
        SessionState.SetString(RequestedScenePathKey, startScenePath);
        SessionState.SetBool(OriginalSceneWasNullKey, original == null);
        SessionState.SetString(OriginalScenePathKey, originalPath);
        SessionState.SetBool(ActiveKey, true);
        try
        {
            EditorSceneManager.playModeStartScene = requested;
        }
        catch (Exception acquireException)
        {
            try
            {
                EditorSceneManager.playModeStartScene = original;
                RequireCurrentScene(original, originalPath, "acquire rollback");
                ClearState();
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    "The PlayMode start-scene lease failed to apply and could not restore its original scene; lease state was retained for recovery.",
                    acquireException,
                    rollbackException);
            }

            throw;
        }
    }

    public static bool RestoreOwned(string ownerId)
    {
        ValidateOwnerId(ownerId);
        if (!SessionState.GetBool(ActiveKey, false))
        {
            return false;
        }

        string requestedPath = SessionState.GetString(
            RequestedScenePathKey,
            string.Empty);
        RequireOwnedLease(ownerId, requestedPath);

        bool originalWasNull = SessionState.GetBool(
            OriginalSceneWasNullKey,
            true);
        string originalPath = SessionState.GetString(
            OriginalScenePathKey,
            string.Empty);
        SceneAsset original = null;
        if (!originalWasNull)
        {
            original = RequireSceneAsset(originalPath, "original");
        }

        EditorSceneManager.playModeStartScene = original;
        RequireCurrentScene(original, originalPath, "restore");
        ClearState();
        return true;
    }

    public static bool IsOwnedBy(string ownerId)
    {
        ValidateOwnerId(ownerId);
        return SessionState.GetBool(ActiveKey, false)
            && string.Equals(
                SessionState.GetString(OwnerKey, string.Empty),
                ownerId,
                StringComparison.Ordinal);
    }

    public static bool TryGetActiveOwner(out string ownerId)
    {
        if (!SessionState.GetBool(ActiveKey, false))
        {
            ownerId = string.Empty;
            return false;
        }

        ownerId = SessionState.GetString(OwnerKey, string.Empty);
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new InvalidOperationException(
                "An active PlayMode start-scene lease has no canonical owner.");
        }

        return true;
    }

    private static void RequireOwnedLease(
        string ownerId,
        string requestedScenePath)
    {
        string currentOwner = SessionState.GetString(OwnerKey, string.Empty);
        string currentRequested = SessionState.GetString(
            RequestedScenePathKey,
            string.Empty);
        if (!string.Equals(currentOwner, ownerId, StringComparison.Ordinal)
            || !string.Equals(
                currentRequested,
                requestedScenePath,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A different or drifted PlayMode start-scene lease is active. "
                + $"requestedOwner={ownerId}; activeOwner={currentOwner}; "
                + $"requestedScene={requestedScenePath}; activeScene={currentRequested}");
        }
    }

    private static SceneAsset RequireSceneAsset(string path, string role)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !string.Equals(path, path.Trim(), StringComparison.Ordinal)
            || !path.StartsWith("Assets/", StringComparison.Ordinal)
            || !path.EndsWith(".unity", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The {role} PlayMode start scene path is noncanonical.",
                nameof(path));
        }

        SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        if (scene == null)
        {
            throw new InvalidOperationException(
                $"The {role} PlayMode start scene asset is missing: {path}");
        }

        return scene;
    }

    private static void RequireCurrentScene(
        SceneAsset expected,
        string expectedPath,
        string operation)
    {
        SceneAsset actual = EditorSceneManager.playModeStartScene;
        string actualPath = actual != null
            ? AssetDatabase.GetAssetPath(actual)
            : string.Empty;
        if (!ReferenceEquals(actual, expected)
            || !string.Equals(
                actualPath,
                expectedPath,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The PlayMode start-scene lease did not complete its "
                + $"{operation} assignment exactly. expected={expectedPath}; actual={actualPath}");
        }
    }

    private static void ValidateOwnerId(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId)
            || !string.Equals(ownerId, ownerId.Trim(), StringComparison.Ordinal)
            || ownerId.IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0)
        {
            throw new ArgumentException(
                "A PlayMode start-scene lease requires a canonical owner ID.",
                nameof(ownerId));
        }
    }

    private static void ClearState()
    {
        SessionState.EraseBool(ActiveKey);
        SessionState.EraseString(OwnerKey);
        SessionState.EraseString(RequestedScenePathKey);
        SessionState.EraseBool(OriginalSceneWasNullKey);
        SessionState.EraseString(OriginalScenePathKey);
    }
}
