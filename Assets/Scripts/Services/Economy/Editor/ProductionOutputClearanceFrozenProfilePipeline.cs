#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

/// <summary>
/// Builds the one authoritative 92-row clearance resource only from a completed
/// bootstrap natural portfolio. Promotion is deliberately a separate editor
/// command so importing the new Resources asset cannot interrupt PlayMode.
/// </summary>
public static class ProductionOutputClearanceFrozenProfilePipeline
{
    public const string CandidatePath =
        "Artifacts/QA/v27-production-output-clearance-profiles.candidate.json";
    public const string GenerationReportPath =
        "Artifacts/QA/v27-production-output-clearance-profile-generation.txt";
    public const string PromotionReportPath =
        "Artifacts/QA/v27-production-output-clearance-profile-promotion.txt";
    public const string ResourceAssetPath =
        "Assets/Resources/V27/production-output-clearance-profiles.json";
    private static readonly UTF8Encoding Utf8NoBom = new(false, true);

    public static void StageFromCompletedBootstrap(
        ProductionOutputClearanceNaturalPortfolioRunResult completed)
    {
        if (completed == null)
            throw new ArgumentNullException(nameof(completed));
        if (!ProductionOutputClearanceNaturalBootstrapProfileSource.IsRequested)
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_STAGE_REQUIRES_BOOTSTRAP");
        }

        int expectedProfiles =
            ProductionOutputClearanceProfileResourceSource.ExpectedProfileCount;
        int expectedSeeds = ProductionOutputClearanceMeasurementPortfolioAuthority
            .RequiredSeedCount;
        int expectedObservations = checked(expectedProfiles * expectedSeeds);
        if (completed.Current.Shards.Count != expectedProfiles
            || completed.Current.Portfolio.Seeds.Count != expectedSeeds
            || completed.Canonical.ProfileObservations.Count
                != expectedObservations)
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_STAGE_DENOMINATOR_INCOMPLETE");
        }

        var profiles = ProductionOutputClearanceProfileAggregator.BuildFrozen(
            completed.Canonical.ProfileObservations,
            completed.Current.AuthoredScope.Coverage.CompleteEnvelopes,
            completed.Current.Portfolio.Seeds,
            expectedProfiles);
        ProductionOutputClearanceCapacityReviewPortfolio review =
            ProductionOutputClearanceCapacityReviewPortfolio.BuildCurrent(
                completed.Current.MeasurementScope,
                profiles);
        if (review.Rows.Count != expectedProfiles
            || review.AcceptedCount < 0
            || review.BackpressureExpectedCount < 0
            || review.AcceptedCount + review.BackpressureExpectedCount
                != expectedProfiles
            || review.BlockingCriticalCount != 0)
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_STAGE_CAPACITY_CRITICAL: rows="
                + review.Rows.Count.ToString(CultureInfo.InvariantCulture)
                + ";accepted="
                + review.AcceptedCount.ToString(CultureInfo.InvariantCulture)
                + ";blockingCritical="
                + review.BlockingCriticalCount.ToString(
                    CultureInfo.InvariantCulture)
                + ";backpressureExpected="
                + review.BackpressureExpectedCount.ToString(
                    CultureInfo.InvariantCulture));
        }

        string canonicalJson =
            ProductionOutputClearanceProfileResourceCodec.SerializeCanonical(
                profiles);
        ProductionOutputClearanceProfileCatalog roundTrip =
            ProductionOutputClearanceProfileResourceCodec.ParseRequired(
                canonicalJson,
                expectedProfiles);
        if (!string.Equals(
                canonicalJson,
                ProductionOutputClearanceProfileResourceCodec.SerializeCanonical(
                    roundTrip.Records),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_STAGE_ROUNDTRIP_DRIFT");
        }

        byte[] candidateBytes = Utf8NoBom.GetBytes(canonicalJson);
        V27BalanceArtifactWriter.WriteIfDifferent(
            CandidatePath,
            stream => stream.Write(candidateBytes, 0, candidateBytes.Length));
        bool secondCandidateWrite = V27BalanceArtifactWriter.WriteIfDifferent(
            CandidatePath,
            stream => stream.Write(candidateBytes, 0, candidateBytes.Length));
        if (secondCandidateWrite)
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_STAGE_SECOND_WRITE_DIFF");
        }

        string report =
            "schema=v27-production-output-clearance-profile-generation@3\n"
            + "result=PASS\n"
            + "currentSourceDigest="
                + V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest() + "\n"
            + "gameplaySceneSha256="
                + V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest() + "\n"
            + "naturalAcceptedDigest=" + completed.Canonical.SourceDigest + "\n"
            + "throughputAuthorityDigest="
                + completed.Current.AuthoredScope.Coverage.SourceDigest + "\n"
            + "capacityReviewDigest=" + review.SourceDigest + "\n"
            + "catalogAuthorityDigest=" + roundTrip.AuthorityDigest + "\n"
            + "profiles=" + expectedProfiles.ToString(CultureInfo.InvariantCulture)
                + "\n"
            + "seedsPerProfile="
                + expectedSeeds.ToString(CultureInfo.InvariantCulture) + "\n"
            + "observations="
                + expectedObservations.ToString(CultureInfo.InvariantCulture) + "\n"
            + "accepted="
                + review.AcceptedCount.ToString(CultureInfo.InvariantCulture)
                + "\n"
            + "backpressureExpected="
                + review.BackpressureExpectedCount.ToString(
                    CultureInfo.InvariantCulture) + "\n"
            + "blockingCritical=0\n"
            + "candidateSha256="
                + V27BalanceArtifactWriter.ComputeSha256(CandidatePath) + "\n"
            + "secondWriteByteDiff=0\n"
            + BuildReviewInputReportLines(review)
            + BuildBackpressureReportLines(review);
        byte[] reportBytes = Utf8NoBom.GetBytes(report);
        V27BalanceArtifactWriter.WriteIfDifferent(
            GenerationReportPath,
            stream => stream.Write(reportBytes, 0, reportBytes.Length));
        if (V27BalanceArtifactWriter.WriteIfDifferent(
                GenerationReportPath,
                stream => stream.Write(reportBytes, 0, reportBytes.Length)))
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_REPORT_SECOND_WRITE_DIFF");
        }
    }

    [MenuItem("DungeonStory/Debug/Balance/V27 Promote Frozen Output Clearance Profiles")]
    public static void PromoteCandidateFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_REQUIRES_EDIT_MODE");

        string candidateAbsolute = Absolute(CandidatePath);
        if (!File.Exists(candidateAbsolute))
            throw new FileNotFoundException(
                "Frozen clearance candidate is missing.", candidateAbsolute);
        string canonicalJson = File.ReadAllText(candidateAbsolute, Utf8NoBom);
        ProductionOutputClearanceProfileCatalog candidate =
            ProductionOutputClearanceProfileResourceCodec.ParseRequired(
                canonicalJson,
                ProductionOutputClearanceProfileResourceSource.ExpectedProfileCount);
        ValidateGenerationReportForPromotion(candidate);
        byte[] bytes = Utf8NoBom.GetBytes(canonicalJson);
        V27BalanceArtifactWriter.WriteIfDifferent(
            ResourceAssetPath,
            stream => stream.Write(bytes, 0, bytes.Length));
        AssetDatabase.ImportAsset(
            ResourceAssetPath,
            ImportAssetOptions.ForceSynchronousImport
                | ImportAssetOptions.ForceUpdate);
        ProductionOutputClearanceProfileResourceSource strict = new();
        if (!string.Equals(
                strict.AuthorityDigest,
                candidate.AuthorityDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_AUTHORITY_DRIFT");
        }
        bool secondWrite = V27BalanceArtifactWriter.WriteIfDifferent(
            ResourceAssetPath,
            stream => stream.Write(bytes, 0, bytes.Length));
        if (secondWrite)
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_SECOND_WRITE_DIFF");

        string report =
            "schema=v27-production-output-clearance-profile-promotion@1\n"
            + "result=PASS\n"
            + "candidateSha256="
                + V27BalanceArtifactWriter.ComputeSha256(CandidatePath) + "\n"
            + "resourceSha256="
                + V27BalanceArtifactWriter.ComputeSha256(ResourceAssetPath) + "\n"
            + "catalogAuthorityDigest=" + strict.AuthorityDigest + "\n"
            + "profiles=" + strict.Records.Count.ToString(
                CultureInfo.InvariantCulture) + "\n"
            + "secondWriteByteDiff=0\n";
        byte[] reportBytes = Utf8NoBom.GetBytes(report);
        V27BalanceArtifactWriter.WriteIfDifferent(
            PromotionReportPath,
            stream => stream.Write(reportBytes, 0, reportBytes.Length));
        if (V27BalanceArtifactWriter.WriteIfDifferent(
                PromotionReportPath,
                stream => stream.Write(reportBytes, 0, reportBytes.Length)))
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_REPORT_SECOND_WRITE_DIFF");
        }
        UnityEngine.Debug.Log(
            "V27_OUTPUT_CLEARANCE_PROFILE_PROMOTION=PASS;authority="
            + strict.AuthorityDigest);
    }

    private static string Absolute(string projectRelativePath)
    {
        string root = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        return Path.Combine(root,
            projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    internal static string BuildBackpressureReportLines(
        ProductionOutputClearanceCapacityReviewPortfolio review)
    {
        StringBuilder builder = new();
        int index = 0;
        foreach (ProductionOutputClearanceCapacityReviewRow row in review.Rows
                     .Where(value => value.RequiresBackpressure))
        {
            ProductionOutputClearanceRequirementAssessment requirement =
                row.Assessment.Requirement;
            builder.Append("backpressure[")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append("]=definition:").Append(row.Input.DefinitionId)
                .Append(";workstation:").Append(row.Input.WorkstationTag)
                .Append(";authoredCycles:")
                .Append(row.Assessment.AuthoredWholeCycles.ToString(
                    CultureInfo.InvariantCulture))
                .Append(";boundedCycles:")
                .Append(requirement.PublishedWholeCycles.ToString(
                    CultureInfo.InvariantCulture))
                .Append(";rawRequiredCycles:")
                .Append(requirement.RequiredWholeCycles.ToString(
                    CultureInfo.InvariantCulture))
                .Append(";p95MilliHours:")
                .Append(row.Profile.P95HaulClearanceMilliHours.ToString(
                    CultureInfo.InvariantCulture))
                .Append(";peakGramsPerHour:")
                .Append(row.Profile.PeakOutputMassGramsPerHour.ToString(
                    CultureInfo.InvariantCulture))
                .Append(";maxCycleGrams:")
                .Append(requirement.MaximumCycleCompletionFootprintGrams.ToString(
                    CultureInfo.InvariantCulture))
                .Append(";requiredGrams:")
                .Append(requirement.RequiredCapacityGrams.ToString(
                    CultureInfo.InvariantCulture))
                .Append(";authoredGrams:")
                .Append(row.Assessment.AuthoredCapacityGrams.ToString(
                    CultureInfo.InvariantCulture))
                .Append(";diagnostic:")
                .Append(row.Assessment.DiagnosticCode)
                .Append(";profileDigest:").Append(row.Profile.SourceDigest)
                .Append(";gateDigest:").Append(row.Assessment.SourceDigest)
                .Append('\n');
            index++;
        }
        return builder.ToString();
    }

    internal static string BuildReviewInputReportLines(
        ProductionOutputClearanceCapacityReviewPortfolio review)
    {
        if (review == null)
            throw new ArgumentNullException(nameof(review));

        StringBuilder builder = new();
        for (int index = 0; index < review.Rows.Count; index++)
        {
            ProductionOutputClearanceCapacityReviewInput input =
                review.Rows[index].Input;
            builder.Append("reviewInput[")
                .Append(index.ToString(CultureInfo.InvariantCulture))
                .Append("]=definition:").Append(input.DefinitionId)
                .Append(";workstation:").Append(input.WorkstationTag)
                .Append(";authoredCycles:")
                .Append(input.AuthoredWholeCycles.ToString(
                    CultureInfo.InvariantCulture))
                .Append(";lanePolicy:")
                .Append(((int)input.LaneProfile.Policy).ToString(
                    CultureInfo.InvariantCulture))
                .Append(";manualLanes:")
                .Append(input.LaneProfile.ManualWorkLaneCount.ToString(
                    CultureInfo.InvariantCulture))
                .Append(";automaticLanes:")
                .Append(input.LaneProfile.AutomaticWorkLaneCount.ToString(
                    CultureInfo.InvariantCulture))
                .Append(";maxCycleGrams:")
                .Append(input.MaximumCycleCompletionFootprintGrams.ToString(
                    CultureInfo.InvariantCulture))
                .Append(";upstreamDigest:")
                .Append(input.UpstreamSourceDigest)
                .Append('\n');
        }
        return builder.ToString();
    }

    private static void ValidateGenerationReportForPromotion(
        ProductionOutputClearanceProfileCatalog candidate)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        string absolute = Absolute(GenerationReportPath);
        if (!File.Exists(absolute))
        {
            throw new FileNotFoundException(
                "Frozen clearance generation report is missing.", absolute);
        }
        byte[] bytes = File.ReadAllBytes(absolute);
        if (bytes.Length >= 3
            && bytes[0] == 0xef
            && bytes[1] == 0xbb
            && bytes[2] == 0xbf)
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_REPORT_BOM");
        }
        string text = Utf8NoBom.GetString(bytes);
        ValidateGenerationReportForPromotion(
            text,
            V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest(),
            V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest(),
            V27BalanceArtifactWriter.ComputeSha256(CandidatePath),
            candidate.AuthorityDigest,
            candidate.Records);
    }

    internal static void ValidateGenerationReportForPromotion(
        string text,
        string expectedSourceDigest,
        string expectedSceneDigest,
        string expectedCandidateSha256,
        string expectedCatalogAuthorityDigest,
        IReadOnlyList<ProductionOutputClearanceProfileRecord> candidateProfiles)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        if (text.Length == 0
            || text[0] == '\uFEFF'
            || text.IndexOf('\r') >= 0
            || !text.EndsWith("\n", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_REPORT_ENCODING");
        }
        string[] expectedScalarKeys =
        {
            "schema", "result", "currentSourceDigest", "gameplaySceneSha256",
            "naturalAcceptedDigest", "throughputAuthorityDigest",
            "capacityReviewDigest", "catalogAuthorityDigest", "profiles",
            "seedsPerProfile", "observations", "accepted",
            "backpressureExpected", "blockingCritical", "candidateSha256",
            "secondWriteByteDiff"
        };
        int expectedProfiles = ProductionOutputClearanceProfileResourceSource
            .ExpectedProfileCount;
        if (candidateProfiles == null
            || candidateProfiles.Count != expectedProfiles
            || !ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                expectedCatalogAuthorityDigest))
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_CANDIDATE_DENOMINATOR");
        }
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        string[] lines = text.Substring(0, text.Length - 1).Split('\n');
        if (lines.Length < expectedScalarKeys.Length + expectedProfiles)
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_REPORT_DENOMINATOR");
        }
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            int separator = line.IndexOf('=');
            if (separator <= 0
                || !values.TryAdd(
                    line.Substring(0, separator),
                    line.Substring(separator + 1)))
            {
                throw new InvalidOperationException(
                    "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_REPORT_MALFORMED");
            }
            string expectedKey;
            if (index < expectedScalarKeys.Length)
            {
                expectedKey = expectedScalarKeys[index];
            }
            else if (index < expectedScalarKeys.Length + expectedProfiles)
            {
                expectedKey = "reviewInput["
                    + (index - expectedScalarKeys.Length).ToString(
                        CultureInfo.InvariantCulture) + "]";
            }
            else
            {
                expectedKey = "backpressure["
                    + (index - expectedScalarKeys.Length - expectedProfiles)
                        .ToString(CultureInfo.InvariantCulture) + "]";
            }
            if (!string.Equals(
                    line.Substring(0, separator),
                    expectedKey,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_REPORT_ORDER");
            }
        }

        RequireGenerationValue(values, "schema",
            "v27-production-output-clearance-profile-generation@3");
        RequireGenerationValue(values, "result", "PASS");
        RequireGenerationValue(values, "currentSourceDigest",
            expectedSourceDigest);
        RequireGenerationValue(values, "gameplaySceneSha256",
            expectedSceneDigest);
        RequireGenerationValue(values, "profiles",
            expectedProfiles.ToString(CultureInfo.InvariantCulture));
        RequireGenerationValue(values, "blockingCritical", "0");
        RequireGenerationValue(values, "candidateSha256",
            expectedCandidateSha256);
        RequireGenerationValue(values, "catalogAuthorityDigest",
            expectedCatalogAuthorityDigest);
        RequireGenerationValue(values, "seedsPerProfile", "32");
        RequireGenerationValue(values, "observations", "2944");
        RequireGenerationValue(values, "secondWriteByteDiff", "0");
        foreach (string digestKey in new[]
        {
            "naturalAcceptedDigest", "throughputAuthorityDigest",
            "capacityReviewDigest", "catalogAuthorityDigest"
        })
        {
            if (!values.TryGetValue(digestKey, out string digest)
                || !ProductionOutputClearanceProfileObservation
                    .IsLowercaseSha256(digest))
            {
                throw new InvalidOperationException(
                    "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_REPORT_DIGEST:"
                    + digestKey);
            }
        }

        if (!values.TryGetValue("accepted", out string acceptedToken)
            || !values.TryGetValue(
                "backpressureExpected",
                out string backpressureToken)
            || !V27CanonicalIntegerText.TryParseNonNegativeInt32(
                acceptedToken, out int accepted)
            || !V27CanonicalIntegerText.TryParseNonNegativeInt32(
                backpressureToken, out int backpressure)
            || accepted > expectedProfiles
            || backpressure > expectedProfiles
            || accepted + backpressure
                != expectedProfiles
            || lines.Length != expectedScalarKeys.Length + expectedProfiles
                + backpressure)
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_DISPOSITION_DENOMINATOR");
        }
        ProductionOutputClearanceCapacityReviewPortfolio rebuilt =
            ValidatePromotionReviewInputs(
                values,
                candidateProfiles,
                expectedProfiles);
        if (rebuilt.AcceptedCount != accepted
            || rebuilt.BackpressureExpectedCount != backpressure
            || rebuilt.BlockingCriticalCount != 0
            || !values.TryGetValue(
                "capacityReviewDigest",
                out string reportedReviewDigest)
            || !string.Equals(
                rebuilt.SourceDigest,
                reportedReviewDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_REVIEW_DRIFT");
        }

        StringBuilder reportedPressure = new();
        for (int index = 0; index < backpressure; index++)
        {
            string key = "backpressure["
                + index.ToString(CultureInfo.InvariantCulture) + "]";
            if (!values.TryGetValue(key, out string pressure))
            {
                throw new InvalidOperationException(
                    "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_PRESSURE_ROW:"
                    + key);
            }
            reportedPressure.Append(key).Append('=').Append(pressure).Append('\n');
        }
        if (!string.Equals(
                reportedPressure.ToString(),
                BuildBackpressureReportLines(rebuilt),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_PRESSURE_DRIFT");
        }
    }

    private static ProductionOutputClearanceCapacityReviewPortfolio
        ValidatePromotionReviewInputs(
            IReadOnlyDictionary<string, string> values,
            IReadOnlyList<ProductionOutputClearanceProfileRecord>
                candidateProfiles,
            int expectedProfiles)
    {
        Dictionary<string, ProductionOutputClearanceProfileRecord> candidates =
            new(StringComparer.Ordinal);
        foreach (ProductionOutputClearanceProfileRecord candidate
                 in candidateProfiles)
        {
            if (candidate == null
                || !candidates.TryAdd(
                    candidate.DefinitionId + "\n" + candidate.WorkstationTag,
                    candidate))
            {
                throw new InvalidOperationException(
                    "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_CANDIDATE_DUPLICATE");
            }
        }

        string[] fieldNames =
        {
            "definition", "workstation", "authoredCycles", "lanePolicy",
            "manualLanes", "automaticLanes", "maxCycleGrams",
            "upstreamDigest"
        };
        List<ProductionOutputClearanceCapacityReviewInput> inputs =
            new(expectedProfiles);
        string previousDefinition = null;
        string previousWorkstation = null;
        for (int index = 0; index < expectedProfiles; index++)
        {
            string reportKey = "reviewInput["
                + index.ToString(CultureInfo.InvariantCulture) + "]";
            if (!values.TryGetValue(reportKey, out string line))
            {
                throw new InvalidOperationException(
                    "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_REVIEW_INPUT_MISSING:"
                    + reportKey);
            }
            Dictionary<string, string> fields = ParseOrderedFields(
                line,
                fieldNames,
                reportKey,
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_REVIEW_INPUT");
            string definition = fields["definition"];
            string workstation = fields["workstation"];
            try
            {
                ProductionOutputClearanceProfileObservation.RequireCanonical(
                    definition,
                    "definition");
                ProductionOutputClearanceProfileObservation.RequireCanonical(
                    workstation,
                    "workstation");
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(
                    "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_REVIEW_INPUT_IDENTITY",
                    exception);
            }

            if (previousDefinition != null
                && (string.CompareOrdinal(previousDefinition, definition) > 0
                    || string.Equals(
                        previousDefinition,
                        definition,
                        StringComparison.Ordinal)
                    && string.CompareOrdinal(previousWorkstation, workstation) >= 0))
            {
                throw new InvalidOperationException(
                    "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_REVIEW_INPUT_ORDER");
            }
            previousDefinition = definition;
            previousWorkstation = workstation;

            string identity = definition + "\n" + workstation;
            if (!candidates.Remove(identity, out var candidate)
                || !V27CanonicalIntegerText.TryParsePositiveInt32(
                    fields["authoredCycles"], out int authoredCycles)
                || !V27CanonicalIntegerText.TryParsePositiveInt32(
                    fields["lanePolicy"], out int lanePolicyValue)
                || !V27CanonicalIntegerText.TryParsePositiveInt32(
                    fields["manualLanes"], out int manualLanes)
                || !V27CanonicalIntegerText.TryParseNonNegativeInt32(
                    fields["automaticLanes"], out int automaticLanes)
                || !V27CanonicalIntegerText.TryParsePositiveInt64(
                    fields["maxCycleGrams"], out long maximumCycleGrams)
                || !ProductionOutputClearanceProfileObservation
                    .IsLowercaseSha256(fields["upstreamDigest"])
                || !Enum.IsDefined(
                    typeof(ProductionWorkstationLanePolicy),
                    lanePolicyValue))
            {
                throw new InvalidOperationException(
                    "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_REVIEW_INPUT_VALUE:"
                    + reportKey);
            }

            try
            {
                ProductionFacilityWorkstationLaneCapacityProfile lane = new(
                    (ProductionWorkstationLanePolicy)lanePolicyValue,
                    manualLanes,
                    automaticLanes);
                ProductionOutputThroughputEnvelopeSnapshot envelope = new(
                    definition,
                    workstation,
                    candidate.PeakOutputMassGramsPerHour,
                    candidate.ThroughputSourceDigest);
                inputs.Add(new ProductionOutputClearanceCapacityReviewInput(
                    definition,
                    workstation,
                    authoredCycles,
                    maximumCycleGrams,
                    lane,
                    envelope,
                    fields["upstreamDigest"]));
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is InvalidOperationException
                || exception is OverflowException)
            {
                throw new InvalidOperationException(
                    "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_REVIEW_INPUT_CONTRACT:"
                    + reportKey,
                    exception);
            }
        }
        if (candidates.Count != 0)
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_CANDIDATE_ORPHAN");
        }
        return ProductionOutputClearanceCapacityReviewPortfolio.Build(
            inputs,
            candidateProfiles);
    }

    private static Dictionary<string, string> ParseOrderedFields(
        string line,
        IReadOnlyList<string> fieldNames,
        string reportKey,
        string diagnosticPrefix)
    {
        string[] tokens = line.Split(';');
        if (tokens.Length != fieldNames.Count)
        {
            throw new InvalidOperationException(
                diagnosticPrefix + "_WIDTH:" + reportKey);
        }
        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        for (int index = 0; index < tokens.Length; index++)
        {
            int separator = tokens[index].IndexOf(':');
            if (separator <= 0
                || !string.Equals(
                    tokens[index].Substring(0, separator),
                    fieldNames[index],
                    StringComparison.Ordinal)
                || !fields.TryAdd(
                    fieldNames[index],
                    tokens[index].Substring(separator + 1))
                || fields[fieldNames[index]].Length == 0)
            {
                throw new InvalidOperationException(
                    diagnosticPrefix + "_FORMAT:" + reportKey);
            }
        }
        return fields;
    }

    private static void RequireGenerationValue(
        IReadOnlyDictionary<string, string> values,
        string key,
        string expected)
    {
        if (!values.TryGetValue(key, out string actual)
            || !string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_PROFILE_PROMOTION_REPORT_DRIFT:"
                + key);
        }
    }
}

public static class ProductionOutputClearanceStrictCurrentVerifier
{
    public const string ReportPath =
        "Artifacts/QA/v27-production-output-clearance-profile-current.txt";
    private static readonly UTF8Encoding Utf8NoBom = new(false, true);

    public static string VerifyAndWrite(
        ProductionOutputClearanceCurrentPortfolioSnapshot current,
        IProductionOutputClearanceProfileSource source)
    {
        if (current == null)
            throw new ArgumentNullException(nameof(current));
        if (source is not ProductionOutputClearanceProfileResourceSource strict)
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_STRICT_CURRENT_RESOURCE_REQUIRED");
        int expected = ProductionOutputClearanceProfileResourceSource
            .ExpectedProfileCount;
        ProductionOutputClearanceCapacityReviewPortfolio review =
            ProductionOutputClearanceCapacityReviewPortfolio.BuildCurrent(
                current.MeasurementScope,
                strict.Records);
        if (strict.Records.Count != expected
            || review.Rows.Count != expected
            || review.AcceptedCount < 0
            || review.BackpressureExpectedCount < 0
            || review.AcceptedCount + review.BackpressureExpectedCount != expected
            || review.BlockingCriticalCount != 0)
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_STRICT_CURRENT_INCOMPLETE");
        }
        foreach (ProductionOutputClearanceCapacityReviewRow row in review.Rows)
        {
            ProductionFacilityCapacitySubject subject = new(
                (BuildingInstanceId)("building:strict-profile:"
                    + row.Input.DefinitionId),
                UnityEngine.Vector2Int.zero,
                row.Input.DefinitionId,
                row.Input.WorkstationTag,
                row.Input.AuthoredWholeCycles,
                row.Input.LaneProfile);
            ProductionOutputClearanceProfileSnapshot selected =
                strict.Capture(subject);
            if (!string.Equals(
                    selected.SourceDigest,
                    row.Profile.SourceDigest,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "PRODUCTION_OUTPUT_CLEARANCE_STRICT_CURRENT_LOOKUP_DRIFT");
            }
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-strict-current@1");
        digest.Append(current.SourceDigest);
        digest.Append(strict.AuthorityDigest);
        digest.Append(review.SourceDigest);
        string verificationDigest = digest.ComputeSha256();
        string report =
            "schema=v27-production-output-clearance-profile-current@2\n"
            + "result=PASS\n"
            + "currentSourceDigest="
                + V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest() + "\n"
            + "gameplaySceneSha256="
                + V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest() + "\n"
            + "currentPortfolioDigest=" + current.SourceDigest + "\n"
            + "catalogAuthorityDigest=" + strict.AuthorityDigest + "\n"
            + "capacityReviewDigest=" + review.SourceDigest + "\n"
            + "verificationDigest=" + verificationDigest + "\n"
            + "profiles=" + strict.Records.Count.ToString(
                CultureInfo.InvariantCulture) + "\n"
            + "accepted="
                + review.AcceptedCount.ToString(CultureInfo.InvariantCulture)
                + "\n"
            + "backpressureExpected="
                + review.BackpressureExpectedCount.ToString(
                    CultureInfo.InvariantCulture) + "\n"
            + "blockingCritical=0\n"
            + "lookupMismatches=0\n"
            + ProductionOutputClearanceFrozenProfilePipeline
                .BuildBackpressureReportLines(review);
        byte[] bytes = Utf8NoBom.GetBytes(report);
        V27BalanceArtifactWriter.WriteIfDifferent(
            ReportPath,
            stream => stream.Write(bytes, 0, bytes.Length));
        if (V27BalanceArtifactWriter.WriteIfDifferent(
                ReportPath,
                stream => stream.Write(bytes, 0, bytes.Length)))
        {
            throw new InvalidOperationException(
                "PRODUCTION_OUTPUT_CLEARANCE_STRICT_CURRENT_SECOND_WRITE_DIFF");
        }
        return verificationDigest;
    }
}

internal static class V27CanonicalIntegerText
{
    public static bool TryParseNonNegativeInt32(string token, out int value)
    {
        value = 0;
        return token != null
            && int.TryParse(
                token,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out value)
            && value >= 0
            && string.Equals(
                value.ToString(CultureInfo.InvariantCulture),
                token,
                StringComparison.Ordinal);
    }

    public static bool TryParsePositiveInt32(string token, out int value) =>
        TryParseNonNegativeInt32(token, out value) && value > 0;

    public static bool TryParsePositiveInt64(string token, out long value)
    {
        value = 0L;
        return token != null
            && long.TryParse(
                token,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out value)
            && value > 0L
            && string.Equals(
                value.ToString(CultureInfo.InvariantCulture),
                token,
                StringComparison.Ordinal);
    }
}
#endif
