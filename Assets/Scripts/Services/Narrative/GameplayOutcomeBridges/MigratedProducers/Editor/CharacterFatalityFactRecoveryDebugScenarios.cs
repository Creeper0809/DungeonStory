#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class MigratedProducerCombatDefenseEntryE2EDebugScenarios
{
    public static string VerifyFatalityFactRecoveryFocused()
    {
        const MigratedProducerOutcomeKind kind = MigratedProducerOutcomeKind.CharacterDeathEvent;
        foreach (var failure in new[] { MigratedProducerOutcomeFaultMode.RejectReservation, MigratedProducerOutcomeFaultMode.RejectCommit })
        {
            using var harness = new OutcomeHarness(kind, "fatality-fact-rejection", false, failure, false);
            using var fixture = new HealthEntryFixture(harness, kind);
            int deaths = 0;
            using var binding = fixture.HealingEvents.Subscribe<CharacterDeathEvent>(_ => deaths++);
            string before = fixture.CaptureDomainState();
            bool rejected = false;
            try { fixture.InvokeProductionEntry(); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && before == fixture.CaptureDomainState() && deaths == 0
                && harness.Facts.ActiveLeaseCount == 0 && harness.Ledger.GetDiagnostics().ReservedCount == 0,
                "Rejected fatality changed health/pending/lifecycle state or leaked a reservation.");
        }
        VerifyFatalityReplyLoss(false);
        VerifyFatalityReplyLoss(true);
        return "fatality-facts/atomic-transitions/rejection-rollback/unknown-before-and-after/json-new-owner/replay-without-death-effects/parent-source-join=PASS";
    }

    private static void VerifyFatalityReplyLoss(bool commitFirst)
    {
        const MigratedProducerOutcomeKind kind = MigratedProducerOutcomeKind.CharacterDeathEvent;
        string id = "character:fatality-fact-" + (commitFirst ? "after" : "before");
        using var harness = new OutcomeHarness(kind, id, false, MigratedProducerOutcomeFaultMode.None, false);
        using var fixture = new HealthEntryFixture(harness, kind, id);
        int deaths = 0;
        string notifiedParent = null;
        using var binding = fixture.HealingEvents.Subscribe<CharacterDeathEvent>(death =>
        { deaths++; notifiedParent = death.ParentPublicationKey; });
        // Same controlled reply-loss port as healing; production publication/ledger remains real.
        fixture.HealingHealth.ConfigureFactPublication(new HealingReplyLoss(harness.Facts, commitFirst), harness.Facts);
        Require(fixture.InvokeProductionEntry(), "Uncertain death response undid its actual fatal health effect.");
        var body = fixture.HealingHealth.Capture();
        var ledger = harness.Ledger.CaptureGameplayOutcomes();
        var counters = harness.Transaction.Capture();
        Require(body.pendingFatalityFacts.Count == 1, "Fatality lost its uncertain frozen cohort.");
        var pending = body.pendingFatalityFacts.Single();
        int expected = 1 + (pending.criticalPublicationKey.Length > 0 ? 1 : 0) + (pending.downedPublicationKey.Length > 0 ? 1 : 0);
        Require(expected >= 2 && pending.fact.healthBefore == 100f && pending.fact.healthAfter == 0f
            && body.characters.Single().currentHealth == 0f && deaths == 1
            && notifiedParent == pending.deathPublicationKey && harness.Facts.ActiveLeaseCount == 0,
            "Fatality lost its actual effect, exact parent reference, or one-shot notification.");
        fixture.InvokeProductionEntry();
        Require(deaths == 1 && fixture.HealingHealth.Capture().pendingFatalityFacts.Count == 1,
            "Repeated fatality command duplicated lifecycle effects or the pending fact.");
        string json = JsonUtility.ToJson(body);
        CharacterBodyHealthSaveSection.ValidateBodyFactJson(json);
        var missing = JObject.Parse(json);
        ((JObject)missing["pendingFatalityFacts"][0]["fact"]).Remove("reasonCode");
        bool rejected = false;
        try { CharacterBodyHealthSaveSection.ValidateBodyFactJson(missing.ToString()); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Fatality save accepted missing frozen cause detail.");
        CheckHealingJoin(body, ledger, counters, true);
        var tampered = JsonUtility.FromJson<DungeonCharacterBodyHealthSaveData>(json);
        tampered.pendingFatalityFacts[0].deathPublicationKey += ":wrong";
        CheckHealingJoin(tampered, ledger, counters, false);
        var reproduction = new ReproductionWorldSaveData();
        reproduction.pendingCarrierDeaths.Add(new ReproductionCarrierDeathNotificationSaveData
        {
            notificationId = "carrier-death:focused", carrierId = id,
            absoluteDay = pending.fact.absoluteDay, cause = pending.fact.cause,
            parentPublicationKey = pending.deathPublicationKey
        });
        CheckFatalityParentJoin(reproduction, ledger, body, counters, true);
        if (!commitFirst)
        {
            CheckFatalityParentJoin(reproduction, ledger, null, counters, false);
            reproduction.pendingCarrierDeaths[0].absoluteDay++;
            CheckFatalityParentJoin(reproduction, ledger, body, counters, false);
            reproduction.pendingCarrierDeaths[0].absoluteDay--;
        }
        else
        {
            tampered = JsonUtility.FromJson<DungeonCharacterBodyHealthSaveData>(json);
            tampered.pendingFatalityFacts[0].fact.characterName += "변조";
            CheckHealingJoin(tampered, ledger, counters, false);
            var torn = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(JsonUtility.ToJson(ledger));
            Require(torn.exactOutcomes.Count == expected, "Fatality did not atomically publish its selected transitions.");
            torn.exactOutcomes.RemoveAt(0);
            CheckHealingJoin(body, torn, counters, false);
        }

        using var resumedHarness = new OutcomeHarness(kind, id + ":restored", false, MigratedProducerOutcomeFaultMode.None, false);
        using var resumed = new HealthEntryFixture(resumedHarness, kind, id);
        int resumedDeaths = 0;
        using var resumedBinding = resumed.HealingEvents.Subscribe<CharacterDeathEvent>(_ => resumedDeaths++);
        var candidate = resumedHarness.Ledger.PrepareGameplayOutcomeRestore(ledger);
        resumedHarness.Ledger.BeginRestoreCandidate(); resumedHarness.Ledger.PublishGameplayOutcomeRestore(candidate);
        resumedHarness.Ledger.PublishRestoreCandidate(); resumedHarness.Ledger.CompleteRestoreCandidate();
        resumedHarness.Transaction.Restore(resumedHarness.Transaction.PrepareRestore(counters));
        resumed.HealingHealth.PublishRestore(resumed.HealingHealth.PrepareRestore(JsonUtility.FromJson<DungeonCharacterBodyHealthSaveData>(json)));
        string stateBefore = JsonUtility.ToJson(resumed.HealingHealth.Capture().characters.Single());
        resumed.HealingActor.name = "변경된 현재 이름";
        resumed.HealingHealth.RecoverPendingFatalityFacts();
        resumed.HealingHealth.RecoverPendingFatalityFacts();
        resumed.InvokeProductionEntry();
        Require(resumedDeaths == 0 && resumed.HealingHealth.Capture().pendingFatalityFacts.Count == 0
            && stateBefore == JsonUtility.ToJson(resumed.HealingHealth.Capture().characters.Single())
            && resumedHarness.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == expected,
            "Fatality replay repeated body/lifecycle effects or duplicated records.");
        CheckHealingJoin(body, resumedHarness.Ledger.CaptureGameplayOutcomes(), resumedHarness.Transaction.Capture(), true);
        CheckFatalityParentJoin(reproduction, resumedHarness.Ledger.CaptureGameplayOutcomes(),
            resumed.HealingHealth.Capture(), resumedHarness.Transaction.Capture(), true);
    }

    private static void CheckFatalityParentJoin(ReproductionWorldSaveData reproduction, GameplayOutcomeLedgerSaveData ledger,
        DungeonCharacterBodyHealthSaveData body, DungeonMigratedProducerOutcomeSaveData counters, bool expected)
    {
        var report = new DungeonGameRestoreReport();
        ReproductionDeathParentPreflight.Validate(reproduction, ledger, report, body, counters);
        Require(report.Success == expected, "Unexpected fatal parent source join: " + string.Join(";", report.Errors));
    }
}
#endif
