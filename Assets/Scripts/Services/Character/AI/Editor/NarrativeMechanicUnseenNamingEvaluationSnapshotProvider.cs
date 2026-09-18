#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Explicit alternate snapshot provider for the unseen naming evaluation set.
/// The default Unity asset source remains the calibration authority; this
/// provider replaces only its positive scenario bundle and attaches a
/// delivery-only novelty review.
/// </summary>
public sealed class NarrativeMechanicUnseenNamingEvaluationSnapshotProvider
    : INarrativeMechanicCatalogSnapshotProvider
{
    public const string EvaluationSetId = "UNSEEN-NAMING-EVAL-100-V2";
    public const string ExclusionLedgerSha256 =
        "sha256:b515811c01961c04def62c170e67681ee001f0d7118675c606812dfa40ee880b";
    public const string ExclusionSourcePilotHash =
        "sha256:df7dc89e8904bbae8387fd36692c827201fcfe0845dc5f12b0a201e957c98e7f";

    private const string ProviderPath =
        "Assets/Scripts/Services/Character/AI/Editor/"
        + "NarrativeMechanicUnseenNamingEvaluationSnapshotProvider.cs";

    private readonly INarrativeMechanicCatalogSnapshotProvider calibrationProvider;

    public NarrativeMechanicUnseenNamingEvaluationSnapshotProvider()
        : this(new NarrativeMechanicCatalogUnityAssetSource())
    {
    }

    internal NarrativeMechanicUnseenNamingEvaluationSnapshotProvider(
        INarrativeMechanicCatalogSnapshotProvider calibrationProvider)
    {
        this.calibrationProvider = calibrationProvider
            ?? throw new ArgumentNullException(nameof(calibrationProvider));
    }

    public NarrativeMechanicCatalogSnapshot CaptureSnapshot()
    {
        NarrativeMechanicCatalogSnapshot calibration = calibrationProvider.CaptureSnapshot()
            ?? throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "unseen-evaluation-calibration-snapshot-null",
                "The calibration provider returned a null snapshot.");
        if (calibration.Scenarios == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "unseen-evaluation-calibration-scenarios-missing",
                "The calibration snapshot has no six-profile scenario bundle.");
        }
        if (calibration.NoveltyReview != null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "unseen-evaluation-calibration-review-present",
                "The calibration snapshot must not already contain a novelty review.");
        }

        NarrativeMechanicScenarioSourceCapture[] evaluationCaptures =
            CaptureEvaluationSources();
        NarrativeMechanicScenario[] evaluationPositives = evaluationCaptures
            .SelectMany(value => value.PositiveScenarios)
            .ToArray();
        string[] relevantPaths = calibration.RelevantPaths
            .Concat(evaluationCaptures.SelectMany(value => value.RelevantPaths))
            .Append(ProviderPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        NarrativeMechanicScenarioBundle scenarios = new NarrativeMechanicScenarioBundle(
            evaluationPositives,
            calibration.Scenarios.NegativeScenarios,
            relevantPaths);
        NarrativeMechanicUnseenNamingEvaluationAudit.RequireNegativeRegressionPreserved(
            calibration.Scenarios,
            scenarios);
        NarrativeMechanicCatalogCanonicalJsonObject noveltyReview =
            NarrativeMechanicUnseenNamingEvaluationAudit.Build(
                calibration.Scenarios,
                scenarios,
                EvaluationSetId,
                ExclusionLedgerSha256,
                ExclusionSourcePilotHash);

        return new NarrativeMechanicCatalogSnapshot(
            calibration.CatalogId,
            calibration.CharacterSkill,
            calibration.FacilityEvolution,
            calibration.EquipmentEvolution,
            calibration.AcquiredTrait,
            relevantPaths,
            BuildEvaluationTrainingPolicy(calibration.TrainingPolicy),
            calibration.PacketParity,
            scenarios,
            noveltyReview,
            calibration.Scenarios);
    }

    private static NarrativeMechanicCatalogTrainingPolicy BuildEvaluationTrainingPolicy(
        NarrativeMechanicCatalogTrainingPolicy calibrationPolicy)
    {
        if (calibrationPolicy == null)
        {
            throw new ArgumentNullException(nameof(calibrationPolicy));
        }
        NarrativeMechanicCatalogParityBlocker evaluationBlocker =
            new NarrativeMechanicCatalogParityBlocker(
                "unseen-naming-evaluation-training-ineligible",
                "The unseen naming set is controlled evaluation input, not training evidence.",
                "Keep the immutable export evaluation-only; create a separately approved training intake if required.");
        return new NarrativeMechanicCatalogTrainingPolicy(
            calibrationPolicy.RequiredEvidenceGateIds,
            calibrationPolicy.StaticBlockers.Concat(new[] { evaluationBlocker }));
    }

    private static NarrativeMechanicScenarioSourceCapture[] CaptureEvaluationSources()
    {
        INarrativeMechanicScenarioSource[] sources =
        {
            new NarrativeMechanicScenarioCharacterSkillSource(
                NarrativeMechanicScenarioSetKind.UnseenNamingEvaluation),
            new NarrativeMechanicScenarioFacilityEvolutionSource(
                NarrativeMechanicScenarioSetKind.UnseenNamingEvaluation),
            new NarrativeMechanicScenarioEquipmentChoiceSource(
                NarrativeMechanicScenarioSetKind.UnseenNamingEvaluation),
            new NarrativeMechanicScenarioEvolutionHistorySource(
                NarrativeMechanicScenarioSetKind.UnseenNamingEvaluation),
            new NarrativeMechanicScenarioAcquiredTraitSource(
                NarrativeMechanicScenarioSetKind.UnseenNamingEvaluation),
            new NarrativeMechanicScenarioPersonaSource(
                NarrativeMechanicScenarioSetKind.UnseenNamingEvaluation)
        };
        NarrativeMechanicScenarioSourceCapture[] captures = sources
            .OrderBy(value => value.ProfileId, StringComparer.Ordinal)
            .Select(source => source.CaptureScenarios()
                ?? throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "unseen-evaluation-source-null",
                    $"Evaluation source '{source.ProfileId}' returned null."))
            .ToArray();
        string[] expectedProfiles = NarrativeMechanicScenarioProfiles
            .ExpectedPositiveCounts
            .Select(value => value.Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!captures.Select(value => value.ProfileId).SequenceEqual(
                expectedProfiles,
                StringComparer.Ordinal))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "unseen-evaluation-source-set",
                "Evaluation capture requires exactly one source for each of the six profiles.");
        }
        if (captures.Any(value => value.NegativeScenarios.Count != 0))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "unseen-evaluation-negative-source-nonempty",
                "Evaluation-mode sources must not replace the calibration negative scenarios.");
        }
        foreach (KeyValuePair<string, int> expected
                 in NarrativeMechanicScenarioProfiles.ExpectedPositiveCounts)
        {
            NarrativeMechanicScenarioSourceCapture capture = captures.Single(value =>
                string.Equals(value.ProfileId, expected.Key, StringComparison.Ordinal));
            if (capture.PositiveScenarios.Count != expected.Value)
            {
                throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "unseen-evaluation-profile-count",
                    $"Evaluation profile '{expected.Key}' requires exactly {expected.Value} "
                    + $"positive scenarios; found {capture.PositiveScenarios.Count}.");
            }
        }
        return captures;
    }
}

/// <summary>
/// Builds the fail-closed, delivery-only novelty audit from immutable scenarios.
/// It does not parse prompts, infer semantic novelty, or enter catalog/model input.
/// </summary>
public static class NarrativeMechanicUnseenNamingEvaluationAudit
{
    public const int ExpectedFamilyCount = 85;
    private const string ScenarioIdToken = "unseen-eval-v1";

    public static NarrativeMechanicCatalogCanonicalJsonObject Build(
        NarrativeMechanicScenarioBundle calibration,
        NarrativeMechanicScenarioBundle evaluation,
        string evaluationSetId,
        string exclusionLedgerSha256,
        string exclusionSourcePilotHash)
    {
        if (calibration == null)
        {
            throw new ArgumentNullException(nameof(calibration));
        }
        if (evaluation == null)
        {
            throw new ArgumentNullException(nameof(evaluation));
        }
        string setId = NarrativeMechanicCatalogExportException.Require(
            evaluationSetId,
            nameof(evaluationSetId));
        string ledgerHash = RequireSha256(exclusionLedgerSha256, nameof(exclusionLedgerSha256));
        string pilotHash = RequireSha256(
            exclusionSourcePilotHash,
            nameof(exclusionSourcePilotHash));
        if (calibration.PositiveScenarios.Count != 100)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "unseen-evaluation-calibration-positive-count",
                $"Calibration requires exactly 100 positives; found {calibration.PositiveScenarios.Count}.");
        }
        if (evaluation.PositiveScenarios.Count != 100)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "unseen-evaluation-positive-count",
                $"Evaluation requires exactly 100 positives; found {evaluation.PositiveScenarios.Count}.");
        }

        HashSet<string> calibrationScenarioIds = calibration.PositiveScenarios
            .Select(value => value.ScenarioId)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, NarrativeMechanicScenario> calibrationById =
            calibration.PositiveScenarios.ToDictionary(
                value => value.ScenarioId,
                value => value,
                StringComparer.Ordinal);
        HashSet<string> calibrationStoryHashes = calibration.PositiveScenarios
            .Select(PublicStoryHash)
            .ToHashSet(StringComparer.Ordinal);
        NarrativeMechanicScenario[] scenarios = evaluation.PositiveScenarios
            .OrderBy(value => value.ProfileId, StringComparer.Ordinal)
            .ThenBy(value => value.ScenarioId, StringComparer.Ordinal)
            .ToArray();

        int exactScenarioIdOverlap = scenarios.Count(value =>
            calibrationScenarioIds.Contains(value.ScenarioId));
        string[] publicStoryHashes = scenarios.Select(PublicStoryHash).ToArray();
        int exactPublicStoryHashOverlap = publicStoryHashes.Count(value =>
            calibrationStoryHashes.Contains(value));
        if (exactScenarioIdOverlap != 0 || exactPublicStoryHashOverlap != 0)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "unseen-evaluation-exact-calibration-overlap",
                "Evaluation rows overlap calibration: "
                + $"scenarioIds={exactScenarioIdOverlap}, publicStories={exactPublicStoryHashOverlap}.");
        }
        string duplicatePublicStoryHash = publicStoryHashes
            .GroupBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (!string.IsNullOrEmpty(duplicatePublicStoryHash))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "unseen-evaluation-public-story-duplicate",
                $"Evaluation rows contain duplicate public story '{duplicatePublicStoryHash}'.");
        }

        List<NarrativeMechanicCatalogCanonicalJsonValue> rows =
            new List<NarrativeMechanicCatalogCanonicalJsonValue>(scenarios.Length);
        Dictionary<string, List<string>> familyProfiles =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
        Dictionary<string, int> profileCounts = NarrativeMechanicScenarioProfiles
            .ExpectedPositiveCounts.ToDictionary(
                value => value.Key,
                _ => 0,
                StringComparer.Ordinal);
        Dictionary<string, int> splitCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        int unreviewedCount = 0;
        for (int index = 0; index < scenarios.Length; index++)
        {
            NarrativeMechanicScenario scenario = scenarios[index];
            NarrativeMechanicScenarioEvaluationMetadata metadata =
                scenario.EvaluationMetadata;
            if (metadata == null)
            {
                unreviewedCount++;
                continue;
            }
            if (scenario.ScenarioId.IndexOf(ScenarioIdToken, StringComparison.Ordinal) < 0
                || metadata.ScenarioFamilyId.IndexOf(
                    ScenarioIdToken,
                    StringComparison.Ordinal) < 0)
            {
                throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "unseen-evaluation-stable-id-token",
                    $"Evaluation scenario/family '{scenario.ScenarioId}' must contain '{ScenarioIdToken}'.");
            }
            if (!calibrationById.TryGetValue(
                    metadata.NearestCalibrationScenarioId,
                    out NarrativeMechanicScenario nearest)
                || !string.Equals(nearest.ProfileId, scenario.ProfileId, StringComparison.Ordinal))
            {
                throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "unseen-evaluation-nearest-calibration-invalid",
                    $"Scenario '{scenario.ScenarioId}' names unknown or cross-profile nearest "
                    + $"calibration row '{metadata.NearestCalibrationScenarioId}'.");
            }
            profileCounts[scenario.ProfileId]++;
            splitCounts[metadata.Split] = splitCounts.TryGetValue(metadata.Split, out int splitCount)
                ? splitCount + 1
                : 1;
            if (!familyProfiles.TryGetValue(
                    metadata.ScenarioFamilyId,
                    out List<string> profiles))
            {
                profiles = new List<string>();
                familyProfiles.Add(metadata.ScenarioFamilyId, profiles);
            }
            profiles.Add(scenario.ProfileId);

            string publicStoryHash = publicStoryHashes[index];
            rows.Add(NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "exactCalibrationStoryMatch",
                    NarrativeMechanicCatalogCanonicalJson.Boolean(false)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "nearestCalibrationScenarioId",
                    NarrativeMechanicCatalogCanonicalJson.String(
                        metadata.NearestCalibrationScenarioId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "profileId",
                    NarrativeMechanicCatalogCanonicalJson.String(scenario.ProfileId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "publicStoryHash",
                    NarrativeMechanicCatalogCanonicalJson.String(publicStoryHash)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "scenarioFamilyId",
                    NarrativeMechanicCatalogCanonicalJson.String(metadata.ScenarioFamilyId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "scenarioId",
                    NarrativeMechanicCatalogCanonicalJson.String(scenario.ScenarioId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "semanticOverlapDecision",
                    NarrativeMechanicCatalogCanonicalJson.String(
                        metadata.SemanticOverlapDecision)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "semanticOverlapReason",
                    NarrativeMechanicCatalogCanonicalJson.String(
                        metadata.SemanticOverlapReason)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "split",
                    NarrativeMechanicCatalogCanonicalJson.String(metadata.Split))));
        }
        if (unreviewedCount != 0)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "unseen-evaluation-review-missing",
                $"Evaluation has {unreviewedCount} rows without immutable review metadata.");
        }
        RequireProfileCounts(profileCounts);
        RequireFamilyContract(familyProfiles);
        if (splitCounts.Count != 1
            || !splitCounts.TryGetValue(
                NarrativeMechanicScenarioEvaluationMetadata.UnseenEvaluationSplit,
                out int evaluationSplitCount)
            || evaluationSplitCount != 100)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "unseen-evaluation-split-count",
                "All 100 evaluation rows must use split 'unseen-evaluation'.");
        }

        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "calibrationScenarioCount",
                NarrativeMechanicCatalogCanonicalJson.Integer(
                    calibration.PositiveScenarios.Count)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "evaluationSetId",
                NarrativeMechanicCatalogCanonicalJson.String(setId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "exactPublicStoryHashOverlap",
                NarrativeMechanicCatalogCanonicalJson.Integer(exactPublicStoryHashOverlap)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "exactScenarioIdOverlap",
                NarrativeMechanicCatalogCanonicalJson.Integer(exactScenarioIdOverlap)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "exclusionLedgerSha256",
                NarrativeMechanicCatalogCanonicalJson.String(ledgerHash)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "exclusionSourcePilotHash",
                NarrativeMechanicCatalogCanonicalJson.String(pilotHash)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "familyCount",
                NarrativeMechanicCatalogCanonicalJson.Integer(familyProfiles.Count)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "humanApprovalClaimed",
                NarrativeMechanicCatalogCanonicalJson.Boolean(false)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "modelInferenceRun",
                NarrativeMechanicCatalogCanonicalJson.String("NOT_RUN")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "naturalPlayDistribution",
                NarrativeMechanicCatalogCanonicalJson.String("NOT_RUN")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "profileCounts",
                CountObject(profileCounts)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "rows",
                new NarrativeMechanicCatalogCanonicalJsonArray(rows)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "schemaVersion",
                NarrativeMechanicCatalogCanonicalJson.Integer(1)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "semanticReviewCount",
                NarrativeMechanicCatalogCanonicalJson.Integer(rows.Count)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "splitCounts",
                CountObject(splitCounts)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "trainingEligible",
                NarrativeMechanicCatalogCanonicalJson.Boolean(false)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "unreviewedCount",
                NarrativeMechanicCatalogCanonicalJson.Integer(unreviewedCount)));
    }

    internal static void RequireNegativeRegressionPreserved(
        NarrativeMechanicScenarioBundle calibration,
        NarrativeMechanicScenarioBundle evaluation)
    {
        NarrativeMechanicScenario[] expected = calibration.NegativeScenarios
            .OrderBy(value => value.ProfileId, StringComparer.Ordinal)
            .ThenBy(value => value.ScenarioId, StringComparer.Ordinal)
            .ToArray();
        NarrativeMechanicScenario[] actual = evaluation.NegativeScenarios
            .OrderBy(value => value.ProfileId, StringComparer.Ordinal)
            .ThenBy(value => value.ScenarioId, StringComparer.Ordinal)
            .ToArray();
        if (expected.Length != NarrativeMechanicScenarioBundle.ExpectedNegativeScenarioCount
            || actual.Length != expected.Length)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "unseen-evaluation-negative-regression-count",
                $"Evaluation must preserve all {NarrativeMechanicScenarioBundle.ExpectedNegativeScenarioCount} "
                + "calibration negatives.");
        }
        for (int index = 0; index < expected.Length; index++)
        {
            byte[] expectedBytes = expected[index].ToCanonicalJson(
                "sha256:negative-regression-catalog",
                "sha256:negative-regression-input").ToCanonicalUtf8();
            byte[] actualBytes = actual[index].ToCanonicalJson(
                "sha256:negative-regression-catalog",
                "sha256:negative-regression-input").ToCanonicalUtf8();
            if (!expectedBytes.SequenceEqual(actualBytes))
            {
                throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "unseen-evaluation-negative-regression-changed",
                    $"Calibration negative '{expected[index].ScenarioId}' changed in evaluation capture.");
            }
        }
    }

    private static string PublicStoryHash(NarrativeMechanicScenario scenario)
    {
        NarrativeMechanicCatalogCanonicalJsonObject publicStory =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "profileId",
                    NarrativeMechanicCatalogCanonicalJson.String(scenario.ProfileId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "publicFacts",
                    scenario.PublicFactsForWitness),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "publicNarrativeContext",
                    scenario.PublicNarrativeContextForWitness));
        return NarrativeMechanicCatalogCanonicalJson.Sha256Prefixed(
            publicStory.ToCanonicalUtf8());
    }

    private static void RequireProfileCounts(IReadOnlyDictionary<string, int> profileCounts)
    {
        foreach (KeyValuePair<string, int> expected
                 in NarrativeMechanicScenarioProfiles.ExpectedPositiveCounts)
        {
            if (!profileCounts.TryGetValue(expected.Key, out int actual)
                || actual != expected.Value)
            {
                throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "unseen-evaluation-audit-profile-count",
                    $"Audit profile '{expected.Key}' requires {expected.Value} rows; found {actual}.");
            }
        }
    }

    private static void RequireFamilyContract(
        IReadOnlyDictionary<string, List<string>> familyProfiles)
    {
        if (familyProfiles.Count != ExpectedFamilyCount)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "unseen-evaluation-family-count",
                $"Evaluation requires exactly {ExpectedFamilyCount} families; found {familyProfiles.Count}.");
        }
        int pairedEquipmentHistoryFamilies = 0;
        foreach (KeyValuePair<string, List<string>> family in familyProfiles)
        {
            string[] profiles = family.Value.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (profiles.Length == 1)
            {
                continue;
            }
            if (profiles.Length == 2
                && string.Equals(
                    profiles[0],
                    NarrativeMechanicScenarioProfiles.EquipmentChoice,
                    StringComparison.Ordinal)
                && string.Equals(
                    profiles[1],
                    NarrativeMechanicScenarioProfiles.EvolutionHistory,
                    StringComparison.Ordinal))
            {
                pairedEquipmentHistoryFamilies++;
                continue;
            }
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "unseen-evaluation-family-cross-profile",
                $"Family '{family.Key}' contains unsupported profiles '{string.Join(",", profiles)}'.");
        }
        if (pairedEquipmentHistoryFamilies != 15)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "unseen-evaluation-family-pair-count",
                $"Expected 15 EquipmentChoice/EvolutionHistory family pairs; found "
                + $"{pairedEquipmentHistoryFamilies}.");
        }
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject CountObject(
        IEnumerable<KeyValuePair<string, int>> counts)
    {
        return new NarrativeMechanicCatalogCanonicalJsonObject(
            counts.Select(value => NarrativeMechanicCatalogCanonicalJson.Property(
                value.Key,
                NarrativeMechanicCatalogCanonicalJson.Integer(value.Value))));
    }

    private static string RequireSha256(string value, string parameterName)
    {
        string normalized = NarrativeMechanicCatalogExportException.Require(value, parameterName);
        if (!normalized.StartsWith("sha256:", StringComparison.Ordinal)
            || normalized.Length != 71
            || normalized.Substring(7).Any(character =>
                !(character >= '0' && character <= '9')
                && !(character >= 'a' && character <= 'f')))
        {
            throw new ArgumentException(
                "Expected a lowercase sha256:<64-hex> identity.",
                parameterName);
        }
        return normalized;
    }
}
#endif
