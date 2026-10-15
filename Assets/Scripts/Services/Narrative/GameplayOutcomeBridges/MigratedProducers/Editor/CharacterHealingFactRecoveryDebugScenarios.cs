#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class MigratedProducerCombatDefenseEntryE2EDebugScenarios
{
    public static string VerifyHealingFactRecoveryFocused()
    {
        const MigratedProducerOutcomeKind kind = MigratedProducerOutcomeKind.CharacterBodyHealthRecoveredEvent;
        foreach (var failure in new[] { MigratedProducerOutcomeFaultMode.RejectReservation, MigratedProducerOutcomeFaultMode.RejectCommit })
        {
            using var harness = new OutcomeHarness(kind, "healing-fact-rejection", false, failure, false);
            using var fixture = new HealthEntryFixture(harness, kind);
            string before = fixture.CaptureDomainState();
            bool rejected = false;
            try { fixture.InvokeProductionEntry(); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && before == fixture.CaptureDomainState() && harness.Facts.ActiveLeaseCount == 0
                && harness.Ledger.GetDiagnostics().ReservedCount == 0,
                "Rejected healing fact changed health/pending state or leaked a reservation.");
        }
        VerifyHealingReplyLoss(false);
        VerifyHealingReplyLoss(true);
        return "healing-facts/atomic-two-transitions/rejection-rollback/unknown-before-and-after/json-new-owner/replay-without-effects/canonical-torn-rejection=PASS";
    }

    private static void VerifyHealingReplyLoss(bool commitFirst)
    {
        const MigratedProducerOutcomeKind kind = MigratedProducerOutcomeKind.CharacterBodyHealthRecoveredEvent;
        string id = "character:healing-fact-" + (commitFirst ? "after" : "before");
        using var harness = new OutcomeHarness(kind, id, false, MigratedProducerOutcomeFaultMode.None, false);
        using var fixture = new HealthEntryFixture(harness, kind, id);
        fixture.HealingHealth.ConfigureFactPublication(new HealingReplyLoss(harness.Facts, commitFirst), harness.Facts);
        Require(fixture.InvokeProductionEntry(), "Uncertain healing response undid its actual health effect.");
        var body = fixture.HealingHealth.Capture();
        var ledger = harness.Ledger.CaptureGameplayOutcomes();
        var counters = harness.Transaction.Capture();
        Require(body.pendingHealingFacts.Count == 1
            && body.pendingHealingFacts[0].criticalPublicationKey.Length > 0
            && body.pendingHealingFacts[0].recoveryPublicationKey.Length > 0
            && harness.Facts.ActiveLeaseCount == 0,
            "Two-transition healing lost its frozen atomic cohort.");
        string json = JsonUtility.ToJson(body);
        CharacterBodyHealthSaveSection.ValidateBodyFactJson(json);
        var malformed = JObject.Parse(json);
        ((JObject)malformed["pendingHealingFacts"][0]["fact"]).Remove("healthBefore");
        bool rejected = false;
        try { CharacterBodyHealthSaveSection.ValidateBodyFactJson(malformed.ToString()); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Healing save accepted missing frozen health.");
        CheckHealingJoin(body, ledger, counters, true);
        var tampered = JsonUtility.FromJson<DungeonCharacterBodyHealthSaveData>(json);
        tampered.pendingHealingFacts[0].criticalPublicationKey += ":wrong";
        CheckHealingJoin(tampered, ledger, counters, false);
        if (commitFirst)
        {
            tampered = JsonUtility.FromJson<DungeonCharacterBodyHealthSaveData>(json);
            tampered.pendingHealingFacts[0].fact.characterName += "변조";
            CheckHealingJoin(tampered, ledger, counters, false);
            var torn = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(JsonUtility.ToJson(ledger));
            Require(torn.exactOutcomes.Count == 2, "Healing did not atomically publish its two transitions.");
            torn.exactOutcomes.RemoveAt(0);
            CheckHealingJoin(body, torn, counters, false);
        }
        // Both aggregate owners are new. This is a controlled DTO restore, not a natural AI/world-save test.
        using var resumedHarness = new OutcomeHarness(kind, id + ":restored", false, MigratedProducerOutcomeFaultMode.None, false);
        using var resumed = new HealthEntryFixture(resumedHarness, kind, id);
        var candidate = resumedHarness.Ledger.PrepareGameplayOutcomeRestore(ledger);
        resumedHarness.Ledger.BeginRestoreCandidate(); resumedHarness.Ledger.PublishGameplayOutcomeRestore(candidate);
        resumedHarness.Ledger.PublishRestoreCandidate(); resumedHarness.Ledger.CompleteRestoreCandidate();
        resumedHarness.Transaction.Restore(resumedHarness.Transaction.PrepareRestore(counters));
        resumed.HealingHealth.PublishRestore(resumed.HealingHealth.PrepareRestore(JsonUtility.FromJson<DungeonCharacterBodyHealthSaveData>(json)));
        string stateBeforeReplay = JsonUtility.ToJson(resumed.HealingHealth.Capture().characters.Single());
        resumed.HealingActor.name = "변경된 현재 이름";
        resumed.HealingHealth.RecoverPendingHealingFacts();
        resumed.HealingHealth.RecoverPendingHealingFacts();
        Require(resumed.HealingHealth.Capture().pendingHealingFacts.Count == 0
            && stateBeforeReplay == JsonUtility.ToJson(resumed.HealingHealth.Capture().characters.Single())
            && resumedHarness.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 2,
            "Healing replay changed body state or duplicated observations.");
        CheckHealingJoin(body, resumedHarness.Ledger.CaptureGameplayOutcomes(), resumedHarness.Transaction.Capture(), true);
    }

    private static void CheckHealingJoin(DungeonCharacterBodyHealthSaveData body, GameplayOutcomeLedgerSaveData ledger,
        DungeonMigratedProducerOutcomeSaveData counters, bool expected)
    {
        var report = new DungeonGameRestoreReport();
        CharacterBodyFactPreflight.Validate(body, ledger, counters, report);
        Require(report.Success == expected, "Unexpected healing canonical join: " + string.Join(";", report.Errors));
    }

    private sealed class HealingReplyLoss : IDomainFactPublisher
    {
        private readonly DomainFactDispatcher inner;
        private readonly bool commitFirst;
        internal HealingReplyLoss(DomainFactDispatcher inner, bool commitFirst)
        { this.inner = inner; this.commitFirst = commitFirst; }
        public bool TryReserve<T>(in T proposed, out DomainFactLease lease, out string failure) => inner.TryReserve(proposed, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T actual) => inner.Publish(lease, actual);
        public DomainFactDelivery Replay<T>(in T fact) => inner.Replay(fact);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure)
        {
            if (commitFirst) inner.PublishBatch(facts, results, out _);
            else foreach (var fact in facts) inner.Cancel(fact.Lease);
            failure = "injected-healing-reply-loss";
            for (int i = 0; i < results.Length; i++) results[i] = DomainFactDelivery.Uncertain(failure);
            return true;
        }
    }
}
#endif
