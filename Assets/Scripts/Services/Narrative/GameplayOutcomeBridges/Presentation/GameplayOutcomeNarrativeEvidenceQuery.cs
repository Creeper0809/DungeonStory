using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Narrative.Korean;

/// <summary>
/// Read-only projection of eligible ledger rows for narrative request builders.
/// It never derives mechanics from prose and never rehydrates forgotten rows.
/// </summary>
public sealed class GameplayOutcomeNarrativeEvidenceQuery :
    IGameplayOutcomeNarrativeEvidenceQuery
{
    private static readonly GameplayEntityKindId CharacterKind = new("character");
    private readonly IGameplayOutcomeQuery query;

    public GameplayOutcomeNarrativeEvidenceQuery(IGameplayOutcomeQuery query)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
    }

    public bool TryResolveExactBinding(
        GameplayOutcomeEvidenceBindingSnapshot binding,
        out GameplayOutcomeNarrativeEvidenceSource source)
    {
        source = default;
        if (!GameplayOutcomeEvidenceBindingAuthority.TryValidate(
                binding,
                out _))
            return false;
        try
        {
            GameplayOutcomeId id = new(
                new GameplayOutcomeRunId(binding.outcomeRunId),
                binding.outcomeSequence);
            GameplayEntityId subject = new(
                new GameplayEntityKindId(binding.subjectKindId),
                binding.subjectId);
            if (!query.TryGetExact(id, out GameplayOutcomeSnapshot snapshot)
                || snapshot == null
                || snapshot.storageTier == NarrativeMemoryTier.Forgotten
                || !ContainsSubject(snapshot.subjects, subject)
                || !TryBuildExact(
                    snapshot,
                    new NarrativePerspectiveContext(
                        subject, NarrativePerspectiveKind.Character, "ko-KR"),
                    out source))
                return false;
            return BindingMatchesSource(binding, source);
        }
        catch (ArgumentException)
        {
            source = default;
            return false;
        }
    }

    private static bool BindingMatchesSource(
        GameplayOutcomeEvidenceBindingSnapshot binding,
        GameplayOutcomeNarrativeEvidenceSource source)
    {
        if (!string.Equals(binding.publicFactId, source.SourceId, StringComparison.Ordinal)
            || !string.Equals(binding.outcomeTypeId, source.OutcomeTypeId, StringComparison.Ordinal)
            || !string.Equals(binding.outcomeRunId, source.ExactOutcomeId.RunId.Value,
                StringComparison.Ordinal)
            || binding.outcomeSequence != source.ExactOutcomeId.Sequence
            || !string.Equals(binding.subjectKindId, source.SubjectId.Kind.Value,
                StringComparison.Ordinal)
            || !string.Equals(binding.subjectId, source.SubjectId.Value,
                StringComparison.Ordinal)
            || binding.anchorRevision != source.AnchorRevision
            || binding.status != (int)source.Status
            || Math.Abs(binding.subjectSalience - source.SubjectSalience) > 0.0001f
            || binding.influenceUseCount != source.InfluenceUseCount
            || binding.influenceRevision != source.InfluenceRevision
            || !string.Equals(binding.canonicalFactText, source.CanonicalFactText,
                StringComparison.Ordinal))
            return false;

        return SameCanonical(binding.roleIds, source.Participants.Select(value => value.RoleId))
            && SameCanonical(binding.metricIds, source.Metrics.Select(value => value.MetricId))
            && SameCanonical(
                binding.metricReferenceIds,
                source.Metrics.Select(value => string.IsNullOrWhiteSpace(value.ReferenceKindId)
                    ? string.Empty
                    : value.ReferenceKindId + ":" + value.ReferenceId))
            && SameCanonical(binding.factIds, source.Facts.Select(value => value.FactId))
            && SameCanonical(binding.semanticTags, source.Tags);
    }

    private static bool SameCanonical(
        IReadOnlyList<string> expected,
        IEnumerable<string> actual)
    {
        string[] left = (expected ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] right = (actual ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return left.SequenceEqual(right, StringComparer.Ordinal);
    }

    private static bool ContainsSubject(
        IReadOnlyList<GameplayOutcomeSubjectSnapshot> subjects,
        GameplayEntityId subject)
    {
        for (int index = 0; index < (subjects?.Count ?? 0); index++)
        {
            GameplayOutcomeSubjectSnapshot candidate = subjects[index];
            if (string.Equals(candidate.entityKindId, subject.Kind.Value,
                    StringComparison.Ordinal)
                && string.Equals(candidate.entityId, subject.Value,
                    StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public IReadOnlyList<GameplayOutcomeNarrativeEvidenceSource> GetForCharacter(
        string characterPersistentId,
        int maximumCount,
        float minimumSalience,
        bool includeCompacted)
    {
        string normalized = characterPersistentId?.Trim() ?? string.Empty;
        if (!GameplayOutcomeStableIdSyntax.IsValid(normalized))
            return Array.Empty<GameplayOutcomeNarrativeEvidenceSource>();

        return GetForEntity(
            new GameplayEntityId(CharacterKind, normalized),
            maximumCount,
            minimumSalience,
            includeCompacted);
    }

    public IReadOnlyList<GameplayOutcomeNarrativeEvidenceSource> GetForEntity(
        GameplayEntityId subject,
        int maximumCount,
        float minimumSalience,
        bool includeCompacted)
    {
        if (!subject.IsValid)
            return Array.Empty<GameplayOutcomeNarrativeEvidenceSource>();
        GameplayOutcomeQueryPage page = query.GetNarrativeEvidence(
            new NarrativeEvidenceQuery(
                subject,
                maximumCount,
                minimumSalience,
                includeCompacted,
                default));
        if (page == null || page.Items.Count == 0)
            return Array.Empty<GameplayOutcomeNarrativeEvidenceSource>();

        List<GameplayOutcomeNarrativeEvidenceSource> result =
            new(page.Items.Count);
        NarrativePerspectiveContext perspective = new(
            subject,
            NarrativePerspectiveKind.Character,
            "ko-KR");
        for (int index = 0; index < page.Items.Count; index++)
        {
            GameplayOutcomeQueryItem item = page.Items[index];
            if (item.IsCompacted)
            {
                if (TryBuildCompacted(item.Compacted, perspective, out var compacted))
                    result.Add(compacted);
            }
            else if (TryBuildExact(item.Exact, perspective, out var exact))
            {
                result.Add(exact);
            }
        }
        return result;
    }

    private bool TryBuildExact(
        GameplayOutcomeSnapshot source,
        NarrativePerspectiveContext perspective,
        out GameplayOutcomeNarrativeEvidenceSource evidence)
    {
        evidence = default;
        if (source == null
            || source.storageTier == NarrativeMemoryTier.Forgotten
            || !GameplayOutcomeStableIdSyntax.IsValid(source.runId)
            || source.sequence <= 0L)
            return false;

        GameplayOutcomeId id = new(
            new GameplayOutcomeRunId(source.runId), source.sequence);
        if (!query.TryProject(id, perspective, out NarrativeView view)
            || string.IsNullOrWhiteSpace(view.Text))
            return false;

        evidence = new GameplayOutcomeNarrativeEvidenceSource(
            BuildPublicFactId("exact", source.outcomeTypeId, id.ToString(), perspective.ViewerId),
            compacted: false,
            source.outcomeTypeId,
            source.absoluteDay,
            source.absoluteDay,
            1,
            view.Text,
            source.status,
            FindSubjectSalience(source.subjects, perspective.ViewerId),
            FindInfluenceUseCount(source.subjects, perspective.ViewerId),
            FindInfluenceRevision(source.subjects, perspective.ViewerId),
            source.tags == null ? Array.Empty<string>() : source.tags.ToArray(),
            id,
            default,
            perspective.ViewerId,
            CopyParticipants(source.participants),
            CopyMetrics(source.metrics),
            CopyFacts(source.facts),
            CopyProvenance(source.provenance),
            new GameplayOutcomeNarrativeEvidenceLocation(
                source.locationId,
                source.roomId,
                source.locationX,
                source.locationY),
            new GameplayOutcomeNarrativeEvidenceCausation(
                source.operationId,
                FormatParent(source.parentRunId, source.parentSequence),
                source.rootOperationId,
                source.causationRelationId),
            source.anchorRevision);
        return true;
    }

    private bool TryBuildCompacted(
        CompactedNarrativeMemorySnapshot source,
        NarrativePerspectiveContext perspective,
        out GameplayOutcomeNarrativeEvidenceSource evidence)
    {
        evidence = default;
        if (source == null
            || !GameplayOutcomeStableIdSyntax.IsValid(source.memoryId))
            return false;
        NarrativeMemoryId id = new(source.memoryId);
        if (!query.TryProjectMemory(id, perspective, out NarrativeMemoryView view)
            || string.IsNullOrWhiteSpace(view.Text))
            return false;

        evidence = new GameplayOutcomeNarrativeEvidenceSource(
            BuildPublicFactId("compacted", source.outcomeTypeId, source.memoryId, perspective.ViewerId),
            compacted: true,
            source.outcomeTypeId,
            source.firstDay,
            source.lastDay,
            Math.Max(1, source.occurrenceCount),
            view.Text,
            source.status,
            Math.Max(0f, source.salience),
            Math.Max(0, source.influenceUseCount),
            Math.Max(0, source.influenceRevision),
            source.tags == null ? Array.Empty<string>() : source.tags.ToArray(),
            default,
            id,
            perspective.ViewerId,
            CopyParticipants(source.participants),
            CopyMetrics(source.metrics),
            CopyFacts(source.facts),
            CopyProvenance(source.provenance),
            new GameplayOutcomeNarrativeEvidenceLocation(
                source.locationId,
                source.roomId,
                source.locationX,
                source.locationY),
            default,
            0);
        return true;
    }

    private static IReadOnlyList<GameplayOutcomeNarrativeEvidenceParticipant>
        CopyParticipants(IReadOnlyList<GameplayOutcomeParticipantSnapshot> source)
    {
        int count = source?.Count ?? 0;
        if (count == 0)
            return Array.Empty<GameplayOutcomeNarrativeEvidenceParticipant>();
        GameplayOutcomeNarrativeEvidenceParticipant[] result = new
            GameplayOutcomeNarrativeEvidenceParticipant[count];
        for (int index = 0; index < count; index++)
        {
            GameplayOutcomeParticipantSnapshot value = source[index];
            result[index] = new GameplayOutcomeNarrativeEvidenceParticipant(
                value.entityKindId,
                value.entityId,
                value.roleId,
                value.participationKind,
                value.hasPerceptionEvidence,
                new KoreanNameSnapshot(
                    value.displayText,
                    value.displayRevision,
                    new KoreanPronunciationHint(
                        value.pronunciationMode,
                        value.pronunciationValue,
                        value.finalConsonant,
                        value.pronunciationRevision),
                    value.locale));
        }
        return result;
    }

    private static IReadOnlyList<GameplayOutcomeNarrativeEvidenceMetric>
        CopyMetrics(IReadOnlyList<GameplayOutcomeMetricSnapshot> source)
    {
        int count = source?.Count ?? 0;
        if (count == 0)
            return Array.Empty<GameplayOutcomeNarrativeEvidenceMetric>();
        GameplayOutcomeNarrativeEvidenceMetric[] result = new
            GameplayOutcomeNarrativeEvidenceMetric[count];
        for (int index = 0; index < count; index++)
        {
            GameplayOutcomeMetricSnapshot value = source[index];
            result[index] = new GameplayOutcomeNarrativeEvidenceMetric(
                value.metricId,
                value.unitId,
                value.referenceKindId,
                value.referenceId,
                value.value,
                value.value,
                value.value,
                1);
        }
        return result;
    }

    private static IReadOnlyList<GameplayOutcomeNarrativeEvidenceMetric>
        CopyMetrics(IReadOnlyList<GameplayOutcomeMetricAggregateSnapshot> source)
    {
        int count = source?.Count ?? 0;
        if (count == 0)
            return Array.Empty<GameplayOutcomeNarrativeEvidenceMetric>();
        GameplayOutcomeNarrativeEvidenceMetric[] result = new
            GameplayOutcomeNarrativeEvidenceMetric[count];
        for (int index = 0; index < count; index++)
        {
            GameplayOutcomeMetricAggregateSnapshot value = source[index];
            result[index] = new GameplayOutcomeNarrativeEvidenceMetric(
                value.metricId,
                value.unitId,
                value.referenceKindId,
                value.referenceId,
                value.sum,
                value.minimum,
                value.maximum,
                value.sampleCount);
        }
        return result;
    }

    private static IReadOnlyList<GameplayOutcomeNarrativeEvidenceFact>
        CopyFacts(IReadOnlyList<GameplayOutcomeFactSnapshot> source)
    {
        int count = source?.Count ?? 0;
        if (count == 0)
            return Array.Empty<GameplayOutcomeNarrativeEvidenceFact>();
        GameplayOutcomeNarrativeEvidenceFact[] result = new
            GameplayOutcomeNarrativeEvidenceFact[count];
        for (int index = 0; index < count; index++)
        {
            GameplayOutcomeFactSnapshot value = source[index];
            result[index] = new GameplayOutcomeNarrativeEvidenceFact(
                value.factId, value.value);
        }
        return result;
    }

    private static IReadOnlyList<GameplayOutcomeNarrativeEvidenceProvenance>
        CopyProvenance(
            IReadOnlyList<GameplayOutcomeProvenanceReferenceSnapshot> source)
    {
        int count = source?.Count ?? 0;
        if (count == 0)
            return Array.Empty<GameplayOutcomeNarrativeEvidenceProvenance>();
        GameplayOutcomeNarrativeEvidenceProvenance[] result = new
            GameplayOutcomeNarrativeEvidenceProvenance[count];
        for (int index = 0; index < count; index++)
        {
            GameplayOutcomeProvenanceReferenceSnapshot value = source[index];
            result[index] = new GameplayOutcomeNarrativeEvidenceProvenance(
                value.kindId, value.value);
        }
        return result;
    }

    private static string FormatParent(string runId, long sequence)
    {
        if (!GameplayOutcomeStableIdSyntax.IsValid(runId) || sequence <= 0L)
            return string.Empty;
        return new GameplayOutcomeId(
            new GameplayOutcomeRunId(runId), sequence).ToString();
    }

    private static float FindSubjectSalience(
        IReadOnlyList<GameplayOutcomeSubjectSnapshot> subjects,
        GameplayEntityId subjectId)
    {
        for (int index = 0; index < (subjects?.Count ?? 0); index++)
        {
            GameplayOutcomeSubjectSnapshot value = subjects[index];
            if (string.Equals(value.entityKindId, subjectId.Kind.Value, StringComparison.Ordinal)
                && string.Equals(value.entityId, subjectId.Value, StringComparison.Ordinal))
                return Math.Max(0f, value.salience);
        }
        return 0f;
    }

    private static int FindInfluenceUseCount(
        IReadOnlyList<GameplayOutcomeSubjectSnapshot> subjects,
        GameplayEntityId subjectId)
    {
        for (int index = 0; index < (subjects?.Count ?? 0); index++)
        {
            GameplayOutcomeSubjectSnapshot value = subjects[index];
            if (string.Equals(value.entityKindId, subjectId.Kind.Value, StringComparison.Ordinal)
                && string.Equals(value.entityId, subjectId.Value, StringComparison.Ordinal))
                return Math.Max(0, value.influenceUseCount);
        }
        return 0;
    }

    private static int FindInfluenceRevision(
        IReadOnlyList<GameplayOutcomeSubjectSnapshot> subjects,
        GameplayEntityId subjectId)
    {
        for (int index = 0; index < (subjects?.Count ?? 0); index++)
        {
            GameplayOutcomeSubjectSnapshot value = subjects[index];
            if (string.Equals(value.entityKindId, subjectId.Kind.Value,
                    StringComparison.Ordinal)
                && string.Equals(value.entityId, subjectId.Value,
                    StringComparison.Ordinal))
            {
                return Math.Max(0, value.influenceRevision);
            }
        }
        return 0;
    }

    private static string BuildPublicFactId(
        string sourceKind,
        string outcomeTypeId,
        string sourceIdentity,
        GameplayEntityId subjectId)
    {
        return NarrativePublicContextFactory.BuildProjectedFactId(
            "GameplayOutcome:" + sourceKind + ":" + outcomeTypeId,
            sourceIdentity,
            subjectId.Value);
    }
}
