#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class V25NarrativeInferenceDebugScenarios
{
    [MenuItem("DungeonStory/Debug/V25/Run Narrative Inference Contracts")]
    public static void RunAll()
    {
        VerifyPublicNarrativeContinuityContract();
        VerifyChoiceCanonicalizationAndGrammar();
        VerifyAcquiredTraitExactResponseContract();
        VerifyContextAwareScheduling();
        VerifyTypedEquipmentEvidence();
        VerifyMechanicalNarrativeSeparation();
        VerifyMultiPerspectiveIdentity();
        VerifyMissingHostFailsClosed();
        VerifyCorruptModelFailsClosed();
        VerifyBundledLlamaCppBackendContract();
        VerifyAcquiredTraitRequestLocalSchemaGrammar();
        VerifyAcquiredTraitDomainEvidenceSupport();
        VerifyCharacterSkillPublicSemanticsContract();
        VerifyCanonicalCatalogJsonParity();
        VerifyIndependentCatalogExportBytes();
        Debug.Log("V25 narrative inference contracts PASS (16/16).");
    }

    public static void RunPublicNarrativeContinuityScenario() =>
        VerifyPublicNarrativeContinuityContract();

    public static void RunCompactPublicTransportScenario()
    {
        VerifyPublicNarrativeContinuityContract();
        VerifyBundledLlamaCppBackendContract();
        VerifyAcquiredTraitRequestLocalSchemaGrammar();
        VerifyAcquiredTraitDomainEvidenceSupport();
        VerifyCharacterSkillLosslessCompactTransport();
        Debug.Log("COMPACT_PUBLIC_TRANSPORT=PASS; exact-public-roundtrips=4; changed-day-and-target-distinct=1; request-local-trait-schema=1; domain-evidence-support=1; skill-lossless-transport=1; original-identity-unchanged=1; model-calls=0");
    }

    public static void RunCompactEquipmentTransportScenario()
    {
        VerifyCompactEquipmentTransport();
        Debug.Log("COMPACT_EQUIPMENT_TRANSPORT=PASS");
    }

    public static void RunFacilitySelectionIdentityTransportScenario()
    {
        VerifyFacilitySelectionIdentityTransport();
        Debug.Log("FACILITY_SELECTION_IDENTITY_TRANSPORT=PASS");
    }

    public static void RunCompactTraitDomainEvidenceScenario()
    {
        VerifyCompactTraitDomainEvidenceTransport();
        Debug.Log("COMPACT_TRAIT_DOMAIN_EVIDENCE=PASS");
    }

    public static void RunTraitGameplayEvidenceGrammarScenario()
    {
        VerifyTraitGameplayEvidenceGrammar();
        Debug.Log("TRAIT_GAMEPLAY_EVIDENCE_GRAMMAR=PASS");
    }

    public static void RunTraitEvidenceReferenceTransportScenario()
    {
        VerifyTraitEvidenceReferenceTransport();
        Debug.Log("TRAIT_EVIDENCE_REFERENCE_TRANSPORT=PASS");
    }

    public static void RunTraitProseJsonBoundaryScenario()
    {
        VerifyTraitProseJsonBoundary();
        Debug.Log("TRAIT_PROSE_JSON_BOUNDARY=PASS");
    }

    public static void RunModuleSelectionProseJsonBoundaryScenario()
    {
        VerifyModuleSelectionProseJsonBoundary();
        Debug.Log("MODULE_SELECTION_PROSE_JSON_BOUNDARY=PASS");
    }

    public static void RunProseLexicalGrammarScenario()
    {
        VerifyTraitProseJsonBoundary();
        VerifyModuleSelectionProseJsonBoundary();
        VerifyCharacterSkillDisplayNameIdentityBoundary();
        VerifyCharacterSkillLosslessCompactTransport();
        Debug.Log("PROSE_LEXICAL_GRAMMAR=PASS");
    }

    public static void RunSkillProseAuthorityInstructionScenario()
    {
        VerifyCharacterSkillLosslessCompactTransport();
        Debug.Log("SKILL_PROSE_AUTHORITY_INSTRUCTION=PASS");
    }

    public static void RunSkillNarrativeEvidenceBatch()
    {
        VerifyCharacterSkillLosslessCompactTransport();
        string prompt = CharacterProgressionDebugScenarios.VerifySkillPublicNarrativeEvidence();
        string compact = BuildCharacterSkillCompactPrompt(prompt);
        JObject mapping = JObject.Parse(compact.Split('\n').Single(line =>
            line.StartsWith("narrativeEvidence=", StringComparison.Ordinal)).Substring("narrativeEvidence=".Length));
        Require(mapping.Count == 7 && mapping.Properties().Select(value => value.Name)
                .SequenceEqual(Enumerable.Range(14, 7).Select(index => "E" + index)),
            "Not every selected narrative E alias has public content.");
        Require(NarrativePublicModelInput.TryParsePrompt(prompt, out var modelInput), "Missing canonical public input.");
        JObject canonical = JObject.Parse(modelInput.CanonicalPayload);
        foreach (JProperty property in mapping.Properties())
        {
            var value = (JObject)property.Value;
            string id = (string)value["fact"]["factId"];
            Require((string)value["sourceKind"] == "progression-aggregate"
                && JToken.DeepEquals(value["fact"], canonical["publicFacts"].Single(fact => (string)fact["factId"] == id))
                && JToken.DeepEquals(value["event"], canonical["publicNarrativeContext"]["events"].Single(fact => (string)fact["factId"] == id)),
                "Narrative E changed public actor/target/day/count/text or pretended to be an exact outcome.");
        }
        string rawMapping = prompt.Replace("\r\n", "\n").Split('\n').Single(line =>
            line.StartsWith("narrativeEvidenceRefs=", StringComparison.Ordinal));
        foreach (string broken in new[] { string.Empty, "narrativeEvidenceRefs={}", rawMapping + "\n" + rawMapping })
        {
            bool rejected = false;
            try { BuildCharacterSkillCompactPrompt(prompt.Replace(rawMapping, broken)); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Missing/empty/duplicate narrative mapping was accepted.");
        }
        string[] args = Environment.GetCommandLineArgs();
        int logIndex = Array.IndexOf(args, "-logFile");
        Require(Application.isBatchMode && logIndex >= 0, "Isolated batch output is required.");
        string output = Path.GetDirectoryName(Path.GetFullPath(args[logIndex + 1]));
        Require(output.StartsWith(Path.GetFullPath("Artifacts/QA") + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase), "QA output outside repository.");
        void WriteNew(string name, string value)
        {
            using var stream = new FileStream(Path.Combine(output, name), FileMode.CreateNew);
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }
        WriteNew("controlled-projection-prompt.txt", prompt);
        WriteNew("controlled-projection-model.txt", compact);
        WriteNew("projection-scope.json", "{\"kind\":\"controlled-historical-input-projection\",\"freshGameCapture\":false,\"trainingEligible\":false,\"humanApprovalClaimed\":false}");
        // Preserve the exact failed submission. Replaying serialization is not
        // a new game capture, model response or repair of the source request.
        string failedPrompt = File.ReadAllText(
            "Artifacts/QA/Source9Fresh-20261012-b148-r1/skill-trait-r1/unity-evidence/skill-submitted-prompt.txt");
        Require(CharacterSkillRequestRef(failedPrompt)
                == "7d03b202b98f5dc0e9cadbd5df403b24490ed8645e72dfafc17bb62547db0733",
            "Fixed failed skill submission changed.");
        string fixedModelPrompt = BuildCharacterSkillCompactPrompt(failedPrompt);
        JObject fixedTable = JObject.Parse(fixedModelPrompt.Split('\n').Single(line =>
            line.StartsWith("{", StringComparison.Ordinal)
            && line.Contains("\"transport\":\"skill-request-refs-v2\"", StringComparison.Ordinal)));
        Require(((JArray)fixedTable["H"]).OfType<JArray>().Any(header => header[8].Type == JTokenType.String),
            "Actual importance assessment disappeared during compaction.");
        byte[] fixedWire = DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
            "qa:serialization-only", LocalLlmRequestProfiles.CharacterSkillModuleSelection,
            LlmStaticSchemaCatalog.Require(LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id), failedPrompt);
        WriteNew("fixed-submission-model.txt", fixedModelPrompt);
        WriteNew("fixed-submission-wire.json", Encoding.UTF8.GetString(fixedWire));
        WriteNew("fixed-submission-scope.json", "{\"kind\":\"fixed-failed-submission-serialization-replay\",\"freshGameCapture\":false,\"networkSent\":false,\"trainingEligible\":false}");
        Debug.Log("B148_SKILL_EXPERIENCE_SERIALIZATION=PASS; fixed-failed-input=1; importance-preserved=1; model-calls=0");
        Debug.Log("B139_SKILL_NARRATIVE_MAPPING=PASS; aliases=7; exact-public-values=1; missing-duplicate-rejected=1; compact-regression=1; model-calls=0");
    }

    public static void RunAcquiredTraitExactResponseScenario() =>
        VerifyAcquiredTraitExactResponseContract();

    public static void RunIndependentCatalogExportByteVerification()
    {
        VerifyIndependentCatalogExportBytes();
        Debug.Log("V25 independent narrative catalog export byte verification PASS.");
    }

    private static void VerifyPublicNarrativeContinuityContract()
    {
        const string EquipmentId = "equipment:continuity:검증";
        const string ActorId = "character:continuity:행위자";
        string sharedLongFactId = "event:" + new string('x', 180) + ":공유-꼬리";
        NarrativePublicEntityInput[] entities =
        {
            new NarrativePublicEntityInput(
                EquipmentId,
                NarrativePublicEntityKind.Equipment,
                "계승 장비"),
            new NarrativePublicEntityInput(
                ActorId,
                NarrativePublicEntityKind.Character,
                "검증 행위자")
        };
        NarrativePublicFactInput[] facts =
        {
            BuildContinuityFact(
                "combat",
                sharedLongFactId,
                EquipmentId,
                EquipmentId,
                ActorId,
                "같은 원본 ID의 전투 기록 F01 literal"),
            BuildContinuityFact(
                "work",
                sharedLongFactId,
                EquipmentId,
                EquipmentId,
                ActorId,
                "같은 원본 ID의 작업 기록"),
            BuildContinuityFact(
                "combat",
                sharedLongFactId,
                "equipment:other-life",
                EquipmentId,
                ActorId,
                "다른 생애의 동일 원본 ID")
        };

        NarrativePublicContextMaterial material = NarrativePublicContextFactory.Build(
            "EquipmentChoiceLegacyV2",
            EquipmentId,
            NarrativePublicSubjectKind.Equipment,
            string.Empty,
            requireCharacterFact: false,
            requireMotif: false,
            entities,
            facts,
            "qa-continuity-selection-v1");

        Require(material.PublicFacts.Count == 3,
            "Public continuity material lost a distinct source tuple.");
        Require(material.PublicFacts.Select(value => value.FactId)
                .Distinct(StringComparer.Ordinal).Count() == 3,
            "Shared raw fact IDs collided across domain or source subject.");
        Require(material.PublicFacts.All(value =>
                value.FactId.StartsWith("public-fact:sha256:", StringComparison.Ordinal)
                && value.FactId.Length == "public-fact:sha256:".Length + 64),
            "Projected fact IDs are not full collision-resistant SHA-256 identities.");
        Require(material.Events.Count == 3,
            "Readable event payloads were not preserved for equipment choice.");

        NarrativePublicPromptEnvelope promptEnvelope =
            NarrativePublicPromptEnvelope.Create("공개 서사 전달 검증", material);
        string finalModelPrompt = NarrativeRequestContext.ToModelPrompt(
            promptEnvelope.Prompt);
        Require(
            NarrativePublicModelInput.TryCaptureSnapshotFromPrompt(
                finalModelPrompt,
                out NarrativePublicModelInputProjection finalProjection),
            "Final model prompt lost its structured public narrative payload.");
        NarrativePublicModelContext finalContext =
            finalProjection.PublicNarrativeContext;
        NarrativeMechanicScenarioPublicContext exportedContext =
            NarrativeMechanicScenarioPublicContextSerializer.Serialize(material);
        string exportedModelInput = NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "publicFacts",
                    exportedContext.PublicFacts),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "publicNarrativeContext",
                    exportedContext.PublicNarrativeContext))
            .ToCanonicalString();
        Require(finalContext.Entities.Any(value =>
                value != null
                && string.Equals(value.EntityId, ActorId, StringComparison.Ordinal)
                && string.Equals(value.DisplayName, "검증 행위자", StringComparison.Ordinal))
            && finalContext.Events.Count == 3
            && finalContext.Events.All(value => value.Day == 12
                && value.Count == 1
                && value.Generation == 2
                && string.Equals(value.ActorId, ActorId, StringComparison.Ordinal))
            && finalContext.MemoryFactIds.Count == 3
            && finalContext.Selection.AvailableFactCount == 3
            && finalContext.Selection.OmittedFactCount == 0
            && finalProjection.PublicFacts.Count == 3
            && finalModelPrompt.Contains(
                promptEnvelope.ModelInput.CanonicalPayload,
                StringComparison.Ordinal)
            && string.Equals(
                exportedModelInput,
                promptEnvelope.ModelInput.CanonicalPayload,
                StringComparison.Ordinal),
            "Final model prompt or export changed dates, participants, counts, generations, references, selection metadata, or public serialization.");

        NarrativePublicContextMaterial changedDate = NarrativePublicContextFactory.Build(
            "EquipmentChoiceLegacyV2",
            EquipmentId,
            NarrativePublicSubjectKind.Equipment,
            string.Empty,
            requireCharacterFact: false,
            requireMotif: false,
            entities,
            new[]
            {
                BuildContinuityFact(
                    "combat",
                    sharedLongFactId,
                    EquipmentId,
                    EquipmentId,
                    ActorId,
                    "같은 원본 ID의 전투 기록",
                    day: 13),
                facts[1],
                facts[2]
            },
            "qa-continuity-selection-v1");
        NarrativePublicContextMaterial changedTarget = NarrativePublicContextFactory.Build(
            "EquipmentChoiceLegacyV2",
            EquipmentId,
            NarrativePublicSubjectKind.Equipment,
            string.Empty,
            requireCharacterFact: false,
            requireMotif: false,
            entities,
            new[]
            {
                BuildContinuityFact(
                    "combat",
                    sharedLongFactId,
                    EquipmentId,
                    ActorId,
                    ActorId,
                    "같은 원본 ID의 전투 기록"),
                facts[1],
                facts[2]
            },
            "qa-continuity-selection-v1");
        string changedDatePrompt = NarrativeRequestContext.ToModelPrompt(
            NarrativePublicPromptEnvelope.Create("공개 서사 전달 검증", changedDate).Prompt);
        string changedTargetPrompt = NarrativeRequestContext.ToModelPrompt(
            NarrativePublicPromptEnvelope.Create("공개 서사 전달 검증", changedTarget).Prompt);
        Require(!string.Equals(finalModelPrompt, changedDatePrompt, StringComparison.Ordinal)
                && !string.Equals(finalModelPrompt, changedTargetPrompt, StringComparison.Ordinal)
                && !string.Equals(changedDatePrompt, changedTargetPrompt, StringComparison.Ordinal),
            "Date or target changes did not reach the final model input.");

        string compact = VerifyCompactPublicTransport(material);
        string compactDate = VerifyCompactPublicTransport(changedDate);
        string compactTarget = VerifyCompactPublicTransport(changedTarget);
        Require(compact != compactDate && compact != compactTarget && compactDate != compactTarget,
            "Compact transport lost a changed date or participant.");

        NarrativePublicContextMaterial restored = NarrativePublicContextFactory.Restore(
            material.CaptureSnapshot());
        Require(string.Equals(restored.SemanticHash, material.SemanticHash, StringComparison.Ordinal),
            "Public narrative snapshot round-trip changed its semantic hash.");
        Require(restored.PublicFacts.Select(value => value.FactId)
                .SequenceEqual(material.PublicFacts.Select(value => value.FactId), StringComparer.Ordinal),
            "Public narrative snapshot round-trip changed projected fact identities.");
        Require(VerifyCompactPublicTransport(restored) == compact,
            "Compact transport changed after official public snapshot restoration.");

        string delimiterA = NarrativePublicContextFactory.BuildProjectedFactId(
            "도메인:1;subject#2",
            "원본#3:값;한글",
            "대상:4;끝");
        string delimiterB = NarrativePublicContextFactory.BuildProjectedFactId(
            "도메인",
            "1;subject#2:원본#3:값",
            "한글;대상:4;끝");
        Require(!string.Equals(delimiterA, delimiterB, StringComparison.Ordinal),
            "Length-prefixed tuple encoding collapsed delimiter-heavy Korean IDs.");

        string boundA = NarrativePublicContextIdentity.Bind("request:continuity", material.SemanticHash);
        string changedHash = NarrativePublicContextFactory.Build(
            "EquipmentChoiceLegacyV2",
            EquipmentId,
            NarrativePublicSubjectKind.Equipment,
            string.Empty,
            requireCharacterFact: false,
            requireMotif: false,
            entities,
            facts.Take(2),
            "qa-continuity-selection-v1").SemanticHash;
        string boundB = NarrativePublicContextIdentity.Bind("request:continuity", changedHash);
        Require(!string.Equals(boundA, boundB, StringComparison.Ordinal),
            "Request identity ignored a changed public narrative context.");

        NarrativePublicFactInput[] overflowFacts = Enumerable.Range(0, 25)
            .Select(index => new NarrativePublicFactInput(
                "persona",
                $"fact:{index:D2}",
                ActorId,
                $"공개 사실 {index:D2}",
                100 - index,
                NarrativePublicFactCategory.Background,
                ActorId))
            .ToArray();
        NarrativePublicContextMaterial selected = NarrativePublicContextFactory.Build(
            "Persona",
            ActorId,
            NarrativePublicSubjectKind.Character,
            string.Empty,
            requireCharacterFact: false,
            requireMotif: false,
            new[]
            {
                new NarrativePublicEntityInput(
                    ActorId,
                    NarrativePublicEntityKind.Character,
                    "검증 인물")
            },
            overflowFacts,
            "qa-continuity-selection-v1");
        Require(selected.PublicFacts.Count == NarrativeRequestContext.MaximumFacts
                && selected.Selection.AvailableFactCount == 25
                && selected.Selection.OmittedFactCount == 1,
            "Deterministic public-context selection did not account for omitted facts.");
    }

    private static NarrativePublicFactInput BuildContinuityFact(
        string domain,
        string originalFactId,
        string sourceSubjectId,
        string publicSubjectId,
        string actorId,
        string text,
        int day = 12)
    {
        return new NarrativePublicFactInput(
            domain,
            originalFactId,
            sourceSubjectId,
            text,
            100,
            NarrativePublicFactCategory.Memory,
            publicSubjectId,
            "completed",
            lastDay: day,
            count: 1,
            milestoneCount: 0,
            totalValue: 1.25m,
            eventData: new NarrativePublicEventInput(
                "evidence:" + domain + ":" + sourceSubjectId,
                actorId,
                publicSubjectId,
                "continuity-event",
                "completed",
                day: day,
                count: 1,
                generation: 2));
    }

    private static string VerifyCompactPublicTransport(NarrativePublicContextMaterial material)
    {
        string traitHeader =
            "selectionId=selection:compact-public-transport\n"
            + "- moduleId=trait:compact-positive; polarity=Positive; meaning=transport fixture\n"
            + "evidenceFactIds=" + material.PublicFacts[0].FactId + "\n";
        NarrativePublicPromptEnvelope envelope = NarrativePublicPromptEnvelope.Create(
            traitHeader + "공개 전달: literal F01 stays prose", material);
        string original = envelope.Prompt;
        var method = typeof(NarrativeRequestContext).GetMethod("ToCompactPublicModelPrompt",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Require(method != null, "Compact public transport entry is missing.");
        string compact = (string)method.Invoke(null, new object[] { original });
        string jsonLine = compact.Split('\n').Single(line => line.StartsWith("{\"publicFacts\":", StringComparison.Ordinal));
        JObject encoded = JObject.Parse(jsonLine);
        encoded["publicFacts"] = ExpandCompactRows((JObject)encoded["publicFacts"], keepNulls: false);
        string[] ids = ((JArray)encoded["publicFacts"]).Select(row => (string)row["factId"]).ToArray();
        Require(ids.Length == material.PublicFacts.Count && compact.Contains("literal F01 stays prose")
            && !compact.Contains(NarrativePublicModelInput.BeginMarker), "Compact fact table/prefix differs.");
        encoded["publicNarrativeContext"]["events"] = ExpandCompactRows(
            (JObject)encoded["publicNarrativeContext"]["events"], keepNulls: true);
        foreach (JProperty property in ((JObject)encoded["publicNarrativeContext"]).Descendants().OfType<JProperty>()
                     .Where(property => property.Name == "factId" || property.Name == "evidenceId").ToArray())
            if (property.Value.Type == JTokenType.Integer)
                property.Value = ids[(int)property.Value - 1];
        foreach (string key in new[] { "backgroundFactIds", "relationshipFactIds", "memoryFactIds", "priorHistoryFactIds" })
            foreach (JValue value in ((JArray)encoded["publicNarrativeContext"][key]).OfType<JValue>())
                if (value.Type == JTokenType.Integer) value.Value = ids[(int)value - 1];
        Require(JToken.DeepEquals(encoded, JObject.Parse(envelope.ModelInput.CanonicalPayload))
            && envelope.Prompt == original, "Compact transport lost public data or changed original identity.");
        byte[] wire = DungeonStoryHostStructuredChatBackend.BuildRequestPayload("test",
            LocalLlmRequestProfiles.AcquiredTraitModuleSelection,
            LlmStaticSchemaCatalog.Require(LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id), original);
        string transported = (string)JObject.Parse(Encoding.UTF8.GetString(wire))["messages"][1]["content"];
        Require(transported == "/no_think\n" + compact, "Production trait wire bypassed compact public transport.");
        return compact;
    }

    private static void VerifyCompactEquipmentTransport()
    {
        const string EquipmentId = "equipment:compact-evolution:검증";
        const string ActorId = "character:compact-evolution:행위자";
        string[] targets =
        {
            EquipmentId,
            ActorId,
            "place:compact-evolution:공방",
            "group:compact-evolution:수비대"
        };
        NarrativePublicEntityInput[] entities =
        {
            new NarrativePublicEntityInput(
                EquipmentId,
                NarrativePublicEntityKind.Equipment,
                "압축 검증 장비"),
            new NarrativePublicEntityInput(
                ActorId,
                NarrativePublicEntityKind.Character,
                "압축 검증 행위자"),
            new NarrativePublicEntityInput(
                targets[2],
                NarrativePublicEntityKind.Place,
                "별빛 공방"),
            new NarrativePublicEntityInput(
                targets[3],
                NarrativePublicEntityKind.Group,
                "새벽 수비대")
        };
        string commonPrefix =
            "반복되는 장비 연대기의 긴 공통 머리말은 원본 사건과 효과를 생략하지 않고 그대로 보존한다. "
            + new string('가', 36) + " 경계 ";
        string commonSuffix =
            " 공통 맺음말은 공개 사실의 식별자와 시간, 횟수, 대상을 바꾸지 않는다. "
            + new string('힣', 36);
        NarrativePublicFactInput[] facts = Enumerable.Range(0, 24)
            .Select(index =>
            {
                int count = 1 + index % 5;
                int? day = index % 6 == 0 ? (int?)null : 40 + index;
                int? generation = index % 7 == 0 ? (int?)null : 2 + index % 3;
                string target = targets[index % targets.Length];
                string outcome = index % 5 == 0
                    ? string.Empty
                    : "outcome:" + index.ToString("D2");
                string text = commonPrefix
                    + (index % 2 == 0 ? "\U0001F600" : "\U0001F601")
                    + " 서로 다른 기록 " + index.ToString("D2")
                    + "; 대상=" + target + "; 문자열수치=" + (index - 12).ToString("D3")
                    + (index % 2 == 0 ? "\U0001F600" : "\U0002F600")
                    + commonSuffix;
                return new NarrativePublicFactInput(
                    "EquipmentUse",
                    "fact:compact-equipment:" + index.ToString("D2"),
                    EquipmentId,
                    text,
                    1000 - index,
                    index % 3 == 0
                        ? NarrativePublicFactCategory.PriorHistory
                        : NarrativePublicFactCategory.Memory,
                    target,
                    outcome,
                    lastDay: day,
                    count: count,
                    milestoneCount: index % 4 == 0 ? (int?)null : index % 7,
                    totalValue: index % 5 == 0 ? (decimal?)null : (index - 12) * 1.25m,
                    eventData: new NarrativePublicEventInput(
                        "evidence:compact-equipment:" + index.ToString("D2"),
                        ActorId,
                        target,
                        "equipment-use:" + index.ToString("D2"),
                        outcome,
                        day,
                        count,
                        generation));
            })
            .ToArray();
        NarrativePublicContextMaterial material = NarrativePublicContextFactory.Build(
            "EvolutionHistory",
            EquipmentId,
            NarrativePublicSubjectKind.Equipment,
            string.Empty,
            requireCharacterFact: false,
            requireMotif: false,
            entities,
            facts,
            "qa-compact-equipment-transport-v1");
        Require(material.PublicFacts.Count == 24 && material.Events.Count == 24,
            "Equipment compact fixture did not retain all 24 actual public records.");

        string equipmentHeader =
            "selectionId=selection:equipment:compact-evolution\n"
            + "- moduleId=equipment-evolution:resonant_guard; polarity=Positive; meaning=공개 기록에 따른 공명 방호 효과\n"
            + "- moduleId=equipment-evolution:burden:echo-strain; polarity=Drawback; meaning=반복 충격의 부담 효과\n"
            + "evidenceFactIds="
            + string.Join(",", material.PublicFacts.Select(value => value.FactId))
            + "\n";
        NarrativePublicPromptEnvelope envelope = NarrativePublicPromptEnvelope.Create(
            equipmentHeader + "장비 진화 반복 텍스트 전송 검증",
            material);
        string originalPrompt = envelope.Prompt;
        string originalSemanticHash = envelope.PublicContextSemanticHash;
        string pendingIdentity = NarrativePublicContextIdentity.Bind(
            "equipment-evolution:compact-transport",
            originalSemanticHash);
        string canonicalPayload = envelope.ModelInput.CanonicalPayload;

        string legacyCompact = ToCompactPublicModelPromptForTest(originalPrompt);
        string legacyJsonLine = legacyCompact.Split('\n').Single(line =>
            line.StartsWith("{\"publicFacts\":", StringComparison.Ordinal));
        JObject rowTablePayload = JObject.Parse(legacyJsonLine);
        string rowTableSnapshot = rowTablePayload.ToString(Newtonsoft.Json.Formatting.None);
        JObject encoded = CompactRepeatedPublicTextForTest(rowTablePayload);
        string encodedSnapshot = encoded.ToString(Newtonsoft.Json.Formatting.None);
        JObject expanded = ExpandCompactPublicTextForTest(encoded);
        JObject secondEncoded = CompactRepeatedPublicTextForTest(rowTablePayload);
        JArray encodedStrings = (JArray)encoded["strings"];
        JObject[] encodedTexts = ((JObject)encoded["data"]).Descendants()
            .OfType<JObject>()
            .Where(value => value["$text"] is JArray)
            .ToArray();
        JObject encodedFactTable = (JObject)encoded["data"]["publicFacts"];
        JObject encodedEventTable =
            (JObject)encoded["data"]["publicNarrativeContext"]["events"];
        JArray encodedFactColumns = (JArray)encodedFactTable["columnValues"];
        JArray encodedEventColumns = (JArray)encodedEventTable["columnValues"];
        bool IsValidColumnTable(JObject table, int expectedRows)
        {
            if (table == null
                || table.Properties().Select(property => property.Name)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(
                        new[] { "columnValues", "columns", "rowCount" },
                        StringComparer.Ordinal) == false
                || (int)table["rowCount"] != expectedRows
                || table["columns"] is not JArray columns
                || table["columnValues"] is not JArray values
                || columns.Count != values.Count)
            {
                return false;
            }
            return values.All(value => value is JArray variable
                    ? variable.Count == expectedRows
                    : value is JObject repeated
                        && repeated.Properties().Count() == 1
                        && repeated.Property("$repeat") != null);
        }
        Require(string.Equals((string)encoded["encoding"],
                    "repeated-public-text-v1", StringComparison.Ordinal)
                && encodedStrings.Count > 0
                && encodedTexts.Length > 0
                && encodedTexts.Any(value => ((JArray)value["$text"]).Any(
                        part => part.Type == JTokenType.String)
                    && ((JArray)value["$text"]).Any(part => part is JObject))
                && encodedStrings.Values<string>().All(value =>
                    value.Length == 0
                    || (!char.IsLowSurrogate(value[0])
                        && !char.IsHighSurrogate(value[value.Length - 1])))
                && IsValidColumnTable(encodedFactTable, 24)
                && IsValidColumnTable(encodedEventTable, 24)
                && encodedFactColumns.OfType<JObject>().Any(value =>
                    value.Property("$repeat") != null)
                && encodedFactColumns.OfType<JArray>().Any()
                && encodedEventColumns.OfType<JObject>().Any(value =>
                    value.Property("$repeat") != null)
                && encodedEventColumns.OfType<JArray>().Any()
                && JToken.DeepEquals(expanded, rowTablePayload)
                && JToken.DeepEquals(encoded, secondEncoded)
                && string.Equals(
                    secondEncoded.ToString(Newtonsoft.Json.Formatting.None),
                    encodedSnapshot,
                    StringComparison.Ordinal)
                && !JToken.DeepEquals(encoded["data"], rowTablePayload)
                && string.Equals(
                    rowTablePayload.ToString(Newtonsoft.Json.Formatting.None),
                    rowTableSnapshot,
                    StringComparison.Ordinal)
                && string.Equals(
                    encoded.ToString(Newtonsoft.Json.Formatting.None),
                    encodedSnapshot,
                    StringComparison.Ordinal),
            "Repeated public text encoding is not lossless, deterministic, surrogate-safe, changed, or input-immutable.");

        JObject restoredCanonical = RestoreCompactPublicRowTablePayload(expanded);
        JObject canonical = JObject.Parse(canonicalPayload);
        JObject publicFactTable = (JObject)rowTablePayload["publicFacts"];
        JObject eventTable = (JObject)rowTablePayload["publicNarrativeContext"]["events"];
        string[] factColumns = ((JArray)publicFactTable["columns"]).Values<string>().ToArray();
        string[] eventColumns = ((JArray)eventTable["columns"]).Values<string>().ToArray();
        JArray factRows = (JArray)publicFactTable["rows"];
        JArray eventRows = (JArray)eventTable["rows"];
        int factIdColumn = Array.IndexOf(factColumns, "factId");
        int factTextColumn = Array.IndexOf(factColumns, "text");
        int totalValueColumn = Array.IndexOf(factColumns, "totalValueDecimal");
        int evidenceColumn = Array.IndexOf(eventColumns, "evidenceId");
        int dayColumn = Array.IndexOf(eventColumns, "day");
        int countColumn = Array.IndexOf(eventColumns, "count");
        int targetColumn = Array.IndexOf(eventColumns, "targetId");
        Require(JToken.DeepEquals(restoredCanonical, canonical)
                && factRows.Count == 24
                && eventRows.Count == 24
                && factIdColumn >= 0
                && factTextColumn >= 0
                && totalValueColumn >= 0
                && evidenceColumn >= 0
                && dayColumn >= 0
                && countColumn >= 0
                && targetColumn >= 0
                && factRows.OfType<JArray>().Select(row => (string)row[factIdColumn])
                    .SequenceEqual(
                        material.PublicFacts.Select(value => value.FactId),
                        StringComparer.Ordinal)
                && eventRows.OfType<JArray>().Select(row => (string)row[evidenceColumn])
                    .Distinct(StringComparer.Ordinal).Count() == 24
                && eventRows.OfType<JArray>().Select(row => row[dayColumn].Type)
                    .Contains(JTokenType.Null)
                && eventRows.OfType<JArray>().Where(row => row[dayColumn].Type != JTokenType.Null)
                    .Select(row => (int)row[dayColumn]).Distinct().Count() > 1
                && eventRows.OfType<JArray>().Select(row => (int)row[countColumn])
                    .Distinct().Count() == 5
                && eventRows.OfType<JArray>().Select(row => (string)row[targetColumn])
                    .Distinct(StringComparer.Ordinal).Count() == targets.Length
                && factRows.OfType<JArray>().Any(row => row.Any(cell => cell.Type == JTokenType.Null))
                && factRows.OfType<JArray>().All(row =>
                    row[totalValueColumn].Type == JTokenType.Null
                    || row[totalValueColumn].Type == JTokenType.String)
                && factRows.OfType<JArray>().Any(row =>
                    row[totalValueColumn].Type == JTokenType.String
                    && ((string)row[totalValueColumn]).StartsWith("-", StringComparison.Ordinal)
                    && ((string)row[totalValueColumn]).Contains(".", StringComparison.Ordinal))
                && factRows.OfType<JArray>().Any(row =>
                    ((string)row[factTextColumn]).Contains("\U0001F600", StringComparison.Ordinal))
                && factRows.OfType<JArray>().Any(row =>
                    ((string)row[factTextColumn]).Contains("\U0002F600", StringComparison.Ordinal)),
            "Equipment compact public rows changed fact IDs, evidence, dates, counts, targets, nulls, numeric strings, or Unicode.");

        JObject changedEncoding = (JObject)encoded.DeepClone();
        changedEncoding["encoding"] = "unknown-public-text-v1";
        RequireInvalidOperation(
            () => ExpandCompactPublicTextForTest(changedEncoding),
            "Repeated public text expansion accepted an unknown encoding.");
        JObject negativeRowCount = (JObject)encoded.DeepClone();
        negativeRowCount["data"]["publicFacts"]["rowCount"] = -1;
        RequireInvalidOperation(
            () => ExpandCompactPublicTextForTest(negativeRowCount),
            "Repeated public text expansion accepted a negative column-table rowCount.");
        JObject mismatchedColumnLength = (JObject)encoded.DeepClone();
        JArray mismatchedValues = (JArray)mismatchedColumnLength["data"]
            ["publicFacts"]["columnValues"];
        JArray shortenedColumn = mismatchedValues.OfType<JArray>().First();
        shortenedColumn.RemoveAt(shortenedColumn.Count - 1);
        RequireInvalidOperation(
            () => ExpandCompactPublicTextForTest(mismatchedColumnLength),
            "Repeated public text expansion accepted a column length different from rowCount.");
        JObject repeatedWithExtraKey = (JObject)encoded.DeepClone();
        JArray repeatedColumns = (JArray)repeatedWithExtraKey["data"]
            ["publicFacts"]["columnValues"];
        JObject repeatedColumn = repeatedColumns.OfType<JObject>()
            .First(value => value.Property("$repeat") != null);
        repeatedColumn["extra"] = true;
        RequireInvalidOperation(
            () => ExpandCompactPublicTextForTest(repeatedWithExtraKey),
            "Repeated public text expansion accepted an extra $repeat key.");
        RequireInvalidOperation(
            () => ExpandCompactPublicTextForTest(new JObject
            {
                ["encoding"] = "repeated-public-text-v1",
                ["strings"] = new JArray("shared"),
                ["data"] = new JObject
                {
                    ["value"] = new JObject
                    {
                        ["$text"] = new JArray(new JObject { ["$s"] = 1 })
                    }
                }
            }),
            "Repeated public text expansion accepted an out-of-range string reference.");
        RequireInvalidOperation(
            () => ExpandCompactPublicTextForTest(new JObject
            {
                ["encoding"] = "repeated-public-text-v1",
                ["strings"] = new JArray("shared"),
                ["data"] = new JObject
                {
                    ["value"] = new JObject { ["$text"] = new JArray(17) }
                }
            }),
            "Repeated public text expansion accepted a non-string literal part.");
        JObject altered = (JObject)encoded.DeepClone();
        ((JArray)altered["strings"])[0] = (string)altered["strings"][0] + "변조";
        Require(!JToken.DeepEquals(
                ExpandCompactPublicTextForTest(altered),
                rowTablePayload),
            "Repeated public text catalog mutation was not detectable after expansion.");

        string evolutionCompact = ToCompactEvolutionModelPromptForTest(originalPrompt);
        string evolutionJsonLine = evolutionCompact.Split('\n').Single(line =>
            line.StartsWith("{\"encoding\":\"repeated-public-text-v1\"",
                StringComparison.Ordinal));
        JObject wireEncoded = JObject.Parse(evolutionJsonLine);
        JObject wireExpanded = ExpandCompactPublicTextForTest(wireEncoded);
        Require(JToken.DeepEquals(wireEncoded, encoded)
                && JToken.DeepEquals(
                    RestoreCompactPublicRowTablePayload(wireExpanded),
                    canonical)
                && evolutionCompact.Contains(equipmentHeader, StringComparison.Ordinal)
                && !evolutionCompact.Contains(
                    NarrativePublicModelInput.BeginMarker,
                    StringComparison.Ordinal)
                && string.Equals(envelope.Prompt, originalPrompt, StringComparison.Ordinal)
                && string.Equals(envelope.ModelInput.CanonicalPayload, canonicalPayload,
                    StringComparison.Ordinal)
                && string.Equals(envelope.PublicContextSemanticHash,
                    originalSemanticHash, StringComparison.Ordinal)
                && string.Equals(
                    NarrativePublicContextIdentity.Bind(
                        "equipment-evolution:compact-transport",
                        envelope.PublicContextSemanticHash),
                    pendingIdentity,
                    StringComparison.Ordinal),
            "Equipment evolution compact prompt changed the header, canonical public payload, prompt, or pending identity.");

        LlmStaticSchemaDefinition traitSchema = LlmStaticSchemaCatalog.Require(
            LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id);
        byte[] traitStaticSchemaBytes = traitSchema.Utf8Bytes.ToArray();
        string traitStaticSchemaHash = traitSchema.Hash;
        string traitCompact = VerifyCompactPublicTransport(material);
        Require(!traitCompact.Contains("repeated-public-text-v1", StringComparison.Ordinal)
                && traitStaticSchemaBytes.SequenceEqual(traitSchema.Utf8Bytes)
                && string.Equals(traitSchema.Hash, traitStaticSchemaHash,
                    StringComparison.Ordinal),
            "Existing Trait compact transport unexpectedly adopted Equipment repeated-text encoding.");

        LlmStaticSchemaDefinition schema = LlmStaticSchemaCatalog.Require(
            LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id);
        byte[] staticSchemaBytes = schema.Utf8Bytes.ToArray();
        string staticSchemaHash = schema.Hash;
        JObject request = JObject.Parse(Encoding.UTF8.GetString(
            DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection,
                schema,
                originalPrompt)));
        JObject actualRequestSchema =
            (JObject)request["response_format"]["json_schema"]["schema"];
        JObject expectedRequestSchema = JObject.Parse(schema.Json);
        expectedRequestSchema["properties"]["displayName"]["pattern"] =
            actualRequestSchema["properties"]["displayName"]["pattern"].DeepClone();
        expectedRequestSchema["properties"]["narrativeFlavor"]["pattern"] =
            actualRequestSchema["properties"]["narrativeFlavor"]["pattern"].DeepClone();
        Require(string.Equals(
                    (string)request["messages"][1]["content"],
                    "/no_think\n" + evolutionCompact,
                    StringComparison.Ordinal)
                && JToken.DeepEquals(
                    actualRequestSchema,
                    expectedRequestSchema)
                && staticSchemaBytes.SequenceEqual(schema.Utf8Bytes)
                && string.Equals(schema.Hash, staticSchemaHash, StringComparison.Ordinal)
                && evolutionCompact.Contains(
                    "equipment-evolution:resonant_guard",
                    StringComparison.Ordinal)
                && evolutionCompact.Contains(
                    "equipment-evolution:burden:echo-strain",
                    StringComparison.Ordinal),
            "Production Equipment wire bypassed compact transport or changed its schema/effect offers.");
    }

    private static JObject RestoreCompactPublicRowTablePayload(JObject rowTablePayload)
    {
        JObject restored = (JObject)rowTablePayload.DeepClone();
        restored["publicFacts"] = ExpandCompactRows(
            (JObject)restored["publicFacts"],
            keepNulls: false);
        string[] ids = ((JArray)restored["publicFacts"])
            .Select(row => (string)row["factId"])
            .ToArray();
        JObject context = (JObject)restored["publicNarrativeContext"];
        context["events"] = ExpandCompactRows((JObject)context["events"], keepNulls: true);
        foreach (JProperty property in context.Descendants().OfType<JProperty>()
                     .Where(property => property.Name == "factId"
                         || property.Name == "evidenceId").ToArray())
        {
            if (property.Value.Type == JTokenType.Integer)
                property.Value = ids[(int)property.Value - 1];
        }
        foreach (string key in new[]
                 {
                     "backgroundFactIds", "relationshipFactIds",
                     "memoryFactIds", "priorHistoryFactIds"
                 })
        {
            foreach (JValue value in ((JArray)context[key]).OfType<JValue>())
            {
                if (value.Type == JTokenType.Integer)
                    value.Value = ids[(int)value - 1];
            }
        }
        return restored;
    }

    private static void VerifyFacilitySelectionIdentityTransport()
    {
        const string FacilityId = "facility:selection-identity:검증";
        const string ActorId = "character:selection-identity:검증";
        const string SelectionId = "selection:facility:exact-identity";
        NarrativePublicContextMaterial material = NarrativePublicContextFactory.Build(
            "FacilityEvolutionModuleSelection",
            FacilityId,
            NarrativePublicSubjectKind.Facility,
            string.Empty,
            requireCharacterFact: false,
            requireMotif: false,
            new[]
            {
                new NarrativePublicEntityInput(
                    FacilityId,
                    NarrativePublicEntityKind.Facility,
                    "선택 식별자 검증 시설"),
                new NarrativePublicEntityInput(
                    ActorId,
                    NarrativePublicEntityKind.Character,
                    "선택 식별자 검증 행위자")
            },
            new[]
            {
                BuildContinuityFact(
                    "facility-selection",
                    "fact:facility-selection:01",
                    FacilityId,
                    FacilityId,
                    ActorId,
                    "시설 선택 식별자 전송의 공개 근거")
            },
            "qa-facility-selection-identity-v1");
        string remainingHeader =
            "- moduleId=facility-evolution:steady-work; polarity=Positive; meaning=안정된 작업 효과\n"
            + "- moduleId=facility-evolution:burden:slow-cycle; polarity=Drawback; meaning=느린 순환 부담\n"
            + "evidenceFactIds=" + material.PublicFacts[0].FactId + "\n";
        string BuildPrompt(string selectionHeader) => NarrativePublicPromptEnvelope.Create(
            selectionHeader + remainingHeader + "시설 진화 선택 식별자 전송 검증",
            material).Prompt;

        string originalPrompt = BuildPrompt("selectionId=" + SelectionId + "\n");
        string originalPromptSnapshot = originalPrompt;
        string originalSemanticHash = material.SemanticHash;
        var facilityProfile = LocalLlmRequestProfiles.FacilityEvolutionModuleSelection;
        LlmStaticSchemaDefinition facilitySchema = LlmStaticSchemaCatalog.Require(
            facilityProfile.Id);
        byte[] facilityStaticBytes = facilitySchema.Utf8Bytes.ToArray();
        string facilityStaticHash = facilitySchema.Hash;
        byte[] firstPayloadBytes = DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
            "test",
            facilityProfile,
            facilitySchema,
            originalPrompt);
        byte[] secondPayloadBytes = DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
            "test",
            facilityProfile,
            facilitySchema,
            originalPrompt);
        JObject request = JObject.Parse(Encoding.UTF8.GetString(firstPayloadBytes));
        JObject requestSchema = (JObject)request["response_format"]["json_schema"]["schema"];
        JObject expectedSchema = JObject.Parse(facilitySchema.Json);
        expectedSchema["properties"]["displayName"]["pattern"] =
            requestSchema["properties"]["displayName"]["pattern"].DeepClone();
        expectedSchema["properties"]["narrativeFlavor"]["pattern"] =
            requestSchema["properties"]["narrativeFlavor"]["pattern"].DeepClone();
        expectedSchema["properties"]["selectionId"]["enum"] = new JArray(SelectionId);
        Require(firstPayloadBytes.SequenceEqual(secondPayloadBytes)
                && JToken.DeepEquals(requestSchema, expectedSchema)
                && ((JArray)requestSchema["properties"]["selectionId"]["enum"])
                    .Values<string>().SequenceEqual(
                        new[] { SelectionId },
                        StringComparer.Ordinal)
                && string.Equals(
                    (string)request["messages"][1]["content"],
                    NarrativeRequestContext.ToModelPrompt(originalPrompt),
                    StringComparison.Ordinal)
                && (int)request["max_tokens"] == facilityProfile.MaxOutputTokens
                && facilityStaticBytes.SequenceEqual(facilitySchema.Utf8Bytes)
                && string.Equals(facilitySchema.Hash, facilityStaticHash,
                    StringComparison.Ordinal)
                && string.Equals(originalPrompt, originalPromptSnapshot,
                    StringComparison.Ordinal)
                && string.Equals(material.SemanticHash, originalSemanticHash,
                    StringComparison.Ordinal),
            "Facility selection identity wire is not exact, deterministic, schema-local, or immutable.");

        RequireInvalidOperation(
            () => DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test", facilityProfile, facilitySchema, BuildPrompt(string.Empty)),
            "Facility selection identity accepted a missing selectionId header.");
        RequireInvalidOperation(
            () => DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test", facilityProfile, facilitySchema, BuildPrompt("selectionId=\n")),
            "Facility selection identity accepted an empty selectionId header.");
        RequireInvalidOperation(
            () => DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test", facilityProfile, facilitySchema, BuildPrompt("selectionId=   \n")),
            "Facility selection identity accepted a whitespace-only selectionId header.");
        RequireInvalidOperation(
            () => DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                facilityProfile,
                facilitySchema,
                BuildPrompt(
                    "selectionId=" + SelectionId + "\n"
                    + "selectionId=selection:facility:duplicate\n")),
            "Facility selection identity accepted duplicate selectionId headers.");
        RequireInvalidOperation(
            () => DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                facilityProfile,
                facilitySchema,
                BuildPrompt("selectionId= " + SelectionId + "\n")),
            "Facility selection identity accepted leading ID whitespace.");
        RequireInvalidOperation(
            () => DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                facilityProfile,
                facilitySchema,
                BuildPrompt("selectionId=" + SelectionId + " \n")),
            "Facility selection identity accepted trailing ID whitespace.");
        RequireInvalidOperation(
            () => DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                facilityProfile,
                facilitySchema,
                BuildPrompt("\tselectionId=" + SelectionId + "\n")),
            "Facility selection identity accepted a trim-dependent header line.");

        const string ObservedContent =
            " { \"selectionId\" : \"selection:facility:raw\", \"displayName\" : \"원문\" } ";
        bool facilityResolved = DungeonStoryHostStructuredChatBackend.TryResolveModelContent(
            facilityProfile,
            originalPrompt,
            ObservedContent,
            out string resolvedContent,
            out string resolveError);
        Require(facilityResolved
                && string.IsNullOrEmpty(resolveError)
                && string.Equals(resolvedContent, ObservedContent, StringComparison.Ordinal),
            "Facility selection transport normalized or replaced raw model content.");

        var equipmentProfile = LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection;
        LlmStaticSchemaDefinition equipmentSchema = LlmStaticSchemaCatalog.Require(
            equipmentProfile.Id);
        byte[] equipmentStaticBytes = equipmentSchema.Utf8Bytes.ToArray();
        string equipmentStaticHash = equipmentSchema.Hash;
        JObject equipmentRequest = JObject.Parse(Encoding.UTF8.GetString(
            DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                equipmentProfile,
                equipmentSchema,
                originalPrompt)));
        JObject expectedEquipmentSchema = JObject.Parse(equipmentSchema.Json);
        JObject actualEquipmentSchema = (JObject)equipmentRequest
            ["response_format"]["json_schema"]["schema"];
        expectedEquipmentSchema["properties"]["displayName"]["pattern"] =
            actualEquipmentSchema["properties"]["displayName"]["pattern"].DeepClone();
        expectedEquipmentSchema["properties"]["narrativeFlavor"]["pattern"] =
            actualEquipmentSchema["properties"]["narrativeFlavor"]["pattern"].DeepClone();
        Require(JToken.DeepEquals(
                    actualEquipmentSchema,
                    expectedEquipmentSchema)
                && equipmentRequest["response_format"]["json_schema"]["schema"]
                    ["properties"]["selectionId"]["enum"] == null
                && equipmentStaticBytes.SequenceEqual(equipmentSchema.Utf8Bytes)
                && string.Equals(equipmentSchema.Hash, equipmentStaticHash,
                    StringComparison.Ordinal)
                && string.Equals(originalPrompt, originalPromptSnapshot,
                    StringComparison.Ordinal),
            "Facility selection narrowing changed the unrelated Equipment selection schema or prompt.");
    }

    private static void VerifyCompactTraitDomainEvidenceTransport()
    {
        string FullId(char value) => "public-fact:sha256:" + new string(value, 64);
        string evidenceGroupA = string.Join(",", new[]
        {
            FullId('a'), FullId('b'), FullId('c'), FullId('d'), FullId('e')
        });
        string evidenceGroupB = string.Join(",", new[]
        {
            FullId('f'), FullId('1'), FullId('2'), FullId('3'), FullId('4')
        });
        string[] moduleLines =
        {
            "- moduleId=trait:focus; polarity=Positive; meaning=집중의 결; domainEvidenceFactIds=" + evidenceGroupA,
            "- moduleId=trait:guard; polarity=Positive; meaning=수호의 결; domainEvidenceFactIds=" + evidenceGroupB,
            "- moduleId=trait:resolve; polarity=Positive; meaning=의지의 결; domainEvidenceFactIds=" + evidenceGroupA,
            "- moduleId=trait:burden:echo; polarity=Drawback; meaning=메아리 부담; domainEvidenceFactIds=" + evidenceGroupB,
            "- moduleId=trait:memory; polarity=Positive; meaning=기억의 결 😀; domainEvidenceFactIds=" + evidenceGroupA
        };
        string modelPrompt = "selectionId=selection:trait:domain-evidence\r\n"
            + moduleLines[0] + "\r\n"
            + moduleLines[1] + "\n"
            + moduleLines[2] + "\r\n"
            + moduleLines[3] + "\n"
            + moduleLines[4] + "\r\n"
            + "Return full evidenceFactIds only; DG keys are transport metadata, not response IDs.";
        string modelPromptSnapshot = modelPrompt;
        string grouped = CompactTraitDomainEvidenceGroupsForTest(modelPrompt);
        string groupedAgain = CompactTraitDomainEvidenceGroupsForTest(modelPrompt);
        string restored = ExpandTraitDomainEvidenceGroupsForTest(grouped);
        string[] groupedModuleLines = grouped.Replace("\r\n", "\n").Split('\n')
            .Where(value => value.StartsWith("- moduleId=", StringComparison.Ordinal))
            .ToArray();
        string[] expectedGroupedModuleLines = moduleLines.Select((value, index) =>
            value.Substring(0, value.LastIndexOf("; domainEvidenceFactIds=", StringComparison.Ordinal))
            + "; domainEvidenceGroup=" + (index == 0 || index == 2 || index == 4 ? "DG01" : "DG02"))
            .ToArray();
        int Count(string source, string value) =>
            source.Split(new[] { value }, StringSplitOptions.None).Length - 1;
        const string GroupInstruction =
            "domainEvidenceGroup refers to the full fact ID list below. Cite full fact IDs, never DG keys, in evidenceFactIds; also cite the required exact gameplay evidence.";
        Require(string.Equals(modelPrompt, modelPromptSnapshot, StringComparison.Ordinal)
                && string.Equals(grouped, groupedAgain, StringComparison.Ordinal)
                && string.Equals(restored, modelPrompt, StringComparison.Ordinal)
                && groupedModuleLines.SequenceEqual(expectedGroupedModuleLines, StringComparer.Ordinal)
                && Count(grouped, evidenceGroupA) == 1
                && Count(grouped, evidenceGroupB) == 1
                && Count(grouped, "domainEvidenceGroup=DG01") == 3
                && Count(grouped, "domainEvidenceGroup=DG02") == 2
                && grouped.Contains("[[trait-domain-evidence-groups-v1]]", StringComparison.Ordinal)
                && grouped.Contains("[[/trait-domain-evidence-groups-v1]]", StringComparison.Ordinal)
                && Count(grouped, GroupInstruction) == 1
                && grouped.Contains("Return full evidenceFactIds only; DG keys are transport metadata, not response IDs.",
                    StringComparison.Ordinal)
                && grouped.Length * 4 < modelPrompt.Length * 3,
            "Trait domain-evidence grouping was not exact, deterministic, Unicode-safe, shared, or substantially smaller.");

        void RequireRejected(Action action, string message)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                return;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return;
            }
            throw new InvalidOperationException(message);
        }
        RequireRejected(
            () => ExpandTraitDomainEvidenceGroupsForTest(grouped.Replace(
                "domainEvidenceGroup=DG01",
                "domainEvidenceGroup=DG99")),
            "Trait domain-evidence inverse accepted an unknown group key.");
        const string BeginMarker = "[[trait-domain-evidence-groups-v1]]";
        const string EndMarker = "[[/trait-domain-evidence-groups-v1]]";
        int mappingStart = grouped.IndexOf('{',
            grouped.IndexOf(BeginMarker, StringComparison.Ordinal) + BeginMarker.Length);
        int endMarker = grouped.IndexOf(EndMarker, mappingStart, StringComparison.Ordinal);
        Require(mappingStart >= 0 && endMarker > mappingStart,
            "Trait domain-evidence compact fixture omitted its mapping object.");
        string mappingJson = grouped.Substring(mappingStart, endMarker - mappingStart)
            .TrimEnd('\r', '\n');
        JProperty firstMapping = JObject.Parse(mappingJson).Properties().First();
        string duplicateMappingJson = "{" + firstMapping.ToString(Newtonsoft.Json.Formatting.None)
            + "," + mappingJson.Substring(1);
        string duplicateMapping = grouped.Substring(0, mappingStart)
            + duplicateMappingJson
            + grouped.Substring(mappingStart + mappingJson.Length);
        RequireRejected(
            () => ExpandTraitDomainEvidenceGroupsForTest(duplicateMapping),
            "Trait domain-evidence inverse accepted a duplicate JSON key.");
        RequireRejected(
            () => ExpandTraitDomainEvidenceGroupsForTest(grouped.Replace(EndMarker, string.Empty)),
            "Trait domain-evidence inverse accepted a missing end marker.");

        const string ActorId = "character:trait-domain-evidence:검증";
        NarrativePublicContextMaterial material = NarrativePublicContextFactory.Build(
            "Persona",
            ActorId,
            NarrativePublicSubjectKind.Character,
            string.Empty,
            requireCharacterFact: false,
            requireMotif: false,
            new[]
            {
                new NarrativePublicEntityInput(
                    ActorId,
                    NarrativePublicEntityKind.Character,
                    "도메인 근거 압축 검증 인물")
            },
            Enumerable.Range(0, 6).Select(index => new NarrativePublicFactInput(
                "trait-domain-evidence",
                "fact:trait-domain-evidence:" + index.ToString("D2"),
                ActorId,
                "도메인 근거 공개 사실 " + index.ToString("D2") + " 🌟",
                100 - index,
                NarrativePublicFactCategory.Memory,
                ActorId)).ToArray(),
            "qa-trait-domain-evidence-transport-v1");
        string[] evidenceIds = material.PublicFacts.Select(value => value.FactId).ToArray();
        Require(evidenceIds.Length == 6,
            "Trait domain-evidence production fixture lost a projected public fact.");
        string offeredGroupA = string.Join(",", evidenceIds.Take(4));
        string offeredGroupB = string.Join(",", evidenceIds.Skip(2));
        string[] positiveIds =
        {
            "acquired-trait:module:domain-focus",
            "acquired-trait:module:domain-resolve",
            "acquired-trait:module:domain-memory"
        };
        const string DrawbackId = "acquired-trait:drawback:domain-echo";
        const string SelectionId = "selection:trait:domain-evidence";
        string header = "selectionId=" + SelectionId + "\n"
            + "- moduleId=" + positiveIds[0] + "; polarity=Positive; meaning=집중; domainEvidenceFactIds=" + offeredGroupA + "\n"
            + "- moduleId=" + positiveIds[1] + "; polarity=Positive; meaning=결의; domainEvidenceFactIds=" + offeredGroupB + "\n"
            + "- moduleId=" + positiveIds[2] + "; polarity=Positive; meaning=기억; domainEvidenceFactIds=" + offeredGroupA + "\n"
            + "- moduleId=" + DrawbackId + "; polarity=Drawback; meaning=메아리; domainEvidenceFactIds=" + offeredGroupB + "\n"
            + "- moduleId=acquired-trait:module:domain-guard; polarity=Positive; meaning=수호; domainEvidenceFactIds=" + offeredGroupA + "\n"
            + "evidenceFactIds=" + string.Join(",", evidenceIds) + "\n"
            + "Cite full evidence IDs only; domain evidence group keys are transport-only.\n";
        string[] allPositiveIds = positiveIds.Concat(
            new[] { "acquired-trait:module:domain-guard" }).ToArray();
        NarrativePublicPromptEnvelope envelope = NarrativePublicPromptEnvelope.Create(
            header + "Trait 도메인 근거 그룹 전송 검증",
            material);
        string originalPrompt = envelope.Prompt;
        string originalPromptSnapshot = originalPrompt;
        string originalSemanticHash = envelope.PublicContextSemanticHash;
        string baseCompact = ToCompactPublicModelPromptForTest(originalPrompt);
        string expectedCompact = CompactTraitDomainEvidenceGroupsForTest(baseCompact);
        string directCompact = ToCompactTraitModelPromptForTest(originalPrompt);
        LlmStaticSchemaDefinition schema = LlmStaticSchemaCatalog.Require(
            LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id);
        byte[] staticSchemaBytes = schema.Utf8Bytes.ToArray();
        string staticSchemaHash = schema.Hash;
        JObject request = JObject.Parse(Encoding.UTF8.GetString(
            DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection,
                schema,
                originalPrompt)));
        JObject properties = (JObject)request["response_format"]["json_schema"]["schema"]["properties"];
        const string ObservedContent =
            "{\"selectionId\":\"selection:trait:domain-evidence\",\"evidenceFactIds\":[\"raw\"]}";
        bool resolved = DungeonStoryHostStructuredChatBackend.TryResolveModelContent(
            LocalLlmRequestProfiles.AcquiredTraitModuleSelection,
            originalPrompt,
            ObservedContent,
            out string resolvedContent,
            out string resolveError);
        Require(string.Equals(directCompact, expectedCompact, StringComparison.Ordinal)
                && string.Equals(
                    (string)request["messages"][1]["content"],
                    "/no_think\n" + directCompact,
                    StringComparison.Ordinal)
                && string.Equals(
                    ExpandTraitDomainEvidenceGroupsForTest(directCompact),
                    baseCompact,
                    StringComparison.Ordinal)
                && ((JArray)properties["selectionId"]["enum"]).Values<string>()
                    .SequenceEqual(new[] { SelectionId }, StringComparer.Ordinal)
                && ((JArray)properties["positiveModuleIds"]["items"]["enum"]).Values<string>()
                    .SequenceEqual(allPositiveIds, StringComparer.Ordinal)
                && ((JArray)properties["drawbackModuleIds"]["items"]["enum"]).Values<string>()
                    .SequenceEqual(new[] { DrawbackId }, StringComparer.Ordinal)
                && ((JArray)properties["evidenceFactIds"]["items"]["enum"]).Values<string>()
                    .SequenceEqual(evidenceIds, StringComparer.Ordinal)
                && staticSchemaBytes.SequenceEqual(schema.Utf8Bytes)
                && string.Equals(schema.Hash, staticSchemaHash, StringComparison.Ordinal)
                && string.Equals(envelope.Prompt, originalPromptSnapshot, StringComparison.Ordinal)
                && string.Equals(envelope.PublicContextSemanticHash, originalSemanticHash,
                    StringComparison.Ordinal)
                && string.Equals(envelope.ModelInput.SemanticHash, originalSemanticHash,
                    StringComparison.Ordinal)
                && resolved
                && string.IsNullOrEmpty(resolveError)
                && string.Equals(resolvedContent, ObservedContent, StringComparison.Ordinal),
            "Trait production wire changed its prompt/schema/IDs or bypassed reversible domain-evidence grouping.");
    }

    private static void VerifyTraitGameplayEvidenceGrammar()
    {
        const string ActorId = "character:trait-gameplay-grammar:검증";
        const string SelectionId = "selection:trait:gameplay-grammar";
        const string PositiveA = "acquired-trait:module:gameplay-focus";
        const string PositiveB = "acquired-trait:module:gameplay-memory";
        const string Drawback = "acquired-trait:drawback:gameplay-echo";
        NarrativePublicContextMaterial material = NarrativePublicContextFactory.Build(
            "Persona",
            ActorId,
            NarrativePublicSubjectKind.Character,
            string.Empty,
            requireCharacterFact: false,
            requireMotif: false,
            new[]
            {
                new NarrativePublicEntityInput(
                    ActorId,
                    NarrativePublicEntityKind.Character,
                    "플레이 근거 문법 검증 인물")
            },
            new[]
            {
                new NarrativePublicFactInput(
                    "trait-gameplay-grammar", "fact:offered:a", ActorId,
                    "첫 번째 제공 근거", 100,
                    NarrativePublicFactCategory.Memory, ActorId),
                new NarrativePublicFactInput(
                    "trait-gameplay-grammar", "fact:offered:b", ActorId,
                    "두 번째 제공 근거", 90,
                    NarrativePublicFactCategory.Memory, ActorId),
                new NarrativePublicFactInput(
                    "trait-gameplay-grammar", "fact:offered:c", ActorId,
                    "세 번째 제공 근거", 80,
                    NarrativePublicFactCategory.Memory, ActorId),
                new NarrativePublicFactInput(
                    "trait-gameplay-grammar", "fact:background-only", ActorId,
                    "스키마에 제공되지 않은 배경 근거", 70,
                    NarrativePublicFactCategory.Background, ActorId)
            },
            "qa-trait-gameplay-evidence-grammar-v1");
        bool foundEvidenceA = material.TryGetProjectedFactId(
            "trait-gameplay-grammar", "fact:offered:a", ActorId, out string evidenceA);
        bool foundEvidenceB = material.TryGetProjectedFactId(
            "trait-gameplay-grammar", "fact:offered:b", ActorId, out string evidenceB);
        bool foundEvidenceC = material.TryGetProjectedFactId(
            "trait-gameplay-grammar", "fact:offered:c", ActorId, out string evidenceC);
        bool foundBackgroundOnly = material.TryGetProjectedFactId(
            "trait-gameplay-grammar", "fact:background-only", ActorId,
            out string backgroundOnly);
        Require(foundEvidenceA && foundEvidenceB && foundEvidenceC && foundBackgroundOnly,
            "Trait gameplay grammar fixture lost a projected public fact ID.");
        string[] evidence = { evidenceA, evidenceB, evidenceC };
        string[] evidenceReferences = evidence.Select((_, index) =>
            "[[trait-e:" + index + "]]").ToArray();
        string unknownEvidence = "public-fact:sha256:" + new string('0', 64);
        const string UnknownReference = "[[trait-e:3]]";
        Require(!evidence.Contains(backgroundOnly, StringComparer.Ordinal)
                && !evidence.Contains(unknownEvidence, StringComparer.Ordinal),
            "Trait gameplay grammar rejection fixtures overlap offered evidence.");
        string baseHeader = "selectionId=" + SelectionId + "\n"
            + "- moduleId=" + PositiveA + "; polarity=Positive; meaning=집중\n"
            + "- moduleId=" + PositiveB + "; polarity=Positive; meaning=기억\n"
            + "- moduleId=" + Drawback + "; polarity=Drawback; meaning=메아리\n"
            + "evidenceFactIds=" + string.Join(",", evidence) + "\n";
        string BuildPrompt(string gameplayHeader) => NarrativePublicPromptEnvelope.Create(
            baseHeader + gameplayHeader + "플레이 근거 요구 문법 검증",
            material).Prompt;
        var profile = LocalLlmRequestProfiles.AcquiredTraitModuleSelection;
        LlmStaticSchemaDefinition staticSchema = LlmStaticSchemaCatalog.Require(profile.Id);
        byte[] staticSchemaBytes = staticSchema.Utf8Bytes.ToArray();
        string staticSchemaHash = staticSchema.Hash;
        string originalSemanticHash = material.SemanticHash;

        JObject Request(byte[] payload) => JObject.Parse(Encoding.UTF8.GetString(payload));
        JObject ResponseSchema(JObject request) =>
            (JObject)request["response_format"]["json_schema"]["schema"];
        bool MatchesEvidenceSubset(JObject responseSchema, string[] values)
        {
            if (responseSchema["properties"]?["evidenceFactIds"]?["anyOf"] is not JArray branches
                || responseSchema["$defs"] is not JObject definitions)
            {
                return false;
            }
            foreach (JObject branch in branches.OfType<JObject>())
            {
                if ((string)branch["type"] != "array"
                    || (int?)branch["minItems"] != values.Length
                    || (int?)branch["maxItems"] != values.Length
                    || (bool?)branch["additionalItems"] != false
                    || branch["items"] is not JArray items
                    || items.Count != values.Length)
                {
                    continue;
                }
                bool match = true;
                for (int index = 0; index < items.Count && match; index++)
                {
                    if (items[index] is not JObject item
                        || item.Properties().Count() != 1
                        || item["$ref"]?.Type != JTokenType.String)
                    {
                        match = false;
                        continue;
                    }
                    const string ReferencePrefix = "#/$defs/";
                    string reference = (string)item["$ref"];
                    if (!reference.StartsWith(ReferencePrefix, StringComparison.Ordinal)
                        || definitions[reference.Substring(ReferencePrefix.Length)]?["enum"]
                            is not JArray allowed
                        || !allowed.Values<string>().Contains(values[index], StringComparer.Ordinal))
                    {
                        match = false;
                    }
                }
                if (match) return true;
            }
            return false;
        }
        string BranchSignature(JObject branch)
        {
            if ((string)branch["type"] != "array"
                || branch["items"] is not JArray items
                || (int?)branch["minItems"] != items.Count
                || (int?)branch["maxItems"] != items.Count
                || (bool?)branch["additionalItems"] != false)
            {
                return string.Empty;
            }
            const string ReferencePrefix = "#/$defs/trait-evidence-";
            string[] references = items.OfType<JObject>()
                .Select(value => (string)value["$ref"])
                .ToArray();
            if (references.Length != items.Count
                || references.Any(value => value == null
                    || !value.StartsWith(ReferencePrefix, StringComparison.Ordinal)))
            {
                return string.Empty;
            }
            return items.Count + ":" + string.Join(",", references.Select(value =>
                value.Substring(ReferencePrefix.Length)));
        }
        string[] expectedBranchSignatures =
        {
            "1:gameplay",
            "2:gameplay,all",
            "2:nongameplay,gameplay",
            "3:gameplay,all,all",
            "3:nongameplay,gameplay,all",
            "3:nongameplay,nongameplay,gameplay"
        };
        var candidateArrays = new List<string[]>();
        for (int first = 0; first < evidenceReferences.Length; first++)
        {
            candidateArrays.Add(new[] { evidenceReferences[first] });
            for (int second = 0; second < evidenceReferences.Length; second++)
            {
                if (second == first) continue;
                candidateArrays.Add(new[] { evidenceReferences[first], evidenceReferences[second] });
                for (int third = 0; third < evidenceReferences.Length; third++)
                {
                    if (third == first || third == second) continue;
                    candidateArrays.Add(new[]
                    {
                        evidenceReferences[first], evidenceReferences[second], evidenceReferences[third]
                    });
                }
            }
        }
        Require(candidateArrays.Count == 15,
            "Trait gameplay grammar did not enumerate every distinct length 1..3 evidence array.");

        foreach (string[] requiredGameplay in new[]
                 {
                     new[] { evidenceB },
                     new[] { evidenceA, evidenceC }
                 })
        {
            string prompt = BuildPrompt(
                "gameplayEvidenceFactIds=" + string.Join(",", requiredGameplay) + "\n");
            string promptSnapshot = prompt;
            byte[] firstPayload = DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test", profile, staticSchema, prompt);
            byte[] secondPayload = DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test", profile, staticSchema, prompt);
            JObject request = Request(firstPayload);
            JObject responseSchema = ResponseSchema(request);
            JObject properties = (JObject)responseSchema["properties"];
            JObject evidenceProperty = (JObject)properties["evidenceFactIds"];
            JObject references = TraitEvidenceReferencesForTest(prompt);
            string[] requiredReferences = evidence
                .Select((value, index) => new { value, reference = evidenceReferences[index] })
                .Where(value => requiredGameplay.Contains(value.value, StringComparer.Ordinal))
                .Select(value => value.reference)
                .ToArray();
            string[] nonGameplayReferences = evidenceReferences
                .Except(requiredReferences, StringComparer.Ordinal)
                .ToArray();
            JObject expectedReferences = new();
            for (int index = 0; index < evidence.Length; index++)
                expectedReferences[evidenceReferences[index]] = evidence[index];
            JObject expectedDefinitions = new()
            {
                ["trait-evidence-all"] = new JObject
                {
                    ["type"] = "string",
                    ["minLength"] = 1,
                    ["enum"] = new JArray(evidenceReferences)
                },
                ["trait-evidence-gameplay"] = new JObject
                {
                    ["type"] = "string",
                    ["minLength"] = 1,
                    ["enum"] = new JArray(requiredReferences)
                },
                ["trait-evidence-nongameplay"] = new JObject
                {
                    ["type"] = "string",
                    ["minLength"] = 1,
                    ["enum"] = new JArray(nonGameplayReferences)
                }
            };
            string[] branchSignatures = ((JArray)properties["evidenceFactIds"]["anyOf"])
                .OfType<JObject>()
                .Select(BranchSignature)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            int acceptedCount = 0;
            foreach (string[] candidate in candidateArrays)
            {
                bool expected = candidate.Any(value =>
                    requiredReferences.Contains(value, StringComparer.Ordinal));
                bool actual = MatchesEvidenceSubset(responseSchema, candidate);
                Require(actual == expected,
                    "Trait gameplay evidence tuple grammar changed a legal combination or admitted a non-gameplay-only array.");
                if (actual) acceptedCount++;
            }
            int expectedAccepted = candidateArrays.Count(candidate => candidate.Any(value =>
                requiredReferences.Contains(value, StringComparer.Ordinal)));
            string requestRef = CharacterSkillRequestRef(prompt);
            JObject observed = new()
            {
                ["selectionId"] = SelectionId,
                ["positiveModuleIds"] = new JArray(PositiveA),
                ["drawbackModuleIds"] = new JArray(),
                ["evidenceFactIds"] = new JArray(requiredReferences[0]),
                ["displayName"] = "플레이 근거 특성",
                ["narrativeFlavor"] = "정확한 플레이 근거에서 이어진 특성이다.",
                ["requestRef"] = requestRef
            };
            string observedContent = observed.ToString(Newtonsoft.Json.Formatting.None);
            string observedSnapshot = observedContent;
            bool resolved = DungeonStoryHostStructuredChatBackend.TryResolveModelContent(
                profile,
                prompt,
                observedContent,
                out string resolvedContent,
                out string resolveError);
            JObject expectedResolved = (JObject)observed.DeepClone();
            expectedResolved.Remove("requestRef");
            expectedResolved["evidenceFactIds"] = new JArray(
                requiredGameplay[0]);
            Require(firstPayload.SequenceEqual(secondPayload)
                    && references != null
                    && JToken.DeepEquals(references, expectedReferences)
                    && JToken.DeepEquals(responseSchema["$defs"], expectedDefinitions)
                    && branchSignatures.SequenceEqual(
                        expectedBranchSignatures.OrderBy(value => value, StringComparer.Ordinal),
                        StringComparer.Ordinal)
                    && acceptedCount == expectedAccepted
                    && !MatchesEvidenceSubset(
                        responseSchema,
                        new[] { requiredReferences[0], backgroundOnly })
                    && !MatchesEvidenceSubset(
                        responseSchema,
                        new[] { requiredReferences[0], UnknownReference })
                    && (string)evidenceProperty["type"] == "array"
                    && (int)evidenceProperty["minItems"] == 1
                    && (int)evidenceProperty["maxItems"] == evidence.Length
                    && ((JArray)evidenceProperty["items"]["enum"]).Values<string>()
                        .SequenceEqual(evidenceReferences, StringComparer.Ordinal)
                    && ((JArray)responseSchema["required"]).Values<string>()
                        .Contains("requestRef", StringComparer.Ordinal)
                    && ((JArray)properties["requestRef"]["enum"]).Values<string>()
                        .SequenceEqual(new[] { requestRef }, StringComparer.Ordinal)
                    && ((JArray)properties["selectionId"]["enum"]).Values<string>()
                        .SequenceEqual(new[] { SelectionId }, StringComparer.Ordinal)
                    && ((JArray)properties["positiveModuleIds"]["items"]["enum"]).Values<string>()
                        .SequenceEqual(new[] { PositiveA, PositiveB }, StringComparer.Ordinal)
                    && ((JArray)properties["drawbackModuleIds"]["items"]["enum"]).Values<string>()
                        .SequenceEqual(new[] { Drawback }, StringComparer.Ordinal)
                    && string.Equals(
                        (string)request["messages"][1]["content"],
                        "/no_think\n" + ToCompactTraitModelPromptForTest(prompt),
                        StringComparison.Ordinal)
                    && string.Equals(prompt, promptSnapshot, StringComparison.Ordinal)
                    && string.Equals(material.SemanticHash, originalSemanticHash,
                        StringComparison.Ordinal)
                    && staticSchemaBytes.SequenceEqual(staticSchema.Utf8Bytes)
                    && string.Equals(staticSchema.Hash, staticSchemaHash,
                        StringComparison.Ordinal)
                    && resolved
                    && string.IsNullOrEmpty(resolveError)
                    && string.Equals(observedContent, observedSnapshot, StringComparison.Ordinal)
                    && string.Equals(
                        resolvedContent,
                        expectedResolved.ToString(Newtonsoft.Json.Formatting.None),
                        StringComparison.Ordinal),
                "Trait gameplay evidence grammar is nondeterministic, mutated transport identity, or rewrote raw content.");
        }

        string absentPrompt = BuildPrompt(string.Empty);
        JObject absentRequest = Request(
            DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test", profile, staticSchema, absentPrompt));
        JObject absentActual = ResponseSchema(absentRequest);
        JObject absentExpected = JObject.Parse(staticSchema.Json);
        JObject absentProperties = (JObject)absentExpected["properties"];
        absentProperties["displayName"]["pattern"] =
            absentActual["properties"]["displayName"]["pattern"].DeepClone();
        absentProperties["narrativeFlavor"]["pattern"] =
            absentActual["properties"]["narrativeFlavor"]["pattern"].DeepClone();
        absentProperties["selectionId"]["enum"] = new JArray(SelectionId);
        absentProperties["positiveModuleIds"]["minItems"] = 1;
        absentProperties["positiveModuleIds"]["maxItems"] = 1;
        absentProperties["positiveModuleIds"]["items"]["enum"] =
            new JArray(PositiveA, PositiveB);
        absentProperties["drawbackModuleIds"]["maxItems"] = 1;
        absentProperties["drawbackModuleIds"]["items"]["enum"] = new JArray(Drawback);
        absentProperties["evidenceFactIds"]["maxItems"] = evidence.Length;
        absentProperties["evidenceFactIds"]["items"]["enum"] = new JArray(evidence);
        const string AbsentObservedContent = " { \"trait\" : \"raw-without-gameplay-header\" } ";
        bool absentResolved = DungeonStoryHostStructuredChatBackend.TryResolveModelContent(
            profile,
            absentPrompt,
            AbsentObservedContent,
            out string absentResolvedContent,
            out string absentResolveError);
        Require(JToken.DeepEquals(absentActual, absentExpected)
                && TraitEvidenceReferencesForTest(absentPrompt) == null
                && string.Equals(
                    (string)absentRequest["messages"][1]["content"],
                    "/no_think\n" + ToCompactTraitModelPromptForTest(absentPrompt),
                    StringComparison.Ordinal)
                && absentResolved
                && string.IsNullOrEmpty(absentResolveError)
                && string.Equals(absentResolvedContent, AbsentObservedContent,
                    StringComparison.Ordinal),
            "Trait request without gameplayEvidenceFactIds changed its existing schema or prompt format.");

        void RequireInvalidGameplayHeader(string gameplayHeader, string message) =>
            RequireInvalidOperation(
                () => DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                    "test", profile, staticSchema, BuildPrompt(gameplayHeader)),
                message);
        RequireInvalidGameplayHeader(
            "gameplayEvidenceFactIds=\n",
            "Trait gameplay evidence grammar accepted an empty required-evidence header.");
        RequireInvalidGameplayHeader(
            "gameplayEvidenceFactIds=" + evidenceA + "," + evidenceA + "\n",
            "Trait gameplay evidence grammar accepted duplicate required evidence IDs.");
        RequireInvalidGameplayHeader(
            "gameplayEvidenceFactIds=" + backgroundOnly + "\n",
            "Trait gameplay evidence grammar accepted a background-only required evidence ID.");
        RequireInvalidGameplayHeader(
            "gameplayEvidenceFactIds=" + unknownEvidence + "\n",
            "Trait gameplay evidence grammar accepted an unknown required evidence ID.");
        RequireInvalidGameplayHeader(
            "gameplayEvidenceFactIds=" + evidenceA + "\n"
            + "gameplayEvidenceFactIds=" + evidenceB + "\n",
            "Trait gameplay evidence grammar accepted duplicate required-evidence headers.");
        Require(staticSchemaBytes.SequenceEqual(staticSchema.Utf8Bytes)
                && string.Equals(staticSchema.Hash, staticSchemaHash, StringComparison.Ordinal)
                && string.Equals(material.SemanticHash, originalSemanticHash,
                    StringComparison.Ordinal),
            "Trait gameplay evidence rejection paths mutated static schema or public identity.");
    }

    private static void VerifyTraitEvidenceReferenceTransport()
    {
        const string ActorId = "character:trait-reference:검증";
        const string SelectionId = "selection:trait:evidence-reference";
        const string PositiveA = "acquired-trait:module:reference-focus";
        const string PositiveB = "acquired-trait:module:reference-memory";
        const string Drawback = "acquired-trait:drawback:reference-echo";
        NarrativePublicContextMaterial material = NarrativePublicContextFactory.Build(
            "Persona",
            ActorId,
            NarrativePublicSubjectKind.Character,
            string.Empty,
            requireCharacterFact: false,
            requireMotif: false,
            new[]
            {
                new NarrativePublicEntityInput(
                    ActorId,
                    NarrativePublicEntityKind.Character,
                    "근거 참조 전송 검증 인물 🌙")
            },
            new[]
            {
                new NarrativePublicFactInput(
                    "trait-reference", "fact:reference:a", ActorId,
                    "불빛을 지킨 첫 기억", 100,
                    NarrativePublicFactCategory.Memory, ActorId),
                new NarrativePublicFactInput(
                    "trait-reference", "fact:reference:b", ActorId,
                    "밤 경계를 완수한 정확한 행동", 90,
                    NarrativePublicFactCategory.Memory, ActorId),
                new NarrativePublicFactInput(
                    "trait-reference", "fact:reference:c", ActorId,
                    "동료를 깨운 세 번째 기억", 80,
                    NarrativePublicFactCategory.Memory, ActorId)
            },
            "qa-trait-evidence-reference-transport-v1");
        bool foundA = material.TryGetProjectedFactId(
            "trait-reference", "fact:reference:a", ActorId, out string evidenceA);
        bool foundB = material.TryGetProjectedFactId(
            "trait-reference", "fact:reference:b", ActorId, out string evidenceB);
        bool foundC = material.TryGetProjectedFactId(
            "trait-reference", "fact:reference:c", ActorId, out string evidenceC);
        Require(foundA && foundB && foundC,
            "Trait evidence-reference fixture lost a projected public fact ID.");
        string[] evidence = { evidenceA, evidenceB, evidenceC };
        string[] referenceIds = evidence.Select((_, index) =>
            "[[trait-e:" + index + "]]").ToArray();
        string header = "selectionId=" + SelectionId + "\n"
            + "- moduleId=" + PositiveA + "; polarity=Positive; meaning=불빛의 집중; domainEvidenceFactIds="
            + evidenceA + "," + evidenceB + "\n"
            + "- moduleId=" + PositiveB + "; polarity=Positive; meaning=밤 경계의 기억; domainEvidenceFactIds="
            + evidenceB + "," + evidenceC + "\n"
            + "- moduleId=" + Drawback + "; polarity=Drawback; meaning=메아리 부담; domainEvidenceFactIds="
            + evidenceA + "," + evidenceB + "\n"
            + "evidenceFactIds=" + string.Join(",", evidence) + "\n"
            + "gameplayEvidenceFactIds=" + evidenceB + "\n"
            + "Cite full evidence IDs only; transport references are never response prose.\n";
        NarrativePublicPromptEnvelope envelope = NarrativePublicPromptEnvelope.Create(
            header + "근거 참조 전송 검증 한국어 문장 🌟",
            material);
        string originalPrompt = envelope.Prompt;
        string originalPromptSnapshot = originalPrompt;
        string originalSemanticHash = envelope.PublicContextSemanticHash;
        string preReferenceCompact = CompactTraitDomainEvidenceGroupsForTest(
            ToCompactPublicModelPromptForTest(originalPrompt));
        JObject references = TraitEvidenceReferencesForTest(originalPrompt);
        JObject expectedReferences = new();
        for (int index = 0; index < evidence.Length; index++)
            expectedReferences[referenceIds[index]] = evidence[index];
        string requestRef = CharacterSkillRequestRef(originalPrompt);
        string compact = ToCompactTraitModelPromptForTest(originalPrompt);
        string compactAgain = ToCompactTraitModelPromptForTest(originalPrompt);
        string expanded = ExpandTraitEvidenceReferencesForTest(compact);
        const string BeginMarker = "[[trait-evidence-refs-v1]]";
        const string EndMarker = "[[/trait-evidence-refs-v1]]";
        int begin = compact.LastIndexOf(BeginMarker, StringComparison.Ordinal);
        int jsonStart = begin < 0 ? -1 : compact.IndexOf('{', begin + BeginMarker.Length);
        int end = jsonStart < 0 ? -1 : compact.IndexOf(EndMarker, jsonStart, StringComparison.Ordinal);
        Require(begin >= 0 && jsonStart > begin && end > jsonStart,
            "Trait evidence-reference compact prompt omitted its metadata envelope.");
        string metadataJson = compact.Substring(jsonStart, end - jsonStart).TrimEnd('\r', '\n');
        JObject metadata = JObject.Parse(metadataJson);
        JObject expectedMetadata = new()
        {
            ["requestRef"] = requestRef,
            ["evidence"] = expectedReferences.DeepClone()
        };
        string compactBody = compact.Substring(0, begin);
        int Count(string source, string value) =>
            source.Split(new[] { value }, StringSplitOptions.None).Length - 1;
        bool everyOccurrenceReferenced = true;
        for (int index = 0; index < evidence.Length; index++)
        {
            int originalOccurrences = Count(preReferenceCompact, evidence[index]);
            everyOccurrenceReferenced &= originalOccurrences > 0
                && Count(compactBody, evidence[index]) == 0
                && Count(compactBody, referenceIds[index]) == originalOccurrences
                && Count(compact, evidence[index]) == 1
                && Count(compact, referenceIds[index]) == originalOccurrences + 1;
        }
        Require(JToken.DeepEquals(references, expectedReferences)
                && JToken.DeepEquals(metadata, expectedMetadata)
                && string.Equals(compact, compactAgain, StringComparison.Ordinal)
                && string.Equals(expanded, preReferenceCompact, StringComparison.Ordinal)
                && everyOccurrenceReferenced
                && compact.Length < preReferenceCompact.Length
                && compact.Contains("근거 참조 전송 검증 한국어 문장 🌟",
                    StringComparison.Ordinal)
                && string.Equals(originalPrompt, originalPromptSnapshot, StringComparison.Ordinal)
                && string.Equals(envelope.PublicContextSemanticHash, originalSemanticHash,
                    StringComparison.Ordinal)
                && string.Equals(envelope.ModelInput.SemanticHash, originalSemanticHash,
                    StringComparison.Ordinal),
            "Trait evidence-reference compact transport is not exact, deterministic, complete, or Unicode-safe.");

        RequireInvalidOperation(
            () => ExpandTraitEvidenceReferencesForTest(compact.Replace(EndMarker, string.Empty)),
            "Trait evidence-reference inverse accepted a missing mandatory end marker.");
        RequireInvalidOperation(
            () => ToCompactTraitModelPromptForTest(originalPrompt.Replace(
                "Cite full evidence IDs only;",
                "reserved [[trait-e:collision]]; Cite full evidence IDs only;")),
            "Trait evidence-reference encoder accepted a reserved reference token in the original prompt.");

        var profile = LocalLlmRequestProfiles.AcquiredTraitModuleSelection;
        LlmStaticSchemaDefinition schema = LlmStaticSchemaCatalog.Require(profile.Id);
        byte[] staticSchemaBytes = schema.Utf8Bytes.ToArray();
        string staticSchemaHash = schema.Hash;
        JObject request = JObject.Parse(Encoding.UTF8.GetString(
            DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test", profile, schema, originalPrompt)));
        JObject responseSchema = (JObject)request["response_format"]["json_schema"]["schema"];
        JObject responseProperties = (JObject)responseSchema["properties"];
        JObject staticProperties = (JObject)JObject.Parse(schema.Json)["properties"];
        JObject expectedSelectionProperty = (JObject)staticProperties["selectionId"].DeepClone();
        expectedSelectionProperty["enum"] = new JArray(SelectionId);
        JObject expectedPositiveProperty = (JObject)staticProperties["positiveModuleIds"].DeepClone();
        expectedPositiveProperty["minItems"] = 1;
        expectedPositiveProperty["maxItems"] = 1;
        expectedPositiveProperty["items"]["enum"] = new JArray(PositiveA, PositiveB);
        JObject expectedDrawbackProperty = (JObject)staticProperties["drawbackModuleIds"].DeepClone();
        expectedDrawbackProperty["maxItems"] = 1;
        expectedDrawbackProperty["items"]["enum"] = new JArray(Drawback);
        JObject expectedDisplayNameProperty = (JObject)staticProperties["displayName"].DeepClone();
        JObject expectedNarrativeFlavorProperty =
            (JObject)staticProperties["narrativeFlavor"].DeepClone();
        expectedDisplayNameProperty["pattern"] =
            responseProperties["displayName"]["pattern"].DeepClone();
        expectedNarrativeFlavorProperty["pattern"] =
            responseProperties["narrativeFlavor"]["pattern"].DeepClone();
        Require(((JArray)responseSchema["required"]).Values<string>()
                    .Contains("requestRef", StringComparer.Ordinal)
                && ((JArray)responseProperties["requestRef"]["enum"]).Values<string>()
                    .SequenceEqual(new[] { requestRef }, StringComparer.Ordinal)
                && ((JArray)responseProperties["evidenceFactIds"]["items"]["enum"])
                    .Values<string>().SequenceEqual(referenceIds, StringComparer.Ordinal)
                && JToken.DeepEquals(responseProperties["selectionId"],
                    expectedSelectionProperty)
                && JToken.DeepEquals(responseProperties["positiveModuleIds"],
                    expectedPositiveProperty)
                && JToken.DeepEquals(responseProperties["drawbackModuleIds"],
                    expectedDrawbackProperty)
                && JToken.DeepEquals(responseProperties["displayName"],
                    expectedDisplayNameProperty)
                && JToken.DeepEquals(responseProperties["narrativeFlavor"],
                    expectedNarrativeFlavorProperty)
                && string.Equals(
                    (string)request["messages"][1]["content"],
                    "/no_think\n" + compact,
                    StringComparison.Ordinal)
                && staticSchemaBytes.SequenceEqual(schema.Utf8Bytes)
                && string.Equals(schema.Hash, staticSchemaHash, StringComparison.Ordinal)
                && string.Equals(originalPrompt, originalPromptSnapshot, StringComparison.Ordinal),
            "Trait request schema or wire did not bind the exact request/evidence references.");

        JObject observed = new()
        {
            ["selectionId"] = SelectionId,
            ["positiveModuleIds"] = new JArray(PositiveB),
            ["drawbackModuleIds"] = new JArray(Drawback),
            ["evidenceFactIds"] = new JArray(referenceIds[1], referenceIds[0]),
            ["displayName"] = "밤의 집중",
            ["narrativeFlavor"] = "밤 경계와 불빛을 지킨 기억이 특성으로 이어졌다. 🌙",
            ["requestRef"] = requestRef
        };
        string observedContent = observed.ToString(Newtonsoft.Json.Formatting.None);
        string observedSnapshot = observedContent;
        JObject expectedResolved = (JObject)observed.DeepClone();
        expectedResolved.Remove("requestRef");
        expectedResolved["evidenceFactIds"] = new JArray(evidenceB, evidenceA);
        string expectedResolvedContent = expectedResolved.ToString(Newtonsoft.Json.Formatting.None);
        string directResolved = ResolveTraitEvidenceReferencesForTest(
            originalPrompt,
            observedContent);
        bool backendResolved = DungeonStoryHostStructuredChatBackend.TryResolveModelContent(
            profile,
            originalPrompt,
            observedContent,
            out string backendResolvedContent,
            out string backendResolveError);
        var callerResult = new LocalLlmResult(
            LocalLlmRequestStatus.Succeeded,
            backendResolvedContent,
            string.Empty,
            "trait-reference-fixture",
            rawModelContent: observedContent);
        Require(backendResolved
                && string.IsNullOrEmpty(backendResolveError)
                && string.Equals(directResolved, expectedResolvedContent,
                    StringComparison.Ordinal)
                && string.Equals(backendResolvedContent, expectedResolvedContent,
                    StringComparison.Ordinal)
                && string.Equals(observedContent, observedSnapshot, StringComparison.Ordinal)
                && string.Equals(callerResult.Content, expectedResolvedContent,
                    StringComparison.Ordinal)
                && string.Equals(callerResult.RawModelContent, observedContent,
                    StringComparison.Ordinal),
            "Trait evidence-reference resolve changed prose, module/selection/evidence order, or raw content.");

        void RequireReferenceReject(string rejectionPrompt, string rejectedContent, string message)
        {
            string rejectedSnapshot = rejectedContent;
            bool accepted = DungeonStoryHostStructuredChatBackend.TryResolveModelContent(
                profile,
                rejectionPrompt,
                rejectedContent,
                out string rejectedResolved,
                out string rejectionError);
            Require(!accepted
                    && string.IsNullOrEmpty(rejectedResolved)
                    && !string.IsNullOrWhiteSpace(rejectionError)
                    && string.Equals(rejectedContent, rejectedSnapshot, StringComparison.Ordinal),
                message);
        }
        string Rejected(Action<JObject> mutate)
        {
            JObject value = (JObject)observed.DeepClone();
            mutate(value);
            return value.ToString(Newtonsoft.Json.Formatting.None);
        }
        string wrongRequestRef = (requestRef[0] == '0' ? "1" : "0")
            + requestRef.Substring(1);
        RequireReferenceReject(
            originalPrompt,
            Rejected(value => value["requestRef"] = wrongRequestRef),
            "Trait evidence-reference resolver accepted a modified request hash.");
        string otherPrompt = originalPrompt.Replace(
            "근거 참조 전송 검증 한국어 문장 🌟",
            "근거 참조 전송 검증 다른 요청 🌟");
        Require(!string.Equals(
                CharacterSkillRequestRef(otherPrompt),
                requestRef,
                StringComparison.Ordinal),
            "Trait evidence-reference other-prompt fixture did not change its request hash.");
        RequireReferenceReject(
            otherPrompt,
            observedContent,
            "Trait evidence-reference resolver accepted a response from another prompt.");
        RequireReferenceReject(
            originalPrompt,
            Rejected(value => value["evidenceFactIds"][0] = "[[trait-e:9]]"),
            "Trait evidence-reference resolver accepted an unknown/out-of-range reference.");
        RequireReferenceReject(
            originalPrompt,
            Rejected(value => value["evidenceFactIds"][0] = "[[trait-e:01]]"),
            "Trait evidence-reference resolver accepted a noncanonical leading-zero reference.");
        RequireReferenceReject(
            originalPrompt,
            Rejected(value => value["evidenceFactIds"] = new JArray(referenceIds[1], referenceIds[1])),
            "Trait evidence-reference resolver accepted duplicate evidence references.");
        RequireReferenceReject(
            originalPrompt,
            Rejected(value => value["evidenceFactIds"][0] = evidenceB),
            "Trait evidence-reference resolver accepted a full evidence ID.");
        RequireReferenceReject(
            originalPrompt,
            Rejected(value => value["extra"] = true),
            "Trait evidence-reference resolver accepted an extra response key.");
        RequireReferenceReject(
            originalPrompt,
            Rejected(value => value.Remove("requestRef")),
            "Trait evidence-reference resolver accepted a missing requestRef.");
        RequireReferenceReject(
            originalPrompt,
            observedContent.Insert(1, "\"selectionId\":\"duplicate\","),
            "Trait evidence-reference resolver accepted a duplicate JSON key.");

        string absentPrompt = originalPrompt.Replace(
            "gameplayEvidenceFactIds=" + evidenceB + "\n",
            string.Empty);
        const string AbsentContent = "trait raw content without reference mode";
        bool absentResolved = DungeonStoryHostStructuredChatBackend.TryResolveModelContent(
            profile,
            absentPrompt,
            AbsentContent,
            out string absentResolvedContent,
            out string absentResolveError);
        string absentExpectedCompact = CompactTraitDomainEvidenceGroupsForTest(
            ToCompactPublicModelPromptForTest(absentPrompt));
        Require(TraitEvidenceReferencesForTest(absentPrompt) == null
                && !ToCompactTraitModelPromptForTest(absentPrompt).Contains(
                    BeginMarker, StringComparison.Ordinal)
                && string.Equals(
                    ToCompactTraitModelPromptForTest(absentPrompt),
                    absentExpectedCompact,
                    StringComparison.Ordinal)
                && absentResolved
                && string.IsNullOrEmpty(absentResolveError)
                && string.Equals(absentResolvedContent, AbsentContent,
                    StringComparison.Ordinal),
            "Trait request without gameplay evidence unexpectedly enabled reference mode.");

        LlmStaticSchemaDefinition otherSchema = LlmStaticSchemaCatalog.Require(
            LocalLlmRequestProfiles.CharacterRecord.Id);
        byte[] otherStaticBytes = otherSchema.Utf8Bytes.ToArray();
        JObject otherRequest = JObject.Parse(Encoding.UTF8.GetString(
            DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                LocalLlmRequestProfiles.CharacterRecord,
                otherSchema,
                originalPrompt)));
        bool otherResolved = DungeonStoryHostStructuredChatBackend.TryResolveModelContent(
            LocalLlmRequestProfiles.CharacterRecord,
            originalPrompt,
            observedContent,
            out string otherResolvedContent,
            out string otherResolveError);
        Require(JToken.DeepEquals(
                    otherRequest["response_format"]["json_schema"]["schema"],
                    JObject.Parse(otherSchema.Json))
                && string.Equals(
                    (string)otherRequest["messages"][1]["content"],
                    NarrativeRequestContext.ToModelPrompt(originalPrompt),
                    StringComparison.Ordinal)
                && otherStaticBytes.SequenceEqual(otherSchema.Utf8Bytes)
                && otherResolved
                && string.IsNullOrEmpty(otherResolveError)
                && string.Equals(otherResolvedContent, observedContent,
                    StringComparison.Ordinal)
                && staticSchemaBytes.SequenceEqual(schema.Utf8Bytes)
                && string.Equals(schema.Hash, staticSchemaHash, StringComparison.Ordinal)
                && string.Equals(originalPrompt, originalPromptSnapshot,
                    StringComparison.Ordinal),
            "Trait evidence-reference transport changed another profile or static/original identity.");
    }

    private static void VerifyTraitProseJsonBoundary()
    {
        const string ActorId = "character:trait-prose-boundary:검증";
        const string SelectionId = "selection:trait:prose-boundary";
        const string ModuleId = "acquired-trait:module:moon-guard";
        NarrativePublicContextMaterial material = NarrativePublicContextFactory.Build(
            "Persona", ActorId, NarrativePublicSubjectKind.Character, string.Empty,
            requireCharacterFact: false, requireMotif: false,
            new[]
            {
                new NarrativePublicEntityInput(
                    ActorId, NarrativePublicEntityKind.Character, "서사 경계 검증 인물")
            },
            new[]
            {
                new NarrativePublicFactInput(
                    "trait-prose-boundary", "fact:moon-guard", ActorId,
                    "달빛 아래 경계를 지킨 기억", 100,
                    NarrativePublicFactCategory.Memory, ActorId)
            },
            "qa-trait-prose-json-boundary-v1");
        string evidenceId = material.PublicFacts.Single().FactId;
        NarrativePublicPromptEnvelope envelope = NarrativePublicPromptEnvelope.Create(
            "selectionId=" + SelectionId + "\n"
            + "- moduleId=" + ModuleId + "; polarity=Positive; meaning=달빛 경계\n"
            + "evidenceFactIds=" + evidenceId + "\n"
            + "gameplayEvidenceFactIds=" + evidenceId + "\n"
            + "Trait 서사 JSON 문자열 경계 검증",
            material);
        string prompt = envelope.Prompt;
        string promptSnapshot = prompt;
        string semanticHash = envelope.PublicContextSemanticHash;
        var profile = LocalLlmRequestProfiles.AcquiredTraitModuleSelection;
        LlmStaticSchemaDefinition schema = LlmStaticSchemaCatalog.Require(profile.Id);
        byte[] staticBytes = schema.Utf8Bytes.ToArray();
        string staticHash = schema.Hash;
        byte[] firstPayload = DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
            "test", profile, schema, prompt);
        byte[] secondPayload = DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
            "test", profile, schema, prompt);
        JObject request = JObject.Parse(Encoding.UTF8.GetString(firstPayload));
        JObject requestProperties = (JObject)request["response_format"]["json_schema"]
            ["schema"]["properties"];
        JObject staticProperties = (JObject)JObject.Parse(schema.Json)["properties"];
        JObject expectedDisplayName = (JObject)staticProperties["displayName"].DeepClone();
        JObject expectedNarrativeFlavor =
            (JObject)staticProperties["narrativeFlavor"].DeepClone();
        string displayPattern = (string)requestProperties["displayName"]["pattern"];
        string narrativePattern = (string)requestProperties["narrativeFlavor"]["pattern"];
        expectedDisplayName["pattern"] = displayPattern;
        expectedNarrativeFlavor["pattern"] = narrativePattern;
        Require(firstPayload.SequenceEqual(secondPayload)
                && !string.IsNullOrWhiteSpace(displayPattern)
                && !string.IsNullOrWhiteSpace(narrativePattern)
                && JToken.DeepEquals(requestProperties["displayName"], expectedDisplayName)
                && JToken.DeepEquals(requestProperties["narrativeFlavor"], expectedNarrativeFlavor)
                && staticProperties["displayName"]["pattern"] != null
                && staticProperties["narrativeFlavor"]["pattern"] != null
                && staticBytes.SequenceEqual(schema.Utf8Bytes)
                && string.Equals(schema.Hash, staticHash, StringComparison.Ordinal)
                && string.Equals(prompt, promptSnapshot, StringComparison.Ordinal)
                && string.Equals(envelope.PublicContextSemanticHash, semanticHash,
                    StringComparison.Ordinal)
                && string.Equals(envelope.ModelInput.SemanticHash, semanticHash,
                    StringComparison.Ordinal),
            "Trait request prose schema changed beyond its request-local lexical pattern or mutated identity.");

        const string DisplayName = "달빛 \"수호\" \\ 결";
        const string NarrativeFlavor = "첫 줄의 기억\n둘째 줄에서 이어진다.";
        string Interior(string value)
        {
            string quoted = Newtonsoft.Json.JsonConvert.ToString(value);
            return quoted.Substring(1, quoted.Length - 2);
        }
        bool Matches(Regex regex, string encoded)
        {
            Match match = regex.Match(encoded);
            return match.Success && match.Index == 0 && match.Length == encoded.Length;
        }
        var displayLexical = new Regex(displayPattern, RegexOptions.CultureInvariant);
        var narrativeLexical = new Regex(narrativePattern, RegexOptions.CultureInvariant);
        Require(Matches(displayLexical, Interior(DisplayName))
                && Matches(narrativeLexical, Interior(NarrativeFlavor))
                && Matches(displayLexical, @"\uD55C\uAE00")
                && Matches(narrativeLexical, @"\uD55C\uAE00\n\uC904")
                && !Matches(displayLexical, string.Empty)
                && !Matches(narrativeLexical, string.Empty)
                && !Matches(displayLexical, Interior(new string('가', 33)))
                && !Matches(narrativeLexical, Interior(new string('가', 181)))
                && !Matches(displayLexical, "\"")
                && !Matches(displayLexical, "\\")
                && !Matches(narrativeLexical, Interior(NarrativeFlavor) + "\n"),
            "Trait JSON-interior lexical pattern rejected safe escapes/Unicode or lost bounds.");
        Require(Matches(displayLexical, "١")
                && Matches(narrativeLexical, @"\u0661"),
            "Trait lexical request no longer reflects the static ASCII/fullwidth-only digit exclusions.");
        foreach (string forbidden in new[]
                 {
                     "0", "9", "０", "９", "%", "％", @"\u0030", @"\u0039",
                     @"\uFF10", @"\uff10", @"\uFF19", @"\u0025", @"\uFF05"
                 })
        {
            Require(!Matches(displayLexical, forbidden)
                    && !Matches(narrativeLexical, forbidden),
                "Trait lexical pattern admitted a literal or escaped digit/percent.");
        }
        string requestRef = CharacterSkillRequestRef(prompt);
        JObject observed = new()
        {
            ["selectionId"] = SelectionId,
            ["positiveModuleIds"] = new JArray(ModuleId),
            ["drawbackModuleIds"] = new JArray(),
            ["evidenceFactIds"] = new JArray("[[trait-e:0]]"),
            ["displayName"] = DisplayName,
            ["narrativeFlavor"] = NarrativeFlavor,
            ["requestRef"] = requestRef
        };
        string raw = observed.ToString(Newtonsoft.Json.Formatting.None);
        string rawSnapshot = raw;
        JObject expectedResolved = (JObject)observed.DeepClone();
        expectedResolved.Remove("requestRef");
        expectedResolved["evidenceFactIds"] = new JArray(evidenceId);
        bool resolved = DungeonStoryHostStructuredChatBackend.TryResolveModelContent(
            profile, prompt, raw, out string resolvedContent, out string resolveError);
        bool parsed = LlmJsonResponseParser.TryParse(
            profile.Id, resolvedContent,
            out CharacterAcquiredTraitModuleSelectionResponseDto response,
            out string parseError);
        Require(resolved && string.IsNullOrEmpty(resolveError) && parsed
                && response.Validate(out string validationError)
                && string.IsNullOrEmpty(validationError)
                && string.Equals(response.displayName, DisplayName, StringComparison.Ordinal)
                && string.Equals(response.narrativeFlavor, NarrativeFlavor,
                    StringComparison.Ordinal)
                && response.evidenceFactIds.SequenceEqual(
                    new[] { evidenceId }, StringComparer.Ordinal)
                && string.Equals(
                    resolvedContent,
                    expectedResolved.ToString(Newtonsoft.Json.Formatting.None),
                    StringComparison.Ordinal)
                && string.Equals(raw, rawSnapshot, StringComparison.Ordinal),
            "Trait reference resolution or C# validation changed valid escaped prose: "
            + parseError);

        foreach (string forbidden in new[] { "1", "１", "%", "％" })
        {
            var invalid = new CharacterAcquiredTraitModuleSelectionResponseDto
            {
                selectionId = SelectionId,
                positiveModuleIds = new List<string> { ModuleId },
                drawbackModuleIds = new List<string>(),
                evidenceFactIds = new List<string> { evidenceId },
                displayName = DisplayName + forbidden,
                narrativeFlavor = NarrativeFlavor
            };
            Require(!invalid.Validate(out _),
                "Trait C# prose guard accepted a numeric or percent variant despite lexical request guards.");
        }
        var currentTraitArabicDigit = new CharacterAcquiredTraitModuleSelectionResponseDto
        {
            selectionId = SelectionId,
            positiveModuleIds = new List<string> { ModuleId },
            drawbackModuleIds = new List<string>(),
            evidenceFactIds = new List<string> { evidenceId },
            displayName = DisplayName + "١",
            narrativeFlavor = NarrativeFlavor
        };
        Require(currentTraitArabicDigit.Validate(out string traitArabicDigitError),
            "Current Trait Unicode-digit policy unexpectedly rejected Arabic-Indic digit: "
            + traitArabicDigitError);
        Debug.Log("CURRENT_TRAIT_DIGIT_POLICY_DIFFERS");

        foreach (LocalLlmRequestProfile otherProfile in new[]
                 {
                     LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection,
                     LocalLlmRequestProfiles.CharacterSkillModuleSelection,
                     LocalLlmRequestProfiles.FacilityEvolutionModuleSelection
                 })
        {
            LlmStaticSchemaDefinition otherSchema = LlmStaticSchemaCatalog.Require(otherProfile.Id);
            byte[] otherStaticBytes = otherSchema.Utf8Bytes.ToArray();
            JObject otherRequest = JObject.Parse(Encoding.UTF8.GetString(
                DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                    "test", otherProfile, otherSchema, prompt)));
            JObject actual = (JObject)otherRequest
                ["response_format"]["json_schema"]["schema"];
            JObject expected = JObject.Parse(otherSchema.Json);
            bool skill = string.Equals(otherProfile.Id,
                    LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                    StringComparison.Ordinal);
            JObject proseProperties = skill
                ? (JObject)expected["properties"]["candidates"]["items"]["properties"]
                : (JObject)expected["properties"];
            JObject actualProseProperties = skill
                ? (JObject)actual["properties"]["candidates"]["items"]["properties"]
                : (JObject)actual["properties"];
            proseProperties["displayName"]["pattern"] =
                actualProseProperties["displayName"]["pattern"].DeepClone();
            proseProperties["narrativeFlavor"]["pattern"] =
                actualProseProperties["narrativeFlavor"]["pattern"].DeepClone();
            if (string.Equals(otherProfile.Id,
                    LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id,
                    StringComparison.Ordinal))
                expected["properties"]["selectionId"]["enum"] = new JArray(SelectionId);
            Require(JToken.DeepEquals(
                        actual, expected)
                    && otherStaticBytes.SequenceEqual(otherSchema.Utf8Bytes),
                "Trait prose request change leaked into another module-selection payload.");
        }
        Require(staticBytes.SequenceEqual(schema.Utf8Bytes)
                && string.Equals(schema.Hash, staticHash, StringComparison.Ordinal)
                && string.Equals(prompt, promptSnapshot, StringComparison.Ordinal)
                && string.Equals(envelope.PublicContextSemanticHash, semanticHash,
                    StringComparison.Ordinal),
            "Trait prose boundary checks mutated static schema, prompt, or semantic identity.");
    }

    private static void VerifyModuleSelectionProseJsonBoundary()
    {
        const string ActorId = "character:module-prose-boundary:검증";
        const string SelectionId = "selection:module-prose-boundary";
        NarrativePublicContextMaterial material = NarrativePublicContextFactory.Build(
            "Persona", ActorId, NarrativePublicSubjectKind.Character, string.Empty,
            requireCharacterFact: false, requireMotif: false,
            new[]
            {
                new NarrativePublicEntityInput(
                    ActorId, NarrativePublicEntityKind.Character, "모듈 서사 경계 검증 인물")
            },
            new[]
            {
                new NarrativePublicFactInput(
                    "module-prose-boundary", "fact:guard", ActorId,
                    "달빛 아래 경계를 지킨 기억", 100,
                    NarrativePublicFactCategory.Memory, ActorId)
            },
            "qa-module-selection-prose-json-boundary-v1");
        string evidenceId = material.PublicFacts.Single().FactId;
        NarrativePublicPromptEnvelope envelope = NarrativePublicPromptEnvelope.Create(
            "selectionId=" + SelectionId + "\n"
            + "character=세나 १\n"
            + "- moduleId=module:moon-guard; polarity=Positive; meaning=달빛 경계\n"
            + "evidenceFactIds=" + evidenceId + "\n"
            + "모듈 선택 서사 JSON 문자열 경계 검증",
            material);
        string prompt = envelope.Prompt;
        string promptSnapshot = prompt;
        string semanticHash = envelope.PublicContextSemanticHash;
        LocalLlmRequestProfile[] profiles =
        {
            LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection,
            LocalLlmRequestProfiles.FacilityEvolutionModuleSelection,
            LocalLlmRequestProfiles.CharacterSkillModuleSelection
        };
        string equipmentDisplayPattern = null;
        string equipmentNarrativePattern = null;
        string facilityDisplayPattern = null;
        string facilityNarrativePattern = null;
        string skillDisplayPattern = null;
        string skillNarrativePattern = null;
        foreach (LocalLlmRequestProfile profile in profiles)
        {
            LlmStaticSchemaDefinition schema = LlmStaticSchemaCatalog.Require(profile.Id);
            byte[] staticBytes = schema.Utf8Bytes.ToArray();
            string staticHash = schema.Hash;
            int staticVersion = schema.Version;
            byte[] first = DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test", profile, schema, prompt);
            byte[] second = DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test", profile, schema, prompt);
            JObject request = JObject.Parse(Encoding.UTF8.GetString(first));
            JObject actualSchema = (JObject)request["response_format"]["json_schema"]["schema"];
            JObject expectedSchema = JObject.Parse(schema.Json);
            bool isSkill = string.Equals(profile.Id,
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                StringComparison.Ordinal);
            JObject expectedProperties = isSkill
                ? (JObject)expectedSchema["properties"]["candidates"]["items"]["properties"]
                : (JObject)expectedSchema["properties"];
            if (string.Equals(profile.Id,
                    LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id,
                    StringComparison.Ordinal))
                expectedSchema["properties"]["selectionId"]["enum"] = new JArray(SelectionId);
            JObject actualProperties = isSkill
                ? (JObject)actualSchema["properties"]["candidates"]["items"]["properties"]
                : (JObject)actualSchema["properties"];
            string actualDisplayPattern = (string)actualProperties["displayName"]["pattern"];
            string actualNarrativePattern = (string)actualProperties["narrativeFlavor"]["pattern"];
            expectedProperties["displayName"]["pattern"] = actualDisplayPattern;
            expectedProperties["narrativeFlavor"]["pattern"] = actualNarrativePattern;
            if (isSkill)
            {
                skillDisplayPattern = actualDisplayPattern;
                skillNarrativePattern = actualNarrativePattern;
            }
            else if (string.Equals(profile.Id,
                         LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id,
                         StringComparison.Ordinal))
            {
                equipmentDisplayPattern = actualDisplayPattern;
                equipmentNarrativePattern = actualNarrativePattern;
            }
            else
            {
                facilityDisplayPattern = actualDisplayPattern;
                facilityNarrativePattern = actualNarrativePattern;
            }
            bool tablelessSkillShape = !isSkill
                || actualSchema["properties"]["requestRef"] == null
                && !((JArray)actualSchema["required"]).Values<string>()
                    .Contains("requestRef", StringComparer.Ordinal)
                && actualSchema["properties"]["candidates"]["items"] is JObject
                && actualSchema["properties"]["candidates"]["additionalItems"] == null;
            Require(first.SequenceEqual(second)
                    && JToken.DeepEquals(actualSchema, expectedSchema)
                    && !string.IsNullOrWhiteSpace(actualDisplayPattern)
                    && !string.IsNullOrWhiteSpace(actualNarrativePattern)
                    && staticBytes.SequenceEqual(schema.Utf8Bytes)
                    && string.Equals(schema.Hash, staticHash, StringComparison.Ordinal)
                    && schema.Version == staticVersion
                    && (int)request["max_tokens"] == profile.MaxOutputTokens
                    && tablelessSkillShape,
                "Module-selection request changed beyond its request-local prose patterns or static contract.");
        }

        const string DisplayName = "달빛 \"수호\"\\결";
        const string MultiLineFlavor = "첫 줄의 \"기억\"\\결\r\n둘째 줄도 이어진다.";
        const string SkillFlavor = "기억의 \"틈\"을 \\ 이어 지킨다.";
        string Interior(string value)
        {
            string quoted = Newtonsoft.Json.JsonConvert.ToString(value);
            return quoted.Substring(1, quoted.Length - 2);
        }
        bool Matches(Regex regex, string encoded)
        {
            Match match = regex.Match(encoded);
            return match.Success && match.Index == 0 && match.Length == encoded.Length;
        }
        var equipmentDisplayLexical = new Regex(
            equipmentDisplayPattern, RegexOptions.CultureInvariant);
        var equipmentNarrativeLexical = new Regex(
            equipmentNarrativePattern, RegexOptions.CultureInvariant);
        var facilityDisplayLexical = new Regex(
            facilityDisplayPattern, RegexOptions.CultureInvariant);
        var facilityNarrativeLexical = new Regex(
            facilityNarrativePattern, RegexOptions.CultureInvariant);
        var skillDisplayLexical = new Regex(
            skillDisplayPattern, RegexOptions.CultureInvariant);
        var skillNarrativeLexical = new Regex(
            skillNarrativePattern, RegexOptions.CultureInvariant);
        Require(Matches(equipmentDisplayLexical, Interior(DisplayName))
                && Matches(equipmentNarrativeLexical, Interior(MultiLineFlavor))
                && Matches(facilityDisplayLexical, Interior(DisplayName))
                && Matches(facilityNarrativeLexical, Interior(MultiLineFlavor))
                && Matches(skillDisplayLexical, Interior(DisplayName))
                && Matches(skillNarrativeLexical, Interior(SkillFlavor))
                && Matches(equipmentDisplayLexical, @"\uD55C\uAE00")
                && Matches(facilityDisplayLexical, @"\uD55C\uAE00")
                && Matches(skillDisplayLexical, @"\uD55C\uAE00")
                && Matches(equipmentNarrativeLexical, @"\uD55C\uAE00")
                && Matches(facilityNarrativeLexical, @"\uD55C\uAE00")
                && Matches(skillNarrativeLexical, @"\uD55C\uAE00.")
                && Matches(skillNarrativeLexical, @"\uD55C\uAE00\u002e")
                && Matches(skillNarrativeLexical, @"\uD55C\uAE00\u002E")
                && Matches(equipmentNarrativeLexical, @"\uD55C\r\n\uC904")
                && Matches(facilityNarrativeLexical, @"\uD55C\n\uC904")
                && !Matches(skillNarrativeLexical, @"\uD55C\n\uC904.")
                && !Matches(skillNarrativeLexical, @"\uD55C\r\uC904.")
                && !Matches(equipmentDisplayLexical, string.Empty)
                && !Matches(equipmentNarrativeLexical, string.Empty)
                && !Matches(facilityDisplayLexical, string.Empty)
                && !Matches(facilityNarrativeLexical, string.Empty)
                && !Matches(skillDisplayLexical, string.Empty)
                && !Matches(skillNarrativeLexical, string.Empty)
                && !Matches(equipmentDisplayLexical, Interior(new string('가', 33)))
                && !Matches(equipmentNarrativeLexical, Interior(new string('가', 181)))
                && !Matches(facilityDisplayLexical, Interior(new string('가', 33)))
                && !Matches(facilityNarrativeLexical, Interior(new string('가', 181)))
                && !Matches(skillDisplayLexical, Interior(new string('가', 15)))
                && !Matches(skillNarrativeLexical, Interior(new string('가', 48) + "."))
                && Matches(skillNarrativeLexical, Interior(new string('가', 47) + "."))
                && !Matches(skillNarrativeLexical, @"\uD55C\uAE00")
                && !Matches(skillNarrativeLexical, Interior("기억을 지킨다"))
                && !Matches(equipmentNarrativeLexical, Interior(MultiLineFlavor) + "\n")
                && !Matches(skillNarrativeLexical, Interior(SkillFlavor) + "\n"),
            "Module-selection JSON-interior lexical patterns lost safe escapes, line rules, or bounds.");
        foreach (string unicodeDigit in new[]
                 {
                     "١", @"\u0661", "१", "९", @"\u0967", @"\u096F",
                     "१००", "९५.०३", @"\u0967\u0966\u0966",
                     @"\u096F\u096B.\u0966\u0969"
                 })
        {
            Require(!Matches(equipmentDisplayLexical, unicodeDigit)
                    && !Matches(equipmentNarrativeLexical, unicodeDigit)
                    && !Matches(facilityDisplayLexical, unicodeDigit)
                    && !Matches(facilityNarrativeLexical, unicodeDigit)
                    && !Matches(skillDisplayLexical, unicodeDigit)
                    && !Matches(skillNarrativeLexical, unicodeDigit)
                    && !Matches(skillNarrativeLexical, unicodeDigit + "."),
                "A module-selection lexical pattern admitted a general BMP Unicode digit.");
        }
        foreach (string forbidden in new[]
                 {
                     "0", "9", "０", "９", "%", "％", @"\u0030", @"\u0039",
                     @"\uFF10", @"\uff10", @"\uFF19", @"\u0025", @"\uFF05"
                 })
        {
            Require(!Matches(equipmentDisplayLexical, forbidden)
                    && !Matches(equipmentNarrativeLexical, forbidden)
                    && !Matches(facilityDisplayLexical, forbidden)
                    && !Matches(facilityNarrativeLexical, forbidden)
                    && !Matches(skillDisplayLexical, forbidden)
                    && !Matches(skillNarrativeLexical, forbidden)
                    && !Matches(skillNarrativeLexical, forbidden + "."),
                "A module-selection lexical pattern admitted a literal or escaped digit/percent.");
        }
        foreach (string otherNumber in new[]
                 {
                     "〇", "⁶", "Ⅻ", "½", "⑳", "²", @"\u3007", @"\u2076",
                     @"\u216B", @"\u00BD", @"\u2473", @"\u00B2"
                 })
        {
            Require(!Matches(skillDisplayLexical, otherNumber)
                    && !Matches(skillNarrativeLexical, otherNumber + ".")
                    && Matches(equipmentDisplayLexical, otherNumber)
                    && Matches(equipmentNarrativeLexical, otherNumber + ".")
                    && Matches(facilityDisplayLexical, otherNumber)
                    && Matches(facilityNarrativeLexical, otherNumber + "."),
                "Skill Number exclusion failed or changed another profile's existing digit policy.");
        }
        Require(Matches(skillDisplayLexical, Interior("세나 १ 감각"))
                && !Matches(skillDisplayLexical, @"세나 \u0967 감각")
                && Matches(skillDisplayLexical, Interior("세나 १" + new string('가', 13))),
            "CharacterSkill lexical pattern lost or broadened the exact literal Unicode-numeric name alternative.");

        string asciiNamePrompt = prompt.Replace(
            "character=세나 १\n", "character=세나 1\n");
        LlmStaticSchemaDefinition asciiNameSchema = LlmStaticSchemaCatalog.Require(
            LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id);
        byte[] asciiNameStaticBytes = asciiNameSchema.Utf8Bytes.ToArray();
        JObject asciiNameRequest = JObject.Parse(Encoding.UTF8.GetString(
            DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                LocalLlmRequestProfiles.CharacterSkillModuleSelection,
                asciiNameSchema,
                asciiNamePrompt)));
        string asciiNamePattern = (string)asciiNameRequest
            ["response_format"]["json_schema"]["schema"]["properties"]
            ["candidates"]["items"]["properties"]["displayName"]["pattern"];
        var asciiNameLexical = new Regex(
            asciiNamePattern, RegexOptions.CultureInvariant);
        Require(Matches(asciiNameLexical, Interior("세나 1 감각"))
                && !Matches(asciiNameLexical, @"세나 \u0031 감각")
                && asciiNameStaticBytes.SequenceEqual(asciiNameSchema.Utf8Bytes)
                && string.Equals(
                    asciiNamePrompt.Replace("character=세나 1\n", "character=세나 १\n"),
                    prompt,
                    StringComparison.Ordinal),
            "CharacterSkill request lost the exact literal ASCII-numeric name alternative or mutated static identity.");
        string otherNumberNamePrompt = prompt.Replace(
            "character=세나 १\n", "character=세나 ²\n");
        JObject otherNumberNameRequest = JObject.Parse(Encoding.UTF8.GetString(
            DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test", LocalLlmRequestProfiles.CharacterSkillModuleSelection,
                asciiNameSchema, otherNumberNamePrompt)));
        JObject otherNumberNameProperties = (JObject)otherNumberNameRequest
            ["response_format"]["json_schema"]["schema"]["properties"]
            ["candidates"]["items"]["properties"];
        var otherNumberNameLexical = new Regex(
            (string)otherNumberNameProperties["displayName"]["pattern"], RegexOptions.CultureInvariant);
        var otherNumberNameFlavorLexical = new Regex(
            (string)otherNumberNameProperties["narrativeFlavor"]["pattern"], RegexOptions.CultureInvariant);
        Require(Matches(otherNumberNameLexical, Interior("세나 ² 감각"))
                && Matches(otherNumberNameFlavorLexical, Interior("세나 ²가 기억을 지킨다."))
                && !Matches(otherNumberNameLexical, @"세나 \u00B2 감각")
                && !Matches(otherNumberNameLexical, Interior("세나 ² Ⅻ 감각"))
                && !Matches(otherNumberNameFlavorLexical, Interior("세나 ²가 ½ 기억을 지킨다."))
                && asciiNameStaticBytes.SequenceEqual(asciiNameSchema.Utf8Bytes),
            "CharacterSkill lost or generalized the literal Number-bearing authoritative-name exception.");
        JObject Selection(string displayName, string narrativeFlavor) => new()
        {
            ["selectionId"] = SelectionId,
            ["positiveModuleIds"] = new JArray("module:moon-guard"),
            ["drawbackModuleIds"] = new JArray(),
            ["evidenceFactIds"] = new JArray(evidenceId),
            ["displayName"] = displayName,
            ["narrativeFlavor"] = narrativeFlavor
        };
        string equipmentJson = Selection(DisplayName, MultiLineFlavor)
            .ToString(Newtonsoft.Json.Formatting.None);
        string facilityJson = Selection(DisplayName, MultiLineFlavor)
            .ToString(Newtonsoft.Json.Formatting.None);
        string skillJson = new JObject
        {
            ["candidates"] = new JArray(Selection(DisplayName, SkillFlavor))
        }.ToString(Newtonsoft.Json.Formatting.None);
        Require(LlmJsonResponseParser.TryParse(
                    LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id,
                    equipmentJson, out EquipmentEvolutionModuleSelectionResponseDto equipment,
                    out _)
                && LlmJsonResponseParser.TryParse(
                    LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id,
                    facilityJson, out FacilityFormulaEvolutionModuleSelectionDto facility,
                    out _)
                && LlmJsonResponseParser.TryParse(
                    LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                    skillJson, out CharacterSkillModuleSelectionBatchResponseDto parsedSkill,
                    out _)
                && string.Equals(equipment.displayName, DisplayName, StringComparison.Ordinal)
                && string.Equals(equipment.narrativeFlavor, MultiLineFlavor,
                    StringComparison.Ordinal)
                && string.Equals(facility.displayName, DisplayName, StringComparison.Ordinal)
                && string.Equals(facility.narrativeFlavor, MultiLineFlavor,
                    StringComparison.Ordinal)
                && string.Equals(parsedSkill.candidates.Single().displayName,
                    DisplayName, StringComparison.Ordinal)
                && string.Equals(parsedSkill.candidates.Single().narrativeFlavor,
                    SkillFlavor, StringComparison.Ordinal),
            "Module-selection escaped Korean prose failed DTO round-trip.");

        EquipmentEvolutionModuleSelectionResponseDto EquipmentText(string name, string flavor) =>
            new() { selectionId = SelectionId, positiveModuleIds = new() { "module" },
                drawbackModuleIds = new(), evidenceFactIds = new() { evidenceId },
                displayName = name, narrativeFlavor = flavor };
        FacilityFormulaEvolutionModuleSelectionDto FacilityText(string name, string flavor) =>
            new() { selectionId = SelectionId, positiveModuleIds = new() { "module" },
                drawbackModuleIds = new(), evidenceFactIds = new() { evidenceId },
                displayName = name, narrativeFlavor = flavor };
        CharacterSkillModuleSelectionResponseDto SkillText(string name, string flavor) =>
            new() { selectionId = SelectionId, positiveModuleIds = new() { "module" },
                drawbackModuleIds = new(), evidenceFactIds = new() { evidenceId },
                displayName = name, narrativeFlavor = flavor };
        foreach (string forbidden in new[] { "1", "１", "١", "%", "％" })
        {
            Require(!EquipmentText(DisplayName + forbidden, "기억").Validate(out _)
                    && !FacilityText(DisplayName + forbidden, "기억").Validate(out _)
                    && !SkillText(DisplayName + forbidden, "기억").Validate(out _),
                "A module-selection C# guard accepted a numeric or percent prose variant.");
        }
        Require(EquipmentText(DisplayName, MultiLineFlavor).Validate(out _)
                && FacilityText(DisplayName, MultiLineFlavor).Validate(out _)
                && !EquipmentText(new string('가', 33), "기억").Validate(out _)
                && !EquipmentText("이름", new string('가', 181)).Validate(out _)
                && !FacilityText(new string('가', 33), "기억").Validate(out _)
                && !FacilityText("이름", new string('가', 181)).Validate(out _)
                && !SkillText(new string('가', 15), "기억").Validate(out _)
                && !SkillText("이름", new string('가', 49)).Validate(out _)
                && !SkillText("이름", "기억\r경계").Validate(out _)
                && !SkillText("이름", "기억\n경계").Validate(out _),
            "Module-selection prose length or profile-specific newline guards changed.");
        CharacterSkillModuleSelectionResponseDto named = SkillText(
            "세나 १ 감각", "세나 १의 기억에서 익힌 감각이다.");
        var namedBatch = new CharacterSkillModuleSelectionBatchResponseDto
        {
            candidates = new List<CharacterSkillModuleSelectionResponseDto> { named }
        };
        Require(namedBatch.Validate(out _, "세나 १")
                && !namedBatch.Validate(out _)
                && !SkillText("X세나 १ 감각", "기억").Validate(out _, "세나 १")
                && !SkillText("세나 १" + new string('가', 13), "기억")
                    .Validate(out _, "세나 १")
                && !SkillText("세나 १ 감각", "세나 १의 회복 २०을 말한다.")
                    .Validate(out _, "세나 १")
                && string.Equals(prompt, promptSnapshot, StringComparison.Ordinal)
                && string.Equals(envelope.PublicContextSemanticHash, semanticHash,
                    StringComparison.Ordinal),
            "CharacterSkill authoritative-name exception broadened or request identity changed.");
        var asciiNamedBatch = new CharacterSkillModuleSelectionBatchResponseDto
        {
            candidates = new List<CharacterSkillModuleSelectionResponseDto>
            {
                SkillText("세나 1 감각", "세나 1의 기억에서 익힌 감각이다.")
            }
        };
        Require(asciiNamedBatch.Validate(out _, "세나 1")
                && !SkillText("X세나 1 감각", "기억").Validate(out _, "세나 1")
                && !SkillText("세나 12 감각", "기억").Validate(out _, "세나 1"),
            "CharacterSkill ASCII-numeric authoritative-name exception lost exact or boundary behavior.");

        foreach (string number in new[] { "〇.⁶", "¹⁰⁰", "Ⅻ", "½", "𝟘", "⑳", "²" })
        {
            string numericFlavor = "Miru는 " + number + "의 수치를 익혔다.";
            Require(!SkillText("파문 수호", numericFlavor).Validate(out string numberError, "Miru")
                    && numberError.Contains("numbers", StringComparison.Ordinal)
                    && !SkillText("파문 " + number, "Miru는 기억에서 수호를 익혔다.")
                        .Validate(out _, "Miru")
                    && !new CharacterSkillGenerationResponseDto
                    {
                        presentationId = "presentation:skill:" + new string('a', 64),
                        displayName = "파문 수호", narrativeFlavor = numericFlavor
                    }.Validate(out _, "Miru"),
                "CharacterSkill accepted a Unicode mechanical number: " + number);
        }
        Require(SkillText("세나 ² 수호", "세나 ²는 기억에서 수호를 익혔다.")
                    .Validate(out _, "세나 ²")
                && !SkillText("세나 ² 수호", "세나 ²는 기억에서 수호를 익혔다.")
                    .Validate(out _)
                && !SkillText("세나 ²³ 수호", "세나 ²는 기억에서 수호를 익혔다.")
                    .Validate(out _, "세나 ²")
                && SkillText("파문 수호", new string('가', 47) + ".").Validate(out _)
                && !SkillText("파문 수호", new string('가', 48)).Validate(out _)
                && !SkillText("파문 수호", "Miru는 기억을 수호로 엮으며")
                    .Validate(out string sentenceError, "Miru")
                && sentenceError.Contains("complete sentence", StringComparison.Ordinal),
            "CharacterSkill exact numeric-name exception or complete-sentence/48-character boundary changed.");

        string nativePath = Path.Combine(Application.dataPath, "..",
            "Artifacts/QA/AuditRemediation-20260926-r1/u6-original-skill-native-r512-r1",
            "unity-evidence/original-skill-prose-native/resolved-content.json");
        byte[] nativeBytes = File.ReadAllBytes(nativePath);
        using (SHA256 nativeHasher = SHA256.Create())
            Require(BitConverter.ToString(nativeHasher.ComputeHash(nativeBytes))
                    .Replace("-", string.Empty).ToLowerInvariant()
                    == "7ec23d75885a81bac812b3bf370d316bc59b98d375297be3630ac906c73d096d",
                "Preserved r512 native negative fixture changed.");
        string nativeJson = Encoding.UTF8.GetString(nativeBytes);
        var nativeBatch = JsonUtility.FromJson<CharacterSkillModuleSelectionBatchResponseDto>(nativeJson);
        string nativeDtoBefore = JsonUtility.ToJson(nativeBatch);
        Require(nativeBatch?.candidates?.Count == 3, "Preserved native fixture lacks three candidates.");
        for (int index = 0; index < nativeBatch.candidates.Count; index++)
            Require(!nativeBatch.candidates[index].Validate(out string nativeError, "Miru")
                    && nativeError.Contains(index == 1 ? "complete sentence" : "numbers",
                        StringComparison.Ordinal),
                "CharacterSkill accepted preserved r512 native candidate " + index + ".");
        Require(!nativeBatch.Validate(out _, "Miru")
                && !LlmJsonResponseParser.TryParse(
                    LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id, nativeJson,
                    out CharacterSkillModuleSelectionBatchResponseDto _, out _)
                && JsonUtility.ToJson(nativeBatch) == nativeDtoBefore
                && File.ReadAllBytes(nativePath).SequenceEqual(nativeBytes),
            "CharacterSkill native rejection mutated the response/fixture or missed the batch/parser boundary.");
        Debug.Log("SKILL_PROSE_VALIDATION_BOUNDARY=PASS; actual-native-rejected=3; unicode-number-variants=7; model-calls=0");
    }

    private static void VerifyCharacterSkillDisplayNameIdentityBoundary()
    {
        const string CharacterName = "Miru";
        CharacterSkillModuleSelectionResponseDto Candidate(
            string selectionId,
            string moduleId,
            string displayName,
            string narrativeFlavor) => new()
        {
            selectionId = selectionId,
            positiveModuleIds = new List<string> { moduleId },
            drawbackModuleIds = new List<string>(),
            evidenceFactIds = new List<string> { "public-fact:display-name-boundary" },
            displayName = displayName,
            narrativeFlavor = narrativeFlavor
        };

        CharacterSkillModuleSelectionResponseDto actualR407Name = Candidate(
            "selection:skill:miru-name-only",
            "guard_module",
            CharacterName,
            "기억을 바탕으로 새 기술을 익힌다.");
        Require(!actualR407Name.Validate(out string nameOnlyError, CharacterName)
                && !string.IsNullOrWhiteSpace(nameOnlyError)
                && nameOnlyError.Contains("displayName", StringComparison.Ordinal),
            "CharacterSkill accepted the r407 character name alone as a skill name or returned no clear error.");

        var duplicateNames = new CharacterSkillModuleSelectionBatchResponseDto
        {
            candidates = new List<CharacterSkillModuleSelectionResponseDto>
            {
                Candidate(
                    "selection:skill:starlight-guard",
                    "guard_module",
                    "별빛 수호",
                    "Miru의 경계 기억을 새로운 수호 기술로 엮는다."),
                Candidate(
                    "selection:skill:starlight-focus",
                    "focus_module",
                    "  별빛 수호  ",
                    "Miru의 집중 경험을 다른 방식의 기술로 엮는다.")
            }
        };
        Require(!duplicateNames.Validate(out string duplicateNameError, CharacterName)
                && !string.IsNullOrWhiteSpace(duplicateNameError)
                && duplicateNameError.Contains("skill name", StringComparison.Ordinal),
            "CharacterSkill batch accepted duplicate trimmed skill names across distinct selections/modules.");

        var distinctNames = new CharacterSkillModuleSelectionBatchResponseDto
        {
            candidates = new List<CharacterSkillModuleSelectionResponseDto>
            {
                Candidate(
                    "selection:skill:starlight-guard",
                    "guard_module",
                    "별빛 수호",
                    "Miru의 경계 기억을 새로운 수호 기술로 엮는다."),
                Candidate(
                    "selection:skill:dawn-focus",
                    "focus_module",
                    "새벽 집중",
                    "Miru의 집중 경험을 새로운 집중 기술로 엮는다.")
            }
        };
        Require(distinctNames.Validate(out string distinctNameError, CharacterName)
                && string.IsNullOrEmpty(distinctNameError),
            "CharacterSkill rejected distinct skill names with the character name confined to narrativeFlavor: "
            + distinctNameError);
    }

    private static JArray ExpandCompactRows(JObject table, bool keepNulls)
    {
        string[] columns = ((JArray)table["columns"]).Values<string>().ToArray();
        JArray expanded = new();
        foreach (JArray row in (JArray)table["rows"])
        {
            Require(row.Count == columns.Length, "Compact row width differs.");
            JObject value = new();
            for (int index = 0; index < columns.Length; index++)
                if (keepNulls || row[index].Type != JTokenType.Null) value[columns[index]] = row[index].DeepClone();
            expanded.Add(value);
        }
        return expanded;
    }

    private static void VerifyAcquiredTraitRequestLocalSchemaGrammar()
    {
        const string ActorId = "character:trait-request-schema";
        const string SelectionId = "selection:trait:request-local-schema";
        const string PositiveA = "acquired-trait:module:steady-hand";
        const string PositiveB = "acquired-trait:module:keen-memory";
        const string Drawback = "acquired-trait:drawback:restless-sleep";
        NarrativePublicContextMaterial material = NarrativePublicContextFactory.Build(
            "Persona",
            ActorId,
            NarrativePublicSubjectKind.Character,
            string.Empty,
            requireCharacterFact: false,
            requireMotif: false,
            new[]
            {
                new NarrativePublicEntityInput(
                    ActorId,
                    NarrativePublicEntityKind.Character,
                    "요청 스키마 검증 인물")
            },
            new[]
            {
                new NarrativePublicFactInput(
                    "trait-schema",
                    "fact:evidence:a",
                    ActorId,
                    "첫 번째 허용 근거",
                    100,
                    NarrativePublicFactCategory.Memory,
                    ActorId),
                new NarrativePublicFactInput(
                    "trait-schema",
                    "fact:evidence:b",
                    ActorId,
                    "두 번째 허용 근거",
                    90,
                    NarrativePublicFactCategory.Memory,
                    ActorId),
                new NarrativePublicFactInput(
                    "trait-schema",
                    "fact:background-only",
                    ActorId,
                    "공개 문맥에만 있는 배경 사실",
                    80,
                    NarrativePublicFactCategory.Background,
                    ActorId)
            },
            "qa-trait-request-schema-v1");
        bool foundEvidenceA = material.TryGetProjectedFactId(
            "trait-schema", "fact:evidence:a", ActorId, out string evidenceA);
        bool foundEvidenceB = material.TryGetProjectedFactId(
            "trait-schema", "fact:evidence:b", ActorId, out string evidenceB);
        bool foundBackgroundOnly = material.TryGetProjectedFactId(
            "trait-schema", "fact:background-only", ActorId, out string backgroundOnly);
        Require(foundEvidenceA && foundEvidenceB && foundBackgroundOnly,
            "Trait request schema fixture lost a projected public fact ID.");
        string[] evidence = { evidenceA, evidenceB };
        string header =
            "selectionId=" + SelectionId + "\n"
            + "- moduleId=" + PositiveA + "; polarity=Positive; meaning=steady hands\n"
            + "- moduleId=" + PositiveB + "; polarity=Positive; meaning=clear memory\n"
            + "- moduleId=" + Drawback + "; polarity=Drawback; meaning=restless sleep\n"
            + "evidenceFactIds=" + string.Join(",", evidence) + "\n";
        NarrativePublicPromptEnvelope envelope = NarrativePublicPromptEnvelope.Create(
            header + "요청별 trait schema 문법 검증", material);
        string originalPrompt = envelope.Prompt;
        string originalSemanticHash = envelope.PublicContextSemanticHash;
        LlmStaticSchemaDefinition traitSchema = LlmStaticSchemaCatalog.Require(
            LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id);
        byte[] staticSchemaBytes = traitSchema.Utf8Bytes.ToArray();

        JObject request = JObject.Parse(Encoding.UTF8.GetString(
            DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection,
                traitSchema,
                originalPrompt)));
        JObject properties = (JObject)request["response_format"]["json_schema"]["schema"]["properties"];
        Require(((JArray)properties["selectionId"]["enum"]).Values<string>()
                .SequenceEqual(new[] { SelectionId }, StringComparer.Ordinal),
            "Trait request schema did not pin the exact offered selection ID.");
        Require((int)properties["positiveModuleIds"]["minItems"] == 1
                && (int)properties["positiveModuleIds"]["maxItems"] == 1
                && ((JArray)properties["positiveModuleIds"]["items"]["enum"]).Values<string>()
                    .SequenceEqual(new[] { PositiveA, PositiveB }, StringComparer.Ordinal),
            "Trait request schema did not preserve both positive offers while requiring one choice.");
        Require((int)properties["drawbackModuleIds"]["maxItems"] == 1
                && ((JArray)properties["drawbackModuleIds"]["items"]["enum"]).Values<string>()
                    .SequenceEqual(new[] { Drawback }, StringComparer.Ordinal),
            "Trait request schema did not preserve the offered drawback choice.");
        Require((int)properties["evidenceFactIds"]["minItems"] == 1
                && (int)properties["evidenceFactIds"]["maxItems"] == evidence.Length
                && ((JArray)properties["evidenceFactIds"]["items"]["enum"]).Values<string>()
                    .SequenceEqual(evidence, StringComparer.Ordinal)
                && !((JArray)properties["evidenceFactIds"]["items"]["enum"])
                    .Values<string>().Contains(backgroundOnly, StringComparer.Ordinal),
            "Trait request schema evidence enum differs from the exact offered IDs.");

        string noDrawbackHeader = header.Replace(
            "- moduleId=" + Drawback + "; polarity=Drawback; meaning=restless sleep\n",
            string.Empty);
        NarrativePublicPromptEnvelope noDrawbackEnvelope = NarrativePublicPromptEnvelope.Create(
            noDrawbackHeader + "drawback 없는 요청별 trait schema 문법 검증", material);
        JObject noDrawbackRequest = JObject.Parse(Encoding.UTF8.GetString(
            DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection,
                traitSchema,
                noDrawbackEnvelope.Prompt)));
        JToken noDrawbackProperty = noDrawbackRequest["response_format"]["json_schema"]["schema"]
            ["properties"]["drawbackModuleIds"];
        Require((int)noDrawbackProperty["maxItems"] == 0
                && noDrawbackProperty["items"]["enum"] == null,
            "Trait request schema did not forbid drawbacks when none were offered.");

        RequireInvalidOperation(
            () => DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection,
                traitSchema,
                NarrativePublicPromptEnvelope.Create(
                    header.Replace(
                        "evidenceFactIds=" + string.Join(",", evidence) + "\n",
                        string.Empty)
                    + "근거 헤더 누락 검증",
                    material).Prompt),
            "Trait request schema accepted a missing evidenceFactIds header.");
        RequireInvalidOperation(
            () => DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection,
                traitSchema,
                NarrativePublicPromptEnvelope.Create(
                    "selectionId=selection:trait:duplicate\n" + header
                    + "selection 헤더 중복 검증",
                    material).Prompt),
            "Trait request schema accepted duplicate selectionId headers.");
        RequireInvalidOperation(
            () => DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection,
                traitSchema,
                NarrativePublicPromptEnvelope.Create(
                    header.Replace("polarity=Drawback", "polarity=Neutral")
                    + "잘못된 polarity 검증",
                    material).Prompt),
            "Trait request schema accepted an invalid module polarity.");
        RequireInvalidOperation(
            () => DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection,
                traitSchema,
                NarrativePublicPromptEnvelope.Create(
                    header.Replace(
                        "evidenceFactIds=" + string.Join(",", evidence),
                        "evidenceFactIds=" + evidenceA + "," + evidenceA)
                    + "중복 근거 ID 검증",
                    material).Prompt),
            "Trait request schema accepted duplicate evidence fact IDs.");

        LlmStaticSchemaDefinition otherSchema = LlmStaticSchemaCatalog.Require(
            LocalLlmRequestProfiles.CharacterRecord.Id);
        byte[] otherStaticSchemaBytes = otherSchema.Utf8Bytes.ToArray();
        JObject otherRequest = JObject.Parse(Encoding.UTF8.GetString(
            DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                LocalLlmRequestProfiles.CharacterRecord,
                otherSchema,
                originalPrompt)));
        Require(JToken.DeepEquals(
                otherRequest["response_format"]["json_schema"]["schema"],
                JObject.Parse(otherSchema.Json))
                && string.Equals(
                    (string)otherRequest["messages"][1]["content"],
                    NarrativeRequestContext.ToModelPrompt(originalPrompt),
                    StringComparison.Ordinal),
            "A non-trait profile request payload or static schema changed.");
        Require(staticSchemaBytes.SequenceEqual(traitSchema.Utf8Bytes)
                && otherStaticSchemaBytes.SequenceEqual(otherSchema.Utf8Bytes)
                && string.Equals(envelope.Prompt, originalPrompt, StringComparison.Ordinal)
                && string.Equals(
                    envelope.PublicContextSemanticHash,
                    originalSemanticHash,
                    StringComparison.Ordinal)
                && string.Equals(
                    envelope.ModelInput.SemanticHash,
                    originalSemanticHash,
                    StringComparison.Ordinal),
            "Trait request-local schema generation mutated static bytes, the prompt, or semantic identity.");
    }

    private static void RequireInvalidOperation(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void VerifyAcquiredTraitDomainEvidenceSupport()
    {
        const string ActorId = "character:trait-domain-support";
        string exactDomainTaggedId =
            "public-fact:sha256:" + new string('a', 64);
        string exactPlainCombatId =
            "public-fact:sha256:" + new string('b', 64);
        string workIdentityOnlyId =
            "public-fact:sha256:" + new string('c', 64);
        string invalidDomainId =
            "public-fact:sha256:" + new string('d', 64);
        string unofferedId =
            "public-fact:sha256:" + new string('e', 64);
        CharacterNarrativeFact localCombat = new CharacterNarrativeFact
        {
            domain = CharacterNarrativeDomain.Combat,
            factId = "trait-domain-local-combat",
            subjectId = ActorId,
            outcome = "전투에서 승리했다.",
            count = 2,
            sourceEventIds = new List<string> { string.Empty, string.Empty },
            milestoneCount = 1,
            importancePoints = 3f
        };
        string localCombatId = CharacterAcquiredTraitEvidenceProjection.Project(localCombat);
        CharacterAcquiredTraitPendingRequestState frozen =
            new CharacterAcquiredTraitPendingRequestState
            {
                evidenceFactIds = new List<string>
                {
                    workIdentityOnlyId,
                    exactPlainCombatId,
                    localCombatId,
                    invalidDomainId,
                    exactDomainTaggedId,
                    exactPlainCombatId,
                    localCombatId
                },
                moduleSelectionLocalFacts = new List<CharacterNarrativeFact>
                {
                    null,
                    localCombat
                },
                evidenceBindings = new List<GameplayOutcomeEvidenceBindingSnapshot>
                {
                    null,
                    new GameplayOutcomeEvidenceBindingSnapshot
                    {
                        publicFactId = exactDomainTaggedId,
                        semanticTags = new List<string> { "domain:combat" }
                    },
                    new GameplayOutcomeEvidenceBindingSnapshot
                    {
                        publicFactId = exactPlainCombatId,
                        semanticTags = new List<string> { "cOmBaT" }
                    },
                    new GameplayOutcomeEvidenceBindingSnapshot
                    {
                        publicFactId = workIdentityOnlyId,
                        canonicalFactText = "Combat 전투에서 적을 쓰러뜨린 기록",
                        factIds = new List<string> { "combat:victory", "domain:combat" },
                        semanticTags = new List<string> { "work-identity" }
                    },
                    new GameplayOutcomeEvidenceBindingSnapshot
                    {
                        publicFactId = invalidDomainId,
                        semanticTags = new List<string>
                        {
                            null,
                            "domain:not-a-real-domain",
                            "999"
                        }
                    },
                    new GameplayOutcomeEvidenceBindingSnapshot
                    {
                        publicFactId = unofferedId,
                        semanticTags = new List<string> { "domain:combat" }
                    }
                }
            };
        string originalJson = JsonUtility.ToJson(frozen);
        var method = typeof(CharacterAcquiredTraitPromptBuilder).GetMethod(
            "BuildDomainEvidenceSupport",
            System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.NonPublic);
        Require(method != null,
            "Acquired-trait domain evidence support helper is missing.");

        IReadOnlyList<string> combatSupport = (IReadOnlyList<string>)method.Invoke(
            null,
            new object[]
            {
                frozen,
                new[] { CharacterNarrativeDomain.Combat }
            });
        string[] expectedCombatSupport =
        {
            localCombatId,
            exactDomainTaggedId,
            exactPlainCombatId
        };
        expectedCombatSupport = expectedCombatSupport
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(combatSupport.SequenceEqual(expectedCombatSupport, StringComparer.Ordinal)
                && !combatSupport.Contains(workIdentityOnlyId, StringComparer.Ordinal)
                && !combatSupport.Contains(invalidDomainId, StringComparer.Ordinal)
                && !combatSupport.Contains(unofferedId, StringComparer.Ordinal),
            "Acquired-trait Combat support was not restricted to frozen, explicitly domain-qualified evidence.");

        IReadOnlyList<string> workSupport = (IReadOnlyList<string>)method.Invoke(
            null,
            new object[]
            {
                frozen,
                new[] { CharacterNarrativeDomain.Work }
            });
        Require(workSupport.Count == 0
                && !workSupport.Contains(localCombatId, StringComparer.Ordinal)
                && !workSupport.Contains(workIdentityOnlyId, StringComparer.Ordinal),
            "Acquired-trait Work support accepted Combat-local or prose-only exact evidence.");
        Require(string.Equals(JsonUtility.ToJson(frozen), originalJson, StringComparison.Ordinal),
            "Acquired-trait domain evidence support mutated its frozen input DTO.");
    }

    private static void VerifyCharacterSkillLosslessCompactTransport()
    {
        const string RawPrimary =
            "주효과 계약 v1: fixture primary wording remains unchanged.";
        const string ProductionPrimary =
            "주효과 계약 v1: positiveModuleIds[0]이 주효과다. 나머지는 보조효과다. 이름과 설명은 첫 효과를 중심으로 쓰고 보조효과를 주효과처럼 이름 붙이지 않는다. 수치·비율은 C#만 배분한다.";
        const string ProseAuthorityInstruction =
            "각 후보에 서로 다른 기술명과 완결된 획득 서사를 창작하되 사실을 만들지는 않는다. "
            + "displayName은 첫 positiveModuleIds의 특징과 문화 모티프를 담은 서로 다른 한국어 기술명이다. "
            + "캐릭터 본명만 기술명으로 쓰지 않는다. "
            + "narrativeFlavor는 character의 정확한 본명을 문장의 주어로 한 번 넣고, 인용한 실제 경험을 문화 모티프로 비유하여 주효과를 익힌 이유를 쓴다. "
            + "본명을 확인하는 문구나 작성 지시를 서사로 쓰지 않는다. 후보별 경험과 주효과가 드러나는 서로 다른 한국어 한 문장을 마침표로 완결한다. "
            + "기술을 익히기 전의 실제 경험과 이제 얻는 기술을 구분한다. 인용한 근거에 없는 청소·방 이동·전투·부상·기술 사용을 과거 사건으로 만들지 않는다. "
            + "주변 환경 변화는 본인이 만든 결과라는 증거가 아니다. 주효과의 모듈 의미를 다른 기능이나 다른 지표의 변화로 바꾸지 않는다. "
            + "문화는 비유로만 쓰고, 주변 수치보다 완료한 행동·경험 근거를 우선한다. "
            + "근거 문장을 그대로 옮기거나 지표 변화·기계 효과를 설명하지 않는다. "
            + "숫자·퍼센트·지표명·ID는 서술에 복사하지 않는다. 숫자를 한글·한자 등으로 바꿔 쓰는 것도 금지다. "
            + "정확한 캐릭터 본명에 원래 포함된 숫자만 예외다. 이름은 14자 이하, 서사는 마침표를 포함해 48자 이하로 쓴다.";
        string evidenceA = "public-fact:sha256:" + new string('1', 64);
        string evidenceShared = "public-fact:sha256:" + new string('2', 64);
        string evidenceB = "public-fact:sha256:" + new string('3', 64);
        string evidenceC = "public-fact:sha256:" + new string('4', 64);
        const string SharedMechanics =
            "skillKind=Active; trigger=OnHit; target=Enemy; targeting=Single; area=Single; areaSize=1; ultimateDomain=Combat";
        const string SharedContext =
            "shared combat context with the same mechanics";
        string packet =
            "candidateCount=3\n"
            + "candidate[0]:\n"
            + "selectionId=selection:skill:alpha\n"
            + SharedMechanics + "\n"
            + "skillContext=" + SharedContext + "\n"
            + "positiveModuleLimit=1..2\n"
            + RawPrimary + "\n"
            + "positiveModules:\n"
            + "- id=shared_module; meaning=첫 문맥 의미\n"
            + "drawbackModules:\n"
            + "- none (return [])\n"
            + "evidenceFactIds=" + evidenceA + "," + evidenceShared + "\n"
            + "candidate[1]:\n"
            + "selectionId=selection:skill:beta\n"
            + SharedMechanics + "\n"
            + "skillContext=" + SharedContext + "\n"
            + "positiveModuleLimit=1..3\n"
            + RawPrimary + "\n"
            + "positiveModules:\n"
            + "- id=shared_module; meaning=둘째 문맥의 다른 의미\n"
            + "- id=focus_module; meaning=집중 보조\n"
            + "drawbackModules:\n"
            + "- id=character-skill:drawback:fatigue; meaning=피로 누적\n"
            + "evidenceFactIds=" + evidenceShared + "," + evidenceB + "," + evidenceC + "\n"
            + "candidate[2]:\n"
            + "selectionId=selection:skill:gamma\n"
            + "skillKind=Passive; trigger=OnWorkCompleted; target=Self; targeting=Self; area=Single; areaSize=1; ultimateDomain=Work\n"
            + "skillContext=distinct work context\n"
            + "positiveModuleLimit=1..1\n"
            + ProductionPrimary + "\n"
            + "positiveModules:\n"
            + "- id=guard_module; meaning=안정 유지\n"
            + "drawbackModules:\n"
            + "- id=character-skill:drawback:delay; meaning=반응 지연\n"
            + "- id=character-skill:drawback:strain; meaning=긴장 누적\n"
            + "evidenceFactIds=" + evidenceC + "\n"
            + "sharedEvidenceFacts:\n"
            + "authoritativeOutcomeEvidence:\n"
            + "- id=" + evidenceA
            + "; sourceKind=exact; selectable=true; outcomeType=combat.hit; subject=character:hero; days=10-11; roles=actor,target; metrics=damage=12.5 hp,morale=-3.75 points; cause=; fact=문을 열었다; 한글 사실 95.03/100\n"
            + "- id=" + evidenceShared
            + "; sourceKind=exact; selectable=true; outcomeType=work.complete; subject=facility:smithy; days=12-12; roles=worker; metrics=momentum=1.25e2 points; cause=outcome:root; fact=제작을 마쳤다\n"
            + "- id=" + evidenceB
            + "; sourceKind=compacted-context-only; selectable=false-context-only; outcomeType=memory.summary; subject=character:hero; days=1-20; roles=subject; metrics=; cause=; fact=장기 기억 요약\n"
            + "- id=" + evidenceC
            + "; sourceKind=exact; selectable=true; outcomeType=combat.hit; subject=character:hero; days=10-11; roles=actor,target; metrics=guard=-0.125 ratio; cause=; fact=문을 열었다; 한글 사실 95.03/100\n";
        string packetSnapshot = packet;
        VerifyCharacterSkillOptionalPublicCause(packet, evidenceA, evidenceShared, evidenceB);

        JObject captured = CaptureCharacterSkillCompactPacket(packet);
        string restored = RestoreCharacterSkillCompactPacket(captured);
        byte[] firstBytes = Encoding.UTF8.GetBytes(
            captured.ToString(Newtonsoft.Json.Formatting.None));
        byte[] secondBytes = Encoding.UTF8.GetBytes(
            CaptureCharacterSkillCompactPacket(packet)
                .ToString(Newtonsoft.Json.Formatting.None));
        Require(string.Equals(restored, packet, StringComparison.Ordinal)
                && restored.IndexOf('\r') < 0
                && firstBytes.SequenceEqual(secondBytes),
            "CharacterSkill compact packet capture/restore is not exact LF or deterministic.");

        string rawTableJson = captured.ToString(Newtonsoft.Json.Formatting.None);
        JObject modelTable = CompactCharacterSkillTable(captured);
        JObject expandedTable = ExpandCharacterSkillTable(modelTable);
        byte[] firstModelBytes = Encoding.UTF8.GetBytes(
            modelTable.ToString(Newtonsoft.Json.Formatting.None));
        byte[] secondModelBytes = Encoding.UTF8.GetBytes(
            CompactCharacterSkillTable(captured)
                .ToString(Newtonsoft.Json.Formatting.None));
        Require(JToken.DeepEquals(expandedTable, captured)
                && string.Equals(
                    RestoreCharacterSkillCompactPacket(expandedTable),
                    packet,
                    StringComparison.Ordinal)
                && string.Equals(
                    captured.ToString(Newtonsoft.Json.Formatting.None),
                    rawTableJson,
                    StringComparison.Ordinal)
                && firstModelBytes.SequenceEqual(secondModelBytes),
            "CharacterSkill model projection is not lossless, packet-exact, immutable, or deterministic.");

        JArray modelCandidates = (JArray)modelTable["C"];
        JArray modelContexts = (JArray)modelTable["X"];
        JArray allowedCatalog = (JArray)modelTable["A"];
        JArray modelOutcomes = (JArray)modelTable["O"];
        Require(string.Equals((string)modelTable["C_columns"][5], "allowed_A",
                    StringComparison.Ordinal)
                && string.Equals((string)modelTable["X_columns"][2], "primary_P",
                    StringComparison.Ordinal)
                && allowedCatalog.Count == 3
                && modelCandidates.OfType<JArray>().Select(row => (int)row[5])
                    .SequenceEqual(new[] { 0, 1, 2 })
                && allowedCatalog.OfType<JArray>().Select(row => row.Count)
                    .SequenceEqual(new[] { 2, 3, 1 })
                && ((JArray)modelTable["P"]).Count == 2
                && modelContexts.OfType<JArray>().Select(row => (int)row[2])
                    .SequenceEqual(new[] { 0, 1 })
                && string.Equals(
                    (string)modelTable["E_prefix"],
                    "public-fact:sha256:",
                    StringComparison.Ordinal)
                && ((JArray)modelTable["E"]).Values<string>().SequenceEqual(
                    new[]
                    {
                        new string('1', 64),
                        new string('2', 64),
                        new string('3', 64),
                        new string('4', 64)
                    },
                    StringComparer.Ordinal)
                && ((JArray)modelTable["O_columns"]).Values<string>().SequenceEqual(
                    new[] { "E", "H", "G" }, StringComparer.Ordinal)
                && ((JArray)modelTable["H_columns"]).Values<string>().SequenceEqual(
                    new[]
                    {
                        "sourceKind", "selectable", "outcomeType", "subject_S",
                        "days", "roles_R", "cause", "fact_T", "experience"
                    },
                    StringComparer.Ordinal)
                && ((JArray)modelTable["H"]).Count == 3
                && ((JArray)modelTable["G"]).Count == 4
                && (int)((JArray)modelOutcomes[0])[1]
                    == (int)((JArray)modelOutcomes[3])[1]
                && ((JArray)modelTable["V"]).Values<string>().SequenceEqual(
                    new[] { "12.5", "-3.75", "1.25e2", "-0.125" },
                    StringComparer.Ordinal),
            "CharacterSkill model projection lost candidate membership, evidence factoring, outcome headers, or numeric spelling.");

        JObject mixedEvidenceTable = (JObject)captured.DeepClone();
        ((JArray)mixedEvidenceTable["E"])[2] = "legacy-evidence:mixed";
        JObject mixedModelTable = CompactCharacterSkillTable(mixedEvidenceTable);
        Require(string.Equals((string)mixedModelTable["E_prefix"], string.Empty,
                    StringComparison.Ordinal)
                && ((JArray)mixedModelTable["E"]).Values<string>().SequenceEqual(
                    new[]
                    {
                        evidenceA,
                        evidenceShared,
                        "legacy-evidence:mixed",
                        evidenceC
                    },
                    StringComparer.Ordinal)
                && JToken.DeepEquals(
                    ExpandCharacterSkillTable(mixedModelTable),
                    mixedEvidenceTable)
                && string.Equals(
                    captured.ToString(Newtonsoft.Json.Formatting.None),
                    rawTableJson,
                    StringComparison.Ordinal),
            "CharacterSkill mixed evidence projection factored a non-shared prefix or mutated the source table.");

        JArray candidates = (JArray)captured["C"];
        JArray contexts = (JArray)captured["X"];
        JArray modules = (JArray)captured["M"];
        JArray evidence = (JArray)captured["E"];
        string[] DecodeModules(JArray indices) => indices.Values<int>()
            .Select(index => (string)modules[index][0] + "|" + (string)modules[index][1])
            .ToArray();
        string[] DecodeModuleIds(JArray indices) => indices.Values<int>()
            .Select(index => (string)modules[index][0])
            .ToArray();
        string[] DecodeEvidence(JArray indices) => indices.Values<int>()
            .Select(index => (string)evidence[index])
            .ToArray();
        JArray candidate0 = (JArray)candidates[0];
        JArray candidate1 = (JArray)candidates[1];
        JArray candidate2 = (JArray)candidates[2];
        Require(((JArray)captured["C_columns"]).Values<string>().SequenceEqual(
                    new[]
                    {
                        "selectionId", "X", "positiveModuleLimit", "positive_M",
                        "drawback_M", "allowed_E", "emptyDrawbackMarker"
                    }, StringComparer.Ordinal)
                && ((JArray)captured["X_columns"]).Values<string>().SequenceEqual(
                    new[] { "mechanics", "context", "primaryContract" },
                    StringComparer.Ordinal)
                && candidates.OfType<JArray>().Select(row => (string)row[0])
                .SequenceEqual(new[]
                {
                    "selection:skill:alpha",
                    "selection:skill:beta",
                    "selection:skill:gamma"
                }, StringComparer.Ordinal)
                && (int)candidate0[1] == (int)candidate1[1]
                && string.Equals((string)contexts[(int)candidate0[1]][2], RawPrimary,
                    StringComparison.Ordinal)
                && !string.Equals((string)contexts[(int)candidate2[1]][2], ProductionPrimary,
                    StringComparison.Ordinal),
            "CharacterSkill compact candidates lost order, shared context, or reversible primary wording.");
        Require(DecodeModules((JArray)candidate0[3]).SequenceEqual(
                    new[] { "shared_module|첫 문맥 의미" }, StringComparer.Ordinal)
                && DecodeModules((JArray)candidate1[3]).SequenceEqual(
                    new[]
                    {
                        "shared_module|둘째 문맥의 다른 의미",
                        "focus_module|집중 보조"
                    }, StringComparer.Ordinal)
                && DecodeModules((JArray)candidate2[3]).SequenceEqual(
                    new[] { "guard_module|안정 유지" }, StringComparer.Ordinal)
                && string.Equals((string)candidate0[2], "1..2", StringComparison.Ordinal)
                && string.Equals((string)candidate1[2], "1..3", StringComparison.Ordinal)
                && string.Equals((string)candidate2[2], "1..1", StringComparison.Ordinal)
                && DecodeModules((JArray)candidate0[4]).Length == 0
                && DecodeModules((JArray)candidate1[4]).SequenceEqual(
                    new[] { "character-skill:drawback:fatigue|피로 누적" },
                    StringComparer.Ordinal)
                && DecodeModules((JArray)candidate2[4]).SequenceEqual(
                    new[]
                    {
                        "character-skill:drawback:delay|반응 지연",
                        "character-skill:drawback:strain|긴장 누적"
                    }, StringComparer.Ordinal)
                && (bool)candidate0[6]
                && !(bool)candidate1[6]
                && !(bool)candidate2[6],
            "CharacterSkill compact transport changed per-candidate positive or drawback offers.");
        Require(DecodeEvidence((JArray)candidate0[5]).SequenceEqual(
                    new[] { evidenceA, evidenceShared }, StringComparer.Ordinal)
                && DecodeEvidence((JArray)candidate1[5]).SequenceEqual(
                    new[] { evidenceShared, evidenceB, evidenceC }, StringComparer.Ordinal)
                && DecodeEvidence((JArray)candidate2[5]).SequenceEqual(
                    new[] { evidenceC }, StringComparer.Ordinal)
                && evidence.Values<string>().SequenceEqual(
                    new[] { evidenceA, evidenceShared, evidenceB, evidenceC },
                    StringComparer.Ordinal)
                && evidence.Values<string>().Distinct(StringComparer.Ordinal).Count()
                    == evidence.Count,
            "CharacterSkill compact transport changed candidate evidence or duplicated full IDs.");
        Require(modules.OfType<JArray>()
                .Where(row => string.Equals(
                    (string)row[0], "shared_module", StringComparison.Ordinal))
                .Select(row => (string)row[1])
                .SequenceEqual(new[]
                {
                    "첫 문맥 의미",
                    "둘째 문맥의 다른 의미"
                }, StringComparer.Ordinal),
            "CharacterSkill compact transport merged one module ID with different meanings.");

        JArray outcomes = (JArray)captured["O"];
        JArray metricCatalog = (JArray)captured["D_metricId_unit"];
        string CatalogValue(string key, int index) => (string)captured[key][index];
        string[] DecodeMetrics(JArray row) => ((JArray)row[7]).OfType<JArray>()
            .Select(metric => (string)metricCatalog[(int)metric[0]][0]
                + "=" + (string)metric[1] + " "
                + (string)metricCatalog[(int)metric[0]][1])
            .ToArray();
        JArray outcome0 = (JArray)outcomes[0];
        JArray outcome1 = (JArray)outcomes[1];
        JArray outcome2 = (JArray)outcomes[2];
        Require(((JArray)captured["O_columns"]).Values<string>().SequenceEqual(
                    new[]
                    {
                        "E", "sourceKind", "selectable", "outcomeType", "subject_S",
                        "days", "roles_R", "metrics_D_value", "cause", "fact_T", "experience"
                    }, StringComparer.Ordinal)
                && outcomes.OfType<JArray>().All(row => row.Count == 11)
                && outcomes.Count == 4
                && string.Equals(CatalogValue("E", (int)outcome0[0]), evidenceA,
                    StringComparison.Ordinal)
                && string.Equals((string)outcome0[1], "exact", StringComparison.Ordinal)
                && string.Equals((string)outcome0[2], "true", StringComparison.Ordinal)
                && string.Equals((string)outcome0[3], "combat.hit", StringComparison.Ordinal)
                && string.Equals(CatalogValue("S", (int)outcome0[4]), "character:hero",
                    StringComparison.Ordinal)
                && string.Equals((string)outcome0[5], "10-11", StringComparison.Ordinal)
                && string.Equals(CatalogValue("R", (int)outcome0[6]), "actor,target",
                    StringComparison.Ordinal)
                && DecodeMetrics(outcome0).SequenceEqual(
                    new[] { "damage=12.5 hp", "morale=-3.75 points" },
                    StringComparer.Ordinal)
                && string.Equals((string)outcome0[8], string.Empty,
                    StringComparison.Ordinal)
                && string.Equals(CatalogValue("T", (int)outcome0[9]),
                    "문을 열었다; 한글 사실 95.03/100", StringComparison.Ordinal)
                && DecodeMetrics(outcome1).SequenceEqual(
                    new[] { "momentum=1.25e2 points" }, StringComparer.Ordinal)
                && string.Equals((string)outcome2[1], "compacted-context-only",
                    StringComparison.Ordinal)
                && string.Equals((string)outcome2[2], "false-context-only",
                    StringComparison.Ordinal)
                && ((JArray)outcome2[7]).Count == 0
                && ((JArray)captured["S"]).Values<string>().SequenceEqual(
                    new[] { "character:hero", "facility:smithy" },
                    StringComparer.Ordinal)
                && ((JArray)captured["R"]).Values<string>().SequenceEqual(
                    new[] { "actor,target", "worker", "subject" },
                    StringComparer.Ordinal)
                && metricCatalog.OfType<JArray>().Select(row =>
                    (string)row[0] + "|" + (string)row[1]).SequenceEqual(
                    new[] { "damage|hp", "morale|points", "momentum|points", "guard|ratio" },
                    StringComparer.Ordinal)
                && ((JArray)captured["T"]).Values<string>().SequenceEqual(
                    new[]
                    {
                        "문을 열었다; 한글 사실 95.03/100",
                        "제작을 마쳤다",
                        "장기 기억 요약"
                    }, StringComparer.Ordinal),
            "CharacterSkill compact outcome tables changed an authoritative outcome or metric field.");

        RequireInvalidOperation(
            () => CaptureCharacterSkillCompactPacket(packet.Replace(
                "; outcomeType=combat.hit", string.Empty)),
            "CharacterSkill compact transport accepted a missing outcome field.");
        RequireInvalidOperation(
            () => CaptureCharacterSkillCompactPacket(packet.Replace(
                "sharedEvidenceFacts:\n", "unknownLine=true\nsharedEvidenceFacts:\n")),
            "CharacterSkill compact transport accepted an unknown packet line.");
        RequireInvalidOperation(
            () => CaptureCharacterSkillCompactPacket(packet.Replace(
                "momentum=1.25e2 points", "momentum=NaN points")),
            "CharacterSkill compact transport accepted a nonfinite metric.");
        RequireInvalidOperation(
            () => CaptureCharacterSkillCompactPacket(packet.Replace(
                "evidenceFactIds=" + evidenceA + "," + evidenceShared,
                "evidenceFactIds=" + evidenceA + "," + evidenceA)),
            "CharacterSkill compact transport accepted duplicate candidate evidence.");

        const string localFactSuffix =
            "F01 = 첫 값; 한국어\n"
            + "F02 = 반복 값; 세미콜론\n"
            + "M01 = 반복 값; 세미콜론\n"
            + "unknown line; 그대로\n"
            + "F03 = 반복 값; 세미콜론\n"
            + "F10 = 마지막 값\n";
        string compactLocalFacts = CompactCharacterSkillLocalFactLines(
            localFactSuffix,
            out JArray localFactCatalog);
        string secondCompactLocalFacts = CompactCharacterSkillLocalFactLines(
            localFactSuffix,
            out JArray secondLocalFactCatalog);
        Require(string.Equals(
                    compactLocalFacts,
                    "F01 = @0\n"
                    + "F02 = @1\n"
                    + "M01 = 반복 값; 세미콜론\n"
                    + "unknown line; 그대로\n"
                    + "F03 = @1\n"
                    + "F10 = @2\n",
                    StringComparison.Ordinal)
                && localFactCatalog.Values<string>().SequenceEqual(
                    new[] { "첫 값; 한국어", "반복 값; 세미콜론", "마지막 값" },
                    StringComparer.Ordinal)
                && string.Equals(
                    RestoreCharacterSkillLocalFactLines(
                        compactLocalFacts,
                        localFactCatalog),
                    localFactSuffix,
                    StringComparison.Ordinal)
                && compactLocalFacts.IndexOf('\r') < 0
                && Encoding.UTF8.GetBytes(compactLocalFacts).SequenceEqual(
                    Encoding.UTF8.GetBytes(secondCompactLocalFacts))
                && JToken.DeepEquals(localFactCatalog, secondLocalFactCatalog),
            "CharacterSkill local fact aliases, values, untouched lines, LF bytes, or deterministic catalog changed.");
        RequireInvalidOperation(
            () => RestoreCharacterSkillLocalFactLines("F02 = @3\n", localFactCatalog),
            "CharacterSkill local fact restore accepted an out-of-range catalog index.");

        string SharedLocalFacts(string suffix) => (string)InvokeNonPublicStatic(
            RequireCharacterSkillCompactModelPromptType(), "SharedLocalFactLines",
            new[] { typeof(string) }, new object[] { suffix });
        const string readableFactInput =
            "intro; 그대로\n"
            + "F01 = 첫 원문; 95.03/100\n"
            + "F02 = 다른 원문\n"
            + "F03 = 첫 원문; 95.03/100\n"
            + "F10 = \n"
            + "M01 = 첫 원문; 95.03/100\n"
            + "F04 = 첫 원문; 95.03/100\n"
            + "end\n";
        string sharedReadableFacts = SharedLocalFacts(readableFactInput);
        Require(sharedReadableFacts ==
                    "intro; 그대로\n"
                    + "F01,F03 = 첫 원문; 95.03/100\n"
                    + "F02 = 다른 원문\n"
                    + "F10 = \n"
                    + "M01 = 첫 원문; 95.03/100\n"
                    + "F04 = 첫 원문; 95.03/100\n"
                    + "end\n"
                && sharedReadableFacts == SharedLocalFacts(readableFactInput)
                && sharedReadableFacts.IndexOf('\r') < 0
                && SharedLocalFacts("unknown line\n") == "unknown line\n"
                && SharedLocalFacts(string.Empty) == string.Empty,
            "CharacterSkill shared F view changed aliases, exact/empty values, non-fact boundaries, LF, or determinism.");

        const string OutputFormat =
            "형식: {\"candidates\":[{\"selectionId\":\"제공된 값 그대로\",\"positiveModuleIds\":[\"lowercase_snake_case 1~3개\"],\"drawbackModuleIds\":[\"character-skill:drawback:lowercase-kebab 0~1개\"],\"evidenceFactIds\":[\"제공된 근거 1~2개\"],\"displayName\":\"14자 이하 한국어 이름\",\"narrativeFlavor\":\"48자 이하 간결한 한국어 한 문장\"}]}";
        const string AllocationContract =
            "주효과 계약 v1: positiveModuleIds의 첫 항목은 근거 서사의 중심 효과이며 나머지는 보조다. C#은 추가 강화 예산을 주효과에 보조 전체 이상 배분한다. displayName은 첫 효과를 중심으로 짓고 보조 기능을 주된 능력처럼 이름 붙이지 않는다. 수치나 배분 비율은 작성하지 않는다.";
        const string CandidateShapeRule =
            "1. 최상위에는 candidates만 출력하고, 제공된 후보 수와 정확히 같은 수의 객체를 제공된 순서대로 출력한다. 각 객체에는 selectionId, positiveModuleIds, drawbackModuleIds, evidenceFactIds, displayName, narrativeFlavor만 출력한다.";
        const string IdRule =
            "2. selectionId는 제공된 값을 한 글자도 바꾸지 않는다. positiveModuleIds에는 positiveModules의 ID만, drawbackModuleIds에는 drawbackModules의 ID만, evidenceFactIds에는 evidenceFactIds의 ID만 1~2개 넣고 서로 섞거나 만들지 않는다.";
        const string CombinationRule =
            "3. 각 후보의 positiveModuleIds에는 어울리는 이로운 모듈을 최소 1개 선택하고, 후보끼리 positiveModuleIds 조합을 중복하지 않는다. drawbackModules가 제공되면 부정 사건이 실제 설명에 필요할 때만 최대 1개 선택하고 그 부정 근거 ID를 반드시 함께 인용한다. 단점 설명의 '함께 선택 금지 이로운 기능'과 겹치는 모듈은 고르지 않는다. narrativeFlavor는 수치나 기계 효과를 설명하지 않는 간결한 한국어 한 문장으로 쓴다. 수치, 비용, 조합 ID는 출력하지 않는다.";
        string promptPrefix =
            "당신은 던전 경영 RPG 기술에 어울리는 기능 모듈과 실제 근거를 고르고 이름과 서사를 작성한다.\n"
            + "C#이 합법인 개별 모듈 전체를 제공한다. 모듈만 선택하며 수치와 비용은 응답 후 C#이 계산한다.\n";
        string promptSuffix =
            "character=검증 용사\n"
            + "origin=fixture\n"
            + "species=human\n"
            + "potential=rare\n"
            + "Available character facts (request-local references):\n"
            + "F01 = 공개 문맥 fixture\n"
            + "F02 = 공개 문맥 fixture\n"
            + "반드시 JSON 객체 하나만 반환한다.\n"
            + OutputFormat + "\n"
            + AllocationContract + "\n"
            + CandidateShapeRule + "\n"
            + IdRule + "\n"
            + CombinationRule + "\n"
            + "4. 공개 사실에 없는 사건이나 결과를 만들지 않는다.\n"
            + "5. displayName 또는 narrativeFlavor에 캐릭터 이름 '검증 용사'을 정확히 넣는다.\n";
        NarrativePublicContextMaterial material = NarrativePublicContextFactory.Build(
            "Persona",
            "character:skill-compact-transport",
            NarrativePublicSubjectKind.Character,
            string.Empty,
            requireCharacterFact: false,
            requireMotif: false,
            new[]
            {
                new NarrativePublicEntityInput(
                    "character:skill-compact-transport",
                    NarrativePublicEntityKind.Character,
                    "검증 용사")
            },
            new[]
            {
                new NarrativePublicFactInput(
                    "skill-compact",
                    "fact:fixture",
                    "character:skill-compact-transport",
                    "공개 문맥 fixture",
                    100,
                    NarrativePublicFactCategory.Background,
                    "character:skill-compact-transport")
            },
            "qa-skill-compact-transport-v1");
        NarrativePublicPromptEnvelope envelope = NarrativePublicPromptEnvelope.Create(
            promptPrefix + packet + promptSuffix, material);
        string originalPrompt = envelope.Prompt;
        string originalPromptSnapshot = originalPrompt;
        string originalSemanticHash = envelope.PublicContextSemanticHash;
        string compactPrompt = BuildCharacterSkillCompactPrompt(originalPrompt);
        string secondCompactPrompt = BuildCharacterSkillCompactPrompt(originalPrompt);
        int proseAuthorityInstructionCount = compactPrompt.Split(
            new[] { ProseAuthorityInstruction }, StringSplitOptions.None).Length - 1;
        Require(proseAuthorityInstructionCount == 1
                && compactPrompt.EndsWith(
                    "\n" + ProseAuthorityInstruction, StringComparison.Ordinal)
                && compactPrompt.Contains("95.03/100", StringComparison.Ordinal)
                && originalPrompt.Contains("95.03/100", StringComparison.Ordinal)
                && string.Equals(packet, packetSnapshot, StringComparison.Ordinal)
                && string.Equals(originalPrompt, originalPromptSnapshot,
                    StringComparison.Ordinal),
            "CharacterSkill prose-authority instruction is not unique/final or changed source quantities.");
        var readableLocalFacts = System.Text.RegularExpressions.Regex.Matches(
                compactPrompt, "^F[0-9]+(?:,F[0-9]+)* = (.+)$",
                System.Text.RegularExpressions.RegexOptions.Multiline)
            .Cast<System.Text.RegularExpressions.Match>().ToArray();
        Require(readableLocalFacts.Length > 0
                && compactPrompt.Contains("F01,F02 = 공개 문맥 fixture\n",
                    StringComparison.Ordinal)
                && readableLocalFacts.All(value => !System.Text.RegularExpressions.Regex.IsMatch(
                    value.Groups[1].Value, "^@[0-9]+$"))
                && compactPrompt.Contains(
                    "E0,E3 [combat.hit; selectable=true] 문을 열었다; 한글 사실 95.03/100\n",
                    StringComparison.Ordinal)
                && compactPrompt.Contains(
                    "E1 [work.complete; selectable=true] 제작을 마쳤다\n",
                    StringComparison.Ordinal)
                && compactPrompt.Contains(
                    "E2 [memory.summary; selectable=false-context-only] 장기 기억 요약\n",
                    StringComparison.Ordinal)
                && System.Text.RegularExpressions.Regex.Matches(compactPrompt,
                    "^E[0-9]+(?:,E[0-9]+)* \\[",
                    System.Text.RegularExpressions.RegexOptions.Multiline).Count == 3,
            "CharacterSkill readable grounding lost exact evidence aliases, original text, eligibility, or decoded prose facts.");
        var differingHintMetadata = new Dictionary<int, JToken>
        {
            [1] = "another-exact",
            [2] = "false-context-only",
            [3] = "combat.other",
            [4] = 1,
            [5] = "12-12",
            [6] = 1,
            [8] = "outcome:other",
            [9] = 1
        };
        foreach (var metadata in differingHintMetadata)
        {
            JObject variantTable = (JObject)captured.DeepClone();
            variantTable["O"][3][metadata.Key] = metadata.Value.DeepClone();
            string variantPacket = RestoreCharacterSkillCompactPacket(variantTable);
            string variantPrompt = BuildCharacterSkillCompactPrompt(
                NarrativePublicPromptEnvelope.Create(
                    promptPrefix + variantPacket + promptSuffix, material).Prompt);
            var variantHints = System.Text.RegularExpressions.Regex.Matches(variantPrompt,
                    "^E[0-9]+(?:,E[0-9]+)* \\[",
                    System.Text.RegularExpressions.RegexOptions.Multiline)
                .Cast<System.Text.RegularExpressions.Match>().ToArray();
            Require(variantHints.Length == 4
                    && variantHints.Count(value => value.Value == "E0 [") == 1
                    && variantHints.Count(value => value.Value == "E3 [") == 1,
                "CharacterSkill shared readable hint merged different outcome metadata column "
                + metadata.Key + ".");
        }
        string requestRef = CharacterSkillRequestRef(originalPrompt);
        string expectedRequestRef;
        using (SHA256 requestHasher = SHA256.Create())
        {
            expectedRequestRef = BitConverter.ToString(requestHasher.ComputeHash(
                    Encoding.UTF8.GetBytes(originalPrompt)))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
        string referencedTableLine = compactPrompt.Split('\n').Single(line =>
            line.StartsWith("{", StringComparison.Ordinal)
            && line.Contains("\"transport\":\"skill-request-refs-v2\"",
                StringComparison.Ordinal));
        JObject builtReferencedTable = JObject.Parse(referencedTableLine);
        JObject directReferencedTable = ReferenceCharacterSkillTable(captured, requestRef);
        JObject comparableBuiltTable = (JObject)builtReferencedTable.DeepClone();
        comparableBuiltTable.Remove("L_values");
        Require(requestRef.Length == 64
                && string.Equals(requestRef, expectedRequestRef, StringComparison.Ordinal)
                && string.Equals(compactPrompt, secondCompactPrompt, StringComparison.Ordinal)
                && string.Equals((string)builtReferencedTable["transport"],
                    "skill-request-refs-v2", StringComparison.Ordinal)
                && string.Equals((string)builtReferencedTable["requestRef"],
                    requestRef, StringComparison.Ordinal)
                && ((JArray)builtReferencedTable["C"]).OfType<JArray>()
                    .Select(row => (string)row[0])
                    .SequenceEqual(new[] { "S0", "S1", "S2" }, StringComparer.Ordinal)
                && ((JArray)builtReferencedTable["E"]).Values<string>()
                    .SequenceEqual(new[] { "E0", "E1", "E2", "E3" },
                        StringComparer.Ordinal)
                && JToken.DeepEquals(comparableBuiltTable, directReferencedTable)
                && JToken.DeepEquals(
                    directReferencedTable,
                    ReferenceCharacterSkillTable(captured, requestRef))
                && JToken.DeepEquals(
                    ExpandReferencedCharacterSkillTable(
                        builtReferencedTable,
                        captured,
                        requestRef),
                    captured)
                && string.Equals(
                    captured.ToString(Newtonsoft.Json.Formatting.None),
                    rawTableJson,
                    StringComparison.Ordinal)
                && string.Equals(envelope.Prompt, originalPrompt, StringComparison.Ordinal),
            "CharacterSkill request references are not deterministic, prompt-bound, ordered, or lossless.");
        Require(compactPrompt.Contains(
                    "Each object has only selectionId, positiveModuleIds, drawbackModuleIds, evidenceFactIds, displayName, narrativeFlavor.",
                    StringComparison.Ordinal)
                && compactPrompt.Contains("Korean <=14 characters", StringComparison.Ordinal)
                && compactPrompt.Contains("Korean one sentence <=48 characters", StringComparison.Ordinal)
                && compactPrompt.Contains("first positiveModuleIds entry", StringComparison.Ordinal)
                && compactPrompt.Contains("extra budget at least as much as all secondary effects combined",
                    StringComparison.Ordinal)
                && compactPrompt.Contains("distinct combinations across candidates",
                    StringComparison.Ordinal)
                && compactPrompt.Contains("Respect forbidden positive combinations",
                    StringComparison.Ordinal)
                && compactPrompt.Contains("Invent no events or outcomes absent from public facts.",
                    StringComparison.Ordinal)
                && compactPrompt.Contains("Include the exact character name '검증 용사'",
                    StringComparison.Ordinal)
                && compactPrompt.Contains("반드시 JSON 객체 하나만 반환한다.",
                    StringComparison.Ordinal),
            "CharacterSkill compact instructions lost output keys, limits, allocation, conflicts, identity, or unknown wording.");

        LlmStaticSchemaDefinition skillSchema = LlmStaticSchemaCatalog.Require(
            LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id);
        byte[] skillStaticSchemaBytes = skillSchema.Utf8Bytes.ToArray();
        string skillStaticSchemaHash = skillSchema.Hash;
        JObject request = JObject.Parse(Encoding.UTF8.GetString(
            DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                LocalLlmRequestProfiles.CharacterSkillModuleSelection,
                skillSchema,
                originalPrompt)));
        Require(string.Equals(
                    (string)request["messages"][1]["content"],
                    "/no_think\n" + compactPrompt,
                    StringComparison.Ordinal),
            "CharacterSkill backend bypassed the lossless compact transport.");
        JObject requestSchema = (JObject)request["response_format"]["json_schema"]["schema"];
        JObject requestProperties = (JObject)requestSchema["properties"];
        Require(((JArray)requestSchema["required"]).Values<string>()
                    .Contains("requestRef", StringComparer.Ordinal)
                && ((JArray)requestProperties["requestRef"]["enum"]).Values<string>()
                    .SequenceEqual(new[] { requestRef }, StringComparer.Ordinal),
            "CharacterSkill batch response schema did not bind the exact request reference.");
        JObject requestCandidates = (JObject)requestProperties["candidates"];
        JArray requestTuple = (JArray)requestCandidates["items"];
        JObject staticCandidateProperties = (JObject)JObject.Parse(skillSchema.Json)
            ["properties"]["candidates"]["items"]["properties"];
        JObject expectedSkillDisplayName =
            (JObject)staticCandidateProperties["displayName"].DeepClone();
        JObject expectedSkillNarrativeFlavor =
            (JObject)staticCandidateProperties["narrativeFlavor"].DeepClone();
        expectedSkillDisplayName["pattern"] =
            requestTuple[0]["properties"]["displayName"]["pattern"].DeepClone();
        expectedSkillNarrativeFlavor["pattern"] =
            requestTuple[0]["properties"]["narrativeFlavor"]["pattern"].DeepClone();
        Require((int)requestCandidates["minItems"] == 3
                && (int)requestCandidates["maxItems"] == 3
                && !(bool)requestCandidates["additionalItems"]
                && requestTuple.Count == 3,
            "CharacterSkill batch response schema is not a closed three-candidate tuple.");
        for (int index = 0; index < candidates.Count; index++)
        {
            JArray candidate = (JArray)candidates[index];
            JObject properties = (JObject)requestTuple[index]["properties"];
            string[] expectedPositiveIds = DecodeModuleIds((JArray)candidate[3]);
            string[] expectedDrawbackIds = DecodeModuleIds((JArray)candidate[4]);
            string[] expectedEvidenceReferences = ((JArray)candidate[5]).Values<int>()
                .Select(value => "E" + value)
                .ToArray();
            Require(((JArray)properties["selectionId"]["enum"]).Values<string>()
                        .SequenceEqual(new[] { "S" + index }, StringComparer.Ordinal)
                    && ((JArray)properties["positiveModuleIds"]["items"]["enum"])
                        .Values<string>().SequenceEqual(
                            expectedPositiveIds, StringComparer.Ordinal)
                    && ((JArray)properties["evidenceFactIds"]["items"]["enum"])
                        .Values<string>().SequenceEqual(
                            expectedEvidenceReferences, StringComparer.Ordinal)
                    && JToken.DeepEquals(
                        properties["displayName"], expectedSkillDisplayName)
                    && JToken.DeepEquals(
                        properties["narrativeFlavor"], expectedSkillNarrativeFlavor),
                "CharacterSkill batch response schema lost a candidate selection/evidence reference or full module offer.");
            JToken drawbackProperty = properties["drawbackModuleIds"];
            Require(expectedDrawbackIds.Length == 0
                    ? (int)drawbackProperty["maxItems"] == 0
                        && drawbackProperty["items"]["enum"] == null
                    : ((JArray)drawbackProperty["items"]["enum"]).Values<string>()
                        .SequenceEqual(expectedDrawbackIds, StringComparer.Ordinal),
                "CharacterSkill batch response schema changed a candidate drawback offer set.");
        }

        string observedContent =
            "{\"requestRef\":\"" + requestRef + "\",\"candidates\":["
            + "{\"selectionId\":\"S0\",\"positiveModuleIds\":[\"shared_module\"],"
            + "\"drawbackModuleIds\":[],\"evidenceFactIds\":[\"E0\",\"E1\"],"
            + "\"displayName\":\"검증 용사의 강타\","
            + "\"narrativeFlavor\":\"문을 연 기억이 단단한 일격으로 이어진다.\"},"
            + "{\"selectionId\":\"S1\",\"positiveModuleIds\":[\"shared_module\",\"focus_module\"],"
            + "\"drawbackModuleIds\":[\"character-skill:drawback:fatigue\"],"
            + "\"evidenceFactIds\":[\"E1\",\"E2\"],"
            + "\"displayName\":\"검증 용사의 집중\","
            + "\"narrativeFlavor\":\"제작과 전투의 기억을 엮어 흐름을 붙든다.\"},"
            + "{\"selectionId\":\"S2\",\"positiveModuleIds\":[\"guard_module\"],"
            + "\"drawbackModuleIds\":[\"character-skill:drawback:delay\"],"
            + "\"evidenceFactIds\":[\"E3\"],"
            + "\"displayName\":\"검증 용사의 수호\","
            + "\"narrativeFlavor\":\"오래된 긴장을 견디며 동료의 길을 지킨다.\"}]}";
        string observedSnapshot = observedContent;
        string expectedResolvedContent =
            "{\"candidates\":["
            + "{\"selectionId\":\"selection:skill:alpha\",\"positiveModuleIds\":[\"shared_module\"],"
            + "\"drawbackModuleIds\":[],\"evidenceFactIds\":[\"" + evidenceA + "\",\""
            + evidenceShared + "\"],\"displayName\":\"검증 용사의 강타\","
            + "\"narrativeFlavor\":\"문을 연 기억이 단단한 일격으로 이어진다.\"},"
            + "{\"selectionId\":\"selection:skill:beta\","
            + "\"positiveModuleIds\":[\"shared_module\",\"focus_module\"],"
            + "\"drawbackModuleIds\":[\"character-skill:drawback:fatigue\"],"
            + "\"evidenceFactIds\":[\"" + evidenceShared + "\",\"" + evidenceB + "\"],"
            + "\"displayName\":\"검증 용사의 집중\","
            + "\"narrativeFlavor\":\"제작과 전투의 기억을 엮어 흐름을 붙든다.\"},"
            + "{\"selectionId\":\"selection:skill:gamma\",\"positiveModuleIds\":[\"guard_module\"],"
            + "\"drawbackModuleIds\":[\"character-skill:drawback:delay\"],"
            + "\"evidenceFactIds\":[\"" + evidenceC + "\"],"
            + "\"displayName\":\"검증 용사의 수호\","
            + "\"narrativeFlavor\":\"오래된 긴장을 견디며 동료의 길을 지킨다.\"}]}";
        string directlyResolved = ResolveCharacterSkillReferences(
            originalPrompt,
            observedContent);
        bool backendResolved = DungeonStoryHostStructuredChatBackend.TryResolveModelContent(
            LocalLlmRequestProfiles.CharacterSkillModuleSelection,
            originalPrompt,
            observedContent,
            out string backendResolvedContent,
            out string backendResolveError);
        var callerResult = new LocalLlmResult(
            LocalLlmRequestStatus.Succeeded,
            backendResolvedContent,
            string.Empty,
            "skill-reference-fixture",
            rawModelContent: observedContent);
        Require(backendResolved
                && string.IsNullOrEmpty(backendResolveError)
                && string.Equals(directlyResolved, expectedResolvedContent,
                    StringComparison.Ordinal)
                && string.Equals(backendResolvedContent, expectedResolvedContent,
                    StringComparison.Ordinal)
                && string.Equals(observedContent, observedSnapshot, StringComparison.Ordinal)
                && string.Equals(callerResult.Content, expectedResolvedContent,
                    StringComparison.Ordinal)
                && string.Equals(callerResult.RawModelContent, observedContent,
                    StringComparison.Ordinal),
            "CharacterSkill request-reference resolve changed IDs, order, Korean prose, observed content, or caller raw content.");
        bool decoded = LlmJsonResponseParser.TryParse(
            LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
            backendResolvedContent,
            out CharacterSkillModuleSelectionBatchResponseDto decodedResponse,
            out string decodeError);
        var namedCandidate = new CharacterSkillModuleSelectionResponseDto
        {
            selectionId = "selection:skill:named-authority",
            positiveModuleIds = new List<string> { "guard_module" },
            drawbackModuleIds = new List<string>(),
            evidenceFactIds = new List<string> { evidenceA },
            displayName = "검증 용사 2 수호",
            narrativeFlavor = "검증 용사 2의 기억에서 익힌 수호다."
        };
        var namedBatch = new CharacterSkillModuleSelectionBatchResponseDto
        {
            candidates = new List<CharacterSkillModuleSelectionResponseDto> { namedCandidate }
        };
        CharacterSkillModuleSelectionResponseDto copiedQuantity =
            new CharacterSkillModuleSelectionResponseDto
            {
                selectionId = namedCandidate.selectionId,
                positiveModuleIds = new List<string>(namedCandidate.positiveModuleIds),
                drawbackModuleIds = new List<string>(),
                evidenceFactIds = new List<string> { evidenceA },
                displayName = namedCandidate.displayName,
                narrativeFlavor = "검증 용사 2의 지표 95.03/100%를 옮긴다."
            };
        Require(decoded && decodedResponse.candidates.Count == 3
                && namedBatch.Validate(out _, "검증 용사 2")
                && !namedBatch.Validate(out _)
                && !copiedQuantity.Validate(out _, "검증 용사 2"),
            "CharacterSkill decoder or numeric/authoritative-name prose guard changed: "
            + decodeError);

        void RequireReferenceReject(string rejectionPrompt, string rejectedContent, string message)
        {
            string rejectedSnapshot = rejectedContent;
            bool accepted = DungeonStoryHostStructuredChatBackend.TryResolveModelContent(
                LocalLlmRequestProfiles.CharacterSkillModuleSelection,
                rejectionPrompt,
                rejectedContent,
                out string rejectedResolved,
                out string rejectionError);
            Require(!accepted
                    && string.IsNullOrEmpty(rejectedResolved)
                    && !string.IsNullOrWhiteSpace(rejectionError)
                    && string.Equals(rejectedContent, rejectedSnapshot, StringComparison.Ordinal),
                message);
        }

        string wrongPrompt = originalPrompt.Replace(
            "origin=fixture",
            "origin=fixture-wrong-request");
        Require(!string.Equals(CharacterSkillRequestRef(wrongPrompt), requestRef,
                StringComparison.Ordinal),
            "CharacterSkill wrong-prompt fixture did not change its request reference.");
        RequireReferenceReject(
            wrongPrompt,
            observedContent,
            "CharacterSkill resolver accepted a response tag from another same-shape prompt.");
        RequireReferenceReject(
            originalPrompt,
            observedContent.Replace("\"E0\"", "\"unknown\""),
            "CharacterSkill resolver accepted an unknown evidence reference.");
        RequireReferenceReject(
            originalPrompt,
            observedContent.Replace("\"E0\"", "\"E4\""),
            "CharacterSkill resolver accepted an out-of-range evidence reference.");
        RequireReferenceReject(
            originalPrompt,
            observedContent.Replace(
                "\"E0\",\"E1\"",
                "\"E0\",\"" + evidenceShared + "\""),
            "CharacterSkill resolver accepted mixed reference and full evidence IDs.");
        RequireReferenceReject(
            originalPrompt,
            observedContent.Replace("\"E0\"", "\"E00\""),
            "CharacterSkill resolver accepted a leading-zero evidence reference.");
        RequireReferenceReject(
            originalPrompt,
            observedContent.Replace("\"E0\"", "0"),
            "CharacterSkill resolver accepted a non-string evidence reference.");
        RequireReferenceReject(
            originalPrompt,
            observedContent.Replace("\"E0\",\"E1\"", "\"E0\",\"E2\""),
            "CharacterSkill resolver accepted evidence outside the candidate's offers.");
        RequireReferenceReject(
            originalPrompt,
            observedContent.Replace("\"selectionId\":\"S0\"", "\"selectionId\":\"S1\""),
            "CharacterSkill resolver accepted the wrong selection order.");
        JObject shortResponse = JObject.Parse(observedContent);
        ((JArray)shortResponse["candidates"]).RemoveAt(2);
        RequireReferenceReject(
            originalPrompt,
            shortResponse.ToString(Newtonsoft.Json.Formatting.None),
            "CharacterSkill resolver accepted the wrong candidate count.");
        RequireReferenceReject(
            originalPrompt,
            observedContent.Insert(observedContent.Length - 1, ",\"extra\":true"),
            "CharacterSkill resolver accepted an extra root response key.");
        RequireReferenceReject(
            originalPrompt,
            observedContent.Insert(
                1,
                "\"requestRef\":\"" + requestRef + "\","),
            "CharacterSkill resolver accepted duplicate JSON keys.");

        NarrativePublicPromptEnvelope noBatchEnvelope = NarrativePublicPromptEnvelope.Create(
            "selectionId=selection:skill:single\ncharacter=검증 용사\n",
            material);
        string noBatchExpected = ToModelPromptWithoutCanonicalPublicPayload(
            noBatchEnvelope.Prompt);
        string noBatchActual = BuildCharacterSkillCompactPrompt(noBatchEnvelope.Prompt);
        JObject noBatchRequest = JObject.Parse(Encoding.UTF8.GetString(
            DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                LocalLlmRequestProfiles.CharacterSkillModuleSelection,
                skillSchema,
                noBatchEnvelope.Prompt)));
        JObject noBatchExpectedSchema = JObject.Parse(skillSchema.Json);
        JObject noBatchExpectedProperties = (JObject)noBatchExpectedSchema
            ["properties"]["candidates"]["items"]["properties"];
        JObject noBatchActualSchema = (JObject)noBatchRequest
            ["response_format"]["json_schema"]["schema"];
        JObject noBatchActualProperties = (JObject)noBatchActualSchema
            ["properties"]["candidates"]["items"]["properties"];
        noBatchExpectedProperties["displayName"]["pattern"] =
            noBatchActualProperties["displayName"]["pattern"].DeepClone();
        noBatchExpectedProperties["narrativeFlavor"]["pattern"] =
            noBatchActualProperties["narrativeFlavor"]["pattern"].DeepClone();
        const string NoBatchContent = "no-batch content remains untouched";
        bool noBatchResolved = DungeonStoryHostStructuredChatBackend.TryResolveModelContent(
            LocalLlmRequestProfiles.CharacterSkillModuleSelection,
            noBatchEnvelope.Prompt,
            NoBatchContent,
            out string noBatchResolvedContent,
            out string noBatchResolveError);
        Require(string.Equals(
                (string)noBatchRequest["messages"][1]["content"],
                "/no_think\n" + noBatchExpected,
                StringComparison.Ordinal)
                && string.Equals(noBatchActual, noBatchExpected, StringComparison.Ordinal)
                && !noBatchActual.Contains(ProseAuthorityInstruction,
                    StringComparison.Ordinal)
                && JToken.DeepEquals(
                    noBatchActualSchema,
                    noBatchExpectedSchema)
                && noBatchActualSchema["properties"]["requestRef"] == null
                && !((JArray)noBatchActualSchema["required"]).Values<string>()
                    .Contains("requestRef", StringComparer.Ordinal)
                && noBatchActualSchema["properties"]["candidates"]["items"] is JObject
                && noBatchActualSchema["properties"]["candidates"]["additionalItems"] == null
                && noBatchResolved
                && string.IsNullOrEmpty(noBatchResolveError)
                && string.Equals(noBatchResolvedContent, NoBatchContent,
                    StringComparison.Ordinal),
            "CharacterSkill no-batch request changed its existing validated prompt path.");

        LlmStaticSchemaDefinition otherSchema = LlmStaticSchemaCatalog.Require(
            LocalLlmRequestProfiles.CharacterRecord.Id);
        byte[] otherStaticSchemaBytes = otherSchema.Utf8Bytes.ToArray();
        JObject otherRequest = JObject.Parse(Encoding.UTF8.GetString(
            DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                "test",
                LocalLlmRequestProfiles.CharacterRecord,
                otherSchema,
                originalPrompt)));
        bool otherResolved = DungeonStoryHostStructuredChatBackend.TryResolveModelContent(
            LocalLlmRequestProfiles.CharacterRecord,
            originalPrompt,
            observedContent,
            out string otherResolvedContent,
            out string otherResolveError);
        Require(string.Equals(
                    (string)otherRequest["messages"][1]["content"],
                    NarrativeRequestContext.ToModelPrompt(originalPrompt),
                    StringComparison.Ordinal)
                && JToken.DeepEquals(
                    otherRequest["response_format"]["json_schema"]["schema"],
                    JObject.Parse(otherSchema.Json))
                && skillStaticSchemaBytes.SequenceEqual(skillSchema.Utf8Bytes)
                && string.Equals(
                    skillSchema.Hash,
                    skillStaticSchemaHash,
                    StringComparison.Ordinal)
                && otherStaticSchemaBytes.SequenceEqual(otherSchema.Utf8Bytes)
                && otherResolved
                && string.IsNullOrEmpty(otherResolveError)
                && string.Equals(otherResolvedContent, observedContent,
                    StringComparison.Ordinal)
                && string.Equals(envelope.Prompt, originalPrompt, StringComparison.Ordinal)
                && string.Equals(
                    envelope.PublicContextSemanticHash,
                    originalSemanticHash,
                    StringComparison.Ordinal)
                && string.Equals(
                    envelope.ModelInput.SemanticHash,
                    originalSemanticHash,
                    StringComparison.Ordinal),
            "CharacterSkill compact transport mutated prompt identity, static schemas, or another profile.");
    }

    private static void VerifyCharacterSkillOptionalPublicCause(
        string prototype, string rootFact, string childFact, string hiddenParentFact)
    {
        const string run = "skill-public-cause-fixture";
        string rootId = new GameplayOutcomeId(new GameplayOutcomeRunId(run), 1).ToString();
        string hiddenId = new GameplayOutcomeId(new GameplayOutcomeRunId(run), 99).ToString();
        GameplayOutcomeEvidenceBindingSnapshot Binding(string fact, long sequence, string parent) => new()
        {
            publicFactId = fact, outcomeRunId = run, outcomeSequence = sequence,
            outcomeTypeId = "combat.hit", subjectKindId = "character", subjectId = "hero",
            firstDay = 1, lastDay = 2, roleIds = new List<string> { "actor" },
            parentOutcomeId = parent, canonicalFactText = "검증 사실; cause=문장 안의 문자",
            metrics = new List<GameplayOutcomeEvidenceMetricBindingSnapshot>
            {
                new() { metricId = "damage", value = 12.5, unitId = "hp" }
            }
        };
        var bindings = new[] { Binding(rootFact, 1, string.Empty),
            Binding(childFact, 2, rootId), Binding(hiddenParentFact, 3, hiddenId) };
        string before = Newtonsoft.Json.JsonConvert.SerializeObject(bindings);
        var facts = new StringBuilder();
        GameplayOutcomeEvidenceFormulaProjection.AppendPromptFacts(facts, bindings);
        string projected = facts.ToString().Replace("\r\n", "\n");
        Require(!projected.Contains(rootId, StringComparison.Ordinal)
            && !projected.Contains(hiddenId, StringComparison.Ordinal)
            && projected.Contains("; cause=" + rootFact + "; fact=", StringComparison.Ordinal),
            "Skill producer leaked internal ancestry or lost a visible public parent.");
        string packet = prototype.Substring(0, prototype.IndexOf("authoritativeOutcomeEvidence:\n",
            StringComparison.Ordinal)) + projected;
        JObject table = CaptureCharacterSkillCompactPacket(packet);
        JArray outcomes = (JArray)table["O"];
        Require(outcomes.Count == 3 && outcomes[0][8].Type == JTokenType.Null
            && (string)outcomes[1][8] == rootFact && outcomes[2][8].Type == JTokenType.Null,
            "Absent public cause was invented or confused with prose text.");
        JObject expanded = ExpandCharacterSkillTable(CompactCharacterSkillTable(table));
        const string requestRef = "optional-public-cause-test";
        JObject referenced = ExpandReferencedCharacterSkillTable(
            ReferenceCharacterSkillTable(table, requestRef), table, requestRef);
        Require(RestoreCharacterSkillCompactPacket(table) == packet
            && RestoreCharacterSkillCompactPacket(expanded) == packet
            && RestoreCharacterSkillCompactPacket(referenced) == packet
            && JToken.DeepEquals(table, expanded) && JToken.DeepEquals(table, referenced)
            && Newtonsoft.Json.JsonConvert.SerializeObject(bindings) == before,
            "Public cause transport changed source bindings, null presence or packet bytes.");
        const string experience = "가벼운 경험 / 판단 근거: 실제 기분 변화.";
        string assessed = packet.Replace("; fact=", "; experience=" + experience + "; fact=");
        JObject assessedTable = CaptureCharacterSkillCompactPacket(assessed);
        JObject assessedExpanded = ExpandCharacterSkillTable(CompactCharacterSkillTable(assessedTable));
        Require(((JArray)assessedTable["O"]).OfType<JArray>().All(row => (string)row[10] == experience)
            && RestoreCharacterSkillCompactPacket(assessedTable) == assessed
            && RestoreCharacterSkillCompactPacket(assessedExpanded) == assessed
            && JToken.DeepEquals(assessedTable, assessedExpanded),
            "Experience assessment corrupted metrics, public cause or exact packet round trip.");
        foreach (string invalid in new[] { packet.Replace("; metrics=", "; missingMetrics="),
            packet.Replace("; fact=", "; missingFact="),
            assessed.Replace(experience, string.Empty),
            assessed.Replace("damage=12.5 hp", "damage=NaN hp") })
        {
            bool rejected = false;
            try { CaptureCharacterSkillCompactPacket(invalid); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Optional cause made a required outcome field optional.");
        }
        Debug.Log("SKILL_OPTIONAL_PUBLIC_CAUSE_TRANSPORT=PASS; producer=actual; absence=2; public-parent=1; exact-projections=3; experience-roundtrip=1; invalid-input-rejections=4; source-unchanged=1");
    }

    private static JObject CaptureCharacterSkillCompactPacket(string packet) =>
        (JObject)InvokeNonPublicStatic(
            RequireCharacterSkillCompactModelPromptType(),
            "CapturePacket",
            new[] { typeof(string) },
            new object[] { packet });

    private static string RestoreCharacterSkillCompactPacket(JObject table) =>
        (string)InvokeNonPublicStatic(
            RequireCharacterSkillCompactModelPromptType(),
            "RestorePacket",
            new[] { typeof(JObject) },
            new object[] { table });

    private static JObject CompactCharacterSkillTable(JObject table) =>
        (JObject)InvokeNonPublicStatic(
            RequireCharacterSkillCompactModelPromptType(),
            "CompactTable",
            new[] { typeof(JObject) },
            new object[] { table });

    private static JObject ExpandCharacterSkillTable(JObject table) =>
        (JObject)InvokeNonPublicStatic(
            RequireCharacterSkillCompactModelPromptType(),
            "ExpandTable",
            new[] { typeof(JObject) },
            new object[] { table });

    private static string CharacterSkillRequestRef(string prompt) =>
        (string)InvokeNonPublicStatic(
            RequireCharacterSkillCompactModelPromptType(),
            "RequestRef",
            new[] { typeof(string) },
            new object[] { prompt });

    private static JObject ReferenceCharacterSkillTable(
        JObject table,
        string requestRef) =>
        (JObject)InvokeNonPublicStatic(
            RequireCharacterSkillCompactModelPromptType(),
            "ReferencedTable",
            new[] { typeof(JObject), typeof(string) },
            new object[] { table, requestRef });

    private static JObject ExpandReferencedCharacterSkillTable(
        JObject modelTable,
        JObject sourceTable,
        string requestRef) =>
        (JObject)InvokeNonPublicStatic(
            RequireCharacterSkillCompactModelPromptType(),
            "ExpandReferencedTable",
            new[] { typeof(JObject), typeof(JObject), typeof(string) },
            new object[] { modelTable, sourceTable, requestRef });

    private static string ResolveCharacterSkillReferences(
        string prompt,
        string content) =>
        (string)InvokeNonPublicStatic(
            RequireCharacterSkillCompactModelPromptType(),
            "Resolve",
            new[] { typeof(string), typeof(string) },
            new object[] { prompt, content });

    private static string CompactCharacterSkillLocalFactLines(
        string suffix,
        out JArray catalog)
    {
        object[] arguments = { suffix, null };
        string result = (string)InvokeNonPublicStatic(
            RequireCharacterSkillCompactModelPromptType(),
            "CompactLocalFactLines",
            new[] { typeof(string), typeof(JArray).MakeByRefType() },
            arguments);
        catalog = (JArray)arguments[1];
        return result;
    }

    private static string RestoreCharacterSkillLocalFactLines(
        string suffix,
        JArray catalog) =>
        (string)InvokeNonPublicStatic(
            RequireCharacterSkillCompactModelPromptType(),
            "RestoreLocalFactLines",
            new[] { typeof(string), typeof(JArray) },
            new object[] { suffix, catalog });

    private static string BuildCharacterSkillCompactPrompt(string prompt) =>
        (string)InvokeNonPublicStatic(
            RequireCharacterSkillCompactModelPromptType(),
            "Build",
            new[] { typeof(string) },
            new object[] { prompt });

    private static string ToModelPromptWithoutCanonicalPublicPayload(string prompt) =>
        (string)InvokeNonPublicStatic(
            typeof(NarrativeRequestContext),
            "ToModelPromptWithoutCanonicalPublicPayload",
            new[] { typeof(string) },
            new object[] { prompt });

    private static string ToCompactPublicModelPromptForTest(string prompt) =>
        (string)InvokeNonPublicStatic(
            typeof(NarrativeRequestContext),
            "ToCompactPublicModelPrompt",
            new[] { typeof(string) },
            new object[] { prompt });

    private static string ToCompactTraitModelPromptForTest(string prompt) =>
        (string)InvokeNonPublicStatic(
            typeof(NarrativeRequestContext),
            "ToCompactTraitModelPrompt",
            new[] { typeof(string) },
            new object[] { prompt });

    private static string CompactTraitDomainEvidenceGroupsForTest(string modelPrompt) =>
        (string)InvokeNonPublicStatic(
            typeof(NarrativeRequestContext),
            "CompactTraitDomainEvidenceGroups",
            new[] { typeof(string) },
            new object[] { modelPrompt });

    private static string ExpandTraitDomainEvidenceGroupsForTest(string encoded) =>
        (string)InvokeNonPublicStatic(
            typeof(NarrativeRequestContext),
            "ExpandTraitDomainEvidenceGroups",
            new[] { typeof(string) },
            new object[] { encoded });

    private static JObject TraitEvidenceReferencesForTest(string prompt) =>
        (JObject)InvokeNonPublicStatic(
            typeof(NarrativeRequestContext),
            "TraitEvidenceReferences",
            new[] { typeof(string) },
            new object[] { prompt });

    private static string ExpandTraitEvidenceReferencesForTest(string encoded) =>
        (string)InvokeNonPublicStatic(
            typeof(NarrativeRequestContext),
            "ExpandTraitEvidenceReferences",
            new[] { typeof(string) },
            new object[] { encoded });

    private static string ResolveTraitEvidenceReferencesForTest(
        string prompt,
        string content) =>
        (string)InvokeNonPublicStatic(
            typeof(NarrativeRequestContext),
            "ResolveTraitEvidenceReferences",
            new[] { typeof(string), typeof(string) },
            new object[] { prompt, content });

    private static string ToCompactEvolutionModelPromptForTest(string prompt) =>
        (string)InvokeNonPublicStatic(
            typeof(NarrativeRequestContext),
            "ToCompactEvolutionModelPrompt",
            new[] { typeof(string) },
            new object[] { prompt });

    private static JObject CompactRepeatedPublicTextForTest(JObject payload) =>
        (JObject)InvokeNonPublicStatic(
            typeof(NarrativeRequestContext),
            "CompactRepeatedPublicText",
            new[] { typeof(JObject) },
            new object[] { payload });

    private static JObject ExpandCompactPublicTextForTest(JObject encoded) =>
        (JObject)InvokeNonPublicStatic(
            typeof(NarrativeRequestContext),
            "ExpandCompactPublicText",
            new[] { typeof(JObject) },
            new object[] { encoded });

    private static Type RequireCharacterSkillCompactModelPromptType() =>
        typeof(DungeonStoryHostStructuredChatBackend).Assembly.GetType(
            "CharacterSkillCompactModelPrompt",
            throwOnError: true);

    private static object InvokeNonPublicStatic(
        Type type,
        string methodName,
        Type[] parameterTypes,
        object[] arguments)
    {
        var method = type.GetMethod(
            methodName,
            System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        Require(method != null,
            "Missing nonpublic static test target '" + type.FullName + "." + methodName + "'.");
        try
        {
            return method.Invoke(null, arguments);
        }
        catch (System.Reflection.TargetInvocationException exception)
        {
            if (exception.InnerException == null) throw;
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static void VerifyAcquiredTraitExactResponseContract()
    {
        const string valid =
            "{\"presentationId\":\"presentation:trait:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\","
            + "\"displayName\":\"전장의 담력\","
            + "\"narrativeFlavor\":\"위기를 거듭 넘기며 침착함을 익혔다.\"}";
        Require(
            NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.AcquiredTrait.Id,
                valid,
                out _,
                out string exactError),
            "valid AcquiredTrait exact-key response failed: " + exactError);
        Require(
            LlmJsonResponseParser.TryParse(
                LocalLlmRequestProfiles.AcquiredTrait.Id,
                valid,
                out CharacterAcquiredTraitFormulaPresentationDto parsed,
                out string parseError)
            && string.Equals(parsed.displayName, "전장의 담력", StringComparison.Ordinal),
            "valid AcquiredTrait payload failed to parse: " + parseError);

        Require(
            !NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.AcquiredTrait.Id,
                valid.Insert(valid.Length - 1, ",\"combatMultiplier\":1.04"),
                out _,
                out _),
            "AcquiredTrait accepted a model-owned mechanical field.");
        Require(
            !NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.AcquiredTrait.Id,
                "{\"presentationId\":\"presentation:trait:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\","
                + "\"displayName\":\"전장의 담력\","
                + "\"description\":\"설명\"}",
                out _,
                out _),
            "AcquiredTrait accepted legacy mechanic fields or a missing narrativeFlavor key.");
        Require(
            !NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.AcquiredTrait.Id,
                valid.Replace(
                    "\"displayName\":\"전장의 담력\"",
                    "\"displayName\":\"전장의 담력\",\"displayName\":\"중복\""),
                out _,
                out _),
            "AcquiredTrait accepted a duplicate response key.");

        const string moduleSelection =
            "{\"selectionId\":\"selection:trait:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"," 
            + "\"positiveModuleIds\":[\"acquired-trait:module:battle-temper\"],"
            + "\"drawbackModuleIds\":[],"
            + "\"evidenceFactIds\":[\"public-fact:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"],"
            + "\"displayName\":\"전장의 담력\","
            + "\"narrativeFlavor\":\"위기를 넘기며 침착함을 익혔다.\"}";
        Require(
            NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id,
                moduleSelection, out _, out string selectionError),
            "valid AcquiredTraitModuleSelection response failed: " + selectionError);
        Require(
            LlmJsonResponseParser.TryParse(
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id,
                moduleSelection,
                out CharacterAcquiredTraitModuleSelectionResponseDto selection,
                out string selectionParseError)
            && selection.positiveModuleIds.Count == 1,
            "valid AcquiredTraitModuleSelection payload failed to parse: " + selectionParseError);
        Require(
            !NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id,
                moduleSelection.Insert(moduleSelection.Length - 1, ",\"magnitude\":99"),
                out _, out _),
            "AcquiredTraitModuleSelection accepted a model-owned numeric field.");
    }

    private static void VerifyChoiceCanonicalizationAndGrammar()
    {
        string[] suffixes = { string.Empty, " ", "\t", "\n", " \t\r\n" };
        foreach (string suffix in suffixes)
        {
            Require(ChoicePromptCanonicalizer.TryCanonicalize(
                    "후보 0과 1 중 선택" + suffix,
                    out ChoicePromptDiagnostic diagnostic,
                    out string error),
                "choice canonicalization failed: " + error);
            Require(diagnostic.Prompt.EndsWith(
                    ChoicePromptCanonicalizer.FinalMarker,
                    StringComparison.Ordinal),
                "choice prompt marker mismatch");
            Require(!char.IsWhiteSpace(diagnostic.Prompt[diagnostic.Prompt.Length - 1]),
                "choice prompt retained trailing whitespace");
        }

        Require(EquipmentChoiceGrammarCatalog.Require(2) == EquipmentChoiceGrammarCatalog.Choice2,
            "choice-2 grammar is not static");
        Require(EquipmentChoiceGrammarCatalog.Require(3) == EquipmentChoiceGrammarCatalog.Choice3,
            "choice-3 grammar is not static");
        Require(EquipmentChoiceResultParser.TryParse(
                " { \"selectedIndex\" : 0 } ",
                2,
                out int zero)
            && zero == 0,
            "whitespace-tolerant JSON choice failed");
        Require(EquipmentChoiceResultParser.TryParse(
                "\n{\"selectedIndex\":2}\r\n",
                3,
                out int two)
            && two == 2,
            "leading-newline JSON choice failed");
        Require(!EquipmentChoiceResultParser.TryParse("{\"selectedIndex\":3}", 3, out _),
            "out-of-range choice passed");
        Require(!EquipmentChoiceResultParser.TryParse("0", 3, out _),
            "legacy raw-digit choice passed");
        Require(!EquipmentChoiceResultParser.TryParse("{\"choice\":0}", 3, out _),
            "wrong-key JSON choice passed");
        Require(!EquipmentChoiceResultParser.TryParse(
                "{\"selectedIndex\":0,\"confidence\":1}",
                3,
                out _),
            "extra EquipmentChoice key passed");
        Require(!EquipmentChoiceResultParser.TryParse(
                "{\"selectedIndex\":0,\"selectedIndex\":1}",
                3,
                out _),
            "duplicate EquipmentChoice key passed");
        Require(!EquipmentChoiceResultParser.TryParse("{\"selectedIndex\":\"0\"}", 3, out _),
            "string EquipmentChoice index passed");
        Require(!EquipmentChoiceResultParser.TryParse(" ", 3, out _),
            "whitespace-only choice passed");

        Require(NarrativeExactKeyContract.TryValidateProfileResponse(
                "Persona",
                "{\"personaName\":\"신중한 손님\",\"flavorText\":\"조용한 방을 선호한다.\"}",
                out _,
                out string personaError),
            "valid Persona exact-key response failed: " + personaError);
        Require(!NarrativeExactKeyContract.TryValidateProfileResponse(
                "Persona",
                "{\"personaName\":\"신중한 손님\",\"flavorText\":\"조용하다.\",\"selfCareMultiplier\":1.2}",
                out _,
                out _),
            "Persona accepted a model-owned mechanical multiplier.");
        Require(!NarrativeExactKeyContract.TryValidateProfileResponse(
                "Persona",
                "{\"personaName\":\"신중한 손님\",\"flavorText\":\"조용하다.\",\"facilityTags\":[\"quiet\"]}",
                out _,
                out _),
            "Persona accepted a model-owned facility tag.");
    }

    private static void VerifyContextAwareScheduling()
    {
        PrefixAffinityKey affinityA = new PrefixAffinityKey("schema", "A", "facts-a", 1, 1);
        PrefixAffinityKey affinityB = new PrefixAffinityKey("schema", "B", "facts-b", 1, 1);
        SchedulerFixture coalesced = new SchedulerFixture(
            1, 0f, affinityA, persistent: true, urgent: false, expiresAt: float.PositiveInfinity);
        Require(!ContextAwareLlmScheduler.CanDispatch(coalesced, 0.05f, 1),
            "persistent request skipped the general coalescing window");
        Require(ContextAwareLlmScheduler.CanDispatch(coalesced, 0.08f, 1),
            "coalescing window did not release a persistent request");
        List<SchedulerFixture> queue = new List<SchedulerFixture>
        {
            new SchedulerFixture(10, 0f, affinityB, persistent: false, urgent: false, expiresAt: 20f),
            new SchedulerFixture(10, 0.01f, affinityA, persistent: false, urgent: false, expiresAt: 20f),
            new SchedulerFixture(1, 0.02f, affinityB, persistent: true, urgent: false, expiresAt: float.PositiveInfinity)
        };
        int persistent = ContextAwareLlmScheduler.FindNext(queue, 0.1f, affinityA, 0);
        Require(persistent == 2, "persistent narrative did not outrank affinity");

        queue.RemoveAt(2);
        int affinity = ContextAwareLlmScheduler.FindNext(queue, 0.1f, affinityA, 0);
        Require(affinity == 1, "matching prefix affinity was not grouped");

        int burstLimited = ContextAwareLlmScheduler.FindNext(
            queue,
            0.7f,
            affinityA,
            ContextAwareLlmScheduler.MaximumAffinityBurst);
        Require(burstLimited == 0, "affinity burst cap did not release an aged request");

        queue.Add(new SchedulerFixture(
            0,
            0.69f,
            affinityB,
            persistent: false,
            urgent: true,
            expiresAt: 0.8f));
        int deadline = ContextAwareLlmScheduler.FindNext(queue, 0.7f, affinityA, 0);
        Require(deadline == 2, "deadline-imminent request did not override affinity");
    }

    private static void VerifyTypedEquipmentEvidence()
    {
        UsageLedger ledger = new UsageLedger();
        UsageLedgerCompactor compactor = new UsageLedgerCompactor();
        compactor.Record(
            ledger,
            "combat:hit",
            12f,
            "character:archer",
            "equipment:test",
            new[] { "ranged" },
            historicalEvidenceKind: HistoricalEvidenceKind.RepeatedLongRangeHit,
            outcomeId: "hit",
            generation: 2,
            repeatCount: 4);
        compactor.Record(
            ledger,
            "combat:block",
            8f,
            "character:guardian",
            "equipment:test",
            new[] { "shield" },
            historicalEvidenceKind: HistoricalEvidenceKind.ProtectedOwner,
            outcomeId: "blocked",
            generation: 2);

        List<string> candidates = EquipmentEvolutionRules
            .BuildLegalHistoricalEffectCandidates(ledger);
        Require(candidates.Count is >= 2 and <= 3, "typed evidence did not create 2-3 legal candidates");
        Require(candidates[0] == "equipment:cadence", "strongest typed evidence did not rank first");

        CompactedHistorySegment segment = compactor.CloseGeneration(ledger, 2);
        Require(segment.historicalEvidence.Any(entry =>
                entry.kind == HistoricalEvidenceKind.RepeatedLongRangeHit
                && entry.occurrences == 4),
            "typed evidence was not compacted with repeat count");
        UsageLedgerEvent currentGenerationEvent = compactor.Record(
            ledger,
            "combat:after-reforge",
            3f,
            "character:archer",
            "equipment:test",
            new[] { "ranged" },
            historicalEvidenceKind: HistoricalEvidenceKind.RepeatedLongRangeHit,
            outcomeId: "hit",
            generation: 3);
        Require((segment.keyEvents ?? new List<UsageLedgerEvent>()).All(entry => !string.Equals(
                    entry.evidenceId,
                    currentGenerationEvent.evidenceId,
                    StringComparison.Ordinal))
                && (segment.keyEvents ?? new List<UsageLedgerEvent>())
                    .Concat(ledger.currentGenerationEvents ?? new List<UsageLedgerEvent>())
                    .Select(entry => entry.evidenceId)
                    .Distinct(StringComparer.Ordinal)
                    .Count()
                == (segment.keyEvents?.Count ?? 0)
                    + (ledger.currentGenerationEvents?.Count ?? 0),
            "Prior and current generations reused a ledger evidence ID.");
    }

    private static void VerifyEquipmentChoiceFailureAudits()
    {
        VerifyEquipmentChoiceFailure(
            EquipmentChoiceStubMode.RuntimeUnavailable,
            EquipmentChoiceFailureKind.RuntimeUnavailable);
        VerifyEquipmentChoiceFailure(
            EquipmentChoiceStubMode.RequestRejected,
            EquipmentChoiceFailureKind.RequestRejected);
        VerifyEquipmentChoiceFailure(
            EquipmentChoiceStubMode.AsyncInvalid,
            EquipmentChoiceFailureKind.AsyncResultInvalid);
        VerifyEquipmentChoiceFailure(
            EquipmentChoiceStubMode.OutOfRange,
            EquipmentChoiceFailureKind.SelectedIndexOutOfRange);
    }

    private static void VerifyEquipmentChoiceFailure(
        EquipmentChoiceStubMode mode,
        EquipmentChoiceFailureKind expectedFailure)
    {
        using EquipmentChoiceParticipantFixture participant =
            new EquipmentChoiceParticipantFixture();
        IGameContentCatalog gameContent = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        WorldItemRepository repository = new WorldItemRepository(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        CombatEquipmentRuntime equipment = CombatEquipmentEditorTestFactory.Create(
            new ResourceCombatEquipmentCatalog(gameContent),
            repository,
            new CharacterCarryInventoryRegistry(),
            researchProvider: EditorAllResearchRuntimeProvider.Instance,
            materialCatalog: EmptyResourceEconomyContentCatalog.Instance,
            evolutionModules: EmptyEvolutionModuleRegistry.Instance,
            moduleCatalog: EmptyEquipmentModuleCatalog.Instance,
            itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance,
            calendar: new EditorFixedGameCalendar(137, 9));
        CombatEquipmentInstance instance = equipment.CreateInstance(
            "weapon:shortbow",
            CombatEquipmentQuality.Normal);
        Require(instance != null, "Could not create equipment choice test instance.");

        string nodeId = "equipment-choice-node:" + mode;
        List<string> candidates = new List<string>
        {
            "equipment:cadence",
            "equipment:durability"
        };
        EvolutionNode seededNode = new EvolutionNode
        {
            nodeId = nodeId,
            effectId = string.Empty,
            historical = true,
            mechanicallyUnlocked = false,
            narrativeReady = false,
            legalCandidateEffectIds = new List<string>(candidates),
            selectedCandidateIndex = -1
        };
        UsageLedger requestLedger = new UsageLedger();
        new UsageLedgerCompactor().Record(
            requestLedger,
            "combat:hit",
            1f,
            participant.ActorId,
            instance.instanceId,
            new[] { "fallback-audit" },
            historicalEvidenceKind: HistoricalEvidenceKind.RepeatedLongRangeHit,
            outcomeId: "hit");
        EvolutionNarrativeRequestSnapshot frozenRequest =
            EvolutionNarrativeRequestFactory.Create(
                EvolutionNarrativeTargetKind.Equipment,
                instance.instanceId,
                seededNode,
                "history:" + mode,
                requestLedger,
                effectBudget: 1);
        string requestKey = frozenRequest.requestKey;
        EquipmentEvolutionState state = new EquipmentEvolutionState
        {
            evolutionNodes = new List<EvolutionNode>
            {
                seededNode
            },
            narrativeRequests = new List<EvolutionNarrativeRequestSnapshot>
            {
                frozenRequest
            }
        };
        Require(equipment.TryUpdateEvolutionState(instance.instanceId, state),
            "Could not seed an equipment choice request.");

        ILocalLlmRuntimeProvider provider = mode == EquipmentChoiceStubMode.RuntimeUnavailable
            ? new MissingEquipmentChoiceRuntimeProvider()
            : new FixedEquipmentChoiceRuntimeProvider(new EquipmentChoiceRuntimeStub(mode));
        EvolutionHistoryNarrativeRuntime runtime = new EvolutionHistoryNarrativeRuntime(
            participant.World,
            equipment,
            new FacilityEvolutionStateComponentFactory(),
            provider,
            CharacterAiEditorTestDependencies.UiClock);
        runtime.Tick();

        Require(equipment.TryGetInstance(instance.instanceId, out CombatEquipmentInstance updated),
            "Equipment choice test instance disappeared.");
        EvolutionNode node = updated.evolution.evolutionNodes.Single(candidate =>
            string.Equals(candidate.nodeId, nodeId, StringComparison.Ordinal));
        Require(node.selectedCandidateIndex == -1
                && string.IsNullOrEmpty(node.effectId)
                && !node.mechanicallyUnlocked
                && !node.equipmentChoiceAuditRecorded
                && !node.equipmentChoiceFallbackUsed,
            $"{mode} mutated the equipment node after a failed legacy choice.");
        bool auditFound = runtime.TryGetEquipmentChoiceAudit(
            requestKey,
            out NarrativeInferenceAuditRecord audit);
        Require(auditFound
                && !audit.Succeeded
                && !audit.FallbackUsed
                && audit.ValidationError.StartsWith(expectedFailure.ToString(), StringComparison.Ordinal)
                && string.IsNullOrEmpty(audit.FallbackReason)
                && string.IsNullOrEmpty(audit.SelectedId)
                && audit.SelectedIndex == -1,
            $"{mode} did not publish a matching failed-choice audit record: "
            + $"found={auditFound}, succeeded={audit.Succeeded}, fallback={audit.FallbackUsed}, "
            + $"error='{audit.ValidationError}', fallbackReason='{audit.FallbackReason}', "
            + $"selectedId='{audit.SelectedId}', selectedIndex={audit.SelectedIndex}.");
    }

    private static void VerifyMechanicalNarrativeSeparation()
    {
        EvolutionNode node = new EvolutionNode
        {
            historical = true,
            mechanicallyUnlocked = true,
            narrativeReady = false,
            uiVisible = true,
            playerVisible = true,
            effectId = "equipment:durability",
            displayName = "버텨 낸 흔적 1단계"
        };
        Require(node.mechanicallyUnlocked && !node.narrativeReady && node.uiVisible,
            "historical mechanics are still gated by narrative readiness");
        EvolutionNode clone = node.Clone();
        Require(clone.mechanicallyUnlocked && !clone.narrativeReady && clone.uiVisible,
            "V25 authority split did not survive cloning");
    }

    private static void VerifyMultiPerspectiveIdentity()
    {
        NarrativeMultiPerspectiveRequest request = new NarrativeMultiPerspectiveRequest
        {
            sharedFactPacket = "event facts",
            viewpoints = new List<NarrativeViewpointRequest>
            {
                new NarrativeViewpointRequest
                {
                    eventId = "event:test",
                    viewpointCharacterId = "character:a",
                    knowledgeSnapshotHash = "knowledge:a",
                    knowledgeSnapshotVersion = 3,
                    cultureStyleId = "culture:orc",
                    modelVersion = "v25"
                },
                new NarrativeViewpointRequest
                {
                    eventId = "event:test",
                    viewpointCharacterId = "character:b",
                    knowledgeSnapshotHash = "knowledge:b",
                    knowledgeSnapshotVersion = 3,
                    cultureStyleId = "culture:orc",
                    modelVersion = "v25"
                }
            }
        };
        Require(request.TryValidate(out string error), error);
        Require(LlmStaticSchemaCatalog.Require("MultiPerspective").PersistentNarrative,
            "multi-perspective schema must be persistent");

        NarrativeMultiPerspectiveOutput output = new NarrativeMultiPerspectiveOutput
        {
            eventId = "event:test",
            perspectives = new List<NarrativePerspectiveOutput>
            {
                new NarrativePerspectiveOutput
                    { viewpointCharacterId = "character:a", line = "첫 시점" },
                new NarrativePerspectiveOutput
                    { viewpointCharacterId = "character:b", line = "둘째 시점" }
            }
        };
        Require(output.Matches(request), "viewpoint outputs were not bound to character identities");
        output.perspectives[1].viewpointCharacterId = "character:a";
        Require(!output.Matches(request), "duplicate viewpoint character was accepted");
    }

    private static void VerifyMissingHostFailsClosed()
    {
        string impossibleRoot = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "DungeonStoryV25Missing-" + Guid.NewGuid().ToString("N"));
        bool started = DungeonStoryLlmHostProcess.TryStart(
            impossibleRoot,
            out DungeonStoryLlmHostProcess host,
            out string error);
        host?.Dispose();
        Require(!started && host == null, "missing host artifacts did not fail closed");
        Require(error.IndexOf("manifest", StringComparison.OrdinalIgnoreCase) >= 0,
            "missing host diagnostic did not identify the manifest boundary");
    }

    private static void VerifyCorruptModelFailsClosed()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "DungeonStoryV25Corrupt-" + Guid.NewGuid().ToString("N"));
        string bundle = Path.Combine(root, "DungeonStoryLlm");
        Directory.CreateDirectory(bundle);
        string hostPath = Path.Combine(bundle, "DungeonStoryLlmHost.exe");
        string modelPath = Path.Combine(bundle, "DungeonStory-Qwen3-1.7B-Q4_K_M.gguf");
        File.WriteAllBytes(hostPath, new byte[] { 1, 2, 3, 4 });
        File.WriteAllBytes(modelPath, Encoding.ASCII.GetBytes("GGUF-corrupt"));
        DungeonStoryLlmHostManifest manifest = new DungeonStoryLlmHostManifest
        {
            hostKind = "LlamaCppServer",
            hostWindows = Path.GetFileName(hostPath),
            hostWindowsSha256 = Sha256(hostPath),
            modelFile = Path.GetFileName(modelPath),
            modelSha256 = new string('0', 64),
            supportFiles = Array.Empty<DungeonStoryLlmHostSupportFile>()
        };
        File.WriteAllText(
            Path.Combine(bundle, "manifest.json"),
            JsonUtility.ToJson(manifest),
            Encoding.UTF8);
        try
        {
            bool started = DungeonStoryLlmHostProcess.TryStart(
                root,
                out DungeonStoryLlmHostProcess host,
                out string error);
            host?.Dispose();
            Require(!started && host == null, "corrupt model unexpectedly started");
            Require(error.IndexOf("model", StringComparison.OrdinalIgnoreCase) >= 0,
                "corrupt model diagnostic did not identify the model boundary");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void VerifyBundledLlamaCppBackendContract()
    {
        DungeonStoryHostStructuredChatBackend backend =
            new DungeonStoryHostStructuredChatBackend(() => "test-token");
        Require(
            backend.ResolveEndpoint("http://127.0.0.1:8080")
                .EndsWith("/v1/chat/completions", StringComparison.Ordinal),
            "bundled backend did not resolve llama.cpp chat completions");
        LlmStaticSchemaDefinition schema = LlmStaticSchemaCatalog.Require("CharacterRecord");
        using UnityEngine.Networking.UnityWebRequest request = backend.BuildRequest(
            "http://127.0.0.1:8080",
            "DungeonStory-Qwen3-1.7B-Q4_K_M",
            LocalLlmRequestProfiles.CharacterRecord,
            schema,
            "test prompt");
        string payload = Encoding.UTF8.GetString(request.uploadHandler.data);
        Require(payload.IndexOf("\"response_format\":{\"type\":\"json_schema\"",
                    StringComparison.Ordinal) >= 0,
            "bundled backend did not pass the static JSON schema to llama.cpp");
        Require(payload.IndexOf("\"enable_thinking\":false", StringComparison.Ordinal) >= 0,
            "bundled backend did not disable thinking in the Qwen chat template");
        Require(string.Equals(
                request.GetRequestHeader("Authorization"),
                "Bearer test-token",
                StringComparison.Ordinal),
            "bundled backend omitted loopback authentication");
    }

    private static void VerifyCanonicalCatalogJsonParity()
    {
        NarrativeMechanicCatalogCanonicalJsonObject fixture =
            NarrativeMechanicCatalogCanonicalJson.Object(
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "z",
                    NarrativeMechanicCatalogCanonicalJson.Integer(1)),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "\uE000",
                    NarrativeMechanicCatalogCanonicalJson.String("bmp")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "😀",
                    NarrativeMechanicCatalogCanonicalJson.String("supplementary")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "한글",
                    NarrativeMechanicCatalogCanonicalJson.String("값")),
                NarrativeMechanicCatalogCanonicalJson.Property(
                    "nested",
                    NarrativeMechanicCatalogCanonicalJson.Object(
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "b",
                            NarrativeMechanicCatalogCanonicalJson.Boolean(true)),
                        NarrativeMechanicCatalogCanonicalJson.Property(
                            "a",
                            NarrativeMechanicCatalogCanonicalJson.String("line\n\"")))));
        byte[] bytes = fixture.ToCanonicalUtf8();
        string canonical = Encoding.UTF8.GetString(bytes);
        const string PythonCanonical =
            "{\"nested\":{\"a\":\"line\\n\\\"\",\"b\":true},\"z\":1,\"한글\":\"값\",\"\":\"bmp\",\"😀\":\"supplementary\"}";
        Require(string.Equals(canonical, PythonCanonical, StringComparison.Ordinal),
            "C# canonical JSON bytes differ from Python json.dumps(sort_keys=True, ensure_ascii=False, separators=(',', ':')).");
        Require(string.Equals(
                NarrativeMechanicCatalogCanonicalJson.Sha256Prefixed(bytes),
                "sha256:bddd83f5e2ea2f19530c142ef3db9842832fdbd2a8f077b409ade64161febf27",
                StringComparison.Ordinal),
            "C# canonical JSON hash differs from the pinned Python fixture hash.");
        Require(bytes.Length >= 3
                && !(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF),
            "Canonical JSON unexpectedly contains a UTF-8 BOM.");

        bool rejectedMalformedUtf16 = false;
        try
        {
            NarrativeMechanicCatalogCanonicalJson.String("\uD800").ToCanonicalUtf8();
        }
        catch (ArgumentException)
        {
            rejectedMalformedUtf16 = true;
        }
        Require(rejectedMalformedUtf16,
            "Canonical JSON silently replaced an unpaired UTF-16 surrogate.");
    }

    private static void VerifyCharacterSkillPublicSemanticsContract()
    {
        NarrativeMechanicCatalogSnapshot catalogSnapshot =
            new NarrativeMechanicCatalogUnityAssetSource().CaptureSnapshot();
        NarrativeMechanicCatalogCanonicalJsonObject coverage = RequireCanonicalObject(
            catalogSnapshot.CharacterSkill,
            "semanticsCoverage");
        Require(coverage.Properties.Count == 6
                && RequireCanonicalProperty<NarrativeMechanicCatalogCanonicalJsonInt64>(
                    coverage,
                    "schemaVersion").Value == CharacterSkillCombinationSemanticsFactory.SchemaVersion
                && string.Equals(
                    RequireCanonicalString(coverage, "coveragePolicy"),
                    CharacterSkillCombinationSemanticsFactory.CatalogCoveragePolicy,
                    StringComparison.Ordinal),
            "The base CharacterSkill catalog semantics coverage contract is missing or changed.");
        NarrativeMechanicCatalogCanonicalJsonArray coverageEntries =
            RequireCanonicalArray(coverage, "entries");
        Require(RequireCanonicalProperty<NarrativeMechanicCatalogCanonicalJsonInt64>(
                    coverage,
                    "entryCount").Value == coverageEntries.Values.Count
                && RequireCanonicalProperty<NarrativeMechanicCatalogCanonicalJsonInt64>(
                    coverage,
                    "moduleCount").Value == 24
                && RequireCanonicalProperty<NarrativeMechanicCatalogCanonicalJsonInt64>(
                    coverage,
                    "variantCount").Value == 50,
            "The base CharacterSkill catalog does not cover all 24 modules and 50 variants.");

        HashSet<string> catalogPairs = new HashSet<string>(StringComparer.Ordinal);
        foreach (NarrativeMechanicCatalogCanonicalJsonObject module in
                 RequireCanonicalArray(catalogSnapshot.CharacterSkill, "modules").Values
                     .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
        {
            string moduleId = RequireCanonicalString(module, "moduleId");
            foreach (NarrativeMechanicCatalogCanonicalJsonObject variant in
                     RequireCanonicalArray(module, "variants").Values
                         .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
            {
                Require(catalogPairs.Add(moduleId + "|" + RequireCanonicalString(
                        variant,
                        "variantId")),
                    $"The base CharacterSkill catalog duplicated module '{moduleId}' variants.");
            }
        }

        HashSet<string> coveredPairs = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> coveredScopes = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> contextIds = new HashSet<string>(StringComparer.Ordinal);
        List<string> coverageOrder = new List<string>();
        foreach (NarrativeMechanicCatalogCanonicalJsonObject entry in coverageEntries.Values
                     .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
        {
            Require(entry.Properties.Count == 9
                    && string.Equals(
                        RequireCanonicalString(entry, "applicability"),
                        CharacterSkillCombinationSemanticsFactory.CatalogEntryApplicability,
                        StringComparison.Ordinal),
                "A CharacterSkill catalog semantics row lost its representative-context contract.");
            string contextId = RequireCanonicalString(entry, "contextId");
            string moduleId = RequireCanonicalString(entry, "moduleId");
            string variantId = RequireCanonicalString(entry, "variantId");
            Require(contextIds.Add(contextId),
                $"The CharacterSkill catalog duplicated semantics context '{contextId}'.");
            coveredPairs.Add(moduleId + "|" + variantId);
            coverageOrder.Add(moduleId + "|" + variantId + "|" + contextId);

            NarrativeMechanicCatalogCanonicalJsonObject semantics =
                RequireCanonicalObject(entry, "semantics");
            ValidatePublicSemanticsShape("catalog:" + contextId, semantics);
            foreach (NarrativeMechanicCatalogCanonicalJsonObject semanticModule in
                     RequireCanonicalArray(semantics, "modules").Values
                         .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
            {
                Require(string.Equals(
                            RequireCanonicalString(semanticModule, "moduleId"),
                            moduleId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            RequireCanonicalString(semanticModule, "variantId"),
                            variantId,
                            StringComparison.Ordinal),
                    $"Catalog semantics context '{contextId}' describes a different module/variant.");
                coveredScopes.Add(RequireCanonicalString(semanticModule, "executionScope"));
            }
        }
        Require(catalogPairs.SetEquals(coveredPairs),
            "CharacterSkill catalog semantics coverage differs from the authored module/variant set.");
        Require(coveredScopes.SetEquals(
                CharacterSkillCombinationSemanticsFactory.ExecutionScopeTokens),
            "CharacterSkill catalog semantics coverage omits a runtime execution scope.");
        Require(coverageOrder.SequenceEqual(
                coverageOrder.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal),
            "CharacterSkill catalog semantics contexts are not deterministically ordered.");
        foreach (string requiredLarge in new[]
                 {
                     "work_speed|large", "output|large", "cleaning|large", "repair|large",
                     "stock|large", "research|large", "needs|large", "mood|large",
                     "relationship|large", "revenue|large"
                 })
        {
            Require(coveredPairs.Contains(requiredLarge),
                $"The catalog-only CharacterSkill contract omitted '{requiredLarge}'.");
        }

        NarrativeMechanicScenarioSourceCapture capture =
            new NarrativeMechanicScenarioCharacterSkillSource().CaptureScenarios();
        Require(capture.PositiveScenarios.Count == 20,
            $"CharacterSkill semantics coverage expected 20 positive scenarios, found "
            + $"{capture.PositiveScenarios.Count}.");

        const string PreservedWorkCompletedRuleId =
            "skill-rule:sha256:62c34e081c49f89c696f15087671c2872ac4ceec2c1e624a4f52d492a49e5d9a";
        const string PreservedWorkCompletedCombinationId =
            "skill-combination:sha256:b34f39adcf06ae8247058a861b39edd4c4bd595d50ed2d4c910e76cae94519cd";
        NarrativeMechanicScenario preservedScenario = capture.PositiveScenarios.Single(
            scenario => string.Equals(
                scenario.ScenarioId,
                "character-skill-authority-10-passive-l01-facility-complete",
                StringComparison.Ordinal));
        NarrativeMechanicCatalogCanonicalJsonObject preservedRequest =
            RequireCanonicalObject(
                preservedScenario.ToCanonicalJson(
                    "sha256:character-skill-preserved-id-catalog",
                    "sha256:character-skill-preserved-id-input"),
                "request");
        NarrativeMechanicCatalogCanonicalJsonObject preservedRule =
            RequireCanonicalArray(preservedRequest, "rules").Values
                .Cast<NarrativeMechanicCatalogCanonicalJsonObject>()
                .Single();
        Require(string.Equals(
                    RequireCanonicalString(preservedRule, "ruleId"),
                    PreservedWorkCompletedRuleId,
                    StringComparison.Ordinal)
                && RequireCanonicalArray(preservedRule, "combinationOptions").Values
                    .Cast<NarrativeMechanicCatalogCanonicalJsonObject>()
                    .Any(option => string.Equals(
                        RequireCanonicalString(option, "combinationId"),
                        PreservedWorkCompletedCombinationId,
                        StringComparison.Ordinal)),
            "The production WorkCompleted draft changed a still-valid v17 rule or combination ID.");

        int inspectedCombinationCount = 0;
        foreach (NarrativeMechanicScenario scenario in capture.PositiveScenarios)
        {
            NarrativeMechanicCatalogCanonicalJsonObject root = scenario.ToCanonicalJson(
                "sha256:character-skill-semantics-test-catalog",
                "sha256:character-skill-semantics-test-input");
            NarrativeMechanicCatalogCanonicalJsonObject request =
                RequireCanonicalObject(root, "request");
            string candidatePacket = RequireCanonicalString(request, "candidatePacket");
            string prompt = RequireCanonicalString(request, "prompt");
            Require(!string.IsNullOrWhiteSpace(candidatePacket)
                    && prompt.Contains(candidatePacket, StringComparison.Ordinal),
                $"Scenario '{scenario.ScenarioId}' final live prompt does not contain its exact "
                + "public candidate packet.");

            NarrativeMechanicCatalogCanonicalJsonArray fullRules =
                RequireCanonicalArray(root, "fullLegalCandidates");
            Dictionary<string, Dictionary<string, string>> fullSemanticsByRule =
                new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (NarrativeMechanicCatalogCanonicalJsonObject fullRule in fullRules.Values
                         .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
            {
                string ruleId = RequireCanonicalString(fullRule, "ruleId");
                Dictionary<string, string> byCombination =
                    new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (NarrativeMechanicCatalogCanonicalJsonObject option in
                         RequireCanonicalArray(fullRule, "options").Values
                             .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
                {
                    string combinationId = RequireCanonicalString(option, "combinationId");
                    NarrativeMechanicCatalogCanonicalJsonObject semantics =
                        RequireCanonicalObject(option, "semantics");
                    ValidatePublicSemanticsShape(scenario.ScenarioId, semantics);
                    Require(byCombination.TryAdd(
                            combinationId,
                            semantics.ToCanonicalString()),
                        $"Scenario '{scenario.ScenarioId}' duplicated combination "
                        + $"'{combinationId}' in fullLegalCandidates.");
                }
                Require(fullSemanticsByRule.TryAdd(ruleId, byCombination),
                    $"Scenario '{scenario.ScenarioId}' duplicated full rule '{ruleId}'.");
            }

            foreach (NarrativeMechanicCatalogCanonicalJsonObject rule in
                     RequireCanonicalArray(request, "rules").Values
                         .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
            {
                string ruleId = RequireCanonicalString(rule, "ruleId");
                Require(fullSemanticsByRule.TryGetValue(ruleId, out Dictionary<string, string> full),
                    $"Scenario '{scenario.ScenarioId}' request rule '{ruleId}' has no full export row.");
                foreach (NarrativeMechanicCatalogCanonicalJsonObject option in
                         RequireCanonicalArray(rule, "combinationOptions").Values
                             .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
                {
                    string combinationId = RequireCanonicalString(option, "combinationId");
                    NarrativeMechanicCatalogCanonicalJsonObject semantics =
                        RequireCanonicalObject(option, "semantics");
                    ValidatePublicSemanticsShape(scenario.ScenarioId, semantics);
                    foreach (NarrativeMechanicCatalogCanonicalJsonValue module in
                             RequireCanonicalArray(semantics, "modules").Values)
                    {
                        Require(candidatePacket.Contains(
                                "json=" + module.ToCanonicalString(),
                                StringComparison.Ordinal),
                            $"Scenario '{scenario.ScenarioId}' final live candidate packet "
                            + $"omitted the exported semantics for combination '{combinationId}'.");
                    }
                    Require(full.TryGetValue(combinationId, out string fullSemantics)
                            && string.Equals(
                                semantics.ToCanonicalString(),
                                fullSemantics,
                                StringComparison.Ordinal),
                        $"Scenario '{scenario.ScenarioId}' live request and export disagree for "
                        + $"combination '{combinationId}'.");
                    inspectedCombinationCount++;
                }
            }
        }
        Require(inspectedCombinationCount > 0,
            "CharacterSkill semantics coverage inspected no legal combinations.");

        NarrativeMechanicScenario affected = capture.PositiveScenarios.Single(scenario =>
            string.Equals(
                scenario.ScenarioId,
                "character-skill-authority-04-active-l05-exceptional-enemy",
                StringComparison.Ordinal));
        NarrativeMechanicCatalogCanonicalJsonObject affectedRequest = RequireCanonicalObject(
            affected.ToCanonicalJson(
                "sha256:character-skill-semantics-test-catalog",
                "sha256:character-skill-semantics-test-input"),
            "request");
        NarrativeMechanicCatalogCanonicalJsonArray affectedRules =
            RequireCanonicalArray(affectedRequest, "rules");
        Require(affectedRules.Values.Count >= 2,
            "The affected CharacterSkill scenario no longer has a second rule.");
        NarrativeMechanicCatalogCanonicalJsonArray unavoidableOptions =
            RequireCanonicalArray(
                (NarrativeMechanicCatalogCanonicalJsonObject)affectedRules.Values[1],
                "combinationOptions");
        Require(unavoidableOptions.Values.Count == 12,
            $"The affected second rule expected 12 legal options, found "
            + $"{unavoidableOptions.Values.Count}.");
        foreach (NarrativeMechanicCatalogCanonicalJsonObject option in unavoidableOptions.Values
                     .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
        {
            NarrativeMechanicCatalogCanonicalJsonObject semantics =
                RequireCanonicalObject(option, "semantics");
            NarrativeMechanicCatalogCanonicalJsonObject conditional =
                RequireCanonicalArray(semantics, "modules").Values
                    .Cast<NarrativeMechanicCatalogCanonicalJsonObject>()
                    .Single(module => string.Equals(
                        RequireCanonicalString(module, "moduleId"),
                        "conditional_amplify",
                        StringComparison.Ordinal));
            string[] terms = RequireCanonicalArray(conditional, "terms").Values
                .Cast<NarrativeMechanicCatalogCanonicalJsonString>()
                .Select(value => value.Value)
                .ToArray();
            Require(terms.Contains("health_ratio_subject=selected_target", StringComparer.Ordinal)
                    && terms.Contains("comparison=less_than_or_equal", StringComparer.Ordinal)
                    && terms.Any(term => term.StartsWith(
                        "health_ratio_threshold=",
                        StringComparison.Ordinal))
                    && terms.Any(term => term.StartsWith(
                        "separate_basic_damage_multiplier=",
                        StringComparison.Ordinal)),
                "An unavoidable conditional_amplify option omitted its subject, comparison, "
                + "threshold, or amplified effect.");
        }
    }

    private static void ValidatePublicSemanticsShape(
        string scenarioId,
        NarrativeMechanicCatalogCanonicalJsonObject semantics)
    {
        NarrativeMechanicCatalogCanonicalJsonInt64 schemaVersion =
            RequireCanonicalProperty<NarrativeMechanicCatalogCanonicalJsonInt64>(
                semantics,
                "schemaVersion");
        Require(schemaVersion.Value == CharacterSkillCombinationSemanticsFactory.SchemaVersion,
            $"Scenario '{scenarioId}' exported an unsupported CharacterSkill semantics version.");
        Require(!string.IsNullOrWhiteSpace(RequireCanonicalString(semantics, "combinationId"))
                && !string.IsNullOrWhiteSpace(RequireCanonicalString(semantics, "descriptionKo")),
            $"Scenario '{scenarioId}' exported blank CharacterSkill semantics identity or prose.");
        NarrativeMechanicCatalogCanonicalJsonArray modules =
            RequireCanonicalArray(semantics, "modules");
        Require(modules.Values.Count > 0,
            $"Scenario '{scenarioId}' exported CharacterSkill semantics without modules.");
        foreach (NarrativeMechanicCatalogCanonicalJsonObject module in modules.Values
                     .Cast<NarrativeMechanicCatalogCanonicalJsonObject>())
        {
            string scope = RequireCanonicalString(module, "executionScope");
            string effectKind = RequireCanonicalString(module, "effectKind");
            Require(CharacterSkillCombinationSemanticsFactory.ExecutionScopeTokens.Contains(
                        scope,
                        StringComparer.Ordinal)
                    && CharacterSkillCombinationSemanticsFactory.EffectKindTokens.Contains(
                        effectKind,
                        StringComparer.Ordinal)
                    && !string.IsNullOrWhiteSpace(RequireCanonicalString(module, "moduleId"))
                    && !string.IsNullOrWhiteSpace(RequireCanonicalString(module, "variantId"))
                    && !string.IsNullOrWhiteSpace(RequireCanonicalString(module, "descriptionKo")),
                $"Scenario '{scenarioId}' exported an incomplete or unknown module semantics row.");
            string[] terms = RequireCanonicalArray(module, "terms").Values
                .Cast<NarrativeMechanicCatalogCanonicalJsonString>()
                .Select(value => value.Value)
                .ToArray();
            Require(terms.Length > 0
                    && terms.SequenceEqual(
                        terms.OrderBy(value => value, StringComparer.Ordinal),
                        StringComparer.Ordinal)
                    && terms.Distinct(StringComparer.Ordinal).Count() == terms.Length
                    && terms.All(term => !string.IsNullOrWhiteSpace(term)
                        && term.IndexOf('=') > 0),
                $"Scenario '{scenarioId}' exported non-canonical CharacterSkill semantic terms.");
            string moduleId = RequireCanonicalString(module, "moduleId");
            string variantId = RequireCanonicalString(module, "variantId");
            if (string.Equals(moduleId, "conditional_amplify", StringComparison.Ordinal)
                && string.Equals(scope, "offense_battle", StringComparison.Ordinal))
            {
                string expectedThreshold = string.Equals(
                    variantId,
                    "critical",
                    StringComparison.Ordinal)
                        ? "0.25"
                        : "0.5";
                string expectedMultiplier = string.Equals(
                    variantId,
                    "critical",
                    StringComparison.Ordinal)
                        ? "0.7"
                        : "0.35";
                Require(terms.Contains(
                            "health_ratio_subject=selected_target",
                            StringComparer.Ordinal)
                        && terms.Contains(
                            "comparison=less_than_or_equal",
                            StringComparer.Ordinal)
                        && terms.Contains(
                            "health_ratio_threshold=" + expectedThreshold,
                            StringComparer.Ordinal)
                        && terms.Contains(
                            "amplified_effect=separate_basic_damage",
                            StringComparer.Ordinal)
                        && terms.Contains(
                            "separate_basic_damage_multiplier=" + expectedMultiplier,
                            StringComparer.Ordinal),
                    $"Scenario '{scenarioId}' exported runtime-mismatched {variantId} "
                    + "conditional amplification semantics.");
            }
            if (string.Equals(moduleId, "conditional_amplify", StringComparison.Ordinal)
                && string.Equals(scope, "defense_ultimate", StringComparison.Ordinal))
            {
                Require(terms.Contains("health_check=none", StringComparer.Ordinal)
                        && terms.Contains(
                            "contribution_is_unconditional=true",
                            StringComparer.Ordinal),
                    $"Scenario '{scenarioId}' falsely described defense-ultimate conditional "
                    + "amplification as health-gated.");
            }
        }
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject RequireCanonicalObject(
        NarrativeMechanicCatalogCanonicalJsonObject owner,
        string key) =>
        RequireCanonicalProperty<NarrativeMechanicCatalogCanonicalJsonObject>(owner, key);

    private static NarrativeMechanicCatalogCanonicalJsonArray RequireCanonicalArray(
        NarrativeMechanicCatalogCanonicalJsonObject owner,
        string key) =>
        RequireCanonicalProperty<NarrativeMechanicCatalogCanonicalJsonArray>(owner, key);

    private static string RequireCanonicalString(
        NarrativeMechanicCatalogCanonicalJsonObject owner,
        string key) =>
        RequireCanonicalProperty<NarrativeMechanicCatalogCanonicalJsonString>(owner, key).Value;

    private static T RequireCanonicalProperty<T>(
        NarrativeMechanicCatalogCanonicalJsonObject owner,
        string key)
        where T : NarrativeMechanicCatalogCanonicalJsonValue
    {
        NarrativeMechanicCatalogCanonicalJsonValue[] values = owner.Properties
            .Where(pair => string.Equals(pair.Key, key, StringComparison.Ordinal))
            .Select(pair => pair.Value)
            .ToArray();
        Require(values.Length == 1 && values[0] is T,
            $"Canonical JSON property '{key}' was missing or had the wrong type.");
        return (T)values[0];
    }

    private static void VerifyIndependentCatalogExportBytes()
    {
        NarrativeMechanicCatalogByteComparison comparison =
            NarrativeMechanicCatalogExporter.CompareIndependentExports(
                () => new NarrativeMechanicCatalogUnityAssetSource());
        comparison.RequireByteIdentical();
        Require(comparison.FirstBytes.Length > 0,
            "Independent catalog comparison produced an empty catalog.");
        Require(string.Equals(
                comparison.FirstInputDigest,
                comparison.SecondInputDigest,
                StringComparison.Ordinal),
            "Independent catalog captures observed different provenance digests.");
    }

    private static string Sha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class SchedulerFixture : IContextAwareLlmRequest
    {
        public SchedulerFixture(
            int priority,
            float enqueuedAt,
            PrefixAffinityKey key,
            bool persistent,
            bool urgent,
            float expiresAt)
        {
            Priority = priority;
            EnqueuedAt = enqueuedAt;
            Scheduling = new NarrativeSchedulingMetadata
            {
                AffinityKey = key,
                Persistent = persistent,
                Urgent = urgent,
                ExpiresAt = expiresAt
            };
        }

        public int Priority { get; }
        public float EnqueuedAt { get; }
        public NarrativeSchedulingMetadata Scheduling { get; }
    }

    private enum EquipmentChoiceStubMode
    {
        RuntimeUnavailable,
        RequestRejected,
        AsyncInvalid,
        OutOfRange
    }

    private sealed class EquipmentChoiceParticipantFixture : IDisposable
    {
        private readonly CharacterSO data;

        public EquipmentChoiceParticipantFixture()
        {
            Root = CharacterAiPlanDebugFixtures.CreateActorObject(
                "EquipmentChoiceFailureParticipant");
            try
            {
                data = CharacterAiPlanDebugFixtures.CreateCharacterData(
                    CharacterType.NPC,
                    "장비 선택 감사자",
                    "human");
                Actor = Root.GetComponent<CharacterActor>();
                if (Actor == null)
                    throw new InvalidOperationException(
                        "Equipment choice failure fixture has no CharacterActor.");
                Actor.EnsureRuntimeState();
                Actor.Identity.SetPersistentId(
                    "character:equipment-choice-auditor");
                Actor.Initialization(data);
                World = new EquipmentChoiceBuildingWorldQuery(Actor);
            }
            catch
            {
                if (data != null) UnityEngine.Object.DestroyImmediate(data);
                UnityEngine.Object.DestroyImmediate(Root);
                throw;
            }
        }

        public GameObject Root { get; }
        public CharacterActor Actor { get; }
        public string ActorId => Actor.Identity.PersistentId;
        public IBuildingWorldQuery World { get; }

        public void Dispose()
        {
            if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private sealed class EquipmentChoiceBuildingWorldQuery :
        IBuildingWorldQuery,
        ICharacterWorldQuery,
        ICharacterLifetimeQuery
    {
        private static readonly IReadOnlyList<BuildableObject> EmptyBuildings =
            Array.Empty<BuildableObject>();
        private readonly CharacterActor[] characters;

        public EquipmentChoiceBuildingWorldQuery(CharacterActor actor)
        {
            characters = new[]
            {
                actor ?? throw new ArgumentNullException(nameof(actor))
            };
        }

        public int BuildingVersion => 0;
        public int CharacterVersion => 0;
        public int LifetimeCharacterVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings => EmptyBuildings;
        public IReadOnlyList<CharacterActor> Characters => characters;
        public IReadOnlyList<CharacterActor> AllCharacters => characters;
    }

    private sealed class MissingEquipmentChoiceRuntimeProvider : ILocalLlmRuntimeProvider
    {
        public bool TryGetRuntime(out ILocalLlmRuntime runtime)
        {
            runtime = null;
            return false;
        }

        public ILocalLlmRuntime GetRequiredRuntime()
        {
            throw new InvalidOperationException("Equipment choice fixture has no runtime.");
        }
    }

    private sealed class FixedEquipmentChoiceRuntimeProvider : ILocalLlmRuntimeProvider
    {
        private readonly ILocalLlmRuntime runtime;

        public FixedEquipmentChoiceRuntimeProvider(ILocalLlmRuntime runtime)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public bool TryGetRuntime(out ILocalLlmRuntime resolved)
        {
            resolved = runtime;
            return true;
        }

        public ILocalLlmRuntime GetRequiredRuntime() => runtime;
    }

    private sealed class EquipmentChoiceRuntimeStub :
        ILocalLlmRuntime,
        IConstrainedEquipmentChoiceLlmRuntime
    {
        private readonly EquipmentChoiceStubMode mode;

        public EquipmentChoiceRuntimeStub(EquipmentChoiceStubMode mode)
        {
            this.mode = mode;
        }

        public bool GenerateEquipmentChoiceAsync(
            string requestKey,
            string prompt,
            int candidateCount,
            Action<LocalLlmChoiceResult> callback)
        {
            if (mode == EquipmentChoiceStubMode.RequestRejected)
            {
                return false;
            }
            if (mode == EquipmentChoiceStubMode.OutOfRange)
            {
                callback?.Invoke(new LocalLlmChoiceResult(true, 9, string.Empty));
                return true;
            }

            callback?.Invoke(new LocalLlmChoiceResult(
                false,
                -1,
                "EquipmentChoice.SchemaInvalid: fixture response rejected"));
            return true;
        }

        public bool GenerateCharacterSkillAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback) => false;
        public bool GenerateCharacterRecordAsync(
            string prompt,
            string originalText,
            Action<LocalLlmResult> callback) => false;
        public bool GenerateBubbleLineAsync(
            string prompt,
            string originalText,
            Action<LocalLlmResult> callback) => false;
    }
}
#endif
