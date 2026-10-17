#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class GameplayModuleSelectionLiveWireCaptureDebugScenarios
{
    public static void RunEvolutionPromptParityBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("A new prompt-parity output directory is required.");
        Directory.CreateDirectory(output);
        JArray cases = new();
        foreach (bool facility in new[] { false, true })
        foreach (bool drawback in new[] { false, true })
        {
            cases.Add(VerifyEvolutionPrompt(facility, drawback));
            Debug.Log($"EVOLUTION_PROMPT_CASE=PASS facility={facility} optionalDrawback={drawback}");
        }
        RunOrThrow();
        FacilityEvolutionDebugScenarios.RunFormulaV3BurdenSeparationFromBatch();
        JObject report = new()
        {
            ["passed"] = true,
            ["cases"] = cases,
            ["scope"] = "Controlled pending prompt/production wire parity, JSON replay, malformed-version rejection and existing facility formula burden/conflict authority regression; not live campaign or model response/effect proof",
            ["managedGcHeapCount"] = Environment.GetEnvironmentVariable("DOTNET_GCHeapCount"),
            ["fullAuditClosure"] = false,
            ["trainingEligible"] = false,
            ["humanApprovalClaimed"] = false
        };
        using (FileStream stream = new(Path.Combine(output, "report.json"), FileMode.CreateNew))
        using (StreamWriter writer = new(stream, new UTF8Encoding(false)))
            writer.Write(report.ToString());
        Debug.Log("EVOLUTION_OPTIONAL_DRAWBACK_PROMPT_PARITY=PASS");
    }

    private static JObject VerifyEvolutionPrompt(bool facility, bool drawback)
    {
        string profile = facility ? LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id
            : LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id;
        string prefix = facility ? "facility" : "equipment";
        EvolutionNode node = new()
        {
            formulaVersion = 3,
            presentationState = EquipmentEvolutionPresentationState.ModuleSelectionPending,
            presentationId = "presentation:" + prefix + ":prompt-fixture",
            moduleSelectionId = "presentation:" + prefix + ":prompt-fixture",
            evidenceIds = new List<string> { "fact:prompt-fixture" },
            gameplayOutcomeEvidence = new List<GameplayOutcomeEvidenceBindingSnapshot>
            {
                new() { publicFactId = "fact:prompt-fixture", sourceKind = "exact",
                    canonicalFactText = "검증 중 충격을 견뎠다." }
            },
            moduleSelectionOffers = new List<EquipmentEvolutionModuleOfferState>
            {
                new() { moduleId = prefix + ":prompt-positive", polarity = EvolutionModuleOfferPolarity.Positive,
                    semanticDescription = "검증용 이로운 모듈" }
            }
        };
        if (drawback)
            node.moduleSelectionOffers.Add(new EquipmentEvolutionModuleOfferState
            {
                moduleId = prefix + ":prompt-drawback", polarity = EvolutionModuleOfferPolarity.Drawback,
                semanticDescription = "검증용 선택형 부담"
            });
        NarrativePublicContextMaterial material = NarrativePublicContextFactory.Build(
            LocalLlmRequestProfiles.EvolutionHistory.Id, "equipment:prompt-fixture",
            NarrativePublicSubjectKind.Equipment, "frontier", false, false,
            new[] { new NarrativePublicEntityInput("equipment:prompt-fixture", NarrativePublicEntityKind.Equipment, "검증 장비") },
            new[] { new NarrativePublicFactInput("Usage", "prompt-fixture", "equipment:prompt-fixture", "검증 중 충격을 견뎠다.", 1,
                NarrativePublicFactCategory.Memory, "equipment:prompt-fixture", "Completed", 1, 1,
                eventData: new NarrativePublicEventInput("fact:prompt-fixture", string.Empty,
                    "equipment:prompt-fixture", "impact", "Completed", 1, 1, null)) },
            "prompt-parity-fixture");
        string Build(EvolutionNode value) => facility
            ? FacilityFormulaEvolutionPresentationRuntime.BuildModuleSelectionPrompt(value)
            : EvolutionHistoryNarrativeRuntime.BuildEquipmentModuleSelectionPrompt(value,
                new EquipmentEvolutionPresentationRequest { presentationId = value.moduleSelectionId, evidenceIds = new List<string>(value.evidenceIds) }, material);
        string before = JsonUtility.ToJson(node);
        string prompt = Build(node);
        int maximumDrawback = drawback ? 1 : 0;
        Require(prompt.Contains("maximumDrawbackModules=" + maximumDrawback, StringComparison.Ordinal),
            "Prompt disagrees with the pending optional drawback limit.");
        Require(prompt.Contains("polarity=Positive", StringComparison.Ordinal), "Positive polarity was lost.");
        Require(prompt.Contains("polarity=Drawback", StringComparison.Ordinal) == drawback,
            "Prompt inverted or invented drawback polarity.");
        if (drawback)
            Require(!prompt.Contains("빈 배열", StringComparison.Ordinal)
                && !prompt.Contains("empty array", StringComparison.Ordinal)
                && !prompt.Contains("no independent drawback", StringComparison.OrdinalIgnoreCase),
                "Prompt prohibits an offered optional drawback.");
        Require(JsonUtility.ToJson(node) == before, "Prompt construction mutated pending state.");
        Require(Build(JsonUtility.FromJson<EvolutionNode>(before)) == prompt,
            "Prompt changed after pending JSON restoration.");
        JObject authority = BuildRequest(profile, 0);
        authority["selectionId"] = node.moduleSelectionId;
        authority["maximumDrawbackModules"] = maximumDrawback;
        authority["moduleOffers"] = new JArray(node.moduleSelectionOffers.Select(value => new JObject
        {
            ["moduleId"] = value.moduleId,
            ["polarity"] = value.polarity == EvolutionModuleOfferPolarity.Positive ? "positive" : "drawback",
            ["semanticDescription"] = value.semanticDescription
        }));
        authority["evidenceFactIds"] = new JArray(node.evidenceIds);
        authority["publicFacts"] = new JArray(new JObject { ["factId"] = node.evidenceIds[0], ["text"] = "검증 중 충격을 견뎠다." });
        JObject captured = GameplayModuleSelectionLiveWireCapture.Capture(
            profile, node.moduleSelectionId, "attempt:prompt-fixture", "dungeonstory-test-model", prompt, new[] { authority });
        JObject body = JObject.Parse((string)captured["wireBodyJson"]);
        JObject schema = (JObject)body["response_format"]["json_schema"]["schema"];
        Require((int)schema["properties"]["drawbackModuleIds"]["maxItems"] >= maximumDrawback,
            "Production wire schema prohibits the offered optional drawback.");
        Require((bool)schema["additionalProperties"] == false && ((JArray)schema["required"]).Count == 6,
            "Closed six-field response contract changed.");
        Require(((string)body["messages"][1]["content"]).Contains("maximumDrawbackModules=" + maximumDrawback, StringComparison.Ordinal),
            "Transport serialization lost the pending limit.");
        if (drawback)
        {
            EvolutionNode invalid = JsonUtility.FromJson<EvolutionNode>(before);
            invalid.formulaVersion = 2;
            bool rejected = false;
            try { Build(invalid); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "An unsupported version silently offered optional drawbacks.");
        }
        return new JObject { ["profileId"] = profile, ["optionalDrawback"] = drawback,
            ["pendingUnchanged"] = true, ["jsonPromptExact"] = true, ["capture"] = captured };
    }

    [MenuItem("Tools/DungeonStory/QA/Gameplay Module Selection Live Wire Capture")]
    public static void RunOrThrow()
    {
        VerifyProfile(
            LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
            candidateCount: 3,
            nameMaximum: 14,
            flavorMaximum: 48,
            maximumEvidence: 2);
        VerifyProfile(
            LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id,
            candidateCount: 1,
            nameMaximum: 32,
            flavorMaximum: 180,
            maximumEvidence: 3);
        VerifyProfile(
            LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id,
            candidateCount: 1,
            nameMaximum: 32,
            flavorMaximum: 180,
            maximumEvidence: 3);
        VerifyProfile(
            LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id,
            candidateCount: 1,
            nameMaximum: 32,
            flavorMaximum: 180,
            maximumEvidence: 3);
        VerifySkillEvidenceOverflowIsRejected();
        Console.WriteLine("Gameplay module-selection live wire capture PASS");
    }

    private static void VerifySkillEvidenceOverflowIsRejected()
    {
        JObject request = BuildRequest(
            LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id, 0);
        ((JArray)request["evidenceFactIds"]).Add("fact:test:overflow:3");
        ((JArray)request["publicFacts"]).Add(new JObject
        {
            ["factId"] = "fact:test:overflow:3",
            ["text"] = "세 번째 근거는 현행 기술 요청에 노출할 수 없다."
        });
        bool rejected = false;
        try
        {
            GameplayModuleSelectionLiveWireCapture.Capture(
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                "logical:overflow",
                "attempt:overflow:1",
                "dungeonstory-test-model",
                "overflow",
                new[] { request });
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected, "skill evidence overflow was accepted");
    }

    private static void VerifyProfile(
        string profileId,
        int candidateCount,
        int nameMaximum,
        int flavorMaximum,
        int maximumEvidence)
    {
        List<JObject> requests = Enumerable.Range(0, candidateCount)
            .Select(index => BuildRequest(profileId, index))
            .ToList();
        string prompt = "profile=" + profileId + "\n실제 캡처 검증";
        if (candidateCount == 1)
        {
            JObject request = requests.Single();
            prompt += "\nselectionId=" + (string)request["selectionId"]
                + "\nevidenceFactIds=" + string.Join(",", ((JArray)request["evidenceFactIds"]).Values<string>())
                + "\n- moduleId=guard; polarity=Positive; meaning=방호 능력을 강화한다.";
        }
        JObject captured = GameplayModuleSelectionLiveWireCapture.Capture(
            profileId,
            "logical:" + profileId,
            "attempt:" + profileId + ":1",
            "dungeonstory-test-model",
            prompt,
            requests);
        Require((int)captured["candidateCount"] == candidateCount,
            "candidate count changed");
        Require(((JArray)captured["selectionIds"]).Values<string>()
                .SequenceEqual(requests.Select(value => (string)value["selectionId"]),
                    StringComparer.Ordinal),
            "selection order changed");
        JObject constraints = (JObject)captured["constraints"];
        Require((int)constraints["nameMaxUtf16"] == nameMaximum
                && (int)constraints["flavorMaxUtf16"] == flavorMaximum
                && (int)constraints["maximumEvidenceFacts"] == maximumEvidence,
            "profile constraints changed");
        string wire = (string)captured["wireBodyJson"];
        JObject body = JObject.Parse(wire);
        Require(body["messages"] is JArray messages
                && messages.Count == 2
                && string.Equals((string)messages[0]["role"], "system",
                    StringComparison.Ordinal)
                && string.Equals((string)messages[1]["role"], "user",
                    StringComparison.Ordinal),
            "production message envelope changed");
        string wireHash = GameplayModuleSelectionCanonicalJson.Sha256Prefixed(
            Encoding.UTF8.GetBytes(wire));
        Require(string.Equals(wireHash, (string)captured["wireBodySha256"],
                StringComparison.Ordinal),
            "wire hash changed");
        JObject schema = (JObject)body["response_format"]?["json_schema"]?["schema"];
        Require(schema != null, "production response schema is missing");
        string schemaHash = GameplayModuleSelectionCanonicalJson.Hash(schema);
        Require(string.Equals(schemaHash, (string)captured["schemaSha256"],
                StringComparison.Ordinal),
            "canonical schema hash changed");
    }

    private static JObject BuildRequest(string profileId, int index)
    {
        string selectionId = "selection:test:" + profileId + ":" + index;
        int evidenceCount = string.Equals(profileId,
            LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
            StringComparison.Ordinal) ? 2 : 3;
        JArray evidence = new(Enumerable.Range(1, evidenceCount).Select(value =>
            "fact:test:" + profileId + ":" + index + ":" + value));
        JObject result = new()
        {
            ["selectionId"] = selectionId,
            ["targetDisplayName"] = "검증 대상",
            ["moduleOffers"] = new JArray
            {
                new JObject
                {
                    ["moduleId"] = "guard",
                    ["polarity"] = "positive",
                    ["semanticDescription"] = "방호 능력을 강화한다."
                }
            },
            ["maximumPositiveModules"] = 1,
            ["maximumDrawbackModules"] = 0,
            ["evidenceFactIds"] = evidence,
            ["publicFacts"] = new JArray(evidence.Values<string>().Select(value =>
                new JObject
                {
                    ["factId"] = value,
                    ["text"] = "검증된 실제 게임 사건이다."
                })),
            ["style"] = new JObject
            {
                ["genre"] = "fantasy-wuxia",
                ["nameMaxLength"] = string.Equals(profileId,
                    LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                    StringComparison.Ordinal) ? 14 : 32,
                ["flavorMaxLength"] = string.Equals(profileId,
                    LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                    StringComparison.Ordinal) ? 48 : 180
            }
        };
        if (string.Equals(profileId,
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                StringComparison.Ordinal))
        {
            result["skillContext"] = new JObject
            {
                ["skillKind"] = "Active",
                ["trigger"] = "ManualCombat",
                ["target"] = "Self",
                ["targetingMode"] = "Self",
                ["effectArea"] = "Single",
                ["areaSize"] = 1,
                ["ultimateDomain"] = "None",
                ["semanticDescription"] = "직접 사용해 자신을 지키는 단일 대상 기술이다."
            };
        }
        return result;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
