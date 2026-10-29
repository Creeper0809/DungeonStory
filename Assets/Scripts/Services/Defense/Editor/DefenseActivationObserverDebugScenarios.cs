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
    public static void RunDefenseActivationObserversForBatchMode()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qa = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts", "QA")) + Path.DirectorySeparatorChar;
        Require(Application.isBatchMode && !string.IsNullOrWhiteSpace(output) && Path.IsPathRooted(output)
            && Path.GetFullPath(output).StartsWith(qa, StringComparison.OrdinalIgnoreCase) && !Directory.Exists(output),
            "Requires fresh project-local isolated evidence.");
        string probe = File.ReadAllText(Path.Combine(Directory.GetParent(Path.GetFullPath(output)).FullName, "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE") && probe.Contains("visible=false")
            && probe.Contains("windowCreatedAndDestroyed=true") && probe.Contains("inJob=true"), "Missing isolation probe.");
        Debug.Log(RunD06DefenseFacilityEffectFocused());
        VerifyB94Recovery(false);
        VerifyB94Recovery(true);
        VerifyB94RejectedActivation();
        VerifyB94ActivityLifetime();
        var observed = new HashSet<MigratedProducerOutcomeKind>();
        VerifyKinds(observed, new[] { MigratedProducerOutcomeKind.DefenseFacilityTriggeredEvent,
            MigratedProducerOutcomeKind.DefenseFacilityStateChangedEvent }, false,
            (harness, _) => new DefenseFacilityEntryFixture(harness));
        Debug.Log("B94_DEFENSE_ACTIVATION_PRODUCTION_ENTRY=PASS");
        Debug.Log("B94_DEFENSE_ACTIVATION_OBSERVERS=PASS");
    }

    private static void VerifyB94RejectedActivation()
    {
        using var scenario = new D06Scenario("activation-reject", rejectActivation: true);
        scenario.SetEffects(scenario.CreateDamage(8));
        float health = scenario.TargetHealth;
        var before = scenario.FacilityBefore;
        Require(scenario.Trigger() == null && scenario.TargetHealth == health && scenario.StatusCount == 0
            && scenario.FacilityAfter.ActivationCount == before.ActivationCount
            && scenario.FacilityAfter.Condition == before.Condition && scenario.FacilityAfter.Supply == before.Supply
            && scenario.Publications.Capture().observations.Count == 0
            && scenario.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 0, "Rejected activation leaked effects.");
        Require(scenario.Trigger() != null && scenario.Publications.Capture().observations.Count == 1,
            "Rejected activation did not release its reservation/cooldown for a real retry.");
    }

    private static void VerifyB94Recovery(bool effectAck)
    {
        using var scenario = new D06Scenario("ack-" + effectAck, hideActivationAck: !effectAck, hideEffectAck: effectAck);
        scenario.SetEffects(scenario.CreateDamage(6), scenario.CreateCorrosion(0.15f, 5));
        float health = scenario.TargetHealth;
        var report = scenario.Trigger();
        var saved = scenario.Publications.Capture();
        Require(report != null && Approximately(health - scenario.TargetHealth, 6)
            && scenario.StatusCount == 1 && saved.observations.Count == 1
            && saved.observations[0].pending == !effectAck && saved.observations[0].effectPending == effectAck,
            "Actual commit ACK loss did not preserve effects and pending proof.");
        string json = DungeonCharacterWorldSaveCodec.SerializeFields(saved);
        DefenseActivationPublicationSaveSection.ValidateJson(json);
        saved = DungeonSaveJson.Deserialize<DefenseActivationPublicationSaveData>(json);
        var owner = JsonUtility.FromJson<DefenseFacilitySaveData>(JsonUtility.ToJson(scenario.Runtime.CaptureState()));
        var counters = JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(JsonUtility.ToJson(scenario.Migrated.Capture()));
        var canonical = scenario.Ledger.CaptureGameplayOutcomes();
        Require(canonical.exactOutcomes.Count == 3, "Activation/effect did not produce exactly three native results.");
        var originalEffect = scenario.RequireEffect();
        var bad = DungeonSaveJson.Deserialize<DefenseActivationPublicationSaveData>(json);
        bad.observations[0].effect.failure = "altered";
        B94ExpectInvalid(() => DefenseActivationFactPreflight.ValidateJournal(bad, canonical, counters));
        bad = DungeonSaveJson.Deserialize<DefenseActivationPublicationSaveData>(json);
        bad.observations.Clear();
        B94ExpectInvalid(() => DefenseActivationFactPreflight.ValidateOwners(bad, owner));
        var raw = JObject.Parse(json); ((JObject)raw["observations"][0]["activation"]).Remove("absoluteDay");
        B94ExpectInvalid(() => DefenseActivationPublicationSaveSection.ValidateJson(raw.ToString()));

        // New observation, native revision and facility owners; target body/status remain the real applied owners.
        var ledger = scenario.CreateEmptyLedger();
        var candidate = ledger.PrepareGameplayOutcomeRestore(canonical);
        ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
        ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
        var recorder = new GameplayOutcomeRecorder(ledger, scenario.Registry, new GameEventBus());
        var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
        migrated.Restore(migrated.PrepareRestore(counters));
        var group = new GameplayDomainFactCommitGroup(recorder, ledger);
        using var facts = new DomainFactDispatcher();
        var activationObserver = new DefenseActivationFactObserver(migrated, group);
        var proposal = new DefenseActivationFact(saved.observations[0].activation, true);
        Require(!facts.TryReserve(proposal, out _, out _), "Missing activation observer accepted a proposal.");
        var connection = activationObserver.Connect(facts);
        B94ExpectInvalid(() => activationObserver.Connect(facts));
        connection.Dispose();
        Require(!facts.TryReserve(proposal, out _, out _), "Disposed activation observer remained subscribed.");
        using var connected = activationObserver.Connect(facts);
        var effectObserver = DefenseEffectFactMapping.CreateBinding(scenario.Registry, ledger, recorder, group);
        using var effectConnection = effectObserver.Connect(facts);
        B94ExpectInvalid(() => effectObserver.Connect(facts));
        var journal = new DefenseActivationPublicationRuntime(facts, facts, facts);
        journal.Restore(saved);
        var restored = new DefenseFacilityRuntime(UnavailableEquipmentPhysicalItemGateway.Instance, new NoopDefensePhysicalGateway(),
            new AlwaysReadyDefenseInputOwner(), new UnityGameClock(), new GameEventBus(), new AlwaysPoweredQuery(),
            new NoopDefenseFacilityNetwork(), new EmptyFacilityCapabilities(), new DungeonRuntimeAggregateRootStore(),
            new R30DefensePreparationBenefits(1f));
        restored.PublishRestoreState(restored.PrepareRestoreState(owner));
        float appliedHealth = scenario.TargetHealth;
        Require(journal.RecoverPending(out string failure) && journal.RecoverPending(out failure),
            "Fresh observation recovery failed:" + failure);
        Require(journal.Capture().observations.All(row => !row.pending && !row.effectPending)
            && ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 3 && scenario.TargetHealth == appliedHealth
            && scenario.StatusCount == 1 && restored.GetSnapshot(scenario.Facility).ActivationCount == 1,
            "Recovery repeated physical activation or effects.");
        var exact = ledger.CaptureGameplayOutcomes().exactOutcomes.Single(row => row.producerId == DefenseFacilityEffectOutcomeIds.ProducerId);
        Require(exact.immutablePayloadHash == originalEffect.immutablePayloadHash && exact.parentSequence == originalEffect.parentSequence
            && exact.sequence == originalEffect.sequence, "Recovery changed native identity or parent.");
        DefenseActivationFactPreflight.ValidateOwners(journal.Capture(), restored.CaptureState());
        DefenseActivationFactPreflight.ValidateJournal(journal.Capture(), ledger.CaptureGameplayOutcomes(), migrated.Capture());
        Debug.Log("B94_DEFENSE_" + (effectAck ? "EFFECT" : "ACTIVATION") + "_ACK_RECOVERY=PASS");
    }

    private static void B94ExpectInvalid(Action action)
    {
        bool rejected = false;
        try { action(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or Newtonsoft.Json.JsonException)
        { rejected = true; }
        Require(rejected, "Invalid defense observation contract accepted.");
    }

    private static void VerifyB94ActivityLifetime()
    {
        using var scenario = new D06Scenario("activity-lifetime");
        scenario.SetEffects(scenario.CreateDamage(1));
        var report = scenario.Trigger();
        var events = new GameEventBus();
        using var observer = new DefenseActivationActivityObserver(events, new B94Characters(scenario.Target));
        int before = scenario.Target.LogComponent.ActivityEntries.Count;
        int received = 0;
        scenario.Target.LogComponent.OnLogAdded += Count;
        void Count(CharacterLogEntry _) => received++;
        try
        {
        observer.Initialize(); observer.Initialize();
        events.Publish(new DefenseFacilityTriggeredEvent(report));
        Require(received == 1 && scenario.Target.LogComponent.ActivityEntries.Count == before + 1,
            "Activity observer duplicated or lost the target entry.");
        observer.Dispose();
        events.Publish(new DefenseFacilityTriggeredEvent(report));
        Require(received == 1 && scenario.Target.LogComponent.ActivityEntries.Count == before + 1,
            "Disposed activity observer still emitted.");
        observer.Initialize();
        events.Publish(new DefenseFacilityTriggeredEvent(report));
        // CharacterLog intentionally aggregates consecutive identical entries.
        Require(received == 2 && scenario.Target.LogComponent.ActivityEntries.Count == before + 1,
            "Activity observer did not reconnect exactly once or broke repeated-entry aggregation.");
        }
        finally { scenario.Target.LogComponent.OnLogAdded -= Count; }
    }

    private sealed class B94Characters : ICharacterLifetimeQuery
    {
        public B94Characters(CharacterActor actor) => AllCharacters = new[] { actor };
        public int LifetimeCharacterVersion => 1;
        public IReadOnlyList<CharacterActor> AllCharacters { get; }
    }

    private sealed class B94PublicationFault : IDomainFactPublisher, IDomainFactStagingPublisher
    {
        private readonly DomainFactDispatcher inner;
        internal bool HideActivation, HideEffect, RejectActivation;
        internal B94PublicationFault(DomainFactDispatcher inner) => this.inner = inner;
        public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure) => inner.TryReserve(fact, out lease, out failure);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
        public DomainFactDelivery Replay<T>(in T fact) => inner.Replay(fact);
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure) =>
            inner.PublishBatch(facts, results, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact)
        {
            if (RejectActivation) { RejectActivation = false; return DomainFactDelivery.Reject("injected-activation-rejection"); }
            var result = inner.Publish(lease, fact);
            if (HideActivation && result.Completed) { HideActivation = false; return DomainFactDelivery.Uncertain("injected-activation-ack-loss"); }
            return result;
        }
        public bool TryStage<T>(in T fact, out IDomainFactStage stage, out string failure)
        {
            if (!inner.TryStage(fact, out stage, out failure)) return false;
            stage = new Stage(this, stage); return true;
        }
        private sealed class Stage : IDomainFactStage
        {
            private readonly B94PublicationFault owner;
            private readonly IDomainFactStage inner;
            public Stage(B94PublicationFault owner, IDomainFactStage inner) { this.owner = owner; this.inner = inner; }
            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;
            public void Cancel() => inner.Cancel();
            public DomainFactDelivery Commit()
            {
                var result = inner.Commit();
                if (owner.HideEffect && result.Completed) { owner.HideEffect = false; return DomainFactDelivery.Uncertain("injected-effect-ack-loss"); }
                return result;
            }
        }
    }
}
#endif
