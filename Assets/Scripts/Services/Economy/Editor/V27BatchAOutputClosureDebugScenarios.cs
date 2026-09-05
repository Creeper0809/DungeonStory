#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class V27BatchAOutputClosureDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/v27-batch-a-output-closure.txt";

    private const string OfficialGameplaySceneSha256 =
        "6c35a17693d3cedca2c85b89b22a8bff9b5bae6de88c01b255481c058d2aee40";
    private const string ManifestCsvSchema = "facility-buffer-owner-manifest-csv@1";
    private const string ManifestReportSchema =
        "facility-buffer-owner-manifest-report@3";
    private const string LiveReportSchema =
        "physical-item-logistics-playmode-report@1";
    private const string FocusedReportSchema =
        "world-resource-transaction-fault-matrix@1";

    private static readonly Regex DeliveryInvocationPattern = new Regex(
        @"\.\s*(TryRequestFacilityDelivery|TryRequestItemDelivery|TryRequestStackDelivery)\s*\(",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [MenuItem("DungeonStory/V27/Physical Mass/Verify Batch A Output Closure")]
    public static void RunFromMenu()
    {
        string report = RunAll();
        byte[] bytes = Utf8(report);
        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
            stream.Write(bytes, 0, bytes.Length));
        string absolute = ResolvePath(ReportPath);
        long length = new FileInfo(absolute).Length;
        long mtime = File.GetLastWriteTimeUtc(absolute).Ticks;
        string digest = Sha256(File.ReadAllBytes(absolute));
        bool secondChanged = V27BalanceArtifactWriter.WriteIfDifferent(
            ReportPath, stream => stream.Write(bytes, 0, bytes.Length));
        Require(!secondChanged
                && length == new FileInfo(absolute).Length
                && mtime == File.GetLastWriteTimeUtc(absolute).Ticks
                && string.Equals(
                    digest,
                    Sha256(File.ReadAllBytes(absolute)),
                    StringComparison.Ordinal),
            "Batch A parent second write changed byte/length/mtime.");
        Debug.Log("V27_BATCH_A_OUTPUT_CLOSURE=PASS");
    }

    [MenuItem("DungeonStory/V27/Physical Mass/Verify Batch A Evidence Contract Focused")]
    public static void RunEvidenceContractFocusedTests()
    {
        string root = ProjectRoot();
        string directory = Path.Combine(root, "Temp", "v27-batch-a-evidence-contract");
        string path = Path.Combine(directory, "report.txt");
        Directory.CreateDirectory(directory);
        try
        {
            const string source =
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            const string scene =
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            string valid = "[PASS] REQUIRED_LABEL\n"
                + "capturedErrors=0; <none>\n"
                + "capturedWarnings=0; <none>\n"
                + "currentSourceDigest=" + source + "\n"
                + "gameplaySceneSha256=" + scene + "\n"
                + "RESULT=PASS; failures=0; <none>\n";

            File.WriteAllText(path, valid, new UTF8Encoding(false, true));
            ArtifactEvidence first = RequireLiveReport(
                "focused-valid", path, source, scene, "REQUIRED_LABEL");
            ArtifactEvidence second = RequireLiveReport(
                "focused-valid", path, source, scene, "REQUIRED_LABEL");
            Require(first.ByteLength == second.ByteLength
                    && string.Equals(first.ByteSha256, second.ByteSha256,
                        StringComparison.Ordinal)
                    && string.Equals(first.CanonicalLine, second.CanonicalLine,
                        StringComparison.Ordinal),
                "Identical Batch A evidence capture was not deterministic.");

            RequireThrows(
                () => RequireLiveReport(
                    "focused-stale-source", path, new string('c', 64), scene,
                    "REQUIRED_LABEL"),
                "Batch A evidence accepted a stale source digest.");
            RequireThrows(
                () => RequireLiveReport(
                    "focused-stale-scene", path, source, new string('d', 64),
                    "REQUIRED_LABEL"),
                "Batch A evidence accepted a stale scene digest.");

            File.WriteAllText(
                path,
                "[PASS] REQUIRED_LABEL\nRESULT=PASS; failures=0; <none>\n",
                new UTF8Encoding(false, true));
            RequireThrows(
                () => RequireLiveReport(
                    "focused-pass-only", path, source, scene,
                    "REQUIRED_LABEL"),
                "Batch A evidence accepted a PASS-only report without its schema.");

            Debug.Log("V27_BATCH_A_EVIDENCE_CONTRACT_FOCUSED=PASS; "
                + "staleSourceRejected=true; staleSceneRejected=true; "
                + "passOnlyRejected=true; deterministicDoubleCapture=true");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    public static string RunAll()
    {
        V27CurrentSourceEvidenceSnapshot source =
            V27CurrentSourceEvidenceDigest.Capture();
        string currentSourceDigest = source.Digest;
        string gameplaySceneSha256 =
            V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest();
        Require(string.Equals(
                gameplaySceneSha256,
                OfficialGameplaySceneSha256,
                StringComparison.Ordinal),
            "The official GameplayScene SHA-256 drifted before Batch A capture: "
            + gameplaySceneSha256);

        // Refresh the persisted manifest first. Its CSV is the executable
        // output-owner snapshot; Batch A must not duplicate its owner set.
        V27FacilityBufferOwnerManifestDebugScenarios.RunFromMenu();
        V27FacilityBufferOwnerManifestDebugScenarios.RequireOutputClosure();

        FacilityOutputExactRouteDebugScenarios.RunAll();
        PreparedOutputCustodyMutationGuardDebugScenarios.RunAll();
        PreparedOutputFreshnessCustodyMutationDebugScenarios.RunAll();
        ProductionPreparedOutputComponentCodecDebugScenarios.RunAll();
        FacilityBufferPlannedOutputPublicationDebugScenarios.RunAll();
        PreparedOutputHaulPlannerGateDebugScenarios.RunAll();
        ProductionPreparedOutputFullPersistenceDebugScenarios
            .VerifyAllCurrentFormatRoundTrips();
        ProductionDomainOutputPublicationDebugScenarios.VerifyAll();
        PhysicalItemExactSourcePublicationDebugScenarios.RunAll();
        WorldResourceTransactionFaultDebugScenarios.RunFromMenu();

        // The handler registry, not a hand-maintained owner list, selects all
        // physical output families exercised by the common contracts.
        ProductionAmmunitionPreparedOutputDebugScenarios.RunAll();
        ApparelPhysicalTransactionDebugScenarios.RunAll();
        EnvironmentalWorkwearPlannedOutputDebugScenarios.RunAll();
        SurgicalPartPreparedOutputDebugScenarios.RunAll();

        ArtifactEvidence synthetic = RequireLiveReport(
            "synthetic-canary",
            PhysicalItemLogisticsPlayModeVerifier.PreparedOutputWarehouseReportPath,
            currentSourceDigest,
            gameplaySceneSha256,
            "PREPARED_OUTPUT_CANARY_CAPACITY_WAIT_EXACT_1G_SHORT",
            "PREPARED_OUTPUT_CANARY_PRE_PICKUP_CANCEL_RELEASES_ONLY_LEASE",
            "PREPARED_OUTPUT_CANARY_MID_CARRY_RESTORE_EXACT",
            "PREPARED_OUTPUT_LIVE_STORED_WITH_RETIRED_ADMISSION");
        ArtifactEvidence sawmill = RequireLiveReport(
            "sawmill",
            PhysicalItemLogisticsPlayModeVerifier
                .SawmillPreparedOutputWarehouseReportPath,
            currentSourceDigest,
            gameplaySceneSha256,
            "PREPARED_OUTPUT_TRANSPORT_DOWNED_CURRENT_CELL_EXACT",
            "PREPARED_OUTPUT_TRANSPORT_DOWNED_AUTHORITY_RELEASED",
            "PREPARED_OUTPUT_TRANSPORT_DOWNED_CHECKPOINT_RESTORED_EXACT",
            "PREPARED_OUTPUT_CANARY_MID_CARRY_ADMISSION_TAMPER_ATOMIC_REJECT",
            "PREPARED_OUTPUT_LIVE_STORED_WITH_RETIRED_ADMISSION");
        ArtifactEvidence surgical = RequireLiveReport(
            "m06-surgical",
            PhysicalItemLogisticsPlayModeVerifier.M06PreparedOutputWarehouseReportPath,
            currentSourceDigest,
            gameplaySceneSha256,
            "PREPARED_OUTPUT_LIVE_EXACT_WAREHOUSE_TARGET",
            "PREPARED_OUTPUT_LIVE_STORED_WITH_RETIRED_ADMISSION",
            "PREPARED_OUTPUT_CANARY_SECOND_RESTORE_NO_DUPLICATE");
        ArtifactEvidence worldResource = RequireFocusedReport(
            "world-resource",
            WorldResourceTransactionFaultDebugScenarios.ReportPath,
            currentSourceDigest,
            gameplaySceneSha256,
            "sourceDebitCount=1",
            "saveBlockedDuringRetained=true",
            "retryReusedFrozenTransaction=true",
            "invalidTopologyLiveStateUnchanged=true",
            "rootSeedTamperRejected=true",
            "zeroPhysicalOutputCycleExact=true");

        OwnerSnapshot owners = CaptureOwnerSnapshot(
            currentSourceDigest,
            gameplaySceneSha256);
        DeliveryCallsiteSnapshot callsites = CaptureDeliveryCallsites();
        Require(owners.DeliveryInvocationCount == callsites.Rows.Length,
            "FacilityBuffer manifest and current-source delivery invocation count "
            + $"do not match: manifest={owners.DeliveryInvocationCount}; "
            + $"source={callsites.Rows.Length}.");
        Require(owners.DeliveryInvocationFileCount == callsites.FileCount,
            "FacilityBuffer manifest and current-source delivery file count "
            + $"do not match: manifest={owners.DeliveryInvocationFileCount}; "
            + $"source={callsites.FileCount}.");
        Require(string.Equals(
                owners.DeliveryInvocationSetDigest,
                callsites.SnapshotDigest,
                StringComparison.Ordinal),
            "FacilityBuffer manifest and current-source delivery exact sets differ.");

        V27CurrentSourceEvidenceSnapshot finalSource =
            V27CurrentSourceEvidenceDigest.Capture();
        string finalSceneDigest =
            V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest();
        Require(string.Equals(currentSourceDigest, finalSource.Digest,
                    StringComparison.Ordinal)
                && source.InputCount == finalSource.InputCount
                && string.Equals(
                    source.PathListDigest,
                    finalSource.PathListDigest,
                    StringComparison.Ordinal)
                && string.Equals(gameplaySceneSha256, finalSceneDigest,
                    StringComparison.Ordinal),
            "Source or official scene changed while Batch A evidence was captured.");

        ArtifactEvidence[] artifacts =
        {
            owners.CsvEvidence,
            owners.ReportEvidence,
            synthetic,
            sawmill,
            surgical,
            worldResource
        };
        Require(artifacts.Select(value => value.Id)
                    .Distinct(StringComparer.Ordinal).Count() == artifacts.Length,
            "Batch A aggregate contains duplicate artifact identities.");
        Require(artifacts.All(value => string.Equals(
                    value.CurrentSourceDigest, currentSourceDigest,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.GameplaySceneSha256, gameplaySceneSha256,
                    StringComparison.Ordinal)),
            "Batch A aggregate mixes source or scene revisions.");
        AggregateSnapshot snapshot = new AggregateSnapshot(
            source,
            gameplaySceneSha256,
            owners,
            callsites,
            artifacts);
        VerifyArtifactsUnchanged(artifacts);
        string first = BuildReport(snapshot);
        string second = BuildReport(snapshot);
        Require(string.Equals(first, second, StringComparison.Ordinal),
            "Batch A aggregate changed between identical captures.");
        VerifyArtifactsUnchanged(artifacts);
        return first;
    }

    private static OwnerSnapshot CaptureOwnerSnapshot(
        string currentSourceDigest,
        string gameplaySceneSha256)
    {
        ArtifactEvidence csvEvidence = CaptureGeneratedArtifact(
            "owner-manifest-csv",
            V27FacilityBufferOwnerManifestDebugScenarios.CsvPath,
            ManifestCsvSchema,
            currentSourceDigest,
            gameplaySceneSha256);
        ArtifactEvidence reportEvidence = CaptureGeneratedArtifact(
            "owner-manifest-report",
            V27FacilityBufferOwnerManifestDebugScenarios.ReportPath,
            ManifestReportSchema,
            currentSourceDigest,
            gameplaySceneSha256);

        string report = StrictUtf8(reportEvidence.Bytes);
        IReadOnlyDictionary<string, string> reportFields =
            ParseUniqueKeyValueLines(report);
        RequireField(reportFields, "schemaVersion", "3",
            "FacilityBuffer owner manifest report schema drifted.");
        RequireField(reportFields, "fullStoredDestinationCoverage", "true",
            "FacilityBuffer owner manifest is not full-destination coverage.");
        RequireField(reportFields, "classificationGate", "PASS",
            "FacilityBuffer owner manifest classification did not pass.");
        RequireField(reportFields, "outputRemaining", "0",
            "FacilityBuffer output owner migration is incomplete.");
        RequireField(reportFields, "bypass", "0",
            "FacilityBuffer owner manifest contains a bypass.");
        RequireField(reportFields, "orphan", "0",
            "FacilityBuffer owner manifest contains an orphan.");
        RequireField(reportFields, "unclassified", "0",
            "FacilityBuffer owner manifest contains an unclassified callsite.");

        List<string[]> csv = ParseRfc4180(StrictUtf8(csvEvidence.Bytes));
        Require(csv.Count > 1,
            "FacilityBuffer owner manifest CSV contains no owner rows.");
        string[] header = csv[0];
        string[] requiredColumns =
        {
            "schemaVersion", "state", "ownerDomain", "destinationRule",
            "producerSymbol", "disposition", "sourcePath", "sourceDigest"
        };
        Dictionary<string, int> columns = header
            .Select((name, index) => new { name, index })
            .ToDictionary(value => value.name, value => value.index,
                StringComparer.Ordinal);
        foreach (string required in requiredColumns)
            Require(columns.ContainsKey(required),
                "FacilityBuffer owner CSV is missing required column: " + required);

        string manifestSourceDigest = RequireField(
            reportFields, "sourceDigest");
        Require(IsSha256(manifestSourceDigest),
            "FacilityBuffer owner manifest source digest is not canonical SHA-256.");
        List<OwnerContract> outputOwners = new List<OwnerContract>();
        foreach (string[] row in csv.Skip(1))
        {
            Require(row.Length == header.Length,
                "FacilityBuffer owner CSV row width drifted.");
            Require(string.Equals(row[columns["schemaVersion"]], "1",
                    StringComparison.Ordinal),
                "FacilityBuffer owner CSV row schema drifted.");
            Require(string.Equals(row[columns["sourceDigest"]],
                    manifestSourceDigest, StringComparison.Ordinal),
                "FacilityBuffer owner CSV mixes source revisions.");
            string state = row[columns["state"]];
            if (!IsOutputState(state)) continue;
            outputOwners.Add(new OwnerContract(
                state,
                row[columns["ownerDomain"]],
                row[columns["destinationRule"]],
                row[columns["producerSymbol"]],
                row[columns["disposition"]],
                row[columns["sourcePath"]]));
        }

        OwnerContract[] sorted = outputOwners
            .OrderBy(value => value.OwnerDomain, StringComparer.Ordinal)
            .ThenBy(value => value.State, StringComparer.Ordinal)
            .ThenBy(value => value.DestinationRule, StringComparer.Ordinal)
            .ToArray();
        Require(sorted.Length > 0,
            "FacilityBuffer owner snapshot contains no output owners.");
        Require(sorted.Select(value => value.OwnerDomain)
                    .Distinct(StringComparer.Ordinal).Count() == sorted.Length,
            "FacilityBuffer output owner domains are not unique.");
        Require(sorted.All(value => string.Equals(
                value.Disposition, "migrated", StringComparison.Ordinal)),
            "FacilityBuffer output snapshot contains an unmigrated owner.");
        Require(sorted.All(value => value.IsComplete),
            "FacilityBuffer output snapshot contains an incomplete owner contract.");
        Require(ParsePositiveInt(reportFields, "outputOwners") == sorted.Length
                && ParsePositiveInt(reportFields, "outputMigrated")
                == sorted.Length,
            "FacilityBuffer report output denominator does not bijectively match "
            + "the current owner snapshot.");

        string ownerSetDigest = HashCanonicalLines(sorted.Select(value =>
            value.CanonicalLine));
        string deliveryInvocationSetDigest = RequireField(
            reportFields, "deliveryInvocationSetDigest");
        Require(IsSha256(deliveryInvocationSetDigest),
            "FacilityBuffer delivery exact-set digest is not canonical SHA-256.");
        return new OwnerSnapshot(
            sorted,
            manifestSourceDigest,
            ownerSetDigest,
            ParsePositiveInt(reportFields, "deliveryInvocations"),
            ParsePositiveInt(reportFields, "deliveryInvocationFiles"),
            deliveryInvocationSetDigest,
            csvEvidence,
            reportEvidence);
    }

    private static DeliveryCallsiteSnapshot CaptureDeliveryCallsites()
    {
        string root = ProjectRoot();
        string scripts = Path.Combine(root, "Assets", "Scripts");
        List<string> rows = new List<string>();
        foreach (string absolute in Directory.GetFiles(
                     scripts, "*.cs", SearchOption.AllDirectories))
        {
            string path = CanonicalPath(Path.GetRelativePath(root, absolute));
            if (path.IndexOf("/Editor/", StringComparison.Ordinal) >= 0)
                continue;
            string source = File.ReadAllText(absolute);
            foreach (Match match in DeliveryInvocationPattern.Matches(source))
            {
                int line = 1;
                for (int index = 0; index < match.Index; index++)
                    if (source[index] == '\n') line++;
                rows.Add(path + "|" + line + "|" + match.Groups[1].Value);
            }
        }

        string[] sorted = rows.OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(sorted.Length > 0,
            "Current source contains no FacilityBuffer delivery invocation.");
        Require(sorted.Distinct(StringComparer.Ordinal).Count() == sorted.Length,
            "Current-source delivery invocation snapshot contains duplicates.");
        int fileCount = sorted.Select(value => value.Substring(
                0, value.IndexOf('|')))
            .Distinct(StringComparer.Ordinal)
            .Count();
        return new DeliveryCallsiteSnapshot(
            sorted,
            fileCount,
            HashCanonicalLines(sorted));
    }

    private static ArtifactEvidence RequireLiveReport(
        string id,
        string path,
        string expectedSourceDigest,
        string expectedSceneDigest,
        params string[] requiredLabels)
    {
        ArtifactEvidence evidence = CaptureGeneratedArtifact(
            id, path, LiveReportSchema, expectedSourceDigest,
            expectedSceneDigest);
        string report = StrictUtf8(evidence.Bytes);
        Require(CountExactLinePrefix(report, "RESULT=PASS; failures=0") == 1,
            "Required live report did not pass exactly once: " + path);
        Require(CountExactLinePrefix(report, "capturedErrors=0;") == 1
                && CountExactLinePrefix(report, "capturedWarnings=0;") == 1,
            "Required live report is missing the zero-error/zero-warning schema: "
            + path);
        RequireSingleEvidenceValue(
            report, "currentSourceDigest", expectedSourceDigest, path);
        RequireSingleEvidenceValue(
            report, "gameplaySceneSha256", expectedSceneDigest, path);
        foreach (string label in requiredLabels ?? Array.Empty<string>())
        {
            Require(CountExactLinePrefix(report, "[PASS] " + label) == 1,
                $"Required live report '{path}' is missing unique PASS label "
                + $"'{label}'.");
        }
        return evidence;
    }

    private static ArtifactEvidence RequireFocusedReport(
        string id,
        string path,
        string currentSourceDigest,
        string gameplaySceneSha256,
        params string[] requiredMarkers)
    {
        // This report is generated synchronously earlier in this RunAll call;
        // the aggregate envelope binds the current source/scene and raw bytes.
        ArtifactEvidence evidence = CaptureGeneratedArtifact(
            id, path, FocusedReportSchema, currentSourceDigest,
            gameplaySceneSha256);
        string report = StrictUtf8(evidence.Bytes);
        IReadOnlyDictionary<string, string> fields =
            ParseUniqueKeyValueLines(report);
        RequireField(fields, "schemaVersion", "1",
            "World-resource focused report schema drifted.");
        RequireField(fields, "RESULT", "PASS",
            "World-resource focused report did not pass.");
        foreach (string marker in requiredMarkers ?? Array.Empty<string>())
        {
            Require(report.Split('\n').Select(value => value.TrimEnd('\r'))
                    .Count(value => string.Equals(
                        value, marker, StringComparison.Ordinal)) == 1,
                $"Required focused report '{path}' is missing unique marker "
                + $"'{marker}'.");
        }
        return evidence;
    }

    private static ArtifactEvidence CaptureGeneratedArtifact(
        string id,
        string path,
        string schema,
        string currentSourceDigest,
        string gameplaySceneSha256)
    {
        Require(!string.IsNullOrWhiteSpace(id)
                && !string.IsNullOrWhiteSpace(schema),
            "Batch A artifact identity/schema is missing.");
        Require(IsSha256(currentSourceDigest) && IsSha256(gameplaySceneSha256),
            "Batch A artifact source/scene digest is not canonical SHA-256.");
        string absolute = ResolvePath(path);
        Require(File.Exists(absolute), "Required Batch A artifact is missing: " + path);
        byte[] bytes = File.ReadAllBytes(absolute);
        Require(bytes.Length > 0, "Required Batch A artifact is empty: " + path);
        StrictUtf8(bytes);
        return new ArtifactEvidence(
            id,
            CanonicalPath(Path.GetRelativePath(ProjectRoot(), absolute)),
            schema,
            currentSourceDigest,
            gameplaySceneSha256,
            bytes,
            Sha256(bytes));
    }

    private static void VerifyArtifactsUnchanged(
        IEnumerable<ArtifactEvidence> artifacts)
    {
        foreach (ArtifactEvidence artifact in artifacts)
        {
            string absolute = ResolvePath(artifact.Path);
            Require(File.Exists(absolute),
                "Batch A artifact disappeared during capture: " + artifact.Path);
            byte[] current = File.ReadAllBytes(absolute);
            Require(current.Length == artifact.ByteLength
                    && string.Equals(
                        Sha256(current), artifact.ByteSha256,
                        StringComparison.Ordinal),
                "Batch A artifact changed during aggregate capture: "
                + artifact.Path);
        }
    }

    private static string BuildReport(AggregateSnapshot snapshot)
    {
        List<string> evidenceInputs = new List<string>
        {
            "currentSourceDigest=" + snapshot.CurrentSourceDigest,
            "gameplaySceneSha256=" + snapshot.GameplaySceneSha256,
            "manifestSourceDigest=" + snapshot.Owners.ManifestSourceDigest,
            "ownerSetDigest=" + snapshot.Owners.OwnerSetDigest,
            "manifestDeliveryCallsiteDigest="
                + snapshot.Owners.DeliveryInvocationSetDigest,
            "deliveryCallsiteDigest=" + snapshot.Callsites.SnapshotDigest
        };
        evidenceInputs.AddRange(snapshot.Artifacts
            .OrderBy(value => value.Id, StringComparer.Ordinal)
            .Select(value => value.CanonicalLine));
        string aggregateSourceDigest = HashCanonicalLines(evidenceInputs);

        StringBuilder report = new StringBuilder(8192);
        report.Append("schemaVersion=2\n")
            .Append("batch=A\n")
            .Append("currentSourceDigest=")
            .Append(snapshot.CurrentSourceDigest).Append('\n')
            .Append("currentSourceInputCount=")
            .Append(snapshot.CurrentSourceInputCount).Append('\n')
            .Append("currentSourcePathListDigest=")
            .Append(snapshot.CurrentSourcePathListDigest).Append('\n')
            .Append("gameplaySceneSha256=")
            .Append(snapshot.GameplaySceneSha256).Append('\n')
            .Append("sourceDigest=")
            .Append(aggregateSourceDigest).Append('\n')
            .Append("manifestSourceDigest=")
            .Append(snapshot.Owners.ManifestSourceDigest).Append('\n')
            .Append("ownerSetDigest=")
            .Append(snapshot.Owners.OwnerSetDigest).Append('\n')
            .Append("outputOwners=")
            .Append(snapshot.Owners.Rows.Length).Append('\n')
            .Append("outputMigrated=")
            .Append(snapshot.Owners.Rows.Length).Append('\n')
            .Append("outputRemaining=0\n")
            .Append("bypass=0\n")
            .Append("orphan=0\n")
            .Append("unclassified=0\n")
            .Append("deliveryInvocations=")
            .Append(snapshot.Callsites.Rows.Length).Append('\n')
            .Append("deliveryInvocationFiles=")
            .Append(snapshot.Callsites.FileCount).Append('\n')
            .Append("manifestDeliveryCallsiteDigest=")
            .Append(snapshot.Owners.DeliveryInvocationSetDigest).Append('\n')
            .Append("deliveryCallsiteDigest=")
            .Append(snapshot.Callsites.SnapshotDigest).Append('\n');

        for (int index = 0; index < snapshot.Owners.Rows.Length; index++)
        {
            OwnerContract owner = snapshot.Owners.Rows[index];
            report.Append("owner[").Append(index).Append("]=")
                .Append(owner.CanonicalLine).Append('\n');
        }
        foreach (ArtifactEvidence artifact in snapshot.Artifacts
                     .OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            report.Append("artifact:").Append(artifact.Id)
                .Append(".schema=").Append(artifact.Schema).Append('\n')
                .Append("artifact:").Append(artifact.Id)
                .Append(".path=").Append(artifact.Path).Append('\n')
                .Append("artifact:").Append(artifact.Id)
                .Append(".currentSourceDigest=")
                .Append(artifact.CurrentSourceDigest).Append('\n')
                .Append("artifact:").Append(artifact.Id)
                .Append(".gameplaySceneSha256=")
                .Append(artifact.GameplaySceneSha256).Append('\n')
                .Append("artifact:").Append(artifact.Id)
                .Append(".byteSha256=").Append(artifact.ByteSha256).Append('\n')
                .Append("artifact:").Append(artifact.Id)
                .Append(".byteLength=").Append(artifact.ByteLength).Append('\n');
        }

        report.Append("partialRoute=PASS\n")
            .Append("perishableComponentCodec=PASS\n")
            .Append("perishableExactRoute=PASS\n")
            .Append("freshnessCustodyMutation=PASS\n")
            .Append("perishableFullPersistence=PASS\n")
            .Append("cancel=PASS\n")
            .Append("downedCurrentCell=PASS\n")
            .Append("midHaulRestore=PASS\n")
            .Append("outputSpaceRetry=PASS\n")
            .Append("syntheticLive=PASS\n")
            .Append("sawmillLive=PASS\n")
            .Append("surgicalLive=PASS\n")
            .Append("worldResourceFaultMatrix=PASS\n")
            .Append("deterministicDoubleCapture=PASS\n")
            .Append("secondRunByteDiff=0\n")
            .Append("secondRunLengthDiff=0\n")
            .Append("secondRunMtimeDiff=0\n")
            .Append("result=PASS\n");
        return report.ToString();
    }

    private static IReadOnlyDictionary<string, string> ParseUniqueKeyValueLines(
        string report)
    {
        Dictionary<string, string> fields = new Dictionary<string, string>(
            StringComparer.Ordinal);
        foreach (string raw in report.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            int separator = line.IndexOf('=');
            if (separator <= 0) continue;
            string key = line.Substring(0, separator);
            string value = line.Substring(separator + 1);
            Require(fields.TryAdd(key, value),
                "Evidence report contains duplicate key: " + key);
        }
        return fields;
    }

    private static List<string[]> ParseRfc4180(string text)
    {
        List<string[]> rows = new List<string[]>();
        List<string> row = new List<string>();
        StringBuilder field = new StringBuilder();
        bool quoted = false;
        for (int index = 0; index < text.Length; index++)
        {
            char value = text[index];
            if (quoted)
            {
                if (value != '"')
                {
                    field.Append(value);
                    continue;
                }
                if (index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                    continue;
                }
                quoted = false;
                continue;
            }

            if (value == '"')
            {
                Require(field.Length == 0,
                    "RFC 4180 quote appeared inside an unquoted field.");
                quoted = true;
            }
            else if (value == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (value == '\r' || value == '\n')
            {
                if (value == '\r')
                    Require(index + 1 < text.Length && text[index + 1] == '\n',
                        "RFC 4180 CSV contains a bare CR.");
                if (value == '\r') index++;
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row.ToArray());
                row.Clear();
            }
            else
            {
                field.Append(value);
            }
        }
        Require(!quoted, "RFC 4180 CSV contains an unterminated quote.");
        Require(field.Length == 0 && row.Count == 0,
            "RFC 4180 CSV must terminate every record with CRLF.");
        return rows;
    }

    private static void RequireSingleEvidenceValue(
        string report,
        string key,
        string expected,
        string path)
    {
        string prefix = key + "=";
        string[] values = report.Split('\n')
            .Select(value => value.TrimEnd('\r'))
            .Where(value => value.StartsWith(prefix, StringComparison.Ordinal))
            .Select(value => value.Substring(prefix.Length))
            .ToArray();
        Require(values.Length == 1
                && string.Equals(values[0], expected, StringComparison.Ordinal),
            $"Required report '{path}' has stale or ambiguous {key}: "
            + $"expected={expected}; actual={string.Join(",", values)}.");
    }

    private static int CountExactLinePrefix(string report, string prefix) =>
        report.Split('\n')
            .Select(value => value.TrimEnd('\r'))
            .Count(value => value.StartsWith(prefix, StringComparison.Ordinal));

    private static string RequireField(
        IReadOnlyDictionary<string, string> fields,
        string key)
    {
        Require(fields.TryGetValue(key, out string value)
                && !string.IsNullOrWhiteSpace(value),
            "Evidence report is missing required key: " + key);
        return value;
    }

    private static void RequireField(
        IReadOnlyDictionary<string, string> fields,
        string key,
        string expected,
        string message)
    {
        Require(fields.TryGetValue(key, out string actual)
                && string.Equals(actual, expected, StringComparison.Ordinal),
            message + $" expected={expected}; actual={actual ?? "<missing>"}.");
    }

    private static int ParsePositiveInt(
        IReadOnlyDictionary<string, string> fields,
        string key)
    {
        string value = RequireField(fields, key);
        Require(int.TryParse(
                    value,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out int result)
                && result > 0,
            "Evidence report key is not a positive integer: " + key + "=" + value);
        return result;
    }

    private static bool IsOutputState(string state) => state is
        "FacilityOutputBuffer" or "DirectLooseOutput";

    private static string HashCanonicalLines(IEnumerable<string> lines)
    {
        using SHA256 sha = SHA256.Create();
        foreach (string line in lines.OrderBy(value => value, StringComparer.Ordinal))
        {
            byte[] bytes = Utf8((line ?? string.Empty) + "\n");
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Hex(sha.Hash);
    }

    private static string CanonicalTuple(params string[] fields)
    {
        StringBuilder value = new StringBuilder();
        foreach (string field in fields ?? Array.Empty<string>())
        {
            string normalized = field ?? string.Empty;
            value.Append(normalized.Length).Append(':').Append(normalized);
        }
        return value.ToString();
    }

    private static string Sha256(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        return Hex(sha.ComputeHash(bytes));
    }

    private static string Hex(byte[] bytes)
    {
        const string alphabet = "0123456789abcdef";
        char[] result = new char[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            result[index * 2] = alphabet[bytes[index] >> 4];
            result[index * 2 + 1] = alphabet[bytes[index] & 0x0f];
        }
        return new string(result);
    }

    private static bool IsSha256(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

    private static byte[] Utf8(string value) =>
        new UTF8Encoding(false, true).GetBytes(value ?? string.Empty);

    private static string StrictUtf8(byte[] bytes) =>
        new UTF8Encoding(false, true).GetString(bytes);

    private static string ProjectRoot() =>
        Directory.GetParent(Application.dataPath)?.FullName
        ?? throw new InvalidOperationException("Project root is unavailable.");

    private static string ResolvePath(string path) => Path.IsPathRooted(path)
        ? Path.GetFullPath(path)
        : Path.GetFullPath(Path.Combine(
            ProjectRoot(),
            (path ?? string.Empty).Replace('/', Path.DirectorySeparatorChar)));

    private static string CanonicalPath(string value) =>
        (value ?? string.Empty).Replace('\\', '/');

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ArtifactEvidence
    {
        internal ArtifactEvidence(
            string id,
            string path,
            string schema,
            string currentSourceDigest,
            string gameplaySceneSha256,
            byte[] bytes,
            string byteSha256)
        {
            Id = id;
            Path = path;
            Schema = schema;
            CurrentSourceDigest = currentSourceDigest;
            GameplaySceneSha256 = gameplaySceneSha256;
            Bytes = bytes;
            ByteSha256 = byteSha256;
        }

        internal string Id { get; }
        internal string Path { get; }
        internal string Schema { get; }
        internal string CurrentSourceDigest { get; }
        internal string GameplaySceneSha256 { get; }
        internal byte[] Bytes { get; }
        internal string ByteSha256 { get; }
        internal int ByteLength => Bytes.Length;
        internal string CanonicalLine => CanonicalTuple(
            Id,
            Path,
            Schema,
            CurrentSourceDigest,
            GameplaySceneSha256,
            ByteSha256,
            ByteLength.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
    }

    private sealed class OwnerContract
    {
        internal OwnerContract(
            string state,
            string ownerDomain,
            string destinationRule,
            string producerSymbol,
            string disposition,
            string sourcePath)
        {
            State = state;
            OwnerDomain = ownerDomain;
            DestinationRule = destinationRule;
            ProducerSymbol = producerSymbol;
            Disposition = disposition;
            SourcePath = sourcePath;
        }

        internal string State { get; }
        internal string OwnerDomain { get; }
        internal string DestinationRule { get; }
        internal string ProducerSymbol { get; }
        internal string Disposition { get; }
        internal string SourcePath { get; }
        internal bool IsComplete => !string.IsNullOrWhiteSpace(State)
            && !string.IsNullOrWhiteSpace(OwnerDomain)
            && !string.IsNullOrWhiteSpace(DestinationRule)
            && !string.IsNullOrWhiteSpace(ProducerSymbol)
            && !string.IsNullOrWhiteSpace(Disposition)
            && !string.IsNullOrWhiteSpace(SourcePath);
        internal string CanonicalLine => CanonicalTuple(
            OwnerDomain,
            State,
            DestinationRule,
            ProducerSymbol,
            Disposition,
            SourcePath);
    }

    private sealed class OwnerSnapshot
    {
        internal OwnerSnapshot(
            OwnerContract[] rows,
            string manifestSourceDigest,
            string ownerSetDigest,
            int deliveryInvocationCount,
            int deliveryInvocationFileCount,
            string deliveryInvocationSetDigest,
            ArtifactEvidence csvEvidence,
            ArtifactEvidence reportEvidence)
        {
            Rows = rows;
            ManifestSourceDigest = manifestSourceDigest;
            OwnerSetDigest = ownerSetDigest;
            DeliveryInvocationCount = deliveryInvocationCount;
            DeliveryInvocationFileCount = deliveryInvocationFileCount;
            DeliveryInvocationSetDigest = deliveryInvocationSetDigest;
            CsvEvidence = csvEvidence;
            ReportEvidence = reportEvidence;
        }

        internal OwnerContract[] Rows { get; }
        internal string ManifestSourceDigest { get; }
        internal string OwnerSetDigest { get; }
        internal int DeliveryInvocationCount { get; }
        internal int DeliveryInvocationFileCount { get; }
        internal string DeliveryInvocationSetDigest { get; }
        internal ArtifactEvidence CsvEvidence { get; }
        internal ArtifactEvidence ReportEvidence { get; }
    }

    private sealed class DeliveryCallsiteSnapshot
    {
        internal DeliveryCallsiteSnapshot(
            string[] rows,
            int fileCount,
            string snapshotDigest)
        {
            Rows = rows;
            FileCount = fileCount;
            SnapshotDigest = snapshotDigest;
        }

        internal string[] Rows { get; }
        internal int FileCount { get; }
        internal string SnapshotDigest { get; }
    }

    private sealed class AggregateSnapshot
    {
        internal AggregateSnapshot(
            V27CurrentSourceEvidenceSnapshot source,
            string gameplaySceneSha256,
            OwnerSnapshot owners,
            DeliveryCallsiteSnapshot callsites,
            ArtifactEvidence[] artifacts)
        {
            CurrentSourceDigest = source.Digest;
            CurrentSourceInputCount = source.InputCount;
            CurrentSourcePathListDigest = source.PathListDigest;
            GameplaySceneSha256 = gameplaySceneSha256;
            Owners = owners;
            Callsites = callsites;
            Artifacts = artifacts;
        }

        internal string CurrentSourceDigest { get; }
        internal int CurrentSourceInputCount { get; }
        internal string CurrentSourcePathListDigest { get; }
        internal string GameplaySceneSha256 { get; }
        internal OwnerSnapshot Owners { get; }
        internal DeliveryCallsiteSnapshot Callsites { get; }
        internal ArtifactEvidence[] Artifacts { get; }
    }
}
#endif
