#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class FacilityUsagePublicProjectionDebugScenarios
{
    public static void RunOrThrow()
    {
        const string facility = "building:public-usage-fixture";
        UsageLedgerEvent original = new()
        {
            evidenceId = "usage:fixture", eventId = "facility.visit", actorId = "character:fixture",
            targetId = facility, outcomeId = "", sequence = 2, repeatCount = 3, generation = 1,
            narrativeContext = new GameplayNarrativeEventContext
            {
                eventInstanceId = "operation:fixture", chainId = facility, locationId = facility,
                locationDisplayName = "옛 시설", actorId = "character:fixture", actorDisplayName = "세나",
                usedObjectId = facility, usedObjectDisplayName = "옛 시설", resultDetail = "이용 완료\n\"기록\"",
                occurredDay = 2, sequence = 2
            }
        };
        string id = "facility-fact:" + NarrativeInferenceHash.ComputeSha256Utf8(facility + "|usage:fixture|2").Substring(7);
        FacilityEvolutionState state = new()
        {
            facilityPersistentId = facility, generation = 4,
            formulaEvidence = new List<FacilityFormulaEvidenceRecord>
            {
                new() { evidenceId = id, domainKey = "facility", eventGroupKey = original.eventId,
                    actionKey = original.eventId, relationshipKey = original.actorId + ">" + facility,
                    originalEvent = original }
            }
        };
        EvolutionNode node = new()
        {
            formulaVersion = 3, moduleSelectionId = "selection:public-usage-fixture",
            presentationState = EquipmentEvolutionPresentationState.ModuleSelectionPending,
            evidenceIds = new List<string> { id },
            moduleSelectionOffers = new List<EquipmentEvolutionModuleOfferState>
            {
                new() { moduleId = "facility:service-support", polarity = EvolutionModuleOfferPolarity.Positive,
                    semanticDescription = "손님 응대 지원" }
            }
        };
        string stateJson = JsonUtility.ToJson(state), nodeJson = JsonUtility.ToJson(node);
        string document = FacilityUsagePublicProjection.BuildJson(node, state);
        string prompt = FacilityFormulaEvolutionPresentationRuntime.BuildModuleSelectionPrompt(node, state);
        JObject parsed = JObject.Parse(document);
        Require(!document.Contains('\n') && (int)parsed["facilityUsageFactsVersion"] == 1
            && (string)parsed["facilityUsageFacts"][0]["sourceKind"] == "facility-usage"
            && (string)parsed["facilityUsageFacts"][0]["event"]["narrativeContext"]["resultDetail"] == original.narrativeContext.resultDetail,
            "JSON escaping or usage classification changed.");
        Require(prompt.Contains(document, StringComparison.Ordinal)
            && !prompt.Contains("importancePoints", StringComparison.Ordinal)
            && !prompt.Contains("influenceUseCount", StringComparison.Ordinal)
            && !prompt.Contains("authoritativeFact=", StringComparison.Ordinal)
            && JsonUtility.ToJson(state) == stateJson && JsonUtility.ToJson(node) == nodeJson,
            "Projection is missing, mutating, or exposes private mechanics.");
        Require(FacilityFormulaEvolutionPresentationRuntime.BuildModuleSelectionPrompt(
            JsonUtility.FromJson<EvolutionNode>(nodeJson), JsonUtility.FromJson<FacilityEvolutionState>(stateJson)) == prompt,
            "Restored frozen usage changed the prompt.");
        state.usageLedger.currentGenerationEvents.Add(original.Clone());
        state.usageLedger.currentGenerationEvents[0].repeatCount = 99;
        state.usageLedger.currentGenerationEvents[0].narrativeContext.actorDisplayName = "다른 현재 이름";
        Require(FacilityUsagePublicProjection.BuildJson(node, state) == document,
            "Live usage replaced frozen evidence.");
        int rejected = 0;
        foreach (Action<FacilityEvolutionState> mutation in new Action<FacilityEvolutionState>[]
        {
            value => value.facilityPersistentId = "building:other",
            value => value.formulaEvidence.Clear(),
            value => value.formulaEvidence.Add(value.formulaEvidence[0].Clone()),
            value => value.formulaEvidence[0].originalEvent.sequence++,
            value => value.formulaEvidence[0].originalEvent.repeatCount = 0,
            value => value.formulaEvidence[0].originalEvent.narrativeContext.actorId = "character:other",
            value => value.formulaEvidence[0].originalEvent.narrativeContext.actorDisplayName = "",
            value => value.formulaEvidence[0].originalEvent.narrativeContext.resultDetail = "",
            value => value.formulaEvidence[0].originalEvent.narrativeContext.locationId = "building:other",
            value => value.formulaEvidence[0].originalEvent.narrativeContext.occurredDay = -1,
            value => value.formulaEvidence[0].originalEvent.narrativeContext.sequence++,
            value => value.formulaEvidence[0].actionKey = "other"
        })
        {
            var copy = JsonUtility.FromJson<FacilityEvolutionState>(stateJson);
            mutation(copy);
            Reject(() => FacilityUsagePublicProjection.BuildJson(node, copy));
            rejected++;
        }
        Reject(() => FacilityUsagePublicProjection.BuildJson(node, null));
        var missing = JsonUtility.FromJson<EvolutionNode>(nodeJson);
        missing.evidenceIds.Add("fact:unresolved");
        Reject(() => FacilityUsagePublicProjection.BuildJson(missing, state));
        missing.evidenceIds[1] = id;
        Reject(() => FacilityUsagePublicProjection.BuildJson(missing, state));
        node.evidenceIds.Add("fact:exact");
        node.gameplayOutcomeEvidence.Add(new GameplayOutcomeEvidenceBindingSnapshot
            { publicFactId = "fact:exact", sourceKind = "exact", canonicalFactText = "별도의 정확한 사실" });
        Require(FacilityUsagePublicProjection.BuildJson(node, state) == document
            && FacilityFormulaEvolutionPresentationRuntime.BuildModuleSelectionPrompt(node, state)
                .Contains("evidence=fact:exact | authoritativeFact=별도의 정확한 사실", StringComparison.Ordinal),
            "Mixed exact and usage evidence was lost or reclassified.");
        node.gameplayOutcomeEvidence[0].sourceKind = "compacted";
        Require(FacilityUsagePublicProjection.BuildJson(node, state) == document,
            "Usage projection unexpectedly changed the existing bound-evidence route.");
        // Facility-wide revenue has no character ledger actor. Preserve this distinction.
        original.actorId = "";
        original.eventId = "facility.revenue";
        original.narrativeContext.actorId = facility;
        original.narrativeContext.actorDisplayName = "옛 시설";
        state.formulaEvidence[0].eventGroupKey = state.formulaEvidence[0].actionKey = original.eventId;
        state.formulaEvidence[0].relationshipKey = ">" + facility;
        JObject revenue = JObject.Parse(FacilityUsagePublicProjection.BuildJson(node, state));
        Require((string)revenue["facilityUsageFacts"][0]["event"]["actorId"] == ""
            && (string)revenue["facilityUsageFacts"][0]["event"]["narrativeContext"]["actorId"] == facility,
            "Facility-wide history invented a character actor.");
        Debug.Log("FACILITY_USAGE_PUBLIC_PROJECTION=PASS; rejected=" + (rejected + 3)
            + "; frozen-roundtrip=1; mixed-exact=1; non-character-route=1; model-calls=0");
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Invalid public facility source was accepted.");
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
#endif
