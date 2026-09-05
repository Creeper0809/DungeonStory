#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Temporarily substitutes a sanitized copy of GameplayScene in build settings
/// for the synthetic full-path canary. The user's loaded/disk GameplayScene is
/// never saved, unloaded, or overwritten by this lease.
/// </summary>
[InitializeOnLoad]
public static class SyntheticPreparedOutputCanaryGameplaySceneLease
{
    private const int SchemaVersion = 1;
    private const string OfficialScenePath = "Assets/Scenes/GameplayScene.unity";
    private const string TemporaryDirectory =
        "Assets/__V27SyntheticPreparedOutputCanary";
    private const string TemporaryScenePath =
        TemporaryDirectory + "/GameplayScene.unity";
    private const string MarkerPath =
        "Temp/v27-synthetic-gameplay-scene-lease.json";
    private const string RemovedFixtureName = "LifecycleDemolitionFixture";
    // The current official GameplayScene is fixture-free. Keep the sanitizer
    // strict so a future accidental serialized lifecycle fixture is detected
    // instead of silently becoming part of the synthetic verification scene.
    private const int ExpectedRemovedFixtureCount = 0;

    [Serializable]
    private sealed class BuildSceneEntry
    {
        public string path = string.Empty;
        public bool enabled;
    }

    [Serializable]
    private sealed class LeaseManifest
    {
        public int schemaVersion;
        public string officialSceneSha256 = string.Empty;
        public string officialMetaSha256 = string.Empty;
        public string temporarySceneGuid = string.Empty;
        public int replacedBuildSceneIndex = -1;
        public int removedFixtureCount;
        public List<BuildSceneEntry> originalBuildScenes = new();
    }

    static SyntheticPreparedOutputCanaryGameplaySceneLease()
    {
        EditorApplication.delayCall -= RecoverIfOrphaned;
        EditorApplication.delayCall += RecoverIfOrphaned;
    }

    public static bool IsActive => File.Exists(MarkerPath);

    internal static string ExpectedRuntimeScenePath => TemporaryScenePath;

    public static void Acquire()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "Synthetic Gameplay scene lease must be acquired in stable EditMode.");
        if (IsActive)
        {
            try
            {
                ValidateActive(ReadManifest());
                return;
            }
            catch (InvalidOperationException)
            {
                // A killed Worker may leave the exact recovery marker after
                // Unity has already persisted the original build settings.
                // Recover from that owned manifest before acquiring a fresh
                // lease; official scene/meta hash drift still fails inside
                // RestoreOwned and is never hidden.
                if (!RestoreOwned())
                    throw;
            }
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TemporaryScenePath) != null
            || AssetDatabase.IsValidFolder(TemporaryDirectory))
        {
            throw new InvalidOperationException(
                "Synthetic Gameplay scene staging path exists without a recovery marker.");
        }

        Scene activeBefore = SceneManager.GetActiveScene();
        string activePathBefore = activeBefore.IsValid()
            ? activeBefore.path
            : string.Empty;
        bool activeDirtyBefore = activeBefore.IsValid() && activeBefore.isDirty;
        string officialHash = ComputeSha256(OfficialScenePath);
        string officialMetaHash = ComputeSha256(OfficialScenePath + ".meta");
        EditorBuildSettingsScene[] original = EditorBuildSettings.scenes;
        int officialIndex = Array.FindIndex(
            original,
            value => string.Equals(
                value.path,
                OfficialScenePath,
                StringComparison.Ordinal));
        if (officialIndex < 0 || !original[officialIndex].enabled)
            throw new InvalidOperationException(
                "The enabled official GameplayScene build entry is missing.");
        if (original.Count(value => string.Equals(
                Path.GetFileNameWithoutExtension(value.path),
                DungeonSceneNavigator.GameplaySceneName,
                StringComparison.Ordinal)) != 1)
        {
            throw new InvalidOperationException(
                "GameplayScene build-name ownership is not unique before leasing.");
        }

        LeaseManifest manifest = new()
        {
            schemaVersion = SchemaVersion,
            officialSceneSha256 = officialHash,
            officialMetaSha256 = officialMetaHash,
            replacedBuildSceneIndex = officialIndex,
            originalBuildScenes = original.Select(value => new BuildSceneEntry
            {
                path = value.path,
                enabled = value.enabled
            }).ToList()
        };

        bool markerWritten = false;
        try
        {
            AssetDatabase.CreateFolder("Assets", "__V27SyntheticPreparedOutputCanary");
            if (!AssetDatabase.CopyAsset(OfficialScenePath, TemporaryScenePath))
                throw new InvalidOperationException(
                    "Failed to copy GameplayScene into synthetic staging.");
            string sanitized = RemoveOwnedRootFixtures(
                File.ReadAllText(TemporaryScenePath),
                out int removedFixtureCount);
            File.WriteAllText(
                TemporaryScenePath,
                sanitized,
                new UTF8Encoding(false, true));
            manifest.removedFixtureCount = removedFixtureCount;
            AssetDatabase.ImportAsset(
                TemporaryScenePath,
                ImportAssetOptions.ForceSynchronousImport
                    | ImportAssetOptions.ForceUpdate);
            manifest.temporarySceneGuid = AssetDatabase.AssetPathToGUID(
                TemporaryScenePath);
            if (!IsCanonicalGuid(manifest.temporarySceneGuid))
                throw new InvalidOperationException(
                    "Synthetic GameplayScene copy has no canonical GUID.");

            RequireOriginalSceneUnchanged(
                officialHash,
                officialMetaHash,
                activePathBefore,
                activeDirtyBefore);
            if (CountToken(
                    File.ReadAllText(TemporaryScenePath),
                    "m_Name: " + RemovedFixtureName) != 0)
                throw new InvalidOperationException(
                    "Synthetic GameplayScene still contains stale lifecycle fixtures.");

            Directory.CreateDirectory("Temp");
            File.WriteAllText(MarkerPath, JsonUtility.ToJson(manifest, true));
            markerWritten = true;

            EditorBuildSettingsScene[] leased = original
                .Select(value => new EditorBuildSettingsScene(
                    value.path,
                    value.enabled))
                .ToArray();
            leased[officialIndex] = new EditorBuildSettingsScene(
                TemporaryScenePath,
                original[officialIndex].enabled);
            EditorBuildSettings.scenes = leased;
            ValidateActive(manifest);
        }
        catch
        {
            try
            {
                RestoreBuildSettings(manifest.originalBuildScenes);
                DeleteTemporaryAsset();
                RequireOriginalSceneUnchanged(
                    officialHash,
                    officialMetaHash,
                    activePathBefore,
                    activeDirtyBefore);
                if (markerWritten)
                    File.Delete(MarkerPath);
            }
            catch (Exception rollback)
            {
                throw new InvalidOperationException(
                    "Synthetic Gameplay scene lease failed and rollback also failed; recovery marker was retained.",
                    rollback);
            }
            throw;
        }
    }

    public static bool RestoreOwned()
    {
        if (!IsActive)
            return false;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "Synthetic Gameplay scene lease cannot restore during PlayMode transition.");

        LeaseManifest manifest = ReadManifest();
        RestoreBuildSettings(manifest.originalBuildScenes);
        DeleteTemporaryAsset();
        if (!string.Equals(
                ComputeSha256(OfficialScenePath),
                manifest.officialSceneSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                ComputeSha256(OfficialScenePath + ".meta"),
                manifest.officialMetaSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Official GameplayScene bytes changed while restoring the synthetic lease.");
        }
        File.Delete(MarkerPath);
        return true;
    }

    public static void ValidateActive()
    {
        if (!IsActive)
            throw new InvalidOperationException(
                "Synthetic Gameplay scene lease is not active.");
        ValidateActive(ReadManifest());
    }

    private static void ValidateActive(LeaseManifest manifest)
    {
        ValidateManifest(manifest);
        EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
        if (current.Length != manifest.originalBuildScenes.Count
            || manifest.replacedBuildSceneIndex >= current.Length)
            throw new InvalidOperationException(
                "Synthetic Gameplay build-settings cardinality drifted.");
        for (int index = 0; index < current.Length; index++)
        {
            BuildSceneEntry original = manifest.originalBuildScenes[index];
            string expectedPath = index == manifest.replacedBuildSceneIndex
                ? TemporaryScenePath
                : original.path;
            if (!string.Equals(current[index].path, expectedPath, StringComparison.Ordinal)
                || current[index].enabled != original.enabled)
                throw new InvalidOperationException(
                    "Synthetic Gameplay build-settings lease drifted at index " + index);
        }
        if (!string.Equals(
                AssetDatabase.AssetPathToGUID(TemporaryScenePath),
                manifest.temporarySceneGuid,
                StringComparison.Ordinal)
            || !string.Equals(
                ComputeSha256(OfficialScenePath),
                manifest.officialSceneSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                ComputeSha256(OfficialScenePath + ".meta"),
                manifest.officialMetaSha256,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Synthetic Gameplay scene lease identity drifted.");
    }

    private static LeaseManifest ReadManifest()
    {
        LeaseManifest manifest = JsonUtility.FromJson<LeaseManifest>(
            File.ReadAllText(MarkerPath));
        ValidateManifest(manifest);
        return manifest;
    }

    private static void ValidateManifest(LeaseManifest manifest)
    {
        if (manifest == null
            || manifest.schemaVersion != SchemaVersion
            || !IsSha256(manifest.officialSceneSha256)
            || !IsSha256(manifest.officialMetaSha256)
            || !IsCanonicalGuid(manifest.temporarySceneGuid)
            || manifest.removedFixtureCount != ExpectedRemovedFixtureCount
            || manifest.originalBuildScenes == null
            || manifest.originalBuildScenes.Count == 0
            || manifest.replacedBuildSceneIndex < 0
            || manifest.replacedBuildSceneIndex >= manifest.originalBuildScenes.Count
            || manifest.originalBuildScenes.Any(value => value == null
                || string.IsNullOrWhiteSpace(value.path)))
            throw new InvalidOperationException(
                "Synthetic Gameplay scene lease marker is invalid.");
    }

    private static void RestoreBuildSettings(
        IReadOnlyList<BuildSceneEntry> entries)
    {
        if (entries == null || entries.Count == 0)
            throw new InvalidOperationException(
                "Synthetic Gameplay scene lease has no build-settings backup.");
        EditorBuildSettings.scenes = entries
            .Select(value => new EditorBuildSettingsScene(
                value.path,
                value.enabled))
            .ToArray();
    }

    private static void DeleteTemporaryAsset()
    {
        if (AssetDatabase.LoadMainAssetAtPath(TemporaryScenePath) != null
            && !AssetDatabase.DeleteAsset(TemporaryScenePath))
            throw new InvalidOperationException(
                "Synthetic GameplayScene asset could not be deleted.");
        if (AssetDatabase.IsValidFolder(TemporaryDirectory)
            && !AssetDatabase.DeleteAsset(TemporaryDirectory))
            throw new InvalidOperationException(
                "Synthetic GameplayScene folder could not be deleted.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void RequireOriginalSceneUnchanged(
        string sceneHash,
        string metaHash,
        string activePath,
        bool activeDirty)
    {
        Scene active = SceneManager.GetActiveScene();
        if (!string.Equals(ComputeSha256(OfficialScenePath), sceneHash,
                StringComparison.Ordinal)
            || !string.Equals(ComputeSha256(OfficialScenePath + ".meta"), metaHash,
                StringComparison.Ordinal)
            || !string.Equals(
                active.IsValid() ? active.path : string.Empty,
                activePath,
                StringComparison.Ordinal)
            || (active.IsValid() && active.isDirty) != activeDirty)
        {
            throw new InvalidOperationException(
                "Synthetic Gameplay scene staging changed the user's official scene state.");
        }
    }

    private static void RecoverIfOrphaned()
    {
        if (!IsActive || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (File.Exists(
            PhysicalItemLogisticsPlayModeVerifier.PreparedOutputWarehouseRequestPath))
            return;
        try
        {
            RestoreOwned();
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Synthetic Gameplay scene lease recovery failed: " + exception);
        }
    }

    private static string ComputeSha256(string path)
    {
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(File.ReadAllBytes(path));
        return BitConverter.ToString(hash).Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static bool IsSha256(string value) =>
        !string.IsNullOrEmpty(value)
        && value.Length == 64
        && value.All(Uri.IsHexDigit)
        && string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal);

    private static bool IsCanonicalGuid(string value) =>
        !string.IsNullOrEmpty(value)
        && value.Length == 32
        && value.All(Uri.IsHexDigit)
        && string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal);

    private static int CountToken(string text, string token)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += token.Length;
        }
        return count;
    }

    private static string RemoveOwnedRootFixtures(
        string yaml,
        out int removedFixtureCount)
    {
        if (string.IsNullOrEmpty(yaml))
            throw new InvalidOperationException(
                "Official GameplayScene YAML is empty.");
        MatchCollection headers = Regex.Matches(
            yaml,
            @"(?m)^--- !u!(?<classId>\d+) &(?<fileId>-?\d+)\r?$",
            RegexOptions.CultureInvariant);
        if (headers.Count == 0)
            throw new InvalidOperationException(
                "GameplayScene YAML contains no document headers.");

        List<YamlDocument> documents = new(headers.Count);
        for (int index = 0; index < headers.Count; index++)
        {
            Match header = headers[index];
            int end = index + 1 < headers.Count
                ? headers[index + 1].Index
                : yaml.Length;
            documents.Add(new YamlDocument(
                int.Parse(header.Groups["classId"].Value,
                    System.Globalization.CultureInfo.InvariantCulture),
                long.Parse(header.Groups["fileId"].Value,
                    System.Globalization.CultureInfo.InvariantCulture),
                yaml.Substring(header.Index, end - header.Index)));
        }
        string prefix = yaml.Substring(0, headers[0].Index);
        Dictionary<long, YamlDocument> byId = documents.ToDictionary(
            value => value.FileId);
        YamlDocument[] fixtureObjects = documents
            .Where(value => value.ClassId == 1
                && Regex.IsMatch(
                    value.Text,
                    "(?m)^  m_Name: "
                        + Regex.Escape(RemovedFixtureName)
                        + "\\r?$",
                    RegexOptions.CultureInvariant))
            .OrderBy(value => value.FileId)
            .ToArray();
        if (fixtureObjects.Length != ExpectedRemovedFixtureCount)
            throw new InvalidOperationException(
                "Synthetic GameplayScene YAML fixture count drifted: "
                + fixtureObjects.Length);

        HashSet<long> removedDocuments = new();
        HashSet<long> removedRootTransforms = new();
        foreach (YamlDocument gameObject in fixtureObjects)
        {
            long[] components = Regex.Matches(
                    gameObject.Text,
                    @"(?m)^  - component: \{fileID: (?<fileId>-?\d+)\}\r?$",
                    RegexOptions.CultureInvariant)
                .Cast<Match>()
                .Select(value => long.Parse(
                    value.Groups["fileId"].Value,
                    System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();
            if (components.Length == 0)
                throw new InvalidOperationException(
                    "Synthetic lifecycle fixture has no serialized components.");
            removedDocuments.Add(gameObject.FileId);
            foreach (long componentId in components)
            {
                if (!byId.TryGetValue(componentId, out YamlDocument component)
                    || !Regex.IsMatch(
                        component.Text,
                        "(?m)^  m_GameObject: \\{fileID: "
                            + gameObject.FileId
                            + "\\}\\r?$",
                        RegexOptions.CultureInvariant))
                {
                    throw new InvalidOperationException(
                        "Synthetic lifecycle fixture component ownership drifted.");
                }
                if (component.ClassId == 4)
                {
                    if (!Regex.IsMatch(component.Text,
                            @"(?m)^  m_Children: \[\]\r?$",
                            RegexOptions.CultureInvariant)
                        || !Regex.IsMatch(component.Text,
                            @"(?m)^  m_Father: \{fileID: 0\}\r?$",
                            RegexOptions.CultureInvariant))
                    {
                        throw new InvalidOperationException(
                            "Synthetic lifecycle fixture is no longer a childless root.");
                    }
                    removedRootTransforms.Add(componentId);
                }
                removedDocuments.Add(componentId);
            }
        }
        if (removedRootTransforms.Count != ExpectedRemovedFixtureCount)
            throw new InvalidOperationException(
                "Synthetic lifecycle fixture root-transform count drifted.");

        int removedRootReferences = 0;
        StringBuilder output = new(yaml.Length);
        output.Append(prefix);
        foreach (YamlDocument document in documents)
        {
            if (removedDocuments.Contains(document.FileId))
                continue;
            string text = document.Text;
            foreach (long transformId in removedRootTransforms)
            {
                string pattern = "(?m)^  - \\{fileID: "
                    + transformId + "\\}\\r?\\n?";
                MatchCollection references = Regex.Matches(
                    text,
                    pattern,
                    RegexOptions.CultureInvariant);
                if (references.Count > 0)
                {
                    removedRootReferences += references.Count;
                    text = Regex.Replace(
                        text,
                        pattern,
                        string.Empty,
                        RegexOptions.CultureInvariant);
                }
            }
            output.Append(text);
        }
        if (removedRootReferences != ExpectedRemovedFixtureCount)
            throw new InvalidOperationException(
                "Synthetic lifecycle fixture SceneRoots reference count drifted: "
                + removedRootReferences);
        string result = output.ToString();
        if (CountToken(result, "m_Name: " + RemovedFixtureName) != 0)
            throw new InvalidOperationException(
                "Synthetic lifecycle fixture name remained after YAML sanitation.");
        removedFixtureCount = fixtureObjects.Length;
        return result;
    }

    private sealed class YamlDocument
    {
        public YamlDocument(int classId, long fileId, string text)
        {
            ClassId = classId;
            FileId = fileId;
            Text = text;
        }

        public int ClassId { get; }
        public long FileId { get; }
        public string Text { get; }
    }
}
#endif
