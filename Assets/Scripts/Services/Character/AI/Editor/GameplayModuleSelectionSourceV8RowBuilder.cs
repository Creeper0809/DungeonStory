#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

/// <summary>
/// Closed source-v8 serializer. It accepts only C#-validated evidence bindings,
/// authority requests and exact live-wire capture; it does not infer facts or
/// mechanics from prose.
/// </summary>
internal static class GameplayModuleSelectionSourceV8RowBuilder
{
    internal const int SourceSchemaVersion = 8;
    internal const string TaskId = "gameplay-module-selection";

    internal static JObject BuildEvidence(
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> bindings)
    {
        GameplayOutcomeEvidenceBindingSnapshot[] values = (bindings
                ?? throw new ArgumentNullException(nameof(bindings)))
            .Select(value => value?.Clone()
                ?? throw new InvalidOperationException(
                    "Source-v8 evidence cannot contain null bindings."))
            .ToArray();
        if (values.Length == 0)
            throw new InvalidOperationException(
                "Source-v8 evidence requires at least one C# binding.");
        foreach (GameplayOutcomeEvidenceBindingSnapshot value in values)
        {
            string failureCode = string.Empty;
            if (value.schemaVersion != GameplayOutcomeEvidenceBindingAuthority
                    .CurrentSchemaVersion
                || !GameplayOutcomeEvidenceBindingAuthority.TryValidate(
                    value, out failureCode))
            {
                throw new InvalidOperationException(
                    "Source-v8 evidence binding was rejected: " + failureCode);
            }
        }

        JArray serialized = new(values.Select(BindingJson));
        JArray publicFacts = new(values.Select(value => new JObject
        {
            ["factId"] = value.publicFactId,
            ["text"] = value.canonicalFactText
        }));
        JObject evidence = new()
        {
            ["schemaVersion"] = 2,
            ["bindings"] = serialized,
            ["publicFacts"] = publicFacts,
            ["visibleFactIds"] = new JArray(values.Select(value =>
                value.publicFactId))
        };
        evidence["evidenceHash"] = GameplayModuleSelectionCanonicalJson.Hash(evidence);
        return evidence;
    }

    internal static JObject BuildRow(
        int formulaVersion,
        string catalogSha256,
        string gameCommit,
        string dirtySourceSha256,
        string contentSha256,
        string profileId,
        string targetPersistentId,
        JObject executionBinding,
        IReadOnlyList<GameplayOutcomeEvidenceBindingSnapshot> evidenceBindings,
        JObject liveRequest,
        IReadOnlyList<JObject> authorityRequests,
        JObject ownerSnapshot,
        string applicationContractId,
        string applicationContractSha256)
    {
        if (formulaVersion < 2)
            throw new ArgumentOutOfRangeException(nameof(formulaVersion));
        JObject execution = RequireClosedClone(
            executionBinding,
            nameof(executionBinding),
            "sourcePackageId", "sourcePackageSha256", "campaignId", "ownerId",
            "ownerCheckpointId", "ownerCheckpointSha256", "ownerRevision",
            "episodeId", "actionIds", "outcomeIds", "compactedMemoryIds",
            "captureDay", "triggerId", "controlledSimulation",
            "restoredFromCheckpoint");
        JObject wire = RequireClosedClone(
            liveRequest,
            nameof(liveRequest),
            "profileId", "mode", "logicalRequestId", "transportAttemptId",
            "candidateCount", "selectionIds", "prompt", "promptSha256",
            "wireBodyJson", "wireBodySha256", "schemaVersion", "schemaSha256",
            "constraints");
        JObject[] requests = (authorityRequests
                ?? throw new ArgumentNullException(nameof(authorityRequests)))
            .Select(value => RequireClone(value, nameof(authorityRequests)))
            .ToArray();
        if (requests.Length == 0)
            throw new InvalidOperationException(
                "Source-v8 authority sidecar requires selection requests.");
        JObject owner = RequireClone(ownerSnapshot, nameof(ownerSnapshot));
        string profile = RequireProfile(profileId);
        string targetId = RequireText(targetPersistentId, nameof(targetPersistentId));
        if (!GameplayOutcomeStableIdSyntax.IsValid(targetId))
            throw new ArgumentException("A stable target identity is required.",
                nameof(targetPersistentId));
        string wireProfile = (string)wire["profileId"];
        JArray wireSelectionIds = wire["selectionIds"] as JArray;
        string[] authoritySelectionIds = requests.Select(value =>
                RequireText((string)value["selectionId"], "selectionId"))
            .ToArray();
        if (!string.Equals(wireProfile, profile, StringComparison.Ordinal)
            || wireSelectionIds == null
            || wireSelectionIds.Count != authoritySelectionIds.Length
            || wire["candidateCount"]?.Value<int>() != authoritySelectionIds.Length
            || !wireSelectionIds.Values<string>().SequenceEqual(
                authoritySelectionIds, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The live wire disagrees with its authority selection requests.");
        }
        JObject evidence = BuildEvidence(evidenceBindings);
        ValidateExecutionEvidenceBinding(execution, evidence);
        HashSet<string> visibleFacts = new(
            ((JArray)evidence["visibleFactIds"]).Values<string>(),
            StringComparer.Ordinal);
        foreach (JObject request in requests)
        {
            if (request["evidenceFactIds"] is not JArray requestEvidence
                || requestEvidence.Count == 0
                || requestEvidence.Values<string>().Any(value =>
                    !visibleFacts.Contains(value)))
            {
                throw new InvalidOperationException(
                    "An authority request exposes evidence outside the bound C# set.");
            }
        }
        string authorityHash = GameplayModuleSelectionCanonicalJson.Hash(
            new JArray(requests));
        string ownerHash = GameplayModuleSelectionCanonicalJson.Hash(owner);
        return new JObject
        {
            ["schemaVersion"] = SourceSchemaVersion,
            ["taskId"] = TaskId,
            ["formulaVersion"] = formulaVersion,
            ["catalogSha256"] = RequireSha256(catalogSha256, nameof(catalogSha256)),
            ["gameCommit"] = RequireCommit(gameCommit, nameof(gameCommit)),
            ["dirtySourceSha256"] = RequireSha256(
                dirtySourceSha256, nameof(dirtySourceSha256)),
            ["contentSha256"] = RequireSha256(contentSha256, nameof(contentSha256)),
            ["profileId"] = profile,
            ["targetPersistentId"] = targetId,
            ["executionBinding"] = execution,
            ["evidence"] = evidence,
            ["liveRequest"] = wire,
            ["authoritySidecar"] = new JObject
            {
                ["selectionRequests"] = new JArray(requests),
                ["ownerSnapshotSha256"] = ownerHash,
                ["authoritySnapshotSha256"] = authorityHash,
                ["applicationContractId"] = RequireText(
                    applicationContractId, nameof(applicationContractId)),
                ["applicationContractSha256"] = RequireSha256(
                    applicationContractSha256,
                    nameof(applicationContractSha256))
            }
        };
    }

    private static void ValidateExecutionEvidenceBinding(
        JObject execution,
        JObject evidence)
    {
        string[] executionOutcomeIds = ((JArray)execution["outcomeIds"])
            .Values<string>()
            .ToArray();
        string[] executionMemoryIds = ((JArray)execution["compactedMemoryIds"])
            .Values<string>()
            .ToArray();
        JObject[] bindings = ((JArray)evidence["bindings"])
            .OfType<JObject>()
            .ToArray();
        string[] exactIds = bindings
            .Where(value => string.Equals(
                (string)value["sourceKind"],
                "exact",
                StringComparison.Ordinal))
            .Select(value => (string)value["outcomeRunId"] + "/"
                + value["outcomeSequence"].Value<long>()
                    .ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        string[] compactedIds = bindings
            .Where(value => string.Equals(
                (string)value["sourceKind"],
                "compacted",
                StringComparison.Ordinal))
            .Select(value => (string)value["compactedMemoryId"])
            .ToArray();
        string[] parentIds = bindings
            .Where(value => string.Equals(
                (string)value["sourceKind"],
                "exact",
                StringComparison.Ordinal))
            .Select(value => (string)value["parentOutcomeId"] ?? string.Empty)
            .Where(value => value.Length > 0)
            .ToArray();
        if (executionOutcomeIds.Length != executionOutcomeIds
                .Distinct(StringComparer.Ordinal).Count()
            || executionMemoryIds.Length != executionMemoryIds
                .Distinct(StringComparer.Ordinal).Count()
            || !executionOutcomeIds.ToHashSet(StringComparer.Ordinal)
                .SetEquals(exactIds)
            || !executionMemoryIds.ToHashSet(StringComparer.Ordinal)
                .SetEquals(compactedIds)
            || parentIds.Any(value => !exactIds.Contains(
                value,
                StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                "Execution evidence identities are not exact, causally closed "
                + "GameplayOutcomeId/compacted-memory bindings.");
        }
    }

    private static JObject BindingJson(GameplayOutcomeEvidenceBindingSnapshot value)
    {
        return new JObject
        {
            ["schemaVersion"] = value.schemaVersion,
            ["sourceKind"] = value.sourceKind,
            ["publicFactId"] = value.publicFactId,
            ["outcomeRunId"] = value.outcomeRunId,
            ["outcomeSequence"] = value.outcomeSequence,
            ["compactedMemoryId"] = value.compactedMemoryId,
            ["outcomeTypeId"] = value.outcomeTypeId,
            ["subjectKindId"] = value.subjectKindId,
            ["subjectId"] = value.subjectId,
            ["perspectiveKind"] = value.perspectiveKind,
            ["firstDay"] = value.firstDay,
            ["lastDay"] = value.lastDay,
            ["occurrenceCount"] = value.occurrenceCount,
            ["anchorRevision"] = value.anchorRevision,
            ["status"] = value.status,
            ["subjectSalience"] = value.subjectSalience,
            ["influenceUseCount"] = value.influenceUseCount,
            ["influenceRevision"] = value.influenceRevision,
            ["canonicalFactText"] = value.canonicalFactText,
            ["roleIds"] = new JArray(value.roleIds),
            ["metricIds"] = new JArray(value.metricIds),
            ["metricReferenceIds"] = new JArray(value.metricReferenceIds),
            ["factIds"] = new JArray(value.factIds),
            ["semanticTags"] = new JArray(value.semanticTags),
            ["participants"] = new JArray(value.participants.Select(item =>
                new JObject
                {
                    ["entityKindId"] = item.entityKindId,
                    ["entityId"] = item.entityId,
                    ["roleId"] = item.roleId,
                    ["participationKind"] = item.participationKind,
                    ["hasPerceptionEvidence"] = item.hasPerceptionEvidence,
                    ["displayText"] = item.displayText,
                    ["displayRevision"] = item.displayRevision,
                    ["locale"] = item.locale,
                    ["pronunciationMode"] = item.pronunciationMode,
                    ["pronunciationValue"] = item.pronunciationValue,
                    ["finalConsonant"] = item.finalConsonant,
                    ["pronunciationRevision"] = item.pronunciationRevision
                })),
            ["metrics"] = new JArray(value.metrics.Select(item => new JObject
            {
                ["metricId"] = item.metricId,
                ["unitId"] = item.unitId,
                ["referenceKindId"] = item.referenceKindId,
                ["referenceId"] = item.referenceId,
                ["value"] = item.value,
                ["minimum"] = item.minimum,
                ["maximum"] = item.maximum,
                ["sampleCount"] = item.sampleCount
            })),
            ["facts"] = new JArray(value.facts.Select(item => new JObject
            {
                ["factId"] = item.factId,
                ["value"] = item.value
            })),
            ["provenance"] = new JArray(value.provenance.Select(item => new JObject
            {
                ["kindId"] = item.kindId,
                ["value"] = item.value
            })),
            ["locationId"] = value.locationId,
            ["roomId"] = value.roomId,
            ["locationX"] = value.locationX,
            ["locationY"] = value.locationY,
            ["operationId"] = value.operationId,
            ["parentOutcomeId"] = value.parentOutcomeId,
            ["rootOperationId"] = value.rootOperationId,
            ["causationRelationId"] = value.causationRelationId
        };
    }

    private static JObject RequireClone(JObject value, string name) => value == null
        ? throw new ArgumentNullException(name)
        : (JObject)value.DeepClone();

    private static JObject RequireClosedClone(
        JObject value,
        string name,
        params string[] properties)
    {
        JObject clone = RequireClone(value, name);
        HashSet<string> expected = new(properties, StringComparer.Ordinal);
        if (!clone.Properties().Select(property => property.Name)
                .ToHashSet(StringComparer.Ordinal).SetEquals(expected))
        {
            throw new InvalidOperationException(
                name + " does not match the closed source-v8 contract.");
        }
        return clone;
    }

    private static string RequireProfile(string value)
    {
        string canonical = RequireText(value, nameof(value));
        string[] supported =
        {
            LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
            LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id,
            LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id,
            LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id
        };
        if (!supported.Contains(canonical, StringComparer.Ordinal))
            throw new ArgumentException(
                "An unsupported source-v8 profile was supplied.", nameof(value));
        return canonical;
    }

    private static string RequireText(string value, string name)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0
            || !string.Equals(canonical, value, StringComparison.Ordinal))
            throw new ArgumentException(
                "A canonical non-empty value is required.", name);
        return canonical;
    }

    private static string RequireSha256(string value, string name)
    {
        string canonical = RequireText(value, name);
        if (canonical.Length != 71
            || !canonical.StartsWith("sha256:", StringComparison.Ordinal)
            || canonical.Substring(7).Any(character =>
                !(character >= '0' && character <= '9')
                && !(character >= 'a' && character <= 'f')))
        {
            throw new ArgumentException(
                "A lowercase prefixed SHA-256 is required.", name);
        }
        return canonical;
    }

    private static string RequireCommit(string value, string name)
    {
        string canonical = RequireText(value, name);
        if (canonical.Length != 40 || canonical.Any(character =>
                !(character >= '0' && character <= '9')
                && !(character >= 'a' && character <= 'f')))
        {
            throw new ArgumentException(
                "A lowercase 40-character Git commit is required.", name);
        }
        return canonical;
    }
}
#endif
