using System;
using System.IO;
using UnityEngine;
using VContainer.Unity;

public sealed class DungeonLegacyPersistenceMigration : IStartable
{
    private const string LegacyCompanyName = "DefaultCompany";
    private const string MarkerRelativePath = "Migration/legacy-default-company-v1.done";

    public static string LastReport { get; private set; } = string.Empty;

    public void Start()
    {
        string destinationRoot = Application.persistentDataPath;
        string legacyRoot = GetLegacyRoot(destinationRoot);
        if (string.IsNullOrWhiteSpace(legacyRoot)
            || string.Equals(destinationRoot, legacyRoot, StringComparison.OrdinalIgnoreCase))
        {
            LastReport = "Legacy persistence migration is not required.";
            return;
        }

        string markerPath = Path.Combine(destinationRoot, MarkerRelativePath);
        if (File.Exists(markerPath))
        {
            LastReport = "Legacy persistence migration was already completed.";
            return;
        }

        try
        {
            int copied = Directory.Exists(legacyRoot)
                ? CopyMissingFiles(legacyRoot, destinationRoot)
                : 0;
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath) ?? destinationRoot);
            File.WriteAllText(
                markerPath,
                $"completedAtUtc={DateTime.UtcNow:O}\nsource={legacyRoot}\ncopiedFiles={copied}");
            LastReport = $"Legacy persistence migration copied {copied} file(s).";
        }
        catch (Exception exception)
        {
            LastReport = "Legacy persistence migration failed: " + exception.Message;
            Debug.LogWarning(LastReport);
        }
    }

    internal static string GetLegacyRoot(string destinationRoot)
    {
        if (string.IsNullOrWhiteSpace(destinationRoot))
        {
            return string.Empty;
        }

        DirectoryInfo productDirectory = Directory.GetParent(destinationRoot);
        DirectoryInfo localLowDirectory = productDirectory?.Parent;
        if (localLowDirectory == null)
        {
            return string.Empty;
        }

        return Path.Combine(localLowDirectory.FullName, LegacyCompanyName, Application.productName);
    }

    internal static int CopyMissingFiles(string sourceRoot, string destinationRoot)
    {
        int copied = 0;
        string normalizedSource = Path.GetFullPath(sourceRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (string sourcePath in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            string fullSource = Path.GetFullPath(sourcePath);
            string relative = fullSource.Substring(normalizedSource.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string destinationPath = Path.Combine(destinationRoot, relative);
            if (File.Exists(destinationPath))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationRoot);
            File.Copy(fullSource, destinationPath, overwrite: false);
            copied++;
        }

        return copied;
    }
}
