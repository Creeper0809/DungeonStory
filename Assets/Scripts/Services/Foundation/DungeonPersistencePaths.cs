using System;
using System.IO;
using UnityEngine;

/// <summary>Shared persistence boundary; explicit batch QA never touches player saves.</summary>
public static class DungeonPersistencePaths
{
    public const string VerificationRootVariable = "DUNGEONSTORY_QA_PERSISTENCE_ROOT";

    public static string Root
    {
        get
        {
#if UNITY_EDITOR
            string requested = Environment.GetEnvironmentVariable(VerificationRootVariable);
            if (!string.IsNullOrEmpty(requested))
                return ValidateVerificationRoot(requested,
                    Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT"),
                    Path.GetDirectoryName(Application.dataPath), Application.isBatchMode);
#endif
            return Application.persistentDataPath;
        }
    }

    public static bool IsIsolatedVerification
    {
        get
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(VerificationRootVariable)))
            {
                _ = Root; // Invalid explicit configuration must fail, never fall back.
                return true;
            }
#endif
            return false;
        }
    }

#if UNITY_EDITOR
    public static string ValidateVerificationRoot(
        string requested, string output, string project, bool isBatchMode)
    {
        if (!isBatchMode || string.IsNullOrWhiteSpace(requested)
            || string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(project)
            || !Path.IsPathRooted(requested) || !Path.IsPathRooted(output))
            throw new InvalidOperationException("Isolated persistence requires explicit absolute batch QA paths.");
        string root = Path.GetFullPath(requested).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string evidence = Path.GetFullPath(output).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string qa = Path.GetFullPath(Path.Combine(project, "Artifacts", "QA")) + Path.DirectorySeparatorChar;
        if (!evidence.StartsWith(qa, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(root, Path.Combine(evidence, "persistent-data"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Isolated persistence must be the current QA evidence directory's persistent-data child.");
        for (DirectoryInfo current = new DirectoryInfo(root); current != null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Isolated persistence cannot traverse a reparse point.");
        return root;
    }
#endif
}
