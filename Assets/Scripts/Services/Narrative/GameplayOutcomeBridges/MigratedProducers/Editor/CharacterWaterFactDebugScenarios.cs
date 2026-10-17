#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Synthetic observer/ledger fixture only. It does not exercise natural drinking,
// water depletion, character health mutation, or notification presentation.
public static class CharacterWaterFactDebugScenarios
{
    public static string Run()
    {
        VerifyCleanDeliveryAndLifetime();
        VerifyFoulRecoveryAndSaveGuards();
        VerifyUnknownPublication(false);
        VerifyUnknownPublication(true);
        return "synthetic-water-observer/clean/foul-atomic/cancel/unsubscribe/cold-recovery/dedup/save-guards/unknown-before-after/notification-once=PASS;natural-water-and-health=NOT-RUN";
    }

    private static void VerifyUnknownPublication(bool afterCommit)
    {
        var fixture = new MigratedProducerOutcomeEditorFixture("run:water-fact:unknown:" + afterCommit);
        using var dispatcher = Connect(fixture);
        var events = new GameEventBus();
        int notifications = 0;
        using var subscription = events.Subscribe<CharacterWaterConsumedEvent>(_ => notifications++);
        var owner = new CharacterDeprivationStateStore(fixture.AggregateRootStore);
        var publisher = new UnknownWaterPublisher(dispatcher, afterCommit);
        var outbox = new CharacterWaterPublicationOutbox(owner, publisher, dispatcher, events);
        var fact = Fact("unknown-" + afterCommit, WorldWaterQuality.Foul);
        var lease = outbox.Reserve(Proposal(fact));
        string key = lease.PublicationKey;
        var result = outbox.Publish(lease, fact);
        Require(result.Phase == DomainFactDeliveryPhase.InDoubt && result.OperationKey == key
            && dispatcher.ActiveLeaseCount == 0 && notifications == 0
            && owner.TryGetWaterPublication(fact.Data.characterId, out var pending)
            && pending.publicationKey == key, "Unknown water response lost its frozen fact or reservation.");
        Require(RowCount(fixture.Ledger.CaptureGameplayOutcomes()) == (afterCommit ? 2 : 0),
            "The injected unknown boundary did not execute at the intended side of commit.");
        var survival = new DungeonDarkSurvivalSaveData { pendingWaterPublications = owner.CaptureWaterPublications() };
        CheckJoin(survival, fixture.Ledger.CaptureGameplayOutcomes(), fixture.Transaction.Capture(), true);
        Require(outbox.Deliver(fact.Data.characterId, out string failure) && notifications == 1
            && !owner.TryGetWaterPublication(fact.Data.characterId, out _), failure);
        long sequence = fixture.Ledger.CaptureGameplayOutcomes().nextSequence;
        Require(RowCount(fixture.Ledger.CaptureGameplayOutcomes()) == 2
            && outbox.Deliver(fact.Data.characterId, out failure) && notifications == 1
            && fixture.Ledger.CaptureGameplayOutcomes().nextSequence == sequence,
            "Water recovery repeated the gameplay notification or allocated new rows.");
    }

    private sealed class UnknownWaterPublisher : IDomainFactPublisher
    {
        private readonly DomainFactDispatcher inner;
        private readonly bool afterCommit;
        internal UnknownWaterPublisher(DomainFactDispatcher inner, bool afterCommit)
        { this.inner = inner; this.afterCommit = afterCommit; }
        public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure) =>
            inner.TryReserve(fact, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact)
        {
            if (afterCommit) Require(inner.Publish(lease, fact).Completed, "Unknown fixture commit failed.");
            throw new InvalidOperationException("controlled-water-response-loss");
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure) =>
            inner.PublishBatch(facts, results, out failure);
        public DomainFactDelivery Replay<T>(in T fact) => inner.Replay(fact);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }

    private static void VerifyCleanDeliveryAndLifetime()
    {
        var fixture = new MigratedProducerOutcomeEditorFixture("run:water-fact:clean");
        using var dispatcher = new DomainFactDispatcher();
        IDisposable subscription = Binding(fixture).Connect(dispatcher);
        CharacterWaterConsumedFact actual = Fact("clean", WorldWaterQuality.Clean);
        Require(dispatcher.TryReserve(Proposal(actual), out DomainFactLease lease, out string failure), failure);
        string key = lease.PublicationKey;
        DomainFactDelivery delivered = dispatcher.Publish(lease, actual);
        Require(delivered.Completed && delivered.OperationKey == key
            && !GameplayDomainFactPairProof.TryReadKeys(key, out _, out _)
            && fixture.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 1,
            "Clean water did not publish exactly one row.");

        long sequence = fixture.Ledger.CaptureGameplayOutcomes().nextSequence;
        string owners = JsonUtility.ToJson(fixture.Transaction.Capture());
        var results = new DomainFactDelivery[1];
        Require(dispatcher.ReplayBatch(new[] { DomainFactReplay.Create(key, actual) }, results, out failure)
            && results[0].Completed
            && fixture.Ledger.CaptureGameplayOutcomes().nextSequence == sequence
            && JsonUtility.ToJson(fixture.Transaction.Capture()) == owners,
            "Clean water duplicate delivery allocated another identity.");

        CharacterWaterConsumedFact cancelled = Fact("cancelled", WorldWaterQuality.Clean);
        Require(dispatcher.TryReserve(Proposal(cancelled), out DomainFactLease cancelledLease, out failure), failure);
        dispatcher.Cancel(cancelledLease);
        Require(!cancelledLease.IsValid && dispatcher.ActiveLeaseCount == 0
            && fixture.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 1
            && JsonUtility.ToJson(fixture.Transaction.Capture()) == owners,
            "Cancelling a water reservation changed durable state.");

        Require(dispatcher.TryReserve(Proposal(cancelled), out DomainFactLease disconnectedLease, out failure), failure);
        subscription.Dispose();
        Require(!disconnectedLease.IsValid && dispatcher.ActiveLeaseCount == 0
            && !dispatcher.TryReserve(Proposal(cancelled), out _, out failure)
            && failure.StartsWith("domain-fact-observer-missing:", StringComparison.Ordinal),
            "Unsubscribing did not cancel the active water reservation.");
    }

    private static void VerifyFoulRecoveryAndSaveGuards()
    {
        var original = new MigratedProducerOutcomeEditorFixture("run:water-fact:foul");
        using var dispatcher = Connect(original);
        CharacterWaterConsumedFact actual = Fact("foul", WorldWaterQuality.Foul);
        Require(dispatcher.TryReserve(Proposal(actual), out DomainFactLease lease, out string failure), failure);
        string key = lease.PublicationKey;
        DomainFactDelivery delivered = dispatcher.Publish(lease, actual);
        GameplayOutcomeLedgerSaveData ledger = original.Ledger.CaptureGameplayOutcomes();
        Require(delivered.Completed && delivered.OperationKey == key
            && GameplayDomainFactPairProof.TryReadKeys(key, out _, out _)
            && ledger.exactOutcomes.Count == 2,
            "Foul water did not atomically publish its two-row cohort.");

        var survival = new DungeonDarkSurvivalSaveData();
        survival.pendingWaterPublications.Add(new CharacterWaterPublicationData
        { publicationKey = key, fact = actual.Snapshot() });
        survival = JsonUtility.FromJson<DungeonDarkSurvivalSaveData>(JsonUtility.ToJson(survival));
        ledger = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(JsonUtility.ToJson(ledger));
        DungeonMigratedProducerOutcomeSaveData counters = JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(
            JsonUtility.ToJson(original.Transaction.Capture()));
        CheckJoin(survival, ledger, counters, true);
        VerifyRawShapeRejectsMissingFactField(survival);

        DungeonDarkSurvivalSaveData dispatchedWithoutLedger = JsonUtility.FromJson<DungeonDarkSurvivalSaveData>(
            JsonUtility.ToJson(survival));
        dispatchedWithoutLedger.pendingWaterPublications[0].notificationDispatched = true;
        var empty = new MigratedProducerOutcomeEditorFixture("run:water-fact:empty");
        CheckJoin(dispatchedWithoutLedger, empty.Ledger.CaptureGameplayOutcomes(),
            empty.Transaction.Capture(), false);

        GameplayOutcomeLedgerSaveData torn = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(
            JsonUtility.ToJson(ledger));
        Require(torn.exactOutcomes.Count == 2, "Foul fixture did not contain two exact rows.");
        torn.exactOutcomes.RemoveAt(1);
        CheckJoin(survival, torn, counters, false);

        var restored = new MigratedProducerOutcomeEditorFixture("run:water-fact:restored");
        var candidate = restored.Ledger.PrepareGameplayOutcomeRestore(ledger);
        restored.Ledger.BeginRestoreCandidate();
        restored.Ledger.PublishGameplayOutcomeRestore(candidate);
        restored.Ledger.PublishRestoreCandidate();
        restored.Ledger.CompleteRestoreCandidate();
        restored.Transaction.Restore(restored.Transaction.PrepareRestore(counters));
        using var replay = Connect(restored);
        CharacterWaterPublicationData saved = survival.pendingWaterPublications[0];
        var frozen = new CharacterWaterConsumedFact(saved.fact);
        long sequence = restored.Ledger.CaptureGameplayOutcomes().nextSequence;
        int rows = RowCount(restored.Ledger.CaptureGameplayOutcomes());
        string owners = JsonUtility.ToJson(restored.Transaction.Capture());
        var results = new DomainFactDelivery[1];
        for (int attempt = 0; attempt < 2; attempt++)
        {
            Require(replay.ReplayBatch(new[] { DomainFactReplay.Create(saved.publicationKey, frozen) },
                    results, out failure)
                && results[0].Completed && results[0].OperationKey == saved.publicationKey
                && restored.Ledger.CaptureGameplayOutcomes().nextSequence == sequence
                && RowCount(restored.Ledger.CaptureGameplayOutcomes()) == rows
                && JsonUtility.ToJson(restored.Transaction.Capture()) == owners,
                "Cold water replay allocated a duplicate identity.");
        }

        CharacterWaterFactData changed = saved.fact.Clone();
        changed.moodAfter += 1f;
        Require(!replay.ReplayBatch(new[]
            {
                DomainFactReplay.Create(saved.publicationKey, new CharacterWaterConsumedFact(changed))
            }, results, out failure)
            && results[0].Phase == DomainFactDeliveryPhase.Rejected
            && restored.Ledger.CaptureGameplayOutcomes().nextSequence == sequence
            && RowCount(restored.Ledger.CaptureGameplayOutcomes()) == rows,
            "Canonical water fact tampering was accepted.");
    }

    private static CharacterWaterConsumedFact Fact(string suffix, WorldWaterQuality quality) => new(
        new CharacterWaterFactData
        {
            characterId = "character:water-" + suffix,
            characterName = "합성 생존자",
            sourceId = "water:source-" + suffix,
            sourceKind = CharacterWaterSourceKind.WorldWater,
            quality = quality,
            pathogenDiseaseId = quality == WorldWaterQuality.Clean ? string.Empty : "disease:waterborne",
            absoluteDay = 4,
            consumed = 1f,
            thirstBefore = 5f,
            thirstAfter = 60f,
            healthBefore = 90f,
            healthAfter = quality == WorldWaterQuality.Clean ? 90f : 85f,
            hygieneBefore = 60f,
            hygieneAfter = quality == WorldWaterQuality.Clean ? 60f : 48f,
            infectionBefore = 0f,
            infectionAfter = quality == WorldWaterQuality.Clean ? 0f : 22f,
            moodBefore = 50f,
            moodAfter = quality == WorldWaterQuality.Clean ? 50f : 43f
        });

    private static CharacterWaterConsumedFact Proposal(CharacterWaterConsumedFact actual)
    {
        CharacterWaterFactData value = actual.Data;
        return new CharacterWaterConsumedFact(new CharacterWaterFactData
        {
            characterId = value.characterId,
            characterName = value.characterName,
            sourceId = value.sourceId,
            sourceKind = value.sourceKind,
            quality = value.quality,
            pathogenDiseaseId = value.pathogenDiseaseId,
            absoluteDay = value.absoluteDay,
            consumed = 0f
        }, proposalOnly: true);
    }

    private static CharacterWaterFactBinding Binding(MigratedProducerOutcomeEditorFixture fixture) => new(
        new CharacterWaterFactObserver(fixture.Transaction,
            new GameplayDomainFactCommitGroup(fixture.Recorder, fixture.Ledger)));

    private static DomainFactDispatcher Connect(MigratedProducerOutcomeEditorFixture fixture) => new(
        new IDomainFactObserverBinding[] { Binding(fixture) });

    private static void VerifyRawShapeRejectsMissingFactField(DungeonDarkSurvivalSaveData survival)
    {
        JObject raw = JObject.Parse(JsonUtility.ToJson(survival));
        ((JObject)raw["pendingWaterPublications"][0]["fact"]).Remove("moodAfter");
        bool rejected = false;
        try { DarkSurvivalSaveSection.RequireWaterPublicationShape(raw.ToString()); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "A missing raw water fact field silently defaulted.");
    }

    private static void CheckJoin(DungeonDarkSurvivalSaveData survival,
        GameplayOutcomeLedgerSaveData ledger, DungeonMigratedProducerOutcomeSaveData counters, bool expected)
    {
        var report = new DungeonGameRestoreReport();
        CharacterWaterFactPreflight.Validate(survival, ledger, counters, report);
        Require(report.Success == expected, "Water canonical join produced an unexpected result.");
    }

    private static int RowCount(GameplayOutcomeLedgerSaveData value) =>
        value.outbox.Count + value.exactOutcomes.Count + value.tombstones.Count;

    private static void Require(bool condition, string detail)
    {
        if (!condition) throw new InvalidOperationException(detail);
    }
}
#endif
