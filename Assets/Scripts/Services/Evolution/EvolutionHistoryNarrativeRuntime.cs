using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

[Serializable]
public sealed class EvolutionHistoryNarrativeResponseDto : ILlmJsonPayload
{
    public string requestKey = string.Empty;
    public string targetPersistentId = string.Empty;
    public string nodeId = string.Empty;
    public string parentNodeId = string.Empty;
    public string effectId = string.Empty;
    public int effectBudget;
    public string[] evidenceIds = Array.Empty<string>();
    public string displayName = string.Empty;
    public string description = string.Empty;
    public string historyReason = string.Empty;
    [NonSerialized] public NarrativeGenerationTrace narrativeTrace;

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(requestKey)
            || string.IsNullOrWhiteSpace(targetPersistentId)
            || string.IsNullOrWhiteSpace(nodeId)
            || string.IsNullOrWhiteSpace(effectId)
            || evidenceIds == null)
        {
            error = "Evolution history identifiers and evidenceIds are required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName)
            || displayName.Trim().Length > 32)
        {
            error = "displayName must contain 1-32 characters.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(description)
            || description.Trim().Length > 180)
        {
            error = "description must contain 1-180 characters.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(historyReason)
            || historyReason.Trim().Length > 180)
        {
            error = "historyReason must contain 1-180 characters.";
            return false;
        }

        return true;
    }
}

[Serializable]
public sealed class EquipmentEvolutionPresentationResponseDto : ILlmJsonPayload
{
    public string presentationId = string.Empty;
    public string displayName = string.Empty;
    public string narrativeFlavor = string.Empty;

    public bool Validate(out string error)
    {
        if (string.IsNullOrWhiteSpace(presentationId)
            || string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(narrativeFlavor))
        {
            error = "presentationId, displayName, and narrativeFlavor are required.";
            return false;
        }
        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class EquipmentEvolutionModuleSelectionResponseDto : ILlmJsonPayload
{
    public string selectionId = string.Empty;
    public List<string> positiveModuleIds = new();
    public List<string> drawbackModuleIds = new();
    public List<string> evidenceFactIds = new();
    public string displayName = string.Empty;
    public string narrativeFlavor = string.Empty;

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(selectionId)
            || positiveModuleIds == null || drawbackModuleIds == null
            || evidenceFactIds == null || string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(narrativeFlavor))
        {
            error = "The six equipment module-selection fields are required.";
            return false;
        }
        if (!string.Equals(displayName, displayName.Trim(), StringComparison.Ordinal)
            || displayName.Length > 32
            || !string.Equals(narrativeFlavor, narrativeFlavor.Trim(), StringComparison.Ordinal)
            || narrativeFlavor.Length > 180
            || (displayName + narrativeFlavor).Any(value => char.IsDigit(value)
                || value is '%' or '％'))
        {
            error = "Equipment selection prose is non-canonical, too long, or contains mechanical numbers.";
            return false;
        }
        return true;
    }
}

public interface IEvolutionHistoryNarrativeRuntime
{
    int PendingCount { get; }
    bool TryGetEquipmentChoiceAudit(
        string requestKey,
        out NarrativeInferenceAuditRecord audit);
    bool TryGetEvolutionHistoryAudit(
        string requestKey,
        out NarrativeInferenceAuditRecord audit);
    bool TryApplyResponseForDebug(
        string requestKey,
        string response,
        out string failureReason);
    void CancelTarget(
        EvolutionNarrativeTargetKind targetKind,
        string targetPersistentId);
}

public enum EquipmentChoiceFailureKind
{
    None,
    RuntimeUnavailable,
    RuntimeCapabilityMissing,
    RequestRejected,
    AsyncResultInvalid,
    SelectedIndexOutOfRange,
    PublicContextUnavailable
}

public static class EvolutionNarrativeResponseValidator
{
    public static bool Validate(
        EvolutionNarrativeRequestSnapshot request,
        EvolutionHistoryNarrativeResponseDto payload,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (request == null
            || payload == null
            || !payload.Validate(out failureReason)
            || !string.Equals(payload.requestKey, request.requestKey, StringComparison.Ordinal)
            || !string.Equals(
                payload.targetPersistentId,
                request.targetPersistentId,
                StringComparison.Ordinal)
            || !string.Equals(payload.nodeId, request.nodeId, StringComparison.Ordinal)
            || !string.Equals(
                payload.parentNodeId ?? string.Empty,
                request.parentNodeId ?? string.Empty,
                StringComparison.Ordinal)
            || !string.Equals(payload.effectId, request.effectId, StringComparison.Ordinal)
            || payload.effectBudget != request.effectBudget)
        {
            if (string.IsNullOrWhiteSpace(failureReason))
            {
                failureReason =
                    "Evolution narrative identifiers or effect budget changed.";
            }
            return false;
        }

        if (!(payload.evidenceIds ?? Array.Empty<string>()).SequenceEqual(
                request.evidenceIds ?? new List<string>(),
                StringComparer.Ordinal))
        {
            failureReason =
                "Evolution narrative evidence does not match the locked snapshot.";
            return false;
        }

        return true;
    }
}

public static class EvolutionNarrativeRequestFactory
{
    public static EvolutionNarrativeRequestSnapshot Create(
        EvolutionNarrativeTargetKind targetKind,
        string targetPersistentId,
        EvolutionNode node,
        string historyHash,
        UsageLedger ledger,
        int effectBudget)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        List<UsageLedgerEvent> evidence = (ledger?.currentGenerationEvents
                ?? new List<UsageLedgerEvent>())
            .Where(entry => entry != null)
            .OrderByDescending(entry => Mathf.Abs(entry.amount))
            .ThenBy(entry => entry.sequence)
            .ThenBy(entry => entry.evidenceId, StringComparer.Ordinal)
            .Take(8)
            .Select(entry => entry.Clone())
            .ToList();
        List<CompactedHistorySegment> priorGenerations =
            (ledger?.compactedSegments ?? new List<CompactedHistorySegment>())
            .Where(segment => segment != null)
            .OrderBy(segment => segment.level)
            .ThenBy(segment => segment.firstGeneration)
            .ThenBy(segment => segment.lastGeneration)
            .ThenBy(segment => segment.historyHash, StringComparer.Ordinal)
            .Select(segment => segment.Clone())
            .ToList();
        string normalizedTarget = targetPersistentId?.Trim() ?? string.Empty;
        string baseRequestKey = $"evolution-history:{targetKind}:{normalizedTarget}:{node.nodeId}:{historyHash}";
        EvolutionNarrativeRequestSnapshot snapshot = new EvolutionNarrativeRequestSnapshot
        {
            requestKey = baseRequestKey,
            targetKind = targetKind,
            targetPersistentId = normalizedTarget,
            nodeId = node.nodeId ?? string.Empty,
            parentNodeId = node.parentNodeId ?? string.Empty,
            effectId = node.effectId ?? string.Empty,
            historyHash = historyHash ?? string.Empty,
            generation = Mathf.Max(0, node.generation),
            effectBudget = Mathf.Max(0, effectBudget),
            legalCandidateEffectIds = node.legalCandidateEffectIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .Take(3)
                .ToList() ?? new List<string>(),
            selectedCandidateIndex = node.selectedCandidateIndex,
            evidenceIds = evidence
                .Select(entry => entry.evidenceId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
            participantIds = evidence
                .SelectMany(entry => new[] { entry.actorId, entry.targetId })
                .Concat(priorGenerations.SelectMany(segment =>
                    segment.participantIds ?? new List<string>()))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
            sourceTags = evidence
                .SelectMany(entry => entry.sourceTags ?? new List<string>())
                .Concat(priorGenerations.SelectMany(segment =>
                    segment.sourceTags ?? new List<string>()))
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToList(),
            frozenHistoryCaptured = true,
            frozenCurrentEvents = evidence
                .Select(entry => entry.Clone())
                .ToList(),
            frozenPriorGenerationSegments = priorGenerations
                .Select(segment => segment.Clone())
                .ToList()
        };
        return snapshot;
    }
}

public static class EvolutionNarrativePublicContextBinding
{
    public static bool TryBind(
        string storedSemanticHash,
        string envelopeSemanticHash,
        out string boundSemanticHash,
        out string failureReason)
    {
        string stored = storedSemanticHash ?? string.Empty;
        string envelope = envelopeSemanticHash ?? string.Empty;
        boundSemanticHash = stored;
        if (string.IsNullOrWhiteSpace(envelope))
        {
            failureReason = "Public context semantic hash is unavailable.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(stored))
        {
            boundSemanticHash = envelope;
            failureReason = string.Empty;
            return true;
        }

        if (!string.Equals(stored, envelope, StringComparison.Ordinal))
        {
            failureReason = "Public context semantic hash changed after binding.";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }
}

public static class EvolutionNarrativePromptFormatter
{
    private const string SelectionPolicyId = "evolution-history-priority-v1";

    public static NarrativePublicContextMaterial BuildPublicMaterial(
        string profileId,
        EvolutionNarrativeRequestSnapshot request,
        string targetDisplayName,
        IReadOnlyDictionary<string, string> resolvedParticipantDisplayNames)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (!request.frozenHistoryCaptured)
        {
            throw new InvalidOperationException(
                EvolutionNarrativeRequestSnapshot.LegacyFrozenHistoryUnavailable);
        }

        string subjectId = RequireId(
            request.targetPersistentId,
            "Evolution narrative targetPersistentId");
        NarrativePublicSubjectKind subjectKind = request.targetKind switch
        {
            EvolutionNarrativeTargetKind.Equipment => NarrativePublicSubjectKind.Equipment,
            EvolutionNarrativeTargetKind.Facility => NarrativePublicSubjectKind.Facility,
            _ => throw new ArgumentOutOfRangeException(nameof(request.targetKind))
        };
        NarrativePublicEntityKind subjectEntityKind = subjectKind ==
            NarrativePublicSubjectKind.Equipment
                ? NarrativePublicEntityKind.Equipment
                : NarrativePublicEntityKind.Facility;
        string subjectName = string.IsNullOrWhiteSpace(targetDisplayName)
            ? subjectId
            : targetDisplayName.Trim();

        Dictionary<string, string> participantNames =
            NormalizeResolvedParticipantNames(resolvedParticipantDisplayNames);
        if (subjectKind == NarrativePublicSubjectKind.Equipment)
        {
            RequireEquipmentParticipantsResolved(
                request.participantIds,
                subjectId,
                participantNames);
        }
        List<NarrativePublicEntityInput> entities =
            new List<NarrativePublicEntityInput>
            {
                new NarrativePublicEntityInput(
                    subjectId,
                    subjectEntityKind,
                    subjectName)
            };
        foreach (KeyValuePair<string, string> participant in participantNames)
        {
            if (string.Equals(participant.Key, subjectId, StringComparison.Ordinal))
            {
                continue;
            }
            entities.Add(new NarrativePublicEntityInput(
                participant.Key,
                NarrativePublicEntityKind.Character,
                participant.Value));
        }

        List<NarrativePublicFactInput> facts = new List<NarrativePublicFactInput>();
        foreach (UsageLedgerEvent entry in request.frozenCurrentEvents
                     ?? new List<UsageLedgerEvent>())
        {
            AddEventFact(
                facts,
                "evolution-current-event",
                entry,
                subjectId,
                subjectName,
                participantNames,
                NarrativePublicFactCategory.Memory,
                1000);
        }

        foreach (CompactedHistorySegment segment in
                 request.frozenPriorGenerationSegments
                     ?? new List<CompactedHistorySegment>())
        {
            AddPriorSegmentFact(
                facts,
                segment,
                subjectId,
                subjectName,
                participantNames);
            foreach (UsageLedgerEvent entry in segment.keyEvents
                         ?? new List<UsageLedgerEvent>())
            {
                AddEventFact(
                    facts,
                    "evolution-prior-event:" + RequireId(
                        segment.historyHash,
                        "Compacted history hash"),
                    entry,
                    subjectId,
                    subjectName,
                    participantNames,
                    NarrativePublicFactCategory.PriorHistory,
                    800);
            }
        }

        bool hasResolvedCharacter = participantNames.Keys.Any(id =>
            !string.Equals(id, subjectId, StringComparison.Ordinal));
        NarrativePublicContextMaterial material = NarrativePublicContextFactory.Build(
            profileId,
            subjectId,
            subjectKind,
            string.Empty,
            requireCharacterFact: hasResolvedCharacter,
            requireMotif: true,
            entities,
            facts,
            SelectionPolicyId);
        RequireLockedEvidenceReadable(request.evidenceIds, material.Events);
        return material;
    }

    public static NarrativePublicPromptEnvelope BuildPromptEnvelope(
        string profileId,
        EvolutionNarrativeRequestSnapshot request,
        string targetDisplayName,
        IReadOnlyDictionary<string, string> resolvedParticipantDisplayNames)
    {
        if (string.Equals(
                profileId,
                LocalLlmRequestProfiles.EquipmentChoiceLegacyV2.Id,
                StringComparison.Ordinal))
        {
            return BuildEquipmentChoicePromptEnvelope(
                request,
                targetDisplayName,
                resolvedParticipantDisplayNames);
        }
        if (string.Equals(
                profileId,
                LocalLlmRequestProfiles.EvolutionHistoryLegacyV2.Id,
                StringComparison.Ordinal))
        {
            return BuildEvolutionHistoryPromptEnvelope(
                request,
                targetDisplayName,
                resolvedParticipantDisplayNames);
        }
        throw new ArgumentException(
            $"Unsupported evolution narrative profile '{profileId}'.",
            nameof(profileId));
    }

    public static NarrativePublicPromptEnvelope BuildEquipmentChoicePromptEnvelope(
        EvolutionNarrativeRequestSnapshot request,
        string targetDisplayName,
        IReadOnlyDictionary<string, string> resolvedParticipantDisplayNames)
    {
        NarrativePublicContextMaterial material = BuildPublicMaterial(
            LocalLlmRequestProfiles.EquipmentChoiceLegacyV2.Id,
            request,
            targetDisplayName,
            resolvedParticipantDisplayNames);
        StringBuilder builder = new StringBuilder(1024);
        builder.AppendLine("아래 후보는 규칙 시스템이 확정한 합법적인 장비 역사 효과다.");
        builder.AppendLine("새 효과나 수치를 만들지 말고, 공개 역사 기록과 가장 어울리는 후보 번호 하나만 고른다.");
        builder.AppendLine("추가 키 없이 {\"selectedIndex\":n} JSON 객체 하나만 반환한다.");
        builder.Append("기록 해시: ").AppendLine(request.historyHash ?? string.Empty);
        builder.AppendLine("역사 사건과 공개 이름은 publicNarrativeContext를 사용한다.");
        for (int index = 0;
             index < (request.legalCandidateEffectIds?.Count ?? 0);
             index++)
        {
            EquipmentHistoricalEffectDefinition definition =
                EquipmentHistoricalEffectCatalog.Require(
                    request.legalCandidateEffectIds[index]);
            builder.Append(index)
                .Append(" = effectId=")
                .Append(definition.EffectId)
                .Append("; name=")
                .Append(definition.DisplayName)
                .Append("; description=")
                .AppendLine(definition.Description);
        }
        return NarrativePublicPromptEnvelope.Create(builder.ToString(), material);
    }

    public static NarrativePublicPromptEnvelope BuildEvolutionHistoryPromptEnvelope(
        EvolutionNarrativeRequestSnapshot request,
        string targetDisplayName,
        IReadOnlyDictionary<string, string> resolvedParticipantDisplayNames)
    {
        NarrativePublicContextMaterial material = BuildPublicMaterial(
            LocalLlmRequestProfiles.EvolutionHistoryLegacyV2.Id,
            request,
            targetDisplayName,
            resolvedParticipantDisplayNames);
        StringBuilder builder = new StringBuilder(1600);
        builder.AppendLine("규칙 시스템이 effectId와 모든 기계 효과를 이미 확정했다.");
        builder.AppendLine("효과나 수치를 추가·삭제·변경하지 않고 이름과 역사 문구만 작성한다.");
        builder.AppendLine("계승 장비는 형태가 바뀔 수 있으므로 특정 무기 형상을 이름에 고정하지 않는다.");
        builder.AppendLine("publicNarrativeContext의 공개 인물·관계·사건만 사용하며 새로운 인물, 사건, 수치를 만들지 않는다.");
        builder.AppendLine("내부 시스템 용어를 플레이어 문구에 쓰지 않는다.");
        builder.AppendLine("판타지·무협풍의 짧고 기억하기 쉬운 한국어 이름과 설명을 작성한다.");
        builder.AppendLine("다음 JSON 객체 하나만 반환한다:");
        builder.AppendLine("{\"requestKey\":\"...\",\"targetPersistentId\":\"...\",\"nodeId\":\"...\",\"parentNodeId\":\"...\",\"effectId\":\"...\",\"effectBudget\":0,\"evidenceIds\":[\"...\"],\"displayName\":\"...\",\"description\":\"...\",\"historyReason\":\"...\"}");
        builder.Append("requestKey=").AppendLine(request.requestKey);
        builder.Append("targetPersistentId=").AppendLine(request.targetPersistentId);
        builder.Append("nodeId=").AppendLine(request.nodeId);
        builder.Append("parentNodeId=").AppendLine(request.parentNodeId);
        builder.Append("effectId=").AppendLine(request.effectId);
        builder.Append("effectBudget=").AppendLine(
            request.effectBudget.ToString(CultureInfo.InvariantCulture));
        builder.Append("historyHash=").AppendLine(request.historyHash);
        builder.Append("generation=").AppendLine(
            request.generation.ToString(CultureInfo.InvariantCulture));
        builder.Append("evidenceIds=").AppendLine(string.Join(
            ",",
            request.evidenceIds ?? new List<string>()));
        builder.Append("sourceTags=").AppendLine(string.Join(
            ",",
            request.sourceTags ?? new List<string>()));
        builder.AppendLine("역사 사건, 이전 세대, 공개 참여자 이름은 publicNarrativeContext를 사용한다.");
        return NarrativePublicPromptEnvelope.Create(builder.ToString(), material);
    }

    private static void AddEventFact(
        ICollection<NarrativePublicFactInput> facts,
        string domain,
        UsageLedgerEvent entry,
        string subjectId,
        string subjectName,
        IReadOnlyDictionary<string, string> participantNames,
        NarrativePublicFactCategory category,
        int priority)
    {
        if (entry == null) return;
        string evidenceId = RequireId(entry.evidenceId, "Evolution evidenceId");
        string eventType = RequireId(entry.eventId, "Evolution eventId");
        string publicActorId = ResolvePublicParticipantId(
            entry.actorId,
            subjectId,
            participantNames);
        string publicTargetId = ResolvePublicParticipantId(
            entry.targetId,
            subjectId,
            participantNames);
        string outcome = entry.outcomeId?.Trim() ?? string.Empty;
        int count = Math.Max(1, entry.repeatCount);
        NarrativePublicEventInput eventData = new NarrativePublicEventInput(
            evidenceId,
            publicActorId,
            publicTargetId,
            eventType,
            outcome,
            null,
            count,
            Math.Max(0, entry.generation));
        facts.Add(new NarrativePublicFactInput(
            domain,
            evidenceId,
            subjectId,
            BuildEventText(
                entry,
                subjectId,
                subjectName,
                participantNames),
            priority,
            category,
            publicTargetId,
            outcome,
            null,
            count,
            null,
            null,
            eventData));
    }

    private static void AddPriorSegmentFact(
        ICollection<NarrativePublicFactInput> facts,
        CompactedHistorySegment segment,
        string subjectId,
        string subjectName,
        IReadOnlyDictionary<string, string> participantNames)
    {
        if (segment == null) return;
        string historyHash = RequireId(
            segment.historyHash,
            "Compacted history hash");
        if (segment.eventCount <= 0)
        {
            throw new InvalidOperationException(
                "Compacted history segment must contain a recorded event.");
        }
        int eventCount = segment.eventCount;
        string participantText = DescribeResolvedParticipants(
            segment.participantIds,
            subjectId,
            subjectName,
            participantNames);
        string metrics = string.Join(
            ", ",
            (segment.metrics ?? new List<UsageLedgerMetric>())
                .Where(metric => metric != null
                    && !string.IsNullOrWhiteSpace(metric.metricId))
                .OrderBy(metric => metric.metricId, StringComparer.Ordinal)
                .Select(metric => metric.metricId.Trim() + "="
                    + metric.value.ToString("0.###", CultureInfo.InvariantCulture)));
        string evidence = string.Join(
            ", ",
            (segment.historicalEvidence ?? new List<HistoricalEvidenceMetric>())
                .Where(metric => metric != null
                    && metric.kind != HistoricalEvidenceKind.None)
                .OrderBy(metric => metric.kind)
                .Select(metric => metric.kind + " x"
                    + Math.Max(0, metric.occurrences).ToString(CultureInfo.InvariantCulture)
                    + " strength="
                    + metric.strength.ToString("0.###", CultureInfo.InvariantCulture)));
        string tags = string.Join(
            ", ",
            segment.sourceTags ?? new List<string>());
        string text = "Prior generation history for " + subjectName
            + " [historyHash=" + historyHash + "]"
            + ": generations "
            + Math.Max(0, segment.firstGeneration).ToString(CultureInfo.InvariantCulture)
            + "-"
            + Math.Max(segment.firstGeneration, segment.lastGeneration)
                .ToString(CultureInfo.InvariantCulture)
            + "; recorded events="
            + Math.Max(0, segment.eventCount).ToString(CultureInfo.InvariantCulture)
            + "; total magnitude="
            + Math.Max(0f, segment.totalMagnitude)
                .ToString("0.###", CultureInfo.InvariantCulture)
            + (metrics.Length > 0 ? "; metrics=" + metrics : string.Empty)
            + (evidence.Length > 0 ? "; evidence=" + evidence : string.Empty)
            + (participantText.Length > 0 ? "; public participants=" + participantText : string.Empty)
            + (tags.Length > 0 ? "; tags=" + tags : string.Empty)
            + ".";
        facts.Add(new NarrativePublicFactInput(
            "evolution-prior-segment",
            historyHash,
            subjectId,
            text,
            900,
            NarrativePublicFactCategory.PriorHistory,
            subjectId,
            string.Empty,
            null,
            eventCount,
            null,
            null,
            new NarrativePublicEventInput(
                historyHash,
                string.Empty,
                subjectId,
                "compacted-history-segment",
                string.Empty,
                null,
                eventCount,
                Math.Max(0, segment.lastGeneration))));
    }

    private static string BuildEventText(
        UsageLedgerEvent entry,
        string subjectId,
        string subjectName,
        IReadOnlyDictionary<string, string> participantNames)
    {
        string actor = ResolvePublicParticipantName(
            entry.actorId,
            subjectId,
            subjectName,
            participantNames);
        string target = ResolvePublicParticipantName(
            entry.targetId,
            subjectId,
            subjectName,
            participantNames);
        StringBuilder text = new StringBuilder(320);
        text.Append("Recorded event ").Append(entry.eventId.Trim())
            .Append(" [evidence=").Append(entry.evidenceId.Trim()).Append(']')
            .Append(" for ").Append(subjectName)
            .Append(" in generation ")
            .Append(Math.Max(0, entry.generation).ToString(CultureInfo.InvariantCulture));
        if (entry.historicalEvidenceKind != HistoricalEvidenceKind.None)
            text.Append("; historical meaning=").Append(entry.historicalEvidenceKind);
        if (actor.Length > 0) text.Append("; public actor=").Append(actor);
        if (target.Length > 0) text.Append("; public target=").Append(target);
        if (!string.IsNullOrWhiteSpace(entry.outcomeId))
            text.Append("; outcome=").Append(entry.outcomeId.Trim());
        text.Append("; magnitude=")
            .Append(entry.amount.ToString("0.###", CultureInfo.InvariantCulture))
            .Append("; repeat count=")
            .Append(Math.Max(1, entry.repeatCount).ToString(CultureInfo.InvariantCulture));
        string tags = string.Join(", ", entry.sourceTags ?? new List<string>());
        if (tags.Length > 0) text.Append("; tags=").Append(tags);
        return text.Append('.').ToString();
    }

    private static string DescribeResolvedParticipants(
        IEnumerable<string> participantIds,
        string subjectId,
        string subjectName,
        IReadOnlyDictionary<string, string> participantNames)
    {
        return string.Join(
            ", ",
            (participantIds ?? Array.Empty<string>())
                .Select(id => ResolvePublicParticipantName(
                    id,
                    subjectId,
                    subjectName,
                    participantNames))
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal));
    }

    private static string ResolvePublicParticipantId(
        string participantId,
        string subjectId,
        IReadOnlyDictionary<string, string> participantNames)
    {
        string normalized = participantId?.Trim() ?? string.Empty;
        if (string.Equals(normalized, subjectId, StringComparison.Ordinal))
            return subjectId;
        return normalized.Length > 0 && participantNames.ContainsKey(normalized)
            ? normalized
            : string.Empty;
    }

    private static string ResolvePublicParticipantName(
        string participantId,
        string subjectId,
        string subjectName,
        IReadOnlyDictionary<string, string> participantNames)
    {
        string normalized = participantId?.Trim() ?? string.Empty;
        if (string.Equals(normalized, subjectId, StringComparison.Ordinal))
            return subjectName + " [id=" + subjectId + "]";
        return normalized.Length > 0
            && participantNames.TryGetValue(normalized, out string name)
                ? name + " [id=" + normalized + "]"
                : string.Empty;
    }

    private static Dictionary<string, string> NormalizeResolvedParticipantNames(
        IReadOnlyDictionary<string, string> source)
    {
        Dictionary<string, string> result =
            new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in source
                     ?? new Dictionary<string, string>())
        {
            string id = entry.Key?.Trim() ?? string.Empty;
            string name = entry.Value?.Trim() ?? string.Empty;
            if (id.Length == 0 || name.Length == 0) continue;
            result.Add(id, name);
        }
        return result;
    }

    private static void RequireEquipmentParticipantsResolved(
        IEnumerable<string> participantIds,
        string subjectId,
        IReadOnlyDictionary<string, string> participantNames)
    {
        foreach (string participantId in participantIds ?? Array.Empty<string>())
        {
            string normalized = RequireId(
                participantId,
                "Evolution narrative participantId");
            if (!string.Equals(normalized, subjectId, StringComparison.Ordinal)
                && !participantNames.ContainsKey(normalized))
            {
                throw new InvalidOperationException(
                    $"Equipment narrative participant '{normalized}' has no resolved public identity.");
            }
        }
    }

    private static void RequireLockedEvidenceReadable(
        IEnumerable<string> evidenceIds,
        IEnumerable<NarrativePublicEvent> readableEvents)
    {
        HashSet<string> readableEvidence = new HashSet<string>(
            (readableEvents ?? Array.Empty<NarrativePublicEvent>())
                .Select(value => value?.EvidenceId)
                .Where(value => !string.IsNullOrWhiteSpace(value)),
            StringComparer.Ordinal);
        foreach (string evidenceId in evidenceIds ?? Array.Empty<string>())
        {
            string normalized = RequireId(
                evidenceId,
                "Evolution narrative locked evidenceId");
            if (!readableEvidence.Contains(normalized))
            {
                throw new InvalidOperationException(
                    $"Evolution narrative locked evidence '{normalized}' is not readable in the public context.");
            }
        }
    }

    private static string RequireId(string value, string label)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new InvalidOperationException(label + " is required.");
        return normalized;
    }
}

public sealed class EvolutionHistoryNarrativeRuntime :
    IEvolutionHistoryNarrativeRuntime,
    ITickable
{
    private const string EquipmentChoiceProfileId = "EquipmentChoiceLegacyV2";
    private const float ScanIntervalSeconds = 0.25f;
    private const int MaximumConcurrentRequests = 2;
    private const int MaximumSubmissionsPerTick = 1;

    private readonly IBuildingWorldQuery buildings;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly IEquipmentEvolutionRuntime equipmentEvolution;
    private readonly IFacilityEvolutionStateComponentFactory facilityStates;
    private readonly ILocalLlmRuntimeProvider llmRuntimeProvider;
    private readonly IUiClock uiClock;
    private readonly HashSet<string> inFlight =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> retryAt =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly HashSet<string> liveKeys =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, NarrativeInferenceAuditRecord>
        equipmentChoiceAudits =
            new Dictionary<string, NarrativeInferenceAuditRecord>(StringComparer.Ordinal);
    private readonly Dictionary<string, NarrativeInferenceAuditRecord>
        evolutionHistoryAudits =
            new Dictionary<string, NarrativeInferenceAuditRecord>(StringComparer.Ordinal);
    private readonly List<EvolutionNarrativeRequestSnapshot> scanBuffer =
        new List<EvolutionNarrativeRequestSnapshot>();
    private float nextScanAt;

    public EvolutionHistoryNarrativeRuntime(
        IBuildingWorldQuery buildings,
        ICombatEquipmentRuntime equipment,
        IFacilityEvolutionStateComponentFactory facilityStates,
        ILocalLlmRuntimeProvider llmRuntimeProvider,
        IUiClock uiClock,
        IEquipmentEvolutionRuntime equipmentEvolution = null)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.equipmentEvolution = equipmentEvolution;
        this.facilityStates = facilityStates
            ?? throw new ArgumentNullException(nameof(facilityStates));
        this.llmRuntimeProvider = llmRuntimeProvider
            ?? throw new ArgumentNullException(nameof(llmRuntimeProvider));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
    }

    public int PendingCount => retryAt.Count + inFlight.Count;

    public bool TryGetEquipmentChoiceAudit(
        string requestKey,
        out NarrativeInferenceAuditRecord audit)
    {
        string normalized = requestKey?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            audit = default;
            return false;
        }

        if (equipmentChoiceAudits.TryGetValue(normalized, out audit))
        {
            return true;
        }

        foreach (CombatEquipmentInstance instance in equipment.Instances)
        {
            EvolutionNarrativeRequestSnapshot request = instance?
                .evolution?
                .narrativeRequests?
                .FirstOrDefault(entry => entry != null
                    && string.Equals(entry.requestKey, normalized, StringComparison.Ordinal));
            EvolutionNode node = request == null
                ? null
                : instance.evolution.evolutionNodes?.FirstOrDefault(entry =>
                    entry != null
                    && string.Equals(entry.nodeId, request.nodeId, StringComparison.Ordinal));
            if (node == null
                || !node.equipmentChoiceAuditRecorded
                || node.equipmentChoiceAuditUtcTicks <= 0L
                || node.equipmentChoiceAuditUtcTicks > DateTime.MaxValue.Ticks)
            {
                continue;
            }

            DateTime timestamp = new DateTime(
                node.equipmentChoiceAuditUtcTicks,
                DateTimeKind.Utc);
            audit = BuildEquipmentChoiceAudit(
                request,
                node,
                node.equipmentChoiceCandidatePacketHash,
                node.equipmentChoiceSucceeded,
                node.equipmentChoiceFailureReason,
                ParseFailureKind(node.equipmentChoiceFailureKind),
                timestamp);
            equipmentChoiceAudits[normalized] = audit;
            return true;
        }

        audit = default;
        return false;
    }

    public bool TryGetEvolutionHistoryAudit(
        string requestKey,
        out NarrativeInferenceAuditRecord audit)
    {
        return evolutionHistoryAudits.TryGetValue(
            requestKey?.Trim() ?? string.Empty,
            out audit);
    }

    public void Tick()
    {
        float now = uiClock.Time;
        if (now < nextScanAt)
        {
            return;
        }

        nextScanAt = now + ScanIntervalSeconds;
        CollectPendingSnapshots();
        CancelOrphanedRequests();

        int submissions = TrySubmitNextFormulaPresentation(now) ? 1 : 0;
        for (int index = 0;
             index < scanBuffer.Count
             && inFlight.Count < MaximumConcurrentRequests
             && submissions < MaximumSubmissionsPerTick;
             index++)
        {
            EvolutionNarrativeRequestSnapshot request = scanBuffer[index];
            if (request == null
                || request.completed
                || request.cancelled
                || inFlight.Contains(request.requestKey)
                || retryAt.TryGetValue(request.requestKey, out float due)
                    && now < due)
            {
                continue;
            }

            submissions++;
            TrySubmit(request, now);
        }
    }

    private void TrySubmitEquipmentChoice(EvolutionNarrativeRequestSnapshot request)
    {
        if (!request.frozenHistoryCaptured)
        {
            ApplyEquipmentChoice(
                request.requestKey,
                false,
                -1,
                EquipmentChoiceFailureKind.PublicContextUnavailable,
                EvolutionNarrativeRequestSnapshot.LegacyFrozenHistoryUnavailable,
                BuildEquipmentChoiceCandidatePacketHash(
                    request,
                    request.legalCandidateEffectIds,
                    request.publicContextSemanticHash));
            return;
        }

        NarrativePublicPromptEnvelope promptEnvelope;
        try
        {
            promptEnvelope = EvolutionNarrativePromptFormatter
                .BuildPromptEnvelope(
                    EquipmentChoiceProfileId,
                    request,
                    ResolveTargetDisplayName(request),
                    ResolveParticipantDisplayNames(request));
        }
        catch (Exception error) when (
            error is InvalidOperationException || error is ArgumentException)
        {
            ApplyEquipmentChoice(
                request.requestKey,
                false,
                -1,
                EquipmentChoiceFailureKind.PublicContextUnavailable,
                error.Message,
                BuildEquipmentChoiceCandidatePacketHash(
                    request,
                    request.legalCandidateEffectIds,
                    request.publicContextSemanticHash));
            return;
        }
        if (!TryBindAndPersistPublicContext(
                request,
                promptEnvelope.PublicContextSemanticHash,
                out string bindingFailure))
        {
            string rejectedCandidatePacketHash =
                BuildEquipmentChoiceCandidatePacketHash(
                    request,
                    request.legalCandidateEffectIds,
                    promptEnvelope.PublicContextSemanticHash);
            ApplyEquipmentChoice(
                request.requestKey,
                false,
                -1,
                EquipmentChoiceFailureKind.PublicContextUnavailable,
                bindingFailure,
                rejectedCandidatePacketHash);
            return;
        }
        string candidatePacketHash = BuildEquipmentChoiceCandidatePacketHash(
            request,
            request.legalCandidateEffectIds,
            promptEnvelope.PublicContextSemanticHash);
        if (!llmRuntimeProvider.TryGetRuntime(out ILocalLlmRuntime runtime))
        {
            ApplyEquipmentChoice(
                request.requestKey,
                false,
                -1,
                EquipmentChoiceFailureKind.RuntimeUnavailable,
                "Local LLM runtime is unavailable.",
                candidatePacketHash);
            return;
        }

        if (runtime is not IConstrainedEquipmentChoiceLlmRuntime choiceRuntime)
        {
            ApplyEquipmentChoice(
                request.requestKey,
                false,
                -1,
                EquipmentChoiceFailureKind.RuntimeCapabilityMissing,
                "Local LLM runtime does not support strict equipment choice.",
                candidatePacketHash);
            return;
        }

        inFlight.Add(request.requestKey);
        bool submissionReturned = false;
        bool callbackBeforeReturn = false;
        LocalLlmChoiceResult synchronousResult = default;
        bool accepted = choiceRuntime.GenerateEquipmentChoiceAsync(
            request.requestKey,
            promptEnvelope.Prompt,
            request.legalCandidateEffectIds.Count,
            result =>
            {
                if (!submissionReturned)
                {
                    callbackBeforeReturn = true;
                    synchronousResult = result;
                    return;
                }

                inFlight.Remove(request.requestKey);
                ApplyEquipmentChoiceResult(
                    request.requestKey,
                    result,
                    candidatePacketHash);
            });
        submissionReturned = true;
        if (!accepted)
        {
            inFlight.Remove(request.requestKey);
            ApplyEquipmentChoice(
                request.requestKey,
                false,
                -1,
                EquipmentChoiceFailureKind.RequestRejected,
                callbackBeforeReturn && !string.IsNullOrWhiteSpace(synchronousResult.Error)
                    ? synchronousResult.Error
                    : "Strict equipment choice request was not accepted.",
                candidatePacketHash);
            return;
        }

        if (callbackBeforeReturn)
        {
            inFlight.Remove(request.requestKey);
            ApplyEquipmentChoiceResult(
                request.requestKey,
                synchronousResult,
                candidatePacketHash);
        }
    }

    private void ApplyEquipmentChoiceResult(
        string requestKey,
        LocalLlmChoiceResult result,
        string candidatePacketHash)
    {
        ApplyEquipmentChoice(
            requestKey,
            result.Succeeded,
            result.SelectedIndex,
            result.Succeeded
                ? EquipmentChoiceFailureKind.None
                : ResolveEquipmentChoiceFailure(result.Error),
            result.Error,
            candidatePacketHash);
    }

    private static EquipmentChoiceFailureKind ResolveEquipmentChoiceFailure(
        string error)
    {
        return !string.IsNullOrWhiteSpace(error)
            && (error.IndexOf("SelectedIndexOutOfRange", StringComparison.Ordinal) >= 0
                || error.IndexOf("out of range", StringComparison.OrdinalIgnoreCase) >= 0)
            ? EquipmentChoiceFailureKind.SelectedIndexOutOfRange
            : EquipmentChoiceFailureKind.AsyncResultInvalid;
    }

    private bool ApplyEquipmentChoice(
        string requestKey,
        bool modelSucceeded,
        int requestedIndex,
        EquipmentChoiceFailureKind failureKind,
        string validationError,
        string candidatePacketHash)
    {
        if (!TryResolveSnapshot(requestKey, out EvolutionNarrativeRequestSnapshot request)
            || request.targetKind != EvolutionNarrativeTargetKind.Equipment
            || !equipment.TryGetInstance(
                request.targetPersistentId,
                out CombatEquipmentInstance instance))
        {
            return false;
        }

        EquipmentEvolutionState state = instance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        EvolutionNode node = state.evolutionNodes?.FirstOrDefault(entry =>
            entry != null
            && string.Equals(entry.nodeId, request.nodeId, StringComparison.Ordinal));
        EvolutionNarrativeRequestSnapshot stored = state.narrativeRequests?
            .FirstOrDefault(entry => entry != null
                && string.Equals(entry.requestKey, requestKey, StringComparison.Ordinal));
        List<string> candidates = stored?.legalCandidateEffectIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Take(3)
            .ToList() ?? new List<string>();
        if (node == null
            || stored == null
            || candidates.Count == 0
            || stored.completed
            || stored.cancelled
            || !MatchesNode(node, request)
            || node.selectedCandidateIndex >= 0
            || stored.selectedCandidateIndex >= 0)
        {
            return false;
        }

        foreach (string candidateId in candidates)
        {
            EquipmentHistoricalEffectCatalog.Require(candidateId);
        }

        bool indexInRange = requestedIndex >= 0 && requestedIndex < candidates.Count;
        bool succeeded = modelSucceeded
            && failureKind == EquipmentChoiceFailureKind.None
            && indexInRange;
        EquipmentChoiceFailureKind resolvedFailure = succeeded
            ? EquipmentChoiceFailureKind.None
            : modelSucceeded && !indexInRange
                ? EquipmentChoiceFailureKind.SelectedIndexOutOfRange
                : failureKind == EquipmentChoiceFailureKind.None
                    ? EquipmentChoiceFailureKind.AsyncResultInvalid
                    : failureKind;
        int selectedIndex = succeeded ? requestedIndex : -1;
        string resolvedError = succeeded
            ? string.Empty
            : BuildEquipmentChoiceFailure(resolvedFailure, validationError, requestedIndex, candidates.Count);
        if (string.IsNullOrWhiteSpace(candidatePacketHash))
        {
            candidatePacketHash = BuildEquipmentChoiceCandidatePacketHash(
                stored,
                candidates,
                stored.publicContextSemanticHash);
        }
        DateTime auditUtc = DateTime.UtcNow;

        if (!succeeded)
        {
            equipmentChoiceAudits[requestKey] = BuildEquipmentChoiceAudit(
                stored,
                node,
                candidatePacketHash,
                false,
                resolvedError,
                resolvedFailure,
                auditUtc);
            return false;
        }

        node.effectId = candidates[selectedIndex];
        node.selectedCandidateIndex = selectedIndex;
        node.legalCandidateEffectIds = new List<string>(candidates);
        node.mechanicallyUnlocked = true;
        node.equipmentChoiceAuditRecorded = true;
        node.equipmentChoiceSucceeded = succeeded;
        node.equipmentChoiceFallbackUsed = false;
        node.equipmentChoiceFailureKind = resolvedFailure == EquipmentChoiceFailureKind.None
            ? string.Empty
            : resolvedFailure.ToString();
        node.equipmentChoiceFailureReason = resolvedError;
        node.equipmentChoiceCandidatePacketHash = candidatePacketHash;
        node.equipmentChoiceRequestedIndex = requestedIndex;
        node.equipmentChoiceAuditUtcTicks = auditUtc.Ticks;
        stored.effectId = node.effectId;
        stored.selectedCandidateIndex = selectedIndex;
        stored.publicContextSemanticHash = string.Empty;
        if (!equipment.TryUpdateEvolutionState(instance.instanceId, state))
        {
            return false;
        }

        equipmentChoiceAudits[requestKey] = BuildEquipmentChoiceAudit(
            stored,
            node,
            candidatePacketHash,
            succeeded,
            resolvedError,
            resolvedFailure,
            auditUtc);
        return true;
    }

    private static string BuildEquipmentChoicePrompt(
        EvolutionNarrativeRequestSnapshot request)
    {
        return EvolutionNarrativePromptFormatter
            .BuildEquipmentChoicePromptEnvelope(
                request,
                request?.targetPersistentId,
                null)
            .Prompt;
    }

    private static string BuildEquipmentChoiceCandidatePacketHash(
        EvolutionNarrativeRequestSnapshot request,
        IReadOnlyList<string> candidates,
        string publicContextSemanticHash)
    {
        StringBuilder builder = new StringBuilder(512);
        builder.Append(request?.requestKey ?? string.Empty)
            .Append('|').Append(request?.targetPersistentId ?? string.Empty)
            .Append('|').Append(request?.nodeId ?? string.Empty)
            .Append('|').Append(request?.historyHash ?? string.Empty)
            .Append('|').Append(request?.effectBudget ?? 0)
            .Append('|').Append(publicContextSemanticHash ?? string.Empty);
        foreach (string candidateId in candidates ?? Array.Empty<string>())
        {
            EquipmentHistoricalEffectDefinition definition =
                EquipmentHistoricalEffectCatalog.Require(candidateId);
            builder.Append('|').Append(definition.EffectId)
                .Append('|').Append(definition.DisplayName)
                .Append('|').Append(definition.Description);
        }
        return NarrativeInferenceHash.ComputeSha256Utf8(builder.ToString());
    }

    private static string BuildEquipmentChoiceFailure(
        EquipmentChoiceFailureKind failureKind,
        string validationError,
        int requestedIndex,
        int candidateCount)
    {
        string detail = validationError?.Trim() ?? string.Empty;
        if (failureKind == EquipmentChoiceFailureKind.SelectedIndexOutOfRange
            && detail.Length == 0)
        {
            detail = $"selectedIndex {requestedIndex} is outside [0,{candidateCount - 1}].";
        }
        if (detail.Length == 0)
        {
            detail = "Strict equipment choice failed without a backend detail.";
        }
        return failureKind + ": " + detail;
    }

    private static NarrativeInferenceAuditRecord BuildEquipmentChoiceAudit(
        EvolutionNarrativeRequestSnapshot request,
        EvolutionNode node,
        string candidatePacketHash,
        bool succeeded,
        string validationError,
        EquipmentChoiceFailureKind failureKind,
        DateTime timestamp)
    {
        return new NarrativeInferenceAuditRecord(
            EquipmentChoiceProfileId,
            request.requestKey,
            candidatePacketHash,
            succeeded,
            validationError,
            false,
            string.Empty,
            node.effectId,
            node.selectedCandidateIndex,
            request.targetPersistentId,
            NarrativeInferenceTimestamp.FromUtc(timestamp));
    }

    private static EquipmentChoiceFailureKind ParseFailureKind(string value)
    {
        return Enum.TryParse(
            value,
            false,
            out EquipmentChoiceFailureKind parsed)
                ? parsed
                : EquipmentChoiceFailureKind.AsyncResultInvalid;
    }

    private void RecordEvolutionHistoryAudit(
        EvolutionNarrativeRequestSnapshot request,
        bool succeeded,
        string validationError,
        string candidatePacketHash = "")
    {
        if (request == null || string.IsNullOrWhiteSpace(request.requestKey))
        {
            return;
        }

        evolutionHistoryAudits[request.requestKey] =
            new NarrativeInferenceAuditRecord(
                LocalLlmRequestProfiles.EvolutionHistoryLegacyV2.Id,
                request.requestKey,
                string.IsNullOrWhiteSpace(candidatePacketHash)
                    ? BuildEvolutionHistoryCandidatePacketHash(
                        request,
                        request.publicContextSemanticHash)
                    : candidatePacketHash,
                succeeded,
                validationError,
                false,
                string.Empty,
                request.effectId,
                request.selectedCandidateIndex,
                request.targetPersistentId,
                NarrativeInferenceTimestamp.FromUtc(DateTime.UtcNow));
    }

    private static string BuildEvolutionHistoryCandidatePacketHash(
        EvolutionNarrativeRequestSnapshot request,
        string publicContextSemanticHash)
    {
        StringBuilder builder = new StringBuilder(512);
        builder.Append(request?.requestKey ?? string.Empty)
            .Append('|').Append(request?.targetPersistentId ?? string.Empty)
            .Append('|').Append(request?.nodeId ?? string.Empty)
            .Append('|').Append(request?.parentNodeId ?? string.Empty)
            .Append('|').Append(request?.effectId ?? string.Empty)
            .Append('|').Append(request?.effectBudget ?? 0)
            .Append('|').Append(request?.historyHash ?? string.Empty)
            .Append('|').Append(request?.generation ?? 0)
            .Append('|').Append(publicContextSemanticHash ?? string.Empty);
        foreach (string evidenceId in request?.evidenceIds ?? new List<string>())
        {
            builder.Append('|').Append(evidenceId);
        }
        return NarrativeInferenceHash.ComputeSha256Utf8(builder.ToString());
    }

    public bool TryApplyResponseForDebug(
        string requestKey,
        string response,
        out string failureReason)
    {
        return TryApplyResponse(
            requestKey,
            response,
            out failureReason);
    }

    public void CancelTarget(
        EvolutionNarrativeTargetKind targetKind,
        string targetPersistentId)
    {
        string normalized = targetPersistentId?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return;
        }

        string[] keys = liveKeys
            .Concat(inFlight)
            .Concat(retryAt.Keys)
            .Where(key => TryResolveSnapshot(
                key,
                out EvolutionNarrativeRequestSnapshot request)
                && request.targetKind == targetKind
                && string.Equals(
                    request.targetPersistentId,
                    normalized,
                    StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (string key in keys)
        {
            CancelRequest(key);
        }
    }

    private void CollectPendingSnapshots()
    {
        scanBuffer.Clear();
        liveKeys.Clear();
        foreach (BuildableObject building in buildings.Buildings)
        {
            if (building == null || building.isDestroy)
            {
                continue;
            }

            FacilityEvolutionStateComponent component =
                building.GetComponent<FacilityEvolutionStateComponent>();
            if (component == null)
            {
                continue;
            }

            component.InitializeIfNeeded(building);
            FacilityEvolutionState state = component.InstanceEvolution;
            AddPending(state.narrativeRequests);
        }

        foreach (CombatEquipmentInstance instance in equipment.Instances)
        {
            AddPending(instance?.evolution?.narrativeRequests);
            foreach (EquipmentEvolutionPresentationRequest request in
                     instance?.evolution?.presentationRequests
                     ?? new List<EquipmentEvolutionPresentationRequest>())
            {
                if (request != null && !string.IsNullOrWhiteSpace(request.presentationId))
                    liveKeys.Add(request.presentationId);
            }
        }
    }

    private bool TrySubmitNextFormulaPresentation(float now)
    {
        if (equipmentEvolution == null || inFlight.Count >= MaximumConcurrentRequests)
            return false;
        foreach (CombatEquipmentInstance instance in equipment.Instances
                     .Where(value => value?.evolution != null)
                     .OrderBy(value => value.instanceId, StringComparer.Ordinal))
        {
            EquipmentEvolutionPresentationRequest request = instance.evolution.presentationRequests?
                .Where(value => value != null
                    && (value.state is EquipmentEvolutionPresentationState.PresentationPending
                        or EquipmentEvolutionPresentationState.ModuleSelectionPending)
                    && ParentPresentationIsCommitted(instance.evolution, value))
                .OrderBy(value => value.presentationId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (request == null
                || inFlight.Contains(request.presentationId)
                || retryAt.TryGetValue(request.presentationId, out float due) && now < due)
                continue;
            EvolutionNode node = instance.evolution.evolutionNodes?.SingleOrDefault(value =>
                value != null && string.Equals(value.nodeId, request.nodeId, StringComparison.Ordinal));
            if (node == null || node.formulaVersion <= 0)
            {
                RegisterFormulaPresentationFailure(
                    request,
                    "Equipment formula presentation lost its frozen node.",
                    now);
                return true;
            }
            string prompt;
            try
            {
                prompt = BuildEquipmentFormulaPresentationPrompt(instance, node, request);
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException)
            {
                RegisterFormulaPresentationFailure(request, error.Message, now);
                return true;
            }
            if (!llmRuntimeProvider.TryGetRuntime(out ILocalLlmRuntime runtime))
            {
                RegisterFormulaPresentationFailure(
                    request,
                    "EvolutionHistory presentation runtime is unavailable.",
                    now);
                return true;
            }

            string requestKey = request.presentationId;
            inFlight.Add(requestKey);
            bool returned = false;
            bool callbackBeforeReturn = false;
            LocalLlmResult synchronous = default;
            Action<LocalLlmResult> callback = result =>
            {
                if (!returned)
                {
                    callbackBeforeReturn = true;
                    synchronous = result;
                    return;
                }
                HandleFormulaPresentationResult(
                    instance.instanceId,
                    requestKey,
                    result);
            };
            bool accepted;
            if (request.state == EquipmentEvolutionPresentationState.ModuleSelectionPending)
            {
                if (runtime is not ICorrelatedEquipmentEvolutionModuleSelectionLlmRuntime selector)
                {
                    inFlight.Remove(requestKey);
                    RegisterFormulaPresentationFailure(
                        request,
                        "EquipmentEvolutionModuleSelection runtime is unavailable.",
                        now);
                    return true;
                }
                accepted = selector.GenerateEquipmentEvolutionModuleSelectionAsync(
                    requestKey, prompt, callback);
            }
            else
            {
                if (runtime is not ICorrelatedEvolutionHistoryLlmRuntime correlated)
                {
                    inFlight.Remove(requestKey);
                    RegisterFormulaPresentationFailure(
                        request,
                        "EvolutionHistory presentation runtime is unavailable.",
                        now);
                    return true;
                }
                accepted = correlated.GenerateEvolutionHistoryAsync(
                    requestKey, prompt, callback);
            }
            returned = true;
            if (!accepted)
            {
                inFlight.Remove(requestKey);
                RegisterFormulaPresentationFailure(
                    request,
                    callbackBeforeReturn && !string.IsNullOrWhiteSpace(synchronous.Error)
                        ? synchronous.Error
                        : "EvolutionHistory presentation request was not accepted.",
                    now);
            }
            else if (callbackBeforeReturn)
            {
                HandleFormulaPresentationResult(
                    instance.instanceId,
                    requestKey,
                    synchronous);
            }
            return true;
        }
        return false;
    }

    private static bool ParentPresentationIsCommitted(
        EquipmentEvolutionState state,
        EquipmentEvolutionPresentationRequest request)
    {
        EvolutionNode node = state?.evolutionNodes?.SingleOrDefault(value => value != null
            && string.Equals(value.nodeId, request?.nodeId, StringComparison.Ordinal));
        if (node == null || string.IsNullOrEmpty(node.parentNodeId)) return node != null;
        EvolutionNode parent = state.evolutionNodes.SingleOrDefault(value => value != null
            && string.Equals(value.nodeId, node.parentNodeId, StringComparison.Ordinal));
        return parent == null
            || parent.formulaVersion == 0
            || parent.presentationState == EquipmentEvolutionPresentationState.Ready;
    }

    private void HandleFormulaPresentationResult(
        string equipmentInstanceId,
        string presentationId,
        LocalLlmResult result)
    {
        inFlight.Remove(presentationId);
        if (!TryResolveFormulaPresentation(
                equipmentInstanceId,
                presentationId,
                out EquipmentEvolutionPresentationRequest request))
        {
            retryAt.Remove(presentationId);
            return;
        }
        string validationError = string.Empty;
        bool moduleSelection = request.state
            == EquipmentEvolutionPresentationState.ModuleSelectionPending;
        bool succeeded;
        if (moduleSelection)
        {
            succeeded = result.IsSuccess
                && NarrativeExactKeyContract.TryValidateProfileResponse(
                    LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id,
                    result.Content, out string selectionJson, out validationError)
                && LlmJsonResponseParser.TryParse(
                    selectionJson,
                    out EquipmentEvolutionModuleSelectionResponseDto selection,
                    out validationError)
                && selection.Validate(out validationError)
                && string.Equals(selection.selectionId, presentationId, StringComparison.Ordinal)
                && equipmentEvolution.TryCommitModuleSelection(
                    equipmentInstanceId, selection, out validationError);
        }
        else
        {
            succeeded = result.IsSuccess
                && NarrativeExactKeyContract.TryValidateProfileResponse(
                    LocalLlmRequestProfiles.EvolutionHistory.Id,
                    result.Content, out string exactJson, out validationError)
                && LlmJsonResponseParser.TryParse(
                    exactJson,
                    out EquipmentEvolutionPresentationResponseDto payload,
                    out validationError)
                && payload.Validate(out validationError)
                && string.Equals(payload.presentationId, presentationId, StringComparison.Ordinal)
                && equipmentEvolution.TryCommitPresentation(
                    equipmentInstanceId,
                    presentationId,
                    payload.displayName,
                    payload.narrativeFlavor,
                    out validationError);
        }
        if (!succeeded)
        {
            string reason = result.IsSuccess
                ? validationError
                : $"{result.Status}: {result.Error}";
            RegisterFormulaPresentationFailure(request, reason, uiClock.Time);
            return;
        }
        retryAt.Remove(presentationId);
    }

    private void RegisterFormulaPresentationFailure(
        EquipmentEvolutionPresentationRequest request,
        string reason,
        float now)
    {
        if (request == null || equipmentEvolution == null) return;
        if (!equipmentEvolution.TryRegisterPresentationFailure(
                request.targetPersistentId,
                request.presentationId,
                reason,
                out bool awaitingNarrativeRetry)
            || awaitingNarrativeRetry)
        {
            retryAt.Remove(request.presentationId);
            return;
        }
        retryAt[request.presentationId] = now + Mathf.Min(
            120f,
            2f * Mathf.Pow(2f, Mathf.Min(6, request.failureCount + 1)));
    }

    private bool TryResolveFormulaPresentation(
        string equipmentInstanceId,
        string presentationId,
        out EquipmentEvolutionPresentationRequest request)
    {
        request = null;
        if (!equipment.TryGetInstance(equipmentInstanceId, out CombatEquipmentInstance instance))
            return false;
        request = instance.evolution?.presentationRequests?.SingleOrDefault(value =>
            value != null && string.Equals(
                value.presentationId, presentationId, StringComparison.Ordinal));
        return request != null
            && string.Equals(request.targetPersistentId, instance.instanceId, StringComparison.Ordinal);
    }

    private string BuildEquipmentFormulaPresentationPrompt(
        CombatEquipmentInstance instance,
        EvolutionNode node,
        EquipmentEvolutionPresentationRequest request)
    {
        if (!string.Equals(instance?.instanceId, request?.targetPersistentId, StringComparison.Ordinal)
            || !string.Equals(node?.presentationId, request?.presentationId, StringComparison.Ordinal))
            throw new InvalidOperationException("Equipment presentation owner or identity changed.");
        HashSet<string> exactIds = (request.gameplayOutcomeEvidence
                ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .Select(value => value.publicFactId)
            .ToHashSet(StringComparer.Ordinal);
        EquipmentEvolutionFormulaEvidenceRecord[] frozenEvidence =
            instance.evolution.formulaEvidence
                .Where(value => value != null
                    && request.evidenceIds.Contains(value.evidenceId, StringComparer.Ordinal)
                    && !exactIds.Contains(value.evidenceId))
                .OrderBy(value => value.originalEvent?.sequence ?? 0L)
                .ThenBy(value => value.evidenceId, StringComparer.Ordinal)
                .ToArray();
        if (frozenEvidence.Length != request.evidenceIds.Count - exactIds.Count)
            throw new InvalidOperationException("Equipment presentation lost frozen evidence.");
        IReadOnlyDictionary<string, string> participantNames =
            ResolveParticipantDisplayNamesFromWorld(
                buildings,
                frozenEvidence.SelectMany(value => new[]
                {
                    value.originalEvent?.actorId,
                    value.originalEvent?.targetId
                }));
        EvolutionNarrativeRequestSnapshot publicRequest = new EvolutionNarrativeRequestSnapshot
        {
            requestKey = request.presentationId,
            targetKind = EvolutionNarrativeTargetKind.Equipment,
            targetPersistentId = instance.instanceId,
            nodeId = node.nodeId,
            parentNodeId = node.parentNodeId,
            historyHash = request.historyHash,
            generation = node.generation,
            evidenceIds = request.evidenceIds
                .Where(value => !exactIds.Contains(value)).ToList(),
            participantIds = participantNames.Keys
                .OrderBy(value => value, StringComparer.Ordinal).ToList(),
            frozenCurrentEvents = frozenEvidence
                .Select(value => value.originalEvent.Clone()).ToList(),
            frozenHistoryCaptured = true
        };
        NarrativePublicContextMaterial material =
            EvolutionNarrativePromptFormatter.BuildPublicMaterial(
                LocalLlmRequestProfiles.EvolutionHistory.Id,
                publicRequest,
                ResolveTargetDisplayName(publicRequest),
                participantNames);
        material = GameplayOutcomeEvidencePublicMaterialComposer.AddExactEvidence(
            material,
            request.gameplayOutcomeEvidence);
        StringBuilder builder = new StringBuilder(1400);
        if (node.presentationState == EquipmentEvolutionPresentationState.ModuleSelectionPending)
        {
            builder.AppendLine("C#이 현재 장비 기록에서 선택 가능한 개별 진화 모듈을 모두 제공했다.");
            builder.AppendLine("기록에 가장 어울리는 이로운 모듈 하나와 근거 사실을 고른다. 수치와 부정 부담은 C#이 선택 뒤 계산한다.");
            builder.Append("profile=").AppendLine(LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id);
            builder.Append("selectionId=").AppendLine(node.moduleSelectionId);
            builder.AppendLine("moduleOffers:");
            foreach (EquipmentEvolutionModuleOfferState offer in node.moduleSelectionOffers
                         .OrderBy(value => value.moduleId, StringComparer.Ordinal))
                builder.Append("- moduleId=").Append(offer.moduleId)
                    .Append("; polarity=Positive; meaning=")
                    .AppendLine(offer.semanticDescription);
            builder.Append("evidenceFactIds=")
                .AppendLine(string.Join(",", request.evidenceIds));
            builder.AppendLine("정확히 selectionId, positiveModuleIds, drawbackModuleIds, evidenceFactIds, displayName, narrativeFlavor 여섯 키만 가진 JSON 객체를 반환한다.");
            builder.AppendLine("positiveModuleIds는 제공된 모듈 중 정확히 하나다. 현재 독립 부정 모듈 제안이 없으므로 drawbackModuleIds는 빈 배열이다.");
            builder.AppendLine("evidenceFactIds는 제공된 실제 사실 ID의 비어 있지 않은 부분집합이다. 수치·비용·효과 ID를 만들지 않는다.");
            return NarrativePublicPromptEnvelope.Create(builder.ToString(), material).Prompt;
        }
        builder.AppendLine("C# 규칙이 장비 진화의 모든 기계 효과를 확정했다.");
        builder.AppendLine("효과나 수치를 바꾸거나 다시 말하지 말고 이름과 짧은 유래만 작성한다.");
        builder.AppendLine("displayName과 narrativeFlavor에는 숫자 또는 퍼센트 표기를 쓰지 않는다.");
        builder.AppendLine("제공된 근거 밖의 인물·사건·효과를 만들지 않는다.");
        builder.AppendLine("정확히 다음 세 문자열 키만 가진 JSON 객체를 반환한다:");
        builder.AppendLine("{\"presentationId\":\"제공된 값 그대로\",\"displayName\":\"32자 이하 한국어 이름\",\"narrativeFlavor\":\"180자 이하 유래\"}");
        builder.Append("presentationId=").AppendLine(request.presentationId);
        builder.Append("mechanicalDescription=").AppendLine(node.mechanicalDescription);
        builder.AppendLine("근거 사건과 공개 인물 이름은 publicNarrativeContext만 사용한다.");
        return NarrativePublicPromptEnvelope.Create(builder.ToString(), material).Prompt;
    }

    private void AddPending(
        IEnumerable<EvolutionNarrativeRequestSnapshot> requests)
    {
        foreach (EvolutionNarrativeRequestSnapshot request in requests
                     ?? Array.Empty<EvolutionNarrativeRequestSnapshot>())
        {
            if (request == null
                || string.IsNullOrWhiteSpace(request.requestKey))
            {
                continue;
            }

            liveKeys.Add(request.requestKey);
            if (!request.completed && !request.cancelled)
            {
                scanBuffer.Add(request.Clone());
            }
        }
    }

    private void CancelOrphanedRequests()
    {
        string[] orphaned = inFlight
            .Concat(retryAt.Keys)
            .Where(key => !liveKeys.Contains(key))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (string requestKey in orphaned)
        {
            CancelRequest(requestKey);
        }
    }

    private void TrySubmit(
        EvolutionNarrativeRequestSnapshot request,
        float now)
    {
        if (!TryResolveSnapshot(
                request.requestKey,
                out EvolutionNarrativeRequestSnapshot current)
            || current.completed
            || current.cancelled)
        {
            return;
        }

        if (!current.frozenHistoryCaptured)
        {
            current.cancelled = true;
            current.historyContinuityLimitation =
                EvolutionNarrativeRequestSnapshot.LegacyFrozenHistoryUnavailable;
            PersistRequest(current);
            retryAt.Remove(current.requestKey);
            RecordEvolutionHistoryAudit(
                current,
                false,
                current.historyContinuityLimitation);
            return;
        }

        NarrativePublicPromptEnvelope promptEnvelope;
        try
        {
            promptEnvelope = EvolutionNarrativePromptFormatter
                .BuildPromptEnvelope(
                    LocalLlmRequestProfiles.EvolutionHistoryLegacyV2.Id,
                    current,
                    ResolveTargetDisplayName(current),
                    ResolveParticipantDisplayNames(current));
        }
        catch (Exception error) when (
            error is InvalidOperationException || error is ArgumentException)
        {
            current.cancelled = true;
            current.historyContinuityLimitation =
                "public-context-invalid:" + error.Message;
            PersistRequest(current);
            retryAt.Remove(current.requestKey);
            RecordEvolutionHistoryAudit(
                current,
                false,
                current.historyContinuityLimitation);
            return;
        }
        if (!TryBindAndPersistPublicContext(
                current,
                promptEnvelope.PublicContextSemanticHash,
                out string bindingFailure))
        {
            string rejectedCandidatePacketHash =
                BuildEvolutionHistoryCandidatePacketHash(
                    current,
                    promptEnvelope.PublicContextSemanticHash);
            current.cancelled = true;
            current.historyContinuityLimitation =
                "public-context-invalid:" + bindingFailure;
            PersistRequest(current);
            retryAt.Remove(current.requestKey);
            RecordEvolutionHistoryAudit(
                current,
                false,
                current.historyContinuityLimitation,
                rejectedCandidatePacketHash);
            return;
        }
        string candidatePacketHash = BuildEvolutionHistoryCandidatePacketHash(
            current,
            promptEnvelope.PublicContextSemanticHash);

        if (!llmRuntimeProvider.TryGetRuntime(out ILocalLlmRuntime runtime))
        {
            RecordEvolutionHistoryAudit(
                current,
                false,
                "Local LLM runtime is unavailable.",
                candidatePacketHash);
            ScheduleRetry(current, now);
            return;
        }

        current.attemptCount++;
        PersistRequest(current);
        inFlight.Add(current.requestKey);
        if (runtime is not ICorrelatedEvolutionHistoryLlmRuntime correlated)
        {
            RecordEvolutionHistoryAudit(
                current,
                false,
                "Legacy evolution-history runtime is unavailable.",
                candidatePacketHash);
            ScheduleRetry(current, now);
            return;
        }
        bool accepted = correlated.GenerateEvolutionHistoryLegacyV2Async(
            current.requestKey,
            promptEnvelope.Prompt,
            result => HandleResult(
                current.requestKey,
                result,
                candidatePacketHash));
        if (!accepted)
        {
            inFlight.Remove(current.requestKey);
            RecordEvolutionHistoryAudit(
                current,
                false,
                "Evolution history request was not accepted.",
                candidatePacketHash);
            ScheduleRetry(current, now);
        }
    }

    private void HandleResult(
        string requestKey,
        LocalLlmResult result,
        string candidatePacketHash)
    {
        inFlight.Remove(requestKey);
        if (!TryResolveSnapshot(
                requestKey,
                out EvolutionNarrativeRequestSnapshot current)
            || current.cancelled
            || current.completed)
        {
            return;
        }

        if (result.IsSuccess
            && TryApplyResponse(
                requestKey,
                result.Content,
                out _,
                result.NarrativeTrace,
                candidatePacketHash))
        {
            retryAt.Remove(requestKey);
            return;
        }

        if (!result.IsSuccess)
        {
            RecordEvolutionHistoryAudit(
                current,
                false,
                $"{result.Status}: {result.Error}",
                candidatePacketHash);
        }

        ScheduleRetry(current, uiClock.Time);
    }

    private bool TryApplyResponse(
        string requestKey,
        string response,
        out string failureReason,
        NarrativeGenerationTrace narrativeTrace = null,
        string candidatePacketHash = "")
    {
        failureReason = string.Empty;
        if (!TryResolveSnapshot(
                requestKey,
                out EvolutionNarrativeRequestSnapshot request)
            || request.cancelled
            || request.completed)
        {
            failureReason = "Evolution narrative target is no longer available.";
            return false;
        }

        if (!NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.EvolutionHistoryLegacyV2.Id,
                response,
                out string exactJson,
                out failureReason)
            || !LlmJsonResponseParser.TryParse(
                exactJson,
                out EvolutionHistoryNarrativeResponseDto payload,
                out failureReason)
            || !EvolutionNarrativeResponseValidator.Validate(
                request,
                payload,
                out failureReason))
        {
            RecordEvolutionHistoryAudit(
                request,
                false,
                failureReason,
                candidatePacketHash);
            return false;
        }
        payload.narrativeTrace = narrativeTrace;

        bool updated = request.targetKind == EvolutionNarrativeTargetKind.Facility
            ? ApplyFacilityNarrative(request, payload)
            : ApplyEquipmentNarrative(request, payload);
        if (!updated)
        {
            failureReason = "Evolution narrative target changed before the response arrived.";
            RecordEvolutionHistoryAudit(
                request,
                false,
                failureReason,
                candidatePacketHash);
            return false;
        }

        RecordEvolutionHistoryAudit(
            request,
            true,
            string.Empty,
            candidatePacketHash);
        return true;
    }

    private bool ApplyFacilityNarrative(
        EvolutionNarrativeRequestSnapshot request,
        EvolutionHistoryNarrativeResponseDto payload)
    {
        BuildableObject facility = FindFacility(request.targetPersistentId);
        if (facility == null)
        {
            return false;
        }

        FacilityEvolutionStateComponent component =
            facilityStates.GetOrAdd(facility);
        component.InitializeIfNeeded(facility);
        FacilityEvolutionState state = component.InstanceEvolution;
        if (!TryApplyToState(state, request, payload))
        {
            return false;
        }

        component.ReplaceInstanceEvolution(state);
        return true;
    }

    private bool ApplyEquipmentNarrative(
        EvolutionNarrativeRequestSnapshot request,
        EvolutionHistoryNarrativeResponseDto payload)
    {
        if (!equipment.TryGetInstance(
                request.targetPersistentId,
                out CombatEquipmentInstance instance))
        {
            return false;
        }

        EquipmentEvolutionState state = instance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        if (!TryApplyToState(state, request, payload))
        {
            return false;
        }

        return equipment.TryUpdateEvolutionState(instance.instanceId, state);
    }

    private static bool TryApplyToState(
        FacilityEvolutionState state,
        EvolutionNarrativeRequestSnapshot request,
        EvolutionHistoryNarrativeResponseDto payload)
    {
        EvolutionNode node = state.evolutionNodes?.FirstOrDefault(entry =>
            MatchesNode(entry, request));
        EvolutionNarrativeRequestSnapshot stored =
            state.narrativeRequests?.FirstOrDefault(entry =>
                entry != null
                && string.Equals(
                    entry.requestKey,
                    request.requestKey,
                    StringComparison.Ordinal));
        return ApplyValidatedNarrative(node, stored, payload);
    }

    private static bool TryApplyToState(
        EquipmentEvolutionState state,
        EvolutionNarrativeRequestSnapshot request,
        EvolutionHistoryNarrativeResponseDto payload)
    {
        EvolutionNode node = state.evolutionNodes?.FirstOrDefault(entry =>
            MatchesNode(entry, request));
        EvolutionNarrativeRequestSnapshot stored =
            state.narrativeRequests?.FirstOrDefault(entry =>
                entry != null
                && string.Equals(
                    entry.requestKey,
                    request.requestKey,
                    StringComparison.Ordinal));
        return ApplyValidatedNarrative(node, stored, payload);
    }

    private static bool ApplyValidatedNarrative(
        EvolutionNode node,
        EvolutionNarrativeRequestSnapshot stored,
        EvolutionHistoryNarrativeResponseDto payload)
    {
        if (node == null || stored == null || stored.completed || stored.cancelled)
        {
            return false;
        }

        node.displayName = payload.displayName.Trim();
        node.description = string.Join(
            "\n",
            payload.description.Trim(),
            payload.historyReason.Trim());
        NarrativeGenerationTrace trace = payload.narrativeTrace;
        if (trace != null)
        {
            node.narrativeSchemaId = trace.schemaId ?? string.Empty;
            node.narrativeSchemaVersion = trace.schemaVersion;
            node.narrativeSchemaHash = trace.schemaHash ?? string.Empty;
            node.narrativeCultureStyleId = trace.cultureStyleId ?? string.Empty;
            node.narrativeMotifIds = trace.usedMotifIds?.ToList() ?? new List<string>();
            node.narrativeCharacterFactIds = trace.usedCharacterFactIds?.ToList() ?? new List<string>();
            node.narrativePassVerdict = trace.verdict.ToString();
            node.narrativeRetryCount = trace.retryCount;
            node.narrativeUsedFallback = trace.usedFallback;
        }
        node.narrativeReady = true;
        node.uiVisible = true;
        node.playerVisible = true;
        stored.completed = true;
        return true;
    }

    private static bool MatchesNode(
        EvolutionNode node,
        EvolutionNarrativeRequestSnapshot request)
    {
        return node != null
            && node.historical
            && string.Equals(node.nodeId, request.nodeId, StringComparison.Ordinal)
            && string.Equals(
                node.parentNodeId ?? string.Empty,
                request.parentNodeId ?? string.Empty,
                StringComparison.Ordinal)
            && string.Equals(node.effectId, request.effectId, StringComparison.Ordinal);
    }

    private bool TryResolveSnapshot(
        string requestKey,
        out EvolutionNarrativeRequestSnapshot request)
    {
        request = null;
        foreach (BuildableObject building in buildings.Buildings)
        {
            if (building == null || building.isDestroy)
            {
                continue;
            }

            FacilityEvolutionStateComponent component =
                building.GetComponent<FacilityEvolutionStateComponent>();
            EvolutionNarrativeRequestSnapshot found = component?
                .InstanceEvolution?
                .narrativeRequests?
                .FirstOrDefault(entry => entry != null
                    && string.Equals(
                        entry.requestKey,
                        requestKey,
                        StringComparison.Ordinal));
            if (found != null)
            {
                if (!string.Equals(
                        component.FacilityPersistentId,
                        found.targetPersistentId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Evolution narrative request target does not match its facility owner.");
                }
                request = found.Clone();
                return true;
            }
        }

        foreach (CombatEquipmentInstance instance in equipment.Instances)
        {
            EvolutionNarrativeRequestSnapshot found = instance?
                .evolution?
                .narrativeRequests?
                .FirstOrDefault(entry => entry != null
                    && string.Equals(
                        entry.requestKey,
                        requestKey,
                        StringComparison.Ordinal));
            if (found != null)
            {
                if (!string.Equals(
                        instance.instanceId,
                        found.targetPersistentId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Evolution narrative request target does not match its equipment owner.");
                }
                request = found.Clone();
                return true;
            }
        }

        return false;
    }

    private bool TryBindAndPersistPublicContext(
        EvolutionNarrativeRequestSnapshot request,
        string envelopeSemanticHash,
        out string failureReason)
    {
        if (request == null)
        {
            failureReason = "Evolution narrative request is unavailable.";
            return false;
        }

        if (!EvolutionNarrativePublicContextBinding.TryBind(
                request.publicContextSemanticHash,
                envelopeSemanticHash,
                out string boundSemanticHash,
                out failureReason))
        {
            return false;
        }

        if (string.Equals(
                request.publicContextSemanticHash,
                boundSemanticHash,
                StringComparison.Ordinal))
        {
            return true;
        }

        request.publicContextSemanticHash = boundSemanticHash;
        if (PersistRequest(request)) return true;

        failureReason = "Public context semantic hash could not be persisted.";
        return false;
    }

    private bool PersistRequest(EvolutionNarrativeRequestSnapshot request)
    {
        if (request.targetKind == EvolutionNarrativeTargetKind.Facility)
        {
            BuildableObject facility = FindFacility(request.targetPersistentId);
            FacilityEvolutionStateComponent component =
                facility != null ? facilityStates.GetOrAdd(facility) : null;
            if (component == null)
            {
                return false;
            }

            FacilityEvolutionState state = component.InstanceEvolution;
            ReplaceRequest(state.narrativeRequests, request);
            component.ReplaceInstanceEvolution(state);
            return true;
        }

        if (equipment.TryGetInstance(
                request.targetPersistentId,
                out CombatEquipmentInstance instance))
        {
            EquipmentEvolutionState state = instance.evolution?.Clone()
                ?? new EquipmentEvolutionState();
            ReplaceRequest(state.narrativeRequests, request);
            return equipment.TryUpdateEvolutionState(instance.instanceId, state);
        }

        return false;
    }

    private static void ReplaceRequest(
        IList<EvolutionNarrativeRequestSnapshot> requests,
        EvolutionNarrativeRequestSnapshot replacement)
    {
        if (requests == null || replacement == null)
        {
            return;
        }

        for (int index = 0; index < requests.Count; index++)
        {
            EvolutionNarrativeRequestSnapshot current = requests[index];
            if (current != null
                && string.Equals(
                    current.requestKey,
                    replacement.requestKey,
                    StringComparison.Ordinal))
            {
                requests[index] = replacement.Clone();
                return;
            }
        }
    }

    private void ScheduleRetry(
        EvolutionNarrativeRequestSnapshot request,
        float now)
    {
        float delay = Mathf.Min(
            120f,
            2f * Mathf.Pow(2f, Mathf.Min(6, request?.attemptCount ?? 0)));
        retryAt[request?.requestKey ?? string.Empty] = now + delay;
    }

    private void CancelRequest(string requestKey)
    {
        inFlight.Remove(requestKey);
        retryAt.Remove(requestKey);
        if (llmRuntimeProvider.TryGetRuntime(out ILocalLlmRuntime runtime)
            && runtime is ICorrelatedEvolutionHistoryLlmRuntime correlated)
        {
            correlated.CancelEvolutionHistoryRequest(requestKey);
        }
    }

    private BuildableObject FindFacility(string facilityPersistentId)
    {
        return buildings.Buildings.FirstOrDefault(building =>
        {
            if (building == null || building.isDestroy)
            {
                return false;
            }

            FacilityEvolutionStateComponent component =
                building.GetComponent<FacilityEvolutionStateComponent>();
            return component != null
                && string.Equals(
                    component.FacilityPersistentId,
                    facilityPersistentId,
                    StringComparison.Ordinal);
        });
    }

    private string ResolveTargetDisplayName(
        EvolutionNarrativeRequestSnapshot request)
    {
        if (request == null) return string.Empty;
        if (request.targetKind == EvolutionNarrativeTargetKind.Equipment
            && equipment.TryGetInstance(
                request.targetPersistentId,
                out CombatEquipmentInstance instance)
            && instance != null
            && equipment.TryGetDefinition(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition)
            && definition != null
            && !string.IsNullOrWhiteSpace(definition.DisplayName))
        {
            return definition.DisplayName.Trim();
        }

        if (request.targetKind == EvolutionNarrativeTargetKind.Facility)
        {
            BuildableObject facility = FindFacility(request.targetPersistentId);
            if (facility?.BuildingData != null)
            {
                string displayName = FacilityShopService.GetBuildingName(
                    facility.BuildingData);
                if (!string.IsNullOrWhiteSpace(displayName))
                    return displayName.Trim();
            }
        }

        return request.targetPersistentId?.Trim() ?? string.Empty;
    }

    private IReadOnlyDictionary<string, string> ResolveParticipantDisplayNames(
        EvolutionNarrativeRequestSnapshot request)
    {
        return ResolveParticipantDisplayNamesFromWorld(
            buildings,
            request?.participantIds);
    }

    public static IReadOnlyDictionary<string, string>
        ResolveParticipantDisplayNamesFromWorld(
            IBuildingWorldQuery world,
            IEnumerable<string> participantIds)
    {
        HashSet<string> requested = new HashSet<string>(
            participantIds?.Where(id => !string.IsNullOrWhiteSpace(id))
                ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        Dictionary<string, string> resolved =
            new Dictionary<string, string>(StringComparer.Ordinal);
        if (requested.Count == 0) return resolved;

        if (world == null)
            throw new ArgumentNullException(nameof(world));
        if (world is ICharacterWorldQuery currentCharacters)
            AddCharacterDisplayNames(currentCharacters.Characters, requested, resolved);
        if (world is ICharacterLifetimeQuery lifetimeCharacters)
            AddCharacterDisplayNames(lifetimeCharacters.AllCharacters, requested, resolved);
        return resolved;
    }

    private static void AddCharacterDisplayNames(
        IEnumerable<CharacterActor> characters,
        ISet<string> requested,
        IDictionary<string, string> resolved)
    {
        foreach (CharacterActor actor in characters ?? Array.Empty<CharacterActor>())
        {
            string id = actor?.Identity?.PersistentId?.Trim() ?? string.Empty;
            string displayName = actor?.Identity?.DisplayName?.Trim() ?? string.Empty;
            if (id.Length == 0
                || displayName.Length == 0
                || !requested.Contains(id)
                || resolved.ContainsKey(id))
            {
                continue;
            }
            resolved.Add(id, displayName);
        }
    }

    private static string BuildPromptV25(
        EvolutionNarrativeRequestSnapshot request)
    {
        return EvolutionNarrativePromptFormatter
            .BuildEvolutionHistoryPromptEnvelope(
                request,
                request?.targetPersistentId,
                null)
            .Prompt;
    }

    [Obsolete("V25 uses BuildPromptV25; retained only to preserve old debug reflection hooks.")]
    private static string BuildPrompt(
        EvolutionNarrativeRequestSnapshot request)
    {
        return BuildPromptV25(request);
    }
}
