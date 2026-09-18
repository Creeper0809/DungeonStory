using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

public sealed class NarrativePublicModelFact
{
    internal NarrativePublicModelFact(
        string factId,
        string domain,
        string text,
        string subjectId,
        string outcome,
        int? lastDay,
        int? count,
        int? milestoneCount,
        string totalValueDecimal)
    {
        FactId = factId;
        Domain = domain;
        Text = text;
        SubjectId = subjectId ?? string.Empty;
        Outcome = outcome ?? string.Empty;
        LastDay = lastDay;
        Count = count;
        MilestoneCount = milestoneCount;
        TotalValueDecimal = totalValueDecimal ?? string.Empty;
    }

    public string FactId { get; }
    public string Domain { get; }
    public string Text { get; }
    public string SubjectId { get; }
    public string Outcome { get; }
    public int? LastDay { get; }
    public int? Count { get; }
    public int? MilestoneCount { get; }
    public string TotalValueDecimal { get; }
}

public sealed class NarrativePublicModelEntity
{
    internal NarrativePublicModelEntity(string entityId, string kind, string displayName)
    {
        EntityId = entityId;
        Kind = kind;
        DisplayName = displayName;
    }

    public string EntityId { get; }
    public string Kind { get; }
    public string DisplayName { get; }
}

public sealed class NarrativePublicModelEvent
{
    internal NarrativePublicModelEvent(
        string factId,
        string evidenceId,
        string actorId,
        string targetId,
        string eventType,
        string outcome,
        int? day,
        int count,
        int? generation)
    {
        FactId = factId;
        EvidenceId = evidenceId ?? string.Empty;
        ActorId = actorId ?? string.Empty;
        TargetId = targetId ?? string.Empty;
        EventType = eventType;
        Outcome = outcome ?? string.Empty;
        Day = day;
        Count = count;
        Generation = generation;
    }

    public string FactId { get; }
    public string EvidenceId { get; }
    public string ActorId { get; }
    public string TargetId { get; }
    public string EventType { get; }
    public string Outcome { get; }
    public int? Day { get; }
    public int Count { get; }
    public int? Generation { get; }
}

public sealed class NarrativePublicModelSelection
{
    internal NarrativePublicModelSelection(
        string policyId,
        int availableFactCount,
        int omittedFactCount)
    {
        PolicyId = policyId;
        AvailableFactCount = availableFactCount;
        OmittedFactCount = omittedFactCount;
    }

    public string PolicyId { get; }
    public int AvailableFactCount { get; }
    public int OmittedFactCount { get; }
}

public sealed class NarrativePublicModelContext
{
    internal NarrativePublicModelContext(
        int schemaVersion,
        string profileId,
        string subjectId,
        string subjectKind,
        IEnumerable<NarrativePublicModelEntity> entities,
        IEnumerable<NarrativePublicModelEvent> events,
        IEnumerable<string> backgroundFactIds,
        IEnumerable<string> relationshipFactIds,
        IEnumerable<string> memoryFactIds,
        IEnumerable<string> priorHistoryFactIds,
        NarrativePublicModelSelection selection)
    {
        SchemaVersion = schemaVersion;
        ProfileId = profileId;
        SubjectId = subjectId;
        SubjectKind = subjectKind;
        Entities = Array.AsReadOnly(entities.ToArray());
        Events = Array.AsReadOnly(events.ToArray());
        BackgroundFactIds = ReadOnlyStrings(backgroundFactIds);
        RelationshipFactIds = ReadOnlyStrings(relationshipFactIds);
        MemoryFactIds = ReadOnlyStrings(memoryFactIds);
        PriorHistoryFactIds = ReadOnlyStrings(priorHistoryFactIds);
        Selection = selection;
    }

    public int SchemaVersion { get; }
    public string ProfileId { get; }
    public string SubjectId { get; }
    public string SubjectKind { get; }
    public IReadOnlyList<NarrativePublicModelEntity> Entities { get; }
    public IReadOnlyList<NarrativePublicModelEvent> Events { get; }
    public IReadOnlyList<string> BackgroundFactIds { get; }
    public IReadOnlyList<string> RelationshipFactIds { get; }
    public IReadOnlyList<string> MemoryFactIds { get; }
    public IReadOnlyList<string> PriorHistoryFactIds { get; }
    public NarrativePublicModelSelection Selection { get; }

    private static IReadOnlyList<string> ReadOnlyStrings(IEnumerable<string> values) =>
        Array.AsReadOnly(values.ToArray());
}

/// <summary>
/// Immutable runtime owner of the NarrativeAI adapter-v2 public allowlist.
/// It deliberately contains only publicFacts and publicNarrativeContext.
/// </summary>
public sealed class NarrativePublicModelInputProjection
{
    internal NarrativePublicModelInputProjection(
        IEnumerable<NarrativePublicModelFact> publicFacts,
        NarrativePublicModelContext publicNarrativeContext)
    {
        PublicFacts = Array.AsReadOnly(publicFacts.ToArray());
        PublicNarrativeContext = publicNarrativeContext;
    }

    public IReadOnlyList<NarrativePublicModelFact> PublicFacts { get; }
    public NarrativePublicModelContext PublicNarrativeContext { get; }
}

/// <summary>
/// Canonical prompt serialization of the adapter-v2 public allowlist. Raw prompts,
/// answers, audit data, fixtures, priorities, categories, motifs, and internal
/// semantic metadata have no field in this projection.
/// </summary>
public sealed class NarrativePublicModelInput
{
    public const string BeginMarker = "[[V25-NARRATIVE-PUBLIC-INPUT]]";
    public const string EndMarker = "[[/V25-NARRATIVE-PUBLIC-INPUT]]";

    private readonly NarrativePublicModelInputProjection projection;

    private NarrativePublicModelInput(
        NarrativePublicModelInputProjection projection,
        string semanticHash)
    {
        this.projection = projection;
        SemanticHash = semanticHash ?? string.Empty;
        CanonicalPayload = Serialize(projection);
    }

    public string CanonicalPayload { get; }
    public string SemanticHash { get; }
    public NarrativePublicModelInputProjection Projection => projection;
    public NarrativePublicModelInputProjection CaptureSnapshot() => projection;

    public string AppendToPrompt(string prompt)
    {
        string source = prompt ?? string.Empty;
        if (source.Contains(BeginMarker) || source.Contains(EndMarker))
        {
            throw new InvalidOperationException(
                "A prompt cannot contain more than one public narrative model input.");
        }

        return source
            + "\nPublic narrative context (canonical public JSON):\n"
            + BeginMarker
            + "\n"
            + CanonicalPayload
            + "\n"
            + EndMarker
            + "\n";
    }

    public static NarrativePublicModelInput Create(NarrativePublicContextMaterial material)
    {
        if (material == null) throw new ArgumentNullException(nameof(material));
        return CreateValidated(material.CaptureSnapshot());
    }

    public static NarrativePublicModelInput Create(
        NarrativePublicContextMaterialSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        NarrativePublicContextMaterial restored = NarrativePublicContextFactory.Restore(
            snapshot.Clone());
        return CreateValidated(restored.CaptureSnapshot());
    }

    public static bool TryParsePrompt(
        string prompt,
        out NarrativePublicModelInput modelInput)
    {
        modelInput = null;
        if (!TryExtractPayload(prompt, out string payload)) return false;

        try
        {
            NarrativePublicModelInputProjection parsed = ParseProjection(payload);
            NarrativePublicModelInput candidate = new(parsed, string.Empty);
            if (!string.Equals(candidate.CanonicalPayload, payload, StringComparison.Ordinal))
                return false;
            modelInput = candidate;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            || exception is InvalidOperationException
            || exception is FormatException)
        {
            return false;
        }
    }

    public static bool TryCaptureSnapshotFromPrompt(
        string prompt,
        out NarrativePublicModelInputProjection snapshot)
    {
        snapshot = null;
        if (!TryParsePrompt(prompt, out NarrativePublicModelInput modelInput)) return false;
        snapshot = modelInput.CaptureSnapshot();
        return true;
    }

    internal static bool ContainsMarker(string prompt) =>
        (prompt ?? string.Empty).Contains(BeginMarker)
        || (prompt ?? string.Empty).Contains(EndMarker);

    internal bool IsPreservedInPrompt(string prompt) =>
        TryExtractPayload(prompt, out string payload)
        && string.Equals(CanonicalPayload, payload, StringComparison.Ordinal);

    internal bool MatchesRequestContext(NarrativeRequestContext context)
    {
        if (context == null
            || !string.Equals(
                projection.PublicNarrativeContext.ProfileId,
                context.ProfileId,
                StringComparison.Ordinal)
            || projection.PublicFacts.Count != context.Facts.Count)
        {
            return false;
        }

        for (int index = 0; index < context.Facts.Count; index++)
        {
            NarrativeContextEntry entry = context.Facts[index];
            string expectedReference = "F" + (index + 1).ToString("00");
            bool matched = projection.PublicFacts.Any(fact =>
            {
                NarrativeContextEntry normalized = new(
                    fact.FactId,
                    fact.Text,
                    priority: 0);
                return string.Equals(
                           normalized.StableId,
                           entry.StableId,
                           StringComparison.Ordinal)
                       && string.Equals(
                           normalized.Label,
                           entry.Label,
                           StringComparison.Ordinal);
            });
            if (!matched
                || !string.Equals(entry.Reference, expectedReference, StringComparison.Ordinal))
            {
                return false;
            }
        }

        for (int index = 0; index < context.Motifs.Count; index++)
        {
            string expectedReference = "M" + (index + 1).ToString("00");
            if (!string.Equals(
                    context.Motifs[index].Reference,
                    expectedReference,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static NarrativePublicModelInput CreateValidated(
        NarrativePublicContextMaterialSnapshot snapshot)
    {
        NarrativePublicModelInputProjection projected = Project(snapshot);
        Validate(projected);
        if (string.IsNullOrWhiteSpace(snapshot.semanticHash))
            throw new InvalidOperationException("Public narrative material has no semantic hash.");
        return new NarrativePublicModelInput(projected, snapshot.semanticHash);
    }

    private static NarrativePublicModelInputProjection Project(
        NarrativePublicContextMaterialSnapshot snapshot)
    {
        NarrativePublicModelFact[] facts = snapshot.publicFacts.Select(value =>
            new NarrativePublicModelFact(
                value.factId,
                value.domain,
                value.text,
                value.subjectId,
                value.outcome,
                value.hasLastDay ? value.lastDay : null,
                value.hasCount ? value.count : null,
                value.hasMilestoneCount ? value.milestoneCount : null,
                value.totalValueDecimal)).ToArray();
        NarrativePublicModelEntity[] entities = snapshot.entities.Select(value =>
            new NarrativePublicModelEntity(
                value.entityId,
                NarrativePublicContextFactory.EntityKindToken(value.kind),
                value.displayName)).ToArray();
        NarrativePublicModelEvent[] events = snapshot.events.Select(value =>
            new NarrativePublicModelEvent(
                value.factId,
                value.evidenceId,
                value.actorId,
                value.targetId,
                value.eventType,
                value.outcome,
                value.hasDay ? value.day : null,
                value.count,
                value.hasGeneration ? value.generation : null)).ToArray();
        NarrativePublicModelContext context = new(
            snapshot.schemaVersion,
            snapshot.profileId,
            snapshot.subjectId,
            NarrativePublicContextFactory.SubjectKindToken(snapshot.subjectKind),
            entities,
            events,
            snapshot.backgroundFactIds,
            snapshot.relationshipFactIds,
            snapshot.memoryFactIds,
            snapshot.priorHistoryFactIds,
            new NarrativePublicModelSelection(
                snapshot.selectionPolicyId,
                snapshot.availableFactCount,
                snapshot.omittedFactCount));
        return new NarrativePublicModelInputProjection(facts, context);
    }

    private static string Serialize(NarrativePublicModelInputProjection value)
    {
        StringBuilder builder = new(8192);
        builder.Append("{\"publicFacts\":[");
        for (int index = 0; index < value.PublicFacts.Count; index++)
        {
            if (index > 0) builder.Append(',');
            AppendFact(builder, value.PublicFacts[index]);
        }
        builder.Append("],\"publicNarrativeContext\":");
        AppendContext(builder, value.PublicNarrativeContext);
        builder.Append('}');
        return builder.ToString();
    }

    private static void AppendFact(StringBuilder builder, NarrativePublicModelFact value)
    {
        builder.Append('{');
        bool wrote = false;
        if (value.Count.HasValue)
            AppendIntegerProperty(builder, "count", value.Count.Value, ref wrote);
        AppendStringProperty(builder, "domain", value.Domain, ref wrote);
        AppendStringProperty(builder, "factId", value.FactId, ref wrote);
        if (value.LastDay.HasValue)
            AppendIntegerProperty(builder, "lastDay", value.LastDay.Value, ref wrote);
        if (value.MilestoneCount.HasValue)
            AppendIntegerProperty(builder, "milestoneCount", value.MilestoneCount.Value, ref wrote);
        if (value.Outcome.Length > 0)
            AppendStringProperty(builder, "outcome", value.Outcome, ref wrote);
        if (value.SubjectId.Length > 0)
            AppendStringProperty(builder, "subjectId", value.SubjectId, ref wrote);
        AppendStringProperty(builder, "text", value.Text, ref wrote);
        if (value.TotalValueDecimal.Length > 0)
            AppendStringProperty(builder, "totalValueDecimal", value.TotalValueDecimal, ref wrote);
        builder.Append('}');
    }

    private static void AppendContext(StringBuilder builder, NarrativePublicModelContext value)
    {
        builder.Append('{');
        bool wrote = false;
        AppendStringArrayProperty(builder, "backgroundFactIds", value.BackgroundFactIds, ref wrote);
        AppendPropertyName(builder, "entities", ref wrote);
        builder.Append('[');
        for (int index = 0; index < value.Entities.Count; index++)
        {
            if (index > 0) builder.Append(',');
            NarrativePublicModelEntity entity = value.Entities[index];
            builder.Append('{');
            bool entityWrote = false;
            AppendStringProperty(builder, "displayName", entity.DisplayName, ref entityWrote);
            AppendStringProperty(builder, "entityId", entity.EntityId, ref entityWrote);
            AppendStringProperty(builder, "kind", entity.Kind, ref entityWrote);
            builder.Append('}');
        }
        builder.Append(']');
        AppendPropertyName(builder, "events", ref wrote);
        builder.Append('[');
        for (int index = 0; index < value.Events.Count; index++)
        {
            if (index > 0) builder.Append(',');
            AppendEvent(builder, value.Events[index]);
        }
        builder.Append(']');
        AppendStringArrayProperty(builder, "memoryFactIds", value.MemoryFactIds, ref wrote);
        AppendStringArrayProperty(builder, "priorHistoryFactIds", value.PriorHistoryFactIds, ref wrote);
        AppendStringProperty(builder, "profileId", value.ProfileId, ref wrote);
        AppendStringArrayProperty(
            builder,
            "relationshipFactIds",
            value.RelationshipFactIds,
            ref wrote);
        AppendIntegerProperty(builder, "schemaVersion", value.SchemaVersion, ref wrote);
        AppendPropertyName(builder, "selection", ref wrote);
        builder.Append('{');
        bool selectionWrote = false;
        AppendIntegerProperty(
            builder,
            "availableFactCount",
            value.Selection.AvailableFactCount,
            ref selectionWrote);
        AppendIntegerProperty(
            builder,
            "omittedFactCount",
            value.Selection.OmittedFactCount,
            ref selectionWrote);
        AppendStringProperty(builder, "policyId", value.Selection.PolicyId, ref selectionWrote);
        builder.Append('}');
        AppendStringProperty(builder, "subjectId", value.SubjectId, ref wrote);
        AppendStringProperty(builder, "subjectKind", value.SubjectKind, ref wrote);
        builder.Append('}');
    }

    private static void AppendEvent(StringBuilder builder, NarrativePublicModelEvent value)
    {
        builder.Append('{');
        bool wrote = false;
        AppendStringProperty(builder, "actorId", value.ActorId, ref wrote);
        AppendIntegerProperty(builder, "count", value.Count, ref wrote);
        AppendNullableIntegerProperty(builder, "day", value.Day, ref wrote);
        AppendStringProperty(builder, "eventType", value.EventType, ref wrote);
        AppendStringProperty(builder, "evidenceId", value.EvidenceId, ref wrote);
        AppendStringProperty(builder, "factId", value.FactId, ref wrote);
        AppendNullableIntegerProperty(builder, "generation", value.Generation, ref wrote);
        AppendStringProperty(builder, "outcome", value.Outcome, ref wrote);
        AppendStringProperty(builder, "targetId", value.TargetId, ref wrote);
        builder.Append('}');
    }

    private static void AppendStringArrayProperty(
        StringBuilder builder,
        string name,
        IReadOnlyList<string> values,
        ref bool wrote)
    {
        AppendPropertyName(builder, name, ref wrote);
        builder.Append('[');
        for (int index = 0; index < values.Count; index++)
        {
            if (index > 0) builder.Append(',');
            AppendJsonString(builder, values[index]);
        }
        builder.Append(']');
    }

    private static void AppendStringProperty(
        StringBuilder builder,
        string name,
        string value,
        ref bool wrote)
    {
        AppendPropertyName(builder, name, ref wrote);
        AppendJsonString(builder, value);
    }

    private static void AppendIntegerProperty(
        StringBuilder builder,
        string name,
        int value,
        ref bool wrote)
    {
        AppendPropertyName(builder, name, ref wrote);
        builder.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendNullableIntegerProperty(
        StringBuilder builder,
        string name,
        int? value,
        ref bool wrote)
    {
        AppendPropertyName(builder, name, ref wrote);
        if (value.HasValue)
            builder.Append(value.Value.ToString(CultureInfo.InvariantCulture));
        else
            builder.Append("null");
    }

    private static void AppendPropertyName(
        StringBuilder builder,
        string name,
        ref bool wrote)
    {
        if (wrote) builder.Append(',');
        wrote = true;
        AppendJsonString(builder, name);
        builder.Append(':');
    }

    private static void AppendJsonString(StringBuilder builder, string value)
    {
        RequireWellFormedUtf16(value, nameof(value));
        builder.Append('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (character < 0x20)
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }
                    break;
            }
        }
        builder.Append('"');
    }

    private static NarrativePublicModelInputProjection ParseProjection(string payload)
    {
        PayloadJson parsed = JsonUtility.FromJson<PayloadJson>(payload);
        if (parsed?.publicFacts == null || parsed.publicNarrativeContext == null)
            throw new InvalidOperationException("Public model input is missing its allowlisted roots.");
        string[] factObjects = ExtractObjectArray(payload, "publicFacts");
        string[] eventObjects = ExtractObjectArray(payload, "events");
        if (factObjects.Length != parsed.publicFacts.Length
            || eventObjects.Length != (parsed.publicNarrativeContext.events?.Length ?? -1))
            throw new InvalidOperationException("Public model input arrays are malformed.");

        NarrativePublicModelFact[] facts = parsed.publicFacts.Select((value, index) =>
            new NarrativePublicModelFact(
                value.factId,
                value.domain,
                value.text,
                value.subjectId,
                value.outcome,
                HasProperty(factObjects[index], "lastDay") ? value.lastDay : null,
                HasProperty(factObjects[index], "count") ? value.count : null,
                HasProperty(factObjects[index], "milestoneCount")
                    ? value.milestoneCount
                    : null,
                value.totalValueDecimal)).ToArray();
        ContextJson source = parsed.publicNarrativeContext;
        NarrativePublicModelEntity[] entities = (source.entities
            ?? Array.Empty<EntityJson>()).Select(value =>
            new NarrativePublicModelEntity(value.entityId, value.kind, value.displayName)).ToArray();
        NarrativePublicModelEvent[] events = (source.events
            ?? Array.Empty<EventJson>()).Select((value, index) =>
            new NarrativePublicModelEvent(
                value.factId,
                value.evidenceId,
                value.actorId,
                value.targetId,
                value.eventType,
                value.outcome,
                IsNullProperty(eventObjects[index], "day") ? null : value.day,
                value.count,
                IsNullProperty(eventObjects[index], "generation") ? null : value.generation))
            .ToArray();
        if (source.selection == null)
            throw new InvalidOperationException("Public model input selection is missing.");
        NarrativePublicModelContext context = new(
            source.schemaVersion,
            source.profileId,
            source.subjectId,
            source.subjectKind,
            entities,
            events,
            source.backgroundFactIds ?? Array.Empty<string>(),
            source.relationshipFactIds ?? Array.Empty<string>(),
            source.memoryFactIds ?? Array.Empty<string>(),
            source.priorHistoryFactIds ?? Array.Empty<string>(),
            new NarrativePublicModelSelection(
                source.selection.policyId,
                source.selection.availableFactCount,
                source.selection.omittedFactCount));
        NarrativePublicModelInputProjection projection = new(facts, context);
        Validate(projection);
        return projection;
    }

    private static void Validate(NarrativePublicModelInputProjection value)
    {
        NarrativePublicModelContext context = value.PublicNarrativeContext
            ?? throw new InvalidOperationException("Public narrative context is missing.");
        if (context.SchemaVersion != NarrativePublicContextFactory.SchemaVersion)
            throw new InvalidOperationException("Unsupported public narrative context schema.");
        NarrativePublicSubjectKind subjectKind = ParseSubjectKind(context.SubjectKind);
        if (!NarrativePublicContextFactory.SupportsProfile(context.ProfileId, subjectKind))
            throw new InvalidOperationException("Public narrative profile and subject kind do not match.");
        string subjectId = RequireText(context.SubjectId, "subjectId", 512, true);
        RequireText(context.ProfileId, "profileId", 512, true);
        if (context.Entities.Count == 0
            || context.Entities.Count > NarrativePublicContextFactory.MaximumEntities)
            throw new InvalidOperationException("Public narrative entity count is out of range.");
        if (value.PublicFacts.Count == 0
            || value.PublicFacts.Count > NarrativePublicContextFactory.MaximumPublicFacts)
            throw new InvalidOperationException("Public narrative fact count is out of range.");
        if (context.Events.Count > NarrativePublicContextFactory.MaximumEvents)
            throw new InvalidOperationException("Public narrative event count is out of range.");

        HashSet<string> entityIds = new(StringComparer.Ordinal);
        bool foundSubject = false;
        foreach (NarrativePublicModelEntity entity in context.Entities)
        {
            string id = RequireText(entity.EntityId, "entityId", 512, true);
            RequireEntityKind(entity.Kind);
            RequireText(entity.DisplayName, "displayName", 120, false);
            if (!entityIds.Add(id))
                throw new InvalidOperationException($"Duplicate public entity '{id}'.");
            if (string.Equals(id, subjectId, StringComparison.Ordinal))
            {
                if (!string.Equals(entity.Kind, context.SubjectKind, StringComparison.Ordinal))
                    throw new InvalidOperationException("Public subject entity kind does not match.");
                foundSubject = true;
            }
        }
        if (!foundSubject)
            throw new InvalidOperationException("Public subject entity is missing.");

        HashSet<string> factIds = new(StringComparer.Ordinal);
        foreach (NarrativePublicModelFact fact in value.PublicFacts)
        {
            string id = RequireText(fact.FactId, "factId", 512, true);
            if (!factIds.Add(id))
                throw new InvalidOperationException($"Duplicate public fact '{id}'.");
            RequireText(fact.Domain, "domain", 128, false);
            RequireText(fact.Text, "text", 2000, false);
            RequireOptionalText(fact.SubjectId, "subjectId", 512, false);
            RequireOptionalText(fact.Outcome, "outcome", 512, false);
            RequireNonNegative(fact.LastDay, "lastDay");
            RequireNonNegative(fact.Count, "count");
            RequireNonNegative(fact.MilestoneCount, "milestoneCount");
            RequireOptionalText(fact.TotalValueDecimal, "totalValueDecimal", 512, false);
        }
        HashSet<string> equipmentEvidenceIds =
            subjectKind == NarrativePublicSubjectKind.Equipment
                ? new HashSet<string>(StringComparer.Ordinal)
                : null;
        foreach (NarrativePublicModelEvent narrativeEvent in context.Events)
        {
            if (!factIds.Contains(RequireText(narrativeEvent.FactId, "event.factId", 512, true)))
                throw new InvalidOperationException("Public event references an unknown fact.");
            string actor = RequireOptionalText(narrativeEvent.ActorId, "actorId", 512, true);
            string target = RequireOptionalText(narrativeEvent.TargetId, "targetId", 512, true);
            if ((actor.Length > 0 && !entityIds.Contains(actor))
                || (target.Length > 0 && !entityIds.Contains(target)))
                throw new InvalidOperationException("Public event participant is unresolved.");
            if (equipmentEvidenceIds != null)
            {
                string evidenceId = RequireText(
                    narrativeEvent.EvidenceId,
                    "evidenceId",
                    512,
                    true);
                if (!equipmentEvidenceIds.Add(evidenceId))
                    throw new InvalidOperationException(
                        $"Duplicate equipment public event evidence '{evidenceId}'.");
            }
            else
            {
                RequireOptionalText(narrativeEvent.EvidenceId, "evidenceId", 512, true);
            }
            RequireText(narrativeEvent.EventType, "eventType", 512, true);
            RequireOptionalText(narrativeEvent.Outcome, "event.outcome", 512, false);
            RequireNonNegative(narrativeEvent.Day, "event.day");
            RequireNonNegative(narrativeEvent.Generation, "event.generation");
            if (narrativeEvent.Count <= 0)
                throw new InvalidOperationException("Public event count must be positive.");
        }

        ValidateReferenceGroup(context.BackgroundFactIds, factIds, "backgroundFactIds");
        ValidateReferenceGroup(context.RelationshipFactIds, factIds, "relationshipFactIds");
        ValidateReferenceGroup(context.MemoryFactIds, factIds, "memoryFactIds");
        ValidateReferenceGroup(context.PriorHistoryFactIds, factIds, "priorHistoryFactIds");
        NarrativePublicModelSelection selection = context.Selection
            ?? throw new InvalidOperationException("Public narrative selection is missing.");
        RequireText(selection.PolicyId, "selection.policyId", 512, true);
        if (selection.OmittedFactCount < 0
            || selection.AvailableFactCount != value.PublicFacts.Count + selection.OmittedFactCount)
            throw new InvalidOperationException("Public narrative selection counts do not reconcile.");
    }

    private static void ValidateReferenceGroup(
        IEnumerable<string> values,
        ISet<string> factIds,
        string fieldName)
    {
        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (string value in values)
        {
            string id = RequireText(value, fieldName, 512, true);
            if (!unique.Add(id) || !factIds.Contains(id))
                throw new InvalidOperationException($"Public reference group '{fieldName}' is invalid.");
        }
    }

    private static string RequireText(
        string value,
        string fieldName,
        int maximumLength,
        bool noWhitespace)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > maximumLength
            || (noWhitespace && value.Any(char.IsWhiteSpace)))
            throw new InvalidOperationException($"Public field '{fieldName}' is invalid.");
        RequireWellFormedUtf16(value, fieldName);
        return value;
    }

    private static string RequireOptionalText(
        string value,
        string fieldName,
        int maximumLength,
        bool noWhitespace)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return RequireText(value, fieldName, maximumLength, noWhitespace);
    }

    private static void RequireNonNegative(int? value, string fieldName)
    {
        if (value < 0)
            throw new InvalidOperationException($"Public field '{fieldName}' cannot be negative.");
    }

    private static NarrativePublicSubjectKind ParseSubjectKind(string value) => value switch
    {
        "character" => NarrativePublicSubjectKind.Character,
        "equipment" => NarrativePublicSubjectKind.Equipment,
        "facility" => NarrativePublicSubjectKind.Facility,
        _ => throw new InvalidOperationException("Public subject kind is invalid.")
    };

    private static void RequireEntityKind(string value)
    {
        if (value != "character" && value != "equipment" && value != "facility"
            && value != "place" && value != "group")
            throw new InvalidOperationException("Public entity kind is invalid.");
    }

    private static void RequireWellFormedUtf16(string value, string fieldName)
    {
        if (value == null) throw new InvalidOperationException($"Public field '{fieldName}' is null.");
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                    throw new InvalidOperationException($"Public field '{fieldName}' has invalid UTF-16.");
                index++;
            }
            else if (char.IsLowSurrogate(character))
            {
                throw new InvalidOperationException($"Public field '{fieldName}' has invalid UTF-16.");
            }
        }
    }

    private static string[] ExtractObjectArray(string json, string propertyName)
    {
        int index = FindPropertyValueStart(json, propertyName);
        if (index < 0 || index >= json.Length || json[index] != '[')
            throw new FormatException($"Canonical array '{propertyName}' is missing.");
        index++;
        List<string> values = new();
        while (index < json.Length)
        {
            if (json[index] == ']') return values.ToArray();
            if (json[index] != '{')
                throw new FormatException($"Canonical array '{propertyName}' is malformed.");
            int start = index;
            int depth = 0;
            bool quoted = false;
            bool escaped = false;
            for (; index < json.Length; index++)
            {
                char character = json[index];
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (character == '\\') escaped = true;
                    else if (character == '"') quoted = false;
                    continue;
                }
                if (character == '"') quoted = true;
                else if (character == '{') depth++;
                else if (character == '}' && --depth == 0)
                {
                    index++;
                    values.Add(json.Substring(start, index - start));
                    break;
                }
            }
            if (depth != 0 || index >= json.Length)
                throw new FormatException($"Canonical array '{propertyName}' is incomplete.");
            if (json[index] == ',') index++;
            else if (json[index] != ']')
                throw new FormatException($"Canonical array '{propertyName}' separator is invalid.");
        }
        throw new FormatException($"Canonical array '{propertyName}' is incomplete.");
    }

    private static bool HasProperty(string jsonObject, string propertyName) =>
        FindPropertyValueStart(jsonObject, propertyName) >= 0;

    private static bool IsNullProperty(string jsonObject, string propertyName)
    {
        int index = FindPropertyValueStart(jsonObject, propertyName);
        if (index < 0)
            throw new FormatException($"Required property '{propertyName}' is missing.");
        return jsonObject.IndexOf("null", index, StringComparison.Ordinal) == index;
    }

    private static int FindPropertyValueStart(string json, string propertyName)
    {
        string token = "\"" + propertyName + "\":";
        bool quoted = false;
        bool escaped = false;
        for (int index = 0; index < json.Length; index++)
        {
            char character = json[index];
            if (quoted)
            {
                if (escaped) escaped = false;
                else if (character == '\\') escaped = true;
                else if (character == '"') quoted = false;
                continue;
            }
            if (character != '"') continue;
            if (json.IndexOf(token, index, StringComparison.Ordinal) == index)
                return index + token.Length;
            quoted = true;
        }
        return -1;
    }

    private static bool TryExtractPayload(string prompt, out string payload)
    {
        payload = string.Empty;
        if (string.IsNullOrEmpty(prompt)) return false;
        int begin = prompt.IndexOf(BeginMarker, StringComparison.Ordinal);
        int end = prompt.LastIndexOf(EndMarker, StringComparison.Ordinal);
        if (begin < 0 || end <= begin) return false;
        int payloadStart = begin + BeginMarker.Length;
        while (payloadStart < end
               && (prompt[payloadStart] == '\r' || prompt[payloadStart] == '\n'))
            payloadStart++;
        int payloadEnd = end;
        while (payloadEnd > payloadStart
               && (prompt[payloadEnd - 1] == '\r' || prompt[payloadEnd - 1] == '\n'))
            payloadEnd--;
        if (payloadEnd <= payloadStart) return false;
        payload = prompt.Substring(payloadStart, payloadEnd - payloadStart);
        return true;
    }

    [Serializable]
    private sealed class PayloadJson
    {
        public FactJson[] publicFacts;
        public ContextJson publicNarrativeContext;
    }

    [Serializable]
    private sealed class FactJson
    {
        public int count;
        public string domain;
        public string factId;
        public int lastDay;
        public int milestoneCount;
        public string outcome;
        public string subjectId;
        public string text;
        public string totalValueDecimal;
    }

    [Serializable]
    private sealed class ContextJson
    {
        public string[] backgroundFactIds;
        public EntityJson[] entities;
        public EventJson[] events;
        public string[] memoryFactIds;
        public string[] priorHistoryFactIds;
        public string profileId;
        public string[] relationshipFactIds;
        public int schemaVersion;
        public SelectionJson selection;
        public string subjectId;
        public string subjectKind;
    }

    [Serializable]
    private sealed class EntityJson
    {
        public string displayName;
        public string entityId;
        public string kind;
    }

    [Serializable]
    private sealed class EventJson
    {
        public string actorId;
        public int count;
        public int day;
        public string evidenceId;
        public string eventType;
        public string factId;
        public int generation;
        public string outcome;
        public string targetId;
    }

    [Serializable]
    private sealed class SelectionJson
    {
        public int availableFactCount;
        public int omittedFactCount;
        public string policyId;
    }
}
