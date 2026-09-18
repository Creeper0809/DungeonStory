#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Emits a compact, deterministic audit document for continuity edge cases.
/// Model-facing projections stay closed. Source-tuple assertions read the
/// immutable audit provenance captured with each production material; this
/// class neither reads prompt text nor recreates facts.
/// </summary>
internal static class NarrativeMechanicScenarioContinuityWitnesses
{
    private const int ExpectedWitnessCount = 6;

    public static NarrativeMechanicCatalogCanonicalJsonObject Build(
        NarrativeMechanicScenarioBundle bundle,
        string catalogHash,
        string inputDigest)
    {
        if (bundle == null) throw new ArgumentNullException(nameof(bundle));
        NarrativeMechanicScenario equipment = RequireScenario(
            bundle, "equipment-choice-execution-ranged", accepted: true);
        NarrativeMechanicScenario history = RequireScenario(
            bundle, "evolution-history-execution-ranged", accepted: true);
        NarrativeMechanicScenario facility = RequireScenario(
            bundle, "facility-evolution-alchemy-research", accepted: true);
        NarrativeMechanicScenario firstLife = RequireScenario(
            bundle, "persona-slime-care", accepted: true);
        NarrativeMechanicScenario secondLife = RequireScenario(
            bundle, "persona-orc-patience", accepted: true);
        NarrativeMechanicScenario negative = RequireScenario(
            bundle, "equipment-choice-reject-out-of-range-index", accepted: false);

        FactTuple collision = RequireProjectedTupleCollision(equipment, history);
        FactTuple longestOriginalId = RequireLongOriginalFactId(facility);
        int coreEventCount = RequireEventCount(equipment);
        string[] priorHistoryFactIds = RequirePriorGeneration(history);
        if (string.Equals(
                firstLife.TargetPersistentId,
                secondLife.TargetPersistentId,
                StringComparison.Ordinal))
        {
            throw Unsupported(
                "continuity-witness-different-life-target",
                "Different-life witness scenarios must retain different persistent targets.");
        }
        if (negative.Accepted)
        {
            throw Unsupported(
                "continuity-witness-negative-accepted",
                "Negative-reference witness must remain a rejected scenario.");
        }

        List<NarrativeMechanicCatalogCanonicalJsonValue> witnesses = new()
        {
            Witness(
                "projected-source-tuple-collision",
                "source-tuple-collision",
                NarrativeMechanicCatalogCanonicalJson.Object(
                    Property("domain", String(collision.Domain)),
                    Property("originalFactId", String(collision.OriginalFactId)),
                    Property("projectedFactIds", StringArray(
                        collision.FactIds.OrderBy(value => value, StringComparer.Ordinal))),
                    Property("sourceSubjectIds", StringArray(
                        collision.SourceSubjectIds.OrderBy(value => value, StringComparer.Ordinal)))),
                equipment,
                history),
            Witness(
                "full-sha256-derived-original-id",
                "long-id",
                NarrativeMechanicCatalogCanonicalJson.Object(
                    Property("domain", String(longestOriginalId.Domain)),
                    Property("minimumObservedOriginalFactIdLength", Integer(
                        longestOriginalId.OriginalFactId.Length)),
                    Property("originalFactId", String(longestOriginalId.OriginalFactId)),
                    Property("projectedFactId", String(longestOriginalId.FactId)),
                    Property("sourceSubjectId", String(longestOriginalId.SourceSubjectId))),
                facility),
            Witness(
                "distinct-persistent-life-targets",
                "different-life",
                NarrativeMechanicCatalogCanonicalJson.Object(
                    Property("targetPersistentIds", StringArray(new[]
                    {
                        firstLife.TargetPersistentId,
                        secondLife.TargetPersistentId
                    }.OrderBy(value => value, StringComparer.Ordinal)))),
                firstLife,
                secondLife),
            Witness(
                "frozen-core-event",
                "core-event",
                NarrativeMechanicCatalogCanonicalJson.Object(
                    Property("eventCount", Integer(coreEventCount))),
                equipment),
            Witness(
                "frozen-prior-generation",
                "prior-generation",
                NarrativeMechanicCatalogCanonicalJson.Object(
                    Property("priorHistoryFactIds", StringArray(priorHistoryFactIds))),
                history),
            Witness(
                "rejected-scenario-reference",
                "negative-reference",
                NarrativeMechanicCatalogCanonicalJson.Object(
                    Property("accepted", Boolean(false)),
                    Property("evaluationOnly", Boolean(true))),
                negative)
        };
        if (witnesses.Count != ExpectedWitnessCount)
        {
            throw Unsupported(
                "continuity-witness-count",
                $"Expected exactly {ExpectedWitnessCount} continuity witnesses; found {witnesses.Count}.");
        }

        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("catalogHash", String(catalogHash)),
            Property("inputDigest", String(inputDigest)),
            Property("schemaVersion", Integer(1)),
            Property("witnessCount", Integer(witnesses.Count)),
            Property("witnesses", new NarrativeMechanicCatalogCanonicalJsonArray(witnesses)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject Witness(
        string witnessId,
        string witnessKind,
        NarrativeMechanicCatalogCanonicalJsonObject assertions,
        params NarrativeMechanicScenario[] scenarios)
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("assertions", assertions),
            Property("scenarioReferences", new NarrativeMechanicCatalogCanonicalJsonArray(
                scenarios.Select(scenario =>
                    (NarrativeMechanicCatalogCanonicalJsonValue)ScenarioProjection(scenario)))),
            Property("witnessId", String(witnessId)),
            Property("witnessKind", String(witnessKind)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject ScenarioProjection(
        NarrativeMechanicScenario scenario)
    {
        if (scenario == null) throw new ArgumentNullException(nameof(scenario));
        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("accepted", Boolean(scenario.Accepted)),
            Property("profileId", String(scenario.ProfileId)),
            Property("publicFacts", scenario.PublicFactsForWitness),
            Property("publicNarrativeContext", scenario.PublicNarrativeContextForWitness),
            Property("scenarioId", String(scenario.ScenarioId)),
            Property("scenarioSemanticHash", String(scenario.SemanticHash)),
            Property("targetPersistentId", String(scenario.TargetPersistentId)));
    }

    private static FactTuple RequireProjectedTupleCollision(
        NarrativeMechanicScenario first,
        NarrativeMechanicScenario second)
    {
        FactTuple[] firstFacts = ReadFactTuples(first).ToArray();
        FactTuple[] secondFacts = ReadFactTuples(second).ToArray();
        foreach (FactTuple candidate in firstFacts)
        {
            FactTuple matching = secondFacts.FirstOrDefault(other =>
                string.Equals(candidate.Domain, other.Domain, StringComparison.Ordinal)
                && string.Equals(candidate.OriginalFactId, other.OriginalFactId, StringComparison.Ordinal)
                && !string.Equals(
                    candidate.SourceSubjectId,
                    other.SourceSubjectId,
                    StringComparison.Ordinal));
            if (matching != null)
            {
                return new FactTuple(
                    candidate.Domain,
                    candidate.OriginalFactId,
                    new[] { candidate.SourceSubjectId, matching.SourceSubjectId },
                    new[] { candidate.FactId, matching.FactId });
            }
        }
        throw Unsupported(
            "continuity-witness-source-tuple-collision-missing",
            "Continuity collision witness requires one shared original fact ID with distinct source subjects.");
    }

    private static FactTuple RequireLongOriginalFactId(NarrativeMechanicScenario scenario)
    {
        FactTuple longest = ReadFactTuples(scenario)
            .OrderByDescending(tuple => tuple.OriginalFactId.Length)
            .ThenBy(tuple => tuple.OriginalFactId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (longest == null || longest.OriginalFactId.Length < 64)
        {
            throw Unsupported(
                "continuity-witness-long-id-missing",
                "Long-ID witness requires a production originalFactId of at least 64 characters.");
        }
        return longest;
    }

    private static int RequireEventCount(NarrativeMechanicScenario scenario)
    {
        NarrativeMechanicCatalogCanonicalJsonArray events = RequireArrayProperty(
            scenario.PublicNarrativeContextForWitness,
            "events",
            scenario.ScenarioId);
        if (events.Values.Count == 0)
        {
            throw Unsupported(
                "continuity-witness-core-event-missing",
                "Core-event witness requires an event from production public material.");
        }
        return events.Values.Count;
    }

    private static string[] RequirePriorGeneration(NarrativeMechanicScenario scenario)
    {
        if (!ReadFactTuples(scenario).Any(tuple => string.Equals(
                tuple.Domain,
                "evolution-prior-segment",
                StringComparison.Ordinal)))
        {
            throw Unsupported(
                "continuity-witness-prior-generation-fact-missing",
                "Prior-generation witness requires the production evolution formatter to select a compacted segment.");
        }
        NarrativeMechanicCatalogCanonicalJsonArray ids = RequireArrayProperty(
            scenario.PublicNarrativeContextForWitness,
            "priorHistoryFactIds",
            scenario.ScenarioId);
        string[] values = ids.Values
            .Select(value => RequireString(value, "priorHistoryFactIds", scenario.ScenarioId))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (values.Length == 0)
        {
            throw Unsupported(
                "continuity-witness-prior-generation-reference-missing",
                "Prior-generation witness requires a public priorHistoryFactIds reference.");
        }
        return values;
    }

    private static IEnumerable<FactTuple> ReadFactTuples(
        NarrativeMechanicScenario scenario)
    {
        foreach (NarrativeMechanicScenarioFactAuditProvenance audit in
                 scenario.FactAuditProvenanceForWitness)
        {
            if (audit == null)
            {
                throw Unsupported(
                    "continuity-witness-audit-fact-missing",
                    $"Scenario '{scenario.ScenarioId}' has null fact audit provenance.");
            }
            yield return new FactTuple(
                RequireAuditString(audit.FactId, "factId", scenario.ScenarioId),
                RequireAuditString(audit.Domain, "domain", scenario.ScenarioId),
                RequireAuditString(
                    audit.OriginalFactId,
                    "originalFactId",
                    scenario.ScenarioId),
                RequireAuditString(
                    audit.SourceSubjectId,
                    "sourceSubjectId",
                    scenario.ScenarioId));
        }
    }

    private static NarrativeMechanicScenario RequireScenario(
        NarrativeMechanicScenarioBundle bundle,
        string scenarioId,
        bool accepted)
    {
        NarrativeMechanicScenario scenario = (accepted
                ? bundle.PositiveScenarios
                : bundle.NegativeScenarios)
            .SingleOrDefault(value => string.Equals(
                value.ScenarioId,
                scenarioId,
                StringComparison.Ordinal));
        if (scenario == null)
        {
            throw Unsupported(
                "continuity-witness-scenario-missing",
                $"Required {(accepted ? "positive" : "negative")} scenario '{scenarioId}' is missing.");
        }
        return scenario;
    }

    private static NarrativeMechanicCatalogCanonicalJsonArray RequireArrayProperty(
        NarrativeMechanicCatalogCanonicalJsonObject source,
        string property,
        string scenarioId)
    {
        NarrativeMechanicCatalogCanonicalJsonArray value = source.Properties
            .Where(pair => string.Equals(pair.Key, property, StringComparison.Ordinal))
            .Select(pair => pair.Value as NarrativeMechanicCatalogCanonicalJsonArray)
            .SingleOrDefault();
        if (value == null)
        {
            throw Unsupported(
                "continuity-witness-public-context-property",
                $"Scenario '{scenarioId}' lacks publicNarrativeContext.{property}.");
        }
        return value;
    }

    private static string RequireString(
        NarrativeMechanicCatalogCanonicalJsonValue value,
        string property,
        string scenarioId)
    {
        NarrativeMechanicCatalogCanonicalJsonString text = value as
            NarrativeMechanicCatalogCanonicalJsonString;
        if (text == null || string.IsNullOrWhiteSpace(text.Value))
        {
            throw Unsupported(
                "continuity-witness-public-context-string",
                $"Scenario '{scenarioId}' has no nonblank '{property}' string.");
        }
        return text.Value;
    }

    private static string RequireAuditString(
        string value,
        string property,
        string scenarioId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Unsupported(
                "continuity-witness-audit-fact-string",
                $"Scenario '{scenarioId}' has no nonblank audit '{property}' string.");
        }
        return value;
    }

    private static NarrativeMechanicCatalogCanonicalJsonArray StringArray(
        IEnumerable<string> values) => new NarrativeMechanicCatalogCanonicalJsonArray(
        (values ?? Array.Empty<string>()).Select(value =>
            (NarrativeMechanicCatalogCanonicalJsonValue)String(value)));

    private static NarrativeMechanicCatalogCanonicalJsonString String(string value) =>
        NarrativeMechanicCatalogCanonicalJson.String(value);
    private static NarrativeMechanicCatalogCanonicalJsonInt64 Integer(long value) =>
        NarrativeMechanicCatalogCanonicalJson.Integer(value);
    private static NarrativeMechanicCatalogCanonicalJsonBoolean Boolean(bool value) =>
        NarrativeMechanicCatalogCanonicalJson.Boolean(value);
    private static KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue> Property(
        string key,
        NarrativeMechanicCatalogCanonicalJsonValue value) =>
        NarrativeMechanicCatalogCanonicalJson.Property(key, value);

    private static NarrativeMechanicCatalogUnsupportedSourceException Unsupported(
        string code,
        string message) => new NarrativeMechanicCatalogUnsupportedSourceException(code, message);

    private sealed class FactTuple
    {
        public FactTuple(
            string factId,
            string domain,
            string originalFactId,
            string sourceSubjectId)
            : this(
                domain,
                originalFactId,
                new[] { sourceSubjectId },
                new[] { factId })
        {
        }

        public FactTuple(
            string domain,
            string originalFactId,
            IEnumerable<string> sourceSubjectIds,
            IEnumerable<string> factIds)
        {
            Domain = domain;
            OriginalFactId = originalFactId;
            SourceSubjectIds = (sourceSubjectIds ?? Array.Empty<string>()).ToArray();
            FactIds = (factIds ?? Array.Empty<string>()).ToArray();
        }

        public string Domain { get; }
        public string OriginalFactId { get; }
        public IReadOnlyList<string> SourceSubjectIds { get; }
        public IReadOnlyList<string> FactIds { get; }
        public string SourceSubjectId => SourceSubjectIds.FirstOrDefault() ?? string.Empty;
        public string FactId => FactIds.FirstOrDefault() ?? string.Empty;
    }
}
#endif
