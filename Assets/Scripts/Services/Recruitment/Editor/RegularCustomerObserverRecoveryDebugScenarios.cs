#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using Scenario = MigratedProducerEconomyServiceEntryE2EDebugScenarios.RegularCustomerScenario;
using MercenaryScenario = MigratedProducerEconomyServiceEntryE2EDebugScenarios.MercenaryRegularCustomerScenario;

public static class RegularCustomerObserverRecoveryDebugScenarios
{
    public static void RunForBatch()
    {
        MigratedProducerEconomyServiceEntryE2EDebugScenarios.RunRecruitmentObserversForBatch();
        RegularCustomerObservationDebugScenarios.RunOrThrow();
        foreach (RegularCustomerFactStage stage in Enum.GetValues(typeof(RegularCustomerFactStage)))
        {
            VerifySavedAcknowledgement(stage, beforeCommit: false);
            VerifySavedAcknowledgement(stage, beforeCommit: true);
        }
        VerifyMercenaryNoSecondPayment();
        Debug.Log("B108_RECRUITMENT_RECOVERY=PASS");
    }

    private static void VerifySavedAcknowledgement(RegularCustomerFactStage stage, bool beforeCommit)
    {
        var kind = stage switch {
            RegularCustomerFactStage.VisitRecorded => MigratedProducerOutcomeKind.RegularCustomerVisitResult,
            RegularCustomerFactStage.RecruitmentCommitted => MigratedProducerOutcomeKind.RegularCustomerRecruitResult,
            _ => MigratedProducerOutcomeKind.RegularCustomerRecruitDeliveryResult
        };
        using var source = new Scenario(kind);
        if (stage != RegularCustomerFactStage.VisitRecorded) source.MakeRecruitCandidate();
        var sourceAdapter = new RegularCustomerPersistenceAdapter(source.Runtime, new Catalog(source.Actor.data));
        var initialCandidate = sourceAdapter.PrepareRestore(sourceAdapter.CaptureState());
        int publications = 0, guardChecks = 0;
        source.Facts.BeforePublish = () => {
            publications++;
            ExpectBlocked(() => source.CaptureSaveJson());
            ExpectBlocked(() => sourceAdapter.PublishRestore(initialCandidate));
            ExpectBlocked(() => source.Recruit());
            ExpectBlocked(() => source.RetryDelivery());
            guardChecks += 4;
            int targetCall = stage == RegularCustomerFactStage.RecruitmentDelivered ? 2 : 1;
            if (beforeCommit && publications == targetCall)
                throw new InvalidOperationException("B108 explicit observation transport failure before native commit");
        };
        source.Facts.HideNextAcknowledgement = !beforeCommit;
        source.Facts.HideAcknowledgementStage = stage;
        if (stage == RegularCustomerFactStage.VisitRecorded) source.Visit();
        else Require(source.Recruit(), "Recruit owner effect was lost after an uncertain observation.");

        string ownerJson = source.CaptureSaveJson();
        var owner = JsonUtility.FromJson<DungeonRegularCustomerSaveData>(ownerJson);
        var row = owner.pendingFacts.Single();
        Require(row.fact.stage == stage && guardChecks >= 4, "Wrong pending stage or mutation fence was not exercised.");
        var ledgerSave = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(
            JsonUtility.ToJson(source.Outcomes.Ledger.CaptureGameplayOutcomes()));
        var counterSave = JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(
            JsonUtility.ToJson(source.Outcomes.Transaction.Capture()));
        RegularCustomerSaveSection.ValidatePendingFactJson(ownerJson);
        RegularCustomerFactPreflight.ValidatePending(owner, ledgerSave, counterSave);
        var raw = JObject.Parse(ownerJson);
        ((JObject)raw["pendingFacts"][0]["fact"]).Remove("absoluteDay");
        ExpectInvalid(() => RegularCustomerSaveSection.ValidatePendingFactJson(raw.ToString()));

        var wrongKey = JsonUtility.FromJson<DungeonRegularCustomerSaveData>(ownerJson);
        wrongKey.pendingFacts[0].publicationKey += ":changed";
        ExpectInvalid(() => RegularCustomerFactPreflight.ValidatePending(wrongKey, ledgerSave, counterSave));
        if (!beforeCommit)
        {
            var wrongFact = JsonUtility.FromJson<DungeonRegularCustomerSaveData>(ownerJson);
            wrongFact.pendingFacts[0].fact.displayName += "-changed";
            ExpectInvalid(() => RegularCustomerFactPreflight.ValidatePending(wrongFact, ledgerSave, counterSave));
        }

        var restoredOutcomes = new MigratedProducerOutcomeEditorFixture(ledgerSave.runId);
        var restoredLedger = restoredOutcomes.Ledger;
        var nativeCandidate = restoredLedger.PrepareGameplayOutcomeRestore(ledgerSave);
        restoredLedger.BeginRestoreCandidate();
        restoredLedger.PublishGameplayOutcomeRestore(nativeCandidate);
        restoredLedger.PublishRestoreCandidate();
        restoredLedger.CompleteRestoreCandidate();
        restoredOutcomes.Transaction.Restore(restoredOutcomes.Transaction.PrepareRestore(counterSave));
        using var restoredFacts = new RegularCustomerFactEditorFixture(restoredOutcomes);
        var host = new GameObject("B108 restored recruitment owner");
        try
        {
            var activation = new CountingActivation(source.Actor);
            var runtime = host.AddComponent<RegularCustomerRuntime>();
            runtime.ConstructRecruitmentRuntime(activation, new GameEventBus(),
                new FixedGameSessionStateProvider(20), restoredFacts.Publisher, restoredFacts.Recovery);
            var adapter = new RegularCustomerPersistenceAdapter(runtime, new Catalog(source.Actor.data));
            var section = new RegularCustomerSaveSection(adapter, adapter);
            var report = new DungeonGameRestoreReport();
            section.Restore(ownerJson, section.SectionVersion, report);
            Require(report.Success, "Current recruitment JSON restore failed: " + string.Join(" | ", report.Errors));
            string intact = section.Capture();
            var invalid = JsonUtility.FromJson<DungeonRegularCustomerSaveData>(intact);
            invalid.pendingFacts[0].fact.visitCount += 100;
            ExpectInvalid(() => adapter.PrepareRestore(invalid));
            Require(section.Capture() == intact, "Rejected restore changed the live owner.");

            int visits = 0, recruits = 0;
            runtime.Updated += _ => visits++;
            runtime.Recruited += _ => recruits++;
            restoredFacts.BlockRecovery = true;
            runtime.RetryPendingRecruitDeliveriesForDebug();
            Require(section.Capture() == intact && activation.Count == 0, "Unavailable recovery lost state or activated twice.");
            restoredFacts.BlockRecovery = false;
            restoredFacts.BeforeReplay = () => {
                ExpectBlocked(() => section.Capture());
                ExpectBlocked(() => adapter.PrepareRestore(owner));
                ExpectBlocked(() => runtime.RetryPendingRecruitDeliveriesForDebug());
            };
            runtime.RetryPendingRecruitDeliveriesForDebug();
            var after = adapter.CaptureState();
            Require(after.pendingFacts.Count == 0, "Exact pending fact was not acknowledged.");
            var record = after.records.Single();
            Require(record.visitCount == owner.records.Single().visitCount, "Recovery counted another visit.");
            if (stage != RegularCustomerFactStage.VisitRecorded)
                Require(record.isRecruited && record.recruitDeliveryApplied && !record.recruitDeliveryPending,
                    "Recovery did not complete the existing recruitment.");
            Require(activation.Count == (stage == RegularCustomerFactStage.RecruitmentCommitted ? 1 : 0),
                "Recovery repeated or skipped actor activation.");
            Require(visits == (stage == RegularCustomerFactStage.VisitRecorded ? 1 : 0)
                && recruits == (stage == RegularCustomerFactStage.RecruitmentCommitted ? 1 : 0),
                "Post-commit observers ran at the wrong boundary.");
            Require(GameplayDomainFactDeliveryQuery.TryReadKey(row.publicationKey, out var key), "Saved publication key is invalid.");
            Require(restoredLedger.TryGetResultIdentity(key, out var identity), "Saved publication identity was replaced.");
            if (!beforeCommit)
            {
                Require(source.Outcomes.Ledger.TryGetResultIdentity(key, out var previous)
                    && identity.OutcomeId == previous.OutcomeId && identity.CanonicalPayloadHash == previous.CanonicalPayloadHash,
                    "Acknowledgement recovery changed native sequence or hash.");
            }
            long expectedNext = ledgerSave.nextSequence + (beforeCommit ? 1 : 0)
                + (stage == RegularCustomerFactStage.RecruitmentCommitted ? 1 : 0);
            Require(restoredLedger.CaptureGameplayOutcomes().nextSequence == expectedNext,
                "Recovery added an unexpected native result.");
            string completeOwner = section.Capture();
            long completeSequence = restoredLedger.CaptureGameplayOutcomes().nextSequence;
            runtime.RetryPendingRecruitDeliveriesForDebug();
            Require(section.Capture() == completeOwner
                && restoredLedger.CaptureGameplayOutcomes().nextSequence == completeSequence,
                "Completed replay duplicated a game or log effect.");
        }
        finally { Object.DestroyImmediate(host); }
        Debug.Log("B108_RECRUITMENT_ACK_" + stage.ToString().ToUpperInvariant()
            + (beforeCommit ? "_BEFORE" : "_AFTER") + "=PASS");
    }

    private static void VerifyMercenaryNoSecondPayment()
    {
        using var source = new MercenaryScenario(
            MigratedProducerOutcomeKind.RegularCustomerRecruitDeliveryResult, MigratedProducerOutcomeFaultMode.None);
        source.MakeRecruitCandidate();
        int before = source.Balance;
        source.Facts.HideNextAcknowledgement = true;
        source.Facts.HideAcknowledgementStage = RegularCustomerFactStage.RecruitmentDelivered;
        Require(source.Hire(out int fee) && source.DeliveryApplied && source.DeliveryPending
            && source.SpendCount == 1 && source.Balance == before - fee, "Mercenary owner effects were not applied once.");
        string pending = source.CaptureSaveJson();
        source.RestoreSaveJson(pending);
        source.RetryDelivery();
        source.RetryDelivery();
        Require(!source.DeliveryPending && source.SpendCount == 1 && source.Balance == before - fee
            && source.MercenaryContractCount == 1, "Mercenary recovery repeated advance payment or contract.");
        Debug.Log("B108_RECRUITMENT_MERCENARY_ACK=PASS");
    }

    private sealed class Catalog : IRunCharacterCatalog
    {
        internal Catalog(CharacterSO data) => Characters = new[] { data };
        public IReadOnlyCollection<CharacterSO> Characters { get; }
    }

    // The actual recruitment owner calls this controlled activation port. This
    // test does not claim a natural spawner/AI path or a new-world actor restore.
    private sealed class CountingActivation : IRecruitedCharacterActivationService
    {
        private readonly CharacterActor actor;
        internal CountingActivation(CharacterActor actor) => this.actor = actor;
        internal int Count { get; private set; }
        public bool TryValidateActivation(RegularCustomerRecord record, out string message)
        { message = ""; return actor.Identity.PersistentId == record.CustomerId; }
        public bool TryActivate(RegularCustomerRecord record, out CharacterActor activated, out string message)
        {
            activated = actor; message = ""; Count++;
            actor.characterType = CharacterType.NPC;
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            return true;
        }
    }

    private static void ExpectBlocked(Action action)
    {
        try { action(); }
        catch (InvalidOperationException error) when (error.Message == "regular-customer-mutation-in-progress") { return; }
        throw new InvalidOperationException("Recruitment mutation reentry was not blocked.");
    }
    private static void ExpectInvalid(Action action)
    {
        try { action(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return; }
        throw new InvalidOperationException("Invalid recruitment checkpoint was accepted.");
    }
    private static void Require(bool valid, string message)
    { if (!valid) throw new InvalidOperationException(message); }
}
#endif
