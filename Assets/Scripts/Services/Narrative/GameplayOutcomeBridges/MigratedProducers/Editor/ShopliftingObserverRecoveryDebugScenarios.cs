#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class MigratedProducerEconomyEntryExtendedE2EDebugScenarios
{
    public static void RunShopliftingObserversForBatch()
    {
        RunLiveCheckoutRejectionProbeBatch();
        RetailScenario.VerifyShopliftingSavedReplay();
        RetailReversibleSinkDebugScenarios.RunForBatch();
        Debug.Log("B102_RETAIL_UNIQUE_SINK_ROLLBACK_AND_ACCEPT=PASS");
        ShopliftingObserverProjectionDebugScenarios.RunForBatch();
        Debug.Log("B102_SHOPLIFTING_PROJECTION_AND_OBSERVER_LIFETIME=PASS");
        Debug.Log("B102_SHOPLIFTING_ACTUAL_OWNER_OBSERVER_AND_RESTORE=PASS");
    }

    private sealed partial class RetailScenario
    {
        internal static void VerifyShopliftingSavedReplay()
        {
            const MigratedProducerOutcomeKind Kind = MigratedProducerOutcomeKind.FacilityCrimeEvent;
            const string Run = "run:b102-shoplifting-recovery";
            const string Owner = "building:b102-shoplifting-recovery";
            string serialized;
            using (var source = new RetailScenario(Kind, true, stableRunId: Run, ownerId: Owner))
            {
                int crimeEvents = 0, stockEvents = 0;
                using var crimes = source.shopEvents.Subscribe<FacilityCrimeEvent>(_ => crimeEvents++);
                using var stocks = source.shopEvents.Subscribe<FacilityStockConsumedEvent>(_ => stockEvents++);
                source.shopliftingFacts.HideNextAcknowledgement = true;
                source.shopliftingFacts.BlockRecovery = true;
                JObject before = source.CaptureOwnerCheckpoint();
                source.RunLiveCheckout();
                source.RequireLiveCheckoutExecutionClosed();
                var journal = source.shopliftingFacts.Journal.Capture();
                Require(source.shopliftingFacts.HiddenAcknowledgements == 1 && journal.pending.Count == 1
                    && source.StockCount == source.InitialStockCount - 1 && source.HoldingMoney == source.InitialMoney
                    && crimeEvents == 1 && stockEvents == 1, "Unknown ACK lost or repeated the actual shoplifting effect.");
                var ledger = source.Outcomes.Ledger.CaptureGameplayOutcomes();
                var counters = source.Outcomes.Transaction.Capture();
                ShopliftingFactPreflight.ValidateJournal(journal, ledger, counters);
                ShopliftingFactPreflight.ValidateOwners(journal, source.ShopOwnerSnapshot(), new DungeonPhysicalItemSaveData());
                var changed = ShopliftingFactJournal.ValidateAndClone(journal);
                changed.pending[0].fact.lossValue++;
                RequireThrows<InvalidOperationException>(() => ShopliftingFactPreflight.ValidateJournal(changed, ledger, counters));
                var unapplied = source.ShopOwnerSnapshot();
                unapplied.buildings[0].stateModules[0].payload = before["shopStock"].ToString(Formatting.None);
                RequireThrows<InvalidOperationException>(() => ShopliftingFactPreflight.ValidateOwners(
                    journal, unapplied, new DungeonPhysicalItemSaveData()));
                string strict = JsonUtility.ToJson(journal);
                ShopliftingFactSaveSection.ValidateJson(strict);
                var missing = JObject.Parse(strict);
                ((JObject)missing["pending"][0]["fact"]).Remove("absoluteDay");
                RequireThrows<InvalidOperationException>(() => ShopliftingFactSaveSection.ValidateJson(missing.ToString(Formatting.None)));
                Require(!source.shopliftingFacts.Journal.TryBegin(new ShopliftingFact(journal.pending[0].fact), out _, out _)
                    && !source.CommitCrime() && source.StockCount == 0 && crimeEvents == 1,
                    "Blocked recovery allowed another stock mutation or notification.");
                serialized = JsonConvert.SerializeObject(new ShopliftingCheckpoint
                {
                    journal = journal, ledger = ledger, counters = counters, owner = source.CaptureOwnerCheckpoint()
                });
            }

            var saved = JsonConvert.DeserializeObject<ShopliftingCheckpoint>(serialized);
            using (var restored = new RetailScenario(Kind, true, stableRunId: Run, ownerId: Owner))
            {
                restored.RestoreOwnerCheckpoint(saved.owner);
                RestoreFacilityShopT01Ledger(restored.Outcomes.Ledger, saved.ledger);
                restored.Outcomes.Transaction.Restore(restored.Outcomes.Transaction.PrepareRestore(saved.counters));
                var journal = restored.shopliftingFacts.Journal;
                journal.BeginRestoreCandidate();
                journal.Restore(saved.journal);
                journal.PublishRestoreCandidate();
                journal.CompleteRestoreCandidate();
                ShopliftingFactPreflight.ValidateJournal(journal.Capture(), saved.ledger, saved.counters);
                ShopliftingFactPreflight.ValidateOwners(journal.Capture(), restored.ShopOwnerSnapshot(), new DungeonPhysicalItemSaveData());
                JObject before = restored.CaptureOwnerCheckpoint();
                int notifications = 0;
                using var crimes = restored.shopEvents.Subscribe<FacilityCrimeEvent>(_ => notifications++);
                using var stocks = restored.shopEvents.Subscribe<FacilityStockConsumedEvent>(_ => notifications++);
                restored.shopliftingFacts.BlockRecovery = true;
                Require(!journal.RecoverPending(out _) && journal.Capture().pending.Count == 1,
                    "Unavailable recovery lost the pending fact.");
                restored.shopliftingFacts.BlockRecovery = false;
                Require(journal.RecoverPending(out string failure) && journal.Capture().pending.Count == 0,
                    "Saved fact recovery failed: " + failure);
                Require(journal.RecoverPending(out _) && !restored.CommitCrime()
                    && notifications == 0 && JToken.DeepEquals(before, restored.CaptureOwnerCheckpoint()),
                    "Saved replay repeated a game effect, notification, or owner update.");
                var after = restored.Outcomes.Ledger.CaptureGameplayOutcomes();
                string type = MigratedProducerOutcomeCatalog.Get(Kind).OutcomeTypeId.Value;
                var original = saved.ledger.exactOutcomes.Concat(saved.ledger.outbox).Single(row => row.outcomeTypeId == type);
                var actual = after.exactOutcomes.Concat(after.outbox).Single(row => row.outcomeTypeId == type);
                Require(after.nextSequence == saved.ledger.nextSequence && actual.sequence == original.sequence
                    && actual.operationId == original.operationId && actual.immutablePayloadHash == original.immutablePayloadHash,
                    "Saved replay changed or duplicated the canonical crime identity.");
            }
            using (var detached = new RetailScenario(Kind, true))
            {
                JObject before = detached.CaptureOwnerCheckpoint();
                detached.shopliftingFacts.Dispose();
                RequireThrows<InvalidOperationException>(() => detached.CommitCrime());
                Require(JToken.DeepEquals(before, detached.CaptureOwnerCheckpoint()),
                    "A detached observer allowed an unobserved stock mutation.");
            }
            Debug.Log("B102_SHOPLIFTING_ACK_LOSS_STRICT_FRESH_OWNER_REPLAY=PASS");
        }

        private ModularFacilityWorldSaveData ShopOwnerSnapshot() => new()
        {
            buildings = new List<ModularFacilityBuildingSaveData>
            {
                new()
                {
                    stateModules = new List<BuildingStateModuleSaveData>
                    {
                        new() { moduleId = BuildingStateModuleIds.ShopStock, version = 1,
                            payload = JsonUtility.ToJson(shop.CreateStockSnapshot()) }
                    }
                }
            }
        };
    }

    [Serializable]
    private sealed class ShopliftingCheckpoint
    {
        public ShopliftingFactJournalData journal;
        public GameplayOutcomeLedgerSaveData ledger;
        public DungeonMigratedProducerOutcomeSaveData counters;
        public JObject owner;
    }
}
#endif
