using System;
using System.Collections.Generic;
using System.Text;
using DungeonStory.Narrative.Korean;

public sealed class GameplayOutcomePresentationQuery :
    IGameplayOutcomePresentationQuery
{
    private static readonly GameplayEntityKindId ExpeditionEntityKind =
        new("expedition");
    private readonly IGameplayOutcomeQuery query;
    private readonly IGameplayOutcomeDisplayNameQuery names;
    private readonly IKoreanJosaFormatter josa;

    public GameplayOutcomePresentationQuery(
        IGameplayOutcomeQuery query,
        IGameplayOutcomeDisplayNameQuery names,
        IKoreanJosaFormatter josa)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.names = names ?? throw new ArgumentNullException(nameof(names));
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));
    }

    public GameplayOutcomePresentationPage GetGlobalPage(
        OutcomeCursor cursor,
        OutcomeFilter filter,
        string locale = "ko-KR")
    {
        GameplayOutcomeQueryPage source = query.GetGlobal(cursor, filter);
        return RenderPage(
            source,
            new NarrativePerspectiveContext(
                default,
                NarrativePerspectiveKind.Global,
                locale),
            "전체 확정 기록",
            string.Empty);
    }

    public GameplayOutcomePresentationPage GetEntityPage(
        GameplayEntityId entityId,
        NarrativePerspectiveKind perspectiveKind,
        OutcomeCursor cursor,
        OutcomeFilter filter,
        string locale = "ko-KR")
    {
        if (!entityId.IsValid || perspectiveKind == NarrativePerspectiveKind.Global)
        {
            return new GameplayOutcomePresentationPage(
                Array.Empty<GameplayOutcomePresentationRow>(),
                cursor,
                "조회 대상 없음",
                "presentation-entity-or-perspective-invalid");
        }

        string heading = BuildEntityHeading(entityId, locale);
        GameplayOutcomeQueryPage source = query.GetForEntity(entityId, cursor, filter);
        return RenderPage(
            source,
            new NarrativePerspectiveContext(entityId, perspectiveKind, locale),
            heading,
            string.Empty);
    }

    public GameplayOutcomePresentationPage GetExpeditionPage(
        GameplayEntityId expeditionId,
        GameplayOperationId operationId,
        OutcomeCursor cursor,
        OutcomeFilter filter,
        string locale = "ko-KR")
    {
        if (!expeditionId.IsValid
            || !expeditionId.Kind.Equals(ExpeditionEntityKind)
            || !operationId.IsValid)
        {
            return new GameplayOutcomePresentationPage(
                Array.Empty<GameplayOutcomePresentationRow>(),
                cursor,
                "원정 기록",
                "presentation-expedition-or-operation-invalid");
        }

        // The core operation query is intentionally bounded to one operation.
        // Apply the caller's display filter without exposing the operation ID.
        GameplayOutcomeQueryPage source = query.GetForOperation(operationId);
        return RenderPage(
            FilterOperationPage(source, cursor, filter),
            new NarrativePerspectiveContext(
                expeditionId,
                NarrativePerspectiveKind.Expedition,
                locale),
            "원정 확정 기록",
            string.Empty);
    }

    public string FormatPage(GameplayOutcomePresentationPage page)
    {
        if (page == null)
            return "기록을 불러올 수 없습니다.";

        StringBuilder builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(page.Heading))
            builder.AppendLine(page.Heading);
        if (!string.IsNullOrWhiteSpace(page.DiagnosticCode))
            builder.AppendLine("기록을 불러올 수 없습니다.");
        if (page.Rows.Count == 0)
        {
            builder.Append("확정된 서사 원장 기록이 없습니다.");
            return builder.ToString().TrimEnd();
        }

        for (int index = 0; index < page.Rows.Count; index++)
        {
            GameplayOutcomePresentationRow row = page.Rows[index];
            if (index > 0)
                builder.AppendLine().AppendLine();
            string day = row.FirstDay == row.LastDay
                ? $"{row.FirstDay}일"
                : $"{row.FirstDay}~{row.LastDay}일";
            builder.Append('[')
                .Append(day)
                .Append(" · ")
                .Append(row.SourceKind == GameplayOutcomePresentationSourceKind.Compacted
                    ? "통합 기억"
                    : "확정 결과")
                .AppendLine("]")
                .Append(row.Text);
        }
        return builder.ToString().TrimEnd();
    }

    private GameplayOutcomePresentationPage RenderPage(
        GameplayOutcomeQueryPage source,
        NarrativePerspectiveContext perspective,
        string heading,
        string diagnosticCode)
    {
        if (source == null)
        {
            return new GameplayOutcomePresentationPage(
                Array.Empty<GameplayOutcomePresentationRow>(),
                OutcomeCursor.FirstPage(),
                heading,
                "outcome-query-returned-null-page");
        }

        List<GameplayOutcomePresentationRow> rows = new(source.Items.Count);
        foreach (GameplayOutcomeQueryItem item in source.Items)
            rows.Add(RenderItem(item, perspective));
        return new GameplayOutcomePresentationPage(
            rows,
            source.NextCursor,
            heading,
            diagnosticCode);
    }

    private GameplayOutcomePresentationRow RenderItem(
        GameplayOutcomeQueryItem item,
        NarrativePerspectiveContext perspective)
    {
        if (item.IsCompacted)
        {
            CompactedNarrativeMemorySnapshot memory = item.Compacted;
            if (memory == null || !GameplayOutcomeStableIdSyntax.IsValid(memory.memoryId))
                return Unavailable(item.SortSequence, "compacted-memory-id-invalid");

            NarrativeMemoryId memoryId = new(memory.memoryId);
            if (!query.TryProjectMemory(memoryId, perspective, out NarrativeMemoryView view))
            {
                return new GameplayOutcomePresentationRow(
                    GameplayOutcomePresentationSourceKind.Compacted,
                    memory.memoryId,
                    memory.lastSequence,
                    memory.firstDay,
                    memory.lastDay,
                    memory.outcomeTypeId,
                    "통합 기억의 관점 표현기를 사용할 수 없습니다.",
                    string.Empty,
                    true,
                    "compacted-projector-unavailable");
            }

            return new GameplayOutcomePresentationRow(
                GameplayOutcomePresentationSourceKind.Compacted,
                memory.memoryId,
                memory.lastSequence,
                memory.firstDay,
                memory.lastDay,
                memory.outcomeTypeId,
                view.Text,
                view.RendererVersion,
                view.NeutralFrameUsed,
                view.NeutralFrameUsed ? "neutral-frame" : string.Empty);
        }

        GameplayOutcomeSnapshot exact = item.Exact;
        if (!TryGetOutcomeId(exact, out GameplayOutcomeId outcomeId))
            return Unavailable(item.SortSequence, "exact-outcome-id-invalid");
        if (!query.TryProject(outcomeId, perspective, out NarrativeView projected))
        {
            return new GameplayOutcomePresentationRow(
                GameplayOutcomePresentationSourceKind.Exact,
                outcomeId.ToString(),
                exact.sequence,
                exact.absoluteDay,
                exact.absoluteDay,
                exact.outcomeTypeId,
                "이 결과를 현재 관점으로 표현할 수 없습니다.",
                string.Empty,
                true,
                "exact-projector-or-perspective-unavailable");
        }

        return new GameplayOutcomePresentationRow(
            GameplayOutcomePresentationSourceKind.Exact,
            outcomeId.ToString(),
            exact.sequence,
            exact.absoluteDay,
            exact.absoluteDay,
            exact.outcomeTypeId,
            projected.Text,
            projected.RendererVersion,
            projected.NeutralFrameUsed,
            projected.NeutralFrameUsed ? "neutral-frame" : string.Empty);
    }

    private string BuildEntityHeading(GameplayEntityId entityId, string locale)
    {
        if (!names.TryGetCurrentName(entityId, out KoreanNameSnapshot name))
            return "선택한 대상의 확정 기록";

        KoreanNameSnapshot localized = new(
            name.DisplayText,
            name.DisplaySnapshotRevision,
            name.PronunciationHint,
            locale);
        KoreanJosaFormatResult formatted = josa.Format(
            new KoreanJosaRequest(localized, KoreanJosaKind.Subject));
        return formatted.RequiresNeutralFrame
            ? $"선택한 대상: {name.DisplayText} · 확정 기록"
            : $"{formatted.Text} 관련된 확정 기록";
    }

    private static GameplayOutcomeQueryPage FilterOperationPage(
        GameplayOutcomeQueryPage source,
        OutcomeCursor cursor,
        in OutcomeFilter filter)
    {
        if (source == null || source.Items.Count == 0)
            return source ?? new GameplayOutcomeQueryPage(
                Array.Empty<GameplayOutcomeQueryItem>(), cursor);

        List<GameplayOutcomeQueryItem> items = new(cursor.Limit);
        for (int index = 0; index < source.Items.Count; index++)
        {
            GameplayOutcomeQueryItem item = source.Items[index];
            if (item.SortSequence >= cursor.BeforeSequenceExclusive)
                continue;
            if (item.IsCompacted || !Matches(item.Exact, filter))
                continue;
            items.Add(item);
            if (items.Count == cursor.Limit)
                break;
        }
        OutcomeCursor next = items.Count == 0
            ? cursor
            : new OutcomeCursor(items[items.Count - 1].SortSequence, cursor.Limit);
        return new GameplayOutcomeQueryPage(items, next);
    }

    private static bool Matches(GameplayOutcomeSnapshot outcome, in OutcomeFilter filter)
    {
        if (outcome == null
            || filter.OutcomeTypeId.IsValid
                && !string.Equals(outcome.outcomeTypeId,
                    filter.OutcomeTypeId.Value, StringComparison.Ordinal)
            || filter.Status.HasValue && outcome.status != filter.Status.Value
            || outcome.absoluteDay < filter.MinimumDay
            || outcome.absoluteDay > filter.MaximumDay)
            return false;
        if (!filter.TagId.IsValid)
            return true;
        List<string> tags = outcome.tags;
        for (int index = 0; index < (tags?.Count ?? 0); index++)
        {
            if (string.Equals(tags[index], filter.TagId.Value, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool TryGetOutcomeId(
        GameplayOutcomeSnapshot snapshot,
        out GameplayOutcomeId outcomeId)
    {
        if (snapshot != null
            && GameplayOutcomeStableIdSyntax.IsValid(snapshot.runId)
            && snapshot.sequence > 0L)
        {
            outcomeId = new GameplayOutcomeId(
                new GameplayOutcomeRunId(snapshot.runId),
                snapshot.sequence);
            return true;
        }
        outcomeId = default;
        return false;
    }

    private static GameplayOutcomePresentationRow Unavailable(
        long sequence,
        string diagnosticCode) => new(
            GameplayOutcomePresentationSourceKind.Unavailable,
            string.Empty,
            sequence,
            0,
            0,
            string.Empty,
            "원장 항목을 표시할 수 없습니다.",
            string.Empty,
            true,
            diagnosticCode);
}
