#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class SocialRitualObserverRecoveryDebugScenarios
{
    public static void RunForBatch()
    {
        var failures = new List<Exception>();
        Check(() => VerifyRecovery(true));
        Check(() => VerifyRecovery(false));
        Check(() =>
        {
            Require(FestivalCelebrationCausalProductionEntryDebugScenarios.RunCausalOwnerFamily(),
                "Actual festival celebration regression failed.");
            Debug.Log("B111_FESTIVAL_CAUSAL=PASS");
        });
        Check(() =>
        {
            Require(ObservedCareerArcCausalProductionEntryDebugScenarios.RunCausalOwnerFamily(),
                "Career/lineage causal regression failed.");
            Debug.Log("B111_LINEAGE_CAUSAL=PASS");
        });
        if (failures.Count > 0) throw new AggregateException("Social ritual verification failed.", failures);
        Debug.Log("B111_SOCIAL_RITUAL_RECOVERY=PASS");
        void Check(Action action)
        {
            try { action(); }
            catch (Exception error) when (error is not OutOfMemoryException
                && error is not StackOverflowException && error is not AccessViolationException)
            { failures.Add(error); Debug.LogError("B111_CASE_FAILED:" + error); }
        }
    }

    private static void VerifyRecovery(bool beforeCommit)
    {
        string id = "social:b111:" + (beforeCommit ? "before" : "after");
        string ownerJson, societyJson, journalJson;
        GameplayOutcomeLedgerSaveData native;
        DungeonMigratedProducerOutcomeSaveData counters;
        using (var source = new Context(id))
        {
            source.Transport.ThrowBeforeCommit = beforeCommit;
            source.Transport.HideAcknowledgement = !beforeCommit;
            source.Social.ArchiveDeath(new CharacterId("character:b111:a"), new CharacterSpeciesId("species:human"),
                1, 10, false, new HouseholdId("household:b111"), 1);
            source.Social.ArchiveDeath(new CharacterId("character:b111:b"), new CharacterSpeciesId("species:human"),
                2, 11, false, new HouseholdId("household:b111"), 1);
            int guards = 0;
            source.Transport.BeforePublish = () =>
            {
                ExpectInvalid(() => source.Social.Capture());
                ExpectInvalid(() => source.Journal.Capture());
                ExpectInvalid(() => source.Social.ArchiveColdData(200, Array.Empty<CharacterId>()));
                guards += 3;
            };
            source.Social.ArchiveColdData(200, Array.Empty<CharacterId>());
            Require(guards == 3 && source.Social.Tombstones.Count == 0,
                "Actual archive did not complete atomically or publication fence failed.");
            var owner = source.Social.Capture();
            Require(owner.kinship.lineageSummaries.Single().archivedCharacterCount == 2,
                "Actual lineage count is wrong.");
            ownerJson = JsonUtility.ToJson(owner);
            societyJson = JsonUtility.ToJson(source.Campaign.CaptureSociety());
            journalJson = new SocialRitualFactSaveSection(source.Journal).Capture();
            native = Copy(source.Outcomes.Ledger.CaptureGameplayOutcomes());
            counters = Copy(source.Outcomes.Transaction.Capture());
            Require(native.exactOutcomes.Count == (beforeCommit ? 0 : 2), "Wrong source native count.");
            var journal = DungeonSaveJson.Deserialize<SocialRitualFactSaveData>(journalJson);
            Require(journal.pending.Count == 2, "Uncertain batch was not saved.");
            Validate(journal, ownerJson, societyJson, native, counters);

            var invalid = JObject.Parse(journalJson);
            ((JObject)invalid["pending"][0]).Remove("publicationKey");
            ExpectInvalid(() => SocialRitualFactSaveSection.ValidateJson(invalid.ToString()));
            var changed = Copy(journal);
            changed.pending[0].publicationKey += ":changed";
            ExpectInvalid(() => SocialRitualFactPreflight.ValidateJournal(changed, native, counters));
            var badOwner = DungeonSaveJson.Deserialize<KinshipHouseholdWorldSaveData>(ownerJson);
            badOwner.kinship.lineageSummaries.Clear();
            ExpectInvalid(() => SocialRitualFactPreflight.ValidateOwners(journal,
                new FestivalExecutionWorldSaveData(),
                DungeonSaveJson.Deserialize<SocietyEventWorldSaveData>(societyJson), badOwner));
        }

        using (var restored = new Context(id))
        {
            var ledger = restored.Outcomes.Ledger;
            var candidate = ledger.PrepareGameplayOutcomeRestore(native);
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            restored.Outcomes.Transaction.Restore(restored.Outcomes.Transaction.PrepareRestore(counters));
            restored.Social.PublishRestore(restored.Social.PrepareRestore(
                DungeonSaveJson.Deserialize<KinshipHouseholdWorldSaveData>(ownerJson)));
            restored.Campaign.PublishSociety(restored.Campaign.PrepareSociety(
                DungeonSaveJson.Deserialize<SocietyEventWorldSaveData>(societyJson)));
            var section = new SocialRitualFactSaveSection(restored.Journal);
            var report = new DungeonGameRestoreReport();
            section.Restore(journalJson, section.SectionVersion, report);
            Require(report.Success, "Current journal JSON restore failed.");
            restored.Transport.BlockRecovery = true;
            Require(!restored.Journal.RecoverPending(out _) && section.Capture() == journalJson,
                "Unavailable recovery mutated pending facts.");
            restored.Transport.BlockRecovery = false;
            restored.Transport.BeforeReplay = () => ExpectInvalid(() => restored.Social.Capture());
            Require(restored.Journal.RecoverPending(out _), "Frozen fact replay failed.");
            Require(restored.Journal.Capture().pending.Count == 0
                && JsonUtility.ToJson(restored.Social.Capture()) == ownerJson
                && JsonUtility.ToJson(restored.Campaign.CaptureSociety()) == societyJson,
                "Replay repeated actual domain effects.");
            var after = ledger.CaptureGameplayOutcomes();
            Require(after.exactOutcomes.Count == 2, "Recovery duplicated or lost an outcome.");
            if (!beforeCommit)
                Require(native.exactOutcomes.All(old => after.exactOutcomes.Any(current =>
                    old.sequence == current.sequence && old.immutablePayloadHash == current.immutablePayloadHash)),
                    "Recovery changed the committed identity/payload.");
            restored.Social.ArchiveColdData(200, Array.Empty<CharacterId>());
            Require(ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 2
                && JsonUtility.ToJson(restored.Social.Capture()) == ownerJson,
                "Duplicate archival repeated effects or observations.");

            // A rejected current-schema restore must not replace a healthy journal.
            string intact = section.Capture();
            var bad = DungeonSaveJson.Deserialize<SocialRitualFactSaveData>(journalJson);
            bad.pending[0].publicationKey = "";
            ExpectInvalid(() => restored.Journal.Restore(bad));
            Require(section.Capture() == intact, "Invalid restore changed current journal.");
        }
        Debug.Log(beforeCommit ? "B111_SOCIAL_ACK_BEFORE=PASS" : "B111_SOCIAL_ACK_AFTER=PASS");
    }

    private static void Validate(SocialRitualFactSaveData journal, string owner, string society,
        GameplayOutcomeLedgerSaveData ledger, DungeonMigratedProducerOutcomeSaveData counters)
    {
        SocialRitualFactPreflight.ValidateOwners(journal, new FestivalExecutionWorldSaveData(),
            DungeonSaveJson.Deserialize<SocietyEventWorldSaveData>(society),
            DungeonSaveJson.Deserialize<KinshipHouseholdWorldSaveData>(owner));
        SocialRitualFactPreflight.ValidateJournal(journal, ledger, counters);
    }
    private static T Copy<T>(T value) where T : class => DungeonSaveJson.Deserialize<T>(JsonUtility.ToJson(value));
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void ExpectInvalid(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected invalid state rejection.");
    }
    private sealed class Context : IDisposable
    {
        internal readonly MigratedProducerOutcomeEditorFixture Outcomes;
        internal readonly SocialRitualFactEditorFixture Facts;
        internal readonly Transport Transport;
        internal readonly SocialRitualFactJournal Journal;
        internal readonly V20CampaignRuntime Campaign;
        internal readonly KinshipHouseholdRuntime Social;
        internal Context(string id)
        {
            var roots = new DungeonRuntimeAggregateRootStore();
            Outcomes = new MigratedProducerOutcomeEditorFixture(id, roots);
            Facts = new SocialRitualFactEditorFixture(Outcomes);
            Transport = new Transport(Facts.Dispatcher);
            Journal = new SocialRitualFactJournal(Transport, Transport);
            Campaign = new V20CampaignRuntime(roots, new V20StoryContentCatalog(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader())), null, null, Journal);
            Social = new KinshipHouseholdRuntime(roots, Campaign, new GameEventBus(), Journal);
        }
        public void Dispose() { Journal.AssertIdle(); Facts.Dispose(); }
    }
    private sealed class Transport : IDomainFactPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher dispatcher;
        internal Transport(DomainFactDispatcher dispatcher) => this.dispatcher = dispatcher;
        internal bool ThrowBeforeCommit, HideAcknowledgement, BlockRecovery;
        internal Action BeforePublish, BeforeReplay;
        public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure) =>
            dispatcher.TryReserve(fact, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact) => dispatcher.Publish(lease, fact);
        public DomainFactDelivery Replay<T>(in T fact) => dispatcher.Replay(fact);
        public void Cancel(in DomainFactLease lease) => dispatcher.Cancel(lease);
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure)
        {
            BeforePublish?.Invoke();
            if (ThrowBeforeCommit) throw new InvalidOperationException("B111 controlled pre-commit transport failure");
            bool accepted = dispatcher.PublishBatch(facts, results, out failure);
            if (HideAcknowledgement)
                for (int index = 0; index < results.Length; index++)
                    results[index] = DomainFactDelivery.Uncertain("B111 controlled lost acknowledgement");
            return accepted;
        }
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> facts, DomainFactDelivery[] results, out string failure)
        {
            if (BlockRecovery) { failure = "B111 controlled unavailable recovery"; return false; }
            BeforeReplay?.Invoke();
            return dispatcher.ReplayBatch(facts, results, out failure);
        }
    }
}
#endif
