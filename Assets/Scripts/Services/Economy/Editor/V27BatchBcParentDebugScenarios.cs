#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Current-source parent evidence for the completed Batch B and Batch C
/// contracts. The parent executes the real focused/broad gates, binds their
/// result to the complete source snapshot and official GameplayScene, and
/// writes deterministic no-op-verifiable reports for portable CI.
/// </summary>
public static class V27BatchBcParentDebugScenarios
{
    public const string BatchBReportPath =
        "Artifacts/QA/v27-batch-b-parent.txt";
    public const string BatchCReportPath =
        "Artifacts/QA/v27-batch-c-parent.txt";

    private const int BatchBExpectedChecks = 40;
    private const int BatchCExpectedInputOwners = 36;
    private const int BatchCExpectedOutputOwners = 10;
    // Stable, reviewable denominator from plan section 16.3. Rows 1-36 are
    // guarded by the complete ProductionEconomy suite; rows 37-40 additionally
    // require their focused parent gate. New content extends the registries and
    // broad suite rather than silently changing this historical closure epoch.
    private static readonly BatchBRow[] BatchBRows =
    {
        Row("b01-cycle-capacity-authority", "production-economy-broad"),
        Row("b02-explicit-live-buffer-authority", "production-economy-broad"),
        Row("b03-p17-maximum-branch-mass", "production-economy-broad"),
        Row("b04-restore-capacity-upper-bound", "production-economy-broad"),
        Row("b05-one-gram-admission-boundary", "production-economy-broad"),
        Row("b06-no-bill-profile-publication", "production-economy-broad"),
        Row("b07-support-rational-maximum", "authored-throughput"),
        Row("b08-generic-recipe-preprojection", "production-economy-broad"),
        Row("b09-capacity-contributor-registry", "production-economy-broad"),
        Row("b10-producer-facility-census", "production-economy-broad"),
        Row("b11-apparel-capability-envelope", "production-economy-broad"),
        Row("b12-surgical-recipe-envelope", "production-economy-broad"),
        Row("b13-combat-shared-eligibility", "production-economy-broad"),
        Row("b14-combat-primary-craft-envelope", "production-economy-broad"),
        Row("b15-combat-recovery-envelope", "production-economy-broad"),
        Row("b16-census-zero-orphans", "production-economy-broad"),
        Row("b17-capacity-source-digest", "production-economy-broad"),
        Row("b18-topology-change-reprojection", "production-economy-broad"),
        Row("b19-destructive-terminal-drain", "production-economy-broad"),
        Row("b20-restore-input-order-invariance", "production-economy-broad"),
        Row("b21-whole-maximum-envelope", "production-economy-broad"),
        Row("b22-projection-execution-registry-parity", "production-economy-broad"),
        Row("b23-certified-seed-eligibility", "production-economy-broad"),
        Row("b24-combat-allowlist-authority", "production-economy-broad"),
        Row("b25-p17-live-current-source-contract", "production-economy-broad"),
        Row("b26-production-normal-boot-contract", "production-economy-broad"),
        Row("b27-output-destination-lifecycle", "output-lifecycle"),
        Row("b28-output-exact-claim-authority", "output-lifecycle"),
        Row("b29-full-path-capacity-canary", "production-economy-broad"),
        Row("b30-direct-demolition-transaction", "production-economy-broad"),
        Row("b31-mutation-epoch-fence", "unified-mutation-fence"),
        Row("b32-structural-loss-fence", "production-economy-broad"),
        Row("b33-world-replacement-retire", "production-economy-broad"),
        Row("b34-active-custody-terminal-drain", "production-economy-broad"),
        Row("b35-crop-whole-vector-publication", "production-economy-broad"),
        Row("b36-destructive-live-integration", "production-economy-broad"),
        Row("b37-reversible-retarget-transaction", "retarget-transaction"),
        Row("b38-support-p95-four-cycle-gate", "clearance-parent"),
        Row("b39-unified-mutation-parent", "unified-mutation-fence"),
        Row("b40-active-multi-facility-retarget", "active-multi-retarget")
    };

    [MenuItem("DungeonStory/V27/Physical Mass/Verify Batch B-C Parents")]
    public static void RunAllFromMenu()
    {
        RunBatchBFromMenu();
        RunBatchCFromMenu();
        Debug.Log("V27_BATCH_BC_PARENT=PASS");
    }

    [MenuItem("DungeonStory/V27/Physical Mass/Verify Batch B Parent")]
    public static void RunBatchBFromMenu()
    {
        EnsureOfficialSceneOpen();
        V27CurrentSourceEvidenceSnapshot source =
            V27CurrentSourceEvidenceDigest.Capture();
        string scene = RequireOfficialScene();
        CapturedLogs logs = new CapturedLogs();
        Application.logMessageReceived += logs.Capture;
        try
        {
            RequireBatchBRowRegistry();
            ProductionFacilityRetargetTransactionDebugScenarios.VerifyFromMenu();
            ProductionAuthoredThroughputEnvelopeDebugScenarios.Validate();
            ProductionOutputClearanceRequirementDebugScenarios.RunAll();
            string profile =
                ProductionOutputClearanceProfileCatalogDebugScenarios.RunAll();
            Require(profile.StartsWith("PASS ", StringComparison.Ordinal),
                "Output-clearance profile gate did not return PASS.");
            RequireActualClearanceProfileResource();
            string portfolio =
                ProductionOutputClearanceCapacityPortfolioGateDebugScenarios
                    .RunAll();
            Require(portfolio.StartsWith(
                    "OUTPUT_CLEARANCE_CAPACITY_PORTFOLIO_GATE_PASS ",
                    StringComparison.Ordinal),
                "Output-clearance capacity portfolio did not return PASS.");
            FacilityBufferDestinationAdmissionFenceDebugScenarios.RunAll();
            ProductionOutputDestinationLifecycleDebugScenarios.RunAll();
            ProductionActiveMultiFacilityRetargetDebugScenarios.VerifyFromMenu();
            ProductionEconomyDebugScenarios.RunAll();
        }
        finally
        {
            Application.logMessageReceived -= logs.Capture;
        }

        logs.RequireClean("Batch B");
        RequireSnapshotUnchanged(source, scene, "Batch B");
        string report = BuildBatchBReport(source, scene);
        WriteTwiceAndRequireNoOp(BatchBReportPath, report);
        Debug.Log("V27_BATCH_B_PARENT=PASS; verified=40/40");
    }

    [MenuItem("DungeonStory/V27/Physical Mass/Verify Batch C Parent")]
    public static void RunBatchCFromMenu()
    {
        EnsureOfficialSceneOpen();
        V27CurrentSourceEvidenceSnapshot source =
            V27CurrentSourceEvidenceDigest.Capture();
        string scene = RequireOfficialScene();
        CapturedLogs logs = new CapturedLogs();
        Application.logMessageReceived += logs.Capture;
        try
        {
            V27FacilityBufferOwnerManifestDebugScenarios.RunFromMenu();
            V27FacilityBufferOwnerManifestDebugScenarios
                .RequireClassificationCoverage();
            V27FacilityBufferOwnerManifestDebugScenarios.RequireOutputClosure();
            V27FacilityBufferOwnerManifestDebugScenarios.RequireFullyMigrated();
        }
        finally
        {
            Application.logMessageReceived -= logs.Capture;
        }

        logs.RequireClean("Batch C");
        RequireSnapshotUnchanged(source, scene, "Batch C");
        ManifestSnapshot manifest = CaptureManifest();
        string report = BuildBatchCReport(source, scene, manifest);
        WriteTwiceAndRequireNoOp(BatchCReportPath, report);
        Debug.Log("V27_BATCH_C_PARENT=PASS; input=36/36; output=10/10");
    }

    private static string BuildBatchBReport(
        V27CurrentSourceEvidenceSnapshot source,
        string scene)
    {
        StringBuilder report = new StringBuilder(2048);
        report.Append("schema=v27-batch-b-parent@1\n")
            .Append("RESULT=PASS\n")
            .Append("batch=B\n")
            .Append("currentSourceDigest=").Append(source.Digest).Append('\n')
            .Append("currentSourceInputCount=")
            .Append(source.InputCount.ToString(CultureInfo.InvariantCulture))
            .Append('\n')
            .Append("currentSourcePathListDigest=")
            .Append(source.PathListDigest).Append('\n')
            .Append("gameplaySceneSha256=").Append(scene).Append('\n')
            .Append("expectedChecks=")
            .Append(BatchBRows.Length).Append('\n')
            .Append("verifiedChecks=")
            .Append(BatchBRows.Length).Append('\n')
            .Append("retargetTransaction=PASS\n")
            .Append("clearanceRequirement=PASS\n")
            .Append("clearanceProfile=PASS\n")
            .Append("clearanceProfileResourceStrict=PASS\n")
            .Append("clearanceCapacityPortfolio=PASS\n")
            .Append("unifiedMutationFence=PASS\n")
            .Append("activeMultiFacilityRetarget=PASS\n")
            .Append("productionEconomyBroad=PASS\n")
            .Append("consoleWarnings=0\n")
            .Append("consoleErrors=0\n")
            .Append("secondWriteDiff=0\n")
            .Append("secondWriteLengthDiff=0\n")
            .Append("secondWriteMtimeDiff=0\n");
        for (int index = 0; index < BatchBRows.Length; index++)
        {
            report.Append("row[").Append(index).Append("]=")
                .Append(BatchBRows[index].Id).Append('|')
                .Append(BatchBRows[index].Gate).Append("|PASS\n");
        }
        return report.ToString();
    }

    private static void RequireBatchBRowRegistry()
    {
        Require(BatchBRows.Length == BatchBExpectedChecks,
            "Batch B closure denominator drifted from 40 rows.");
        for (int index = 0; index < BatchBRows.Length; index++)
        {
            BatchBRow row = BatchBRows[index];
            Require(!string.IsNullOrWhiteSpace(row.Id)
                    && !string.IsNullOrWhiteSpace(row.Gate),
                "Batch B closure row is incomplete at index " + index + ".");
            if (index == 0) continue;
            Require(string.CompareOrdinal(
                    BatchBRows[index - 1].Id,
                    row.Id) < 0,
                "Batch B closure rows must be unique ordinal-sorted.");
        }
    }

    private static BatchBRow Row(string id, string gate) =>
        new BatchBRow(id, gate);

    private static string BuildBatchCReport(
        V27CurrentSourceEvidenceSnapshot source,
        string scene,
        ManifestSnapshot manifest)
    {
        StringBuilder report = new StringBuilder(2048);
        report.Append("schema=v27-batch-c-parent@1\n")
            .Append("RESULT=PASS\n")
            .Append("batch=C\n")
            .Append("currentSourceDigest=").Append(source.Digest).Append('\n')
            .Append("currentSourceInputCount=")
            .Append(source.InputCount.ToString(CultureInfo.InvariantCulture))
            .Append('\n')
            .Append("currentSourcePathListDigest=")
            .Append(source.PathListDigest).Append('\n')
            .Append("gameplaySceneSha256=").Append(scene).Append('\n')
            .Append("inputOwners=").Append(manifest.InputOwners).Append('\n')
            .Append("inputMigrated=").Append(manifest.InputMigrated).Append('\n')
            .Append("inputRemaining=0\n")
            .Append("outputOwners=").Append(manifest.OutputOwners).Append('\n')
            .Append("outputMigrated=").Append(manifest.OutputMigrated).Append('\n')
            .Append("outputRemaining=0\n")
            .Append("remaining=0\n")
            .Append("bypass=0\n")
            .Append("orphan=0\n")
            .Append("unclassified=0\n")
            .Append("ownerManifestCsvSha256=")
            .Append(manifest.CsvSha256).Append('\n')
            .Append("ownerManifestReportSha256=")
            .Append(manifest.ReportSha256).Append('\n')
            .Append("ownerManifestAuthority=PASS\n")
            .Append("fullStoredDestinationCoverage=true\n")
            .Append("consoleWarnings=0\n")
            .Append("consoleErrors=0\n")
            .Append("secondWriteDiff=0\n")
            .Append("secondWriteLengthDiff=0\n")
            .Append("secondWriteMtimeDiff=0\n");
        return report.ToString();
    }

    private static ManifestSnapshot CaptureManifest()
    {
        string reportAbsolute = Resolve(
            V27FacilityBufferOwnerManifestDebugScenarios.ReportPath);
        string csvAbsolute = Resolve(
            V27FacilityBufferOwnerManifestDebugScenarios.CsvPath);
        Require(File.Exists(reportAbsolute) && File.Exists(csvAbsolute),
            "Batch C owner manifest artifacts are missing.");
        Dictionary<string, string> fields = ParseUniqueFields(
            File.ReadAllText(reportAbsolute, StrictUtf8));
        RequireField(fields, "schemaVersion", "3");
        RequireField(fields, "fullStoredDestinationCoverage", "true");
        RequireField(fields, "classificationGate", "PASS");
        RequireField(fields, "fullMigrationGate", "PASS");
        RequireField(fields, "inputRemaining", "0");
        RequireField(fields, "outputRemaining", "0");
        RequireField(fields, "remaining", "0");
        RequireField(fields, "bypass", "0");
        RequireField(fields, "orphan", "0");
        RequireField(fields, "unclassified", "0");
        int inputOwners = ParseInt(fields, "inputOwners");
        int inputMigrated = ParseInt(fields, "inputMigrated");
        int outputOwners = ParseInt(fields, "outputOwners");
        int outputMigrated = ParseInt(fields, "outputMigrated");
        Require(inputOwners == BatchCExpectedInputOwners
                && inputMigrated == inputOwners,
            $"Batch C input owner denominator drifted: "
            + $"{inputMigrated}/{inputOwners}.");
        Require(outputOwners == BatchCExpectedOutputOwners
                && outputMigrated == outputOwners,
            $"Batch C output owner denominator drifted: "
            + $"{outputMigrated}/{outputOwners}.");
        return new ManifestSnapshot(
            inputOwners,
            inputMigrated,
            outputOwners,
            outputMigrated,
            Sha256(File.ReadAllBytes(csvAbsolute)),
            Sha256(File.ReadAllBytes(reportAbsolute)));
    }

    private static void WriteTwiceAndRequireNoOp(string path, string report)
    {
        byte[] bytes = StrictUtf8.GetBytes(report);
        V27BalanceArtifactWriter.WriteIfDifferent(
            path, stream => stream.Write(bytes, 0, bytes.Length));
        string absolute = Resolve(path);
        long length = new FileInfo(absolute).Length;
        long mtime = File.GetLastWriteTimeUtc(absolute).Ticks;
        string digest = Sha256(File.ReadAllBytes(absolute));
        bool secondChanged = V27BalanceArtifactWriter.WriteIfDifferent(
            path, stream => stream.Write(bytes, 0, bytes.Length));
        Require(!secondChanged,
            "Identical parent evidence write was not a no-op: " + path);
        Require(length == new FileInfo(absolute).Length
                && mtime == File.GetLastWriteTimeUtc(absolute).Ticks
                && string.Equals(
                    digest,
                    Sha256(File.ReadAllBytes(absolute)),
                    StringComparison.Ordinal),
            "Identical parent evidence changed byte/length/mtime: " + path);
    }

    private static void RequireSnapshotUnchanged(
        V27CurrentSourceEvidenceSnapshot source,
        string scene,
        string label)
    {
        V27CurrentSourceEvidenceSnapshot after =
            V27CurrentSourceEvidenceDigest.Capture();
        Require(string.Equals(source.Digest, after.Digest, StringComparison.Ordinal)
                && source.InputCount == after.InputCount
                && string.Equals(
                    source.PathListDigest,
                    after.PathListDigest,
                    StringComparison.Ordinal)
                && string.Equals(
                    scene,
                    V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest(),
                    StringComparison.Ordinal),
            label + " source or official scene changed during capture.");
    }

    private static string RequireOfficialScene()
    {
        string scene = V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest();
        Require(string.Equals(
                scene,
                V27CurrentSourceEvidenceDigest.OfficialGameplaySceneSha256,
                StringComparison.Ordinal),
            "Official GameplayScene SHA-256 drifted: " + scene);
        return scene;
    }

    private static void EnsureOfficialSceneOpen()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid()
            && string.Equals(
                active.path,
                V27CurrentSourceEvidenceDigest.GameplayScenePath,
                StringComparison.Ordinal))
            return;
        EditorSceneManager.OpenScene(
            V27CurrentSourceEvidenceDigest.GameplayScenePath,
            OpenSceneMode.Single);
        Scene opened = SceneManager.GetActiveScene();
        Require(opened.IsValid()
                && string.Equals(
                    opened.path,
                    V27CurrentSourceEvidenceDigest.GameplayScenePath,
                    StringComparison.Ordinal),
            "Official GameplayScene could not be opened as the verifier fixture.");
    }

    private static Dictionary<string, string> ParseUniqueFields(string text)
    {
        Dictionary<string, string> fields = new Dictionary<string, string>(
            StringComparer.Ordinal);
        foreach (string raw in (text ?? string.Empty).Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            int separator = line.IndexOf('=');
            if (separator <= 0) continue;
            string key = line.Substring(0, separator);
            string value = line.Substring(separator + 1);
            Require(fields.TryAdd(key, value),
                "Duplicate Batch C owner manifest field: " + key);
        }
        return fields;
    }

    private static void RequireField(
        IReadOnlyDictionary<string, string> fields,
        string key,
        string expected)
    {
        Require(fields.TryGetValue(key, out string actual)
                && string.Equals(actual, expected, StringComparison.Ordinal),
            $"Owner manifest field {key} drifted: "
            + $"expected={expected}; actual={actual ?? "<missing>"}.");
    }

    private static int ParseInt(
        IReadOnlyDictionary<string, string> fields,
        string key)
    {
        int result = -1;
        Require(fields.TryGetValue(key, out string value)
                && V27CanonicalIntegerText.TryParseNonNegativeInt32(
                    value,
                    out result),
            "Owner manifest field is not a non-negative integer: " + key);
        return result;
    }

    private static string Resolve(string relative)
    {
        string root = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        return Path.GetFullPath(Path.Combine(
            root,
            (relative ?? string.Empty).Replace(
                '/', Path.DirectorySeparatorChar)));
    }

    private static string Sha256(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(bytes);
        const string alphabet = "0123456789abcdef";
        char[] result = new char[digest.Length * 2];
        for (int index = 0; index < digest.Length; index++)
        {
            result[index * 2] = alphabet[digest[index] >> 4];
            result[index * 2 + 1] = alphabet[digest[index] & 0x0f];
        }
        return new string(result);
    }

    private static readonly UTF8Encoding StrictUtf8 =
        new UTF8Encoding(false, true);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void RequireActualClearanceProfileResource()
    {
        ProductionOutputClearanceProfileResourceSource strict = new();
        Require(
            strict.Records.Count
                == ProductionOutputClearanceProfileResourceSource
                    .ExpectedProfileCount
            && strict.Records.All(value => value.SampleCount == 32
                && value.DistinctSeedCount == 32),
            "Actual 92-row strict output-clearance resource is incomplete.");
        string root = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string reportPath = Path.Combine(
            root,
            ProductionOutputClearanceStrictCurrentVerifier.ReportPath.Replace(
                '/', Path.DirectorySeparatorChar));
        Require(File.Exists(reportPath),
            "Actual strict current-profile verification report is missing.");
        string report = File.ReadAllText(reportPath, new UTF8Encoding(false, true));
        Require(report.Length > 0
            && report[0] != '\uFEFF'
            && report.IndexOf('\r') < 0
            && report.EndsWith("\n", StringComparison.Ordinal),
            "Actual strict current-profile report encoding is non-canonical.");
        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        string[] lines = report.Substring(0, report.Length - 1).Split('\n');
        foreach (string line in lines)
        {
            int separator = line.IndexOf('=');
            Require(separator > 0
                && fields.TryAdd(
                    line.Substring(0, separator),
                    line.Substring(separator + 1)),
                "Actual strict current-profile report has malformed or duplicate keys.");
        }
        bool acceptedPresent = fields.TryGetValue(
            "accepted",
            out string acceptedToken);
        bool backpressurePresent = fields.TryGetValue(
            "backpressureExpected",
            out string backpressureToken);
        Require(acceptedPresent && backpressurePresent,
            "Output-clearance disposition counts are missing.");
        bool acceptedParsed = V27CanonicalIntegerText.TryParseNonNegativeInt32(
            acceptedToken,
            out int acceptedCount);
        bool backpressureParsed =
            V27CanonicalIntegerText.TryParseNonNegativeInt32(
                backpressureToken,
                out int backpressureCount);
        Require(acceptedParsed
            && backpressureParsed
            && acceptedCount + backpressureCount
                == ProductionOutputClearanceProfileResourceSource
                    .ExpectedProfileCount,
            "Output-clearance disposition denominator is inconsistent.");
        Require(
            fields.Count == 13 + backpressureCount
            && fields.TryGetValue("schema", out string schema)
            && string.Equals(
                schema,
                "v27-production-output-clearance-profile-current@2",
                StringComparison.Ordinal)
            && fields.TryGetValue("result", out string result)
            && string.Equals(result, "PASS", StringComparison.Ordinal)
            && fields.TryGetValue("currentSourceDigest", out string sourceDigest)
            && string.Equals(
                sourceDigest,
                V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest(),
                StringComparison.Ordinal)
            && fields.TryGetValue(
                "gameplaySceneSha256",
                out string gameplaySceneDigest)
            && string.Equals(
                gameplaySceneDigest,
                V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest(),
                StringComparison.Ordinal)
            && fields.TryGetValue(
                "currentPortfolioDigest",
                out string currentPortfolioDigest)
            && ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                currentPortfolioDigest)
            && fields.TryGetValue("catalogAuthorityDigest", out string catalogDigest)
            && string.Equals(
                catalogDigest,
                strict.AuthorityDigest,
                StringComparison.Ordinal)
            && fields.TryGetValue(
                "capacityReviewDigest",
                out string capacityReviewDigest)
            && ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                capacityReviewDigest)
            && fields.TryGetValue("verificationDigest", out string verificationDigest)
            && ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                verificationDigest)
            && fields.TryGetValue("profiles", out string profileCount)
            && string.Equals(
                profileCount,
                ProductionOutputClearanceProfileResourceSource
                    .ExpectedProfileCount.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
            && fields.TryGetValue(
                "blockingCritical",
                out string blockingCritical)
            && string.Equals(blockingCritical, "0", StringComparison.Ordinal)
            && fields.TryGetValue("lookupMismatches", out string lookupMismatches)
            && string.Equals(lookupMismatches, "0", StringComparison.Ordinal)
            && Enumerable.Range(0, backpressureCount).All(
                index => fields.TryGetValue(
                    "backpressure[" + index.ToString(
                        CultureInfo.InvariantCulture) + "]",
                    out string pressure)
                    && pressure.Contains(
                        "diagnostic:PRODUCTION_OUTPUT_CLEARANCE_BACKPRESSURE_EXPECTED",
                        StringComparison.Ordinal)),
            "Actual strict current-profile verification is stale or incomplete.");
    }

    private sealed class CapturedLogs
    {
        private readonly List<string> warnings = new List<string>();
        private readonly List<string> errors = new List<string>();

        internal void Capture(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning)
                warnings.Add(condition ?? string.Empty);
            else if (type is LogType.Error or LogType.Exception or LogType.Assert)
                errors.Add(condition ?? string.Empty);
        }

        internal void RequireClean(string label)
        {
            Require(warnings.Count == 0 && errors.Count == 0,
                label + " emitted unexpected console issues: warnings="
                + warnings.Count + "; errors=" + errors.Count + "; "
                + string.Join(" | ", warnings.Concat(errors)));
        }
    }

    private readonly struct ManifestSnapshot
    {
        internal ManifestSnapshot(
            int inputOwners,
            int inputMigrated,
            int outputOwners,
            int outputMigrated,
            string csvSha256,
            string reportSha256)
        {
            InputOwners = inputOwners;
            InputMigrated = inputMigrated;
            OutputOwners = outputOwners;
            OutputMigrated = outputMigrated;
            CsvSha256 = csvSha256;
            ReportSha256 = reportSha256;
        }

        internal int InputOwners { get; }
        internal int InputMigrated { get; }
        internal int OutputOwners { get; }
        internal int OutputMigrated { get; }
        internal string CsvSha256 { get; }
        internal string ReportSha256 { get; }
    }

    private readonly struct BatchBRow
    {
        internal BatchBRow(string id, string gate)
        {
            Id = id;
            Gate = gate;
        }

        internal string Id { get; }
        internal string Gate { get; }
    }
}
#endif
