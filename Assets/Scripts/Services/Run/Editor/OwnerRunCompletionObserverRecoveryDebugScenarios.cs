#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Context = OwnerRunCompletionCausalProductionEntryDebugScenarios.Context;

public static class OwnerRunCompletionObserverRecoveryDebugScenarios
{
    public static void RunForBatch()
    {
        OwnerRunObservationDebugScenarios.RunOrThrow();
        if (!OwnerRunCompletionCausalProductionEntryDebugScenarios.RunCausalOwnerFamily())
            throw new InvalidOperationException("Owner completion causal regression failed.");
        Debug.Log("B109_OWNER_RUN_CAUSAL=PASS");
        VerifyRecovery(false);
        VerifyRecovery(true);
        Debug.Log("B109_OWNER_RUN_RECOVERY=PASS");
        VerifyFrozenSettlement();
        Debug.Log("B110_OWNER_RUN_FROZEN_SETTLEMENT=PASS");
    }

    private static void VerifyRecovery(bool beforeCommit)
    {
        string ownerId = "owner-run:b109:" + (beforeCommit ? "before" : "after");
        string flowJson;
        string metaJson;
        GameplayOutcomeLedgerSaveData ledger;
        DungeonMigratedProducerOutcomeSaveData counters;
        const string reason = "세 번째 운영일의 통제된 런 종료";
        using (var source = new Context(ownerId))
        {
            var transport = new Transport(source.DomainFacts.Dispatcher)
            { ThrowBeforeCommit = beforeCommit, HideAcknowledgement = !beforeCommit };
            int guards = 0;
            transport.BeforePublish = () =>
            {
                ExpectInvalid(() => source.FlowSave.Capture());
                ExpectInvalid(() => source.Manager.ResumePendingRunCompletion());
                Require(!source.Manager.CompleteRun(DungeonRunOutcome.Victory, reason),
                    "Completion reentered during publication.");
                guards += 3;
            };
            source.BindCompletionTransport(transport, transport);
            Require(source.Manager.CompleteRun(DungeonRunOutcome.Victory, reason),
                "Uncertain observation rolled back the terminal owner.");
            Require(source.Manager.IsRunEnded && source.Stage == -1 && guards == 3,
                "Owner ended/settlement/fence state is wrong.");
            flowJson = source.FlowSave.Capture();
            metaJson = source.MetaSave.Capture();
            ledger = Copy(source.Outcomes.Ledger.CaptureGameplayOutcomes());
            counters = Copy(source.Outcomes.Transaction.Capture());
            Require(ledger.exactOutcomes.Count == (beforeCommit ? 0 : 1),
                "Wrong source completion count.");
            var flow = DungeonSaveJson.Deserialize<DungeonRunFlowSaveData>(flowJson);
            Require(flow.pendingCompletion != null, "Pending completion was not saved.");
            RunFlowSaveSection.ValidatePendingCompletionJson(flowJson);
            OwnerRunCompletionFactPreflight.ValidatePending(flow, ledger, counters);
            OwnerRunCompletionFactPreflight.ValidateSettlementJoin(flow,
                DungeonSaveJson.Deserialize<DungeonMetaProgressionSaveData>(metaJson));
            var missing = JObject.Parse(flowJson);
            ((JObject)missing["pendingCompletion"]["fact"]).Remove("absoluteDay");
            ExpectInvalid(() => RunFlowSaveSection.ValidatePendingCompletionJson(missing.ToString()));
            var changed = DungeonSaveJson.Deserialize<DungeonRunFlowSaveData>(flowJson);
            changed.pendingCompletion.publicationKey += ":changed";
            ExpectInvalid(() => OwnerRunCompletionFactPreflight.ValidatePending(changed, ledger, counters));
            if (!beforeCommit)
            {
                changed = DungeonSaveJson.Deserialize<DungeonRunFlowSaveData>(flowJson);
                changed.pendingCompletion.fact.reason += "-changed";
                ExpectInvalid(() => OwnerRunCompletionFactPreflight.ValidatePending(changed, ledger, counters));
            }
        }

        using (var restored = new Context(ownerId))
        {
            var native = restored.Outcomes.Ledger;
            var candidate = native.PrepareGameplayOutcomeRestore(ledger);
            native.BeginRestoreCandidate();
            native.PublishGameplayOutcomeRestore(candidate);
            native.PublishRestoreCandidate();
            native.CompleteRestoreCandidate();
            restored.Outcomes.Transaction.Restore(restored.Outcomes.Transaction.PrepareRestore(counters));
            var report = new DungeonGameRestoreReport();
            restored.MetaSave.Restore(metaJson, restored.MetaSave.SectionVersion, report);
            restored.FlowSave.Restore(flowJson, restored.FlowSave.SectionVersion, report);
            Require(report.Success, "Current run-flow JSON failed to restore.");
            // The real restore-completed hook projects this same terminal value.
            restored.Manager.RestoreRunEnded(true);
            var transport = new Transport(restored.DomainFacts.Dispatcher) { BlockRecovery = true };
            restored.BindCompletionTransport(transport, transport);
            string intact = restored.FlowSave.Capture();
            Require(!restored.Manager.ResumePendingRunCompletion()
                && restored.FlowSave.Capture() == intact && restored.Stage == -1,
                "Unavailable recovery altered the completed owner or awarded settlement.");
            transport.BlockRecovery = false;
            transport.BeforeReplay = () =>
            {
                ExpectInvalid(() => restored.FlowSave.Capture());
                ExpectInvalid(() => restored.Manager.ResumePendingRunCompletion());
            };
            Require(restored.Manager.ResumePendingRunCompletion(), "Saved fact did not recover.");
            restored.Validate();
            Require(restored.Stage == 1
                && DungeonSaveJson.Deserialize<DungeonRunFlowSaveData>(restored.FlowSave.Capture()).pendingCompletion == null,
                "Settlement or pending cleanup failed.");
            string ended = restored.FlowSave.Capture();
            var exact = native.CaptureGameplayOutcomes().exactOutcomes;
            Require(exact.Count == 2, "Completion or settlement duplicated.");
            if (!beforeCommit)
                Require(exact[0].sequence == ledger.exactOutcomes[0].sequence
                    && exact[0].immutablePayloadHash == ledger.exactOutcomes[0].immutablePayloadHash,
                    "Recovery changed the original completion identity.");
            Require(!restored.Manager.CompleteRun(DungeonRunOutcome.Victory, reason)
                && restored.Manager.ResumePendingRunCompletion()
                && restored.FlowSave.Capture() == ended
                && native.CaptureGameplayOutcomes().exactOutcomes.Count == 2,
                "Repeated completion/recovery duplicated owner effects.");
            // Rejected current-format restore must not partially publish.
            var invalid = JObject.Parse(ended);
            invalid["currentDay"] = 0;
            ExpectInvalid(() => restored.FlowSave.StageRestore(invalid.ToString(), restored.FlowSave.SectionVersion,
                new DungeonGameRestoreReport()));
            Require(restored.FlowSave.Capture() == ended, "Rejected restore changed live terminal state.");
        }
        Debug.Log("B109_OWNER_RUN_ACK_" + (beforeCommit ? "BEFORE" : "AFTER") + "=PASS");
    }

    private static void VerifyFrozenSettlement()
    {
        const string id = "owner-run:b110:frozen";
        const string reason = "종료 시점 정산 고정";
        string flowJson, metaJson;
        int expectedCurrency;
        GameplayOutcomeLedgerSaveData ledger;
        DungeonMigratedProducerOutcomeSaveData counters;
        var clock = new AdvancingClock();
        using (var source = new Context(id, clock: clock))
        {
            clock.Time = 60f;
            source.Meta.RecordResearchRecipes(new[] { "b110:before-completion" });
            var transport = new Transport(source.DomainFacts.Dispatcher) { HideAcknowledgement = true };
            source.BindCompletionTransport(transport, transport);
            Require(source.Manager.CompleteRun(DungeonRunOutcome.Victory, reason), "Completion did not become pending.");
            var frozen = source.Meta.PendingCompletion;
            Require(frozen != null && frozen.Result.survivalSeconds == 60f, "End time was not captured.");
            expectedCurrency = frozen.Result.legacyCurrency;
            Require(expectedCurrency == 123, "Expected 3 days + 60 seconds + one discovered recipe reward.");
            // Actual progression events after the terminal command must not enter its settlement.
            clock.Time = 3600f;
            source.Meta.RecordResearchRecipes(new[] { "b110:after-completion" });
            source.Meta.RecordOffenseSuccess();
            Require(source.Meta.RunProgress.ElapsedSeconds == 3600f, "Clock did not advance.");
            flowJson = source.FlowSave.Capture();
            metaJson = source.MetaSave.Capture();
            ledger = Copy(source.Outcomes.Ledger.CaptureGameplayOutcomes());
            counters = Copy(source.Outcomes.Transaction.Capture());
            var flow = DungeonSaveJson.Deserialize<DungeonRunFlowSaveData>(flowJson);
            var meta = DungeonSaveJson.Deserialize<DungeonMetaProgressionSaveData>(metaJson);
            OwnerRunCompletionFactPreflight.ValidateSettlementJoin(flow, meta);
            meta.pendingCompletion.fact.reason += "-wrong";
            ExpectInvalid(() => OwnerRunCompletionFactPreflight.ValidateSettlementJoin(flow, meta));
            foreach (string field in new[] { "runSequence", "preservedRecipeSlots", "fact", "result", "recipeCandidates" })
            {
                var missing = JObject.Parse(metaJson);
                ((JObject)missing["pendingCompletion"]).Remove(field);
                ExpectInvalid(() => MetaProgressionSaveSection.ValidatePendingCompletionJson(missing.ToString()));
            }
        }
        var restoreClock = new AdvancingClock { Time = 10000f };
        using var restored = new Context(id, clock: restoreClock);
        string untouched = restored.MetaSave.Capture();
        foreach (string field in new[] { "runSequence", "legacyCurrency" })
        {
            var invalid = JObject.Parse(metaJson);
            if (field == "runSequence") invalid["pendingCompletion"][field] = 2;
            else invalid["pendingCompletion"]["result"][field] = expectedCurrency + 1;
            ExpectInvalid(() => restored.MetaSave.StageRestore(invalid.ToString(), restored.MetaSave.SectionVersion,
                new DungeonGameRestoreReport()));
            Require(restored.MetaSave.Capture() == untouched, "Rejected settlement restore changed live state.");
        }
        var report = new DungeonGameRestoreReport();
        restored.MetaSave.Restore(metaJson, restored.MetaSave.SectionVersion, report);
        restored.FlowSave.Restore(flowJson, restored.FlowSave.SectionVersion, report);
        Require(report.Success, "Frozen settlement JSON restore failed.");
        var native = restored.Outcomes.Ledger;
        var candidate = native.PrepareGameplayOutcomeRestore(ledger);
        native.BeginRestoreCandidate();
        native.PublishGameplayOutcomeRestore(candidate);
        native.PublishRestoreCandidate();
        native.CompleteRestoreCandidate();
        restored.Outcomes.Transaction.Restore(restored.Outcomes.Transaction.PrepareRestore(counters));
        restored.Manager.RestoreRunEnded(true);
        restoreClock.Time += 7200f;
        Require(restored.Manager.ResumePendingRunCompletion(), "Frozen settlement replay failed.");
        var result = restored.Meta.LatestResult;
        Require(result != null && result.survivalSeconds == 60f && result.legacyCurrency == expectedCurrency
            && result.firstUnlockedRecipeCount == 1 && result.offenseSuccessCount == 0
            && restored.Meta.PendingCompletion == null && restored.Meta.State.CompletedRunCount == 1
            && restored.Meta.State.LifetimeEarnedCurrency == expectedCurrency,
            "Settlement used delayed inputs or awarded more than once.");
        Require(native.CaptureGameplayOutcomes().exactOutcomes.Count == 2
            && restored.Manager.ResumePendingRunCompletion()
            && restored.Meta.State.CompletedRunCount == 1,
            "Frozen settlement duplicated its owner or outcome.");
    }

    private sealed class AdvancingClock : IGameClock
    {
        public float Time { get; set; }
        public float DeltaTime => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private static T Copy<T>(T value) where T : class =>
        DungeonSaveJson.Deserialize<T>(JsonUtility.ToJson(value));
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    private static void ExpectInvalid(Action action)
    {
        bool rejected = false;
        try { action(); } catch (InvalidOperationException) { rejected = true; }
        if (!rejected) throw new InvalidOperationException("Expected invalid state rejection.");
    }

    private sealed class Transport : IDomainFactPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher dispatcher;
        internal Transport(DomainFactDispatcher dispatcher) => this.dispatcher = dispatcher;
        internal bool ThrowBeforeCommit, HideAcknowledgement, BlockRecovery;
        internal Action BeforePublish, BeforeReplay;
        public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure) =>
            dispatcher.TryReserve(fact, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact)
        {
            BeforePublish?.Invoke();
            if (ThrowBeforeCommit) throw new InvalidOperationException("B109 controlled pre-commit transport failure");
            var result = dispatcher.Publish(lease, fact);
            return HideAcknowledgement ? DomainFactDelivery.Uncertain("B109 controlled lost acknowledgement") : result;
        }
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> facts, DomainFactDelivery[] results, out string failure)
        {
            if (BlockRecovery) { failure = "B109 controlled unavailable recovery"; return false; }
            BeforeReplay?.Invoke();
            return dispatcher.ReplayBatch(facts, results, out failure);
        }
        public void Cancel(in DomainFactLease lease) => dispatcher.Cancel(lease);
        public DomainFactDelivery Replay<T>(in T fact) => dispatcher.Replay(fact);
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure) =>
            dispatcher.PublishBatch(facts, results, out failure);
    }
}
#endif
