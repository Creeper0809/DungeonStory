#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class ProductionPreparedOutputFullPersistenceDebugScenarios
{
    public static void RunCommandObserverRecoveryForBatch()
    {
        Require(ProductionCommandOutcomeDebugScenarios.RunAll(), "Production command observer mapping failed.");
        Debug.Log("B124_COMMAND_OBSERVER_MAPPING=PASS");
        VerifyProductionCommandOutcomeTransactions();
        Debug.Log("B124_COMMAND_OWNER_TRANSACTIONS=PASS");
        foreach (bool committed in new[] { false, true }) VerifyCommandAckRecovery(committed);
        Require(RunCausalProductionOwnerFamily(), "Production causal owner family failed.");
        Debug.Log("B124_COMMAND_CAUSAL_OWNER_RESTORE=PASS");
        Debug.Log("B124_COMMAND_OBSERVER_RECOVERY=PASS");
    }

    private static void VerifyCommandAckRecovery(bool committed)
    {
        string json, frozenName;
        using (var observations = new CommandObserverProbe(committed))
        using (var source = new RuntimeGraph("command-ack-source", 7197, SawmillScenario,
                   observations.Transport, observations.Transport))
        {
            source.SeedOwnedCurrentFormatBill();
            var before = source.Production.Capture();
            bool boundariesVerified = false;
            observations.Transport.BeforeCommit = () =>
            {
                CommandMustThrow(() => source.Production.Capture());
                CommandMustThrow(() => source.Production.BuildRestore(before));
                CommandMustThrow(() => source.Production.SetSuspended(source.SingleBill.billId, false));
                boundariesVerified = true;
            };
            Require(!source.Production.SetSuspended(source.SingleBill.billId, true).Succeeded,
                "Lost ACK was reported as confirmed success.");
            Require(boundariesVerified, "Capture/restore/command re-entry was not fenced.");
            var owner = source.Production.Capture();
            var ledger = observations.Ledger.CaptureGameplayOutcomes();
            Require(source.SingleBill.suspended && owner.nextCommandSequence == 2
                && owner.pendingCommandObservations.Count == 1, "Unknown commit lost its state/fact.");
            frozenName = owner.pendingCommandObservations[0].fact.facilityDisplayName;
            Require(frozenName == source.FacilityHandle.DisplayName
                && owner.pendingCommandObservations[0].fact.absoluteDay == 137, "First name/day was not captured.");
            ProductionCommandFactPreflight.Validate(owner, ledger);
            var missing = JObject.Parse(JsonUtility.ToJson(owner));
            ((JObject)missing["pendingCommandObservations"][0]["fact"]["before"]["workerPolicy"]).Remove("sortMode");
            CommandMustThrow(() => ProductionBillsSaveSection.ValidateCommandObservationsJson(missing.ToString()));
            if (committed)
            {
                var tampered = JsonUtility.FromJson<DungeonProductionBillSaveData>(JsonUtility.ToJson(owner));
                tampered.pendingCommandObservations[0].fact.absoluteDay++;
                CommandMustThrow(() => ProductionCommandFactPreflight.Validate(tampered, ledger));
                tampered = JsonUtility.FromJson<DungeonProductionBillSaveData>(JsonUtility.ToJson(owner));
                tampered.pendingCommandObservations[0].fact.after.suspended = false;
                CommandMustThrow(() => ProductionCommandFactPreflight.Validate(tampered, ledger));
            }
            json = JsonUtility.ToJson(new CommandCheckpoint
            { sections = source.Registry.CaptureAll().Select(CloneEnvelope).ToList(), ledger = ledger, owner = owner });
            observations.Transport.AllowRecovery = false;
            Require(!source.Production.SetSuspended(source.SingleBill.billId, false).Succeeded
                && JsonUtility.ToJson(source.Production.Capture()) == JsonUtility.ToJson(owner),
                "A later command bypassed the pending observation.");
        }
        using (var observations = new CommandObserverProbe(null))
        using (var target = new RuntimeGraph("command-ack-restored", 7198, SawmillScenario,
                   observations.Transport, observations.Transport))
        {
            var checkpoint = JsonUtility.FromJson<CommandCheckpoint>(json);
            ProductionCommandFactPreflight.Validate(checkpoint.owner, checkpoint.ledger);
            var candidate = observations.Ledger.PrepareGameplayOutcomeRestore(checkpoint.ledger);
            observations.Ledger.BeginRestoreCandidate();
            observations.Ledger.PublishGameplayOutcomeRestore(candidate);
            observations.Ledger.PublishRestoreCandidate(); observations.Ledger.CompleteRestoreCandidate();
            var report = new DungeonGameRestoreReport();
            Require(target.Registry.RestoreAll(checkpoint.sections, report) && report.Success,
                "Fresh physical/facility/production section restore failed: " + string.Join(" | ", report.Errors));
            target.CommandCalendar.SetDateTime(180, 9);
            int version = target.Production.Version;
            Require(target.Production.SetSuspended(target.SingleBill.billId, true).Succeeded
                && target.Production.Version == version, "Recovery reapplied the command.");
            var owner = target.Production.Capture();
            var ledger = observations.Ledger.CaptureGameplayOutcomes();
            ProductionCommandFactPreflight.Validate(owner, ledger);
            var row = ledger.exactOutcomes.Concat(ledger.outbox).Single();
            Require(owner.nextCommandSequence == 2 && owner.pendingCommandObservations.Count == 0
                && row.absoluteDay == 137 && row.ownerRevision == 1
                && row.participants.Single().displayText == frozenName,
                "Recovered command identity, name, day, or pending state differs.");
            if (committed)
            {
                var original = checkpoint.ledger.exactOutcomes.Concat(checkpoint.ledger.outbox).Single();
                Require(row.sequence == original.sequence && row.immutablePayloadHash == original.immutablePayloadHash
                    && ledger.nextSequence == checkpoint.ledger.nextSequence, "Recovery duplicated the original row.");
            }
            var rewind = JsonUtility.FromJson<DungeonProductionBillSaveData>(JsonUtility.ToJson(owner));
            rewind.nextCommandSequence = 1;
            CommandMustThrow(() => ProductionCommandFactPreflight.Validate(rewind, ledger));
            Require(target.Production.SetSuspended(target.SingleBill.billId, false).Succeeded
                && target.Production.Capture().nextCommandSequence == 3, "Next command did not resume.");
            Require(observations.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count
                + observations.Ledger.CaptureGameplayOutcomes().outbox.Count == 2, "Next command count differs.");
        }
        Debug.Log("B124_COMMAND_ACK_" + (committed ? "AFTER" : "BEFORE") + "_JSON_FRESH_OWNER=PASS");
    }

    [Serializable] private sealed class CommandCheckpoint
    {
        public List<DungeonSaveSectionEnvelope> sections;
        public GameplayOutcomeLedgerSaveData ledger;
        public DungeonProductionBillSaveData owner;
    }

    private sealed class CommandObserverProbe : IDisposable
    {
        private readonly DomainFactDispatcher facts = new();
        internal readonly GameplayOutcomeLedger Ledger;
        internal readonly CommandAckTransport Transport;
        internal CommandObserverProbe(bool? committed)
        {
            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { new ProductionCommandOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new ProductionCommandOutcomeAdapter() });
            Ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(),
                new GameplayOutcomeRunId("run:b124-production-command"), 1L);
            var recorder = new GameplayOutcomeRecorder(Ledger, registry, new GameEventBus());
            facts.Subscribe("production.command-observer", new GameplayDomainFactObserver<ProductionCommandAppliedFact, ProductionCommandOutcomeReceipt>(
                ProductionCommandFactObservation.Capture, registry, Ledger, recorder, new GameplayDomainFactCommitGroup(recorder, Ledger)));
            Transport = new CommandAckTransport(facts, committed);
        }
        public void Dispose() => facts.Dispose();
    }

    private sealed class CommandAckTransport : IDomainFactStagingPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner; private readonly bool? committed;
        internal Action BeforeCommit; internal bool AllowRecovery = true;
        internal CommandAckTransport(DomainFactDispatcher inner, bool? committed) { this.inner = inner; this.committed = committed; }
        public bool TryStage<T>(in T fact, out IDomainFactStage stage, out string failure)
        {
            if (!inner.TryStage(fact, out var prepared, out failure)) { stage = null; return false; }
            stage = new Stage(this, prepared); return true;
        }
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> requests, DomainFactDelivery[] results, out string failure)
        {
            if (!AllowRecovery) { failure = "fixture:recovery-unavailable"; return false; }
            return inner.ReplayBatch(requests, results, out failure);
        }
        private sealed class Stage : IDomainFactStage
        {
            private readonly CommandAckTransport transport; private readonly IDomainFactStage inner;
            internal Stage(CommandAckTransport transport, IDomainFactStage inner) { this.transport = transport; this.inner = inner; }
            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;
            public DomainFactDelivery Commit()
            {
                transport.BeforeCommit?.Invoke();
                if (!transport.committed.HasValue) return inner.Commit();
                if (transport.committed.Value) Require(inner.Commit().Completed, "Real observer commit failed before lost ACK.");
                return DomainFactDelivery.Uncertain("fixture:lost-ack");
            }
            public void Cancel() => inner.Cancel();
        }
    }

    private static void CommandMustThrow(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected explicit command validation failure.");
    }
}
#endif

