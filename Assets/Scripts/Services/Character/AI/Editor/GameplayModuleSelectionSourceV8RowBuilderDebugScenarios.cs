#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;

public static class GameplayModuleSelectionSourceV8RowBuilderDebugScenarios
{
    [MenuItem("Tools/DungeonStory/QA/Gameplay Module Selection Source V8 Row")]
    public static void RunOrThrow()
    {
        JObject crossLanguageCanonical = new()
        {
            ["b"] = 1.0d,
            ["a"] = "연화",
            ["c"] = new JArray(5.0d, -0.25d, 2),
            ["d"] = 0.62000000476837158d,
            ["e"] = 0.550000011920929d
        };
        Require(string.Equals(
                GameplayModuleSelectionCanonicalJson.Hash(crossLanguageCanonical),
                "sha256:bd992b55f8bb11b0d06b336a98c8a17cc8f15e07b6f3935a9b24c1744f994d49",
                StringComparison.Ordinal),
            "C#/Python canonical JSON hash changed");
        GameplayOutcomeEvidenceBindingSnapshot binding = BuildBinding();
        JObject request = BuildRequest(binding.publicFactId);
        string profileId = LocalLlmRequestProfiles
            .CharacterSkillModuleSelection.Id;
        JObject live = GameplayModuleSelectionLiveWireCapture.Capture(
            profileId,
            "logical:source-v8:1",
            "attempt:source-v8:1",
            "dungeonstory-test-model",
            "candidateCount=1\nselectionId=selection:source-v8:1",
            new[] { request });
        JObject owner = new()
        {
            ["ownerId"] = "owner:source-v8:1",
            ["revision"] = 2,
            ["domainState"] = new JObject { ["hp"] = 72 }
        };
        JObject execution = new()
        {
            ["sourcePackageId"] = "causal-package:source-v8:1",
            ["sourcePackageSha256"] = Hash('1'),
            ["campaignId"] = "campaign:source-v8:1",
            ["ownerId"] = "owner:source-v8:1",
            ["ownerCheckpointId"] = "checkpoint:source-v8:1",
            ["ownerCheckpointSha256"] = GameplayModuleSelectionCanonicalJson
                .Sha256Prefixed(GameplayModuleSelectionSourceV8PackageExporter
                    .CheckpointFileBytes(owner)),
            ["ownerRevision"] = 2,
            ["episodeId"] = "episode:source-v8:1",
            ["actionIds"] = new JArray("action:defend"),
            ["outcomeIds"] = new JArray(
                binding.outcomeRunId + "/" + binding.outcomeSequence),
            ["compactedMemoryIds"] = new JArray(),
            ["captureDay"] = 3,
            ["triggerId"] = "trigger:skill",
            ["controlledSimulation"] = true,
            ["restoredFromCheckpoint"] = true
        };
        JObject row = GameplayModuleSelectionSourceV8RowBuilder.BuildRow(
            formulaVersion: 5,
            catalogSha256: Hash('3'),
            gameCommit: new string('4', 40),
            dirtySourceSha256: Hash('5'),
            contentSha256: Hash('6'),
            profileId: profileId,
            targetPersistentId: binding.subjectId,
            executionBinding: execution,
            evidenceBindings: new[] { binding },
            liveRequest: live,
            authorityRequests: new[] { request },
            ownerSnapshot: owner,
            applicationContractId: "contract:skill:5",
            applicationContractSha256: Hash('7'));

        string[] expectedKeys =
        {
            "schemaVersion", "taskId", "formulaVersion", "catalogSha256",
            "gameCommit", "dirtySourceSha256", "contentSha256", "profileId",
            "targetPersistentId", "executionBinding", "evidence", "liveRequest",
            "authoritySidecar"
        };
        Require(row.Properties().Select(value => value.Name)
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(expectedKeys),
            "source-v8 row keys changed");
        JObject evidence = (JObject)row["evidence"];
        JObject unsignedEvidence = (JObject)evidence.DeepClone();
        unsignedEvidence.Remove("evidenceHash");
        Require(string.Equals(
                (string)evidence["evidenceHash"],
                GameplayModuleSelectionCanonicalJson.Hash(unsignedEvidence),
                StringComparison.Ordinal),
            "evidence hash changed");
        JObject sidecar = (JObject)row["authoritySidecar"];
        Require(string.Equals(
                (string)sidecar["authoritySnapshotSha256"],
                GameplayModuleSelectionCanonicalJson.Hash(new JArray(request)),
                StringComparison.Ordinal),
            "authority snapshot hash changed");
        Require(string.Equals(
                (string)sidecar["ownerSnapshotSha256"],
                GameplayModuleSelectionCanonicalJson.Hash(owner),
                StringComparison.Ordinal),
            "owner snapshot hash changed");

        GameplayOutcomeEvidenceBindingSnapshot invalid = binding.Clone();
        invalid.participants[0].roleId = "target";
        bool rejected = false;
        try
        {
            GameplayModuleSelectionSourceV8RowBuilder.BuildEvidence(
                new[] { invalid });
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected, "typed evidence mismatch was accepted");
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_MODULE_SOURCE_V8_DEBUG_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            GameplayModuleSelectionSourceV8PackageExporter.Export(
                output,
                "module-source:debug:1",
                new[] { row },
                new[]
                {
                    new GameplayModuleSelectionSourceCheckpoint(
                        "checkpoint:source-v8:1",
                        "owner:source-v8:1",
                        2,
                        owner)
                },
                new[]
                {
                    "Assets/Scripts/Services/Character/AI/Editor/GameplayModuleSelectionCanonicalJson.cs",
                    "Assets/Scripts/Services/Character/AI/Editor/GameplayModuleSelectionLiveWireCapture.cs",
                    "Assets/Scripts/Services/Character/AI/Editor/GameplayModuleSelectionSourceV8PackageExporter.cs",
                    "Assets/Scripts/Services/Character/AI/Editor/GameplayModuleSelectionSourceV8RowBuilder.cs"
                });
        }
        Console.WriteLine("Gameplay module-selection source-v8 row PASS");
    }

    private static GameplayOutcomeEvidenceBindingSnapshot BuildBinding()
    {
        string subjectId = "character:source-v8:1";
        return new GameplayOutcomeEvidenceBindingSnapshot
        {
            schemaVersion = 2,
            sourceKind = "exact",
            publicFactId = "public-fact:sha256:" + new string('a', 64),
            outcomeRunId = "run:source-v8:1",
            outcomeSequence = 1,
            outcomeTypeId = "combat.damage",
            subjectKindId = "character",
            subjectId = subjectId,
            perspectiveKind = (int)NarrativePerspectiveKind.Character,
            firstDay = 3,
            lastDay = 3,
            occurrenceCount = 1,
            anchorRevision = 0,
            status = (int)GameplayOutcomeStatus.Succeeded,
            subjectSalience = 1f,
            influenceUseCount = 0,
            influenceRevision = 0,
            canonicalFactText = "연화가 적의 공격을 막아 냈다.",
            roleIds = new List<string> { "actor" },
            metricIds = new List<string> { "damage" },
            metricReferenceIds = new List<string>
                { "character:source-v8-target:1" },
            factIds = new List<string> { "result" },
            semanticTags = new List<string> { "combat" },
            participants = new List<GameplayOutcomeEvidenceParticipantBindingSnapshot>
            {
                new()
                {
                    entityKindId = "character",
                    entityId = subjectId,
                    roleId = "actor",
                    participationKind = (int)GameplayParticipationKind.Direct,
                    hasPerceptionEvidence = true,
                    displayText = "연화",
                    displayRevision = "name:1",
                    locale = "ko-KR",
                    pronunciationMode = 0,
                    pronunciationValue = string.Empty,
                    finalConsonant = 0,
                    pronunciationRevision = "pronunciation:1"
                }
            },
            metrics = new List<GameplayOutcomeEvidenceMetricBindingSnapshot>
            {
                new()
                {
                    metricId = "damage",
                    unitId = "hp",
                    referenceKindId = "character",
                    referenceId = "source-v8-target:1",
                    value = 5d,
                    minimum = 5d,
                    maximum = 5d,
                    sampleCount = 1
                }
            },
            facts = new List<GameplayOutcomeEvidenceFactBindingSnapshot>
            {
                new() { factId = "result", value = "blocked" }
            },
            provenance = new List<GameplayOutcomeEvidenceProvenanceBindingSnapshot>
            {
                new() { kindId = "campaign", value = "campaign:source-v8:1" }
            },
            locationId = "dungeon",
            roomId = "room:source-v8:1",
            locationX = 1,
            locationY = 2,
            operationId = "operation:source-v8:1",
            rootOperationId = "operation:source-v8:1",
            causationRelationId = "direct"
        };
    }

    private static JObject BuildRequest(string factId) => new()
    {
        ["selectionId"] = "selection:source-v8:1",
        ["targetDisplayName"] = "연화",
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
        ["evidenceFactIds"] = new JArray(factId),
        ["publicFacts"] = new JArray
        {
            new JObject
            {
                ["factId"] = factId,
                ["text"] = "연화가 적의 공격을 막아 냈다."
            }
        },
        ["style"] = new JObject
        {
            ["genre"] = "fantasy-wuxia",
            ["nameMaxLength"] = 14,
            ["flavorMaxLength"] = 48
        },
        ["skillContext"] = new JObject
        {
            ["skillKind"] = "Active",
            ["trigger"] = "ManualCombat",
            ["target"] = "Self",
            ["targetingMode"] = "Self",
            ["effectArea"] = "Single",
            ["areaSize"] = 1,
            ["ultimateDomain"] = "None",
            ["semanticDescription"] = "직접 사용해 자신을 지키는 단일 대상 기술이다."
        }
    };

    private static string Hash(char value) =>
        "sha256:" + new string(value, 64);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
