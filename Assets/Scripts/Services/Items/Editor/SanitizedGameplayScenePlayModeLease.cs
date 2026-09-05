#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Exact-owned wrapper for PlayMode verifiers that must execute against the
/// sanitized GameplayScene copy while preserving the official scene bytes.
/// </summary>
internal static class SanitizedGameplayScenePlayModeLease
{
    internal const string OfficialScenePath =
        "Assets/Scenes/GameplayScene.unity";

    internal static void Acquire(string ownerMarkerPath, string ownerToken)
    {
        ValidateOwner(ownerMarkerPath, ownerToken);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "A sanitized GameplayScene lease requires stable EditMode.");
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid())
            throw new InvalidOperationException(
                "A sanitized GameplayScene lease requires a valid scene.");
        if (active.isDirty
            && !ByteIdenticalSceneDirtinessGuard.TryClearFalseDirty(
                active,
                out string dirtyFailure))
        {
            throw new InvalidOperationException(
                "A sanitized GameplayScene lease refused a real unsaved scene change: "
                + dirtyFailure);
        }
        if (File.Exists(ownerMarkerPath)
            || SyntheticPreparedOutputCanaryGameplaySceneLease.IsActive)
        {
            throw new InvalidOperationException(
                "Another sanitized GameplayScene lease is already active.");
        }

        bool leaseAcquired = false;
        bool ownerWritten = false;
        try
        {
            SyntheticPreparedOutputCanaryGameplaySceneLease.Acquire();
            leaseAcquired = true;
            Directory.CreateDirectory(
                Path.GetDirectoryName(ownerMarkerPath) ?? "Temp");
            File.WriteAllText(ownerMarkerPath, ownerToken);
            ownerWritten = true;
            Scene temporary = EditorSceneManager.OpenScene(
                SyntheticPreparedOutputCanaryGameplaySceneLease
                    .ExpectedRuntimeScenePath,
                OpenSceneMode.Single);
            if (!temporary.IsValid()
                || temporary.isDirty
                || !string.Equals(
                    temporary.path,
                    SyntheticPreparedOutputCanaryGameplaySceneLease
                        .ExpectedRuntimeScenePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The sanitized GameplayScene copy could not be opened cleanly.");
            }
        }
        catch
        {
            if (ownerWritten)
            {
                Release(ownerMarkerPath, ownerToken);
            }
            else if (leaseAcquired)
            {
                SyntheticPreparedOutputCanaryGameplaySceneLease.RestoreOwned();
            }
            throw;
        }
    }

    internal static void Release(string ownerMarkerPath, string ownerToken)
    {
        ValidateOwner(ownerMarkerPath, ownerToken);
        if (!File.Exists(ownerMarkerPath))
            return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "A sanitized GameplayScene lease cannot release during a PlayMode transition.");
        string marker = File.ReadAllText(ownerMarkerPath);
        if (!string.Equals(marker, ownerToken, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The sanitized GameplayScene lease owner marker is not exact-owned.");

        string temporaryPath =
            SyntheticPreparedOutputCanaryGameplaySceneLease
                .ExpectedRuntimeScenePath;
        Scene temporary = SceneManager.GetSceneByPath(temporaryPath);
        if (temporary.IsValid() && temporary.isLoaded)
        {
            if (temporary.isDirty
                && !EditorSceneManager.SaveScene(
                    temporary,
                    temporaryPath,
                    saveAsCopy: false))
            {
                throw new InvalidOperationException(
                    "The disposable sanitized GameplayScene could not be saved before release.");
            }
            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
        }

        if (!SyntheticPreparedOutputCanaryGameplaySceneLease.RestoreOwned())
            throw new InvalidOperationException(
                "The sanitized GameplayScene lease marker disappeared before release.");
        Scene official = EditorSceneManager.OpenScene(
            OfficialScenePath,
            OpenSceneMode.Single);
        if (!official.IsValid()
            || official.isDirty
            || !string.Equals(
                official.path,
                OfficialScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The clean official GameplayScene could not be restored after verification.");
        }
        File.Delete(ownerMarkerPath);
    }

    private static void ValidateOwner(
        string ownerMarkerPath,
        string ownerToken)
    {
        if (string.IsNullOrWhiteSpace(ownerMarkerPath)
            || string.IsNullOrWhiteSpace(ownerToken))
        {
            throw new ArgumentException(
                "A sanitized GameplayScene lease requires an exact owner path and token.");
        }
    }
}
#endif
