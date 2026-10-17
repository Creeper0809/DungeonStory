#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class FacilitySynthesisDebugScenarios
{
    public static void RunObserversForBatchMode()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qa = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts", "QA")) + Path.DirectorySeparatorChar;
        Require(Application.isBatchMode && !string.IsNullOrWhiteSpace(output) && Path.IsPathRooted(output)
            && Path.GetFullPath(output).StartsWith(qa, StringComparison.OrdinalIgnoreCase) && !Directory.Exists(output),
            "Requires fresh project-local isolated evidence.");
        string probe = File.ReadAllText(Path.Combine(Directory.GetParent(Path.GetFullPath(output)).FullName, "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE") && probe.Contains("visible=false")
            && probe.Contains("windowCreatedAndDestroyed=true") && probe.Contains("inJob=true"), "Missing isolation probe.");
        VerifySynthesisObserverRecovery();
        Require(FacilitySynthesisOutcomeDebugScenarios.RunAll(true), "Native synthesis projection regression failed.");
        Require(VerifyStrategySynthesis() && VerifyDeclaredAnchorWinsSelectionOrder()
            && VerifyRetargetCommitFailureRestoresAllMaterials() && VerifyOutcomeCommitFailureRestoresAllMaterials()
            && VerifyObserverFailureDoesNotRepeatDomain(), "Actual synthesis regression failed.");
        Require(RunCausalSynthesisOwnerFamily(true), "Two-stage synthesis restore regression failed.");
        Debug.Log("B99_SYNTHESIS_ACTUAL_OWNER_OBSERVER_AND_RESTORE=PASS");
    }

    private static void VerifySynthesisObserverRecovery()
    {
        const string owner = "facility-synthesis:b99:ack-loss";
        JObject checkpoint;
        string hash;
        long sequence;
        using (var first = new CausalSynthesisContext("run:b99:first", owner, null))
        {
            first.Observations.LoseNextAcknowledgement = true;
            Require(first.ExecuteFirst(), "Actual synthesis did not retain committed replacement after ACK loss.");
            Require(first.Observations.HiddenAcknowledgements == 1 && first.Observations.Outbox.Capture().pending.Count == 1,
                "Did not reach the actual native commit ACK-loss boundary.");
            var identity = first.RequireFirstOutcome();
            hash = identity.CanonicalPayloadHash;
            sequence = first.Observations.Ledger.CaptureGameplayOutcomes().nextSequence;
            checkpoint = JObject.Parse(first.CaptureOwnerCheckpoint().ToString(Formatting.None));
        }
        using (var restored = new CausalSynthesisContext("run:b99:fresh", owner, checkpoint))
        {
            var pending = restored.Observations.Outbox.Capture();
            Require(pending.pending.Count == 1 && !restored.ExecuteFirst(), "Fresh owner repeated domain synthesis.");
            var data = pending.pending.Single();
            var mutated = data.Clone(); mutated.fact.inheritedLevel++;
            bool conflict = false;
            try { FacilitySynthesisFactPreflight.ValidateObservation(mutated, restored.Observations.Ledger.CaptureGameplayOutcomes()); }
            catch (InvalidOperationException) { conflict = true; }
            Require(conflict, "Tampered frozen fact was accepted.");
            var missing = JObject.Parse(DungeonCharacterWorldSaveCodec.SerializeFields(pending));
            ((JObject)missing["pending"][0]["fact"]).Remove("absoluteDay");
            bool missingRejected = false;
            try { FacilitySynthesisFactSaveSection.ValidateJson(missing.ToString(Formatting.None)); }
            catch (InvalidOperationException) { missingRejected = true; }
            Require(missingRejected, "Missing frozen day was accepted.");

            restored.Observations.Disconnect();
            Require(!restored.Observations.Outbox.RecoverPending(out _) && restored.Observations.Outbox.Capture().pending.Count == 1,
                "Missing observer discarded pending synthesis.");
            restored.Observations.Connect();
            bool duplicateRejected = false;
            try { restored.Observations.Connect(); } catch (InvalidOperationException) { duplicateRejected = true; }
            Require(duplicateRejected, "Duplicate synthesis observer was accepted.");
            restored.Observations.Outbox.OnRestoreCompleted();
            Require(restored.Observations.Outbox.Capture().pending.Count == 0
                && restored.RequireFirstOutcome().CanonicalPayloadHash == hash
                && restored.Observations.Ledger.CaptureGameplayOutcomes().nextSequence == sequence
                && restored.Observations.Outbox.RecoverPending(out _) && !restored.ExecuteFirst(),
                "Synthesis recovery changed canonical identity or repeated effects.");
            Require(!restored.Observations.Outbox.TryBegin(new FacilitySynthesisCompletedFact(data.fact), out _, out _),
                "An already-observed source fact allowed another physical synthesis.");
            Require(restored.ExecuteSecond(), "Next actual synthesis did not proceed after recovery.");
        }
        Debug.Log("B99_SYNTHESIS_ACK_LOSS_STRICT_FRESH_OWNER_REPLAY=PASS");
    }

    private sealed class SynthesisObservationFixture : IDomainFactStagingPublisher, IDisposable
    {
        internal readonly DomainFactDispatcher Dispatcher = new();
        internal readonly GameplayOutcomeLedger Ledger;
        internal readonly FacilitySynthesisFactOutbox Outbox;
        private readonly IDomainFactObserverBinding binding;
        private IDisposable connection;
        internal bool RejectCommit, LoseNextAcknowledgement;
        internal int HiddenAcknowledgements;
        internal SynthesisObservationFixture(GameplayOutcomeRegistry registry = null,
            GameplayOutcomeLedger ledger = null, IGameEventBus events = null)
        {
            registry ??= new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[]
                { new FacilitySynthesisOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new FacilitySynthesisOutcomeAdapter() });
            Ledger = ledger ?? new GameplayOutcomeLedger(registry, GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId("run:b99:fixture"), 1L);
            var recorder = new GameplayOutcomeRecorder(Ledger, registry, events ?? new GameEventBus());
            binding = FacilitySynthesisFactObserver.CreateBinding(registry, Ledger, recorder,
                new GameplayDomainFactCommitGroup(recorder, Ledger));
            Connect(); Outbox = new FacilitySynthesisFactOutbox(this, Dispatcher);
        }
        internal void Connect()
        {
            var candidate = binding.Connect(Dispatcher);
            Require(connection == null, "Existing observer subscription was silently replaced.");
            connection = candidate;
        }
        internal void Disconnect() { connection?.Dispose(); connection = null; }
        public bool TryStage<TFact>(in TFact fact, out IDomainFactStage stage, out string failure)
        {
            stage = null;
            if (!Dispatcher.TryStage(fact, out var inner, out failure)) return false;
            stage = new FaultStage(this, inner); return true;
        }
        private sealed class FaultStage : IDomainFactStage
        {
            private readonly SynthesisObservationFixture owner;
            private readonly IDomainFactStage inner;
            internal FaultStage(SynthesisObservationFixture owner, IDomainFactStage inner)
            { this.owner = owner; this.inner = inner; }
            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;
            public void Cancel() => inner.Cancel();
            public DomainFactDelivery Commit()
            {
                if (owner.RejectCommit) return DomainFactDelivery.Reject("fixture-forced-outcome-rejection");
                var result = inner.Commit();
                if (owner.LoseNextAcknowledgement && result.Completed)
                { owner.LoseNextAcknowledgement = false; owner.HiddenAcknowledgements++;
                    return DomainFactDelivery.Uncertain("fixture-hidden-real-native-ack"); }
                return result;
            }
        }
        public void Dispose() { Disconnect(); Dispatcher.Dispose(); }
    }
}
#endif
