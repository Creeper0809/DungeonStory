#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class MigratedProducerEconomyEntryExtendedE2EDebugScenarios
{
    public static void RunDailyOfferOwnershipForBatch()
    {
        VerifyInternalBuildingOfferRejection();
        const MigratedProducerOutcomeKind kind = MigratedProducerOutcomeKind.FacilityShopPurchaseResult;
        var receiver = new ShopEnqueueProbe();
        string purchased;
        using (var scenario = new FacilityShopScenario(kind, purchaseDelivery: receiver))
        {
            FacilityShopSaveSection section = scenario.CreateShopSaveSection();
            string before = section.Capture();
            string key = scenario.ShopOwner.GetDailyOfferKey(0);
            Require(scenario.Purchase(out _), "Daily ownership fixture purchase failed.");
            purchased = section.Capture();
            int money = scenario.Money, payments = scenario.PaymentRecords.Count;
            Require(scenario.ShopOwner.WasDailyOfferPurchased(key), "Committed daily purchase was not recorded.");
            Require(!scenario.ShopOwner.TryPurchaseDailyOffer(0, scenario.Session,
                    new EconomyTransactionContext(EconomyTransactionKind.AutoProcurement,
                        "wishlist:test", "test", "ownership test"), out var duplicate)
                    && duplicate.failureStage == FacilityShopPurchaseFailureStage.AlreadyPurchased
                    && scenario.Money == money && scenario.PaymentRecords.Count == payments,
                "Automatic purchase bypassed the daily owner or changed payment.");
            scenario.ShopOwner.Refresh(5);
            Require(section.Capture() == purchased && !scenario.Purchase(out _),
                "Same-day refresh reset the purchase restriction.");
            section.Restore(before, section.SectionVersion, new DungeonGameRestoreReport());
            Require(!scenario.ShopOwner.WasDailyOfferPurchased(key), "An earlier save retained the later UI purchase cache.");
            section.Restore(purchased, section.SectionVersion, new DungeonGameRestoreReport());
            Require(!scenario.Purchase(out _) && scenario.Money == money,
                "Restored daily purchase allowed another debit.");
            foreach (string corruption in new[] { "missing", "null", "duplicate", "wrong-day" })
            {
                var json = JObject.Parse(purchased);
                if (corruption == "missing") json.Remove("purchasedDailyOfferKeys");
                if (corruption == "null") json["purchasedDailyOfferKeys"] = JValue.CreateNull();
                if (corruption == "duplicate") ((JArray)json["purchasedDailyOfferKeys"]).Add(key);
                if (corruption == "wrong-day") json["currentOfferDay"] = 6;
                bool rejected = false;
                try { section.Restore(json.ToString(Formatting.None), section.SectionVersion, new DungeonGameRestoreReport()); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected && section.Capture() == purchased && scenario.Money == money,
                    "Daily purchase corruption mutated live state: " + corruption);
            }
            var legacy = JObject.Parse(purchased);
            legacy.Remove("dailyOfferPurchaseStateVersion"); legacy.Remove("purchasedDailyOfferKeys");
            section.Restore(legacy.ToString(Formatting.None), section.SectionVersion, new DungeonGameRestoreReport());
            Require(scenario.PurchaseSequence == 1 && scenario.Money == money
                && scenario.ShopOwner.CaptureState().PurchasedDailyOfferKeys.Count == 0,
                "Legacy V1 compatibility fabricated purchase records or lost existing state.");
        }
        using (var fresh = new FacilityShopScenario(kind, purchaseDelivery: receiver))
        {
            FacilityShopSaveSection section = fresh.CreateShopSaveSection();
            section.Restore(purchased, section.SectionVersion, new DungeonGameRestoreReport());
            Require(section.Capture() == purchased && !fresh.Purchase(out var duplicate)
                && duplicate.failureStage == FacilityShopPurchaseFailureStage.AlreadyPurchased
                && fresh.Money == fresh.InitialMoney && fresh.PaymentRecords.Count == 0,
                "A fresh owner lost the saved daily restriction.");
            fresh.ShopOwner.Refresh(6);
            Require(fresh.Purchase(out _) && fresh.PurchaseSequence == 2,
                "A new day did not permit its new offer.");
        }
        using (var retry = new FacilityShopScenario(kind, MigratedProducerOutcomeFaultMode.RejectCommit,
                   purchaseDelivery: receiver))
        {
            string before = retry.CreateShopSaveSection().Capture();
            Require(!retry.Purchase(out _) && retry.CreateShopSaveSection().Capture() == before
                && retry.Money == retry.InitialMoney && retry.PaymentRecords.Count == 0,
                "Rejected publication consumed the daily offer.");
            retry.Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            Require(retry.Purchase(out _) && retry.PurchaseSequence == 1 && retry.PaymentRecords.Count == 1,
                "Failed daily purchase could not retry exactly once.");
        }
        VerifyShopUnknownAcknowledgementRestore();
        Debug.Log("R27_DAILY_OFFER_OWNER_SAVE_RESTORE_RETRY=PASS");
    }

    private static void VerifyInternalBuildingOfferRejection()
    {
        var internalBuilding = UnityEditor.AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_ReceptionPoint_FloorOverlay.asset");
        var purchasable = UnityEditor.AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/P1/P1_SpikeTrap.asset");
        Require(internalBuilding != null && internalBuilding.id < 0 && purchasable != null,
            "Authored internal and purchasable building fixtures are required.");
        var offers = FacilityShopService.CreateDailyOffers(5, new[] { internalBuilding, purchasable },
            Array.Empty<FacilityBlueprintSO>(), 0, _ => 1f, _ => 1f,
            CharacterAiEditorTestDependencies.AuthoredGameplay);
        Require(offers.Count == 1 && offers[0].DataId == purchasable.id && offers[0].IsValid
            && FacilityShopUnlockState.IsDailyOfferKey(
                FacilityShopUnlockState.DailyOfferKey(5, 0, offers[0].OfferTypeId, offers[0].DataId), 5),
            "An internal runtime archetype occupied a daily shop slot.");
        using var scenario = new FacilityShopScenario(MigratedProducerOutcomeKind.FacilityShopPurchaseResult);
        int money = scenario.Money;
        string before = scenario.CreateShopSaveSection().Capture();
        var account = new EditorGameMoneyAccount(scenario.Session,
            new EconomyTransactionLedgerRuntime(new UnityGameClock(), scenario.Treasury));
        var invalidOffer = new FacilityBuildingOffer(internalBuilding, 100, FacilityShopRarity.Common, false, true);
        Require(!invalidOffer.IsValid && !FacilityShopService.CanEnterBasicPurchase(internalBuilding)
            && !FacilityShopService.TryPurchaseOffer(account, invalidOffer,
                scenario.ShopOwner.UnlockState, default, DisabledDungeonDebugRuleQuery.Instance, out var rejected)
            && rejected.failureStage == FacilityShopPurchaseFailureStage.OfferValidation
            && scenario.Money == money && scenario.PaymentRecords.Count == 0
            && scenario.CreateShopSaveSection().Capture() == before,
            "Direct purchase of an internal archetype changed money or shop state.");
        Debug.Log("R27_INTERNAL_BUILDING_SHOP_REJECTION=PASS");
    }

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
            serialized = JsonConvert.SerializeObject(new ShopObservationCheckpoint
            {
                journal = journal, ledger = ledger, counters = counters,
                session = GameSessionSaveData.From(source.Session.Capture()),
                shop = JsonUtility.FromJson<DungeonFacilityShopSaveData>(source.CreateShopSaveSection().Capture()),
                treasury = source.Treasury.Current.Copy()
            });
        }
        var saved = JsonConvert.DeserializeObject<ShopObservationCheckpoint>(serialized);
        using (var restored = new FacilityShopScenario(Kind, purchaseDelivery: receiver))
        {
            new TestGameSessionStateStore(restored.Session).Restore(saved.session.ToSnapshot());
            restored.Treasury.Replace(saved.treasury);
            var shop = restored.CreateShopSaveSection();
            shop.Restore(JsonUtility.ToJson(saved.shop), shop.SectionVersion, new DungeonGameRestoreReport());
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
            Require(!restored.Purchase(out var duplicate)
                && duplicate.failureStage == FacilityShopPurchaseFailureStage.AlreadyPurchased
                && restored.Money == expectedBalance && restored.PaymentRecords.Count == 1,
                "Restored acknowledged purchase lost its daily restriction.");
            restored.ShopOwner.Refresh(6);
            Require(restored.Purchase(out var next) && next.purchasePublicationKey != publicationKey
                && restored.PurchaseSequence == 2 && restored.PaymentRecords.Count == 2
                && restored.Money == expectedBalance - next.cost, "Fresh restored owner could not make its next purchase.");
            // Dispose removes the durable observer. The next actual owner command must reject before effects.
            restored.Facts.Dispose();
            restored.ShopOwner.Refresh(7);
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
