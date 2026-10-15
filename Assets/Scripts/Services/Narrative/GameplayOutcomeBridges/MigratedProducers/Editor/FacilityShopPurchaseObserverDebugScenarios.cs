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
    public static void RunFacilityShopObserversForBatch()
    {
        RunFacilityShopT01OrThrow();
        Debug.Log("B100_SHOP_T01_PAYMENT_REJECT_DELIVERY_REGRESSION=PASS");
        VerifyShopUnknownAcknowledgementRestore();
        FacilityWishlistProcurementCausalProductionEntryDebugScenarios.RunOrThrow();
        Debug.Log("B100_SHOP_T02_ACTUAL_FULFILLMENT_WISHLIST_RESTORE=PASS");
        Debug.Log("B100_SHOP_ACTUAL_OWNER_OBSERVER_AND_RESTORE=PASS");
    }

    private static void VerifyShopUnknownAcknowledgementRestore()
    {
        const MigratedProducerOutcomeKind Kind = MigratedProducerOutcomeKind.FacilityShopPurchaseResult;
        string serialized;
        string publicationKey;
        int expectedBalance, expectedPaid;
        var receiver = new ShopEnqueueProbe();
        using (var source = new FacilityShopScenario(Kind, purchaseDelivery: receiver))
        {
            source.Facts.HideNextAcknowledgement = true;
            source.Facts.BlockRecovery = true;
            Require(source.Purchase(out var result) && result.success, "Unknown observation lost an applied purchase.");
            publicationKey = result.purchasePublicationKey;
            expectedBalance = source.Money;
            expectedPaid = result.cost;
            var journal = source.Facts.Journal.Capture();
            Require(source.Facts.HiddenAcknowledgements == 1 && journal.pending.Count == 1
                && !journal.pending[0].observationCommitted && receiver.Accepted.Count == 0
                && source.PurchaseSequence == 1 && source.PaymentRecords.Count == 1
                && source.InitialMoney - source.Money == expectedPaid, "Unknown ACK did not retain exact purchase ownership.");
            var ledger = source.Outcomes.Ledger.CaptureGameplayOutcomes();
            var counters = source.Outcomes.Transaction.Capture();
            FacilityShopPurchaseFactPreflight.ValidateJournal(journal, ledger, counters);
            var changed = FacilityShopPurchaseJournal.ValidateAndClone(journal);
            changed.pending[0].fact.offerName += " changed";
            bool rejected = false;
            try { FacilityShopPurchaseFactPreflight.ValidateJournal(changed, ledger, counters); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Canonical purchase preflight accepted a changed fact.");
            string strictJson = JsonUtility.ToJson(journal);
            FacilityShopPurchaseFactSaveSection.ValidateJson(strictJson);
            var missing = JObject.Parse(strictJson);
            ((JObject)missing["pending"][0]["fact"]).Remove("absoluteDay");
            rejected = false;
            try { FacilityShopPurchaseFactSaveSection.ValidateJson(missing.ToString(Formatting.None)); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Purchase save accepted missing day metadata.");
            Require(!source.Purchase(out _) && source.Money == expectedBalance && source.PaymentRecords.Count == 1,
                "Pending unknown observation allowed a second debit.");
            var shop = source.ShopOwner.CaptureState();
            serialized = JsonConvert.SerializeObject(new ShopObservationCheckpoint
            {
                journal = journal, ledger = ledger, counters = counters,
                session = GameSessionSaveData.From(source.Session.Capture()),
                shop = new DungeonFacilityShopSaveData
                {
                    currentOfferDay = shop.CurrentOfferDay, purchaseSequence = shop.PurchaseSequence,
                    basicPurchaseBuildingIds = shop.BasicPurchaseBuildingIds.ToList(),
                    acquiredBlueprintIds = shop.AcquiredBlueprintIds.ToList()
                },
                treasury = source.Treasury.Current.Copy()
            });
        }
        var saved = JsonConvert.DeserializeObject<ShopObservationCheckpoint>(serialized);
        using (var restored = new FacilityShopScenario(Kind, purchaseDelivery: receiver))
        {
            new TestGameSessionStateStore(restored.Session).Restore(saved.session.ToSnapshot());
            restored.Treasury.Replace(saved.treasury);
            var shop = (IFacilityShopPersistence)restored.ShopOwner;
            shop.PublishRestoreCandidate(shop.BuildRestoreCandidate(new FacilityShopStateSnapshot(
                saved.shop.currentOfferDay, saved.shop.basicPurchaseBuildingIds,
                saved.shop.acquiredBlueprintIds, saved.shop.purchaseSequence)));
            RestoreFacilityShopT01Ledger(restored.Outcomes.Ledger, saved.ledger);
            restored.Outcomes.Transaction.Restore(restored.Outcomes.Transaction.PrepareRestore(saved.counters));
            restored.Facts.Journal.BeginRestoreCandidate();
            restored.Facts.Journal.Restore(saved.journal);
            restored.Facts.Journal.PublishRestoreCandidate();
            restored.Facts.Journal.CompleteRestoreCandidate();
            FacilityShopPurchaseFactPreflight.ValidateOwners(saved.journal, saved.shop, new DungeonResearchSaveData());
            FacilityShopPurchaseFactPreflight.ValidateJournal(saved.journal,
                restored.Outcomes.Ledger.CaptureGameplayOutcomes(), restored.Outcomes.Transaction.Capture());
            receiver.Reject = true;
            Require(!restored.Facts.Journal.RecoverPending(out _) && restored.Facts.Journal.Capture().pending.Count == 1
                && restored.Facts.Journal.Capture().pending[0].observationCommitted,
                "An enqueue failure lost the already committed purchase.");
            // Receive once, then lose the command acknowledgement. Receiver deduplicates the retry.
            receiver.Reject = false; receiver.LoseAcknowledgement = true;
            Require(!restored.Facts.Journal.RecoverPending(out _) && receiver.Accepted.Count == 1
                && restored.Facts.Journal.Capture().pending.Count == 1, "Lost enqueue ACK was not retained.");
            Require(restored.Facts.Journal.RecoverPending(out _) && restored.Facts.Journal.Capture().pending.Count == 0
                && receiver.Accepted.Count == 1 && receiver.Accepted.Contains(publicationKey)
                && restored.Money == expectedBalance && restored.PaymentRecords.Count == 1
                && restored.PurchaseSequence == 1, "Recovery repeated domain effects.");
            var after = restored.Outcomes.Ledger.CaptureGameplayOutcomes();
            var exact = after.exactOutcomes.Single();
            var original = saved.ledger.exactOutcomes.Single();
            Require(after.nextSequence == saved.ledger.nextSequence && exact.sequence == original.sequence
                && exact.immutablePayloadHash == original.immutablePayloadHash
                && exact.operationId == original.operationId, "Recovery changed canonical identity.");
            Require(restored.Facts.Journal.RecoverPending(out _) && restored.PaymentRecords.Count == 1,
                "Empty journal replay repeated payment.");
            Require(restored.Purchase(out var next) && next.purchasePublicationKey != publicationKey
                && restored.PurchaseSequence == 2 && restored.PaymentRecords.Count == 2
                && restored.Money == expectedBalance - next.cost, "Fresh restored owner could not make its next purchase.");
            // Dispose removes the durable observer. The next actual owner command must reject before effects.
            restored.Facts.Dispose();
            int balance = restored.Money;
            Require(!restored.Purchase(out _) && restored.Money == balance && restored.PaymentRecords.Count == 2,
                "Disposed observation lifetime allowed an unobserved payment.");
        }
        Debug.Log("B100_SHOP_ACK_LOSS_STRICT_FRESH_OWNER_REPLAY=PASS");
    }

    [Serializable]
    private sealed class ShopObservationCheckpoint
    {
        public FacilityShopPurchaseJournalData journal;
        public GameplayOutcomeLedgerSaveData ledger;
        public DungeonMigratedProducerOutcomeSaveData counters;
        public GameSessionSaveData session;
        public DungeonFacilityShopSaveData shop;
        public TreasuryEconomyAggregateState treasury;
    }

    // Controlled delivery enqueue authority; physical delivery is exercised by the separate T02 path.
    private sealed class ShopEnqueueProbe : IFacilityShopPurchaseDelivery
    {
        internal readonly HashSet<string> Accepted = new(StringComparer.Ordinal);
        internal bool Reject, LoseAcknowledgement;
        public bool TryAcceptPurchase(FacilityShopPurchaseFact fact, string key, out string failure)
        {
            failure = string.Empty;
            if (Reject) { failure = "fixture-delivery-rejected"; return false; }
            Accepted.Add(key);
            if (LoseAcknowledgement)
            { LoseAcknowledgement = false; throw new InvalidOperationException("fixture-enqueue-acknowledgement-lost"); }
            return true;
        }
    }
}
#endif
