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
    private readonly GameplayExperienceObserverRegistry experienceObservers;

    public GameplayOutcomeNarrativeEvidenceQuery(IGameplayOutcomeQuery query, IGameplayOutcomeRegistry registry = null)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        experienceObservers = registry is IGameplayActionLogRegistry actions && actions.ExperienceObservers != null
            ? actions.ExperienceObservers : GameplayExperienceObserverRegistration.Default;
        if (registry != null)
        {
            string[] missing = registry.RegisteredOutcomeTypes.Select(value => value.Value)
                .Except(experienceObservers.SupportedOutcomeTypes, StringComparer.Ordinal).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException("Missing experience policies: " + string.Join(",", missing));
        }
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
            GameplayEntityId subject = new(
                new GameplayEntityKindId(binding.subjectKindId),
                binding.subjectId);
            if (!TryResolvePerspectiveKind(subject.Kind, out var perspectiveKind))
                return false;
            NarrativePerspectiveContext perspective = new(
                subject, perspectiveKind, "ko-KR");
            if (string.Equals(
                    GameplayOutcomeEvidenceBindingAuthority.EffectiveSourceKind(binding),
                    "compacted",
                    StringComparison.Ordinal))
            {
                NarrativeMemoryId memoryId = new(binding.compactedMemoryId);
                if (!query.TryGetCompacted(memoryId, out var memory)
                    || memory == null
                    || !string.Equals(memory.subjectKindId, subject.Kind.Value,
                        StringComparison.Ordinal)
                    || !string.Equals(memory.subjectId, subject.Value,
                        StringComparison.Ordinal)
                    || !TryBuildCompacted(memory, perspective, out source))
                    return false;
            }
            else
            {
                GameplayOutcomeId id = new(
                    new GameplayOutcomeRunId(binding.outcomeRunId),
                    binding.outcomeSequence);
                if (!query.TryGetExact(id, out GameplayOutcomeSnapshot snapshot)
                    || snapshot == null
                    || snapshot.storageTier == NarrativeMemoryTier.Forgotten
                    || !ContainsSubject(snapshot.subjects, subject)
                    || !TryBuildExact(snapshot, perspective, out source))
                    return false;
            }
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
            || !SourceIdentityMatches(binding, source)
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

        if (!SameCanonical(binding.roleIds, source.Participants.Select(value => value.RoleId))
            || !SameCanonical(binding.metricIds, source.Metrics.Select(value => value.MetricId))
            || !SameCanonical(
                binding.metricReferenceIds,
                source.Metrics.Select(value => string.IsNullOrWhiteSpace(value.ReferenceKindId)
                    ? string.Empty
                    : value.ReferenceKindId + ":" + value.ReferenceId))
            || !SameCanonical(binding.factIds, source.Facts.Select(value => value.FactId))
            || !SameCanonical(binding.semanticTags, source.Tags))
            return false;

        int version = binding.schemaVersion <= 1 ? 1 : binding.schemaVersion;
        return version == 1
            || binding.perspectiveKind == (int)source.PerspectiveKind
            && binding.firstDay == source.FirstDay
            && binding.lastDay == source.LastDay
            && binding.occurrenceCount == source.Count
            && string.Equals(binding.locationId, source.Location.LocationId,
                StringComparison.Ordinal)
            && string.Equals(binding.roomId, source.Location.RoomId,
                StringComparison.Ordinal)
            && binding.locationX == source.Location.X
            && binding.locationY == source.Location.Y
            && string.Equals(binding.operationId, source.Causation.OperationId,
                StringComparison.Ordinal)
            && string.Equals(binding.parentOutcomeId, source.Causation.ParentOutcomeId,
                StringComparison.Ordinal)
            && string.Equals(binding.rootOperationId, source.Causation.RootOperationId,
                StringComparison.Ordinal)
            && string.Equals(binding.causationRelationId, source.Causation.RelationId,
                StringComparison.Ordinal)
            && SameParticipants(binding.participants, source.Participants)
            && SameMetrics(binding.metrics, source.Metrics)
            && SameFacts(binding.facts, source.Facts)
            && SameProvenance(binding.provenance, source.Provenance);
    }

    private static bool SourceIdentityMatches(
        GameplayOutcomeEvidenceBindingSnapshot binding,
        GameplayOutcomeNarrativeEvidenceSource source)
    {
        bool compacted = string.Equals(
            GameplayOutcomeEvidenceBindingAuthority.EffectiveSourceKind(binding),
            "compacted",
            StringComparison.Ordinal);
        return compacted == source.IsCompacted
            && (compacted
                ? string.Equals(binding.compactedMemoryId,
                    source.CompactedMemoryId.Value, StringComparison.Ordinal)
                : string.Equals(binding.outcomeRunId,
                    source.ExactOutcomeId.RunId.Value, StringComparison.Ordinal)
                  && binding.outcomeSequence == source.ExactOutcomeId.Sequence);
    }

    private static bool SameParticipants(
        IEnumerable<GameplayOutcomeEvidenceParticipantBindingSnapshot> expected,
        IEnumerable<GameplayOutcomeNarrativeEvidenceParticipant> actual) =>
        Canonical(expected, value => value == null ? "<null>" : string.Join("\u001f",
                value.entityKindId, value.entityId, value.roleId,
                value.participationKind, value.hasPerceptionEvidence,
                value.displayText, value.displayRevision, value.locale,
                value.pronunciationMode, value.pronunciationValue,
                value.finalConsonant, value.pronunciationRevision))
            .SequenceEqual(Canonical(actual, value => string.Join("\u001f",
                value.EntityKindId, value.EntityId, value.RoleId,
                (int)value.ParticipationKind, value.HasPerceptionEvidence,
                value.DisplayName.DisplayText,
                value.DisplayName.DisplaySnapshotRevision,
                value.DisplayName.Locale,
                (int)value.DisplayName.PronunciationHint.Mode,
                value.DisplayName.PronunciationHint.Value,
                (int)value.DisplayName.PronunciationHint.ExplicitFinalConsonant,
                value.DisplayName.PronunciationHint.Revision)),
                StringComparer.Ordinal);

    private static bool SameMetrics(
        IEnumerable<GameplayOutcomeEvidenceMetricBindingSnapshot> expected,
        IEnumerable<GameplayOutcomeNarrativeEvidenceMetric> actual) =>
        Canonical(expected, value => value == null ? "<null>" : string.Join("\u001f",
                value.metricId, value.unitId, value.referenceKindId,
                value.referenceId, Bits(value.value), Bits(value.minimum),
                Bits(value.maximum), value.sampleCount))
            .SequenceEqual(Canonical(actual, value => string.Join("\u001f",
                value.MetricId, value.UnitId, value.ReferenceKindId,
                value.ReferenceId, Bits(value.Value), Bits(value.Minimum),
                Bits(value.Maximum), value.SampleCount)), StringComparer.Ordinal);

    private static bool SameFacts(
        IEnumerable<GameplayOutcomeEvidenceFactBindingSnapshot> expected,
        IEnumerable<GameplayOutcomeNarrativeEvidenceFact> actual) =>
        Canonical(expected, value => value == null ? "<null>" :
                value.factId + "\u001f" + value.value)
            .SequenceEqual(Canonical(actual, value =>
                value.FactId + "\u001f" + value.Value), StringComparer.Ordinal);

    private static bool SameProvenance(
        IEnumerable<GameplayOutcomeEvidenceProvenanceBindingSnapshot> expected,
        IEnumerable<GameplayOutcomeNarrativeEvidenceProvenance> actual) =>
        Canonical(expected, value => value == null ? "<null>" :
                value.kindId + "\u001f" + value.value)
            .SequenceEqual(Canonical(actual, value =>
                value.KindId + "\u001f" + value.Value), StringComparer.Ordinal);

    private static string[] Canonical<T>(
        IEnumerable<T> values,
        Func<T, string> projector) =>
        (values ?? Array.Empty<T>())
            .Select(projector)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);

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
        if (float.IsNaN(minimumSalience) || float.IsInfinity(minimumSalience)
            || minimumSalience < 0f || minimumSalience > 1f)
            throw new ArgumentOutOfRangeException(nameof(minimumSalience));
        int requestedCount = Math.Min(100, Math.Max(1, maximumCount));
        var episodes = GetExperiencesForEntity(subject, includeCompacted);
        // One representative per observed experience. Do not refill the request
        // with its other ticks: that would count the same experience repeatedly
        // in downstream formula budgets. Full members remain queryable in episodes.
        var heads = new List<GameplayOutcomeNarrativeEvidenceSource>();
        var assessedHeads = new HashSet<string>(StringComparer.Ordinal);
        foreach (var episode in episodes)
        {
            var eligible = episode.Members.Select(value => new
                { Source = value, Assessment = episode.AssessmentFor(value.SourceId) })
                .Where(value => value.Assessment.category != (int)GameplayExperienceCategory.NotExperience)
                .Where(value => minimumSalience == 0f || value.Assessment.category != 0
                    && value.Assessment.importance >= minimumSalience)
                .OrderByDescending(value => value.Assessment.importance)
                .ThenByDescending(value => value.Source.HasExactOutcome ? value.Source.ExactOutcomeId.Sequence : 0L)
                .Select(value => value.Source).ToArray();
            if (eligible.Length == 0) continue;
            heads.Add(eligible[0]);
            if (episode.AssessmentFor(eligible[0].SourceId).category != 0)
                assessedHeads.Add(eligible[0].SourceId);
        }
        // Unknown is not "unimportant". Reserve an explicit context lane instead
        // of giving it an invented salience or letting old pins rank it as intense.
        var known = new Queue<GameplayOutcomeNarrativeEvidenceSource>(heads.Where(value => assessedHeads.Contains(value.SourceId)));
        var unknown = new Queue<GameplayOutcomeNarrativeEvidenceSource>(heads.Where(value => !assessedHeads.Contains(value.SourceId))
            .OrderByDescending(value => value.LastDay)
            .ThenByDescending(value => value.HasExactOutcome ? value.ExactOutcomeId.Sequence : 0L));
        var ranked = new List<GameplayOutcomeNarrativeEvidenceSource>(heads.Count);
        while (known.Count > 0 || unknown.Count > 0)
            ranked.Add(unknown.Count > 0 && (known.Count == 0 || ranked.Count % 4 == 3)
                ? unknown.Dequeue() : known.Dequeue());
        return SelectWithCausalContext(ranked, requestedCount,
            episodes.SelectMany(value => value.Members).ToArray());
    }

    public IReadOnlyList<GameplayExperienceEpisode> GetExperiencesForEntity(
        GameplayEntityId subject, bool includeCompacted)
    {
        var result = ReadVisibleEvidence(subject, includeCompacted);
        return GameplayExperienceProjection.Build(result, experienceObservers);
    }

    private IReadOnlyList<GameplayOutcomeNarrativeEvidenceSource> ReadVisibleEvidence(
        GameplayEntityId subject, bool includeCompacted)
    {
        var result = new List<GameplayOutcomeNarrativeEvidenceSource>();
        if (!TryResolvePerspectiveKind(subject.Kind, out var perspectiveKind))
            return Array.Empty<GameplayOutcomeNarrativeEvidenceSource>();
        NarrativePerspectiveContext perspective = new(
            subject,
            perspectiveKind,
            "ko-KR");
        var cursor = OutcomeCursor.FirstPage(200);
        var filter = new OutcomeFilter(default, default, null, int.MinValue, int.MaxValue, includeCompacted);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            // Read by subject/cursor, not the old pinned/salience top-100 query.
            var page = query.GetForEntity(subject, cursor, filter)
                ?? throw new InvalidOperationException("Experience source page is missing.");
            for (int index = 0; index < page.Items.Count; index++)
            {
                GameplayOutcomeQueryItem item = page.Items[index];
                GameplayOutcomeNarrativeEvidenceSource evidence;
                bool built = item.IsCompacted
                    ? TryBuildCompacted(item.Compacted, perspective, out evidence)
                    : TryBuildExact(item.Exact, perspective, out evidence);
                if (!built) continue;
                if (!identities.Add(evidence.SourceId))
                    throw new InvalidOperationException("Experience source pagination repeated a fact.");
                result.Add(evidence);
            }
            if (page.Items.Count < cursor.Limit) break;
            if (page.NextCursor.BeforeSequenceExclusive == cursor.BeforeSequenceExclusive
                && page.NextCursor.Kind == cursor.Kind && page.NextCursor.StableId == cursor.StableId)
                throw new InvalidOperationException("Experience source pagination did not advance.");
            cursor = page.NextCursor;
        }
        return result;
    }

    private static IReadOnlyList<GameplayOutcomeNarrativeEvidenceSource>
        SelectWithCausalContext(
            IReadOnlyList<GameplayOutcomeNarrativeEvidenceSource> candidates,
            int maximumCount,
            IReadOnlyList<GameplayOutcomeNarrativeEvidenceSource> visibleContext)
    {
        if (candidates == null || candidates.Count == 0)
            return candidates ?? Array.Empty<GameplayOutcomeNarrativeEvidenceSource>();
        Dictionary<string, GameplayOutcomeNarrativeEvidenceSource> exactById =
            visibleContext
                .Where(value => value.HasExactOutcome)
                .GroupBy(value => value.ExactOutcomeId.ToString(), StringComparer.Ordinal)
                .ToDictionary(value => value.Key, value => value.First(),
                    StringComparer.Ordinal);
        List<GameplayOutcomeNarrativeEvidenceSource> selected = new(maximumCount);
        HashSet<string> selectedIds = new(StringComparer.Ordinal);
        foreach (GameplayOutcomeNarrativeEvidenceSource candidate in candidates)
        {
            List<GameplayOutcomeNarrativeEvidenceSource> bundle = new();
            CollectVisibleAncestors(candidate, exactById, selectedIds,
                new HashSet<string>(StringComparer.Ordinal), bundle);
            if (selected.Count + bundle.Count > maximumCount)
                continue;
            foreach (GameplayOutcomeNarrativeEvidenceSource value in bundle)
            {
                if (selectedIds.Add(value.SourceId))
                    selected.Add(value);
            }
            if (selected.Count == maximumCount)
                break;
        }
        return selected;
    }

    private static void CollectVisibleAncestors(
        GameplayOutcomeNarrativeEvidenceSource candidate,
        IReadOnlyDictionary<string, GameplayOutcomeNarrativeEvidenceSource> exactById,
        ISet<string> alreadySelected,
        ISet<string> visiting,
        ICollection<GameplayOutcomeNarrativeEvidenceSource> bundle)
    {
        if (alreadySelected.Contains(candidate.SourceId)
            || !visiting.Add(candidate.SourceId))
            return;
        string parentId = candidate.Causation.ParentOutcomeId;
        if (!string.IsNullOrWhiteSpace(parentId)
            && exactById.TryGetValue(parentId, out var parent))
            CollectVisibleAncestors(parent, exactById, alreadySelected, visiting, bundle);
        bundle.Add(candidate);
        visiting.Remove(candidate.SourceId);
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
            perspective.Kind,
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
            perspective.Kind,
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

    internal static bool TryResolvePerspectiveKind(
        GameplayEntityKindId kind,
        out NarrativePerspectiveKind perspectiveKind)
    {
        perspectiveKind = kind.Value switch
        {
            "character" => NarrativePerspectiveKind.Character,
            "facility" or "building" => NarrativePerspectiveKind.Facility,
            "item-instance" or "equipment" or "item-definition" =>
                NarrativePerspectiveKind.Equipment,
            "expedition" => NarrativePerspectiveKind.Expedition,
            // These authority subjects intentionally use their descriptor's
            // global projection. The ledger query already enforced subject
            // visibility before reaching this branch.
            "research-project" or "recipe" or "faction" or "route"
                or "world-location" or "ability" or "wildlife"
                or "knowledge-task" or "codex-entry" or "region"
                or "operation" => NarrativePerspectiveKind.Global,
            _ => 0
        };
        return perspectiveKind != 0;
    }
}
