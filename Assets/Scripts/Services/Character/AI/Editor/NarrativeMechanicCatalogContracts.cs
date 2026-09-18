#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class NarrativeMechanicCatalogExportException : InvalidOperationException
{
    public NarrativeMechanicCatalogExportException(string code, string message)
        : base(message)
    {
        Code = Require(code, nameof(code));
    }

    public string Code { get; }

    internal static string Require(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new ArgumentException("A non-blank value is required.", parameterName);
        }

        return normalized;
    }
}

public sealed class NarrativeMechanicCatalogUnsupportedSourceException
    : NarrativeMechanicCatalogExportException
{
    public NarrativeMechanicCatalogUnsupportedSourceException(string code, string message)
        : base(code, message)
    {
    }
}

public sealed class NarrativeMechanicCatalogParityBlocker
{
    public NarrativeMechanicCatalogParityBlocker(
        string blockerId,
        string reason,
        string resolution)
    {
        BlockerId = NarrativeMechanicCatalogExportException.Require(blockerId, nameof(blockerId));
        Reason = NarrativeMechanicCatalogExportException.Require(reason, nameof(reason));
        Resolution = NarrativeMechanicCatalogExportException.Require(resolution, nameof(resolution));
    }

    public string BlockerId { get; }
    public string Reason { get; }
    public string Resolution { get; }

    internal static NarrativeMechanicCatalogParityBlocker[] NormalizeAndValidate(
        IEnumerable<NarrativeMechanicCatalogParityBlocker> values,
        string parameterName,
        string ownerLabel)
    {
        NarrativeMechanicCatalogParityBlocker[] normalized =
            (values ?? Array.Empty<NarrativeMechanicCatalogParityBlocker>())
                .Select(value => value ?? throw new ArgumentException(
                    $"{ownerLabel} blockers cannot contain null.", parameterName))
                .OrderBy(value => value.BlockerId, StringComparer.Ordinal)
                .ToArray();
        string duplicate = normalized
            .GroupBy(value => value.BlockerId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (!string.IsNullOrWhiteSpace(duplicate))
        {
            throw new ArgumentException(
                $"{ownerLabel} has duplicate blocker '{duplicate}'.",
                parameterName);
        }

        return normalized;
    }

    internal NarrativeMechanicCatalogCanonicalJsonObject ToCanonicalJson()
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "blockerId", NarrativeMechanicCatalogCanonicalJson.String(BlockerId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "reason", NarrativeMechanicCatalogCanonicalJson.String(Reason)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "resolution", NarrativeMechanicCatalogCanonicalJson.String(Resolution)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "status", NarrativeMechanicCatalogCanonicalJson.String("blocked")));
    }
}

public sealed class NarrativeMechanicCatalogPacketParity
{
    private static readonly string[] RequiredProfileIds =
    {
        "CharacterSkillModuleSelection",
        "FacilityEvolution",
        "EvolutionHistory",
        "AcquiredTrait",
        "Persona"
    };
    private readonly NarrativeMechanicCatalogCanonicalJsonObject[] cases;
    private readonly NarrativeMechanicCatalogParityBlocker[] blockers;

    public NarrativeMechanicCatalogPacketParity(
        IEnumerable<NarrativeMechanicCatalogCanonicalJsonObject> cases,
        IEnumerable<NarrativeMechanicCatalogParityBlocker> blockers)
    {
        this.cases = (cases ?? Array.Empty<NarrativeMechanicCatalogCanonicalJsonObject>())
            .Select(value => value ?? throw new ArgumentException(
                "Packet parity cases cannot contain null.", nameof(cases)))
            .Select(value => (NarrativeMechanicCatalogCanonicalJsonObject)value.Copy())
            .OrderBy(RequireCaseId, StringComparer.Ordinal)
            .ToArray();
        this.blockers = NarrativeMechanicCatalogParityBlocker.NormalizeAndValidate(
            blockers,
            nameof(blockers),
            "Packet parity");

        string duplicateCase = this.cases
            .GroupBy(RequireCaseId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (!string.IsNullOrWhiteSpace(duplicateCase))
        {
            throw new ArgumentException(
                $"Packet parity has duplicate case '{duplicateCase}'.", nameof(cases));
        }

        foreach (string profileId in RequiredProfileIds)
        {
            bool[] outcomes = this.cases
                .Where(value => string.Equals(
                    RequireStringProperty(value, "profileId"),
                    profileId,
                    StringComparison.Ordinal))
                .Select(RequireAcceptedOutcome)
                .Distinct()
                .ToArray();
            if (outcomes.Length != 2 || !outcomes.Contains(true) || !outcomes.Contains(false))
            {
                throw new ArgumentException(
                    $"Packet parity profile '{profileId}' requires at least one accepted and one rejected case.",
                    nameof(cases));
            }
        }
    }

    public IReadOnlyList<NarrativeMechanicCatalogCanonicalJsonObject> Cases =>
        Array.AsReadOnly(cases);

    public IReadOnlyList<NarrativeMechanicCatalogParityBlocker> Blockers =>
        Array.AsReadOnly(blockers);

    private static string RequireCaseId(
        NarrativeMechanicCatalogCanonicalJsonObject value)
    {
        NarrativeMechanicCatalogCanonicalJsonString caseId = value.Properties
            .Where(pair => string.Equals(pair.Key, "caseId", StringComparison.Ordinal))
            .Select(pair => pair.Value as NarrativeMechanicCatalogCanonicalJsonString)
            .SingleOrDefault();
        if (caseId == null || string.IsNullOrWhiteSpace(caseId.Value)
            || !string.Equals(caseId.Value, caseId.Value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Every packet parity case requires a canonical caseId string.", nameof(value));
        }
        return caseId.Value;
    }

    private static string RequireStringProperty(
        NarrativeMechanicCatalogCanonicalJsonObject value,
        string propertyName)
    {
        NarrativeMechanicCatalogCanonicalJsonString property = value.Properties
            .Where(pair => string.Equals(pair.Key, propertyName, StringComparison.Ordinal))
            .Select(pair => pair.Value as NarrativeMechanicCatalogCanonicalJsonString)
            .SingleOrDefault();
        if (property == null || string.IsNullOrWhiteSpace(property.Value)
            || !string.Equals(property.Value, property.Value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Every packet parity case requires a canonical {propertyName} string.",
                nameof(value));
        }
        return property.Value;
    }

    private static bool RequireAcceptedOutcome(
        NarrativeMechanicCatalogCanonicalJsonObject value)
    {
        NarrativeMechanicCatalogCanonicalJsonObject expected = value.Properties
            .Where(pair => string.Equals(pair.Key, "expected", StringComparison.Ordinal))
            .Select(pair => pair.Value as NarrativeMechanicCatalogCanonicalJsonObject)
            .SingleOrDefault();
        NarrativeMechanicCatalogCanonicalJsonBoolean accepted = expected?.Properties
            .Where(pair => string.Equals(pair.Key, "accepted", StringComparison.Ordinal))
            .Select(pair => pair.Value as NarrativeMechanicCatalogCanonicalJsonBoolean)
            .SingleOrDefault();
        if (accepted == null)
        {
            throw new ArgumentException(
                "Every packet parity case requires expected.accepted as a boolean.",
                nameof(value));
        }
        return accepted.Value;
    }
}

public sealed class NarrativeMechanicCatalogTrainingPolicy
{
    private readonly string[] requiredEvidenceGateIds;

    public NarrativeMechanicCatalogTrainingPolicy(
        IEnumerable<string> requiredEvidenceGateIds,
        IEnumerable<NarrativeMechanicCatalogParityBlocker> staticBlockers = null)
    {
        this.requiredEvidenceGateIds = (requiredEvidenceGateIds ?? Array.Empty<string>())
            .Select(value => NarrativeMechanicCatalogExportException.Require(value, nameof(requiredEvidenceGateIds)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string duplicate = this.requiredEvidenceGateIds
            .GroupBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (!string.IsNullOrWhiteSpace(duplicate))
        {
            throw new ArgumentException(
                $"Training policy has duplicate evidence gate '{duplicate}'.",
                nameof(requiredEvidenceGateIds));
        }

        StaticBlockers = Array.AsReadOnly(
            NarrativeMechanicCatalogParityBlocker.NormalizeAndValidate(
                staticBlockers,
                nameof(staticBlockers),
                "Training policy"));
    }

    public IReadOnlyList<string> RequiredEvidenceGateIds =>
        Array.AsReadOnly(requiredEvidenceGateIds);

    public IReadOnlyList<NarrativeMechanicCatalogParityBlocker> StaticBlockers { get; }
}

public sealed class NarrativeMechanicCatalogEvidenceGate
{
    public NarrativeMechanicCatalogEvidenceGate(
        string gateId,
        bool passed,
        string inputDigest,
        string artifactPath,
        string artifactHash,
        string detail)
    {
        GateId = NarrativeMechanicCatalogExportException.Require(gateId, nameof(gateId));
        Passed = passed;
        InputDigest = inputDigest?.Trim() ?? string.Empty;
        ArtifactPath = artifactPath?.Replace('\\', '/').Trim() ?? string.Empty;
        ArtifactHash = artifactHash?.Trim() ?? string.Empty;
        Detail = detail?.Trim() ?? string.Empty;
    }

    public string GateId { get; }
    public bool Passed { get; }
    public string InputDigest { get; }
    public string ArtifactPath { get; }
    public string ArtifactHash { get; }
    public string Detail { get; }

    internal NarrativeMechanicCatalogCanonicalJsonObject ToCanonicalJson()
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "artifactHash", NarrativeMechanicCatalogCanonicalJson.String(ArtifactHash)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "artifactPath", NarrativeMechanicCatalogCanonicalJson.String(ArtifactPath)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "detail", NarrativeMechanicCatalogCanonicalJson.String(Detail)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "gateId", NarrativeMechanicCatalogCanonicalJson.String(GateId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "inputDigest", NarrativeMechanicCatalogCanonicalJson.String(InputDigest)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "passed", NarrativeMechanicCatalogCanonicalJson.Boolean(Passed)));
    }
}

public sealed class NarrativeMechanicCatalogTestEvidence
{
    private readonly NarrativeMechanicCatalogEvidenceGate[] gates;

    public NarrativeMechanicCatalogTestEvidence(
        string capturedInputDigest,
        IEnumerable<NarrativeMechanicCatalogEvidenceGate> gates)
    {
        CapturedInputDigest = capturedInputDigest?.Trim() ?? string.Empty;
        this.gates = (gates ?? Array.Empty<NarrativeMechanicCatalogEvidenceGate>())
            .Select(value => value ?? throw new ArgumentException(
                "Test evidence cannot contain null gates.", nameof(gates)))
            .OrderBy(value => value.GateId, StringComparer.Ordinal)
            .ToArray();
        string duplicate = this.gates
            .GroupBy(value => value.GateId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (!string.IsNullOrWhiteSpace(duplicate))
        {
            throw new ArgumentException(
                $"Test evidence has duplicate gate '{duplicate}'.", nameof(gates));
        }
    }

    public string CapturedInputDigest { get; }
    public IReadOnlyList<NarrativeMechanicCatalogEvidenceGate> Gates =>
        Array.AsReadOnly(gates);
}

/// <summary>
/// Immutable semantic snapshot.  Providers own live asset/runtime reads; the exporter
/// only serializes this value and its captured provenance.
/// </summary>
public sealed class NarrativeMechanicCatalogSnapshot
{
    private readonly string[] relevantPaths;
    private readonly NarrativeMechanicCatalogCanonicalJsonObject noveltyReview;

    public NarrativeMechanicCatalogSnapshot(
        string catalogId,
        NarrativeMechanicCatalogCanonicalJsonObject characterSkill,
        NarrativeMechanicCatalogCanonicalJsonObject facilityEvolution,
        NarrativeMechanicCatalogCanonicalJsonObject equipmentEvolution,
        NarrativeMechanicCatalogCanonicalJsonObject acquiredTrait,
        IEnumerable<string> relevantPaths,
        NarrativeMechanicCatalogTrainingPolicy trainingPolicy,
        NarrativeMechanicCatalogPacketParity packetParity,
        NarrativeMechanicScenarioBundle scenarios = null,
        NarrativeMechanicCatalogCanonicalJsonObject noveltyReview = null,
        NarrativeMechanicScenarioBundle continuityScenarios = null)
    {
        CatalogId = NarrativeMechanicCatalogExportException.Require(catalogId, nameof(catalogId));
        CharacterSkill = CopyRequired(characterSkill, nameof(characterSkill));
        FacilityEvolution = CopyRequired(facilityEvolution, nameof(facilityEvolution));
        EquipmentEvolution = CopyRequired(equipmentEvolution, nameof(equipmentEvolution));
        AcquiredTrait = CopyRequired(acquiredTrait, nameof(acquiredTrait));
        if (CharacterSkill.ContainsKey("catalogHash")
            || FacilityEvolution.ContainsKey("catalogHash")
            || EquipmentEvolution.ContainsKey("catalogHash")
            || AcquiredTrait.ContainsKey("catalogHash"))
        {
            throw new ArgumentException(
                "A catalog section cannot carry the top-level catalogHash field.");
        }

        RequireKeys(
            CharacterSkill,
            "characterSkill",
            "formulaPolicy",
            "rarityBudgets",
            "modules");
        RequireKeys(FacilityEvolution, "facilityEvolution", "recipes");
        RequireKeys(EquipmentEvolution, "equipmentEvolution", "effects");
        RequireKeys(
            AcquiredTrait,
            "acquiredTrait",
            "implementationStatus",
            "gates",
            "maxActive",
            "eraseItemId",
            "eraseDisplayName",
            "modules");

        this.relevantPaths = (relevantPaths ?? Array.Empty<string>())
            .Concat(scenarios?.RelevantPaths ?? Array.Empty<string>())
            .Select(NormalizeRelativePath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (this.relevantPaths.Length == 0)
        {
            throw new ArgumentException(
                "A catalog snapshot must declare the source files it depends on.",
                nameof(relevantPaths));
        }

        TrainingPolicy = trainingPolicy ?? throw new ArgumentNullException(nameof(trainingPolicy));
        PacketParity = packetParity ?? throw new ArgumentNullException(nameof(packetParity));
        Scenarios = scenarios;
        ContinuityScenarios = continuityScenarios ?? scenarios;
        if (noveltyReview != null
            && (noveltyReview.ContainsKey("catalogHash")
                || noveltyReview.ContainsKey("inputDigest")))
        {
            throw new ArgumentException(
                "Snapshot novelty review must not pre-claim package identity.",
                nameof(noveltyReview));
        }
        this.noveltyReview = noveltyReview == null
            ? null
            : (NarrativeMechanicCatalogCanonicalJsonObject)noveltyReview.Copy();
    }

    public string CatalogId { get; }
    public NarrativeMechanicCatalogCanonicalJsonObject CharacterSkill { get; }
    public NarrativeMechanicCatalogCanonicalJsonObject FacilityEvolution { get; }
    public NarrativeMechanicCatalogCanonicalJsonObject EquipmentEvolution { get; }
    public NarrativeMechanicCatalogCanonicalJsonObject AcquiredTrait { get; }
    public NarrativeMechanicCatalogTrainingPolicy TrainingPolicy { get; }
    public NarrativeMechanicCatalogPacketParity PacketParity { get; }
    public NarrativeMechanicScenarioBundle Scenarios { get; }
    public NarrativeMechanicScenarioBundle ContinuityScenarios { get; }
    public NarrativeMechanicCatalogCanonicalJsonObject NoveltyReview =>
        noveltyReview == null
            ? null
            : (NarrativeMechanicCatalogCanonicalJsonObject)noveltyReview.Copy();
    public IReadOnlyList<string> RelevantPaths => Array.AsReadOnly(relevantPaths);

    internal NarrativeMechanicCatalogCanonicalJsonObject BuildCatalogWithoutHash(
        string gameCommit,
        bool trainingEligible)
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "acquiredTrait", AcquiredTrait),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "catalogId", NarrativeMechanicCatalogCanonicalJson.String(CatalogId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "characterSkill", CharacterSkill),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "equipmentEvolution", EquipmentEvolution),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "facilityEvolution", FacilityEvolution),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "schemaVersion", NarrativeMechanicCatalogCanonicalJson.Integer(1)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "source", NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "authority", NarrativeMechanicCatalogCanonicalJson.String("unity-editor-export")),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "gameCommit", NarrativeMechanicCatalogCanonicalJson.String(
                            NarrativeMechanicCatalogExportException.Require(gameCommit, nameof(gameCommit)))))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "trainingEligible", NarrativeMechanicCatalogCanonicalJson.Boolean(trainingEligible)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject CopyRequired(
        NarrativeMechanicCatalogCanonicalJsonObject value,
        string parameterName)
    {
        if (value == null)
        {
            throw new ArgumentNullException(parameterName);
        }

        return (NarrativeMechanicCatalogCanonicalJsonObject)value.Copy();
    }

    private static string NormalizeRelativePath(string value)
    {
        string normalized = NarrativeMechanicCatalogExportException.Require(value, nameof(value))
            .Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal)
            || normalized.Contains(":"))
        {
            throw new ArgumentException(
                $"Catalog provenance path must be repository-relative: '{value}'.",
                nameof(value));
        }

        string[] segments = normalized.Split('/');
        if (segments.Any(segment => segment == ".." || segment.Length == 0))
        {
            throw new ArgumentException(
                $"Catalog provenance path is invalid: '{value}'.", nameof(value));
        }

        return normalized;
    }

    private static void RequireKeys(
        NarrativeMechanicCatalogCanonicalJsonObject section,
        string sectionName,
        params string[] keys)
    {
        foreach (string key in keys)
        {
            if (!section.ContainsKey(key))
            {
                throw new NarrativeMechanicCatalogUnsupportedSourceException(
                    "catalog-section-schema-incomplete",
                    $"Catalog section '{sectionName}' is missing required key '{key}'.");
            }
        }
    }
}

public interface INarrativeMechanicCatalogSnapshotProvider
{
    NarrativeMechanicCatalogSnapshot CaptureSnapshot();
}

public sealed class NarrativeMechanicCatalogExportRequest
{
    private readonly NarrativeMechanicCatalogCanonicalJsonValue rawTestResults;

    public NarrativeMechanicCatalogExportRequest(
        string utcVersion,
        bool requestTrainingEligibility = false,
        NarrativeMechanicCatalogTestEvidence testEvidence = null,
        NarrativeMechanicCatalogCanonicalJsonValue rawTestResults = null)
    {
        UtcVersion = NarrativeMechanicCatalogExportException.Require(utcVersion, nameof(utcVersion));
        RequestTrainingEligibility = requestTrainingEligibility;
        TestEvidence = testEvidence;
        this.rawTestResults = rawTestResults?.Copy();
    }

    public string UtcVersion { get; }
    public bool RequestTrainingEligibility { get; }
    public NarrativeMechanicCatalogTestEvidence TestEvidence { get; }
    public NarrativeMechanicCatalogCanonicalJsonValue RawTestResults => rawTestResults?.Copy();
}

public sealed class NarrativeMechanicCatalogByteComparison
{
    private readonly byte[] firstBytes;
    private readonly byte[] secondBytes;

    internal NarrativeMechanicCatalogByteComparison(
        byte[] first,
        byte[] second,
        string firstInputDigest,
        string secondInputDigest)
    {
        firstBytes = (byte[])(first ?? throw new ArgumentNullException(nameof(first))).Clone();
        secondBytes = (byte[])(second ?? throw new ArgumentNullException(nameof(second))).Clone();
        FirstInputDigest = firstInputDigest ?? string.Empty;
        SecondInputDigest = secondInputDigest ?? string.Empty;
    }

    public byte[] FirstBytes => (byte[])firstBytes.Clone();
    public byte[] SecondBytes => (byte[])secondBytes.Clone();
    public string FirstInputDigest { get; }
    public string SecondInputDigest { get; }
    public bool IsByteIdentical => firstBytes.SequenceEqual(secondBytes)
        && string.Equals(FirstInputDigest, SecondInputDigest, StringComparison.Ordinal);

    public void RequireByteIdentical()
    {
        if (!IsByteIdentical)
        {
            throw new NarrativeMechanicCatalogExportException(
                "independent-export-mismatch",
                "Two independent package captures produced different bytes or input digests.");
        }
    }
}

public sealed class NarrativeMechanicCatalogExportResult
{
    internal NarrativeMechanicCatalogExportResult(
        string directoryPath,
        string catalogHash,
        string inputDigest,
        string uncommittedSourceHash,
        bool trainingEligible)
    {
        DirectoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));
        CatalogHash = catalogHash ?? throw new ArgumentNullException(nameof(catalogHash));
        InputDigest = inputDigest ?? throw new ArgumentNullException(nameof(inputDigest));
        UncommittedSourceHash = uncommittedSourceHash
            ?? throw new ArgumentNullException(nameof(uncommittedSourceHash));
        TrainingEligible = trainingEligible;
    }

    public string DirectoryPath { get; }
    public string CatalogHash { get; }
    public string InputDigest { get; }
    public string UncommittedSourceHash { get; }
    public bool TrainingEligible { get; }
}
/// <summary>
/// Builds and self-validates representative packet parity for narrative profiles
/// that do not depend on the acquired-trait aggregate fixture.
/// </summary>
public static class NarrativeMechanicCatalogExtendedParityCases
{
    private static readonly string[] FacilityEvolutionResponseKeys =
    {
        "proposalIds", "mutationTags", "reasons", "reasons[].proposalId",
        "reasons[].reason", "flavorText", "confidence"
    };
    private static readonly string[] EquipmentChoiceResponseKeys =
    {
        "selectedIndex"
    };
    private static readonly string[] EvolutionHistoryResponseKeys =
    {
        "requestKey", "targetPersistentId", "nodeId", "parentNodeId", "effectId",
        "effectBudget", "evidenceIds", "displayName", "description", "historyReason"
    };
    private static readonly string[] PersonaResponseKeys =
    {
        "personaName", "flavorText"
    };

    public static List<NarrativeMechanicCatalogCanonicalJsonObject>
        BuildFacilityEvolutionParityCases()
    {
        FacilityEvolutionRecipeSO recipe = FindAssetPaths<FacilityEvolutionRecipeSO>()
            .Select(path => AssetDatabase.LoadAssetAtPath<FacilityEvolutionRecipeSO>(path))
            .Where(value => value != null)
            .OrderBy(value => value.EffectiveId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (recipe == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "facility-evolution-parity-recipe-missing",
                "Facility evolution parity requires an authored recipe.");
        }

        string validId = RequireAuthoredText(
            recipe.EffectiveId,
            "facility evolution recipe ID",
            AssetDatabase.GetAssetPath(recipe));
        string[] validIds = { validId };
        string[] allowedMutationTags = (recipe.allowedMutationTags ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string validResponse = FacilityEvolutionResponseJson(validId);
        RequireFacilityEvolutionResponseValidation(
            validResponse,
            validIds,
            allowedMutationTags,
            expectedAccepted: true,
            expectedIssueCode: FacilityEvolutionProposalRejectionKind.None,
            label: "valid legal candidate");

        const string ForgedId = "facility-evolution:catalog-parity-forged";
        string rejectedResponse = FacilityEvolutionResponseJson(ForgedId);
        RequireFacilityEvolutionResponseValidation(
            rejectedResponse,
            validIds,
            allowedMutationTags,
            expectedAccepted: false,
            expectedIssueCode: FacilityEvolutionProposalRejectionKind.IllegalProposalId,
            label: "forged candidate ID");

        NarrativeMechanicCatalogCanonicalJsonObject request =
            FacilityEvolutionRequestJson(recipe, validIds, allowedMutationTags);
        return new List<NarrativeMechanicCatalogCanonicalJsonObject>
        {
            BuildParityCase(
                LocalLlmRequestProfiles.FacilityEvolutionLegacyV2.Id,
                "facility-evolution-reject-forged-proposal-id",
                "FacilityEvolutionProposalJsonDto.TryCreateRuntimeProposal",
                request,
                rejectedResponse,
                accepted: false,
                expectedIssueCode: FacilityEvolutionProposalRejectionKind.IllegalProposalId.ToString(),
                responseKeys: FacilityEvolutionResponseKeys),
            BuildParityCase(
                LocalLlmRequestProfiles.FacilityEvolutionLegacyV2.Id,
                "facility-evolution-valid-response",
                "FacilityEvolutionProposalJsonDto.TryCreateRuntimeProposal",
                request,
                validResponse,
                accepted: true,
                expectedIssueCode: FacilityEvolutionProposalRejectionKind.None.ToString(),
                responseKeys: FacilityEvolutionResponseKeys)
        };
    }

    private static void RequireFacilityEvolutionResponseValidation(
        string responseJson,
        IReadOnlyCollection<string> validCandidateIds,
        IReadOnlyCollection<string> validMutationTags,
        bool expectedAccepted,
        FacilityEvolutionProposalRejectionKind expectedIssueCode,
        string label)
    {
        bool accepted = LlmJsonResponseParser.TryParse(
            LocalLlmRequestProfiles.FacilityEvolutionLegacyV2.Id,
            responseJson,
            out FacilityEvolutionProposalJsonDto payload,
            out string error);
        FacilityEvolutionProposalRejectionKind issueCode = accepted
            ? FacilityEvolutionProposalRejectionKind.None
            : FacilityEvolutionProposalRejectionKind.InvalidPayload;
        if (accepted)
        {
            accepted = payload.TryCreateRuntimeProposal(
                "catalog-parity-facility",
                validCandidateIds,
                validMutationTags,
                new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal),
                null,
                out _,
                out issueCode,
                out error);
        }
        if (accepted != expectedAccepted || issueCode != expectedIssueCode)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "facility-evolution-parity-unexpected",
                $"Facility evolution {label} produced {accepted}/{issueCode}, expected {expectedAccepted}/{expectedIssueCode}: {error}");
        }
    }

    public static List<NarrativeMechanicCatalogCanonicalJsonObject>
        BuildEquipmentChoiceParityCases()
    {
        EquipmentHistoricalEffectDefinition[] candidates = EquipmentHistoricalEffectCatalog.All
            .Where(value => value != null)
            .OrderBy(value => value.EffectId, StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        if (candidates.Length != 2)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "equipment-choice-parity-candidate-count",
                $"Equipment choice parity requires two authored effects; found {candidates.Length}.");
        }
        const int CandidateCount = 2;
        string grammar = EquipmentChoiceGrammarCatalog.Require(CandidateCount);
        string validResponse = EquipmentChoiceResponseJson(0);
        if (!EquipmentChoiceResultParser.TryParse(
                validResponse,
                CandidateCount,
                out int selectedIndex,
                out string validError)
            || selectedIndex != 0)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "equipment-choice-parity-valid-rejected",
                $"EquipmentChoiceResultParser rejected the valid parity response: {validError}");
        }
        string rejectedResponse = EquipmentChoiceResponseJson(CandidateCount);
        if (EquipmentChoiceResultParser.TryParse(
                rejectedResponse,
                CandidateCount,
                out _,
                out string rejectedError)
            || !string.Equals(
                rejectedError,
                "EquipmentChoice.SelectedIndexOutOfRange",
                StringComparison.Ordinal))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "equipment-choice-parity-invalid-accepted",
                $"EquipmentChoiceResultParser did not reject index {CandidateCount} with its current range error: {rejectedError}");
        }

        NarrativeMechanicCatalogCanonicalJsonObject request =
            EquipmentChoiceRequestJson(candidates, grammar);
        return new List<NarrativeMechanicCatalogCanonicalJsonObject>
        {
            BuildParityCase(
                LocalLlmRequestProfiles.EquipmentChoiceLegacyV2.Id,
                "equipment-choice-reject-out-of-range-index",
                "EquipmentChoiceResultParser.TryParse",
                request,
                rejectedResponse,
                accepted: false,
                expectedIssueCode: "EquipmentChoice.SelectedIndexOutOfRange",
                responseKeys: EquipmentChoiceResponseKeys),
            BuildParityCase(
                LocalLlmRequestProfiles.EquipmentChoiceLegacyV2.Id,
                "equipment-choice-valid-index-zero",
                "EquipmentChoiceResultParser.TryParse",
                request,
                validResponse,
                accepted: true,
                expectedIssueCode: "None",
                responseKeys: EquipmentChoiceResponseKeys)
        };
    }

    public static List<NarrativeMechanicCatalogCanonicalJsonObject>
        BuildEvolutionHistoryParityCases()
    {
        EquipmentHistoricalEffectDefinition[] effects = EquipmentHistoricalEffectCatalog.All
            .Where(value => value != null)
            .OrderBy(value => value.EffectId, StringComparer.Ordinal)
            .Take(3)
            .ToArray();
        if (effects.Length == 0)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "evolution-history-parity-effect-missing",
                "Evolution history parity requires an authored equipment historical effect.");
        }
        EvolutionNode node = new EvolutionNode
        {
            nodeId = "catalog-parity-node",
            parentNodeId = "catalog-parity-parent",
            effectId = effects[0].EffectId,
            generation = 1,
            legalCandidateEffectIds = effects.Select(value => value.EffectId).ToList(),
            selectedCandidateIndex = 0
        };
        UsageLedger ledger = new UsageLedger
        {
            currentGenerationEvents = new List<UsageLedgerEvent>
            {
                new UsageLedgerEvent
                {
                    evidenceId = "catalog-parity-evidence",
                    eventId = "catalog-parity-event",
                    actorId = "catalog-parity-owner",
                    targetId = "catalog-parity-target",
                    amount = 1f,
                    generation = 1,
                    sequence = 1,
                    sourceTags = new List<string> { "catalog-parity" }
                }
            }
        };
        EvolutionNarrativeRequestSnapshot request = EvolutionNarrativeRequestFactory.Create(
            EvolutionNarrativeTargetKind.Equipment,
            "catalog-parity-equipment",
            node,
            "catalog-parity-history-hash",
            ledger,
            effectBudget: 1);
        string validResponse = EvolutionHistoryResponseJson(
            request,
            request.effectBudget);
        RequireEvolutionHistoryResponseValidation(
            request,
            validResponse,
            expectedAccepted: true,
            expectedError: string.Empty,
            label: "exact echo");

        string rejectedResponse = EvolutionHistoryResponseJson(
            request,
            request.effectBudget + 1);
        const string SnapshotMismatch =
            "Evolution narrative identifiers or effect budget changed.";
        RequireEvolutionHistoryResponseValidation(
            request,
            rejectedResponse,
            expectedAccepted: false,
            expectedError: SnapshotMismatch,
            label: "changed effect budget");

        NarrativeMechanicCatalogCanonicalJsonObject requestJson =
            EvolutionHistoryRequestJson(request);
        return new List<NarrativeMechanicCatalogCanonicalJsonObject>
        {
            BuildParityCase(
                LocalLlmRequestProfiles.EvolutionHistoryLegacyV2.Id,
                "evolution-history-reject-changed-effect-budget",
                "EvolutionNarrativeRequestFactory.Create+EvolutionNarrativeResponseValidator.Validate",
                requestJson,
                rejectedResponse,
                accepted: false,
                expectedIssueCode: SnapshotMismatch,
                responseKeys: EvolutionHistoryResponseKeys),
            BuildParityCase(
                LocalLlmRequestProfiles.EvolutionHistoryLegacyV2.Id,
                "evolution-history-valid-exact-echo",
                "EvolutionNarrativeRequestFactory.Create+EvolutionNarrativeResponseValidator.Validate",
                requestJson,
                validResponse,
                accepted: true,
                expectedIssueCode: "None",
                responseKeys: EvolutionHistoryResponseKeys)
        };
    }

    private static void RequireEvolutionHistoryResponseValidation(
        EvolutionNarrativeRequestSnapshot request,
        string responseJson,
        bool expectedAccepted,
        string expectedError,
        string label)
    {
        bool accepted = LlmJsonResponseParser.TryParse(
            LocalLlmRequestProfiles.EvolutionHistoryLegacyV2.Id,
            responseJson,
            out EvolutionHistoryNarrativeResponseDto payload,
            out string error);
        if (accepted)
        {
            accepted = EvolutionNarrativeResponseValidator.Validate(
                request,
                payload,
                out error);
        }
        if (accepted != expectedAccepted
            || !string.Equals(error, expectedError, StringComparison.Ordinal))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "evolution-history-parity-unexpected",
                $"Evolution history {label} produced {accepted}/'{error}', expected {expectedAccepted}/'{expectedError}'.");
        }
    }

    public static List<NarrativeMechanicCatalogCanonicalJsonObject>
        BuildPersonaParityCases()
    {
        string validResponse = PersonaResponseJson(includeExtraMechanicalKey: false);
        if (!LlmJsonResponseParser.TryParse(
                LocalLlmRequestProfiles.Persona.Id,
                validResponse,
                out CustomerPersonaJsonDto _,
                out string validError))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "persona-parity-valid-rejected",
                $"CustomerPersonaJsonDto rejected the valid exact response: {validError}");
        }
        string rejectedResponse = PersonaResponseJson(includeExtraMechanicalKey: true);
        if (LlmJsonResponseParser.TryParse(
                LocalLlmRequestProfiles.Persona.Id,
                rejectedResponse,
                out CustomerPersonaJsonDto _,
                out _))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "persona-parity-extra-key-accepted",
                "The Persona exact-key contract accepted selfCareMultiplier.");
        }
        NarrativeMechanicCatalogCanonicalJsonObject request =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "requestKey", NarrativeMechanicCatalogCanonicalJson.String(
                        "catalog-parity:persona")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "responseContract", StringArray(
                        PersonaResponseKeys,
                        "persona response keys",
                        "catalog-parity:persona")));
        return new List<NarrativeMechanicCatalogCanonicalJsonObject>
        {
            BuildParityCase(
                LocalLlmRequestProfiles.Persona.Id,
                "persona-reject-extra-mechanical-key",
                "LlmJsonResponseParser.TryParse<CustomerPersonaJsonDto>",
                request,
                rejectedResponse,
                accepted: false,
                expectedIssueCode: "NarrativeExactKeyContract.ExtraKey",
                responseKeys: PersonaResponseKeys),
            BuildParityCase(
                LocalLlmRequestProfiles.Persona.Id,
                "persona-valid-exact-response",
                "LlmJsonResponseParser.TryParse<CustomerPersonaJsonDto>",
                request,
                validResponse,
                accepted: true,
                expectedIssueCode: "None",
                responseKeys: PersonaResponseKeys)
        };
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject FacilityEvolutionRequestJson(
        FacilityEvolutionRecipeSO recipe,
        IReadOnlyCollection<string> validCandidateIds,
        IReadOnlyCollection<string> allowedMutationTags)
    {
        NarrativeMechanicCatalogCanonicalJsonObject recipeJson =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "allowedMutationTags", StringArray(
                        recipe.allowedMutationTags ?? Array.Empty<string>(),
                        "allowed mutation tags",
                        recipe.EffectiveId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "identityPressureWeights", FacilityEvolutionValuesJson(
                        recipe.identityPressureWeights,
                        "identity pressure weights",
                        recipe.EffectiveId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "minimumIdentityScoreDecimal", NarrativeMechanicCatalogCanonicalJson.String(
                        CanonicalFloat(recipe.minimumIdentityScore, recipe.EffectiveId))),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "recipeId", NarrativeMechanicCatalogCanonicalJson.String(recipe.EffectiveId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "requiredRecordTokens", FacilityEvolutionTokenRequirementsJson(
                        recipe.requiredRecordTokens,
                        recipe.EffectiveId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "requiredRoomMetrics", FacilityEvolutionMetricRequirementsJson(
                        recipe.requiredRoomMetrics,
                        "required room metrics",
                        recipe.EffectiveId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "requiredRoomScores", FacilityEvolutionMetricRequirementsJson(
                        recipe.requiredRoomScores,
                        "required room scores",
                        recipe.EffectiveId)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "requiredRoomTags", StringArray(
                        recipe.requiredRoomTags ?? Array.Empty<string>(),
                        "required room tags",
                        recipe.EffectiveId)));
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "allowedMutationTags", StringArray(
                    allowedMutationTags,
                    "allowed mutation tags",
                    "catalog-parity:facility-evolution")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "candidateIds", StringArray(
                    validCandidateIds,
                    "candidate IDs",
                    "catalog-parity:facility-evolution")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "candidateRecipes", NarrativeMechanicCatalogCanonicalJson.Array(recipeJson)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "requestKey", NarrativeMechanicCatalogCanonicalJson.String(
                    "catalog-parity:facility-evolution")));
    }

    private static NarrativeMechanicCatalogCanonicalJsonArray
        FacilityEvolutionMetricRequirementsJson(
            IEnumerable<FacilityEvolutionMetricRequirement> requirements,
            string label,
            string sourceId)
    {
        return new NarrativeMechanicCatalogCanonicalJsonArray(
            (requirements ?? Array.Empty<FacilityEvolutionMetricRequirement>())
                .Where(value => !string.IsNullOrWhiteSpace(value.key))
                .OrderBy(value => value.key, StringComparer.Ordinal)
                .Select(value =>
                    (NarrativeMechanicCatalogCanonicalJsonValue)
                    NarrativeMechanicCatalogCanonicalJson.Object(
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "key", NarrativeMechanicCatalogCanonicalJson.String(
                                RequireAuthoredText(value.key, label, sourceId))),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "maximumDecimal", NarrativeMechanicCatalogCanonicalJson.String(
                                CanonicalFloat(value.maxValue, sourceId + ":" + value.key))),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "minimumDecimal", NarrativeMechanicCatalogCanonicalJson.String(
                                CanonicalFloat(value.minValue, sourceId + ":" + value.key))),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "requireMaximum", NarrativeMechanicCatalogCanonicalJson.Boolean(
                                value.requireMax)),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "requireMinimum", NarrativeMechanicCatalogCanonicalJson.Boolean(
                                value.requireMin)))));
    }

    private static NarrativeMechanicCatalogCanonicalJsonArray
        FacilityEvolutionTokenRequirementsJson(
            IEnumerable<FacilityEvolutionTokenRequirement> requirements,
            string sourceId)
    {
        return new NarrativeMechanicCatalogCanonicalJsonArray(
            (requirements ?? Array.Empty<FacilityEvolutionTokenRequirement>())
                .Where(value => !string.IsNullOrWhiteSpace(value.key))
                .OrderBy(value => value.key, StringComparer.Ordinal)
                .Select(value =>
                    (NarrativeMechanicCatalogCanonicalJsonValue)
                    NarrativeMechanicCatalogCanonicalJson.Object(
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "key", NarrativeMechanicCatalogCanonicalJson.String(
                                RequireAuthoredText(
                                    value.key,
                                    "required record token",
                                    sourceId))),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "minimumCount", NarrativeMechanicCatalogCanonicalJson.Integer(
                                value.minCount)))));
    }

    private static NarrativeMechanicCatalogCanonicalJsonArray FacilityEvolutionValuesJson(
        IEnumerable<FacilityEvolutionValue> values,
        string label,
        string sourceId)
    {
        return new NarrativeMechanicCatalogCanonicalJsonArray(
            (values ?? Array.Empty<FacilityEvolutionValue>())
                .Where(value => !string.IsNullOrWhiteSpace(value.key))
                .OrderBy(value => value.key, StringComparer.Ordinal)
                .Select(value =>
                    (NarrativeMechanicCatalogCanonicalJsonValue)
                    NarrativeMechanicCatalogCanonicalJson.Object(
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "key", NarrativeMechanicCatalogCanonicalJson.String(
                                RequireAuthoredText(value.key, label, sourceId))),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "valueDecimal", NarrativeMechanicCatalogCanonicalJson.String(
                                CanonicalFloat(value.value, sourceId + ":" + value.key))))));
    }

    private static string FacilityEvolutionResponseJson(string proposalId) =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "confidence", NarrativeMechanicCatalogCanonicalJson.Integer(1)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "flavorText", NarrativeMechanicCatalogCanonicalJson.String(
                    "검증 가능한 시설 계보 제안입니다.")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "mutationTags", NarrativeMechanicCatalogCanonicalJson.Array()),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "proposalIds", StringArray(
                    new[] { proposalId },
                    "facility proposal IDs",
                    "catalog-parity:facility-evolution")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "reasons", NarrativeMechanicCatalogCanonicalJson.Array(
                    NarrativeMechanicCatalogCanonicalJson.Object(
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "proposalId", NarrativeMechanicCatalogCanonicalJson.String(
                                proposalId)),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "reason", NarrativeMechanicCatalogCanonicalJson.String(
                                "현재 시설 기록과 일치하는 검증 제안입니다.")))))).ToCanonicalString();

    private static NarrativeMechanicCatalogCanonicalJsonObject EquipmentChoiceRequestJson(
        IReadOnlyList<EquipmentHistoricalEffectDefinition> candidates,
        string grammar)
    {
        List<NarrativeMechanicCatalogCanonicalJsonValue> candidateValues = candidates
            .Select((value, index) =>
                (NarrativeMechanicCatalogCanonicalJsonValue)
                NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "displayName", NarrativeMechanicCatalogCanonicalJson.String(
                            RequireAuthoredText(
                                value.DisplayName,
                                "equipment effect display name",
                                value.EffectId))),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "effectId", NarrativeMechanicCatalogCanonicalJson.String(value.EffectId)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "index", NarrativeMechanicCatalogCanonicalJson.Integer(index))))
            .ToList();
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "candidateCount", NarrativeMechanicCatalogCanonicalJson.Integer(candidates.Count)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "candidates", new NarrativeMechanicCatalogCanonicalJsonArray(candidateValues)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "grammar", NarrativeMechanicCatalogCanonicalJson.String(grammar)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "requestKey", NarrativeMechanicCatalogCanonicalJson.String(
                    "catalog-parity:equipment-choice")));
    }

    private static string EquipmentChoiceResponseJson(int selectedIndex) =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "selectedIndex", NarrativeMechanicCatalogCanonicalJson.Integer(
                    selectedIndex))).ToCanonicalString();

    private static NarrativeMechanicCatalogCanonicalJsonObject EvolutionHistoryRequestJson(
        EvolutionNarrativeRequestSnapshot request)
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "effectBudget", NarrativeMechanicCatalogCanonicalJson.Integer(request.effectBudget)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "effectId", NarrativeMechanicCatalogCanonicalJson.String(request.effectId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "evidenceIds", StringArray(request.evidenceIds, "evidence IDs", request.requestKey)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "generation", NarrativeMechanicCatalogCanonicalJson.Integer(request.generation)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "historyHash", NarrativeMechanicCatalogCanonicalJson.String(request.historyHash)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "legalCandidateEffectIds", StringArray(
                    request.legalCandidateEffectIds,
                    "legal candidate effect IDs",
                    request.requestKey)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "nodeId", NarrativeMechanicCatalogCanonicalJson.String(request.nodeId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "parentNodeId", NarrativeMechanicCatalogCanonicalJson.String(request.parentNodeId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "participantIds", StringArray(
                    request.participantIds,
                    "participant IDs",
                    request.requestKey)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "requestKey", NarrativeMechanicCatalogCanonicalJson.String(request.requestKey)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "sourceTags", StringArray(request.sourceTags, "source tags", request.requestKey)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "targetKind", NarrativeMechanicCatalogCanonicalJson.String(request.targetKind.ToString())),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "targetPersistentId", NarrativeMechanicCatalogCanonicalJson.String(
                    request.targetPersistentId)));
    }

    private static string EvolutionHistoryResponseJson(
        EvolutionNarrativeRequestSnapshot request,
        int effectBudget)
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "description", NarrativeMechanicCatalogCanonicalJson.String(
                    "확정된 효과를 바꾸지 않는 검증용 역사입니다.")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "displayName", NarrativeMechanicCatalogCanonicalJson.String("검증의 계보")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "effectBudget", NarrativeMechanicCatalogCanonicalJson.Integer(effectBudget)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "effectId", NarrativeMechanicCatalogCanonicalJson.String(request.effectId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "evidenceIds", StringArray(request.evidenceIds, "evidence IDs", request.requestKey)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "historyReason", NarrativeMechanicCatalogCanonicalJson.String(
                    "동일한 증거 묶음을 정확히 반영했습니다.")),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "nodeId", NarrativeMechanicCatalogCanonicalJson.String(request.nodeId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "parentNodeId", NarrativeMechanicCatalogCanonicalJson.String(request.parentNodeId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "requestKey", NarrativeMechanicCatalogCanonicalJson.String(request.requestKey)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "targetPersistentId", NarrativeMechanicCatalogCanonicalJson.String(
                    request.targetPersistentId))).ToCanonicalString();
    }

    private static string PersonaResponseJson(bool includeExtraMechanicalKey)
    {
        List<KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>> properties =
            new List<KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>>
            {
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "flavorText", NarrativeMechanicCatalogCanonicalJson.String(
                        "조용히 주변을 살피는 손님입니다.")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "personaName", NarrativeMechanicCatalogCanonicalJson.String("신중한 손님"))
            };
        if (includeExtraMechanicalKey)
        {
            properties.Add(NarrativeMechanicCatalogCanonicalJson.Property(
                "selfCareMultiplier", NarrativeMechanicCatalogCanonicalJson.Integer(1)));
        }
        return new NarrativeMechanicCatalogCanonicalJsonObject(properties)
            .ToCanonicalString();
    }


    private static NarrativeMechanicCatalogCanonicalJsonObject BuildParityCase(
        string profileId,
        string caseId,
        string authority,
        NarrativeMechanicCatalogCanonicalJsonObject request,
        string responseJson,
        bool accepted,
        string expectedIssueCode,
        IEnumerable<string> responseKeys)
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            NarrativeMechanicCatalogCanonicalJson.Property(
                "authority", NarrativeMechanicCatalogCanonicalJson.String(authority)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "caseId", NarrativeMechanicCatalogCanonicalJson.String(caseId)),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "expected", NarrativeMechanicCatalogCanonicalJson.Object(
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "accepted", NarrativeMechanicCatalogCanonicalJson.Boolean(accepted)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "issueCode", NarrativeMechanicCatalogCanonicalJson.String(expectedIssueCode)),
                    NarrativeMechanicCatalogCanonicalJson.Property(
                        "responseKeys", StringArray(
                            responseKeys,
                            "response keys",
                            caseId)))),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "profileId", NarrativeMechanicCatalogCanonicalJson.String(
                    NarrativeMechanicCatalogExportException.Require(
                        profileId,
                        nameof(profileId)))),
            NarrativeMechanicCatalogCanonicalJson.Property("request", request),
            NarrativeMechanicCatalogCanonicalJson.Property(
                "responseJson", NarrativeMechanicCatalogCanonicalJson.String(responseJson)));
    }

    private static string[] FindAssetPaths<T>() where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static NarrativeMechanicCatalogCanonicalJsonArray StringArray(
        IEnumerable<string> values,
        string sourceKind,
        string sourceId)
    {
        if (values == null)
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                $"{sourceKind.Replace(' ', '-')}-missing",
                $"'{sourceId}' has no authored {sourceKind} collection.");
        }
        return new NarrativeMechanicCatalogCanonicalJsonArray(values.Select(value =>
            (NarrativeMechanicCatalogCanonicalJsonValue)
            NarrativeMechanicCatalogCanonicalJson.String(
                RequireAuthoredText(value, sourceKind, sourceId))));
    }

    private static string RequireAuthoredText(string value, string field, string sourceId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "authored-text-missing",
                $"'{sourceId}' has no authored {field}.");
        }
        return value;
    }

    private static string CanonicalFloat(float value, string sourceId)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new NarrativeMechanicCatalogUnsupportedSourceException(
                "catalog-parity-value-non-finite",
                $"Parity source '{sourceId}' has a non-finite value.");
        }
        return value.ToString("R", CultureInfo.InvariantCulture);
    }
}

#endif
