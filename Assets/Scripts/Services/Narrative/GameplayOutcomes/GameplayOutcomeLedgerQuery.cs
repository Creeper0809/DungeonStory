using System;
using System.Collections.Generic;

public sealed partial class GameplayOutcomeLedger
{
    // Query DTO/list allocations are intentional read-side costs. The hot
    // recorder and delivery paths do not call these projection methods.
    public GameplayOutcomeQueryPage GetGlobal(OutcomeCursor cursor, OutcomeFilter filter)
    {
        lock (gate)
            return BuildQueryPage(default, default, cursor, filter, entityOnly: false, operationOnly: false);
    }

    public GameplayOutcomeQueryPage GetForEntity(
        GameplayEntityId id,
        OutcomeCursor cursor,
        OutcomeFilter filter)
    {
        if (!id.IsValid)
            return new GameplayOutcomeQueryPage(Array.Empty<GameplayOutcomeQueryItem>(), OutcomeCursor.FirstPage(cursor.Limit));
        lock (gate)
            return BuildQueryPage(id, default, cursor, filter, entityOnly: true, operationOnly: false);
    }

    public GameplayOutcomeQueryPage GetForOperation(GameplayOperationId id)
    {
        if (!id.IsValid)
            return new GameplayOutcomeQueryPage(Array.Empty<GameplayOutcomeQueryItem>(), OutcomeCursor.FirstPage());
        lock (gate)
            return BuildQueryPage(default, id, OutcomeCursor.FirstPage(200), OutcomeFilter.All, entityOnly: false, operationOnly: true);
    }

    public GameplayOutcomeQueryPage GetNarrativeEvidence(NarrativeEvidenceQuery query)
    {
        if (!query.SubjectId.IsValid)
            return new GameplayOutcomeQueryPage(Array.Empty<GameplayOutcomeQueryItem>(), OutcomeCursor.FirstPage(query.MaximumCount));
        lock (gate)
        {
            List<EvidenceCandidate> candidates = new List<EvidenceCandidate>();
            for (int slot = state.PublishedPageIndices.Count - 1; slot >= 0; slot--)
            {
                int pageIndex = state.PublishedPageIndices[slot];
                if (pageIndex < 0)
                    continue;
                GameplayOutcomeValuePage page = state.Pool.Get(pageIndex);
                if (!IsPublished(page) || page.StorageTier == NarrativeMemoryTier.Forgotten)
                    continue;
                if (query.OutcomeTypeId.IsValid && page.OutcomeTypeId != query.OutcomeTypeId)
                    continue;
                if (!TryGetSubject(page, query.SubjectId, out GameplayOutcomeSubjectLink subject)
                    || !GameplayOutcomeVisibilityRules.IsExactSubjectTierVisible(subject.Tier)
                    || subject.Salience < query.MinimumSalience)
                    continue;
                candidates.Add(new EvidenceCandidate(
                    new GameplayOutcomeQueryItem(GameplayOutcomeSnapshotCodec.Capture(page)),
                    subject.Salience,
                    HasAnchorFor(page, query.SubjectId)));
            }
            if (query.IncludeCompacted)
            {
                for (int index = 0; index < state.CompactedMemories.Count; index++)
                {
                    CompactedNarrativeMemorySnapshot memory = state.CompactedMemories[index];
                    if (MatchesEntity(memory, query.SubjectId)
                        && memory.salience >= query.MinimumSalience
                        && (!query.OutcomeTypeId.IsValid
                            || string.Equals(memory.outcomeTypeId, query.OutcomeTypeId.Value, StringComparison.Ordinal)))
                    {
                        candidates.Add(new EvidenceCandidate(
                            new GameplayOutcomeQueryItem(CloneCompacted(memory)),
                            memory.salience,
                            anchored: false));
                    }
                }
            }
            candidates.Sort(CompareEvidenceCandidates);
            int count = Math.Min(query.MaximumCount, candidates.Count);
            GameplayOutcomeQueryItem[] result = new GameplayOutcomeQueryItem[count];
            for (int index = 0; index < count; index++)
                result[index] = candidates[index].Item;
            OutcomeCursor next = count > 0
                ? CursorAfter(result[count - 1], query.MaximumCount)
                : OutcomeCursor.FirstPage(query.MaximumCount);
            return new GameplayOutcomeQueryPage(result, next);
        }
    }

    public bool TryGetExact(GameplayOutcomeId id, out GameplayOutcomeSnapshot outcome)
    {
        lock (gate)
        {
            if (id.IsValid
                && state.ActiveByOutcomeId.TryGetValue(id, out int pageIndex))
            {
                GameplayOutcomeValuePage page = state.Pool.Get(pageIndex);
                if (IsPublished(page) && page.StorageTier != NarrativeMemoryTier.Forgotten)
                {
                    outcome = GameplayOutcomeSnapshotCodec.Capture(page);
                    return true;
                }
            }
            outcome = null;
            return false;
        }
    }

    public bool TryProject(
        GameplayOutcomeId id,
        NarrativePerspectiveContext perspective,
        out NarrativeView view)
    {
        lock (gate)
        {
            if (!id.IsValid
                || !state.ActiveByOutcomeId.TryGetValue(id, out int pageIndex))
            {
                view = default;
                return false;
            }
            GameplayOutcomeValuePage page = state.Pool.Get(pageIndex);
            if (!IsPublished(page)
                || page.StorageTier == NarrativeMemoryTier.Forgotten
                || perspective.Kind != NarrativePerspectiveKind.Global
                    && (!perspective.ViewerId.IsValid
                        || !HasVisibleSubject(page, perspective.ViewerId))
                || !registry.TryGetDescriptor(page.OutcomeTypeId, out IGameplayOutcomeDescriptor descriptor))
            {
                view = default;
                return false;
            }
            view = descriptor.PerspectiveProjector.Project(new GameplayOutcomeReadView(page), perspective);
            return view.OutcomeId == id
                && view.PerspectiveKind == perspective.Kind
                && !string.IsNullOrWhiteSpace(view.Text)
                && !string.IsNullOrWhiteSpace(view.RendererVersion)
                && (!RequiresNeutralFrame(page) || view.NeutralFrameUsed);
        }
    }

    public bool TryProjectMemory(
        NarrativeMemoryId id,
        NarrativePerspectiveContext perspective,
        out NarrativeMemoryView view)
    {
        if (!id.IsValid)
        {
            view = default;
            return false;
        }
        lock (gate)
        {
            for (int index = 0; index < state.CompactedMemories.Count; index++)
            {
                CompactedNarrativeMemorySnapshot memory = state.CompactedMemories[index];
                if (!string.Equals(memory.memoryId, id.Value, StringComparison.Ordinal))
                    continue;
                GameplayEntityId subjectId = new GameplayEntityId(
                    new GameplayEntityKindId(memory.subjectKindId),
                    memory.subjectId);
                if (perspective.Kind != NarrativePerspectiveKind.Global
                    && (!perspective.ViewerId.IsValid || perspective.ViewerId != subjectId)
                    || !registry.TryGetDescriptor(
                        new GameplayOutcomeTypeId(memory.outcomeTypeId),
                        out IGameplayOutcomeDescriptor descriptor)
                    || descriptor.PerspectiveProjector
                        is not ICompactedNarrativePerspectiveProjector projector)
                {
                    view = default;
                    return false;
                }

                view = projector.ProjectCompacted(
                    new CompactedNarrativeMemoryReadView(memory),
                    perspective);
                return view.MemoryId.Equals(id)
                    && view.PerspectiveKind == perspective.Kind
                    && !string.IsNullOrWhiteSpace(view.Text)
                    && !string.IsNullOrWhiteSpace(view.RendererVersion)
                    && (!RequiresNeutralFrame(memory) || view.NeutralFrameUsed);
            }
        }
        view = default;
        return false;
    }

    private GameplayOutcomeQueryPage BuildQueryPage(
        GameplayEntityId entityId,
        GameplayOperationId operationId,
        OutcomeCursor cursor,
        OutcomeFilter filter,
        bool entityOnly,
        bool operationOnly)
    {
        List<GameplayOutcomeQueryItem> items = new List<GameplayOutcomeQueryItem>(cursor.Limit + 16);
        HashSet<string> emittedSharedAggregates = entityOnly || operationOnly
            ? null
            : new HashSet<string>(StringComparer.Ordinal);
        for (int slot = state.PublishedPageIndices.Count - 1; slot >= 0; slot--)
        {
            int pageIndex = state.PublishedPageIndices[slot];
            if (pageIndex < 0)
                continue;
            GameplayOutcomeValuePage page = state.Pool.Get(pageIndex);
            if (!IsPublished(page)
                || !IsAfterCursor(page.Sequence, 1, page.OutcomeId.ToString(), cursor)
                || page.StorageTier == NarrativeMemoryTier.Compacted
                || page.StorageTier == NarrativeMemoryTier.Forgotten
                || !MatchesFilter(page, filter)
                || operationOnly && page.OperationId != operationId
                || entityOnly && !HasVisibleSubject(page, entityId))
                continue;
            items.Add(new GameplayOutcomeQueryItem(GameplayOutcomeSnapshotCodec.Capture(page)));
        }
        if (filter.IncludeCompacted && !operationOnly)
        {
            for (int index = 0; index < state.CompactedMemories.Count; index++)
            {
                CompactedNarrativeMemorySnapshot memory = state.CompactedMemories[index];
                if (!IsAfterCursor(memory.lastSequence, 2, memory.memoryId, cursor)
                    || entityOnly && !MatchesEntity(memory, entityId)
                    || !entityOnly && !IsCanonicalGlobalMemory(index)
                    || !MatchesFilter(memory, filter))
                    continue;
                if (emittedSharedAggregates != null
                    && !emittedSharedAggregates.Add(memory.sharedAggregateId))
                    continue;
                items.Add(new GameplayOutcomeQueryItem(CloneCompacted(memory)));
            }
        }
        items.Sort(CompareQueryItems);
        if (items.Count > cursor.Limit)
            items.RemoveRange(cursor.Limit, items.Count - cursor.Limit);
        OutcomeCursor next = items.Count > 0
            ? CursorAfter(items[items.Count - 1], cursor.Limit)
            : cursor;
        return new GameplayOutcomeQueryPage(items, next);
    }

    private static bool MatchesFilter(GameplayOutcomeValuePage page, in OutcomeFilter filter)
    {
        if (filter.OutcomeTypeId.IsValid && page.OutcomeTypeId != filter.OutcomeTypeId)
            return false;
        if (filter.Status.HasValue && page.Status != filter.Status.Value)
            return false;
        if (page.AbsoluteDay < filter.MinimumDay || page.AbsoluteDay > filter.MaximumDay)
            return false;
        if (filter.TagId.IsValid)
        {
            for (int index = 0; index < page.TagCount; index++)
            {
                if (page.Tags[index].Equals(filter.TagId))
                    return true;
            }
            return false;
        }
        return true;
    }

    private static bool MatchesFilter(CompactedNarrativeMemorySnapshot memory, in OutcomeFilter filter)
    {
        if (filter.OutcomeTypeId.IsValid
            && !string.Equals(memory.outcomeTypeId, filter.OutcomeTypeId.Value, StringComparison.Ordinal))
            return false;
        if (filter.Status.HasValue && memory.status != filter.Status.Value)
            return false;
        if (memory.lastDay < filter.MinimumDay || memory.firstDay > filter.MaximumDay)
            return false;
        if (filter.TagId.IsValid)
            return memory.tags != null && memory.tags.Contains(filter.TagId.Value);
        return true;
    }

    private static bool HasVisibleSubject(GameplayOutcomeValuePage page, GameplayEntityId entityId) =>
        TryGetSubject(page, entityId, out GameplayOutcomeSubjectLink subject)
        && GameplayOutcomeVisibilityRules.IsExactSubjectTierVisible(subject.Tier);

    private static bool TryGetSubject(
        GameplayOutcomeValuePage page,
        GameplayEntityId entityId,
        out GameplayOutcomeSubjectLink subject)
    {
        for (int index = 0; index < page.SubjectCount; index++)
        {
            if (page.Subjects[index].SubjectId == entityId)
            {
                subject = page.Subjects[index];
                return true;
            }
        }
        subject = default;
        return false;
    }

    private static bool HasAnchorFor(GameplayOutcomeValuePage page, GameplayEntityId subjectId)
    {
        for (int index = 0; index < page.AnchorCount; index++)
        {
            if (page.Anchors[index].SubjectId == subjectId)
                return true;
        }
        return false;
    }

    private static bool MatchesEntity(CompactedNarrativeMemorySnapshot memory, GameplayEntityId entityId) =>
        string.Equals(memory.subjectKindId, entityId.Kind.Value, StringComparison.Ordinal)
        && string.Equals(memory.subjectId, entityId.Value, StringComparison.Ordinal);

    private static bool RequiresNeutralFrame(CompactedNarrativeMemorySnapshot memory)
    {
        if (memory?.participants == null)
            return false;
        for (int index = 0; index < memory.participants.Count; index++)
        {
            if (memory.participants[index].pronunciationMode
                == DungeonStory.Narrative.Korean.KoreanPronunciationMode.Unknown)
                return true;
        }
        return false;
    }

    private static bool IsPublished(GameplayOutcomeValuePage page) =>
        page != null
        && (page.State == GameplayOutcomePageState.PublishedAwaitingAcknowledgement
            || page.State == GameplayOutcomePageState.PublishedAcknowledged);

    private static int CompareQueryItems(GameplayOutcomeQueryItem left, GameplayOutcomeQueryItem right)
    {
        int sequence = right.SortSequence.CompareTo(left.SortSequence);
        if (sequence != 0)
            return sequence;
        int kind = left.SortKind.CompareTo(right.SortKind);
        return kind != 0 ? kind : string.CompareOrdinal(left.StableId, right.StableId);
    }

    private static bool IsAfterCursor(
        long sequence,
        int kind,
        string stableId,
        in OutcomeCursor cursor)
    {
        if (!cursor.HasTieBreak)
            return sequence < cursor.BeforeSequenceExclusive;
        if (sequence != cursor.BeforeSequenceExclusive)
            return sequence < cursor.BeforeSequenceExclusive;
        if (kind != cursor.Kind)
            return kind > cursor.Kind;
        return string.CompareOrdinal(stableId, cursor.StableId) > 0;
    }

    private static OutcomeCursor CursorAfter(in GameplayOutcomeQueryItem item, int limit) =>
        new OutcomeCursor(item.SortSequence, item.SortKind, item.StableId, true, limit);

    private static int CompareEvidenceCandidates(EvidenceCandidate left, EvidenceCandidate right)
    {
        int anchor = right.Anchored.CompareTo(left.Anchored);
        if (anchor != 0)
            return anchor;
        int salience = right.Salience.CompareTo(left.Salience);
        if (salience != 0) return salience;
        int sequence = right.Item.SortSequence.CompareTo(left.Item.SortSequence);
        if (sequence != 0) return sequence;
        int kind = left.Item.SortKind.CompareTo(right.Item.SortKind);
        return kind != 0 ? kind : string.CompareOrdinal(left.Item.StableId, right.Item.StableId);
    }

    private static CompactedNarrativeMemorySnapshot CloneCompacted(CompactedNarrativeMemorySnapshot source)
    {
        CompactedNarrativeMemorySnapshot clone = new CompactedNarrativeMemorySnapshot
        {
            memoryId = source.memoryId,
            sharedAggregateId = source.sharedAggregateId,
            signature = source.signature,
            outcomeTypeId = source.outcomeTypeId,
            status = source.status,
            subjectKindId = source.subjectKindId,
            subjectId = source.subjectId,
            locationId = source.locationId,
            roomId = source.roomId,
            locationX = source.locationX,
            locationY = source.locationY,
            firstSequence = source.firstSequence,
            lastSequence = source.lastSequence,
            firstDay = source.firstDay,
            lastDay = source.lastDay,
            occurrenceCount = source.occurrenceCount,
            salience = source.salience,
            influenceUseCount = source.influenceUseCount,
            influenceRevision = source.influenceRevision,
            sourceSegmentHash = source.sourceSegmentHash,
            compactedPayloadHash = source.compactedPayloadHash,
            tags = source.tags != null
                ? new List<string>(source.tags)
                : new List<string>(),
            metrics = new List<GameplayOutcomeMetricAggregateSnapshot>(source.metrics?.Count ?? 0),
            participants = new List<GameplayOutcomeParticipantSnapshot>(source.participants?.Count ?? 0),
            provenance = new List<GameplayOutcomeProvenanceReferenceSnapshot>(source.provenance?.Count ?? 0),
            facts = new List<GameplayOutcomeFactSnapshot>(source.facts?.Count ?? 0)
        };
        if (source.metrics != null)
        {
            for (int index = 0; index < source.metrics.Count; index++)
            {
                GameplayOutcomeMetricAggregateSnapshot metric = source.metrics[index];
                clone.metrics.Add(new GameplayOutcomeMetricAggregateSnapshot
                {
                    metricId = metric.metricId,
                    unitId = metric.unitId,
                    referenceKindId = metric.referenceKindId,
                    referenceId = metric.referenceId,
                    sum = metric.sum,
                    minimum = metric.minimum,
                    maximum = metric.maximum,
                    sampleCount = metric.sampleCount
                });
            }
        }
        if (source.participants != null)
        {
            for (int index = 0; index < source.participants.Count; index++)
                clone.participants.Add(GameplayOutcomeSnapshotCodec.CloneParticipant(source.participants[index]));
        }
        if (source.provenance != null)
        {
            for (int index = 0; index < source.provenance.Count; index++)
            {
                GameplayOutcomeProvenanceReferenceSnapshot provenance = source.provenance[index];
                clone.provenance.Add(new GameplayOutcomeProvenanceReferenceSnapshot
                {
                    kindId = provenance.kindId,
                    value = provenance.value
                });
            }
        }
        if (source.facts != null)
        {
            for (int index = 0; index < source.facts.Count; index++)
            {
                GameplayOutcomeFactSnapshot fact = source.facts[index];
                clone.facts.Add(new GameplayOutcomeFactSnapshot
                { factId = fact.factId, value = fact.value });
            }
        }
        return clone;
    }

    private bool IsCanonicalGlobalMemory(int candidateIndex)
    {
        CompactedNarrativeMemorySnapshot candidate = state.CompactedMemories[candidateIndex];
        for (int index = 0; index < state.CompactedMemories.Count; index++)
        {
            if (index == candidateIndex)
                continue;
            CompactedNarrativeMemorySnapshot other = state.CompactedMemories[index];
            if (string.Equals(other.sharedAggregateId, candidate.sharedAggregateId, StringComparison.Ordinal)
                && string.CompareOrdinal(other.memoryId, candidate.memoryId) < 0)
                return false;
        }
        return true;
    }

    private readonly struct EvidenceCandidate
    {
        public EvidenceCandidate(GameplayOutcomeQueryItem item, float salience, bool anchored)
        {
            Item = item;
            Salience = salience;
            Anchored = anchored;
        }
        public GameplayOutcomeQueryItem Item { get; }
        public float Salience { get; }
        public bool Anchored { get; }
    }
}

internal static class GameplayOutcomeVisibilityRules
{
    internal static bool IsExactSubjectTierVisible(NarrativeMemoryTier tier) =>
        tier == NarrativeMemoryTier.Recent
        || tier == NarrativeMemoryTier.Core
        || tier == NarrativeMemoryTier.Episodic;

    internal static void ValidateExactSubjectTierContract()
    {
        if (!IsExactSubjectTierVisible(NarrativeMemoryTier.Recent)
            || !IsExactSubjectTierVisible(NarrativeMemoryTier.Core)
            || !IsExactSubjectTierVisible(NarrativeMemoryTier.Episodic)
            || IsExactSubjectTierVisible(NarrativeMemoryTier.Compacted)
            || IsExactSubjectTierVisible(NarrativeMemoryTier.Forgotten))
        {
            throw new InvalidOperationException(
                "Gameplay outcome exact-subject visibility contract is invalid.");
        }
    }
}
