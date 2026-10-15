#if UNITY_EDITOR
using System;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class CharacterCorpseConsumptionFactDebugScenarios
{
    public static string Run()
    {
        VerifyRecovery(false); VerifyRecovery(true);
        return "corpse-observer/atomic-pair/frozen-json/cold-recovery/dedup/canonical-and-raw-rejection=PASS;physical-effects-and-natural-AI=NOT-RUN";
    }

    private static void VerifyRecovery(bool committed)
    {
        var original = new MigratedProducerOutcomeEditorFixture("run:corpse-fact:" + committed, includeTradeInventory: true);
        using var dispatcher = Connect(original);
        var fact = Fact();
        Require(dispatcher.TryStage(fact, out var stage, out string failure), failure);
        var pending = new CharacterCorpseConsumptionPublicationData
        { publicationKey = stage.OperationKey, fact = fact.Snapshot() };
        if (committed) Require(stage.Commit().Completed, "corpse pair did not deliver");
        else stage.Cancel(); // Lost response before durable commit; owner retains applied-domain evidence.
        var survival = new DungeonDarkSurvivalSaveData();
        survival.pendingCorpsePublications.Add(pending);
        survival = JsonUtility.FromJson<DungeonDarkSurvivalSaveData>(JsonUtility.ToJson(survival));
        var physical = new DungeonPhysicalItemSaveData();
        var debit = pending.fact.Clone().physical;
        debit.defaultObservationDay = 0; debit.publicationExpected = true; debit.publicationKey = pending.publicationKey;
        physical.pendingBatchDispositions.Add(debit);
        var ledger = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(JsonUtility.ToJson(original.Ledger.CaptureGameplayOutcomes()));
        var counters = JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(JsonUtility.ToJson(original.Transaction.Capture()));
        CheckJoin(survival, physical, ledger, counters, true);

        var restored = new MigratedProducerOutcomeEditorFixture("run:corpse-fact:restored", includeTradeInventory: true);
        var candidate = restored.Ledger.PrepareGameplayOutcomeRestore(ledger);
        restored.Ledger.BeginRestoreCandidate(); restored.Ledger.PublishGameplayOutcomeRestore(candidate);
        restored.Ledger.PublishRestoreCandidate(); restored.Ledger.CompleteRestoreCandidate();
        restored.Transaction.Restore(restored.Transaction.PrepareRestore(counters));
        using var replay = Connect(restored);
        var saved = survival.pendingCorpsePublications[0];
        var frozen = CharacterCorpseConsumedFact.FromSave(saved.fact);
        var results = new DomainFactDelivery[1];
        Require(replay.ReplayBatch(new[] { DomainFactReplay.Create(saved.publicationKey, frozen) }, results, out failure)
            && results[0].Completed && results[0].OperationKey == pending.publicationKey, failure);
        long sequence = restored.Ledger.CaptureGameplayOutcomes().nextSequence;
        string owners = JsonUtility.ToJson(restored.Transaction.Capture());
        Require(replay.ReplayBatch(new[] { DomainFactReplay.Create(saved.publicationKey, frozen) }, results, out failure)
            && results[0].Completed && restored.Ledger.CaptureGameplayOutcomes().nextSequence == sequence
            && JsonUtility.ToJson(restored.Transaction.Capture()) == owners, "corpse replay allocated another identity");
        ledger = restored.Ledger.CaptureGameplayOutcomes(); counters = restored.Transaction.Capture();
        debit.publicationId = results[0].PublicationId;
        debit.publicationFingerprint = results[0].Fingerprint; debit.publicationDelivered = true;
        CheckJoin(survival, physical, ledger, counters, true);
        saved.fact.experience.memory += "-changed";
        CheckJoin(survival, physical, ledger, counters, false);
        saved.fact.experience.memory = pending.fact.experience.memory;
        physical.pendingBatchDispositions.Clear(); // Acknowledged journals may retire before owner cleanup.
        CheckJoin(survival, physical, ledger, counters, true);
        var raw = JObject.Parse(JsonUtility.ToJson(survival));
        ((JObject)raw["pendingCorpsePublications"][0]["fact"]["experience"]).Remove("hungerAfter");
        bool rejected = false;
        try { DarkSurvivalSaveSection.RequireCorpsePublicationShape(raw.ToString()); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "corpse missing state field silently defaulted");
    }

    private static DomainFactDispatcher Connect(MigratedProducerOutcomeEditorFixture fixture) => new(
        new IDomainFactObserverBinding[]
        {
            new CharacterCorpseConsumptionFactBinding(new CharacterCorpseConsumptionFactObserver(
                fixture.Registry, fixture.Ledger, fixture.Recorder, fixture.Transaction,
                new GameplayDomainFactCommitGroup(fixture.Recorder, fixture.Ledger)))
        });

    private static CharacterCorpseConsumedFact Fact()
    {
        const string stack = "stack:corpse-fact";
        const string reason = "extreme-hunger";
        var receipt = new PhysicalItemBatchDispositionReceipt(PhysicalItemDispositionKind.Sink,
            "operation:corpse-fact", reason, (int)PhysicalItemDispositionKind.Sink + ":" + reason + ":" + stack + "=1",
            new[] { stack }, 1, 1000, 1, new[]
            {
                new PhysicalItemDispositionSourceFact(stack, DarkSurvivalItemDefinitions.HumanoidCorpseItemId,
                    string.Empty, 1, 1000, new Vector2Int(2, 3), "당시의 시체")
            });
        return new CharacterCorpseConsumedFact(receipt, new CharacterCorpseConsumptionExperienceData
        {
            characterId = "character:eater", characterName = "생존자",
            victimId = "character:corpse", victimName = "사망자", victimSpecies = "Human",
            deathReason = "starvation", memory = "극한의 굶주림 속에서 사망자를 먹었다",
            sameSpecies = true, absoluteDay = 3, memoriesBefore = 0, memoriesAfter = 1,
            hungerBefore = 0, hungerAfter = 75, hygieneBefore = 50, hygieneAfter = 30,
            infectionBefore = 0, infectionAfter = 20, moodBefore = 50, moodAfter = 32
        });
    }

    private static void CheckJoin(DungeonDarkSurvivalSaveData survival, DungeonPhysicalItemSaveData physical,
        GameplayOutcomeLedgerSaveData ledger, DungeonMigratedProducerOutcomeSaveData counters, bool expected)
    {
        var report = new DungeonGameRestoreReport();
        CharacterCorpseConsumptionFactPreflight.Validate(survival, physical, ledger, counters, report);
        Require(report.Success == expected, "corpse canonical join unexpected result");
    }
    private static void Require(bool condition, string detail)
    { if (!condition) throw new InvalidOperationException(detail); }
}
#endif
