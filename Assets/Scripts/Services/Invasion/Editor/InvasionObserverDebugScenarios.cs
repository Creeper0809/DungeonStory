#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class MigratedProducerCombatDefenseEntryE2EDebugScenarios
{
    public static void RunInvasionObserversForBatchMode()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qa = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts", "QA")) + Path.DirectorySeparatorChar;
        Require(Application.isBatchMode && !string.IsNullOrWhiteSpace(output) && Path.IsPathRooted(output)
            && Path.GetFullPath(output).StartsWith(qa, StringComparison.OrdinalIgnoreCase) && !Directory.Exists(output),
            "Requires fresh project-local isolated evidence.");
        string probe = File.ReadAllText(Path.Combine(Directory.GetParent(Path.GetFullPath(output)).FullName, "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE") && probe.Contains("visible=false")
            && probe.Contains("windowCreatedAndDestroyed=true") && probe.Contains("inJob=true"), "Missing isolation probe.");
        VerifyInvasionObserverFocused();
        Debug.Log("B95_INVASION_PRODUCTION_AND_CAUSAL_RESTORE=PASS");
        VerifyB95AckLoss(InvasionLifecycleStage.Started, MigratedProducerOutcomeKind.InvasionStartedEvent);
        VerifyB95AckLoss(InvasionLifecycleStage.FacilityDamaged, MigratedProducerOutcomeKind.InvasionFacilityDamagedEvent);
        VerifyB95AckLoss(InvasionLifecycleStage.Resolved, MigratedProducerOutcomeKind.InvasionResolvedEvent);
        VerifyB95ActivityLifetime();
        using (var milestone = new InvasionLifecycleContext("run:b95:milestone", null, null, milestonePressure: true))
            _ = milestone.RunG06MilestonePressure();
        Debug.Log("B95_INVASION_MILESTONE_LINEAGE=PASS");
        Debug.Log("B95_INVASION_OBSERVERS=PASS");
    }

    private static void VerifyB95AckLoss(InvasionLifecycleStage stage, MigratedProducerOutcomeKind kind)
    {
        using var harness = new OutcomeHarness(kind, "b95-ack", false,
            MigratedProducerOutcomeFaultMode.None, false, hideInvasionAck: stage);
        using IEntryFixture entry = stage == InvasionLifecycleStage.Started
            ? new InvasionDirectorEntryFixture(harness) : new InvasionIntruderEntryFixture(harness, kind);
        string before = entry.CaptureDomainState();
        Require(Invoke(entry, out var error), "ACK-loss production entry failed" + FailureSuffix(error));
        string applied = entry.CaptureDomainState();
        Require(before != applied, "ACK-loss did not apply actual invasion state.");
        string json = DungeonCharacterWorldSaveCodec.SerializeFields(harness.InvasionFacts.Capture());
        InvasionFactPublicationSaveSection.ValidateJson(json);
        var saved = DungeonSaveJson.Deserialize<InvasionFactPublicationSaveData>(json);
        var target = saved.observations.Single(row => row.fact.stage == stage);
        Require(target.pending && target.publicationId == string.Empty && target.fingerprint == string.Empty,
            "The actual commit acknowledgement was not hidden.");
        var canonical = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(JsonUtility.ToJson(harness.Ledger.CaptureGameplayOutcomes()));
        var counters = JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(JsonUtility.ToJson(harness.Transaction.Capture()));
        InvasionFactPreflight.ValidateJournal(saved, canonical, counters);
        Require(GameplayDomainFactDeliveryQuery.TryReadKey(target.publicationKey, out var key)
            && harness.Ledger.TryGetResultIdentity(key, out _), "ACK-loss has no actual native commit.");
        harness.Ledger.TryGetResultIdentity(key, out var identity);

        var malformed = JObject.Parse(json);
        ((JObject)malformed["observations"][0]["fact"]).Remove("absoluteDay");
        ExpectB95Invalid(() => InvasionFactPublicationSaveSection.ValidateJson(malformed.ToString()));
        var tampered = InvasionFactPublicationRuntime.ValidateAndClone(saved);
        tampered.observations.Last().fact.riskAfter += 1f;
        ExpectB95Invalid(() => InvasionFactPreflight.ValidateJournal(tampered, canonical, counters));

        using var fresh = new OutcomeHarness(kind, "b95-fresh", false, MigratedProducerOutcomeFaultMode.None, false);
        var candidate = fresh.Ledger.PrepareGameplayOutcomeRestore(canonical);
        fresh.Ledger.BeginRestoreCandidate(); fresh.Ledger.PublishGameplayOutcomeRestore(candidate);
        fresh.Ledger.PublishRestoreCandidate(); fresh.Ledger.CompleteRestoreCandidate();
        fresh.Transaction.Restore(fresh.Transaction.PrepareRestore(counters));
        fresh.InvasionFacts.Restore(saved);
        Require(fresh.InvasionFacts.RecoverPending(out string failure), "Fresh pending recovery failed:" + failure);
        Require(fresh.InvasionFacts.RecoverPending(out failure), "Repeated recovery failed:" + failure);
        var recovered = fresh.InvasionFacts.Capture().observations.Single(row => row.fact.stage == stage);
        Require(!recovered.pending && recovered.publicationId == identity.OutcomeId.ToString()
            && recovered.fingerprint == identity.CanonicalPayloadHash
            && fresh.Ledger.TryGetResultIdentity(key, out var replayed) && replayed.OutcomeId == identity.OutcomeId
            && fresh.Ledger.CaptureGameplayOutcomes().nextSequence == canonical.nextSequence
            && entry.CaptureDomainState() == applied && harness.Probe.CommitCount(kind) == 1,
            "Fresh ACK recovery changed native identity, repeated a mutation or duplicated the result.");
        InvasionFactPreflight.ValidateJournal(fresh.InvasionFacts.Capture(), fresh.Ledger.CaptureGameplayOutcomes(), fresh.Transaction.Capture());
        Debug.Log("B95_INVASION_ACK_RECOVERY_" + stage + "=PASS");
    }
    private static void ExpectB95Invalid(Action action)
    {
        bool rejected = false;
        try { action(); }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or Newtonsoft.Json.JsonException) { rejected = true; }
        Require(rejected, "Invalid invasion observation state accepted.");
    }
    private static void VerifyB95ActivityLifetime()
    {
        using var harness = new OutcomeHarness(MigratedProducerOutcomeKind.InvasionResolvedEvent, "b95-activity", false,
            MigratedProducerOutcomeFaultMode.None, false);
        var events = new GameEventBus();
        var actorObject = CreatePreparedIntruderObject(harness.InvasionFacts, new Grid(4, 1), events, out var runtime);
        using var observer = new InvasionActivityObserver(events);
        int received = 0;
        void Count(CharacterLogEntry _) => received++;
        runtime.IntruderActor.LogComponent.OnLogAdded += Count;
        try
        {
            var fact = new InvasionSuppressedEvent(runtime.IntruderActor, null);
            observer.Initialize(); observer.Initialize(); events.Publish(fact);
            Require(received == 1, "Invasion activity duplicated/lost its first notification.");
            observer.Dispose(); events.Publish(fact);
            Require(received == 1, "Disposed invasion activity observer still emitted.");
            observer.Initialize(); events.Publish(fact);
            Require(received == 2, "Reconnected invasion activity observer did not emit exactly once.");
        }
        finally
        { runtime.IntruderActor.LogComponent.OnLogAdded -= Count; UnityEngine.Object.DestroyImmediate(actorObject); }
        Debug.Log("B95_INVASION_ACTIVITY_LIFETIME=PASS");
    }

    private sealed class B95AckLossPublisher : IDomainFactPublisher
    {
        private readonly DomainFactDispatcher inner;
        private readonly InvasionLifecycleStage target;
        private bool hidden;
        internal B95AckLossPublisher(DomainFactDispatcher inner, InvasionLifecycleStage target)
        { this.inner = inner; this.target = target; }
        public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure) => inner.TryReserve(fact, out lease, out failure);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
        public DomainFactDelivery Replay<T>(in T fact) => inner.Replay(fact);
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure) =>
            inner.PublishBatch(facts, results, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact)
        {
            var result = inner.Publish(lease, fact);
            if (!hidden && result.Completed && fact is InvasionLifecycleFact invasion && invasion.Snapshot().stage == target)
            { hidden = true; return DomainFactDelivery.Uncertain("injected-invasion-ack-loss"); }
            return result;
        }
    }
}
#endif
