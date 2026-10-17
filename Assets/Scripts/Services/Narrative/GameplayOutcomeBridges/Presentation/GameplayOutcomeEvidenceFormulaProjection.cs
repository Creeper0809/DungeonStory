using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class GameplayOutcomeEvidenceFormulaProjection
{
    public const int CurrentBindingSchemaVersion = 4;

    public static List<GameplayOutcomeEvidenceBindingSnapshot> CaptureExact(
        IEnumerable<GameplayOutcomeNarrativeEvidenceSource> sources)
        => Capture(sources, includeCompacted: false);

    public static List<GameplayOutcomeEvidenceBindingSnapshot> Capture(
        IEnumerable<GameplayOutcomeNarrativeEvidenceSource> sources,
        bool includeCompacted = true)
    {
        List<GameplayOutcomeEvidenceBindingSnapshot> result = new();
        foreach (GameplayOutcomeNarrativeEvidenceSource source in
                 sources ?? Array.Empty<GameplayOutcomeNarrativeEvidenceSource>())
        {
            if (!GameplayOutcomeStableIdSyntax.IsValid(source.SourceId)
                || source.IsCompacted && !includeCompacted
                || source.IsCompacted && !source.CompactedMemoryId.IsValid
                || !source.IsCompacted && !source.HasExactOutcome)
                continue;
            var binding = new GameplayOutcomeEvidenceBindingSnapshot
            {
                schemaVersion = CurrentBindingSchemaVersion,
                sourceKind = source.IsCompacted ? "compacted" : "exact",
                publicFactId = source.SourceId,
                outcomeRunId = source.IsCompacted
                    ? string.Empty : source.ExactOutcomeId.RunId.Value,
                outcomeSequence = source.IsCompacted
                    ? 0L : source.ExactOutcomeId.Sequence,
                compactedMemoryId = source.IsCompacted
                    ? source.CompactedMemoryId.Value : string.Empty,
                outcomeTypeId = source.OutcomeTypeId,
                subjectKindId = source.SubjectId.Kind.Value,
                subjectId = source.SubjectId.Value,
                perspectiveKind = (int)source.PerspectiveKind,
                firstDay = source.FirstDay,
                lastDay = source.LastDay,
                occurrenceCount = source.Count,
                anchorRevision = source.AnchorRevision,
                status = (int)source.Status,
                subjectSalience = source.SubjectSalience,
                influenceUseCount = source.InfluenceUseCount,
                influenceRevision = source.InfluenceRevision,
                canonicalFactText = source.CanonicalFactText,
                roleIds = CanonicalDistinct(source.Participants.Select(value => value.RoleId)),
                metricIds = CanonicalDistinct(source.Metrics.Select(value => value.MetricId)),
                metricReferenceIds = CanonicalDistinct(source.Metrics.Select(value =>
                    string.IsNullOrWhiteSpace(value.ReferenceKindId)
                        ? string.Empty
                        : value.ReferenceKindId + ":" + value.ReferenceId)),
                factIds = CanonicalDistinct(source.Facts.Select(value => value.FactId)),
                semanticTags = CanonicalDistinct(source.Tags),
                participants = source.Participants.Select(value =>
                    new GameplayOutcomeEvidenceParticipantBindingSnapshot
                    {
                        entityKindId = value.EntityKindId,
                        entityId = value.EntityId,
                        roleId = value.RoleId,
                        participationKind = (int)value.ParticipationKind,
                        hasPerceptionEvidence = value.HasPerceptionEvidence,
                        displayText = value.DisplayName.DisplayText,
                        displayRevision = value.DisplayName.DisplaySnapshotRevision,
                        locale = value.DisplayName.Locale,
                        pronunciationMode = (int)value.DisplayName.PronunciationHint.Mode,
                        pronunciationValue = value.DisplayName.PronunciationHint.Value,
                        finalConsonant = (int)value.DisplayName.PronunciationHint
                            .ExplicitFinalConsonant,
                        pronunciationRevision = value.DisplayName.PronunciationHint.Revision
                    })
                    .OrderBy(value => value.roleId, StringComparer.Ordinal)
                    .ThenBy(value => value.entityKindId, StringComparer.Ordinal)
                    .ThenBy(value => value.entityId, StringComparer.Ordinal)
                    .ToList(),
                metrics = source.Metrics.Select(value =>
                    new GameplayOutcomeEvidenceMetricBindingSnapshot
                    {
                        metricId = value.MetricId,
                        unitId = value.UnitId,
                        referenceKindId = value.ReferenceKindId,
                        referenceId = value.ReferenceId,
                        value = value.Value,
                        minimum = value.Minimum,
                        maximum = value.Maximum,
                        sampleCount = value.SampleCount
                    })
                    .OrderBy(value => value.metricId, StringComparer.Ordinal)
                    .ThenBy(value => value.referenceKindId, StringComparer.Ordinal)
                    .ThenBy(value => value.referenceId, StringComparer.Ordinal)
                    .ToList(),
                facts = source.Facts.Select(value =>
                    new GameplayOutcomeEvidenceFactBindingSnapshot
                    {
                        factId = value.FactId,
                        value = value.Value
                    })
                    .OrderBy(value => value.factId, StringComparer.Ordinal)
                    .ToList(),
                provenance = source.Provenance.Select(value =>
                    new GameplayOutcomeEvidenceProvenanceBindingSnapshot
                    {
                        kindId = value.KindId,
                        value = value.Value
                    })
                    .OrderBy(value => value.kindId, StringComparer.Ordinal)
                    .ThenBy(value => value.value, StringComparer.Ordinal)
                    .ToList(),
                locationId = source.Location.LocationId,
                roomId = source.Location.RoomId,
                locationX = source.Location.X,
                locationY = source.Location.Y,
                operationId = source.Causation.OperationId,
                parentOutcomeId = source.Causation.ParentOutcomeId,
                rootOperationId = source.Causation.RootOperationId,
                causationRelationId = source.Causation.RelationId
            };
            binding.experience = GameplayExperienceObserverRegistration.Default.Observe(binding);
            result.Add(binding);
        }
        return result.OrderBy(value => value.publicFactId, StringComparer.Ordinal).ToList();
    }

    public static NarrativeFormulaEvidence ToFormulaEvidence(
        GameplayOutcomeEvidenceBindingSnapshot binding,
        NarrativeFormulaStrengthPolicy policy)
    {
        if (binding == null)
            throw new ArgumentNullException(nameof(binding));
        GameplayOutcomeEvidenceBindingAuthority.RequireStatus(binding);
        if (binding.schemaVersion != CurrentBindingSchemaVersion || binding.experience == null)
            throw new InvalidOperationException("Formula evidence requires a frozen experience assessment.");
        // Unknown/technical context must not gain invented importance from a
        // policy floor. It can still appear as an explicitly linked causal parent.
        double authoredImportance = binding.experience.importance * (policy?.MaximumImportance ?? 4d);
        double importance = Math.Min(
            policy?.MaximumImportance ?? authoredImportance,
            Math.Max(policy?.MinimumImportance ?? 0d, authoredImportance));
        if (binding.experience.category == (int)GameplayExperienceCategory.Unassessed
            || binding.experience.category == (int)GameplayExperienceCategory.NotExperience) importance = 0d;
        string[] subjectRoles = (binding.participants
                ?? new List<GameplayOutcomeEvidenceParticipantBindingSnapshot>())
            .Where(value => value != null
                && string.Equals(value.entityKindId, binding.subjectKindId,
                    StringComparison.Ordinal)
                && string.Equals(value.entityId, binding.subjectId,
                    StringComparison.Ordinal))
            .Select(value => value.roleId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new NarrativeFormulaEvidence(
            binding.publicFactId,
            binding.outcomeTypeId,
            subjectRoles.Length > 0
                ? string.Join("+", subjectRoles)
                : string.Join("+", binding.roleIds ?? new List<string>()),
            string.Empty,
            DomainKey(binding.outcomeTypeId),
            Math.Max(1, binding.occurrenceCount),
            importance,
            Math.Max(0, binding.influenceUseCount));
    }

    public static bool HasCommonOrigin(
        GameplayOutcomeEvidenceBindingSnapshot binding,
        CharacterNarrativeFact legacy)
    {
        if (binding == null || legacy == null)
            return false;
        return HasCommonOrigin(binding, new[]
        {
            legacy.factId,
            legacy.lastEventContext?.eventInstanceId
        });
    }

    public static bool HasCommonOrigin(
        GameplayOutcomeEvidenceBindingSnapshot binding,
        UsageLedgerEvent legacy)
    {
        if (binding == null || legacy == null)
            return false;
        return HasCommonOrigin(binding, new[]
        {
            legacy.evidenceId,
            legacy.narrativeContext?.eventInstanceId
        });
    }

    private static bool HasCommonOrigin(
        GameplayOutcomeEvidenceBindingSnapshot binding,
        IEnumerable<string> legacyOriginValues)
    {
        if (binding == null)
            return false;
        string[] legacyOrigins = (legacyOriginValues ?? Array.Empty<string>())
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        HashSet<string> exactOrigins = CollectOriginIds(new[] { binding });
        return legacyOrigins.Any(exactOrigins.Contains);
    }

    // This is a budget projection, not a rewritten narrative fact. The original
    // count, text, values, last context and persisted influence history stay intact.
    public static IReadOnlyDictionary<CharacterNarrativeFact, int> GetRemainingLegacyMilestoneCounts(
        IEnumerable<CharacterNarrativeFact> facts,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> bindings)
    {
        HashSet<string> represented = CollectOriginIds(bindings);
        Dictionary<CharacterNarrativeFact, int> result = new();
        foreach (CharacterNarrativeFact fact in facts ?? Array.Empty<CharacterNarrativeFact>())
        {
            if (fact == null) continue;
            if (!fact.HasCompleteSourceEventIds)
                throw new InvalidOperationException(
                    $"Narrative fact '{fact.factId}' has incomplete source occurrence identities.");
            int remaining = fact.sourceEventIds.Count(id => id.Length == 0 || !represented.Contains(id));
            int milestones = CharacterAcquiredTraitMeaningfulLedgerValidator.RecomputeMilestoneCount(remaining);
            if (milestones > 0) result.Add(fact, milestones);
        }
        return result;
    }

    private static HashSet<string> CollectOriginIds(
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> bindings)
    {
        HashSet<string> exactOrigins = new(StringComparer.Ordinal);
        foreach (GameplayOutcomeEvidenceBindingSnapshot binding in
                 bindings ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
        {
            if (binding == null) continue;
            exactOrigins.Add(binding.publicFactId?.Trim() ?? string.Empty);
            exactOrigins.Add(binding.operationId?.Trim() ?? string.Empty);
            exactOrigins.Add(binding.compactedMemoryId?.Trim() ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(binding.outcomeRunId)
                && binding.outcomeSequence > 0L)
            {
                exactOrigins.Add(new GameplayOutcomeId(
                    new GameplayOutcomeRunId(binding.outcomeRunId),
                    binding.outcomeSequence).ToString());
            }
            foreach (GameplayOutcomeEvidenceProvenanceBindingSnapshot provenance in
                     binding.provenance
                     ?? new List<GameplayOutcomeEvidenceProvenanceBindingSnapshot>())
            {
                // Only explicit receipt operation aliases identify the same event.
                // Shared facilities, contracts and causal ancestors do not.
                if (provenance != null
                    && (string.Equals(provenance.kindId, "receipt", StringComparison.Ordinal)
                        || string.Equals(provenance.kindId, "domain-commit", StringComparison.Ordinal)))
                    exactOrigins.Add(provenance.value?.Trim() ?? string.Empty);
            }
            foreach (GameplayOutcomeEvidenceFactBindingSnapshot fact in
                     binding.facts
                     ?? new List<GameplayOutcomeEvidenceFactBindingSnapshot>())
            {
                if (fact != null
                    && string.Equals(fact.factId, "operation-id", StringComparison.Ordinal))
                    exactOrigins.Add(fact.value?.Trim() ?? string.Empty);
            }
        }
        exactOrigins.Remove(string.Empty);
        return exactOrigins;
    }

    public static bool IsNegative(GameplayOutcomeEvidenceBindingSnapshot binding)
    {
        if (binding == null)
            return false;
        GameplayOutcomeStatus status =
            GameplayOutcomeEvidenceBindingAuthority.RequireStatus(binding);
        if (binding.experience != null && (binding.experience.category == (int)GameplayExperienceCategory.NotExperience
            || binding.experience.category == (int)GameplayExperienceCategory.Unassessed)) return false;
        return status is GameplayOutcomeStatus.Failed
                or GameplayOutcomeStatus.Blocked
                or GameplayOutcomeStatus.Cancelled
            || NarrativeFormulaNegativeEvidence.Matches(
                binding.outcomeTypeId,
                binding.canonicalFactText,
                string.Join("|", binding.factIds ?? new List<string>()));
    }

    public static List<GameplayOutcomeEvidenceBindingSnapshot> Select(
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> bindings,
        IEnumerable<string> publicFactIds)
    {
        Dictionary<string, GameplayOutcomeEvidenceBindingSnapshot> index =
            (bindings ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .ToDictionary(value => value.publicFactId, StringComparer.Ordinal);
        List<GameplayOutcomeEvidenceBindingSnapshot> selected = new();
        foreach (string id in publicFactIds ?? Array.Empty<string>())
        {
            if (!index.TryGetValue(id ?? string.Empty, out var binding))
                continue;
            selected.Add(binding.Clone());
        }
        return selected;
    }

    public static void AppendPromptFacts(
        StringBuilder builder,
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> bindings)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        GameplayOutcomeEvidenceBindingSnapshot[] values = (bindings
                ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .OrderBy(value => value.publicFactId, StringComparer.Ordinal)
            .ToArray();
        builder.AppendLine("authoritativeOutcomeEvidence:");
        if (values.Length == 0)
        {
            builder.AppendLine("- none");
            return;
        }
        Dictionary<(string outcome, string kind, string subject), string> publicParents = new();
        foreach (GameplayOutcomeEvidenceBindingSnapshot value in values)
        {
            if (string.Equals(GameplayOutcomeEvidenceBindingAuthority.EffectiveSourceKind(value),
                    "exact", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(value.outcomeRunId)
                && value.outcomeSequence > 0L)
            {
                string outcomeId = new GameplayOutcomeId(
                    new GameplayOutcomeRunId(value.outcomeRunId), value.outcomeSequence).ToString();
                publicParents[(outcomeId, value.subjectKindId, value.subjectId)] = value.publicFactId;
            }
        }
        foreach (GameplayOutcomeEvidenceBindingSnapshot value in values)
        {
            string sourceKind = GameplayOutcomeEvidenceBindingAuthority
                .EffectiveSourceKind(value);
            builder.Append("- id=").Append(value.publicFactId)
                .Append("; sourceKind=").Append(sourceKind)
                .Append("; selectable=").Append(
                    string.Equals(sourceKind, "exact", StringComparison.Ordinal)
                        ? "true"
                        : "false-context-only")
                .Append("; outcomeType=").Append(value.outcomeTypeId)
                .Append("; subject=").Append(value.subjectKindId)
                .Append(':').Append(value.subjectId)
                .Append("; days=").Append(value.firstDay)
                .Append('-').Append(value.lastDay)
                .Append("; roles=").Append(string.Join(",", value.roleIds))
                .Append("; metrics=").Append(string.Join(",", (value.metrics
                    ?? new List<GameplayOutcomeEvidenceMetricBindingSnapshot>())
                    .Where(metric => metric != null)
                    .Select(metric => metric.metricId + "="
                        + metric.value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                        + " " + metric.unitId)));
            // Internal lineage stays in the binding. Public packets reference
            // only evidence actually included for this same subject.
            if (!string.IsNullOrWhiteSpace(value.parentOutcomeId)
                && publicParents.TryGetValue(
                    (value.parentOutcomeId, value.subjectKindId, value.subjectId), out string publicParent))
                builder.Append("; cause=").Append(publicParent);
            if (value.schemaVersion >= 3)
                builder.Append("; experience=").Append(GameplayExperienceImportancePolicy.Describe(value.experience));
            builder.Append("; fact=").AppendLine(value.canonicalFactText);
        }
    }

    private static string DomainKey(string outcomeTypeId)
    {
        string value = outcomeTypeId?.Trim() ?? string.Empty;
        int separator = value.IndexOfAny(new[] { '.', ':', '/' });
        return separator > 0 ? value.Substring(0, separator) : value;
    }

    private static List<string> CanonicalDistinct(IEnumerable<string> source) =>
        (source ?? Array.Empty<string>())
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value.Trim())
        .Distinct(StringComparer.Ordinal)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToList();
}
