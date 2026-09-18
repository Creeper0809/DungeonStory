using System;
using System.Collections.Generic;

public enum GameplayOutcomePresentationSourceKind
{
    Exact = 1,
    Compacted = 2,
    Unavailable = 3
}

public readonly struct GameplayOutcomePresentationRow
{
    public GameplayOutcomePresentationRow(
        GameplayOutcomePresentationSourceKind sourceKind,
        string sourceId,
        long sortSequence,
        int firstDay,
        int lastDay,
        string outcomeTypeId,
        string text,
        string rendererVersion,
        bool neutralFrameUsed,
        string diagnosticCode)
    {
        SourceKind = sourceKind;
        SourceId = sourceId ?? string.Empty;
        SortSequence = sortSequence;
        FirstDay = firstDay;
        LastDay = lastDay;
        OutcomeTypeId = outcomeTypeId ?? string.Empty;
        Text = text ?? string.Empty;
        RendererVersion = rendererVersion ?? string.Empty;
        NeutralFrameUsed = neutralFrameUsed;
        DiagnosticCode = diagnosticCode ?? string.Empty;
    }

    public GameplayOutcomePresentationSourceKind SourceKind { get; }
    public string SourceId { get; }
    public long SortSequence { get; }
    public int FirstDay { get; }
    public int LastDay { get; }
    public string OutcomeTypeId { get; }
    public string Text { get; }
    public string RendererVersion { get; }
    public bool NeutralFrameUsed { get; }
    public string DiagnosticCode { get; }
}

public sealed class GameplayOutcomePresentationPage
{
    public GameplayOutcomePresentationPage(
        IReadOnlyList<GameplayOutcomePresentationRow> rows,
        OutcomeCursor nextCursor,
        string heading,
        string diagnosticCode)
    {
        Rows = rows ?? Array.Empty<GameplayOutcomePresentationRow>();
        NextCursor = nextCursor;
        Heading = heading ?? string.Empty;
        DiagnosticCode = diagnosticCode ?? string.Empty;
    }

    public IReadOnlyList<GameplayOutcomePresentationRow> Rows { get; }
    public OutcomeCursor NextCursor { get; }
    public string Heading { get; }
    public string DiagnosticCode { get; }
}

public interface IGameplayOutcomePresentationQuery
{
    GameplayOutcomePresentationPage GetGlobalPage(
        OutcomeCursor cursor,
        OutcomeFilter filter,
        string locale = "ko-KR");

    GameplayOutcomePresentationPage GetEntityPage(
        GameplayEntityId entityId,
        NarrativePerspectiveKind perspectiveKind,
        OutcomeCursor cursor,
        OutcomeFilter filter,
        string locale = "ko-KR");

    GameplayOutcomePresentationPage GetOperationPage(
        GameplayOperationId operationId,
        OutcomeCursor cursor,
        OutcomeFilter filter,
        string locale = "ko-KR");

    string FormatPage(GameplayOutcomePresentationPage page);
}

public readonly struct GameplayOutcomeNarrativeEvidenceSource
{
    public GameplayOutcomeNarrativeEvidenceSource(
        string sourceId,
        bool compacted,
        string outcomeTypeId,
        int firstDay,
        int lastDay,
        int count,
        string canonicalFactText,
        GameplayOutcomeStatus status,
        float subjectSalience,
        int influenceUseCount,
        int influenceRevision,
        IReadOnlyList<string> tags,
        GameplayOutcomeId exactOutcomeId,
        NarrativeMemoryId compactedMemoryId,
        GameplayEntityId subjectId,
        IReadOnlyList<GameplayOutcomeNarrativeEvidenceParticipant> participants,
        IReadOnlyList<GameplayOutcomeNarrativeEvidenceMetric> metrics,
        IReadOnlyList<GameplayOutcomeNarrativeEvidenceFact> facts,
        IReadOnlyList<GameplayOutcomeNarrativeEvidenceProvenance> provenance,
        GameplayOutcomeNarrativeEvidenceLocation location,
        GameplayOutcomeNarrativeEvidenceCausation causation,
        int anchorRevision)
    {
        SourceId = sourceId ?? string.Empty;
        IsCompacted = compacted;
        OutcomeTypeId = outcomeTypeId ?? string.Empty;
        FirstDay = firstDay;
        LastDay = lastDay;
        Count = count;
        CanonicalFactText = canonicalFactText ?? string.Empty;
        Status = status;
        SubjectSalience = Math.Max(0f, subjectSalience);
        InfluenceUseCount = Math.Max(0, influenceUseCount);
        InfluenceRevision = Math.Max(0, influenceRevision);
        Tags = tags ?? Array.Empty<string>();
        ExactOutcomeId = exactOutcomeId;
        CompactedMemoryId = compactedMemoryId;
        SubjectId = subjectId;
        Participants = participants
            ?? Array.Empty<GameplayOutcomeNarrativeEvidenceParticipant>();
        Metrics = metrics ?? Array.Empty<GameplayOutcomeNarrativeEvidenceMetric>();
        Facts = facts ?? Array.Empty<GameplayOutcomeNarrativeEvidenceFact>();
        Provenance = provenance
            ?? Array.Empty<GameplayOutcomeNarrativeEvidenceProvenance>();
        Location = location;
        Causation = causation;
        AnchorRevision = Math.Max(0, anchorRevision);
    }

    public string SourceId { get; }
    public bool IsCompacted { get; }
    public string OutcomeTypeId { get; }
    public int FirstDay { get; }
    public int LastDay { get; }
    public int Count { get; }
    public string CanonicalFactText { get; }
    public GameplayOutcomeStatus Status { get; }
    public float SubjectSalience { get; }
    public int InfluenceUseCount { get; }
    public int InfluenceRevision { get; }
    public IReadOnlyList<string> Tags { get; }
    public GameplayOutcomeId ExactOutcomeId { get; }
    public NarrativeMemoryId CompactedMemoryId { get; }
    public GameplayEntityId SubjectId { get; }
    public bool HasExactOutcome => ExactOutcomeId.IsValid;
    public IReadOnlyList<GameplayOutcomeNarrativeEvidenceParticipant> Participants { get; }
    public IReadOnlyList<GameplayOutcomeNarrativeEvidenceMetric> Metrics { get; }
    public IReadOnlyList<GameplayOutcomeNarrativeEvidenceFact> Facts { get; }
    public IReadOnlyList<GameplayOutcomeNarrativeEvidenceProvenance> Provenance { get; }
    public GameplayOutcomeNarrativeEvidenceLocation Location { get; }
    public GameplayOutcomeNarrativeEvidenceCausation Causation { get; }
    public int AnchorRevision { get; }
}

public readonly struct GameplayOutcomeNarrativeEvidenceParticipant
{
    public GameplayOutcomeNarrativeEvidenceParticipant(
        string entityKindId,
        string entityId,
        string roleId,
        GameplayParticipationKind participationKind,
        bool hasPerceptionEvidence,
        DungeonStory.Narrative.Korean.KoreanNameSnapshot displayName)
    {
        EntityKindId = entityKindId ?? string.Empty;
        EntityId = entityId ?? string.Empty;
        RoleId = roleId ?? string.Empty;
        ParticipationKind = participationKind;
        HasPerceptionEvidence = hasPerceptionEvidence;
        DisplayName = displayName;
    }

    public string EntityKindId { get; }
    public string EntityId { get; }
    public string RoleId { get; }
    public GameplayParticipationKind ParticipationKind { get; }
    public bool HasPerceptionEvidence { get; }
    public DungeonStory.Narrative.Korean.KoreanNameSnapshot DisplayName { get; }
}

public readonly struct GameplayOutcomeNarrativeEvidenceMetric
{
    public GameplayOutcomeNarrativeEvidenceMetric(
        string metricId,
        string unitId,
        string referenceKindId,
        string referenceId,
        double value,
        double minimum,
        double maximum,
        int sampleCount)
    {
        MetricId = metricId ?? string.Empty;
        UnitId = unitId ?? string.Empty;
        ReferenceKindId = referenceKindId ?? string.Empty;
        ReferenceId = referenceId ?? string.Empty;
        Value = value;
        Minimum = minimum;
        Maximum = maximum;
        SampleCount = Math.Max(1, sampleCount);
    }

    public string MetricId { get; }
    public string UnitId { get; }
    public string ReferenceKindId { get; }
    public string ReferenceId { get; }
    public double Value { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public int SampleCount { get; }
}

public readonly struct GameplayOutcomeNarrativeEvidenceFact
{
    public GameplayOutcomeNarrativeEvidenceFact(string factId, string value)
    {
        FactId = factId ?? string.Empty;
        Value = value ?? string.Empty;
    }

    public string FactId { get; }
    public string Value { get; }
}

public readonly struct GameplayOutcomeNarrativeEvidenceProvenance
{
    public GameplayOutcomeNarrativeEvidenceProvenance(string kindId, string value)
    {
        KindId = kindId ?? string.Empty;
        Value = value ?? string.Empty;
    }

    public string KindId { get; }
    public string Value { get; }
}

public readonly struct GameplayOutcomeNarrativeEvidenceLocation
{
    public GameplayOutcomeNarrativeEvidenceLocation(
        string locationId,
        string roomId,
        int x,
        int y)
    {
        LocationId = locationId ?? string.Empty;
        RoomId = roomId ?? string.Empty;
        X = x;
        Y = y;
    }

    public string LocationId { get; }
    public string RoomId { get; }
    public int X { get; }
    public int Y { get; }
}

public readonly struct GameplayOutcomeNarrativeEvidenceCausation
{
    public GameplayOutcomeNarrativeEvidenceCausation(
        string operationId,
        string parentOutcomeId,
        string rootOperationId,
        string relationId)
    {
        OperationId = operationId ?? string.Empty;
        ParentOutcomeId = parentOutcomeId ?? string.Empty;
        RootOperationId = rootOperationId ?? string.Empty;
        RelationId = relationId ?? string.Empty;
    }

    public string OperationId { get; }
    public string ParentOutcomeId { get; }
    public string RootOperationId { get; }
    public string RelationId { get; }
}

public interface IGameplayOutcomeNarrativeEvidenceQuery
{
    bool TryResolveExactBinding(
        GameplayOutcomeEvidenceBindingSnapshot binding,
        out GameplayOutcomeNarrativeEvidenceSource source);

    IReadOnlyList<GameplayOutcomeNarrativeEvidenceSource> GetForEntity(
        GameplayEntityId entityId,
        int maximumCount,
        float minimumSalience,
        bool includeCompacted);

    IReadOnlyList<GameplayOutcomeNarrativeEvidenceSource> GetForCharacter(
        string characterPersistentId,
        int maximumCount,
        float minimumSalience,
        bool includeCompacted);
}

public readonly struct GameplayOutcomeNarrativeEvidenceSelection
{
    public GameplayOutcomeNarrativeEvidenceSelection(
        string publicFactId,
        GameplayOutcomeNarrativeEvidenceSource source)
    {
        PublicFactId = publicFactId?.Trim() ?? string.Empty;
        Source = source;
    }

    public string PublicFactId { get; }
    public GameplayOutcomeNarrativeEvidenceSource Source { get; }
    public bool IsValid => GameplayOutcomeStableIdSyntax.IsValid(PublicFactId)
        && string.Equals(PublicFactId, Source.SourceId, StringComparison.Ordinal)
        && Source.SubjectId.IsValid
        && (Source.ExactOutcomeId.IsValid || Source.CompactedMemoryId.IsValid);
}

public interface IGameplayOutcomeDisplayNameQuery
{
    bool TryGetCurrentName(
        GameplayEntityId entityId,
        out DungeonStory.Narrative.Korean.KoreanNameSnapshot name);
}
