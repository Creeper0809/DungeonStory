using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class FullGameplayOutcomeExecutionSnapshotExporter
{
    private const string Schema =
        "dungeonstory-full-game-production-execution-snapshots@1";
    private const string Parent =
        "Artifacts/Exports/FullGameplayOutcomeExecutionSnapshots";

    public static void RunFromEnvironment()
    {
        string version = RequireValue(
            Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_FULL_EXECUTION_SNAPSHOT_VERSION"),
            "snapshot version");
        string closurePath = RequireExistingFile(
            Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_FINAL_CLOSURE_PATH"),
            "final closure");
        string migratedPath = RequireExistingFile(
            Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_MIGRATED_EXECUTION_SNAPSHOT_PATH"),
            "migrated execution snapshots");
        string path = Export(version, closurePath, migratedPath);
        UnityEngine.Debug.Log(
            "FULL_GAMEPLAY_OUTCOME_EXECUTION_SNAPSHOTS=PASS; path=" + path);
    }

    public static string Export(
        string version,
        string closurePath,
        string migratedPath)
    {
        string canonicalVersion = RequireVersion(version);
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity project root is unavailable.");
        string output = Path.Combine(projectRoot, Parent, canonicalVersion);
        if (Directory.Exists(output) || File.Exists(output))
        {
            throw new InvalidOperationException(
                "Full execution snapshot export is create-only: " + output);
        }

        JObject closure = StrictLoad(closurePath);
        JArray closureRows = closure["rows"] as JArray
            ?? throw new InvalidOperationException("Final closure rows are missing.");
        JObject[] records = closureRows.Cast<JObject>()
            .Where(row => string.Equals(
                (string)row["disposition"],
                "Record",
                StringComparison.Ordinal))
            .ToArray();
        var allowedTypes = new HashSet<string>(
            records.Select(row => (string)row["outcomeTypeId"]),
            StringComparer.Ordinal);
        if (records.Length != 95 || allowedTypes.Count != 93)
        {
            throw new InvalidOperationException(
                "Final closure denominator must be 95 producers / 93 outcome types.");
        }

        var migratedTypes = new HashSet<string>(
            MigratedProducerOutcomeCatalog.Definitions.Select(
                value => value.OutcomeTypeId.Value),
            StringComparer.Ordinal);
        JObject migrated = StrictLoad(migratedPath);
        JArray migratedRows = migrated["rows"] as JArray
            ?? throw new InvalidOperationException(
                "Migrated execution snapshot rows are missing.");
        if ((int?)migrated["executionCount"] != 59
            || migratedRows.Count != 59
            || !migratedTypes.SetEquals(migratedRows
                .Cast<JObject>()
                .Select(row => (string)row["outcomeTypeId"])))
        {
            throw new InvalidOperationException(
                "Migrated execution snapshot package is not exact 59/59 coverage.");
        }

        var captured = new Dictionary<string, Captured>(StringComparer.Ordinal);
        using (GameplayOutcomePublishedEvidenceCapture.Begin(
                   (step, snapshot) =>
                   {
                       if (!allowedTypes.Contains(snapshot.outcomeTypeId)
                           || migratedTypes.Contains(snapshot.outcomeTypeId))
                       {
                           return;
                       }
                       string key = snapshot.outcomeTypeId + "|" + snapshot.producerId;
                       if (!captured.ContainsKey(key))
                       {
                           captured.Add(
                               key,
                               new Captured(
                                   step,
                                   CloneSnapshot(snapshot)));
                       }
                   }))
        {
            GameplayOutcomeLedgerPhase80BatchRunner.RunBatch();
            GameplayOutcomePublishedEvidenceCapture.ThrowIfFaulted();
        }

        var expectedNonMigrated = new HashSet<string>(allowedTypes, StringComparer.Ordinal);
        expectedNonMigrated.ExceptWith(migratedTypes);
        var capturedTypes = new HashSet<string>(
            captured.Values.Select(value => value.Snapshot.outcomeTypeId),
            StringComparer.Ordinal);
        if (expectedNonMigrated.Count != 36
            || captured.Count != 38
            || !expectedNonMigrated.SetEquals(capturedTypes))
        {
            throw new InvalidOperationException(
                "Owner-specific execution capture is incomplete."
                + " expectedTypes=" + expectedNonMigrated.Count
                + "; capturedTypes=" + capturedTypes.Count
                + "; capturedProducerPairs=" + captured.Count
                + "; missing=" + string.Join(",", expectedNonMigrated.Except(capturedTypes)));
        }
        foreach (IGrouping<string, Captured> group in captured.Values
                     .GroupBy(value => value.Snapshot.outcomeTypeId))
        {
            int expectedCount = string.Equals(
                group.Key,
                "trade-inventory.result",
                StringComparison.Ordinal)
                    ? 3
                    : 1;
            if (group.Count() != expectedCount)
            {
                throw new InvalidOperationException(
                    "Unexpected producer count for " + group.Key
                    + ": " + group.Count());
            }
        }

        var rows = new JArray();
        foreach (JObject source in migratedRows.Cast<JObject>()
                     .OrderBy(row => (string)row["outcomeTypeId"],
                         StringComparer.Ordinal))
        {
            JObject copy = (JObject)source.DeepClone();
            copy["executionSource"] = "migrated-production-entry-58";
            copy["focusedStep"] = "migrated-producer-production-entry-58";
            rows.Add(copy);
        }
        foreach (Captured value in captured.Values
                     .OrderBy(item => item.Snapshot.outcomeTypeId,
                         StringComparer.Ordinal)
                     .ThenBy(item => item.Snapshot.producerId,
                         StringComparer.Ordinal))
        {
            GameplayOutcomeSnapshot snapshot = value.Snapshot;
            JObject exact = JObject.Parse(JsonUtility.ToJson(snapshot));
            string suffix = string.Equals(
                snapshot.outcomeTypeId,
                "trade-inventory.result",
                StringComparison.Ordinal)
                    ? "--" + Slug(snapshot.producerId)
                    : string.Empty;
            rows.Add(new JObject
            {
                ["rowId"] = "execution:" + (rows.Count + 1).ToString("D3"),
                ["actionId"] = "action:"
                    + snapshot.outcomeTypeId.Replace('.', '-') + suffix,
                ["domainId"] = snapshot.outcomeTypeId.Split('.')[0],
                ["outcomeTypeId"] = snapshot.outcomeTypeId,
                ["producerId"] = snapshot.producerId,
                ["productionEntryVerified"] = true,
                ["executionSource"] = "owner-specific-focused-production-scenario",
                ["focusedStep"] = value.Step,
                ["controlledSimulation"] = true,
                ["naturalPlayTelemetry"] = false,
                ["longHorizonExecution"] = false,
                ["outcomeId"] = snapshot.runId + ":" + snapshot.sequence,
                ["exactOutcomeSha256"] =
                    CausalGameplayCanonicalJson.Sha256(exact),
                ["exactOutcome"] = exact
            });
        }
        if (rows.Count != 95
            || rows.Cast<JObject>()
                .Select(row => (string)row["outcomeTypeId"])
                .Distinct(StringComparer.Ordinal).Count() != 93)
        {
            throw new InvalidOperationException(
                "Combined execution rows are not 95 producers / 93 outcome types.");
        }

        string gameCommit = ReadGit(projectRoot, "rev-parse HEAD");
        string dirtyStatus = ReadGit(projectRoot, "status --porcelain -uno");
        Directory.CreateDirectory(output);
        JObject payload = new()
        {
            ["schemaVersion"] = 1,
            ["schema"] = Schema,
            ["version"] = canonicalVersion,
            ["gameCommit"] = gameCommit,
            ["sourceDirtyEntryCount"] = DirtyLineCount(dirtyStatus),
            ["sourceDirtyStatusSha256"] = Sha256Text(dirtyStatus),
            ["authority"] = "unity-production-entry-execution",
            ["controlledSimulation"] = true,
            ["naturalPlayTelemetry"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false,
            ["releaseGate"] = "REJECT_BEFORE_DPO",
            ["connectedProducerCount"] = 95,
            ["connectedOutcomeTypeCount"] = 93,
            ["migratedProductionEntryCount"] = 57,
            ["ownerSpecificProductionEntryCount"] = 38,
            ["rows"] = rows
        };
        string payloadPath = Path.Combine(output, "execution_snapshots.json");
        WriteJson(payloadPath, payload);
        JObject manifest = new()
        {
            ["schemaVersion"] = 1,
            ["schema"] = Schema,
            ["version"] = canonicalVersion,
            ["gameCommit"] = gameCommit,
            ["connectedProducerCount"] = 95,
            ["connectedOutcomeTypeCount"] = 93,
            ["controlledSimulation"] = true,
            ["naturalPlayTelemetry"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false,
            ["releaseGate"] = "REJECT_BEFORE_DPO",
            ["sourceFiles"] = new JArray(
                SourceFileRow("finalClosure", closurePath),
                SourceFileRow("migratedExecutionSnapshots", migratedPath)),
            ["files"] = new JArray(FileRow(output, payloadPath))
        };
        string manifestPath = Path.Combine(output, "manifest.json");
        WriteJson(manifestPath, manifest);
        WriteJson(
            Path.Combine(output, "delivery_manifest.json"),
            new JObject
            {
                ["schemaVersion"] = 1,
                ["schema"] = "dungeonstory-delivery-manifest@1",
                ["files"] = new JArray(
                    FileRow(output, payloadPath),
                    FileRow(output, manifestPath))
            });
        return output;
    }

    private static GameplayOutcomeSnapshot CloneSnapshot(
        GameplayOutcomeSnapshot snapshot) =>
        JsonUtility.FromJson<GameplayOutcomeSnapshot>(
            JsonUtility.ToJson(snapshot))
        ?? throw new InvalidOperationException("Outcome snapshot clone failed.");

    private static JObject StrictLoad(string path) =>
        JObject.Parse(File.ReadAllText(path, Encoding.UTF8));

    private static JObject SourceFileRow(string role, string path) => new()
    {
        ["role"] = role,
        ["path"] = Path.GetFullPath(path).Replace('\\', '/'),
        ["sha256"] = Sha256File(path),
        ["bytes"] = new FileInfo(path).Length
    };

    private static JObject FileRow(string root, string path) => new()
    {
        ["path"] = Path.GetRelativePath(root, path).Replace('\\', '/'),
        ["sha256"] = Sha256File(path),
        ["bytes"] = new FileInfo(path).Length
    };

    private static void WriteJson(string path, JToken value) =>
        File.WriteAllText(
            path,
            value.ToString(Formatting.Indented) + "\n",
            new UTF8Encoding(false));

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 algorithm = SHA256.Create();
        return Hex(algorithm.ComputeHash(stream));
    }

    private static string Sha256Text(string value)
    {
        using SHA256 algorithm = SHA256.Create();
        return Hex(algorithm.ComputeHash(
            Encoding.UTF8.GetBytes(value ?? string.Empty)));
    }

    private static string Hex(byte[] bytes) =>
        BitConverter.ToString(bytes).Replace("-", string.Empty);

    private static int DirtyLineCount(string value) =>
        string.IsNullOrEmpty(value)
            ? 0
            : value.Split(new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries).Length;

    private static string ReadGit(string root, string arguments)
    {
        var start = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "-C \"" + root + "\" " + arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Failed to start git.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException("git failed: " + stderr.Trim());
        return stdout.TrimEnd('\r', '\n');
    }

    private static string RequireExistingFile(string value, string label)
    {
        string path = RequireValue(value, label);
        if (!File.Exists(path))
            throw new FileNotFoundException(label + " is missing.", path);
        return Path.GetFullPath(path);
    }

    private static string RequireVersion(string value)
    {
        string version = RequireValue(value, "snapshot version");
        if (version.Length > 128
            || !char.IsLower(version[0]) && !char.IsDigit(version[0])
            || version.Any(character =>
                !char.IsLower(character)
                && !char.IsDigit(character)
                && character is not '-' and not '_' and not '.'))
        {
            throw new ArgumentException("Snapshot version is not filesystem-safe.");
        }
        return version;
    }

    private static string RequireValue(string value, string label)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new ArgumentException("Missing " + label + ".");
        return normalized;
    }

    private static string Slug(string value) =>
        new string((value ?? string.Empty)
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character)
                ? character
                : '-')
            .ToArray())
            .Trim('-');

    private sealed class Captured
    {
        internal Captured(string step, GameplayOutcomeSnapshot snapshot)
        {
            Step = step ?? string.Empty;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        internal string Step { get; }
        internal GameplayOutcomeSnapshot Snapshot { get; }
    }
}
