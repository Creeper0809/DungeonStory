using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
        public List<string> directories = new List<string>();
        public List<SnapshotEntry> entries = new List<SnapshotEntry>();
    }

    [Serializable]
    private sealed class SnapshotEntry
    {
        public string relativePath;
        public long lastWriteTimeUtcTicks;
        public long length;
        public string sha256;
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
        string snapshotPath = GetSnapshotPath(id);
        if (Directory.Exists(snapshotPath))
        {
            throw new InvalidOperationException(
                $"Persistence snapshot '{id}' already exists. "
                + "Restore it explicitly before capturing a new snapshot with the same id.");
        }

        string persistentRoot = ValidatePersistentRoot(Application.persistentDataPath);
        string filesPath = Path.Combine(snapshotPath, "files");
        Directory.CreateDirectory(filesPath);

        SnapshotManifest manifest = new SnapshotManifest
        {
            rootExisted = Directory.Exists(persistentRoot)
        };
        if (manifest.rootExisted)
        {
            manifest.directories = Directory.GetDirectories(
                    persistentRoot,
                    "*",
                    SearchOption.AllDirectories)
                .Select(path => GetSafeRelativePath(persistentRoot, path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            foreach (string source in Directory.GetFiles(
                         persistentRoot,
                         "*",
                         SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.Ordinal))
            {
                string relativePath = GetSafeRelativePath(persistentRoot, source);
                string destination = GetSafeSnapshotFilePath(filesPath, relativePath);
                string directory = Path.GetDirectoryName(destination);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(source, destination, true);
                FileInfo snapshotFile = new FileInfo(destination);
                manifest.entries.Add(new SnapshotEntry
                {
                    relativePath = relativePath,
                    lastWriteTimeUtcTicks = File.GetLastWriteTimeUtc(source).Ticks,
                    length = snapshotFile.Length,
                    sha256 = ComputeSha256(destination)
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
        string filesPath = Path.Combine(snapshotPath, "files");
        ValidateSnapshotManifest(manifest, filesPath);

        if (Directory.Exists(persistentRoot))
        {
            Directory.Delete(persistentRoot, true);
        }

        if (manifest.rootExisted)
        {
            Directory.CreateDirectory(persistentRoot);
        }

        foreach (string relativeDirectory in manifest.directories)
        {
            string directory = Path.GetFullPath(Path.Combine(
                persistentRoot,
                relativeDirectory));
            GetSafeRelativePath(persistentRoot, directory);
            Directory.CreateDirectory(directory);
        }

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

        VerifyRestoredState(persistentRoot, manifest);
        Directory.Delete(snapshotPath, true);
        return true;
    }

    private static void ValidateSnapshotManifest(
        SnapshotManifest manifest,
        string filesPath)
    {
        manifest.directories ??= new List<string>();
        manifest.entries ??= new List<SnapshotEntry>();
        if (!manifest.rootExisted
            && (manifest.directories.Count > 0 || manifest.entries.Count > 0))
        {
            throw new InvalidDataException(
                "A persistence snapshot for an absent root contains entries.");
        }

        var directoryPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (string relativeDirectory in manifest.directories)
        {
            RequireSafeRelativePath(
                filesPath,
                relativeDirectory,
                "snapshot directory");
            if (!directoryPaths.Add(relativeDirectory))
            {
                throw new InvalidDataException(
                    $"Duplicate persistence snapshot directory '{relativeDirectory}'.");
            }
        }

        var filePaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (SnapshotEntry entry in manifest.entries)
        {
            if (entry == null)
            {
                throw new InvalidDataException(
                    "Persistence snapshot contains a null file entry.");
            }
            RequireSafeRelativePath(
                filesPath,
                entry.relativePath,
                "snapshot file");
            if (!filePaths.Add(entry.relativePath))
            {
                throw new InvalidDataException(
                    $"Duplicate persistence snapshot file '{entry.relativePath}'.");
            }
            if (directoryPaths.Contains(entry.relativePath))
            {
                throw new InvalidDataException(
                    $"Persistence snapshot path '{entry.relativePath}' is both a file and directory.");
            }
            if (entry.lastWriteTimeUtcTicks < DateTime.MinValue.Ticks
                || entry.lastWriteTimeUtcTicks > DateTime.MaxValue.Ticks
                || entry.length < 0
                || string.IsNullOrWhiteSpace(entry.sha256))
            {
                throw new InvalidDataException(
                    $"Persistence snapshot metadata is invalid for '{entry.relativePath}'.");
            }

            string snapshotFile = GetSafeSnapshotFilePath(
                filesPath,
                entry.relativePath);
            if (!File.Exists(snapshotFile))
            {
                throw new InvalidDataException(
                    $"Persistence snapshot file is missing: '{entry.relativePath}'.");
            }
            FileInfo file = new FileInfo(snapshotFile);
            string hash = ComputeSha256(snapshotFile);
            if (file.Length != entry.length
                || !string.Equals(
                    hash,
                    entry.sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Persistence snapshot file failed hash validation: '{entry.relativePath}'.");
            }
        }
    }

    private static void VerifyRestoredState(
        string persistentRoot,
        SnapshotManifest manifest)
    {
        if (!manifest.rootExisted)
        {
            if (Directory.Exists(persistentRoot))
            {
                throw new IOException(
                    "Persistence root still exists after restoring an originally absent root.");
            }
            return;
        }

        if (!Directory.Exists(persistentRoot))
        {
            throw new IOException(
                "Persistence root is missing after snapshot restoration.");
        }

        HashSet<string> expectedDirectories = manifest.directories
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> actualDirectories = Directory.GetDirectories(
                persistentRoot,
                "*",
                SearchOption.AllDirectories)
            .Select(path => GetSafeRelativePath(persistentRoot, path))
            .ToHashSet(StringComparer.Ordinal);
        if (!actualDirectories.SetEquals(expectedDirectories))
        {
            throw new IOException(
                "Persistence directory topology does not match the snapshot.");
        }

        Dictionary<string, SnapshotEntry> expectedFiles = manifest.entries
            .ToDictionary(entry => entry.relativePath, StringComparer.Ordinal);
        string[] actualFiles = Directory.GetFiles(
            persistentRoot,
            "*",
            SearchOption.AllDirectories);
        HashSet<string> actualRelativeFiles = actualFiles
            .Select(path => GetSafeRelativePath(persistentRoot, path))
            .ToHashSet(StringComparer.Ordinal);
        if (!actualRelativeFiles.SetEquals(expectedFiles.Keys))
        {
            throw new IOException(
                "Persistence file set does not match the snapshot.");
        }

        foreach (string filePath in actualFiles)
        {
            string relativePath = GetSafeRelativePath(
                persistentRoot,
                filePath);
            SnapshotEntry expected = expectedFiles[relativePath];
            FileInfo actual = new FileInfo(filePath);
            if (actual.Length != expected.length
                || actual.LastWriteTimeUtc.Ticks
                    != expected.lastWriteTimeUtcTicks
                || !string.Equals(
                    ComputeSha256(filePath),
                    expected.sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(
                    $"Restored persistence file does not match its manifest: '{relativePath}'.");
            }
        }
    }

    private static void RequireSafeRelativePath(
        string root,
        string relativePath,
        string label)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException(
                $"Persistence {label} has an invalid relative path.");
        }

        string combined = Path.GetFullPath(Path.Combine(root, relativePath));
        string normalized = GetSafeRelativePath(
            Path.GetFullPath(root),
            combined);
        if (normalized.Equals(".", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Persistence {label} cannot refer to the snapshot root.");
        }
    }

    private static string ComputeSha256(string path)
    {
        using SHA256 sha256 = SHA256.Create();
        using FileStream stream = File.Open(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        byte[] hash = sha256.ComputeHash(stream);
        StringBuilder builder = new StringBuilder(hash.Length * 2);
        foreach (byte value in hash)
        {
            builder.Append(value.ToString("x2"));
        }
        return builder.ToString();
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
