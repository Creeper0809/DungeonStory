#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

internal sealed class GameplayModuleSelectionSourceCheckpoint
{
    internal GameplayModuleSelectionSourceCheckpoint(
        string checkpointId,
        string ownerId,
        int revision,
        JObject value)
    {
        CheckpointId = RequireStable(checkpointId, nameof(checkpointId));
        OwnerId = RequireStable(ownerId, nameof(ownerId));
        if (revision < 0)
            throw new ArgumentOutOfRangeException(nameof(revision));
        Revision = revision;
        Value = value == null
            ? throw new ArgumentNullException(nameof(value))
            : (JObject)value.DeepClone();
    }

    internal string CheckpointId { get; }
    internal string OwnerId { get; }
    internal int Revision { get; }
    internal JObject Value { get; }

    private static string RequireStable(string value, string name)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (!string.Equals(canonical, value, StringComparison.Ordinal)
            || !GameplayOutcomeStableIdSyntax.IsValid(canonical))
            throw new ArgumentException("A canonical stable ID is required.", name);
        return canonical;
    }
}

/// <summary>
/// Create-only source-v8 package writer. Rows, owner checkpoints and source
/// files are all mutually hash-bound; response data is intentionally absent.
/// </summary>
internal static class GameplayModuleSelectionSourceV8PackageExporter
{
    internal const string RowsFileName = "source_rows.jsonl";

    internal static byte[] CheckpointFileBytes(JObject value) => AppendNewline(
        GameplayModuleSelectionCanonicalJson.ToUtf8(
            value ?? throw new ArgumentNullException(nameof(value))));

    internal static JObject Export(
        string outputDirectory,
        string sourcePackageId,
        IReadOnlyList<JObject> sourceRows,
        IReadOnlyList<GameplayModuleSelectionSourceCheckpoint> checkpoints,
        IReadOnlyList<string> sourceFiles)
    {
        string root = Path.GetFullPath(
            Path.Combine(Application.dataPath, ".."));
        string output = Path.GetFullPath(outputDirectory
            ?? throw new ArgumentNullException(nameof(outputDirectory)));
        if (Directory.Exists(output) || File.Exists(output))
            throw new InvalidOperationException(
                "Source-v8 output is create-only: " + output);
        string packageId = RequireStable(sourcePackageId, nameof(sourcePackageId));
        JObject[] rows = (sourceRows
                ?? throw new ArgumentNullException(nameof(sourceRows)))
            .Select(value => value == null
                ? throw new InvalidOperationException("A source-v8 row is null.")
                : (JObject)value.DeepClone())
            .ToArray();
        if (rows.Length == 0)
            throw new InvalidOperationException("Source-v8 export requires rows.");
        GameplayModuleSelectionSourceCheckpoint[] checkpointValues = (checkpoints
                ?? throw new ArgumentNullException(nameof(checkpoints)))
            .ToArray();
        if (checkpointValues.Length == 0 || checkpointValues.Any(value => value == null)
            || checkpointValues.Select(value => value.CheckpointId)
                .Distinct(StringComparer.Ordinal).Count() != checkpointValues.Length)
        {
            throw new InvalidOperationException(
                "Source-v8 checkpoints must be non-empty and unique.");
        }
        ValidateRows(rows, checkpointValues);

        byte[] rowBytes = BuildJsonLines(rows);
        List<(GameplayModuleSelectionSourceCheckpoint Value, string Path,
            byte[] Bytes, string Hash)> checkpointFiles = checkpointValues
            .OrderBy(value => value.CheckpointId, StringComparer.Ordinal)
            .Select(value =>
            {
                string relative = "checkpoints/" + FileName(value.CheckpointId)
                    + ".json";
                byte[] bytes = CheckpointFileBytes(value.Value);
                return (value, relative, bytes,
                    GameplayModuleSelectionCanonicalJson.Sha256Prefixed(bytes));
            }).ToList();
        JObject[] sourceFileRows = CaptureSources(root, sourceFiles);
        JObject first = rows[0];
        JObject manifest = new()
        {
            ["schemaVersion"] = GameplayModuleSelectionSourceV8RowBuilder
                .SourceSchemaVersion,
            ["taskId"] = GameplayModuleSelectionSourceV8RowBuilder.TaskId,
            ["sourcePackageId"] = packageId,
            ["gameCommit"] = (string)first["gameCommit"],
            ["dirtySourceSha256"] = (string)first["dirtySourceSha256"],
            ["contentSha256"] = (string)first["contentSha256"],
            ["catalogSha256"] = (string)first["catalogSha256"],
            ["rowsFile"] = RowsFileName,
            ["rowsSha256"] = GameplayModuleSelectionCanonicalJson
                .Sha256Prefixed(rowBytes),
            ["rowCount"] = rows.Length,
            ["checkpoints"] = new JArray(checkpointFiles.Select(item =>
                new JObject
                {
                    ["checkpointId"] = item.Value.CheckpointId,
                    ["ownerId"] = item.Value.OwnerId,
                    ["revision"] = item.Value.Revision,
                    ["path"] = item.Path,
                    ["sha256"] = item.Hash
                })),
            ["sourceFiles"] = new JArray(sourceFileRows)
        };
        byte[] manifestBytes = AppendNewline(
            GameplayModuleSelectionCanonicalJson.ToUtf8(manifest));

        try
        {
            Directory.CreateDirectory(output);
            WriteNew(Path.Combine(output, RowsFileName), rowBytes);
            foreach (var item in checkpointFiles)
                WriteNew(Path.Combine(output, item.Path.Replace('/',
                    Path.DirectorySeparatorChar)), item.Bytes);
            WriteNew(Path.Combine(output, "manifest.json"), manifestBytes);
        }
        catch
        {
            // Preserve a partial create-only directory as failure evidence.
            // A later attempt must use a new versioned output path.
            throw;
        }

        return new JObject
        {
            ["outputDirectory"] = output.Replace('\\', '/'),
            ["rowCount"] = rows.Length,
            ["checkpointCount"] = checkpointValues.Length,
            ["manifestSha256"] = GameplayModuleSelectionCanonicalJson
                .Sha256Prefixed(manifestBytes),
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false
        };
    }

    private static void ValidateRows(
        IReadOnlyList<JObject> rows,
        IReadOnlyList<GameplayModuleSelectionSourceCheckpoint> checkpoints)
    {
        Dictionary<string, GameplayModuleSelectionSourceCheckpoint> byId =
            checkpoints.ToDictionary(value => value.CheckpointId,
                StringComparer.Ordinal);
        string[] identityKeys =
        {
            "gameCommit", "dirtySourceSha256", "contentSha256", "catalogSha256"
        };
        foreach (JObject row in rows)
        {
            if ((int?)row["schemaVersion"] != 8
                || !string.Equals((string)row["taskId"],
                    GameplayModuleSelectionSourceV8RowBuilder.TaskId,
                    StringComparison.Ordinal)
                || row["executionBinding"] is not JObject execution)
            {
                throw new InvalidOperationException(
                    "A package row is not a source-v8 module-selection row.");
            }
            string checkpointId = (string)execution["ownerCheckpointId"];
            if (!byId.TryGetValue(checkpointId, out var checkpoint)
                || !string.Equals((string)execution["ownerId"],
                    checkpoint.OwnerId, StringComparison.Ordinal)
                || (int?)execution["ownerRevision"] != checkpoint.Revision
                || !string.Equals((string)execution["ownerCheckpointSha256"],
                    GameplayModuleSelectionCanonicalJson.Sha256Prefixed(
                        CheckpointFileBytes(checkpoint.Value)),
                    StringComparison.Ordinal)
                || row["authoritySidecar"] is not JObject authority
                || !string.Equals((string)authority["ownerSnapshotSha256"],
                    GameplayModuleSelectionCanonicalJson.Hash(checkpoint.Value),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A source-v8 row is not bound to its exact owner checkpoint.");
            }
            foreach (string key in identityKeys)
            {
                if (!JToken.DeepEquals(row[key], rows[0][key]))
                    throw new InvalidOperationException(
                        "One source-v8 package cannot mix source identities.");
            }
        }
    }

    private static JObject[] CaptureSources(
        string projectRoot,
        IReadOnlyList<string> paths)
    {
        string[] values = (paths ?? throw new ArgumentNullException(nameof(paths)))
            .Select(value => value?.Replace('\\', '/') ?? string.Empty)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (values.Length == 0 || values.Any(value => value.Length == 0
                || Path.IsPathRooted(value) || value.Contains("..", StringComparison.Ordinal))
            || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            throw new InvalidOperationException(
                "Source-v8 source paths must be unique project-relative files.");
        }
        return values.Select(value =>
        {
            string full = Path.GetFullPath(Path.Combine(
                projectRoot, value.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(full))
                throw new FileNotFoundException(
                    "A source-v8 authority source is missing.", full);
            return new JObject
            {
                ["path"] = value,
                ["sha256"] = GameplayModuleSelectionCanonicalJson
                    .Sha256Prefixed(File.ReadAllBytes(full))
            };
        }).ToArray();
    }

    private static byte[] BuildJsonLines(IEnumerable<JObject> rows)
    {
        using MemoryStream stream = new();
        foreach (JObject row in rows)
        {
            byte[] value = GameplayModuleSelectionCanonicalJson.ToUtf8(row);
            stream.Write(value, 0, value.Length);
            stream.WriteByte((byte)'\n');
        }
        return stream.ToArray();
    }

    private static byte[] AppendNewline(byte[] value)
    {
        byte[] result = new byte[value.Length + 1];
        Buffer.BlockCopy(value, 0, result, 0, value.Length);
        result[result.Length - 1] = (byte)'\n';
        return result;
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Output parent is missing."));
        using FileStream stream = new(
            path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(flushToDisk: true);
    }

    private static string FileName(string stableId)
    {
        string result = new(stableId.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_'
                ? character
                : '_').ToArray());
        return result.Length <= 120 ? result : result.Substring(0, 120);
    }

    private static string RequireStable(string value, string name)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (!string.Equals(canonical, value, StringComparison.Ordinal)
            || !GameplayOutcomeStableIdSyntax.IsValid(canonical))
            throw new ArgumentException("A canonical stable ID is required.", name);
        return canonical;
    }
}
#endif
