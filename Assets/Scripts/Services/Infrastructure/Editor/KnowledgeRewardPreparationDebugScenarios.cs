using System;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public static class KnowledgeRewardPreparationDebugScenarios
{
    public static string RunAll()
    {
        GameObject host = new("Knowledge prepared reward");
        host.SetActive(false);
        try
        {
            var codex = host.AddComponent<CodexRuntime>();
            Require(codex.TryGetNextMemoryResidueClue(out string clue), "authored clue available");
            Require(codex.TryPrepareMemoryResidueClue(clue, out var first, out _), "prepare clue");
            Require(!codex.State.HasInfo(CodexEntryCategory.Invasion, "memory-residue", clue), "prepare is detached");
            Require(codex.TryPrepareMemoryResidueClue(clue, out var stale, out _), "second detached preparation");
            Require(first.Before == 0 && first.After == 1 && first.TryPublish(), "publish without application port or observers");
            Require(!first.TryPublish() && !stale.TryPublish(), "duplicate and stale publication refused");
            Require(codex.State.HasInfo(CodexEntryCategory.Invasion, "memory-residue", clue), "exact clue published");
            first.Rollback();
            first.Rollback();
            Require(codex.TryGetNextMemoryResidueClue(out var again) && clue == again, "rollback exact authored order");
            Require(codex.TryPrepareMemoryResidueClue(clue, out var unrelated, out _), "prepare before unrelated mutation");
            codex.State.AddInfo(CodexEntryCategory.Invasion, "other", "다른 기록", "보존할 사실", CodexInfoSource.Research);
            Require(!unrelated.TryPublish()
                && codex.State.HasInfo(CodexEntryCategory.Invasion, "other", "보존할 사실"), "stale preparation cannot overwrite other codex entry");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }

        var regions = new OffenseRegionRuntime();
        var region = regions.Regions[0];
        region.intelligenceDamage = 97;
        Require(regions.TryPrepareReconnaissance(region.regionId, 10, out var recon), "prepare region");
        Require(recon.Before == 97 && recon.After == 100 && region.intelligenceDamage == 97, "actual clamp prepared only");
        Require(recon.TryPublish() && region.intelligenceDamage == 100 && !recon.TryPublish(), "publish region exactly once");
        recon.Rollback();
        Require(region.intelligenceDamage == 97, "region rollback");
        Require(regions.TryPrepareReconnaissance(region.regionId, 10, out var staleRecon), "prepare region stale");
        region.intelligenceDamage = 98;
        Require(!staleRecon.TryPublish() && region.intelligenceDamage == 98, "intervening region update retained");
        Require(!regions.TryPrepareReconnaissance(region.regionId, float.NaN, out _), "invalid amount refused");
        VerifyTaskAllocationPersistence();
        VerifyCompletionEncoding(KnowledgeResidueUse.RegionReconnaissance);
        VerifyCompletionEncoding(KnowledgeResidueUse.CodexAnalysis);
        return "PASS knowledge reward preparations and identity (6 groups): actual Codex/region prepared state, empty-queue allocation JSON, typed completion encoding/projection/restore";
    }

    private static void VerifyCompletionEncoding(KnowledgeResidueUse use)
    {
        bool codex = use == KnowledgeResidueUse.CodexAnalysis;
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[] { new KnowledgeCompletionOutcomeDescriptor() },
            new IGameplayOutcomeAdapterRegistration[] { new KnowledgeCompletionOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, GameplayOutcomeBufferLimits.Default);
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        const string operation = "research-knowledge-residue-sink:knowledge-00001";
        const string reason = "memory-residue-research-consumed";
        const string stack = "stack:knowledge-encoding";
        var source = new PhysicalItemDispositionSourceFact(stack,
            KnowledgeResidueDestinationAuthority.MemoryResidueItemId, "instance:knowledge-encoding", 1, 200,
            new Vector2Int(4, 5), ResearchWorkOutcomeNames.Snapshot("residue", "기억 잔재"));
        var input = new PhysicalItemBatchDispositionReceipt(PhysicalItemDispositionKind.Sink, operation, reason,
            $"{(int)PhysicalItemDispositionKind.Sink}:{reason}:{stack}=1", new[] { stack }, 1, 200, 41, new[] { source });
        var key = new GameplayResultKey(KnowledgeCompletionOutcomeIds.ProducerId, new GameplayOperationId(operation), 41, 0);
        KnowledgeCompletionOutcomeReceipt Create(float after) => new(key, 2, "knowledge-00001", "기억 분석",
            "facility:knowledge-encoding", "연구소", use, codex ? "" : "region:knowledge-encoding",
            codex ? "" : "변경", codex ? "제공된 기억 단서" : "", codex ? 0 : 97, after, input);
        bool invalidRejected = false;
        try { _ = Create(20); } catch (ArgumentException) { invalidRejected = true; }
        Require(invalidRejected, "invented reward magnitude rejected");
        var receipt = Create(codex ? 1 : 100);
        var prepare = recorder.TryPrepare(receipt, out var token);
        Require(prepare.Success, "completion encoding prepare: " + prepare.DetailCode);
        var commit = recorder.CommitPrepared(token, 41, out var committed);
        Require(commit.Success, "completion encoding commit: " + commit.DetailCode);
        var saved = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(JsonUtility.ToJson(ledger.CaptureGameplayOutcomes()));
        var restored = new GameplayOutcomeLedger(registry, GameplayOutcomeBufferLimits.Default);
        var candidate = restored.PrepareGameplayOutcomeRestore(saved);
        restored.BeginRestoreCandidate();
        restored.PublishGameplayOutcomeRestore(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        var delivery = new GameplayOutcomeRecorder(restored, registry, new GameEventBus());
        Require(delivery.RetryPendingDeliveries(1) == 1 && delivery.RetryPendingDeliveries(1) == 0,
            "pending completion JSON publishes once");
        Require(restored.TryGetExact(committed.OutcomeId, out var exact)
            && exact.facts.Any(f => f.factId == KnowledgeCompletionOutcomeIds.SourceStackFact.Value && f.value == stack),
            "exact physical source survives outbox JSON");
        Require(restored.TryProject(committed.OutcomeId,
            new NarrativePerspectiveContext(new GameplayEntityId(KnowledgeCompletionOutcomeIds.FacilityKind,
                "facility:knowledge-encoding"), NarrativePerspectiveKind.Facility, "ko-KR"), out var view)
            && view.OutcomeId.Equals(committed.OutcomeId) && view.Text.Contains(codex ? "도감 단서" : "정보망 약화"),
            "facility view retains canonical completion and actual reward meaning");
    }

    private static void VerifyTaskAllocationPersistence()
    {
        var state = new KnowledgeResidueAggregateState();
        Require(state.AllocateTaskSequence() == 1 && state.AllocateTaskSequence() == 2, "monotonic task allocation");
        var payload = new DungeonResearchSaveData
        {
            nextKnowledgeTaskSequence = state.CaptureNextTaskSequence,
            knowledgeTaskIdentityGeneration =
                state.CaptureTaskIdentityGeneration
        };
        var loaded = JsonUtility.FromJson<DungeonResearchSaveData>(JsonUtility.ToJson(payload));
        var restored = new KnowledgeResidueAggregateState();
        restored.RestoreAllocationIdentity(
            loaded.nextKnowledgeTaskSequence,
            loaded.knowledgeTaskIdentityGeneration);
        Require(restored.AllocateTaskSequence() == 3 && restored.DeepClone().AllocateTaskSequence() == 4,
            "empty queue JSON and aggregate clone cannot reuse completed task IDs");
        var legacy = new KnowledgeResidueAggregateState();
        legacy.RestoreAllocationIdentity(0);
        Require(legacy.CanAllocateTaskSequence
            && legacy.CaptureNextTaskSequence == 1
            && legacy.CaptureTaskIdentityGeneration
                == KnowledgeResidueTaskIdentity.MigratedGeneration
            && legacy.AllocateTaskId() == "knowledge-g2-00001"
            && legacy.DeepClone().AllocateTaskId() == "knowledge-g2-00002",
            "unknown legacy history must move to the disjoint migrated namespace");
        restored.AddRestoredTask(new KnowledgeResidueTaskSaveData
        {
            taskId = "knowledge-00009"
        });
        RequireThrows(() => restored.RestoreAllocationIdentity(
            9,
            KnowledgeResidueTaskIdentity.OriginalGeneration),
            "counter must exceed every active task ID in its generation");
        RequireThrows(() => restored.RestoreAllocationIdentity(-1), "negative counter rejected");
        restored.RestoreAllocationIdentity(int.MaxValue);
        RequireThrows(() => restored.AllocateTaskSequence(), "exhaustion cannot wrap task identity");
    }

    private static void RequireThrows(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (InvalidOperationException) { rejected = true; }
        Require(rejected, message);
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Knowledge reward preparation: " + message); }
}
