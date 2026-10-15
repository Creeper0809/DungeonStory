#if UNITY_EDITOR
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

public static class MigratedProducerExecutionSnapshotExporter
{
    private const string Schema =
        "dungeonstory-migrated-production-execution-snapshots@2";
    private const string Parent =
        "Artifacts/Exports/MigratedProducerExecutionSnapshots";

    public static void RunFromEnvironment()
    {
        string version = RequireVersion(Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_MIGRATED_EXECUTION_SNAPSHOT_VERSION"));
        VerifyCoverageValidation();
        string path = Export(version);
        UnityEngine.Debug.Log(
            "MIGRATED_PRODUCER_EXECUTION_SNAPSHOTS=PASS; path=" + path);
    }

    public static string Export(string version)
    {
        string canonicalVersion = RequireVersion(version);
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity project root is unavailable.");
        string output = Path.Combine(projectRoot, Parent, canonicalVersion);
        if (Directory.Exists(output) || File.Exists(output))
        {
            throw new InvalidOperationException(
                "Execution snapshot export is create-only: " + output);
        }

        string gameCommit = ReadGit(projectRoot, "rev-parse HEAD");
        string dirtyStatus = ReadGit(projectRoot, "status --porcelain -uno");
        var captured = new Dictionary<
            MigratedProducerOutcomeKind,
            MigratedProducerProductionTransitionEvidence>();
        using (MigratedProducerProductionTransitionEvidenceCapture.Begin(
                   evidence =>
                   {
                       if (!captured.ContainsKey(evidence.Kind))
                       {
                           captured.Add(
                               evidence.Kind,
                               new MigratedProducerProductionTransitionEvidence(
                                   evidence.Kind,
                                   evidence.Identity,
                                   CloneSnapshot(evidence.Outcome),
                                   evidence.BeforeState,
                                   evidence.AfterState));
                       }
                   }))
        {
            if (!MigratedProducerProductionEntry57E2EDebugScenarios.RunAll(
                    throwOnFailure: true))
            {
                throw new InvalidOperationException(
                    "The current production-entry E2E suite failed.");
            }
        }

        MigratedProducerOutcomeKind[] expected =
            MigratedProducerOutcomeCatalog.Definitions
                .Select(value => value.Kind)
                .OrderBy(value => (int)value)
                .ToArray();
        RequireExactCoverage(expected, captured.Keys.ToArray());

        var rows = new JArray();
        var outcomeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (MigratedProducerOutcomeKind kind in expected)
        {
            MigratedProducerOutcomeDefinition definition =
                MigratedProducerOutcomeCatalog.Get(kind);
            MigratedProducerProductionTransitionEvidence transition =
                captured[kind];
            GameplayOutcomeSnapshot snapshot = transition.Outcome;
            if (!string.Equals(
                    snapshot.outcomeTypeId,
                    definition.OutcomeTypeId.Value,
                    StringComparison.Ordinal)
                || !string.Equals(snapshot.producerId, definition.ProducerId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Captured outcome type/producer drifted for " + kind);
            }
            string outcomeId = snapshot.runId + ":" + snapshot.sequence;
            if (!outcomeIds.Add(outcomeId))
            {
                throw new InvalidOperationException(
                    "Captured outcome IDs must be unique: " + outcomeId);
            }
            JObject exactOutcome = JObject.Parse(JsonUtility.ToJson(snapshot));
            var transitionState = new JObject
            {
                ["before"] = transition.BeforeState,
                ["after"] = transition.AfterState
            };
            rows.Add(new JObject
            {
                ["rowId"] = "execution:" + ((int)kind).ToString("D2"),
                ["actionId"] = "action:" + definition.OutcomeTypeId.Value
                    .Replace('.', '-'),
                ["domainId"] = definition.DomainTag.Value
                    .Replace("domain:", string.Empty),
                ["outcomeKind"] = kind.ToString(),
                ["outcomeTypeId"] = definition.OutcomeTypeId.Value,
                ["producerId"] = definition.ProducerId,
                ["sourceTypeName"] = definition.SourceTypeName,
                ["koreanLabel"] = definition.KoreanLabel,
                ["productionEntryVerified"] = true,
                ["coverageContract"] =
                    "success+reject-rollback+delivery-retry",
                ["controlledSimulation"] = true,
                ["naturalPlayTelemetry"] = false,
                ["longHorizonExecution"] = false,
                ["stateTransitionVerified"] = true,
                ["stateSnapshotEncoding"] =
                    "authoritative-owner-capture@1",
                ["beforeStateSha256"] =
                    Sha256Text(transition.BeforeState),
                ["afterStateSha256"] =
                    Sha256Text(transition.AfterState),
                ["transitionSha256"] =
                    CausalGameplayCanonicalJson.Sha256(transitionState),
                ["beforeState"] = transition.BeforeState,
                ["afterState"] = transition.AfterState,
                ["outcomeId"] = outcomeId,
                ["exactOutcomeSha256"] =
                    CausalGameplayCanonicalJson.Sha256(exactOutcome),
                ["exactOutcome"] = exactOutcome
            });
        }

        Directory.CreateDirectory(output);
        JObject payload = new()
        {
            ["schemaVersion"] = 2,
            ["schema"] = Schema,
            ["version"] = canonicalVersion,
            ["gameCommit"] = gameCommit,
            ["sourceDirtyEntryCount"] = DirtyLineCount(dirtyStatus),
            ["sourceDirtyStatusSha256"] = Sha256Text(dirtyStatus),
            ["authority"] = "unity-production-entry-execution",
            ["coverageContract"] =
                "success+reject-rollback+delivery-retry",
            ["controlledSimulation"] = true,
            ["naturalPlayTelemetry"] = false,
            ["stateTransitionVerifiedCount"] = rows.Count,
            ["stateCaptureNotes"] = new JArray(
                "Snapshots are controlled regression fixture evidence, not natural-play training samples.",
                "WorldResourceCycleResult captures the real migrated outcome-owner revision around a production port; its prepared input is fixture data, not a WorldResourceRuntime/physical-output owner transition.",
                "Stock-policy sale snapshots capture ResourceStockPolicyRuntime only; physical item, money and economic-ledger joins are asserted separately.",
                "Work-order/repair snapshots capture WorkOrderRuntime and regional acceptance captures RegionalSupplyContractRuntime; they are domain owner save aggregates, not whole-world or physical-inventory snapshots."),
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false,
            ["releaseGate"] = "REJECT_BEFORE_DPO",
            ["executionCount"] = rows.Count,
            ["rows"] = rows
        };
        // Parse the same frozen input for both exports. The second pass never
        // replays commands or substitutes results from another execution.
        string frozenInput = payload.ToString(Formatting.None);
        string frozenPath = Path.Combine(output, "frozen_execution_input.json");
        WriteJson(frozenPath, JObject.Parse(frozenInput));
        string frozenHash = Sha256File(frozenPath);
        WritePackage(output, JObject.Parse(File.ReadAllText(frozenPath, Encoding.UTF8)));
        string repeatDirectory = Path.Combine(output, "determinism-repeat");
        Directory.CreateDirectory(repeatDirectory);
        WritePackage(repeatDirectory, JObject.Parse(File.ReadAllText(frozenPath, Encoding.UTF8)));
        if (!string.Equals(frozenHash, Sha256File(frozenPath), StringComparison.Ordinal))
            throw new InvalidOperationException("Frozen execution input changed between exports.");
        var comparisons = new JArray();
        foreach (string name in new[]
                 { "execution_snapshots.json", "manifest.json", "delivery_manifest.json" })
        {
            string first = Path.Combine(output, name);
            string second = Path.Combine(repeatDirectory, name);
            string firstHash = Sha256File(first);
            string secondHash = Sha256File(second);
            if (new FileInfo(first).Length != new FileInfo(second).Length
                || !string.Equals(firstHash, secondHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Frozen-input export was not deterministic: " + name);
            comparisons.Add(new JObject
            {
                ["path"] = name,
                ["bytes"] = new FileInfo(first).Length,
                ["firstSha256"] = firstHash,
                ["secondSha256"] = secondHash,
                ["match"] = true
            });
        }
        WriteJson(Path.Combine(output, "determinism_report.json"), new JObject
        {
            ["schema"] = "dungeonstory-frozen-execution-export-determinism@1",
            ["passed"] = true,
            ["frozenInput"] = FileRow(output, frozenPath),
            ["comparisonScope"] = "all three delivered package files; operational timestamps/PIDs excluded",
            ["files"] = comparisons,
            ["trainingEligible"] = false,
            ["humanApprovalClaimed"] = false
        });
        return output;
    }

    private static void WritePackage(string output, JObject payload)
    {
        string payloadPath = Path.Combine(output, "execution_snapshots.json");
        WriteJson(payloadPath, payload);
        JObject manifest = new()
        {
            ["schemaVersion"] = 2,
            ["schema"] = Schema,
            ["version"] = payload["version"].DeepClone(),
            ["gameCommit"] = payload["gameCommit"].DeepClone(),
            ["executionCount"] = payload["executionCount"].DeepClone(),
            ["controlledSimulation"] = true,
            ["naturalPlayTelemetry"] = false,
            ["stateTransitionVerifiedCount"] = payload["stateTransitionVerifiedCount"].DeepClone(),
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false,
            ["releaseGate"] = "REJECT_BEFORE_DPO",
            ["files"] = new JArray(FileRow(output, payloadPath))
        };
        string manifestPath = Path.Combine(output, "manifest.json");
        WriteJson(manifestPath, manifest);
        JObject delivery = new()
        {
            ["schemaVersion"] = 1,
            ["schema"] = "dungeonstory-delivery-manifest@1",
            ["files"] = new JArray(
                FileRow(output, payloadPath),
                FileRow(output, manifestPath))
        };
        WriteJson(Path.Combine(output, "delivery_manifest.json"), delivery);
    }

    // The live definition catalog owns the set, not a historical cardinality.
    private static void RequireExactCoverage(
        MigratedProducerOutcomeKind[] expected,
        MigratedProducerOutcomeKind[] observed)
    {
        MigratedProducerOutcomeKind[] missing = expected.Except(observed).ToArray();
        MigratedProducerOutcomeKind[] unexpected = observed.Except(expected).ToArray();
        if (expected.Length == 0
            || expected.Distinct().Count() != expected.Length
            || observed.Distinct().Count() != observed.Length
            || observed.Length != expected.Length
            || missing.Length > 0
            || unexpected.Length > 0)
        {
            throw new InvalidOperationException(
                "Production execution capture differs from the current definition catalog."
                + " expected=" + expected.Length
                + "; captured=" + observed.Length
                + "; missing=" + string.Join(",", missing)
                + "; unexpected=" + string.Join(",", unexpected));
        }
    }

    private static void VerifyCoverageValidation()
    {
        MigratedProducerOutcomeKind[] expected = MigratedProducerOutcomeCatalog
            .Definitions.Select(value => value.Kind).ToArray();
        RequireExactCoverage(expected, expected);
        RequireCoverageRejected(expected, expected.Skip(1).ToArray());
        RequireCoverageRejected(expected, expected.Append((MigratedProducerOutcomeKind)(-1)).ToArray());
        RequireCoverageRejected(expected, expected.Append(expected[0]).ToArray());
        RequireCoverageRejected(expected.Append(expected[0]).ToArray(), expected);
        RequireCoverageRejected(Array.Empty<MigratedProducerOutcomeKind>(), Array.Empty<MigratedProducerOutcomeKind>());
        UnityEngine.Debug.Log("MIGRATED_EXPORT_COVERAGE_CONTRACT=PASS; positive=1; negative=5");
    }

    private static void RequireCoverageRejected(
        MigratedProducerOutcomeKind[] expected,
        MigratedProducerOutcomeKind[] observed)
    {
        try
        {
            RequireExactCoverage(expected, observed);
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException("Invalid export coverage was accepted.");
    }

    private static GameplayOutcomeSnapshot CloneSnapshot(
        GameplayOutcomeSnapshot snapshot)
    {
        string json = JsonUtility.ToJson(snapshot);
        GameplayOutcomeSnapshot clone = JsonUtility.FromJson<
            GameplayOutcomeSnapshot>(json);
        return clone ?? throw new InvalidOperationException(
            "Captured gameplay outcome could not be cloned.");
    }

    private static JObject FileRow(string root, string path) => new()
    {
        ["path"] = Path.GetRelativePath(root, path).Replace('\\', '/'),
        ["sha256"] = Sha256File(path),
        ["bytes"] = new FileInfo(path).Length
    };

    private static void WriteJson(string path, JToken value)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using StreamWriter writer = new(stream, new UTF8Encoding(false));
        writer.Write(value.ToString(Formatting.Indented) + "\n");
    }

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
                    StringSplitOptions.RemoveEmptyEntries)
                .Length;

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
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException("git failed: " + error.Trim());
        return output.TrimEnd('\r', '\n');
    }

    private static string RequireVersion(string value)
    {
        string version = value?.Trim() ?? string.Empty;
        if (version.Length is < 1 or > 128
            || !char.IsLower(version[0]) && !char.IsDigit(version[0])
            || version.Any(character =>
                !char.IsLower(character)
                && !char.IsDigit(character)
                && character is not '-' and not '_' and not '.'))
        {
            throw new ArgumentException(
                "A lowercase filesystem-safe version is required.",
                nameof(value));
        }
        return version;
    }
}
#endif
