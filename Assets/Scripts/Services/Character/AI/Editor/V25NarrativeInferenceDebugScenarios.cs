#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class V25NarrativeInferenceDebugScenarios
{
    [MenuItem("DungeonStory/Debug/V25/Run Narrative Inference Contracts")]
    public static void RunAll()
    {
        VerifyChoiceCanonicalizationAndGrammar();
        VerifyContextAwareScheduling();
        VerifyTypedEquipmentEvidence();
        VerifyMechanicalNarrativeSeparation();
        VerifyMultiPerspectiveIdentity();
        VerifyMissingHostFailsClosed();
        VerifyCorruptModelFailsClosed();
        VerifyBundledLlamaCppBackendContract();
        Debug.Log("V25 narrative inference contracts PASS (8/8).");
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
        Require(EquipmentChoiceResultParser.TryParse(" 0", 2, out int zero) && zero == 0,
            "leading-space choice failed");
        Require(EquipmentChoiceResultParser.TryParse("\n2", 3, out int two) && two == 2,
            "leading-newline choice failed");
        Require(!EquipmentChoiceResultParser.TryParse("3", 3, out _),
            "out-of-range choice passed");
        Require(!EquipmentChoiceResultParser.TryParse("C01", 3, out _),
            "symbolic choice passed");
        Require(!EquipmentChoiceResultParser.TryParse("{\"choice\":0}", 3, out _),
            "JSON choice passed");
        Require(!EquipmentChoiceResultParser.TryParse(" ", 3, out _),
            "whitespace-only choice passed");
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
}
#endif
