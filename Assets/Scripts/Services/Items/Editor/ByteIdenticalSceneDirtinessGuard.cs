#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Clears an Editor scene's false-positive dirty flag only after serializing
/// the in-memory scene to an exact-owned probe and proving byte identity with
/// the on-disk scene. A real unsaved edit is never saved, discarded, or hidden.
/// </summary>
internal static class ByteIdenticalSceneDirtinessGuard
{
    private const string ProbeDirectory = "Assets/__V27DirtySceneProbe";
    private const string ProbeScenePath =
        ProbeDirectory + "/GameplayScene.unity";

    private static readonly MethodInfo ClearSceneDirtiness =
        typeof(EditorSceneManager).GetMethod(
            "ClearSceneDirtiness",
            BindingFlags.Static | BindingFlags.NonPublic);

    internal static bool TryClearFalseDirty(
        Scene scene,
        out string failure)
    {
        failure = string.Empty;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            failure = "scene-invalid-or-unloaded";
            return false;
        }
        if (!scene.isDirty)
            return true;
        if (string.IsNullOrWhiteSpace(scene.path)
            || !scene.path.StartsWith("Assets/", StringComparison.Ordinal)
            || !File.Exists(scene.path))
        {
            failure = "dirty-scene-has-no-canonical-disk-authority";
            return false;
        }
        if (ClearSceneDirtiness == null)
        {
            failure = "unity-clear-scene-dirtiness-api-unavailable";
            return false;
        }

        try
        {
            string officialHash = ComputeSha256(scene.path);
            if (AssetDatabase.IsValidFolder(ProbeDirectory)
                || File.Exists(ProbeScenePath))
            {
                if (!File.Exists(ProbeScenePath)
                    || !string.Equals(
                        ComputeSha256(ProbeScenePath),
                        officialHash,
                        StringComparison.Ordinal))
                {
                    failure = "existing-dirty-scene-probe-is-not-byte-identical:"
                        + ProbeScenePath;
                    return false;
                }
                if (!AssetDatabase.DeleteAsset(ProbeDirectory))
                {
                    failure = "byte-identical-stale-probe-delete-failed";
                    return false;
                }
            }

            string folderGuid = AssetDatabase.CreateFolder(
                "Assets",
                "__V27DirtySceneProbe");
            if (string.IsNullOrWhiteSpace(folderGuid)
                || !EditorSceneManager.SaveScene(
                    scene,
                    ProbeScenePath,
                    saveAsCopy: true))
            {
                failure = "dirty-scene-probe-write-failed";
                return false;
            }

            string probeHash = ComputeSha256(ProbeScenePath);
            if (!string.Equals(
                    probeHash,
                    officialHash,
                    StringComparison.Ordinal))
            {
                failure = "real-unsaved-scene-change-preserved-at:"
                    + ProbeScenePath;
                return false;
            }

            ClearSceneDirtiness.Invoke(null, new object[] { scene });
            if (scene.isDirty
                || !string.Equals(
                    ComputeSha256(scene.path),
                    officialHash,
                    StringComparison.Ordinal))
            {
                failure = "false-dirty-clearance-changed-scene-authority";
                return false;
            }
            if (!AssetDatabase.DeleteAsset(ProbeDirectory))
            {
                failure = "byte-identical-probe-delete-failed";
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            failure = "false-dirty-clearance-exception:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }

    private static string ComputeSha256(string path)
    {
        using SHA256 sha = SHA256.Create();
        using FileStream stream = File.OpenRead(path);
        return BitConverter.ToString(sha.ComputeHash(stream))
            .Replace("-", string.Empty);
    }
}
#endif
