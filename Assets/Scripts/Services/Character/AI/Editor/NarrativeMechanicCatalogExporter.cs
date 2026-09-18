#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class NarrativeMechanicCatalogExporter
{
    private const string ExportParentRelativePath = "Artifacts/Exports/NarrativeMechanicCatalog";

    [MenuItem("DungeonStory/Narrative/Export Mechanic Catalog")]
    private static void ExportCurrentProjectFromMenu()
    {
        try
        {
            NarrativeMechanicCatalogExportResult result = Export(
                new NarrativeMechanicCatalogUnityAssetSource(),
                new NarrativeMechanicCatalogExportRequest(CreateUtcVersion()));
            UnityEngine.Debug.Log(
                $"Narrative mechanic catalog exported: {result.DirectoryPath} ({result.CatalogHash}); trainingEligible={result.TrainingEligible}.");
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogError($"Narrative mechanic catalog export failed: {exception.Message}");
            throw;
        }
    }

    public static void ExportCurrentProjectFromBatch()
    {
        ExportCurrentProjectFromMenu();
    }

    public static NarrativeMechanicCatalogExportResult Export(
        INarrativeMechanicCatalogSnapshotProvider provider,
        NarrativeMechanicCatalogExportRequest request)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        string projectRoot = GetProjectRoot();
        CapturedExport captured = Capture(provider, request, projectRoot);
        string outputDirectory = WriteNewExport(projectRoot, request.UtcVersion, captured);
        return new NarrativeMechanicCatalogExportResult(
            outputDirectory,
            captured.CatalogHash,
            captured.InputDigest,
            captured.UncommittedSourceHash,
            captured.TrainingEligible);
    }

    /// <summary>
    /// Captures and serializes the complete delivered package twice. Each pass
    /// calls CaptureSnapshot again, so a caller cannot prove determinism by
    /// reusing one already-built DOM.
    /// </summary>
    public static NarrativeMechanicCatalogByteComparison CompareIndependentExports(
        INarrativeMechanicCatalogSnapshotProvider provider)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        return CompareIndependentExports(
            () => provider,
            new NarrativeMechanicCatalogExportRequest("independent-comparison"));
    }

    public static NarrativeMechanicCatalogByteComparison CompareIndependentExports(
        INarrativeMechanicCatalogSnapshotProvider provider,
        NarrativeMechanicCatalogExportRequest request)
    {
        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        return CompareIndependentExports(() => provider, request);
    }

    /// <summary>
    /// Stronger comparison hook for CI: the factory is invoked for each capture,
    /// avoiding any cached snapshot held by a provider instance.
    /// </summary>
    public static NarrativeMechanicCatalogByteComparison CompareIndependentExports(
        Func<INarrativeMechanicCatalogSnapshotProvider> providerFactory)
    {
        return CompareIndependentExports(
            providerFactory,
            new NarrativeMechanicCatalogExportRequest("independent-comparison"));
    }

    public static NarrativeMechanicCatalogByteComparison CompareIndependentExports(
        Func<INarrativeMechanicCatalogSnapshotProvider> providerFactory,
        NarrativeMechanicCatalogExportRequest request)
    {
        if (providerFactory == null)
        {
            throw new ArgumentNullException(nameof(providerFactory));
        }
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        string projectRoot = GetProjectRoot();
        INarrativeMechanicCatalogSnapshotProvider firstProvider = providerFactory()
            ?? throw new NarrativeMechanicCatalogExportException(
                "independent-export-provider-null",
                "The first independent export provider was null.");
        INarrativeMechanicCatalogSnapshotProvider secondProvider = providerFactory()
            ?? throw new NarrativeMechanicCatalogExportException(
                "independent-export-provider-null",
                "The second independent export provider was null.");
        CapturedExport first = Capture(firstProvider, request, projectRoot);
        CapturedExport second = Capture(secondProvider, request, projectRoot);
        return new NarrativeMechanicCatalogByteComparison(
            BuildPackageComparisonBytes(first),
            BuildPackageComparisonBytes(second),
            first.InputDigest,
            second.InputDigest);
    }

    private static CapturedExport Capture(
        INarrativeMechanicCatalogSnapshotProvider provider,
        NarrativeMechanicCatalogExportRequest request,
        string projectRoot)
    {
        NarrativeMechanicCatalogSnapshot snapshot = provider.CaptureSnapshot()
            ?? throw new NarrativeMechanicCatalogExportException(
                "snapshot-provider-returned-null",
                "Narrative mechanic catalog snapshot provider returned null.");
        if (snapshot.Scenarios == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "narrative-scenario-bundle-missing",
                "Narrative mechanic catalog export requires the authoritative six-profile scenario bundle.");
        }
        string gameCommit = GetRequiredGitCommit(projectRoot);
        SourceProvenance provenance = SourceProvenance.Capture(
            projectRoot,
            snapshot.RelevantPaths,
            gameCommit);
        TrainingDecision training = TrainingDecision.Evaluate(
            snapshot,
            request,
            projectRoot,
            provenance.InputDigest);

        NarrativeMechanicCatalogCanonicalJsonObject catalogWithoutHash =
            snapshot.BuildCatalogWithoutHash(gameCommit, training.IsEligible);
        byte[] canonicalWithoutHash = catalogWithoutHash.ToCanonicalUtf8();
        string catalogHash = NarrativeMechanicCatalogCanonicalJson.Sha256Prefixed(canonicalWithoutHash);
        NarrativeMechanicCatalogCanonicalJsonObject catalog = catalogWithoutHash.With(
            "catalogHash",
            NarrativeMechanicCatalogCanonicalJson.String(catalogHash));
        byte[] catalogBytes = catalog.ToCanonicalUtf8();
        byte[] rawTestResultsBytes = request.RawTestResults?.ToCanonicalUtf8();

        return new CapturedExport(
            snapshot,
            provenance,
            training,
            gameCommit,
            catalogHash,
            catalogBytes,
            rawTestResultsBytes);
    }

    private static string WriteNewExport(
        string projectRoot,
        string version,
        CapturedExport captured)
    {
        ValidateVersion(version);
        IReadOnlyList<DeliveredFile> deliveredFiles = BuildDeliveredFiles(captured);
        string parent = Path.Combine(projectRoot, ExportParentRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(parent);
        string finalDirectory = Path.Combine(parent, version);
        if (Directory.Exists(finalDirectory) || File.Exists(finalDirectory))
        {
            throw new NarrativeMechanicCatalogExportException(
                "export-version-collision",
                $"Narrative mechanic catalog export '{finalDirectory}' already exists. Existing exports are never overwritten.");
        }

        string stagingDirectory = Path.Combine(
            parent,
            $".{version}.staging-{Guid.NewGuid():N}");
        if (Directory.Exists(stagingDirectory) || File.Exists(stagingDirectory))
        {
            throw new NarrativeMechanicCatalogExportException(
                "export-staging-collision",
                $"Narrative mechanic catalog staging directory '{stagingDirectory}' already exists.");
        }

        Directory.CreateDirectory(stagingDirectory);
        string claimPath = Path.Combine(stagingDirectory, ".create-new-claim");
        try
        {
            WriteCreateNew(claimPath, Array.Empty<byte>());
            foreach (DeliveredFile file in deliveredFiles)
            {
                WriteCreateNew(Path.Combine(stagingDirectory, file.Name), file.Bytes);
            }
            File.Delete(claimPath);
            Directory.Move(stagingDirectory, finalDirectory);
            return finalDirectory;
        }
        catch
        {
            // Preserve the unique staging directory for audit.  In particular, do
            // not replace a failed export with a partial success on a retry.
            throw;
        }
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildManifest(CapturedExport captured)
    {
        List<NarrativeMechanicCatalogCanonicalJsonValue> files = captured.Provenance.Files
            .Select(file => (NarrativeMechanicCatalogCanonicalJsonValue)
                NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "gitStatus", NarrativeMechanicCatalogCanonicalJson.String(file.GitStatus)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "path", NarrativeMechanicCatalogCanonicalJson.String(file.Path)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "sha256", NarrativeMechanicCatalogCanonicalJson.String(file.Hash)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "tracked", NarrativeMechanicCatalogCanonicalJson.Boolean(file.Tracked))))
            .ToList();
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "catalogBytesSha256", NarrativeMechanicCatalogCanonicalJson.String(
                    NarrativeMechanicCatalogCanonicalJson.Sha256Prefixed(captured.CatalogBytes))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "catalogHash", NarrativeMechanicCatalogCanonicalJson.String(captured.CatalogHash)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "catalogId", NarrativeMechanicCatalogCanonicalJson.String(captured.Snapshot.CatalogId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "files", new NarrativeMechanicCatalogCanonicalJsonArray(files)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "inputDigest", NarrativeMechanicCatalogCanonicalJson.String(captured.InputDigest)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "schemaVersion", NarrativeMechanicCatalogCanonicalJson.Integer(2)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "source", NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "authority", NarrativeMechanicCatalogCanonicalJson.String("unity-editor-export")),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "gameCommit", NarrativeMechanicCatalogCanonicalJson.String(captured.GameCommit)))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "trainingEligible", NarrativeMechanicCatalogCanonicalJson.Boolean(captured.TrainingEligible)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "uncommittedSourceHash", NarrativeMechanicCatalogCanonicalJson.String(
                    captured.UncommittedSourceHash)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildPolicy(CapturedExport captured)
    {
        List<NarrativeMechanicCatalogCanonicalJsonValue> blockers = captured.Training.Blockers
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(value => (NarrativeMechanicCatalogCanonicalJsonValue)
                NarrativeMechanicCatalogCanonicalJson.String(value))
            .ToList();
        List<NarrativeMechanicCatalogCanonicalJsonValue> gates = captured.Snapshot.TrainingPolicy
            .RequiredEvidenceGateIds
            .Select(value => (NarrativeMechanicCatalogCanonicalJsonValue)
                NarrativeMechanicCatalogCanonicalJson.String(value))
            .ToList();
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "blockers", new NarrativeMechanicCatalogCanonicalJsonArray(blockers)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "catalogHash", NarrativeMechanicCatalogCanonicalJson.String(captured.CatalogHash)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "inputDigest", NarrativeMechanicCatalogCanonicalJson.String(captured.InputDigest)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "requestedTrainingEligibility", NarrativeMechanicCatalogCanonicalJson.Boolean(
                    captured.Training.Requested)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "requiredEvidenceGateIds", new NarrativeMechanicCatalogCanonicalJsonArray(gates)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "schemaVersion", NarrativeMechanicCatalogCanonicalJson.Integer(1)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "trainingEligible", NarrativeMechanicCatalogCanonicalJson.Boolean(captured.TrainingEligible)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildPacketParity(CapturedExport captured)
    {
        List<NarrativeMechanicCatalogCanonicalJsonValue> cases = captured.Snapshot.PacketParity.Cases
            .Select(value => (NarrativeMechanicCatalogCanonicalJsonValue)value)
            .ToList();
        List<NarrativeMechanicCatalogCanonicalJsonValue> blockers = captured.Snapshot.PacketParity.Blockers
            .Select(value => (NarrativeMechanicCatalogCanonicalJsonValue)value.ToCanonicalJson())
            .ToList();
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "blockers", new NarrativeMechanicCatalogCanonicalJsonArray(blockers)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "cases", new NarrativeMechanicCatalogCanonicalJsonArray(cases)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "catalogHash", NarrativeMechanicCatalogCanonicalJson.String(captured.CatalogHash)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "inputDigest", NarrativeMechanicCatalogCanonicalJson.String(captured.InputDigest)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "schemaVersion", NarrativeMechanicCatalogCanonicalJson.Integer(1)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "status", NarrativeMechanicCatalogCanonicalJson.String(
                    blockers.Count == 0 ? "ready-for-review" : "blocked")));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildTestEvidence(CapturedExport captured)
    {
        NarrativeMechanicCatalogTestEvidence evidence = captured.Training.Evidence;
        List<NarrativeMechanicCatalogCanonicalJsonValue> gates = evidence == null
            ? new List<NarrativeMechanicCatalogCanonicalJsonValue>()
            : evidence.Gates.Select(value =>
                (NarrativeMechanicCatalogCanonicalJsonValue)value.ToCanonicalJson()).ToList();
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "capturedInputDigest", NarrativeMechanicCatalogCanonicalJson.String(
                    evidence?.CapturedInputDigest ?? string.Empty)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "catalogHash", NarrativeMechanicCatalogCanonicalJson.String(captured.CatalogHash)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "evidenceGates", new NarrativeMechanicCatalogCanonicalJsonArray(gates)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "exportInputDigest", NarrativeMechanicCatalogCanonicalJson.String(captured.InputDigest)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "schemaVersion", NarrativeMechanicCatalogCanonicalJson.Integer(1)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "status", NarrativeMechanicCatalogCanonicalJson.String(
                    captured.Training.Evidence == null ? "not-supplied" : "supplied")));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildNoveltyReview(
        CapturedExport captured)
    {
        NarrativeMechanicCatalogCanonicalJsonObject review = captured.Snapshot.NoveltyReview;
        if (review == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "novelty-review-missing",
                "Cannot serialize novelty_review.json without snapshot-owned review data.");
        }
        return review.With(
                "catalogHash",
                NarrativeMechanicCatalogCanonicalJson.String(captured.CatalogHash))
            .With(
                "inputDigest",
                NarrativeMechanicCatalogCanonicalJson.String(captured.InputDigest));
    }

    private static IReadOnlyList<DeliveredFile> BuildDeliveredFiles(CapturedExport captured)
    {
        if (captured?.Snapshot?.Scenarios == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "narrative-scenario-bundle-missing",
                "Cannot serialize a package without an authoritative scenario bundle.");
        }

        List<DeliveredFile> files = new List<DeliveredFile>
        {
            new DeliveredFile("catalog.json", captured.CatalogBytes),
            new DeliveredFile("continuity_scenarios.json", BuildContinuityScenarios(captured).ToCanonicalUtf8()),
            new DeliveredFile("manifest.json", BuildManifest(captured).ToCanonicalUtf8()),
            new DeliveredFile("negative_scenarios.json", BuildNegativeScenarios(captured).ToCanonicalUtf8()),
            new DeliveredFile("packet_parity_cases.json", BuildPacketParity(captured).ToCanonicalUtf8()),
            new DeliveredFile("policy.json", BuildPolicy(captured).ToCanonicalUtf8()),
            new DeliveredFile("README.md", BuildReadmeBytes()),
            new DeliveredFile("scenario_schema.json", BuildScenarioSchema().ToCanonicalUtf8()),
            new DeliveredFile("scenarios_100.json", BuildPositiveScenarios(captured).ToCanonicalUtf8()),
            new DeliveredFile("test_evidence.json", BuildTestEvidence(captured).ToCanonicalUtf8())
        };
        if (captured.RawTestResultsBytes != null)
        {
            files.Add(new DeliveredFile("raw_test_results.json", captured.RawTestResultsBytes));
        }
        if (captured.Snapshot.NoveltyReview != null)
        {
            files.Add(new DeliveredFile(
                "novelty_review.json",
                BuildNoveltyReview(captured).ToCanonicalUtf8()));
        }
        files = files.OrderBy((file) => file.Name, StringComparer.Ordinal).ToList();
        files.Add(new DeliveredFile(
            "delivery_manifest.json",
            BuildDeliveryManifest(captured, files).ToCanonicalUtf8()));
        return files.AsReadOnly();
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildPositiveScenarios(
        CapturedExport captured)
    {
        NarrativeMechanicScenarioBundle bundle = captured.Snapshot.Scenarios;
        List<NarrativeMechanicCatalogCanonicalJsonValue> scenarios = bundle.PositiveScenarios
            .Select((scenario) =>
                (NarrativeMechanicCatalogCanonicalJsonValue)scenario.ToCanonicalJson(
                    captured.CatalogHash,
                    captured.InputDigest))
            .ToList();
        List<KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>> expectedCounts =
            NarrativeMechanicScenarioProfiles.ExpectedPositiveCounts
                .Select((pair) => NarrativeMechanicCatalogCanonicalJson.Property(
                    pair.Key,
                    NarrativeMechanicCatalogCanonicalJson.Integer(pair.Value)))
                .ToList();
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "catalogHash", NarrativeMechanicCatalogCanonicalJson.String(captured.CatalogHash)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "expectedCounts", new NarrativeMechanicCatalogCanonicalJsonObject(expectedCounts)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "inputDigest", NarrativeMechanicCatalogCanonicalJson.String(captured.InputDigest)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "scenarioCount", NarrativeMechanicCatalogCanonicalJson.Integer(scenarios.Count)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "scenarios", new NarrativeMechanicCatalogCanonicalJsonArray(scenarios)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "schemaVersion", NarrativeMechanicCatalogCanonicalJson.Integer(2)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildNegativeScenarios(
        CapturedExport captured)
    {
        List<NarrativeMechanicCatalogCanonicalJsonValue> scenarios = captured.Snapshot.Scenarios
            .NegativeScenarios
            .Select((scenario) =>
                (NarrativeMechanicCatalogCanonicalJsonValue)scenario.ToCanonicalJson(
                    captured.CatalogHash,
                    captured.InputDigest))
            .ToList();
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "catalogHash", NarrativeMechanicCatalogCanonicalJson.String(captured.CatalogHash)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "inputDigest", NarrativeMechanicCatalogCanonicalJson.String(captured.InputDigest)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "negativeScenarioCount", NarrativeMechanicCatalogCanonicalJson.Integer(scenarios.Count)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "scenarios", new NarrativeMechanicCatalogCanonicalJsonArray(scenarios)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "schemaVersion", NarrativeMechanicCatalogCanonicalJson.Integer(3)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildContinuityScenarios(
        CapturedExport captured)
    {
        if (captured?.Snapshot?.ContinuityScenarios == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "continuity-witness-scenario-bundle-missing",
                "Continuity witnesses require the authoritative scenario bundle.");
        }
        return NarrativeMechanicScenarioContinuityWitnesses.Build(
            captured.Snapshot.ContinuityScenarios,
            captured.CatalogHash,
            captured.InputDigest);
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildDeliveryManifest(
        CapturedExport captured,
        IEnumerable<DeliveredFile> deliveredFiles)
    {
        List<NarrativeMechanicCatalogCanonicalJsonValue> files = deliveredFiles
            .OrderBy((file) => file.Name, StringComparer.Ordinal)
            .Select((file) =>
                (NarrativeMechanicCatalogCanonicalJsonValue)
                NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "byteLength", NarrativeMechanicCatalogCanonicalJson.Integer(
                            file.Bytes.Length)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "path", NarrativeMechanicCatalogCanonicalJson.String(file.Name)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "sha256", NarrativeMechanicCatalogCanonicalJson.String(
                            NarrativeMechanicCatalogCanonicalJson.Sha256Prefixed(file.Bytes)))))
            .ToList();
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "catalogHash", NarrativeMechanicCatalogCanonicalJson.String(captured.CatalogHash)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "currentCommit", NarrativeMechanicCatalogCanonicalJson.String(captured.GameCommit)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "files", new NarrativeMechanicCatalogCanonicalJsonArray(files)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "humanApprovalClaimed", NarrativeMechanicCatalogCanonicalJson.Boolean(false)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "inputDigest", NarrativeMechanicCatalogCanonicalJson.String(captured.InputDigest)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "schemaVersion", NarrativeMechanicCatalogCanonicalJson.Integer(2)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "uncommittedSourceHash", NarrativeMechanicCatalogCanonicalJson.String(
                    captured.UncommittedSourceHash)));
    }

    private static byte[] BuildReadmeBytes()
    {
        const string readme =
            "# DungeonStory V25 Narrative Mechanic Package\n\n"
            + "This immutable package contains controlled C# fixtures. It is not evidence of natural gameplay or human approval.\n\n"
            + "## Regeneration\n\n"
            + "In the authoritative DungeonStory Unity project, run `DungeonStory/Narrative/Export Mechanic Catalog`. The exporter captures every fixture through its named production producer and validator, computes full `inputDigest` provenance plus the distinct dirty-only `uncommittedSourceHash`, and writes a create-new directory through an atomic staging move. Existing versions are never overwritten. To attach raw test output, pass an immutable canonical JSON value as `rawTestResults` on `NarrativeMechanicCatalogExportRequest`; the resulting `raw_test_results.json` is delivery-only and does not enter catalog or training inputs.\n\n"
            + "## Validation\n\n"
            + "Verify every SHA-256 entry in `delivery_manifest.json`, then require its `currentCommit`, `inputDigest`, `uncommittedSourceHash`, and `catalogHash` to match the package. Recompute `uncommittedSourceHash` from schemaVersion 1 plus only non-clean declared source entries using canonical `path`, `sha256`, `gitStatus`, and `tracked` fields; the empty set is still hashed. Validate `scenarios_100.json` and `negative_scenarios.json` against `scenario_schema.json`. `continuity_scenarios.json` is a separate deterministic boundary-witness document; it is never added to the 100 accepted scenarios or 15 rejected scenarios. If `raw_test_results.json` is present, verify it through the normal delivery hashes but never treat it as catalog/model input, catalogHash/inputDigest material, or training evidence. Run `NarrativeMechanicCatalogExporter.CompareIndependentExports(() => new NarrativeMechanicCatalogUnityAssetSource()).RequireByteIdentical()` for a normal package, or pass the same immutable request to the overload when comparing a package with raw results, in the authoritative Unity Editor. Supply separately produced evidence gates before claiming training eligibility.\n\n"
            + "## CharacterSkill public semantics\n\n"
            + "CharacterSkill formula generation is authoritative in C#. `characterSkill.formulaPolicy` exports formulaVersion, catalogSha256, strength/soft-cap inputs, milestone weights and the instance-local drawback-credit policy. Every module exports a closed `formula` descriptor containing all four quantized ranges, costs, affinity/conflict declarations, formatterId and applicatorId. Missing metadata fails export. For formulaVersion 2 and later C# exposes all individually legal authored modules and evidence IDs; the model returns only `{selectionId,positiveModuleIds,drawbackModuleIds,evidenceFactIds,displayName,narrativeFlavor}`. C# rejects stale, invented, conflicting or negative-only selections and allocates all numeric values only after validation. A reachable, mandatory and inseparable drawback may finance only its attached generated instance; `positiveCost - drawbackCredit = netCost <= narrativeBudget`, and no credit enters a global or persistent budget. Legacy committed numeric instances remain load support and never authorize new mechanics.\n\n"
            + "## Immutable reproduction\n\n"
            + "A released v15 or v16 directory is immutable and must not be overwritten or served through a compatibility branch. Reproduce it only from its recorded game commit, source digest/inputDigest, catalogHash, exporter source provenance, and delivery-manifest file hashes. A changed semantics contract produces a new create-only export version and new hashes.\n\n"
            + "## AI consumption\n\n"
            + "The current NarrativeAI catalog importer reads only `catalog.json`; it does not ingest scenario files. A separate scenario adapter may consume `scenarios_100.json` and `negative_scenarios.json` only after `delivery_manifest.json` and `scenario_schema.json` validation. Its model-input allowlist is exactly `publicNarrativeContext` and `publicFacts`. It must never consume raw `request`, raw prompt, response, audit, semantic hashes, or fixture data. In particular exclude `responseJson`, expected or accepted outcomes, failure reasons, validator results, internal state, `authorityContext`, `fixtureInput`, `originalFactId`, and `sourceSubjectId`. Positive examples are exactly the accepted records in `scenarios_100.json`; command or validator rejections in `negative_scenarios.json` are evaluation-only and never count toward the 100 positives or become model input. `scenarios_100.json` uses schemaVersion 2 and `negative_scenarios.json` uses schemaVersion 3; an adapter must not fabricate candidates for a rejected command with an empty `fullLegalCandidates` array. Do not infer gameplay authority from generated prose.\n";
        return NarrativeMechanicCatalogCanonicalJson.Utf8NoBom.GetBytes(readme);
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildScenarioSchema()
    {
        NarrativeMechanicCatalogCanonicalJsonObject stringSchema = SchemaType("string");
        NarrativeMechanicCatalogCanonicalJsonObject characterSkillConstraint =
            BuildCharacterSkillScenarioConstraint();
        NarrativeMechanicCatalogCanonicalJsonObject scenarioSchema =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "allOf", NarrativeMechanicCatalogCanonicalJson.Array(
                        characterSkillConstraint)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "additionalProperties", NarrativeMechanicCatalogCanonicalJson.Boolean(false)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                        SchemaProperty("accepted", SchemaType("boolean")),
                        SchemaProperty("authorityContext", SchemaType("object")),
                        SchemaProperty("catalogHash", stringSchema),
                        SchemaProperty("diversityAxes", SchemaType("object")),
                        SchemaProperty("effectDescriptions", SchemaArray(stringSchema, 1)),
                        SchemaProperty("failureReason", stringSchema),
                        SchemaProperty("fixtureInput", SchemaType("object")),
                        SchemaProperty("fullLegalCandidates", SchemaArray(
                            NarrativeMechanicCatalogCanonicalJson.Object(), 0)),
                        SchemaProperty("inputDigest", stringSchema),
                        SchemaProperty("producerIdentifier", stringSchema),
                        SchemaProperty("profileId", SchemaStringEnum(
                            NarrativeMechanicScenarioProfiles.ExpectedPositiveCounts
                                .Select((pair) => pair.Key))),
                        SchemaProperty("publicFacts", SchemaArray(
                            SchemaReference("#/$defs/publicFact"), 1, 128)),
                        SchemaProperty("publicNarrativeContext", SchemaReference(
                            "#/$defs/publicNarrativeContext")),
                        SchemaProperty("request", SchemaType("object")),
                        SchemaProperty("scenarioId", stringSchema),
                        SchemaProperty("schemaVersion", SchemaConstInteger(
                            NarrativeMechanicScenario.SchemaVersion)),
                        SchemaProperty("semanticHash", stringSchema),
                        SchemaProperty("targetPersistentId", SchemaReference("#/$defs/id")),
                        SchemaProperty("validatorIdentifier", stringSchema))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "required", SchemaStringArray(
                        "accepted", "authorityContext", "catalogHash", "diversityAxes",
                        "effectDescriptions", "failureReason", "fixtureInput",
                        "fullLegalCandidates", "inputDigest", "producerIdentifier",
                        "profileId", "publicFacts", "publicNarrativeContext", "request",
                        "scenarioId", "schemaVersion", "semanticHash", "targetPersistentId",
                        "validatorIdentifier")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "type", NarrativeMechanicCatalogCanonicalJson.String("object")));
        NarrativeMechanicCatalogCanonicalJsonObject positiveScenario =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "allOf", NarrativeMechanicCatalogCanonicalJson.Array(
                        SchemaReference("#/$defs/scenario"),
                        NarrativeMechanicCatalogCanonicalJson.Object(
                            NarrativeMechanicCatalogCanonicalJson.Property(
                                "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                                    SchemaProperty("accepted", SchemaConstBoolean(true)),
                                    SchemaProperty("failureReason", SchemaConstString(string.Empty)),
                                    SchemaProperty("fullLegalCandidates", SchemaArray(
                                        NarrativeMechanicCatalogCanonicalJson.Object(), 1))))))));
        NarrativeMechanicCatalogCanonicalJsonObject negativeScenario =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "allOf", NarrativeMechanicCatalogCanonicalJson.Array(
                        SchemaReference("#/$defs/scenario"),
                        NarrativeMechanicCatalogCanonicalJson.Object(
                            NarrativeMechanicCatalogCanonicalJson.Property(
                                "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                                    SchemaProperty("accepted", SchemaConstBoolean(false)),
                                    SchemaProperty("failureReason", SchemaNonBlankString())))))));

        NarrativeMechanicCatalogCanonicalJsonObject positiveDocument =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "additionalProperties", NarrativeMechanicCatalogCanonicalJson.Boolean(false)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                        SchemaProperty("catalogHash", stringSchema),
                        SchemaProperty("expectedCounts", BuildExpectedCountsSchema()),
                        SchemaProperty("inputDigest", stringSchema),
                        SchemaProperty("scenarioCount", SchemaConstInteger(100)),
                        SchemaProperty("scenarios", SchemaArray(
                            SchemaReference("#/$defs/positiveScenario"), 100, 100)),
                        SchemaProperty("schemaVersion", SchemaConstInteger(2)))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "required", SchemaStringArray(
                        "catalogHash", "expectedCounts", "inputDigest", "scenarioCount",
                        "scenarios", "schemaVersion")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "type", NarrativeMechanicCatalogCanonicalJson.String("object")));
        NarrativeMechanicCatalogCanonicalJsonObject negativeDocument =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "additionalProperties", NarrativeMechanicCatalogCanonicalJson.Boolean(false)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                        SchemaProperty("catalogHash", stringSchema),
                        SchemaProperty("inputDigest", stringSchema),
                        SchemaProperty("negativeScenarioCount", SchemaConstInteger(
                            NarrativeMechanicScenarioBundle.ExpectedNegativeScenarioCount)),
                        SchemaProperty("scenarios", SchemaArray(
                            SchemaReference("#/$defs/negativeScenario"), 0)),
                        SchemaProperty("schemaVersion", SchemaConstInteger(3)))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "required", SchemaStringArray(
                        "catalogHash", "inputDigest", "negativeScenarioCount", "scenarios",
                        "schemaVersion")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "type", NarrativeMechanicCatalogCanonicalJson.String("object")));
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "$defs", NarrativeMechanicCatalogCanonicalJson.Object(
                    SchemaProperty("negativeDocument", negativeDocument),
                    SchemaProperty("negativeScenario", negativeScenario),
                    SchemaProperty("positiveDocument", positiveDocument),
                    SchemaProperty("positiveScenario", positiveScenario),
                    SchemaProperty("characterSkillCombinationSemantics",
                        BuildCharacterSkillCombinationSemanticsSchema()),
                    SchemaProperty("characterSkillModuleSemantics",
                        BuildCharacterSkillModuleSemanticsSchema()),
                    SchemaProperty("characterSkillRequest",
                        BuildCharacterSkillRequestSchema()),
                    SchemaProperty("characterSkillFullLegalCandidate",
                        BuildCharacterSkillFullLegalCandidateSchema()),
                    SchemaProperty("id", BuildPublicContextIdSchema()),
                    SchemaProperty("publicFact", BuildPublicFactSchema()),
                    SchemaProperty("publicNarrativeContext", BuildPublicNarrativeContextSchema()),
                    SchemaProperty("scenario", scenarioSchema))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "$id", NarrativeMechanicCatalogCanonicalJson.String(
                    "https://dungeonstory.invalid/schema/v25/narrative-mechanic-scenarios-3.json")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "$schema", NarrativeMechanicCatalogCanonicalJson.String(
                    "https://json-schema.org/draft/2020-12/schema")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "oneOf", NarrativeMechanicCatalogCanonicalJson.Array(
                    SchemaReference("#/$defs/positiveDocument"),
                    SchemaReference("#/$defs/negativeDocument"))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "title", NarrativeMechanicCatalogCanonicalJson.String(
                        "DungeonStory V25 narrative mechanic scenario documents")));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject
        BuildCharacterSkillScenarioConstraint()
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "if", NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                            SchemaProperty("profileId", SchemaConstString(
                                NarrativeMechanicScenarioProfiles.CharacterSkill)))),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "required", SchemaStringArray("profileId")))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "then", NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                            SchemaProperty("fullLegalCandidates", SchemaArray(
                                SchemaReference("#/$defs/characterSkillFullLegalCandidate"), 0)),
                            SchemaProperty("request", SchemaReference(
                                "#/$defs/characterSkillRequest")))))));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject
        BuildCharacterSkillModuleSemanticsSchema()
    {
        NarrativeMechanicCatalogCanonicalJsonObject canonicalTerm =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "minLength", NarrativeMechanicCatalogCanonicalJson.Integer(3)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "pattern", NarrativeMechanicCatalogCanonicalJson.String(
                        "^[a-z0-9_.]+=[^=\\s](?:[^=]*[^=\\s])?$")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "type", NarrativeMechanicCatalogCanonicalJson.String("string")));
        NarrativeMechanicCatalogCanonicalJsonObject terms =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property("items", canonicalTerm),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "minItems", NarrativeMechanicCatalogCanonicalJson.Integer(1)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "type", NarrativeMechanicCatalogCanonicalJson.String("array")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "uniqueItems", NarrativeMechanicCatalogCanonicalJson.Boolean(true)));
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "additionalProperties", NarrativeMechanicCatalogCanonicalJson.Boolean(false)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                    SchemaProperty("descriptionKo", SchemaNonBlankString()),
                    SchemaProperty("effectKind", SchemaStringEnum(
                        CharacterSkillCombinationSemanticsFactory.EffectKindTokens)),
                    SchemaProperty("executionScope", SchemaStringEnum(
                        CharacterSkillCombinationSemanticsFactory.ExecutionScopeTokens)),
                    SchemaProperty("moduleId", BuildCanonicalSemanticIdSchema()),
                    SchemaProperty("terms", terms),
                    SchemaProperty("variantId", BuildCanonicalSemanticIdSchema()))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "required", SchemaStringArray(
                    "descriptionKo", "effectKind", "executionScope", "moduleId", "terms",
                    "variantId")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String("object")));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject
        BuildCharacterSkillCombinationSemanticsSchema()
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "additionalProperties", NarrativeMechanicCatalogCanonicalJson.Boolean(false)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                    SchemaProperty("combinationId", SchemaNonBlankString()),
                    SchemaProperty("descriptionKo", SchemaNonBlankString()),
                    SchemaProperty("modules", SchemaArray(
                        SchemaReference("#/$defs/characterSkillModuleSemantics"), 1)),
                    SchemaProperty("schemaVersion", SchemaConstInteger(
                        CharacterSkillCombinationSemanticsFactory.SchemaVersion)))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "required", SchemaStringArray(
                    "combinationId", "descriptionKo", "modules", "schemaVersion")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String("object")));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildCharacterSkillRequestSchema()
    {
        NarrativeMechanicCatalogCanonicalJsonObject combination =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                        SchemaProperty("semantics", SchemaReference(
                            "#/$defs/characterSkillCombinationSemantics")))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "required", SchemaStringArray("semantics")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "type", NarrativeMechanicCatalogCanonicalJson.String("object")));
        NarrativeMechanicCatalogCanonicalJsonObject rule =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                        SchemaProperty("combinationOptions", SchemaArray(combination, 1)))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "required", SchemaStringArray("combinationOptions")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "type", NarrativeMechanicCatalogCanonicalJson.String("object")));
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                    SchemaProperty("rules", SchemaArray(rule, 1)))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "required", SchemaStringArray("rules")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String("object")));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject
        BuildCharacterSkillFullLegalCandidateSchema()
    {
        NarrativeMechanicCatalogCanonicalJsonObject combination =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                        SchemaProperty("semantics", SchemaReference(
                            "#/$defs/characterSkillCombinationSemantics")))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "required", SchemaStringArray("semantics")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "type", NarrativeMechanicCatalogCanonicalJson.String("object")));
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                    SchemaProperty("options", SchemaArray(combination, 1)))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "required", SchemaStringArray("options")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String("object")));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildCanonicalSemanticIdSchema() =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "minLength", NarrativeMechanicCatalogCanonicalJson.Integer(1)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "pattern", NarrativeMechanicCatalogCanonicalJson.String("^[a-z0-9_]+$")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String("string")));

    private static KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>
        SchemaProperty(string name, NarrativeMechanicCatalogCanonicalJsonValue schema) =>
        NarrativeMechanicCatalogCanonicalJson.Property(name, schema);

    private static NarrativeMechanicCatalogCanonicalJsonObject SchemaType(string type) =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String(type)));

    private static NarrativeMechanicCatalogCanonicalJsonObject SchemaConstInteger(long value) =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "const", NarrativeMechanicCatalogCanonicalJson.Integer(value)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String("integer")));

    private static NarrativeMechanicCatalogCanonicalJsonObject SchemaConstBoolean(bool value) =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "const", NarrativeMechanicCatalogCanonicalJson.Boolean(value)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String("boolean")));

    private static NarrativeMechanicCatalogCanonicalJsonObject SchemaConstString(string value) =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "const", NarrativeMechanicCatalogCanonicalJson.String(value)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String("string")));

    private static NarrativeMechanicCatalogCanonicalJsonObject SchemaNonBlankString() =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "minLength", NarrativeMechanicCatalogCanonicalJson.Integer(1)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String("string")));

    private static NarrativeMechanicCatalogCanonicalJsonObject SchemaStringEnum(
        IEnumerable<string> values) =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "enum", SchemaStringArray(values.ToArray())),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String("string")));

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildExpectedCountsSchema()
    {
        KeyValuePair<string, int>[] counts = NarrativeMechanicScenarioProfiles
            .ExpectedPositiveCounts.ToArray();
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "additionalProperties", NarrativeMechanicCatalogCanonicalJson.Boolean(false)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "properties", new NarrativeMechanicCatalogCanonicalJsonObject(
                    counts.Select((pair) => SchemaProperty(
                        pair.Key,
                        SchemaConstInteger(pair.Value))))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "required", SchemaStringArray(counts.Select((pair) => pair.Key).ToArray())),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String("object")));
    }

    /// <summary>
    /// Mirrors tools/v25_narrative_training/public_narrative_context.schema.json
    /// inside the scenario envelope schema.  Keep this model-facing schema closed:
    /// audit fields intentionally have no slot here.
    /// </summary>
    private static NarrativeMechanicCatalogCanonicalJsonObject BuildPublicNarrativeContextSchema()
    {
        NarrativeMechanicCatalogCanonicalJsonObject entity =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "additionalProperties", NarrativeMechanicCatalogCanonicalJson.Boolean(false)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                        SchemaProperty("displayName", SchemaString(1, 120)),
                        SchemaProperty("entityId", SchemaReference("#/$defs/id")),
                        SchemaProperty("kind", SchemaStringEnum(new[]
                        {
                            "character", "equipment", "facility", "place", "group"
                        })))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "required", SchemaStringArray("entityId", "kind", "displayName")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "type", NarrativeMechanicCatalogCanonicalJson.String("object")));
        NarrativeMechanicCatalogCanonicalJsonObject narrativeEvent =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "additionalProperties", NarrativeMechanicCatalogCanonicalJson.Boolean(false)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                        SchemaProperty("actorId", SchemaStringMaximum(512)),
                        SchemaProperty("count", SchemaIntegerMinimum(1)),
                        SchemaProperty("day", SchemaNullableNonNegativeInteger()),
                        SchemaProperty("evidenceId", SchemaStringMaximum(512)),
                        SchemaProperty("eventType", SchemaReference("#/$defs/id")),
                        SchemaProperty("factId", SchemaReference("#/$defs/id")),
                        SchemaProperty("generation", SchemaNullableNonNegativeInteger()),
                        SchemaProperty("outcome", SchemaStringMaximum(512)),
                        SchemaProperty("targetId", SchemaStringMaximum(512)))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "required", SchemaStringArray(
                        "factId", "evidenceId", "actorId", "targetId", "eventType",
                        "outcome", "day", "count", "generation")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "type", NarrativeMechanicCatalogCanonicalJson.String("object")));
        NarrativeMechanicCatalogCanonicalJsonObject selection =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "additionalProperties", NarrativeMechanicCatalogCanonicalJson.Boolean(false)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                        SchemaProperty("availableFactCount", SchemaIntegerMinimum(1)),
                        SchemaProperty("omittedFactCount", SchemaIntegerMinimum(0)),
                        SchemaProperty("policyId", SchemaReference("#/$defs/id")))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "required", SchemaStringArray(
                        "policyId", "availableFactCount", "omittedFactCount")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "type", NarrativeMechanicCatalogCanonicalJson.String("object")));
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "additionalProperties", NarrativeMechanicCatalogCanonicalJson.Boolean(false)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                    SchemaProperty("backgroundFactIds", SchemaReferenceArray(0, 128)),
                    SchemaProperty("entities", SchemaArray(entity, 1, 64)),
                    SchemaProperty("events", SchemaArray(narrativeEvent, 0, 64)),
                    SchemaProperty("memoryFactIds", SchemaReferenceArray(0, 128)),
                    SchemaProperty("priorHistoryFactIds", SchemaReferenceArray(0, 128)),
                    SchemaProperty("profileId", SchemaStringEnum(
                        NarrativeMechanicScenarioProfiles.ExpectedPositiveCounts
                            .Select(pair => pair.Key))),
                    SchemaProperty("relationshipFactIds", SchemaReferenceArray(0, 128)),
                    SchemaProperty("schemaVersion", SchemaConstInteger(1)),
                    SchemaProperty("selection", selection),
                    SchemaProperty("subjectId", SchemaReference("#/$defs/id")),
                    SchemaProperty("subjectKind", SchemaStringEnum(new[]
                    {
                        "character", "equipment", "facility"
                    })))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "required", SchemaStringArray(
                    "schemaVersion", "profileId", "subjectId", "subjectKind", "entities",
                    "backgroundFactIds", "relationshipFactIds", "memoryFactIds",
                    "priorHistoryFactIds", "events", "selection")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String("object")));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildPublicFactSchema() =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "additionalProperties", NarrativeMechanicCatalogCanonicalJson.Boolean(false)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "properties", NarrativeMechanicCatalogCanonicalJson.Object(
                    SchemaProperty("count", SchemaIntegerMinimum(0)),
                    SchemaProperty("domain", SchemaString(1, 128)),
                    SchemaProperty("factId", SchemaReference("#/$defs/id")),
                    SchemaProperty("lastDay", SchemaIntegerMinimum(0)),
                    SchemaProperty("milestoneCount", SchemaIntegerMinimum(0)),
                    SchemaProperty("outcome", SchemaStringMaximum(512)),
                    SchemaProperty("subjectId", SchemaStringMaximum(512)),
                    SchemaProperty("text", SchemaString(1, 2000)),
                    SchemaProperty("totalValueDecimal", SchemaStringMaximum(512)))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "required", SchemaStringArray("factId", "text")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String("object")));

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildPublicContextIdSchema() =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "maxLength", NarrativeMechanicCatalogCanonicalJson.Integer(512)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "minLength", NarrativeMechanicCatalogCanonicalJson.Integer(1)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "pattern", NarrativeMechanicCatalogCanonicalJson.String("^\\S+$")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String("string")));

    private static NarrativeMechanicCatalogCanonicalJsonObject SchemaReferenceArray(
        long minimum,
        long maximum) =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "items", SchemaReference("#/$defs/id")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "maxItems", NarrativeMechanicCatalogCanonicalJson.Integer(maximum)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "minItems", NarrativeMechanicCatalogCanonicalJson.Integer(minimum)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String("array")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "uniqueItems", NarrativeMechanicCatalogCanonicalJson.Boolean(true)));

    private static NarrativeMechanicCatalogCanonicalJsonObject SchemaString(
        long minimum,
        long maximum) =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "maxLength", NarrativeMechanicCatalogCanonicalJson.Integer(maximum)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "minLength", NarrativeMechanicCatalogCanonicalJson.Integer(minimum)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String("string")));

    private static NarrativeMechanicCatalogCanonicalJsonObject SchemaStringMaximum(long maximum) =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "maxLength", NarrativeMechanicCatalogCanonicalJson.Integer(maximum)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String("string")));

    private static NarrativeMechanicCatalogCanonicalJsonObject SchemaIntegerMinimum(long minimum) =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "minimum", NarrativeMechanicCatalogCanonicalJson.Integer(minimum)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.String("integer")));

    private static NarrativeMechanicCatalogCanonicalJsonObject SchemaNullableNonNegativeInteger() =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "minimum", NarrativeMechanicCatalogCanonicalJson.Integer(0)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "type", NarrativeMechanicCatalogCanonicalJson.Array(
                    NarrativeMechanicCatalogCanonicalJson.String("integer"),
                    NarrativeMechanicCatalogCanonicalJson.String("null"))));

    private static NarrativeMechanicCatalogCanonicalJsonObject SchemaReference(string reference) =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "$ref", NarrativeMechanicCatalogCanonicalJson.String(reference)));

    private static NarrativeMechanicCatalogCanonicalJsonObject SchemaArray(
        NarrativeMechanicCatalogCanonicalJsonValue items,
        long minimum,
        long? maximum = null)
    {
        List<KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>> properties =
            new List<KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>>
            {
                NarrativeMechanicCatalogCanonicalJson.Property("items", items),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "minItems", NarrativeMechanicCatalogCanonicalJson.Integer(minimum)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "type", NarrativeMechanicCatalogCanonicalJson.String("array"))
            };
        if (maximum.HasValue)
        {
            properties.Add(NarrativeMechanicCatalogCanonicalJson.Property(
                "maxItems", NarrativeMechanicCatalogCanonicalJson.Integer(maximum.Value)));
        }
        return new NarrativeMechanicCatalogCanonicalJsonObject(properties);
    }

    private static NarrativeMechanicCatalogCanonicalJsonArray SchemaStringArray(
        params string[] values) =>
        new NarrativeMechanicCatalogCanonicalJsonArray(values.Select((value) =>
            (NarrativeMechanicCatalogCanonicalJsonValue)
            NarrativeMechanicCatalogCanonicalJson.String(value)));

    private static byte[] BuildPackageComparisonBytes(CapturedExport captured)
    {
        List<byte> bytes = new List<byte>();
        foreach (DeliveredFile file in BuildDeliveredFiles(captured)
                     .OrderBy((value) => value.Name, StringComparer.Ordinal))
        {
            bytes.AddRange(NarrativeMechanicCatalogCanonicalJson.Utf8NoBom.GetBytes(file.Name));
            bytes.Add(0);
            bytes.AddRange(Encoding.ASCII.GetBytes(
                file.Bytes.Length.ToString(CultureInfo.InvariantCulture)));
            bytes.Add(0);
            bytes.AddRange(file.Bytes);
            bytes.Add(0);
        }
        return bytes.ToArray();
    }

    private static void WriteCreateNew(string path, byte[] bytes)
    {
        using FileStream stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(true);
    }

    private static string GetProjectRoot()
    {
        DirectoryInfo directory = Directory.GetParent(Application.dataPath);
        if (directory == null)
        {
            throw new NarrativeMechanicCatalogExportException(
                "project-root-unavailable",
                "Could not resolve the Unity project root from Application.dataPath.");
        }

        return directory.FullName;
    }

    private static string GetRequiredGitCommit(string projectRoot)
    {
        string commit = RunGit(projectRoot, "rev-parse HEAD").Trim();
        if (commit.Length != 40 || commit.Any(value => !Uri.IsHexDigit(value)))
        {
            throw new NarrativeMechanicCatalogExportException(
                "git-commit-invalid",
                "git rev-parse HEAD did not return a full 40-character commit hash.");
        }

        return commit.ToLowerInvariant();
    }

    private static string RunGit(string projectRoot, string arguments)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"-C \"{projectRoot.Replace("\"", "\\\"")}\" {arguments}",
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding = new UTF8Encoding(false, true),
            StandardOutputEncoding = new UTF8Encoding(false, true),
            UseShellExecute = false
        };
        using Process process = Process.Start(startInfo);
        if (process == null)
        {
            throw new NarrativeMechanicCatalogExportException(
                "git-process-start-failed",
                "Could not start git for narrative mechanic catalog export.");
        }

        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new NarrativeMechanicCatalogExportException(
                "git-command-failed",
                $"git {arguments} failed with exit code {process.ExitCode}: {standardError.Trim()}");
        }

        return standardOutput;
    }

    private static string CreateUtcVersion()
    {
        return DateTime.UtcNow.ToString("yyyyMMddTHHmmss.fffffffZ", CultureInfo.InvariantCulture);
    }

    private static void ValidateVersion(string version)
    {
        if (version.Length == 0 || version.Any(character =>
                !(char.IsLetterOrDigit(character)
                    || character == '-'
                    || character == '_'
                    || character == '.')))
        {
            throw new NarrativeMechanicCatalogExportException(
                "export-version-invalid",
                "Export UTC version may contain only letters, digits, '-', '_', and '.'.");
        }
    }

    private sealed class CapturedExport
    {
        public CapturedExport(
            NarrativeMechanicCatalogSnapshot snapshot,
            SourceProvenance provenance,
            TrainingDecision training,
            string gameCommit,
            string catalogHash,
            byte[] catalogBytes,
            byte[] rawTestResultsBytes)
        {
            Snapshot = snapshot;
            Provenance = provenance;
            Training = training;
            GameCommit = gameCommit;
            CatalogHash = catalogHash;
            CatalogBytes = (byte[])catalogBytes.Clone();
            RawTestResultsBytes = rawTestResultsBytes == null
                ? null
                : (byte[])rawTestResultsBytes.Clone();
        }

        public NarrativeMechanicCatalogSnapshot Snapshot { get; }
        public SourceProvenance Provenance { get; }
        public TrainingDecision Training { get; }
        public string GameCommit { get; }
        public string CatalogHash { get; }
        public byte[] CatalogBytes { get; }
        public byte[] RawTestResultsBytes { get; }
        public string InputDigest => Provenance.InputDigest;
        public string UncommittedSourceHash => Provenance.UncommittedSourceHash;
        public bool TrainingEligible => Training.IsEligible;
    }

    private sealed class DeliveredFile
    {
        private readonly byte[] bytes;

        public DeliveredFile(string name, byte[] bytes)
        {
            Name = NarrativeMechanicCatalogExportException.Require(name, nameof(name));
            if (Name.IndexOfAny(new[] { '/', '\\' }) >= 0)
            {
                throw new ArgumentException(
                    "Delivered package file names cannot contain path separators.",
                    nameof(name));
            }
            this.bytes = (byte[])(bytes ?? throw new ArgumentNullException(nameof(bytes))).Clone();
        }

        public string Name { get; }
        public byte[] Bytes => (byte[])bytes.Clone();
    }

    private sealed class SourceFile
    {
        public SourceFile(string path, string hash, string gitStatus, bool tracked)
        {
            Path = path;
            Hash = hash;
            GitStatus = gitStatus;
            Tracked = tracked;
        }

        public string Path { get; }
        public string Hash { get; }
        public string GitStatus { get; }
        public bool Tracked { get; }
    }

    private sealed class SourceProvenance
    {
        private SourceProvenance(
            IReadOnlyList<SourceFile> files,
            string inputDigest,
            string uncommittedSourceHash)
        {
            Files = files;
            InputDigest = inputDigest;
            UncommittedSourceHash = uncommittedSourceHash;
        }

        public IReadOnlyList<SourceFile> Files { get; }
        public string InputDigest { get; }
        public string UncommittedSourceHash { get; }

        public static SourceProvenance Capture(
            string projectRoot,
            IEnumerable<string> declaredPaths,
            string gameCommit)
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in declaredPaths ?? Array.Empty<string>())
            {
                string normalized = NormalizeRelativePath(path);
                paths.Add(normalized);
                if (normalized.StartsWith("Assets/", StringComparison.Ordinal)
                    && !normalized.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    paths.Add(normalized + ".meta");
                }
            }

            if (paths.Count == 0)
            {
                throw new NarrativeMechanicCatalogExportException(
                    "provenance-paths-empty",
                    "Catalog export has no declared relevant source files.");
            }

            IReadOnlyDictionary<string, string> gitStatus = ReadGitStatus(projectRoot);
            List<SourceFile> files = paths.OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => CaptureFile(projectRoot, path, gitStatus))
                .ToList();
            List<NarrativeMechanicCatalogCanonicalJsonValue> canonicalFiles = files
                .Select(file => (NarrativeMechanicCatalogCanonicalJsonValue)
                    NarrativeMechanicCatalogCanonicalJson.Object(
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "gitStatus", NarrativeMechanicCatalogCanonicalJson.String(file.GitStatus)),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "path", NarrativeMechanicCatalogCanonicalJson.String(file.Path)),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "sha256", NarrativeMechanicCatalogCanonicalJson.String(file.Hash)),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "tracked", NarrativeMechanicCatalogCanonicalJson.Boolean(file.Tracked))))
                .ToList();
            NarrativeMechanicCatalogCanonicalJsonObject digestDocument =
                NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "files", new NarrativeMechanicCatalogCanonicalJsonArray(canonicalFiles)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "gameCommit", NarrativeMechanicCatalogCanonicalJson.String(gameCommit)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "schemaVersion", NarrativeMechanicCatalogCanonicalJson.Integer(1)));
            List<NarrativeMechanicCatalogCanonicalJsonValue> canonicalUncommittedFiles = files
                .Where(file => !string.Equals(file.GitStatus, "clean", StringComparison.Ordinal))
                .Select(file => (NarrativeMechanicCatalogCanonicalJsonValue)
                    NarrativeMechanicCatalogCanonicalJson.Object(
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "gitStatus", NarrativeMechanicCatalogCanonicalJson.String(file.GitStatus)),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "path", NarrativeMechanicCatalogCanonicalJson.String(file.Path)),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "sha256", NarrativeMechanicCatalogCanonicalJson.String(file.Hash)),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "tracked", NarrativeMechanicCatalogCanonicalJson.Boolean(file.Tracked))))
                .ToList();
            NarrativeMechanicCatalogCanonicalJsonObject uncommittedDocument =
                NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "files", new NarrativeMechanicCatalogCanonicalJsonArray(
                            canonicalUncommittedFiles)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "schemaVersion", NarrativeMechanicCatalogCanonicalJson.Integer(1)));
            return new SourceProvenance(
                files.AsReadOnly(),
                NarrativeMechanicCatalogCanonicalJson.Sha256Prefixed(
                    digestDocument.ToCanonicalUtf8()),
                NarrativeMechanicCatalogCanonicalJson.Sha256Prefixed(
                    uncommittedDocument.ToCanonicalUtf8()));
        }

        private static SourceFile CaptureFile(
            string projectRoot,
            string relativePath,
            IReadOnlyDictionary<string, string> gitStatus)
        {
            string fullPath = Path.GetFullPath(Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string normalizedRoot = Path.GetFullPath(projectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new NarrativeMechanicCatalogExportException(
                    "provenance-path-escaped-root",
                    $"Catalog provenance path escaped the project root: '{relativePath}'.");
            }

            if (!File.Exists(fullPath))
            {
                throw new NarrativeMechanicCatalogExportException(
                    "provenance-file-missing",
                    $"Catalog provenance file is missing: '{relativePath}'.");
            }

            bool changed = gitStatus.TryGetValue(relativePath, out string status);
            return new SourceFile(
                relativePath,
                ComputeFileHash(fullPath),
                changed ? status : "clean",
                !string.Equals(status, "??", StringComparison.Ordinal));
        }

        private static IReadOnlyDictionary<string, string> ReadGitStatus(string projectRoot)
        {
            string raw = RunGit(projectRoot, "status --porcelain=v1 -z --untracked-files=all");
            Dictionary<string, string> result =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] records = raw.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < records.Length; index++)
            {
                string record = records[index];
                if (record.Length < 4)
                {
                    throw new NarrativeMechanicCatalogExportException(
                        "git-status-parse-failed",
                        "git status returned a malformed porcelain-v1 record.");
                }

                string status = record.Substring(0, 2);
                string path = NormalizeRelativePath(record.Substring(3));
                result[path] = status;
                if (status[0] == 'R'
                    || status[0] == 'C'
                    || status[1] == 'R'
                    || status[1] == 'C')
                {
                    if (++index >= records.Length)
                    {
                        throw new NarrativeMechanicCatalogExportException(
                            "git-status-rename-parse-failed",
                            "git status returned a rename/copy record without its original path.");
                    }

                    result[NormalizeRelativePath(records[index])] = status;
                }
            }

            return result;
        }

        private static string NormalizeRelativePath(string value)
        {
            string normalized = NarrativeMechanicCatalogExportException.Require(value, nameof(value))
                .Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal)
                || normalized.Contains(":"))
            {
                throw new NarrativeMechanicCatalogExportException(
                    "provenance-path-not-relative",
                    $"Catalog provenance path must be repository-relative: '{value}'.");
            }

            if (normalized.Split('/').Any(segment => segment.Length == 0 || segment == ".."))
            {
                throw new NarrativeMechanicCatalogExportException(
                    "provenance-path-invalid",
                    $"Catalog provenance path is invalid: '{value}'.");
            }

            return normalized;
        }

        private static string ComputeFileHash(string path)
        {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using SHA256 sha256 = SHA256.Create();
            return "sha256:" + string.Concat(sha256.ComputeHash(stream)
                .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }

    private sealed class TrainingDecision
    {
        private TrainingDecision(
            bool requested,
            bool isEligible,
            NarrativeMechanicCatalogTestEvidence evidence,
            IReadOnlyList<string> blockers)
        {
            Requested = requested;
            IsEligible = isEligible;
            Evidence = evidence;
            Blockers = blockers;
        }

        public bool Requested { get; }
        public bool IsEligible { get; }
        public NarrativeMechanicCatalogTestEvidence Evidence { get; }
        public IReadOnlyList<string> Blockers { get; }

        public static TrainingDecision Evaluate(
            NarrativeMechanicCatalogSnapshot snapshot,
            NarrativeMechanicCatalogExportRequest request,
            string projectRoot,
            string inputDigest)
        {
            SortedSet<string> blockers = new SortedSet<string>(StringComparer.Ordinal);
            foreach (NarrativeMechanicCatalogParityBlocker blocker in snapshot.TrainingPolicy.StaticBlockers)
            {
                blockers.Add($"policy:{blocker.BlockerId}");
            }

            if (!request.RequestTrainingEligibility)
            {
                blockers.Add("training-eligibility-not-requested");
            }

            NarrativeMechanicCatalogTestEvidence evidence = request.TestEvidence;
            if (evidence == null)
            {
                blockers.Add("test-evidence-not-supplied");
            }
            else
            {
                if (!string.Equals(evidence.CapturedInputDigest, inputDigest, StringComparison.Ordinal))
                {
                    blockers.Add("test-evidence-input-digest-mismatch");
                }

                Dictionary<string, NarrativeMechanicCatalogEvidenceGate> gates = evidence.Gates
                    .ToDictionary(value => value.GateId, value => value, StringComparer.Ordinal);
                foreach (string requiredGate in snapshot.TrainingPolicy.RequiredEvidenceGateIds)
                {
                    if (!gates.TryGetValue(requiredGate, out NarrativeMechanicCatalogEvidenceGate gate))
                    {
                        blockers.Add($"required-evidence-gate-missing:{requiredGate}");
                        continue;
                    }

                    if (!gate.Passed)
                    {
                        blockers.Add($"required-evidence-gate-failed:{requiredGate}");
                    }

                    if (!string.Equals(gate.InputDigest, inputDigest, StringComparison.Ordinal))
                    {
                        blockers.Add($"evidence-gate-input-digest-mismatch:{requiredGate}");
                    }

                    if (!VerifyEvidenceArtifact(projectRoot, gate))
                    {
                        blockers.Add($"evidence-gate-artifact-mismatch:{requiredGate}");
                    }
                }
            }

            return new TrainingDecision(
                request.RequestTrainingEligibility,
                request.RequestTrainingEligibility && blockers.Count == 0,
                evidence,
                blockers.ToArray());
        }

        private static bool VerifyEvidenceArtifact(
            string projectRoot,
            NarrativeMechanicCatalogEvidenceGate gate)
        {
            if (string.IsNullOrWhiteSpace(gate.ArtifactPath)
                || string.IsNullOrWhiteSpace(gate.ArtifactHash)
                || !gate.ArtifactHash.StartsWith("sha256:", StringComparison.Ordinal)
                || gate.ArtifactHash.Length != 71)
            {
                return false;
            }

            string relative = gate.ArtifactPath.Replace('\\', '/');
            if (relative.StartsWith("/", StringComparison.Ordinal)
                || relative.Contains(":")
                || relative.Split('/').Any(segment => segment.Length == 0 || segment == ".."))
            {
                return false;
            }

            string fullPath = Path.GetFullPath(Path.Combine(
                projectRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            string normalizedRoot = Path.GetFullPath(projectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(fullPath))
            {
                return false;
            }

            using FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using SHA256 sha256 = SHA256.Create();
            string actual = "sha256:" + string.Concat(sha256.ComputeHash(stream)
                .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            return string.Equals(actual, gate.ArtifactHash, StringComparison.Ordinal);
        }
    }
}
/// <summary>
/// Captures the deterministic source dependency closure for the V25 handoff.
/// </summary>
public static class NarrativeMechanicCatalogProvenance
{
    public static void AddRelevantPaths(ICollection<string> relevantPaths)
    {
        if (relevantPaths == null)
        {
            throw new ArgumentNullException(nameof(relevantPaths));
        }
        AddExporterSourcePaths(relevantPaths);
        AddValidatedDependencyClosure(relevantPaths);
    }

    private static void AddExporterSourcePaths(ICollection<string> relevantPaths)
    {
        string[] sourcePaths =
        {
            "Assets/Scripts/Content/GameContentCatalogSO.cs",
            "Assets/Scripts/Content/GameDomainContentCatalogSO.cs",
            "Assets/Scripts/Models/AI/Core/LlmStaticSchemaCatalog.cs",
            "Assets/Scripts/Models/AI/Core/LocalLlmRequestQueue.cs",
            "Assets/Scripts/Models/AI/Core/NarrativePublicContext.cs",
            "Assets/Scripts/Models/AI/Core/NarrativePublicModelInput.cs",
            "Assets/Scripts/Models/AI/Core/NarrativeStructuredOutputCore.cs",
            "Assets/Scripts/Models/AI/Core/V25NarrativeInferenceContracts.cs",
            "Assets/Scripts/Models/Economy/Content/GenericItemDefinitionSO.cs",
            "Assets/Scripts/Models/Economy/Content/ItemDefinitionCatalogSO.cs",
            "Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs",
            "Assets/Scripts/Models/Items/Core/ItemPrimitives.cs",
            "Assets/Scripts/Models/Evolution/Core/EvolutionHistoryModels.cs",
            "Assets/Scripts/Models/Evolution/Core/EvolutionModuleRegistry.cs",
            "Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionTerms.cs",
            "Assets/Scripts/Models/NarrativeMechanics/NarrativeFormulaContracts.cs",
            "Assets/Scripts/Models/NarrativeMechanics/NarrativeFormulaCore.cs",
            "Assets/Scripts/Services/Character/AI/CustomerPersonaRuntime.cs",
            "Assets/Scripts/Services/Character/AI/LlmJsonResponseParser.cs",
            "Assets/Scripts/Services/Character/AI/NarrativeRequestContext.cs",
            "Assets/Scripts/Services/Character/AI/Editor/EditorCharacterSkillGenerationService.cs",
            "Assets/Scripts/Services/Character/AI/Editor/NarrativeMechanicCatalogCanonicalJson.cs",
            "Assets/Scripts/Services/Character/AI/Editor/NarrativeMechanicCatalogContracts.cs",
            "Assets/Scripts/Services/Character/AI/Editor/NarrativeMechanicCatalogExporter.cs",
            "Assets/Scripts/Services/Character/AI/Editor/NarrativeMechanicCatalogUnityAssetSource.cs",
            "Assets/Scripts/Services/Character/AI/Editor/NarrativeMechanicScenarioContracts.cs",
            "Assets/Scripts/Services/Character/AI/Editor/NarrativeMechanicScenarioPublicContextSerializer.cs",
            "Assets/Scripts/Services/Character/AI/Editor/NarrativeMechanicScenarioContinuityWitnesses.cs",
            "Assets/Scripts/Services/Character/AI/Editor/NarrativeMechanicScenarioCharacterSkillSource.cs",
            "Assets/Scripts/Services/Character/AI/Editor/NarrativeMechanicScenarioAcquiredTraitSource.cs",
            "Assets/Scripts/Services/Character/AI/Editor/NarrativeMechanicScenarioOtherProfilesSource.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitDefinitions.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitInferenceContracts.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitInferenceService.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitEffectSource.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillDrawbackEffectSource.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitManifestationRuntime.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitModuleSO.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSpecialReaction.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitState.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitValidation.cs",
            "Assets/Scripts/Services/Character/Core/CharacterActor.cs",
            "Assets/Scripts/Services/Character/Core/CharacterPerformanceQuery.cs",
            "Assets/Scripts/Services/Character/Core/CharacterPopulationService.cs",
            "Assets/Scripts/Services/Character/Core/CharacterProgression.cs",
            "Assets/Scripts/Services/Character/Core/CharacterStatsProjectionService.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillCombinationSemantics.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillGenerationService.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillFormulaGeneration.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillModuleCapabilityRegistry.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillModels.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillRuntimeEffects.cs",
            "Assets/Scripts/Services/Character/Core/DungeonCharacterSaveData.cs",
            "Assets/Scripts/Services/Character/Core/MemoryErasureSealCommandService.cs",
            "Assets/Scripts/Services/Character/Core/MemoryErasureSealContracts.cs",
            "Assets/Scripts/Services/Character/Core/MemoryErasureSealTransactionService.cs",
            "Assets/Scripts/Services/Character/Ability/AbilityWork.cs",
            "Assets/Scripts/Services/Character/Editor/V25AcquiredTraitContentAssetBuilder.cs",
            "Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs",
            "Assets/Scripts/Services/Combat/EquipmentEvolutionRules.cs",
            "Assets/Scripts/Services/Evolution/Editor/InstanceEvolutionDebugScenarios.cs",
            "Assets/Scripts/Services/Evolution/EvolutionHistoryNarrativeRuntime.cs",
            "Assets/Scripts/Services/Effects/Runtime/CharacterDerivedStatsSnapshot.cs",
            "Assets/Scripts/Services/Effects/Runtime/CharacterGameplayEffectProjector.cs",
            "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionLlmProposalProvider.cs",
            "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs",
            "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionService.cs",
            "Assets/Scripts/Services/Foundation/GameplayEffectConditionDefinitionSO.cs",
            "Assets/Scripts/Services/Foundation/GameplayEffectContracts.cs",
            "Assets/Scripts/Services/Foundation/GameplayEffectDefinitionSO.cs",
            "Assets/Scripts/Services/Infrastructure/CharacterV18RestoreIdentityResolver.cs",
            "Assets/Scripts/Services/Infrastructure/CharacterWorldSaveService.cs",
            "Assets/Scripts/Services/Infrastructure/CharacterWorldSaveValidation.cs",
            "Assets/Scripts/Services/Infrastructure/DungeonRuntimeLifetimeScope.cs",
            "Assets/Scripts/Services/Infrastructure/OffenseSaveService.cs",
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonCharacterRegistration.cs",
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonPresentationRegistration.cs",
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonProgressionOffenseRegistration.cs",
            "Assets/Scripts/Services/Infrastructure/Save/DungeonAggregateReferencePreflight.cs",
            "Assets/Scripts/Services/Infrastructure/Save/WorldAndCharacterSaveSections.cs",
            "Assets/Scripts/Services/Items/CharacterCarryInventory.cs",
            "Assets/Scripts/Services/Items/ItemPileInfoPanel.cs",
            "Assets/Scripts/Services/Items/ItemTransferService.cs",
            "Assets/Scripts/Services/Items/PhysicalItemBatchDispositionService.cs",
            "Assets/Scripts/Services/Items/PhysicalItemsSaveSection.cs",
            "Assets/Scripts/Services/Items/PhysicalItemSaveValidation.cs",
            "Assets/Scripts/Services/Items/WarehousePhysicalRestoreValidation.cs",
            "Assets/Scripts/Services/Items/WorldItemModels.cs",
            "Assets/Scripts/Services/Items/WorldItemPersistenceService.cs",
            "Assets/Scripts/Services/Items/WorldItemStackRuntime.cs",
            "Assets/Scripts/Services/Items/WorldItemWarehouseService.cs",
            "Assets/Scripts/Services/Offense/MemoryErasureSealBossAwardService.cs",
            "Assets/Scripts/Services/Offense/OffenseAggregateSaveValidation.cs",
            "Assets/Scripts/Services/Offense/OffenseExpeditionBattleCompletionHandler.cs",
            "Assets/Scripts/Services/Offense/OffenseRegionRuntime.cs",
            "Assets/Scripts/Services/Offense/OffenseSaveSections.cs",
            "Assets/Scripts/Services/Character/AI/Editor/CharacterProgressionDebugScenarios.cs",
            "Assets/Scripts/Services/Character/AI/Editor/V25NarrativeInferenceDebugScenarios.cs",
            "Assets/Scripts/Services/Items/Editor/PhysicalItemPilePlayModeVerifier.cs",
            "Assets/Scripts/Services/Offense/Editor/OffenseRewardDebugScenarios.cs",
            "Assets/Scripts/Views/UI/CharacterSummaryGrowthPresenter.cs",
            "Assets/Scripts/Views/UI/CharacterSummaryInfo.cs",
            "Assets/Scripts/Views/UI/MemoryErasureSealUseModal.cs",
            "tools/Documentation/verify_v25_narrative_ai_adapter_input.py",
            "tools/Documentation/verify_v25_narrative_mechanic_handoff.py"
        };
        foreach (string sourcePath in sourcePaths)
        {
            AddRequiredFileAndExistingMeta(relevantPaths, sourcePath);
        }
    }

    /// <summary>
    /// Mirrors the handoff verifier's dependency closure from the current tree.
    /// Content-based discovery is deliberate: a new save/UI/DI/test caller that
    /// names the acquired-trait or seal contract enters provenance automatically.
    /// </summary>
    private static void AddValidatedDependencyClosure(ICollection<string> relevantPaths)
    {
        string projectRoot = GetProjectRootForDiscovery();
        string scriptsRoot = Path.Combine(projectRoot, "Assets", "Scripts");
        foreach (string fullPath in Directory.EnumerateFiles(
                     scriptsRoot,
                     "*.cs",
                     SearchOption.AllDirectories).OrderBy(value => value, StringComparer.Ordinal))
        {
            string contents = File.ReadAllText(fullPath);
            bool directContractReference =
                contents.IndexOf("CharacterAcquiredTrait", StringComparison.Ordinal) >= 0
                || contents.IndexOf("MemoryErasureSeal", StringComparison.Ordinal) >= 0;
            string sourceFileName = Path.GetFileName(fullPath);
            bool exporterSource = (sourceFileName.StartsWith(
                    "NarrativeMechanicCatalog",
                    StringComparison.Ordinal)
                || sourceFileName.StartsWith(
                    "NarrativeMechanicScenario",
                    StringComparison.Ordinal))
                && string.Equals(
                    NormalizeDiscoveredPath(projectRoot, Path.GetDirectoryName(fullPath)),
                    "Assets/Scripts/Services/Character/AI/Editor",
                    StringComparison.Ordinal);
            if (directContractReference || exporterSource)
            {
                AddRequiredFileAndExistingMeta(
                    relevantPaths,
                    NormalizeDiscoveredPath(projectRoot, fullPath));
            }
        }

        const string AcquiredTraitAssetRoot =
            "Assets/Resources/SO/V25/AcquiredTraits";
        string authoredRoot = Path.Combine(
            projectRoot,
            AcquiredTraitAssetRoot.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(authoredRoot))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "acquired-trait-provenance-root-missing",
                $"Required provenance root '{AcquiredTraitAssetRoot}' is missing.");
        }
        foreach (string fullPath in Directory.EnumerateFiles(
                     authoredRoot,
                     "*",
                     SearchOption.AllDirectories).OrderBy(value => value, StringComparer.Ordinal))
        {
            AddRequiredFileAndExistingMeta(
                relevantPaths,
                NormalizeDiscoveredPath(projectRoot, fullPath));
        }
        string authoredRootMeta = AcquiredTraitAssetRoot + ".meta";
        if (File.Exists(Path.Combine(
                projectRoot,
                authoredRootMeta.Replace('/', Path.DirectorySeparatorChar))))
        {
            AddRequiredFileAndExistingMeta(relevantPaths, authoredRootMeta);
        }

        string resourcesRoot = Path.Combine(projectRoot, "Assets", "Resources");
        foreach (string fullPath in Directory.EnumerateFiles(
                     resourcesRoot,
                     "*.asset",
                     SearchOption.AllDirectories).OrderBy(value => value, StringComparer.Ordinal))
        {
            if (File.ReadAllText(fullPath).IndexOf(
                    "item:memory-erasure-seal",
                    StringComparison.Ordinal) >= 0)
            {
                AddRequiredFileAndExistingMeta(
                    relevantPaths,
                    NormalizeDiscoveredPath(projectRoot, fullPath));
            }
        }

        string toolsRoot = Path.Combine(projectRoot, "tools");
        if (Directory.Exists(toolsRoot))
        {
            foreach (string fullPath in Directory.EnumerateFiles(
                         toolsRoot,
                         "*.py",
                         SearchOption.AllDirectories).OrderBy(value => value, StringComparer.Ordinal))
            {
                string fileName = Path.GetFileName(fullPath);
                string contents = File.ReadAllText(fullPath);
                if (fileName.IndexOf(
                        "v25_narrative_mechanic_handoff",
                        StringComparison.OrdinalIgnoreCase) >= 0
                    || contents.IndexOf(
                        "verify_v25_narrative_mechanic_handoff",
                        StringComparison.Ordinal) >= 0)
                {
                    AddRequiredFileAndExistingMeta(
                        relevantPaths,
                        NormalizeDiscoveredPath(projectRoot, fullPath));
                }
            }
        }
    }

    private static string GetProjectRootForDiscovery()
    {
        string dataPath = Path.GetFullPath(Application.dataPath);
        DirectoryInfo parent = Directory.GetParent(dataPath);
        if (parent == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "catalog-project-root-missing",
                $"Could not resolve the Unity project root from '{dataPath}'.");
        }
        return parent.FullName.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
    }

    private static string NormalizeDiscoveredPath(string projectRoot, string fullPath)
    {
        string normalizedRoot = Path.GetFullPath(projectRoot).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        string normalizedFullPath = Path.GetFullPath(fullPath);
        string prefix = normalizedRoot + Path.DirectorySeparatorChar;
        if (!normalizedFullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "catalog-provenance-path-outside-project",
                $"Discovered provenance path '{fullPath}' is outside '{projectRoot}'.");
        }
        return normalizedFullPath.Substring(prefix.Length).Replace('\\', '/');
    }

    private static void AddRequiredFileAndExistingMeta(
        ICollection<string> relevantPaths,
        string relativePath)
    {
        string normalized = NarrativeMechanicCatalogExportException.Require(
            relativePath,
            nameof(relativePath)).Replace('\\', '/');
        string fullPath = Path.Combine(
            GetProjectRootForDiscovery(),
            normalized.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "catalog-provenance-file-missing",
                $"Required provenance source '{normalized}' is missing.");
        }
        relevantPaths.Add(normalized);
        if (File.Exists(fullPath + ".meta"))
        {
            relevantPaths.Add(normalized + ".meta");
        }
    }

}

#endif
