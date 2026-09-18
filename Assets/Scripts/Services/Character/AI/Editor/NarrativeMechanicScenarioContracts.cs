#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

public static class NarrativeMechanicScenarioProfiles
{
    public const string CharacterSkill = "CharacterSkillModuleSelection";
    public const string FacilityEvolution = "FacilityEvolution";
    public const string EquipmentChoice = "EquipmentChoice";
    public const string EvolutionHistory = "EvolutionHistory";
    public const string AcquiredTrait = "AcquiredTrait";
    public const string Persona = "Persona";

    private static readonly KeyValuePair<string, int>[] expectedPositiveCounts =
    {
        new KeyValuePair<string, int>(CharacterSkill, 20),
        new KeyValuePair<string, int>(FacilityEvolution, 15),
        new KeyValuePair<string, int>(EquipmentChoice, 15),
        new KeyValuePair<string, int>(EvolutionHistory, 15),
        new KeyValuePair<string, int>(AcquiredTrait, 20),
        new KeyValuePair<string, int>(Persona, 15)
    };

    public static IReadOnlyList<KeyValuePair<string, int>> ExpectedPositiveCounts =>
        Array.AsReadOnly(expectedPositiveCounts);
}

public enum NarrativeMechanicScenarioSetKind
{
    Calibration = 0,
    UnseenNamingEvaluation = 1
}

/// <summary>
/// Immutable delivery-only review metadata for an evaluation scenario.  This
/// value is deliberately excluded from scenario semantic/public JSON and is
/// serialized only by the snapshot-owned novelty review.
/// </summary>
public sealed class NarrativeMechanicScenarioEvaluationMetadata
{
    public const string UnseenEvaluationSplit = "unseen-evaluation";
    public const string DistinctContextDecision = "distinct-context";

    private static readonly string[] substantiveDimensionLabels =
    {
        "event sequence",
        "domain combination",
        "outcome",
        "participant role",
        "generation structure",
        "facility operational history",
        "need/species/background interaction",
        "prior active/erased state"
    };

    public NarrativeMechanicScenarioEvaluationMetadata(
        string familyId,
        string split,
        string nearestCalibrationScenarioId,
        string decision,
        string reason)
    {
        ScenarioFamilyId = RequireCanonicalText(familyId, nameof(familyId));
        Split = RequireCanonicalText(split, nameof(split));
        NearestCalibrationScenarioId = RequireCanonicalText(
            nearestCalibrationScenarioId,
            nameof(nearestCalibrationScenarioId));
        SemanticOverlapDecision = RequireCanonicalText(decision, nameof(decision));
        SemanticOverlapReason = RequireCanonicalText(reason, nameof(reason));

        if (!string.Equals(Split, UnseenEvaluationSplit, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Evaluation split must be exactly '{UnseenEvaluationSplit}'.",
                nameof(split));
        }
        if (!string.Equals(
                SemanticOverlapDecision,
                DistinctContextDecision,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Evaluation decision must be exactly '{DistinctContextDecision}'.",
                nameof(decision));
        }

        int namedDimensions = substantiveDimensionLabels.Count(label =>
            SemanticOverlapReason.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0);
        if (SemanticOverlapReason.Length < 40 || namedDimensions < 2)
        {
            throw new ArgumentException(
                "Evaluation reason must contain at least 40 characters and name at least "
                + "two frozen substantive dimensions: "
                + string.Join(", ", substantiveDimensionLabels) + ".",
                nameof(reason));
        }
    }

    public string ScenarioFamilyId { get; }
    public string Split { get; }
    public string NearestCalibrationScenarioId { get; }
    public string SemanticOverlapDecision { get; }
    public string SemanticOverlapReason { get; }

    internal NarrativeMechanicScenarioEvaluationMetadata Copy() =>
        new NarrativeMechanicScenarioEvaluationMetadata(
            ScenarioFamilyId,
            Split,
            NearestCalibrationScenarioId,
            SemanticOverlapDecision,
            SemanticOverlapReason);

    private static string RequireCanonicalText(string value, string parameterName)
    {
        string normalized = NarrativeMechanicCatalogExportException.Require(
            value,
            parameterName);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Evaluation metadata strings must already be trimmed.",
                parameterName);
        }
        return normalized;
    }
}

/// <summary>
/// One immutable controlled fixture. The request, candidate packet and public
/// facts are copied at construction; catalog/input hashes are injected only at
/// package serialization so fixture construction cannot claim package identity.
/// </summary>
public sealed class NarrativeMechanicScenario
{
    public const int SchemaVersion = 2;

    private readonly NarrativeMechanicCatalogCanonicalJsonObject request;
    private readonly NarrativeMechanicCatalogCanonicalJsonArray fullLegalCandidates;
    private readonly NarrativeMechanicCatalogCanonicalJsonArray publicFacts;
    private readonly NarrativeMechanicCatalogCanonicalJsonObject publicNarrativeContext;
    private readonly NarrativeMechanicScenarioFactAuditProvenance[] factAuditProvenance;
    private readonly NarrativeMechanicCatalogCanonicalJsonArray effectDescriptions;
    private readonly NarrativeMechanicCatalogCanonicalJsonObject authorityContext;
    private readonly NarrativeMechanicCatalogCanonicalJsonObject fixtureInput;
    private readonly KeyValuePair<string, string>[] diversityAxes;
    private readonly NarrativeMechanicScenarioEvaluationMetadata evaluationMetadata;

    public NarrativeMechanicScenario(
        string scenarioId,
        string profileId,
        NarrativeMechanicCatalogCanonicalJsonObject request,
        NarrativeMechanicCatalogCanonicalJsonArray fullLegalCandidates,
        string targetPersistentId,
        NarrativeMechanicScenarioPublicContext publicContext,
        IEnumerable<string> effectDescriptions,
        NarrativeMechanicCatalogCanonicalJsonObject authorityContext,
        NarrativeMechanicCatalogCanonicalJsonObject fixtureInput,
        string producerIdentifier,
        string validatorIdentifier,
        bool accepted,
        string failureReason,
        IEnumerable<KeyValuePair<string, string>> diversityAxes = null,
        NarrativeMechanicScenarioEvaluationMetadata evaluationMetadata = null)
    {
        ScenarioId = RequireCanonicalText(scenarioId, nameof(scenarioId));
        ProfileId = RequireCanonicalText(profileId, nameof(profileId));
        ProducerIdentifier = RequireCanonicalText(
            producerIdentifier,
            nameof(producerIdentifier));
        ValidatorIdentifier = RequireCanonicalText(
            validatorIdentifier,
            nameof(validatorIdentifier));
        this.request = CopyRequired(request, nameof(request));
        this.fullLegalCandidates = CopyRequired(
            fullLegalCandidates,
            nameof(fullLegalCandidates));
        TargetPersistentId = RequireCanonicalText(
            targetPersistentId,
            nameof(targetPersistentId));
        if (publicContext == null)
        {
            throw new ArgumentNullException(nameof(publicContext));
        }
        if (!string.Equals(
                TargetPersistentId,
                publicContext.TargetPersistentId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Scenario '{ScenarioId}' target '{TargetPersistentId}' does not match "
                + "the production public-context subject.",
                nameof(targetPersistentId));
        }
        this.publicFacts = CopyRequired(publicContext.PublicFacts, nameof(publicContext));
        this.publicNarrativeContext = CopyRequired(
            publicContext.PublicNarrativeContext,
            nameof(publicContext));
        factAuditProvenance = publicContext.FactAuditProvenance
            .Select(value => value.Copy())
            .ToArray();
        this.authorityContext = CopyRequired(authorityContext, nameof(authorityContext));
        this.fixtureInput = CopyRequired(fixtureInput, nameof(fixtureInput));
        this.effectDescriptions = new NarrativeMechanicCatalogCanonicalJsonArray(
            (effectDescriptions ?? throw new ArgumentNullException(nameof(effectDescriptions)))
                .Select((value) =>
                    (NarrativeMechanicCatalogCanonicalJsonValue)
                    NarrativeMechanicCatalogCanonicalJson.String(
                        RequireCanonicalText(value, nameof(effectDescriptions)))));
        if (accepted && this.fullLegalCandidates.Values.Count == 0)
        {
            throw new ArgumentException(
                $"Scenario '{ScenarioId}' requires the full non-empty legal candidate packet.",
                nameof(fullLegalCandidates));
        }
        if (this.publicFacts.Values.Count == 0)
        {
            throw new ArgumentException(
                $"Scenario '{ScenarioId}' requires at least one public fact.",
                nameof(publicFacts));
        }
        if (factAuditProvenance.Length != this.publicFacts.Values.Count)
        {
            throw new ArgumentException(
                $"Scenario '{ScenarioId}' audit fact provenance does not match public facts.",
                nameof(publicContext));
        }
        if (this.effectDescriptions.Values.Count == 0)
        {
            throw new ArgumentException(
                $"Scenario '{ScenarioId}' requires at least one C#-authored effect description.",
                nameof(effectDescriptions));
        }

        Accepted = accepted;
        FailureReason = failureReason ?? string.Empty;
        if (!string.Equals(FailureReason, FailureReason.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Scenario '{ScenarioId}' failure reason must already be canonical.",
                nameof(failureReason));
        }
        if (Accepted && FailureReason.Length != 0)
        {
            throw new ArgumentException(
                $"Accepted scenario '{ScenarioId}' must have an empty failure reason.",
                nameof(failureReason));
        }
        if (!Accepted && FailureReason.Length == 0)
        {
            throw new ArgumentException(
                $"Rejected scenario '{ScenarioId}' requires an exact failure reason.",
                nameof(failureReason));
        }

        this.diversityAxes = (diversityAxes
                ?? Array.Empty<KeyValuePair<string, string>>())
            .Select((pair) => new KeyValuePair<string, string>(
                RequireCanonicalText(pair.Key, nameof(diversityAxes)),
                RequireCanonicalText(pair.Value, nameof(diversityAxes))))
            .OrderBy((pair) => pair.Key, StringComparer.Ordinal)
            .ToArray();
        string duplicateAxis = this.diversityAxes
            .GroupBy((pair) => pair.Key, StringComparer.Ordinal)
            .FirstOrDefault((group) => group.Count() > 1)?.Key;
        if (!string.IsNullOrEmpty(duplicateAxis))
        {
            throw new ArgumentException(
                $"Scenario '{ScenarioId}' has duplicate diversity axis '{duplicateAxis}'.",
                nameof(diversityAxes));
        }
        this.evaluationMetadata = evaluationMetadata?.Copy();

        RequireUniquePublicFactIds();
        SemanticHash = NarrativeMechanicCatalogCanonicalJson.Sha256Prefixed(
            BuildSemanticJson(includeScenarioId: false).ToCanonicalUtf8());
    }

    public string ScenarioId { get; }
    public string ProfileId { get; }
    public string TargetPersistentId { get; }
    public string ProducerIdentifier { get; }
    public string ValidatorIdentifier { get; }
    public bool Accepted { get; }
    public string FailureReason { get; }
    public string SemanticHash { get; }
    public IReadOnlyList<KeyValuePair<string, string>> DiversityAxes =>
        Array.AsReadOnly(diversityAxes);
    public NarrativeMechanicScenarioEvaluationMetadata EvaluationMetadata =>
        evaluationMetadata?.Copy();

    // Boundary witnesses reuse immutable production-derived model projections and
    // the separate audit-only tuple sidecar; they never parse a prompt or
    // reconstruct public facts from fixture input.
    internal NarrativeMechanicCatalogCanonicalJsonObject PublicNarrativeContextForWitness =>
        CopyRequired(publicNarrativeContext, nameof(publicNarrativeContext));
    internal NarrativeMechanicCatalogCanonicalJsonArray PublicFactsForWitness =>
        CopyRequired(publicFacts, nameof(publicFacts));
    internal IReadOnlyList<NarrativeMechanicScenarioFactAuditProvenance>
        FactAuditProvenanceForWitness => Array.AsReadOnly(
            factAuditProvenance.Select(value => value.Copy()).ToArray());

    /// <summary>
    /// This overload is retained solely to make stale Editor-only debug code fail
    /// loudly instead of exporting a reconstructed or prompt-derived context.
    /// Official scenario sources must pass a production public material through
    /// NarrativeMechanicScenarioPublicContextSerializer.
    /// </summary>
    [Obsolete("Narrative scenarios require a production public narrative context.")]
    public NarrativeMechanicScenario(
        string scenarioId,
        string profileId,
        NarrativeMechanicCatalogCanonicalJsonObject request,
        NarrativeMechanicCatalogCanonicalJsonArray fullLegalCandidates,
        NarrativeMechanicCatalogCanonicalJsonArray legacyPublicFacts,
        IEnumerable<string> effectDescriptions,
        NarrativeMechanicCatalogCanonicalJsonObject authorityContext,
        NarrativeMechanicCatalogCanonicalJsonObject fixtureInput,
        string producerIdentifier,
        string validatorIdentifier,
        bool accepted,
        string failureReason,
        IEnumerable<KeyValuePair<string, string>> diversityAxes = null)
    {
        throw new NarrativeMechanicCatalogUnsupportedSourceException(
            "narrative-scenario-public-context-required",
            "Scenario fixtures must serialize a production NarrativePublicContextMaterial; "
            + "legacy Editor public-fact arrays cannot be exported.");
    }

    internal NarrativeMechanicCatalogCanonicalJsonObject ToCanonicalJson(
        string catalogHash,
        string inputDigest)
    {
        return BuildSemanticJson(includeScenarioId: true).With(
                "catalogHash",
                NarrativeMechanicCatalogCanonicalJson.String(
                    NarrativeMechanicCatalogExportException.Require(
                        catalogHash,
                        nameof(catalogHash))))
            .With(
                "inputDigest",
                NarrativeMechanicCatalogCanonicalJson.String(
                    NarrativeMechanicCatalogExportException.Require(
                        inputDigest,
                        nameof(inputDigest))))
            .With(
                "semanticHash",
                NarrativeMechanicCatalogCanonicalJson.String(SemanticHash));
    }

    private NarrativeMechanicCatalogCanonicalJsonObject BuildSemanticJson(
        bool includeScenarioId)
    {
        List<KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>> properties =
            new List<KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>>
            {
            NarrativeMechanicCatalogCanonicalJson.Property(
                "accepted", NarrativeMechanicCatalogCanonicalJson.Boolean(Accepted)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "authorityContext", authorityContext),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "diversityAxes", new NarrativeMechanicCatalogCanonicalJsonObject(
                    diversityAxes.Select((pair) =>
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            pair.Key,
                            NarrativeMechanicCatalogCanonicalJson.String(pair.Value))))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "effectDescriptions", effectDescriptions),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "failureReason", NarrativeMechanicCatalogCanonicalJson.String(FailureReason)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "fixtureInput", fixtureInput),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "fullLegalCandidates", fullLegalCandidates),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "producerIdentifier", NarrativeMechanicCatalogCanonicalJson.String(
                    ProducerIdentifier)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "profileId", NarrativeMechanicCatalogCanonicalJson.String(ProfileId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "publicFacts", publicFacts),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "publicNarrativeContext", publicNarrativeContext),
            NarrativeMechanicCatalogCanonicalJson.Property("request", request),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "schemaVersion", NarrativeMechanicCatalogCanonicalJson.Integer(SchemaVersion)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "targetPersistentId", NarrativeMechanicCatalogCanonicalJson.String(
                    TargetPersistentId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "validatorIdentifier", NarrativeMechanicCatalogCanonicalJson.String(
                    ValidatorIdentifier))
            };
        if (includeScenarioId)
        {
            properties.Add(NarrativeMechanicCatalogCanonicalJson.Property(
                "scenarioId", NarrativeMechanicCatalogCanonicalJson.String(ScenarioId)));
        }
        return new NarrativeMechanicCatalogCanonicalJsonObject(properties);
    }

    private void RequireUniquePublicFactIds()
    {
        HashSet<string> factIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (NarrativeMechanicCatalogCanonicalJsonValue value in publicFacts.Values)
        {
            NarrativeMechanicCatalogCanonicalJsonObject fact = value
                as NarrativeMechanicCatalogCanonicalJsonObject;
            NarrativeMechanicCatalogCanonicalJsonString factId = fact?.Properties
                .Where((pair) => string.Equals(pair.Key, "factId", StringComparison.Ordinal))
                .Select((pair) => pair.Value as NarrativeMechanicCatalogCanonicalJsonString)
                .SingleOrDefault();
            if (factId == null
                || string.IsNullOrWhiteSpace(factId.Value)
                || !string.Equals(factId.Value, factId.Value.Trim(), StringComparison.Ordinal)
                || !factIds.Add(factId.Value))
            {
                throw new ArgumentException(
                    $"Scenario '{ScenarioId}' public facts require unique canonical factId strings.",
                    nameof(publicFacts));
            }
        }
    }

    private static string RequireCanonicalText(string value, string parameterName)
    {
        string normalized = NarrativeMechanicCatalogExportException.Require(
            value,
            parameterName);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Scenario identifiers and labels must already be trimmed.",
                parameterName);
        }
        return normalized;
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject CopyRequired(
        NarrativeMechanicCatalogCanonicalJsonObject value,
        string parameterName) =>
        (NarrativeMechanicCatalogCanonicalJsonObject)(value
            ?? throw new ArgumentNullException(parameterName)).Copy();

    private static NarrativeMechanicCatalogCanonicalJsonArray CopyRequired(
        NarrativeMechanicCatalogCanonicalJsonArray value,
        string parameterName) =>
        (NarrativeMechanicCatalogCanonicalJsonArray)(value
            ?? throw new ArgumentNullException(parameterName)).Copy();
}

public sealed class NarrativeMechanicScenarioSourceCapture
{
    private readonly NarrativeMechanicScenario[] positiveScenarios;
    private readonly NarrativeMechanicScenario[] negativeScenarios;
    private readonly string[] relevantPaths;

    public NarrativeMechanicScenarioSourceCapture(
        string profileId,
        IEnumerable<NarrativeMechanicScenario> positiveScenarios,
        IEnumerable<NarrativeMechanicScenario> negativeScenarios,
        IEnumerable<string> relevantPaths,
        bool allowEmptyNegativeScenarios = false)
    {
        ProfileId = NarrativeMechanicCatalogExportException.Require(
            profileId,
            nameof(profileId));
        this.positiveScenarios = CopyScenarios(
            positiveScenarios,
            expectedAccepted: true,
            nameof(positiveScenarios));
        this.negativeScenarios = CopyScenarios(
            negativeScenarios,
            expectedAccepted: false,
            nameof(negativeScenarios));
        if (this.negativeScenarios.Length == 0 && !allowEmptyNegativeScenarios)
        {
            throw new ArgumentException(
                $"Scenario source '{ProfileId}' requires at least one rejected validator case.",
                nameof(negativeScenarios));
        }
        if (this.positiveScenarios.Concat(this.negativeScenarios).Any((scenario) =>
                !string.Equals(scenario.ProfileId, ProfileId, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                $"Scenario source '{ProfileId}' returned another profile's scenario.");
        }
        this.relevantPaths = (relevantPaths ?? Array.Empty<string>())
            .Select(NormalizeRelativePath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy((path) => path, StringComparer.Ordinal)
            .ToArray();
        if (this.relevantPaths.Length == 0)
        {
            throw new ArgumentException(
                $"Scenario source '{ProfileId}' must declare its producer, validator and authored dependencies.",
                nameof(relevantPaths));
        }
    }

    public string ProfileId { get; }
    public IReadOnlyList<NarrativeMechanicScenario> PositiveScenarios =>
        Array.AsReadOnly(positiveScenarios);
    public IReadOnlyList<NarrativeMechanicScenario> NegativeScenarios =>
        Array.AsReadOnly(negativeScenarios);
    public IReadOnlyList<string> RelevantPaths => Array.AsReadOnly(relevantPaths);

    private static NarrativeMechanicScenario[] CopyScenarios(
        IEnumerable<NarrativeMechanicScenario> scenarios,
        bool expectedAccepted,
        string parameterName)
    {
        NarrativeMechanicScenario[] values = (scenarios
                ?? Array.Empty<NarrativeMechanicScenario>())
            .Select((scenario) => scenario ?? throw new ArgumentException(
                "Scenario sources cannot contain null.",
                parameterName))
            .OrderBy((scenario) => scenario.ScenarioId, StringComparer.Ordinal)
            .ToArray();
        if (values.Any((scenario) => scenario.Accepted != expectedAccepted))
        {
            throw new ArgumentException(
                $"{parameterName} contains a scenario with the wrong accepted outcome.",
                parameterName);
        }
        return values;
    }

    private static string NormalizeRelativePath(string value)
    {
        string normalized = NarrativeMechanicCatalogExportException.Require(
            value,
            nameof(value)).Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal)
            || normalized.Contains(":")
            || normalized.Split('/').Any((segment) => segment.Length == 0 || segment == ".."))
        {
            throw new ArgumentException(
                $"Scenario provenance path must be repository-relative: '{value}'.",
                nameof(value));
        }
        return normalized;
    }
}

public interface INarrativeMechanicScenarioSource
{
    /// <summary>
    /// Implementations must invoke the named production producer and matching
    /// validator before returning. They may control fixture inputs, but must not
    /// synthesize candidate packets or clone gameplay policy in Editor code.
    /// </summary>
    string ProfileId { get; }
    NarrativeMechanicScenarioSourceCapture CaptureScenarios();
}

public sealed class NarrativeMechanicScenarioBundle
{
    public const int ExpectedNegativeScenarioCount = 15;

    private readonly NarrativeMechanicScenario[] positiveScenarios;
    private readonly NarrativeMechanicScenario[] negativeScenarios;
    private readonly string[] relevantPaths;

    internal NarrativeMechanicScenarioBundle(
        IEnumerable<NarrativeMechanicScenario> positiveScenarios,
        IEnumerable<NarrativeMechanicScenario> negativeScenarios,
        IEnumerable<string> relevantPaths)
    {
        this.positiveScenarios = (positiveScenarios
                ?? throw new ArgumentNullException(nameof(positiveScenarios)))
            .OrderBy((scenario) => scenario.ProfileId, StringComparer.Ordinal)
            .ThenBy((scenario) => scenario.ScenarioId, StringComparer.Ordinal)
            .ToArray();
        this.negativeScenarios = (negativeScenarios
                ?? throw new ArgumentNullException(nameof(negativeScenarios)))
            .OrderBy((scenario) => scenario.ProfileId, StringComparer.Ordinal)
            .ThenBy((scenario) => scenario.ScenarioId, StringComparer.Ordinal)
            .ToArray();
        this.relevantPaths = (relevantPaths ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .OrderBy((path) => path, StringComparer.Ordinal)
            .ToArray();
        Validate();
    }

    public IReadOnlyList<NarrativeMechanicScenario> PositiveScenarios =>
        Array.AsReadOnly(positiveScenarios);
    public IReadOnlyList<NarrativeMechanicScenario> NegativeScenarios =>
        Array.AsReadOnly(negativeScenarios);
    public IReadOnlyList<string> RelevantPaths => Array.AsReadOnly(relevantPaths);

    private void Validate()
    {
        foreach (KeyValuePair<string, int> expected
                 in NarrativeMechanicScenarioProfiles.ExpectedPositiveCounts)
        {
            int actual = positiveScenarios.Count((scenario) =>
                string.Equals(scenario.ProfileId, expected.Key, StringComparison.Ordinal));
            if (actual != expected.Value)
            {
                throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "narrative-scenario-profile-count",
                    $"Profile '{expected.Key}' requires exactly {expected.Value} positive fixtures; found {actual}.");
            }
        }
        if (positiveScenarios.Length != 100)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "narrative-scenario-positive-count",
                $"Narrative scenario export requires exactly 100 positive fixtures; found {positiveScenarios.Length}.");
        }
        if (negativeScenarios.Length != ExpectedNegativeScenarioCount)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "narrative-scenario-negative-count",
                $"Narrative scenario export requires exactly {ExpectedNegativeScenarioCount} rejected fixtures; found {negativeScenarios.Length}.");
        }
        HashSet<string> knownProfiles = NarrativeMechanicScenarioProfiles
            .ExpectedPositiveCounts.Select((pair) => pair.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (positiveScenarios.Any((scenario) => !knownProfiles.Contains(scenario.ProfileId))
            || negativeScenarios.Any((scenario) => !knownProfiles.Contains(scenario.ProfileId)))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "narrative-scenario-profile-unknown",
                "Narrative scenario export contains an unknown profile ID.");
        }

        NarrativeMechanicScenario[] all = positiveScenarios
            .Concat(negativeScenarios)
            .ToArray();
        string duplicateId = all.GroupBy((scenario) => scenario.ScenarioId, StringComparer.Ordinal)
            .FirstOrDefault((group) => group.Count() > 1)?.Key;
        if (!string.IsNullOrEmpty(duplicateId))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "narrative-scenario-id-duplicate",
                $"Narrative scenario ID '{duplicateId}' is duplicated.");
        }
        string duplicatePositiveMeaning = positiveScenarios
            .GroupBy((scenario) => scenario.SemanticHash, StringComparer.Ordinal)
            .FirstOrDefault((group) => group.Count() > 1)?.Key;
        if (!string.IsNullOrEmpty(duplicatePositiveMeaning))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "narrative-scenario-semantic-clone",
                $"Positive scenarios contain relabeled semantic clone '{duplicatePositiveMeaning}'.");
        }

        RequireAxisCoverage(
            NarrativeMechanicScenarioProfiles.CharacterSkill,
            "kind",
            new[] { "Active", "Passive", "Ultimate" });
        RequireAxisCoverage(
            NarrativeMechanicScenarioProfiles.AcquiredTrait,
            "manifestationMilestone",
            new[] { "3", "8", "20" });
    }

    private void RequireAxisCoverage(
        string profileId,
        string axis,
        IEnumerable<string> requiredValues)
    {
        HashSet<string> actual = positiveScenarios
            .Where((scenario) => string.Equals(
                scenario.ProfileId,
                profileId,
                StringComparison.Ordinal))
            .SelectMany((scenario) => scenario.DiversityAxes)
            .Where((pair) => string.Equals(pair.Key, axis, StringComparison.Ordinal))
            .Select((pair) => pair.Value)
            .ToHashSet(StringComparer.Ordinal);
        string[] missing = requiredValues.Where((value) => !actual.Contains(value)).ToArray();
        if (missing.Length > 0)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "narrative-scenario-axis-coverage",
                $"Profile '{profileId}' lacks required '{axis}' values: {string.Join(",", missing)}.");
        }
    }
}

public static class NarrativeMechanicScenarioCatalog
{
    public static NarrativeMechanicScenarioBundle Capture(
        params INarrativeMechanicScenarioSource[] sources) =>
        Capture((IEnumerable<INarrativeMechanicScenarioSource>)sources);

    public static NarrativeMechanicScenarioBundle Capture(
        IEnumerable<INarrativeMechanicScenarioSource> sources)
    {
        INarrativeMechanicScenarioSource[] ordered = (sources
                ?? throw new ArgumentNullException(nameof(sources)))
            .Select((source) => source ?? throw new ArgumentException(
                "Scenario source collection cannot contain null.",
                nameof(sources)))
            .OrderBy((source) => source.ProfileId, StringComparer.Ordinal)
            .ToArray();
        string duplicateProfile = ordered
            .GroupBy((source) => source.ProfileId, StringComparer.Ordinal)
            .FirstOrDefault((group) => group.Count() > 1)?.Key;
        if (!string.IsNullOrEmpty(duplicateProfile))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "narrative-scenario-source-duplicate",
                $"Narrative scenario profile source '{duplicateProfile}' is duplicated.");
        }

        string[] expectedProfiles = NarrativeMechanicScenarioProfiles
            .ExpectedPositiveCounts.Select((pair) => pair.Key).ToArray();
        if (!ordered.Select((source) => source.ProfileId)
                .SequenceEqual(expectedProfiles.OrderBy((value) => value, StringComparer.Ordinal),
                    StringComparer.Ordinal))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "narrative-scenario-source-set",
                "Narrative scenario capture requires exactly one source for each of the six profiles.");
        }

        NarrativeMechanicScenarioSourceCapture[] captures = ordered
            .Select((source) => source.CaptureScenarios()
                ?? throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "narrative-scenario-source-null",
                    $"Scenario source '{source.ProfileId}' returned null."))
            .ToArray();
        for (int index = 0; index < ordered.Length; index++)
        {
            if (!string.Equals(
                    ordered[index].ProfileId,
                    captures[index].ProfileId,
                    StringComparison.Ordinal))
            {
                throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "narrative-scenario-source-profile-mismatch",
                    $"Scenario source '{ordered[index].ProfileId}' returned capture '{captures[index].ProfileId}'.");
            }
        }

        return new NarrativeMechanicScenarioBundle(
            captures.SelectMany((capture) => capture.PositiveScenarios),
            captures.SelectMany((capture) => capture.NegativeScenarios),
            captures.SelectMany((capture) => capture.RelevantPaths));
    }
}
#endif
