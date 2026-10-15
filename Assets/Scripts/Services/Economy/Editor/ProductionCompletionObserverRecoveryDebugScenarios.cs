#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class ProductionPreparedOutputFullPersistenceDebugScenarios
{
    public static void RunCompletionObserverRecoveryForBatch()
    {
        VerifyProductionCompletedOutcomeFromRuntime();
        Debug.Log("B125_PRODUCTION_COMPLETION_OWNER=PASS");
        foreach (bool committed in new[] { false, true }) VerifyCompletionAckRecovery(committed);
        MigratedProducerCharacterSurvivalEntryE2EDebugScenarios.VerifyProductionLossObserverFocused();
        Require(RunCausalProductionOwnerFamily(), "Production owner-family causal restore failed.");
        Debug.Log("B125_PRODUCTION_COMPLETION_CAUSAL_RESTORE=PASS");
        Debug.Log("B125_PRODUCTION_COMPLETION_OBSERVERS=PASS");
    }

    private static void VerifyCompletionAckRecovery(bool committed)
    {
        string json, physicalBefore;
        using (var probe = new CompletionObserverProbe(committed))
        using (var source = new RuntimeGraph("completion-ack-source", 7210, SawmillScenario,
                   completionObservations: probe.Transport, completionRecovery: probe.Transport))
        {
            source.SeedOwnedCurrentFormatBill();
            var record = source.SingleBill;
            source.PrepareWip(record);
            var initial = source.Production.Capture();
            bool boundariesVerified = false;
            probe.Transport.AllowRecovery = false;
            probe.Transport.BeforeCommit = () =>
            {
                CommandMustThrow(() => source.Production.Capture());
                CommandMustThrow(() => source.Production.BuildRestore(initial));
                CommandMustThrow(() => source.Production.SetSuspended(record.billId, true));
                CommandMustThrow(() => source.Production.ExecuteWork(source.WorkerHandle, source.FacilityHandle, record.billId, 1));
                boundariesVerified = true;
            };
            var result = source.Production.ExecuteWork(source.WorkerHandle, source.FacilityHandle, record.billId, float.MaxValue);
            Require(!result.Succeeded && boundariesVerified, "Completion ACK loss or publication re-entry fence was not exercised.");
            var owner = source.Production.Capture();
            var ledger = probe.Ledger.CaptureGameplayOutcomes();
            var pending = owner.bills.Single().completionObservations.Single();
            Require(record.cycleSequence == 1 && record.preparedOutput.phase == ProductionPreparedOutputPhase.PhysicalBatchCommittedPublicationPending
                && source.WorldItems.GetAllStacks().Count > 0 && pending.fact.absoluteDay == 137
                && pending.fact.workerDisplayName == source.WorkerHandle.DisplayName
                && pending.fact.facilityDisplayName == source.FacilityHandle.DisplayName,
                "Unconfirmed completion lost its physical batch, first names/day or pending fact: cycle=" + record.cycleSequence
                + ";phase=" + record.preparedOutput.phase + ";stacks=" + source.WorldItems.GetAllStacks().Count
                + ";day=" + pending.fact.absoluteDay + ";worker=" + pending.fact.workerDisplayName
                + ";facility=" + pending.fact.facilityDisplayName);
            ProductionCompletionFactPreflight.Validate(owner, ledger);
            physicalBefore = CompletionPhysicalSignature(source);
            var raw = JObject.Parse(JsonUtility.ToJson(owner));
            ((JObject)raw["bills"][0]["completionObservations"][0]["fact"]).Remove("absoluteDay");
            CommandMustThrow(() => ProductionBillsSaveSection.ValidateCommandObservationsJson(raw.ToString()));
            var tampered = JsonUtility.FromJson<DungeonProductionBillSaveData>(JsonUtility.ToJson(owner));
            tampered.bills[0].completionObservations[0].fact.lines[0].quantity++;
            CommandMustThrow(() => ProductionCompletionFactPreflight.Validate(tampered, ledger));
            if (committed)
            {
                tampered = JsonUtility.FromJson<DungeonProductionBillSaveData>(JsonUtility.ToJson(owner));
                tampered.bills[0].completionObservations[0].fact.absoluteDay++;
                CommandMustThrow(() => ProductionCompletionFactPreflight.Validate(tampered, ledger));
            }
            Require(!source.Production.SetSuspended(record.billId, true).Succeeded,
                "A later command bypassed a pending physical completion.");
            Require(!source.Production.ExecuteWork(source.WorkerHandle, source.FacilityHandle, record.billId, float.MaxValue).Succeeded
                && CompletionPhysicalSignature(source) == physicalBefore, "A pending completion duplicated its physical outputs.");
            json = JsonUtility.ToJson(new CommandCheckpoint
            { sections = source.Registry.CaptureAll().Select(CloneEnvelope).ToList(), ledger = ledger, owner = owner });
        }
        using (var probe = new CompletionObserverProbe(null))
        using (var target = new RuntimeGraph("completion-ack-restored", 7211, SawmillScenario,
                   completionObservations: probe.Transport, completionRecovery: probe.Transport))
        {
            var checkpoint = JsonUtility.FromJson<CommandCheckpoint>(json);
            ProductionCompletionFactPreflight.Validate(checkpoint.owner, checkpoint.ledger);
            var candidate = probe.Ledger.PrepareGameplayOutcomeRestore(checkpoint.ledger);
            probe.Ledger.BeginRestoreCandidate(); probe.Ledger.PublishGameplayOutcomeRestore(candidate);
            probe.Ledger.PublishRestoreCandidate(); probe.Ledger.CompleteRestoreCandidate();
            var report = new DungeonGameRestoreReport();
            Require(target.Registry.RestoreAll(checkpoint.sections, report) && report.Success,
                "Fresh completion physical/facility/owner restore failed: " + string.Join(" | ", report.Errors));
            Require(CompletionPhysicalSignature(target) == physicalBefore, "Restore changed the pending physical output batch.");
            target.CommandCalendar.SetDateTime(190, 9);
            var result = target.Production.ExecuteWork(target.WorkerHandle, target.FacilityHandle, target.SingleBill.billId, float.MaxValue);
            Require(result.Succeeded && result.CycleCompleted && target.SingleBill.cycleSequence == 2
                && target.SingleBill.CompletionObservation == null && CompletionPhysicalSignature(target) == physicalBefore,
                "Completion recovery did not advance once while preserving the original output: " + result.Failure.Code
                + ":" + string.Join(",", result.Failure.Parameters.ToArray()));
            var ledger = probe.Ledger.CaptureGameplayOutcomes();
            var row = ledger.exactOutcomes.Concat(ledger.outbox).Single();
            var fact = checkpoint.owner.bills[0].completionObservations.Single().fact;
            Require(row.absoluteDay == 137 && row.ownerRevision == 1
                && row.participants.Any(value => value.displayText == fact.workerDisplayName)
                && row.participants.Any(value => value.displayText == fact.facilityDisplayName),
                "Completion recovery changed its first names/day or identity.");
            if (committed)
            {
                var original = checkpoint.ledger.exactOutcomes.Concat(checkpoint.ledger.outbox).Single();
                Require(row.sequence == original.sequence && row.immutablePayloadHash == original.immutablePayloadHash
                    && ledger.nextSequence == checkpoint.ledger.nextSequence, "Recovery created a second completion identity.");
            }
            ProductionCompletionFactPreflight.Validate(target.Production.Capture(), ledger);
            target.Production.ExecuteWork(target.WorkerHandle, target.FacilityHandle, target.SingleBill.billId, float.MaxValue);
            Require(CompletionPhysicalSignature(target) == physicalBefore
                && probe.Ledger.CaptureGameplayOutcomes().nextSequence == ledger.nextSequence,
                "Completed cycle replay duplicated its physical output or observation.");
        }
        Debug.Log("B125_COMPLETION_ACK_" + (committed ? "AFTER" : "BEFORE") + "_JSON_FRESH_OWNER=PASS");
    }

    private static string CompletionPhysicalSignature(RuntimeGraph graph) => string.Join("|", graph.WorldItems.GetAllStacks()
        .OrderBy(value => value.StackId, StringComparer.Ordinal).Select(value => value.StackId + ":" + value.Quantity));

    private sealed class CompletionObserverProbe : IDisposable
    {
        private readonly DomainFactDispatcher facts = new();
        internal readonly GameplayOutcomeLedger Ledger;
        internal readonly CommandAckTransport Transport;
        internal CompletionObserverProbe(bool? committed)
        {
            var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[] { new ProductionCompletedOutcomeDescriptor() },
                new IGameplayOutcomeAdapterRegistration[] { new ProductionCompletedOutcomeAdapter() });
            Ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(), new GameplayOutcomeRunId("run:b125-production-completion"), 1);
            var recorder = new GameplayOutcomeRecorder(Ledger, registry, new GameEventBus());
            facts.Subscribe("production.completion-observer", new GameplayDomainFactObserver<ProductionCycleCompletedFact, ProductionCompletedOutcomeReceipt>(
                ProductionCompletionFactObservation.Capture, registry, Ledger, recorder, new GameplayDomainFactCommitGroup(recorder, Ledger)));
            Transport = new CommandAckTransport(facts, committed);
        }
        public void Dispose() => facts.Dispose();
    }
}
#endif
