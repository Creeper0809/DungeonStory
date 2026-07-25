using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PlayModeVerificationPersistenceSnapshot
{
    private const string SnapshotRoot = "Temp/playmode-persistence-snapshots";

    [Serializable]
    private sealed class SnapshotManifest
    {
        public bool rootExisted;
        public List<SnapshotEntry> entries = new List<SnapshotEntry>();
    }

    [Serializable]
    private sealed class SnapshotEntry
    {
        public string relativePath;
        public long lastWriteTimeUtcTicks;
    }

    static PlayModeVerificationPersistenceSnapshot()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall += RestoreStaleSnapshots;
    }

    public static void CaptureCurrent(string snapshotId)
    {
        string id = ValidateId(snapshotId);
        Restore(id);

        string persistentRoot = ValidatePersistentRoot(Application.persistentDataPath);
        string snapshotPath = GetSnapshotPath(id);
        string filesPath = Path.Combine(snapshotPath, "files");
        Directory.CreateDirectory(filesPath);

        SnapshotManifest manifest = new SnapshotManifest
        {
            rootExisted = Directory.Exists(persistentRoot)
        };
        if (manifest.rootExisted)
        {
            foreach (string source in Directory.GetFiles(persistentRoot, "*", SearchOption.AllDirectories))
            {
                string relativePath = GetSafeRelativePath(persistentRoot, source);
                string destination = GetSafeSnapshotFilePath(filesPath, relativePath);
                string directory = Path.GetDirectoryName(destination);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(source, destination, true);
                manifest.entries.Add(new SnapshotEntry
                {
                    relativePath = relativePath,
                    lastWriteTimeUtcTicks = File.GetLastWriteTimeUtc(source).Ticks
                });
            }
        }

        File.WriteAllText(
            Path.Combine(snapshotPath, "manifest.json"),
            JsonUtility.ToJson(manifest, true));
    }

    public static bool Restore(string snapshotId)
    {
        string id = ValidateId(snapshotId);
        string snapshotPath = GetSnapshotPath(id);
        string manifestPath = Path.Combine(snapshotPath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return false;
        }

        SnapshotManifest manifest = JsonUtility.FromJson<SnapshotManifest>(File.ReadAllText(manifestPath))
            ?? new SnapshotManifest();
        string persistentRoot = ValidatePersistentRoot(Application.persistentDataPath);
        if (Directory.Exists(persistentRoot))
        {
            foreach (string currentPath in Directory.GetFiles(
                         persistentRoot,
                         "*",
                         SearchOption.AllDirectories))
            {
                GetSafeRelativePath(persistentRoot, currentPath);
                File.Delete(currentPath);
            }
        }

        if (manifest.rootExisted || manifest.entries.Count > 0)
        {
            Directory.CreateDirectory(persistentRoot);
        }

        string filesPath = Path.Combine(snapshotPath, "files");
        foreach (SnapshotEntry entry in manifest.entries)
        {
            string source = GetSafeSnapshotFilePath(filesPath, entry.relativePath);
            string destination = Path.GetFullPath(Path.Combine(persistentRoot, entry.relativePath));
            GetSafeRelativePath(persistentRoot, destination);
            string directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(source, destination, true);
            File.SetLastWriteTimeUtc(destination, new DateTime(entry.lastWriteTimeUtcTicks, DateTimeKind.Utc));
        }

        Directory.Delete(snapshotPath, true);
        return true;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            RestoreStaleSnapshots();
        }
    }

    private static void RestoreStaleSnapshots()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !Directory.Exists(SnapshotRoot))
        {
            return;
        }

        foreach (string directory in Directory.GetDirectories(SnapshotRoot))
        {
            string id = Path.GetFileName(directory);
            try
            {
                Restore(id);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    private static string GetSnapshotPath(string snapshotId)
    {
        string root = Path.GetFullPath(SnapshotRoot);
        string path = Path.GetFullPath(Path.Combine(root, snapshotId));
        GetSafeRelativePath(root, path);
        return path;
    }

    private static string GetSafeSnapshotFilePath(string filesRoot, string relativePath)
    {
        string path = Path.GetFullPath(Path.Combine(filesRoot, relativePath));
        GetSafeRelativePath(Path.GetFullPath(filesRoot), path);
        return path;
    }

    private static string ValidateId(string snapshotId)
    {
        if (string.IsNullOrWhiteSpace(snapshotId)
            || snapshotId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || snapshotId.Contains(Path.DirectorySeparatorChar)
            || snapshotId.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Invalid persistence snapshot id.", nameof(snapshotId));
        }

        return snapshotId;
    }

    private static string ValidatePersistentRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Unity persistent data path is empty.");
        }

        string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string volumeRoot = Path.GetPathRoot(fullPath)?.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(volumeRoot)
            || string.Equals(fullPath, volumeRoot, StringComparison.OrdinalIgnoreCase)
            || Directory.GetParent(fullPath) == null)
        {
            throw new InvalidOperationException($"Unsafe persistent data path: {fullPath}");
        }

        return fullPath;
    }

    private static string GetSafeRelativePath(string root, string path)
    {
        string relativePath = Path.GetRelativePath(root, Path.GetFullPath(path));
        if (Path.IsPathRooted(relativePath)
            || relativePath.Equals("..", StringComparison.Ordinal)
            || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Path escaped its expected root: {path}");
        }

        return relativePath;
    }
}
