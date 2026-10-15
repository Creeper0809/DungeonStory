#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

public static class EquipmentPresentationContextDebugScenarios
{
    private const string StateJsonKey = "state-json";
    private const string ExactMetricId = "context-impact";
    private const double ExactMetricValue = 97.054885864257813d;

    public static void VerifyCodecAndBinding(CombatEquipmentInstance source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        string sourceJson = JsonUtility.ToJson(source);

        CombatEquipmentInstance legacy = source.Clone();
        EvolutionNode legacyNode = RequireSingleFormulaNode(legacy);
        EquipmentEvolutionPresentationRequest legacyRequest =
            RequireSinglePresentationRequest(legacy);
        RequireRequestMatchesNode(legacy, legacyNode, legacyRequest);
        legacyRequest.publicContextVersion = 0;
        legacyRequest.publicContextHash = string.Empty;
        legacyRequest.publicContext = null;

        ItemInstanceComponentSaveData legacyDto = EquipmentItemStateCodec.Encode(legacy);
        Require(EquipmentItemStateCodec.TryDecode(
                legacyDto,
                out CombatEquipmentInstance restoredLegacy,
                out string error),
            "Legacy uncaptured equipment context did not decode: " + error);
        Require(RequireSinglePresentationRequest(restoredLegacy).RestorePublicContext() == null,
            "Legacy uncaptured equipment context was not preserved explicitly.");
        Require(string.Equals(
                    JsonUtility.ToJson(legacy.evolution.usageLedger),
                    JsonUtility.ToJson(restoredLegacy.evolution.usageLedger),
                    StringComparison.Ordinal)
                && string.Equals(
                    SerializeFormulaEvidence(legacy.evolution.formulaEvidence),
                    SerializeFormulaEvidence(restoredLegacy.evolution.formulaEvidence),
                    StringComparison.Ordinal),
            "Legacy usage or formula evidence changed while decoding uncaptured context.");
        RequireSourceUnchanged(source, sourceJson);

        CombatEquipmentInstance exactLegacy = AddValidExactBinding(legacy);
        ItemInstanceComponentSaveData exactLegacyDto =
            EquipmentItemStateCodec.Encode(exactLegacy);
        Require(EquipmentItemStateCodec.TryDecode(
                exactLegacyDto,
                out CombatEquipmentInstance restoredExactLegacy,
                out error),
            "Legacy uncaptured exact binding did not decode: " + error);
        EquipmentEvolutionPresentationRequest exactLegacyRequest =
            RequireSinglePresentationRequest(exactLegacy);
        EquipmentEvolutionPresentationRequest restoredExactLegacyRequest =
            RequireSinglePresentationRequest(restoredExactLegacy);
        Require(restoredExactLegacyRequest.RestorePublicContext() == null,
            "Legacy exact-binding fixture unexpectedly captured public context.");
        RequireExactBindingRoundTrip(exactLegacyRequest, restoredExactLegacyRequest);
        EquipmentPhysicalStatePayload nativeControl = JsonUtility.FromJson<EquipmentPhysicalStatePayload>(
            exactLegacyDto.values.Single(value => value.key == StateJsonKey).stringValue);
        double nativeValue = RequireSinglePresentationRequest(nativeControl.equipment)
            .gameplayOutcomeEvidence.Single().metrics.Single().value;
        double exactValue = restoredExactLegacyRequest.gameplayOutcomeEvidence.Single().metrics.Single().value;
        Debug.Log("Equipment exact numeric codec control: expected="
            + ExactMetricValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
            + "; native=" + nativeValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
            + "; restored=" + exactValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        RequireRejected(
            exactLegacyDto,
            source,
            sourceJson,
            "v0 request/node exact-binding payload divergence",
            payload =>
            {
                EquipmentEvolutionPresentationRequest request =
                    RequireSinglePresentationRequest(payload.equipment);
                EvolutionNode node = RequireSingleFormulaNode(payload.equipment);
                GameplayOutcomeEvidenceBindingSnapshot requestBinding =
                    request.gameplayOutcomeEvidence.Single();
                GameplayOutcomeEvidenceBindingSnapshot nodeBinding =
                    node.gameplayOutcomeEvidence.Single();
                requestBinding.canonicalFactText += " 변조";
                Require(string.Equals(
                            requestBinding.publicFactId,
                            nodeBinding.publicFactId,
                            StringComparison.Ordinal)
                        && GameplayOutcomeEvidenceBindingAuthority.TryValidate(
                            requestBinding,
                            out _)
                        && GameplayOutcomeEvidenceBindingAuthority.TryValidate(
                            nodeBinding,
                            out _)
                        && !string.Equals(
                            JsonUtility.ToJson(requestBinding),
                            JsonUtility.ToJson(nodeBinding),
                            StringComparison.Ordinal),
                    "V0 divergence fixture did not retain one valid ID with different payloads.");
            });

        CombatEquipmentInstance bound = exactLegacy.Clone();
        EvolutionNode boundNode = RequireSingleFormulaNode(bound);
        EquipmentEvolutionPresentationRequest boundRequest =
            RequireSinglePresentationRequest(bound);
        NarrativePublicContextMaterial material = BuildPublicMaterial(
            bound,
            boundNode,
            boundRequest);
        boundRequest.BindPublicContext(material);
        boundRequest.ValidatePublicContext();
        Require(boundRequest.publicContextVersion == 1
                && !string.IsNullOrWhiteSpace(boundRequest.publicContextHash)
                && boundRequest.publicContext != null,
            "Equipment presentation context did not freeze before encoding.");

        ItemInstanceComponentSaveData encoded = EquipmentItemStateCodec.Encode(bound);
        Require(EquipmentItemStateCodec.TryDecode(
                encoded,
                out CombatEquipmentInstance restored,
                out error),
            "Frozen equipment presentation context did not decode: " + error);
        EquipmentEvolutionPresentationRequest restoredRequest =
            RequireSinglePresentationRequest(restored);
        NarrativePublicContextMaterial restoredMaterial =
            restoredRequest.RestorePublicContext();
        Require(string.Equals(
                    material.SemanticHash,
                    restoredMaterial.SemanticHash,
                    StringComparison.Ordinal),
            "Equipment presentation context semantic hash changed through the item codec.");
        Require(restoredRequest.publicContextVersion == boundRequest.publicContextVersion
                && string.Equals(
                    restoredRequest.publicContextHash,
                    boundRequest.publicContextHash,
                    StringComparison.Ordinal),
            "Equipment presentation request context binding changed through the item codec.");
        Require(Encoding.UTF8.GetBytes(material.ToModelInput().CanonicalPayload)
                .SequenceEqual(Encoding.UTF8.GetBytes(
                    restoredMaterial.ToModelInput().CanonicalPayload)),
            "Equipment presentation context model-input bytes changed through the item codec.");
        Require(string.Equals(
                    SerializeBindings(boundRequest.gameplayOutcomeEvidence),
                    SerializeBindings(restoredRequest.gameplayOutcomeEvidence),
                    StringComparison.Ordinal),
            "Exact gameplay-outcome bindings changed through the item codec.");
        RequireExactBindingRoundTrip(boundRequest, restoredRequest);
        Require(!ReferenceEquals(boundRequest, restoredRequest)
                && !ReferenceEquals(boundRequest.publicContext, restoredRequest.publicContext)
                && !ReferenceEquals(
                    boundRequest.publicContext.entities,
                    restoredRequest.publicContext.entities)
                && !ReferenceEquals(
                    bound.evolution.formulaEvidence,
                    restored.evolution.formulaEvidence),
            "Equipment context codec restoration retained mutable source references.");
        Require(restoredRequest.publicContext.entities.Count > 0,
            "Frozen equipment context has no subject entity for clone verification.");
        string boundSubjectName = boundRequest.publicContext.entities[0].displayName;
        restoredRequest.publicContext.entities[0].displayName += " 변조";
        Require(string.Equals(
                    boundRequest.publicContext.entities[0].displayName,
                    boundSubjectName,
                    StringComparison.Ordinal),
            "Mutating a decoded public-context child changed the encoded source graph.");
        RequireSourceUnchanged(source, sourceJson);

        RequireRejected(encoded, source, sourceJson, "public name text", payload =>
        {
            EquipmentEvolutionPresentationRequest request =
                RequireSinglePresentationRequest(payload.equipment);
            Require(request.publicContext?.entities?.Count > 0,
                "Name-tamper fixture has no public entity.");
            request.publicContext.entities[0].displayName += " 변조";
        });
        RequireRejected(encoded, source, sourceJson, "missing public-context payload", payload =>
            RequireSinglePresentationRequest(payload.equipment).publicContext = null);
        RequireRejected(encoded, source, sourceJson, "unsupported public-context version", payload =>
            RequireSinglePresentationRequest(payload.equipment).publicContextVersion = 2);
        RequireRejected(encoded, source, sourceJson, "zeroed version only", payload =>
            RequireSinglePresentationRequest(payload.equipment).publicContextVersion = 0);
        RequireRejected(encoded, source, sourceJson, "zeroed version and missing payload", payload =>
        {
            EquipmentEvolutionPresentationRequest request =
                RequireSinglePresentationRequest(payload.equipment);
            request.publicContextVersion = 0;
            request.publicContext = null;
        });
        RequireRejected(encoded, source, sourceJson, "changed request history", payload =>
            RequireSinglePresentationRequest(payload.equipment).historyHash += ":tampered");
        RequireRejected(encoded, source, sourceJson, "changed request identity", payload =>
        {
            EquipmentEvolutionPresentationRequest request =
                RequireSinglePresentationRequest(payload.equipment);
            EvolutionNode node = RequireSingleFormulaNode(payload.equipment);
            string previousId = request.presentationId;
            string changedId = "presentation:equipment:" + new string('f', 64);
            if (string.Equals(previousId, changedId, StringComparison.Ordinal))
                changedId = "presentation:equipment:" + new string('e', 64);
            request.presentationId = changedId;
            node.presentationId = changedId;
            if (string.Equals(node.moduleSelectionId, previousId, StringComparison.Ordinal))
                node.moduleSelectionId = changedId;
        });
        RequireRejected(encoded, source, sourceJson, "changed request owner", payload =>
        {
            EquipmentEvolutionPresentationRequest request =
                RequireSinglePresentationRequest(payload.equipment);
            if (!string.IsNullOrEmpty(request.attunementOwnerPersistentId))
                request.attunementOwnerPersistentId += ":tampered";
            else
                request.reforgeOrderId += ":tampered";
        });
        RequireRejected(encoded, source, sourceJson, "changed request evidence", payload =>
        {
            EquipmentEvolutionPresentationRequest request =
                RequireSinglePresentationRequest(payload.equipment);
            request.evidenceIds[0] += ":tampered";
            request.evidenceIds = request.evidenceIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        });
        RequireRejected(
            encoded,
            source,
            sourceJson,
            "v1 matched node/request exact-binding payload tamper",
            payload =>
            {
                EquipmentEvolutionPresentationRequest request =
                    RequireSinglePresentationRequest(payload.equipment);
                EvolutionNode node = RequireSingleFormulaNode(payload.equipment);
                request.gameplayOutcomeEvidence.Single().canonicalFactText += " 변조";
                node.gameplayOutcomeEvidence.Single().canonicalFactText += " 변조";
                Require(GameplayOutcomeEvidenceBindingAuthority.TryValidate(
                            request.gameplayOutcomeEvidence.Single(),
                            out _)
                        && GameplayOutcomeEvidenceBindingAuthority.TryValidate(
                            node.gameplayOutcomeEvidence.Single(),
                            out _),
                    "V1 matched binding tamper stopped being semantically valid.");
                request.ValidateFrozenNodeBinding(node);
            });
        RequireSourceUnchanged(source, sourceJson);
    }

    private static CombatEquipmentInstance AddValidExactBinding(
        CombatEquipmentInstance legacy)
    {
        CombatEquipmentInstance fixture = legacy.Clone();
        EvolutionNode node = RequireSingleFormulaNode(fixture);
        EquipmentEvolutionPresentationRequest request =
            RequireSinglePresentationRequest(fixture);
        Require((node.gameplayOutcomeEvidence?.Count ?? 0) == 0
                && (request.gameplayOutcomeEvidence?.Count ?? 0) == 0,
            "Exact-binding regression requires the legacy-only equipment fixture.");

        const string runId = "run:equipment-context-binding";
        const long outcomeSequence = 1L;
        const string outcomeTypeId = "equipment-context-binding";
        string originalFactId = new GameplayOutcomeId(
            new GameplayOutcomeRunId(runId),
            outcomeSequence).ToString();
        string publicFactId = NarrativePublicContextFactory.BuildProjectedFactId(
            "GameplayOutcome:exact:" + outcomeTypeId,
            originalFactId,
            fixture.instanceId);
        string actorId = fixture.evolution.formulaEvidence
            .Where(value => value?.originalEvent != null)
            .Select(value => value.originalEvent.actorId)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? "character:equipment-context-owner";
        GameplayOutcomeEvidenceBindingSnapshot binding = new()
        {
            schemaVersion = GameplayOutcomeEvidenceBindingAuthority.CurrentSchemaVersion,
            sourceKind = "exact",
            publicFactId = publicFactId,
            outcomeRunId = runId,
            outcomeSequence = outcomeSequence,
            outcomeTypeId = outcomeTypeId,
            subjectKindId = "equipment",
            subjectId = fixture.instanceId,
            perspectiveKind = (int)NarrativePerspectiveKind.Equipment,
            firstDay = 7,
            lastDay = 7,
            occurrenceCount = 1,
            anchorRevision = 1,
            status = (int)GameplayOutcomeStatus.Succeeded,
            subjectSalience = 1f,
            influenceUseCount = 0,
            influenceRevision = 1,
            canonicalFactText = "검증 소유자가 장비의 정확한 결과를 남겼다.",
            roleIds = new List<string> { "owner" },
            metricIds = new List<string> { ExactMetricId },
            metricReferenceIds = new List<string>(),
            factIds = new List<string> { "context-proof" },
            semanticTags = new List<string> { "equipment" },
            participants = new List<GameplayOutcomeEvidenceParticipantBindingSnapshot>
            {
                new()
                {
                    entityKindId = "character",
                    entityId = actorId,
                    roleId = "owner",
                    participationKind = (int)GameplayParticipationKind.Direct,
                    hasPerceptionEvidence = true,
                    displayText = "검증 소유자"
                }
            },
            metrics = new List<GameplayOutcomeEvidenceMetricBindingSnapshot>
            {
                new()
                {
                    metricId = ExactMetricId,
                    unitId = "points",
                    value = ExactMetricValue,
                    minimum = ExactMetricValue,
                    maximum = ExactMetricValue,
                    sampleCount = 1
                }
            },
            facts = new List<GameplayOutcomeEvidenceFactBindingSnapshot>
            {
                new() { factId = "context-proof", value = "bound" }
            },
            provenance = new List<GameplayOutcomeEvidenceProvenanceBindingSnapshot>()
        };
        binding.experience = GameplayExperienceObserverRegistration.Default
            .Observe(binding);
        Require(GameplayOutcomeEvidenceBindingAuthority.TryValidate(
                binding,
                out string bindingFailure),
            "Exact-binding regression fixture is invalid: " + bindingFailure);

        node.gameplayOutcomeEvidence.Add(binding.Clone());
        node.gameplayOutcomeEvidence = node.gameplayOutcomeEvidence
            .OrderBy(value => value.publicFactId, StringComparer.Ordinal)
            .ToList();
        request.gameplayOutcomeEvidence.Add(binding.Clone());
        request.gameplayOutcomeEvidence = request.gameplayOutcomeEvidence
            .OrderBy(value => value.publicFactId, StringComparer.Ordinal)
            .ToList();
        // An exact binding consumes a public slot, not the full formula ledger.
        // Use the same immutable window selector as production formula generation.
        string formulaLedgerBefore = SerializeFormulaEvidence(fixture.evolution.formulaEvidence);
        MethodInfo selector = typeof(EquipmentEvolutionRules).GetMethod(
            "SelectPresentationEvidenceWindow", BindingFlags.NonPublic | BindingFlags.Static);
        Require(selector != null, "Production equipment evidence-window selector is missing.");
        var window = (EquipmentEvolutionFormulaEvidenceRecord[])selector.Invoke(null,
            new object[] { fixture.evolution.formulaEvidence, new[] { binding } });
        List<string> selectedIds = window.Select(value => value.evidenceId)
            .Append(publicFactId).OrderBy(value => value, StringComparer.Ordinal).ToList();
        Require(selectedIds.Count == Math.Min(fixture.evolution.formulaEvidence.Count,
                    NarrativeRequestContext.MaximumFacts - 1) + 1
                && selectedIds.Count <= NarrativeRequestContext.MaximumFacts
                && string.Equals(formulaLedgerBefore,
                    SerializeFormulaEvidence(fixture.evolution.formulaEvidence), StringComparison.Ordinal),
            "Exact-binding fixture changed the full ledger or exceeded the production public window.");
        node.evidenceIds = new List<string>(selectedIds);
        request.evidenceIds = new List<string>(selectedIds);
        request.ValidateFrozenNodeBinding(node);
        return fixture;
    }

    private static NarrativePublicContextMaterial BuildPublicMaterial(
        CombatEquipmentInstance equipment,
        EvolutionNode node,
        EquipmentEvolutionPresentationRequest request)
    {
        GameplayOutcomeEvidenceBindingSnapshot[] exactBindings =
            (request.gameplayOutcomeEvidence
                ?? new List<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .OrderBy(value => value.publicFactId, StringComparer.Ordinal)
            .ToArray();
        Require(exactBindings.Length
                == (request.gameplayOutcomeEvidence?.Count ?? 0),
            "Equipment request contains a null exact evidence binding.");
        HashSet<string> exactIds = exactBindings
            .Select(value => value.publicFactId)
            .ToHashSet(StringComparer.Ordinal);
        Require(exactIds.Count == exactBindings.Length
                && exactIds.All(id => request.evidenceIds.Contains(
                    id,
                    StringComparer.Ordinal)),
            "Equipment request exact bindings do not match its evidence IDs.");

        EquipmentEvolutionFormulaEvidenceRecord[] formulaEvidence =
            (equipment.evolution.formulaEvidence
                ?? new List<EquipmentEvolutionFormulaEvidenceRecord>())
            .Where(value => value != null
                && request.evidenceIds.Contains(value.evidenceId, StringComparer.Ordinal)
                && !exactIds.Contains(value.evidenceId))
            .OrderBy(value => value.originalEvent?.sequence ?? 0L)
            .ThenBy(value => value.evidenceId, StringComparer.Ordinal)
            .ToArray();
        Require(formulaEvidence.Length > 0
                && formulaEvidence.Length == request.evidenceIds.Count - exactIds.Count
                && formulaEvidence.All(value => value.originalEvent != null),
            "Equipment request lost its legacy formula evidence events.");
        List<UsageLedgerEvent> frozenEvents = formulaEvidence
            .Select(value => value.originalEvent.Clone())
            .ToList();
        string[] participantIds = frozenEvents
            .SelectMany(value => new[] { value.actorId, value.targetId })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, string> participantNames =
            new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < participantIds.Length; index++)
            participantNames.Add(participantIds[index], $"검증 공개 인물 {index + 1}");

        EvolutionNarrativeRequestSnapshot publicRequest = new()
        {
            requestKey = request.presentationId,
            targetKind = EvolutionNarrativeTargetKind.Equipment,
            targetPersistentId = equipment.instanceId,
            nodeId = node.nodeId,
            parentNodeId = node.parentNodeId,
            historyHash = request.historyHash,
            generation = node.generation,
            evidenceIds = formulaEvidence
                .Select(value => value.evidenceId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            participantIds = participantIds.ToList(),
            sourceTags = frozenEvents
                .SelectMany(value => value.sourceTags ?? new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            frozenHistoryCaptured = true,
            frozenCurrentEvents = frozenEvents
        };
        NarrativePublicContextMaterial material =
            EvolutionNarrativePromptFormatter.BuildPublicMaterial(
                LocalLlmRequestProfiles.EvolutionHistory.Id,
                publicRequest,
                "문맥 검증 장비",
                participantNames,
                exactBindings);
        Require(material.Events.Select(value => value.EvidenceId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(
                    request.evidenceIds.OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal),
            "Built equipment material did not preserve formula and exact evidence.");
        return material;
    }

    private static void RequireRejected(
        ItemInstanceComponentSaveData encoded,
        CombatEquipmentInstance source,
        string sourceJson,
        string label,
        Action<EquipmentPhysicalStatePayload> mutate)
    {
        ItemInstanceComponentSaveData tampered = encoded.Clone();
        ItemStateValueSaveData stateValue = tampered.values.Single(value => value != null
            && string.Equals(value.key, StateJsonKey, StringComparison.Ordinal)
            && value.kind == ItemStateValueKind.String);
        EquipmentPhysicalStatePayload payload =
            DungeonSaveSectionPayload.Deserialize<EquipmentPhysicalStatePayload>(
                stateValue.stringValue);
        Require(payload?.equipment != null,
            $"Could not prepare the {label} rejection fixture.");
        mutate(payload);
        stateValue.stringValue = JsonUtility.ToJson(payload);

        bool accepted = EquipmentItemStateCodec.TryDecode(
            tampered,
            out CombatEquipmentInstance rejected,
            out _);
        Require(!accepted && rejected == null,
            $"Equipment codec accepted {label} or returned a partial instance.");
        RequireSourceUnchanged(source, sourceJson);
    }

    private static EquipmentEvolutionPresentationRequest RequireSinglePresentationRequest(
        CombatEquipmentInstance equipment)
    {
        Require(equipment?.evolution?.presentationRequests != null,
            "Equipment presentation request owner is unavailable.");
        return equipment.evolution.presentationRequests.Single(value => value != null);
    }

    private static EvolutionNode RequireSingleFormulaNode(CombatEquipmentInstance equipment)
    {
        Require(equipment?.evolution?.evolutionNodes != null,
            "Equipment formula node owner is unavailable.");
        EvolutionNode[] nodes = equipment.evolution.evolutionNodes
            .Where(value => value != null)
            .ToArray();
        Require(nodes.Length == 1 && nodes[0].formulaVersion > 0,
            "Equipment context scenario requires exactly one formula node.");
        return nodes[0];
    }

    private static void RequireRequestMatchesNode(
        CombatEquipmentInstance equipment,
        EvolutionNode node,
        EquipmentEvolutionPresentationRequest request)
    {
        Require(string.Equals(
                    request.targetPersistentId,
                    equipment.instanceId,
                    StringComparison.Ordinal)
                && string.Equals(request.nodeId, node.nodeId, StringComparison.Ordinal)
                && string.Equals(
                    request.presentationId,
                    node.presentationId,
                    StringComparison.Ordinal)
                && request.evidenceIds != null
                && request.evidenceIds.Count > 0,
            "Equipment formula node and presentation request are not one pending owner.");
    }

    private static string SerializeBindings(
        IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> bindings) =>
        string.Join("\n", (bindings ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .OrderBy(value => value.publicFactId, StringComparer.Ordinal)
            .Select(value => JsonUtility.ToJson(value)));

    private static string SerializeFormulaEvidence(
        IEnumerable<EquipmentEvolutionFormulaEvidenceRecord> evidence) =>
        string.Join("\n", (evidence ?? Array.Empty<EquipmentEvolutionFormulaEvidenceRecord>())
            .Where(value => value != null)
            .OrderBy(value => value.evidenceId, StringComparer.Ordinal)
            .Select(value => JsonUtility.ToJson(value)));

    private static void RequireExactBindingRoundTrip(
        EquipmentEvolutionPresentationRequest expected,
        EquipmentEvolutionPresentationRequest actual)
    {
        Require(string.Equals(
                    SerializeBindings(expected.gameplayOutcomeEvidence),
                    SerializeBindings(actual.gameplayOutcomeEvidence),
                    StringComparison.Ordinal),
            "Exact gameplay-outcome binding JSON changed through the item codec.");
        GameplayOutcomeEvidenceMetricBindingSnapshot expectedMetric = expected
            .gameplayOutcomeEvidence.Single().metrics.Single(value =>
                string.Equals(value.metricId, ExactMetricId, StringComparison.Ordinal));
        GameplayOutcomeEvidenceMetricBindingSnapshot actualMetric = actual
            .gameplayOutcomeEvidence.Single().metrics.Single(value =>
                string.Equals(value.metricId, ExactMetricId, StringComparison.Ordinal));
        Require(BitConverter.DoubleToInt64Bits(expectedMetric.value)
                    == BitConverter.DoubleToInt64Bits(actualMetric.value)
                && BitConverter.DoubleToInt64Bits(expectedMetric.minimum)
                    == BitConverter.DoubleToInt64Bits(actualMetric.minimum)
                && BitConverter.DoubleToInt64Bits(expectedMetric.maximum)
                    == BitConverter.DoubleToInt64Bits(actualMetric.maximum),
            "Exact gameplay-outcome metric doubles changed bits through the item codec.");
    }

    private static void RequireSourceUnchanged(
        CombatEquipmentInstance source,
        string sourceJson)
    {
        Require(string.Equals(
                JsonUtility.ToJson(source),
                sourceJson,
                StringComparison.Ordinal),
            "Equipment context codec scenario mutated its source instance.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
