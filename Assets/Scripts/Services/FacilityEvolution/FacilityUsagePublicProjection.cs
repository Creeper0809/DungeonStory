using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

// Read-only projection of the selected, frozen source. Never reads the live
// usage ledger or turns facility usage into a character's exact/intense memory.
public static class FacilityUsagePublicProjection
{
    [Serializable]
    public sealed class Document
    {
        public int facilityUsageFactsVersion = 1;
        public List<Fact> facilityUsageFacts = new();
    }

    [Serializable]
    public sealed class Fact
    {
        public string factId;
        public string sourceKind = "facility-usage";
        public string text;
        public PublicEvent @event;
    }

    [Serializable]
    public sealed class PublicEvent
    {
        public string eventId;
        public string actorId;
        public string targetId;
        public string outcomeId;
        public int generation;
        public int repeatCount;
        public GameplayNarrativeEventContext narrativeContext;
    }

    public static string BuildJson(EvolutionNode node, FacilityEvolutionState state)
    {
        Require(node?.evidenceIds != null && node.evidenceIds.Count > 0,
            "Selected evidence is missing.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Document document = new();
        foreach (string id in node.evidenceIds.OrderBy(value => value, StringComparer.Ordinal))
        {
            Require(!string.IsNullOrWhiteSpace(id) && seen.Add(id),
                "Selected evidence is empty or duplicated.");
            var exact = node.gameplayOutcomeEvidence?.Where(value => value != null
                && string.Equals(value.publicFactId, id, StringComparison.Ordinal)).ToArray()
                ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>();
            Require(exact.Length <= 1, "Exact evidence is duplicated.");
            if (exact.Length == 1)
            {
                Require(!id.StartsWith("facility-fact:", StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(exact[0].canonicalFactText),
                    "Exact evidence is ambiguous or has no public content.");
                continue;
            }

            Require(state != null && !string.IsNullOrWhiteSpace(state.facilityPersistentId),
                "Selected usage has no saved owner.");
            var records = state.formulaEvidence?.Where(value => value != null
                && string.Equals(value.evidenceId, id, StringComparison.Ordinal)).ToArray();
            Require(records?.Length == 1 && records[0].originalEvent != null,
                "Selected frozen usage is missing or duplicated.");
            FacilityFormulaEvidenceRecord record = records[0];
            UsageLedgerEvent source = record.originalEvent;
            Require(!string.IsNullOrWhiteSpace(source.evidenceId) && source.sequence > 0,
                "Frozen usage identity is invalid.");
            string expectedId = "facility-fact:" + NarrativeInferenceHash.ComputeSha256Utf8(
                state.facilityPersistentId + "|" + source.evidenceId + "|"
                + source.sequence.ToString(CultureInfo.InvariantCulture)).Substring("sha256:".Length);
            Require(id == expectedId && source.targetId == state.facilityPersistentId
                && !string.IsNullOrWhiteSpace(source.eventId) && source.actorId != null
                && source.outcomeId != null && source.generation >= 0 && source.repeatCount > 0,
                "Frozen usage hash, owner or event is invalid.");
            Require(record.domainKey == "facility" && record.eventGroupKey == source.eventId
                && record.actionKey == (string.IsNullOrEmpty(source.outcomeId) ? source.eventId : source.outcomeId)
                && record.relationshipKey == source.actorId + ">" + source.targetId,
                "Frozen formula record differs from its usage.");
            GameplayNarrativeEventContext context = source.narrativeContext;
            Require(context != null
                && !string.IsNullOrWhiteSpace(context.eventInstanceId)
                && !string.IsNullOrWhiteSpace(context.actorId)
                && !string.IsNullOrWhiteSpace(context.actorDisplayName)
                && !string.IsNullOrWhiteSpace(context.locationDisplayName)
                && !string.IsNullOrWhiteSpace(context.resultDetail)
                && context.usedObjectDisplayName != null && context.counterpartyId != null
                && context.counterpartyDisplayName != null
                && context.chainId == state.facilityPersistentId
                && context.locationId == state.facilityPersistentId
                && context.usedObjectId == state.facilityPersistentId
                && (source.actorId.Length == 0 || source.actorId == context.actorId)
                && context.occurredDay >= 0 && context.sequence == source.sequence,
                "Frozen usage public context is missing or inconsistent.");
            // JSON round-trip copies exact strings; Context.Clone normalizes them.
            var publicEvent = new PublicEvent
            {
                eventId = source.eventId, actorId = source.actorId,
                targetId = source.targetId, outcomeId = source.outcomeId,
                generation = source.generation, repeatCount = source.repeatCount,
                narrativeContext = JsonUtility.FromJson<GameplayNarrativeEventContext>(JsonUtility.ToJson(context))
            };
            document.facilityUsageFacts.Add(new Fact
            {
                factId = id, @event = publicEvent,
                text = "행동: " + source.eventId + "; 주체: " + context.actorDisplayName
                    + "; 장소: " + context.locationDisplayName
                    + (context.usedObjectDisplayName.Length > 0 ? "; 사용 대상: " + context.usedObjectDisplayName : "")
                    + (context.counterpartyDisplayName.Length > 0 ? "; 상대: " + context.counterpartyDisplayName : "")
                    + "; 결과: " + context.resultDetail + "; 기록된 횟수: "
                    + source.repeatCount.ToString(CultureInfo.InvariantCulture) + "."
            });
        }
        return document.facilityUsageFacts.Count == 0 ? null : JsonUtility.ToJson(document);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Facility public usage: " + message);
    }
}
