#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Closed, model-facing projection of a production NarrativePublicContextMaterial.
/// The runtime-owned NarrativePublicModelInput is the sole owner of the
/// adapter-v2 projection and validation contract. This Editor boundary only
/// converts that already-validated projection into the export canonical JSON.
/// </summary>
public sealed class NarrativeMechanicScenarioPublicContext
{
    private readonly NarrativeMechanicScenarioFactAuditProvenance[] factAuditProvenance;

    internal NarrativeMechanicScenarioPublicContext(
        string targetPersistentId,
        NarrativeMechanicCatalogCanonicalJsonObject publicNarrativeContext,
        NarrativeMechanicCatalogCanonicalJsonArray publicFacts,
        string materialSemanticHash,
        IEnumerable<NarrativeMechanicScenarioFactAuditProvenance> factAuditProvenance)
    {
        TargetPersistentId = targetPersistentId;
        PublicNarrativeContext = publicNarrativeContext;
        PublicFacts = publicFacts;
        MaterialSemanticHash = materialSemanticHash;
        this.factAuditProvenance = CopyFactAuditProvenance(factAuditProvenance);
    }

    public string TargetPersistentId { get; }
    public NarrativeMechanicCatalogCanonicalJsonObject PublicNarrativeContext { get; }
    public NarrativeMechanicCatalogCanonicalJsonArray PublicFacts { get; }
    public string MaterialSemanticHash { get; }
    internal IReadOnlyList<NarrativeMechanicScenarioFactAuditProvenance>
        FactAuditProvenance => Array.AsReadOnly(factAuditProvenance);

    private static NarrativeMechanicScenarioFactAuditProvenance[] CopyFactAuditProvenance(
        IEnumerable<NarrativeMechanicScenarioFactAuditProvenance> values)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        List<NarrativeMechanicScenarioFactAuditProvenance> copied = new();
        foreach (NarrativeMechanicScenarioFactAuditProvenance value in values)
        {
            if (value == null)
            {
                throw new ArgumentException(
                    "Fact audit provenance cannot contain null.",
                    nameof(values));
            }
            copied.Add(value.Copy());
        }
        return copied.ToArray();
    }
}

/// <summary>
/// Internal-only provenance for deterministic continuity witnesses. These values
/// are captured from the production material, never emitted as adapter input,
/// and never folded into the scenario semantic hash.
/// </summary>
internal sealed class NarrativeMechanicScenarioFactAuditProvenance
{
    internal NarrativeMechanicScenarioFactAuditProvenance(
        string factId,
        string domain,
        string originalFactId,
        string sourceSubjectId)
    {
        FactId = factId ?? string.Empty;
        Domain = domain ?? string.Empty;
        OriginalFactId = originalFactId ?? string.Empty;
        SourceSubjectId = sourceSubjectId ?? string.Empty;
    }

    internal string FactId { get; }
    internal string Domain { get; }
    internal string OriginalFactId { get; }
    internal string SourceSubjectId { get; }

    internal NarrativeMechanicScenarioFactAuditProvenance Copy() => new(
        FactId,
        Domain,
        OriginalFactId,
        SourceSubjectId);
}

/// <summary>
/// Canonically exports the runtime-owned NarrativeAI adapter-v2 public model
/// input. The model-facing allowlist is exactly publicFacts and
/// publicNarrativeContext; audit, prompt, answer, fixture, and material-only
/// fields deliberately have no serialization path here. The exporter retains
/// its own canonical object-key order and requires that encoding to remain
/// byte-identical to the runtime adapter's authoritative prompt serialization.
/// </summary>
public static class NarrativeMechanicScenarioPublicContextSerializer
{
    public static NarrativeMechanicScenarioPublicContext Serialize(
        NarrativePublicContextMaterial material)
    {
        if (material == null)
        {
            throw new ArgumentNullException(nameof(material));
        }

        NarrativePublicModelInput modelInput = material.ToModelInput();
        return SerializeModelInput(
            modelInput,
            CaptureFactAuditProvenance(material));
    }

    private static NarrativeMechanicScenarioPublicContext SerializeModelInput(
        NarrativePublicModelInput modelInput,
        IReadOnlyList<NarrativeMechanicScenarioFactAuditProvenance> factAuditProvenance)
    {
        if (modelInput == null)
        {
            throw new ArgumentNullException(nameof(modelInput));
        }

        NarrativePublicModelInputProjection projection = modelInput.Projection
            ?? throw Unsupported(
                "public-model-input-projection-missing",
                "Runtime public narrative model input has no projection.");
        NarrativePublicModelContext context = projection.PublicNarrativeContext
            ?? throw Unsupported(
                "public-model-input-context-missing",
                "Runtime public narrative model input has no narrative context.");
        if (string.IsNullOrWhiteSpace(modelInput.SemanticHash))
        {
            throw Unsupported(
                "public-context-semantic-hash-empty",
                "Runtime public narrative model input has no material semantic hash.");
        }
        if (string.IsNullOrWhiteSpace(modelInput.CanonicalPayload))
        {
            throw Unsupported(
                "public-model-input-canonical-payload-empty",
                "Runtime public narrative model input has no canonical payload.");
        }
        ValidateFactAuditProvenance(projection, factAuditProvenance);

        List<NarrativeMechanicCatalogCanonicalJsonValue> facts = new();
        foreach (NarrativePublicModelFact fact in projection.PublicFacts)
        {
            List<KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>> properties =
                new()
                {
                    Property("domain", String(fact.Domain)),
                    Property("factId", String(fact.FactId)),
                    Property("text", String(fact.Text))
                };
            if (fact.LastDay.HasValue)
            {
                properties.Add(Property("lastDay", Integer(fact.LastDay.Value)));
            }
            if (fact.Count.HasValue)
            {
                properties.Add(Property("count", Integer(fact.Count.Value)));
            }
            if (fact.MilestoneCount.HasValue)
            {
                properties.Add(Property("milestoneCount", Integer(fact.MilestoneCount.Value)));
            }
            AddOptionalText(properties, "subjectId", fact.SubjectId);
            AddOptionalText(properties, "outcome", fact.Outcome);
            AddOptionalText(properties, "totalValueDecimal", fact.TotalValueDecimal);
            facts.Add(new NarrativeMechanicCatalogCanonicalJsonObject(properties));
        }

        List<NarrativeMechanicCatalogCanonicalJsonValue> entities = new();
        foreach (NarrativePublicModelEntity entity in context.Entities)
        {
            entities.Add(NarrativeMechanicCatalogCanonicalJson.Object(
                Property("displayName", String(entity.DisplayName)),
                Property("entityId", String(entity.EntityId)),
                Property("kind", String(entity.Kind))));
        }

        List<NarrativeMechanicCatalogCanonicalJsonValue> events = new();
        foreach (NarrativePublicModelEvent narrativeEvent in context.Events)
        {
            events.Add(NarrativeMechanicCatalogCanonicalJson.Object(
                Property("actorId", String(narrativeEvent.ActorId)),
                Property("count", Integer(narrativeEvent.Count)),
                Property("day", narrativeEvent.Day.HasValue
                    ? Integer(narrativeEvent.Day.Value)
                    : NarrativeMechanicScenarioCanonicalJsonNull.Value),
                Property("evidenceId", String(narrativeEvent.EvidenceId)),
                Property("eventType", String(narrativeEvent.EventType)),
                Property("factId", String(narrativeEvent.FactId)),
                Property("generation", narrativeEvent.Generation.HasValue
                    ? Integer(narrativeEvent.Generation.Value)
                    : NarrativeMechanicScenarioCanonicalJsonNull.Value),
                Property("outcome", String(narrativeEvent.Outcome)),
                Property("targetId", String(narrativeEvent.TargetId))));
        }

        NarrativeMechanicCatalogCanonicalJsonObject serializedContext =
            NarrativeMechanicCatalogCanonicalJson.Object(
                Property("backgroundFactIds", ReferenceArray(context.BackgroundFactIds)),
                Property("entities", new NarrativeMechanicCatalogCanonicalJsonArray(entities)),
                Property("events", new NarrativeMechanicCatalogCanonicalJsonArray(events)),
                Property("memoryFactIds", ReferenceArray(context.MemoryFactIds)),
                Property("priorHistoryFactIds", ReferenceArray(context.PriorHistoryFactIds)),
                Property("profileId", String(context.ProfileId)),
                Property("relationshipFactIds", ReferenceArray(context.RelationshipFactIds)),
                Property("schemaVersion", Integer(context.SchemaVersion)),
                Property("selection", NarrativeMechanicCatalogCanonicalJson.Object(
                    Property("availableFactCount", Integer(context.Selection.AvailableFactCount)),
                    Property("omittedFactCount", Integer(context.Selection.OmittedFactCount)),
                    Property("policyId", String(context.Selection.PolicyId)))),
                Property("subjectId", String(context.SubjectId)),
                Property("subjectKind", String(context.SubjectKind)));
        NarrativeMechanicCatalogCanonicalJsonArray serializedFacts =
            new NarrativeMechanicCatalogCanonicalJsonArray(facts);
        NarrativeMechanicCatalogCanonicalJsonObject serializedModelInput =
            NarrativeMechanicCatalogCanonicalJson.Object(
                Property("publicFacts", serializedFacts),
                Property("publicNarrativeContext", serializedContext));
        byte[] runtimeCanonicalPayload = NarrativeMechanicCatalogCanonicalJson.Utf8NoBom
            .GetBytes(modelInput.CanonicalPayload);
        if (!runtimeCanonicalPayload.SequenceEqual(serializedModelInput.ToCanonicalUtf8()))
        {
            throw Unsupported(
                "public-model-input-canonical-payload-mismatch",
                "Runtime public narrative model input does not match the export canonical payload.");
        }

        return new NarrativeMechanicScenarioPublicContext(
            context.SubjectId,
            serializedContext,
            serializedFacts,
            modelInput.SemanticHash,
            factAuditProvenance);
    }

    private static IReadOnlyList<NarrativeMechanicScenarioFactAuditProvenance>
        CaptureFactAuditProvenance(NarrativePublicContextMaterial material)
    {
        List<NarrativeMechanicScenarioFactAuditProvenance> result = new();
        foreach (NarrativePublicFact fact in material.PublicFacts)
        {
            if (fact == null)
            {
                throw Unsupported(
                    "public-context-audit-fact-null",
                    "Production public narrative material contains a null fact.");
            }
            result.Add(new NarrativeMechanicScenarioFactAuditProvenance(
                fact.FactId,
                fact.Domain,
                fact.OriginalFactId,
                fact.SourceSubjectId));
        }
        return result;
    }

    private static void ValidateFactAuditProvenance(
        NarrativePublicModelInputProjection projection,
        IReadOnlyList<NarrativeMechanicScenarioFactAuditProvenance> factAuditProvenance)
    {
        if (factAuditProvenance == null
            || factAuditProvenance.Count != projection.PublicFacts.Count)
        {
            throw Unsupported(
                "public-context-audit-fact-count",
                "Audit fact provenance does not match the runtime public model input.");
        }
        for (int index = 0; index < projection.PublicFacts.Count; index++)
        {
            NarrativeMechanicScenarioFactAuditProvenance audit = factAuditProvenance[index]
                ?? throw Unsupported(
                    "public-context-audit-fact-null",
                    "Audit fact provenance contains a null fact.");
            if (!string.Equals(
                    audit.FactId,
                    projection.PublicFacts[index].FactId,
                    StringComparison.Ordinal))
            {
                throw Unsupported(
                    "public-context-audit-fact-order",
                    "Audit fact provenance does not align with the runtime public model input.");
            }
        }
    }

    private static NarrativeMechanicCatalogCanonicalJsonArray ReferenceArray(
        IReadOnlyList<string> values)
    {
        List<NarrativeMechanicCatalogCanonicalJsonValue> result = new();
        foreach (string value in values)
        {
            result.Add(String(value));
        }
        return new NarrativeMechanicCatalogCanonicalJsonArray(result);
    }

    private static void AddOptionalText(
        ICollection<KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>> properties,
        string key,
        string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            properties.Add(Property(key, String(value)));
        }
    }

    private static NarrativeMechanicCatalogUnsupportedSourceException Unsupported(
        string code,
        string message) => new(code, message);

    private static NarrativeMechanicCatalogCanonicalJsonString String(string value) =>
        NarrativeMechanicCatalogCanonicalJson.String(value);

    private static NarrativeMechanicCatalogCanonicalJsonInt64 Integer(long value) =>
        NarrativeMechanicCatalogCanonicalJson.Integer(value);

    private static KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue> Property(
        string key,
        NarrativeMechanicCatalogCanonicalJsonValue value) =>
        NarrativeMechanicCatalogCanonicalJson.Property(key, value);
}

internal sealed class NarrativeMechanicScenarioCanonicalJsonNull
    : NarrativeMechanicCatalogCanonicalJsonValue
{
    public static readonly NarrativeMechanicScenarioCanonicalJsonNull Value = new();

    private NarrativeMechanicScenarioCanonicalJsonNull()
    {
    }

    internal override void WriteCanonical(System.Text.StringBuilder builder)
    {
        builder.Append("null");
    }

    internal override NarrativeMechanicCatalogCanonicalJsonValue Copy() => this;
}
#endif
