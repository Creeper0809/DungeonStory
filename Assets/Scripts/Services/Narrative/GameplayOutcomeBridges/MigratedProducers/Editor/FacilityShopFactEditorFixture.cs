#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

internal sealed class FacilityShopFactEditorFixture : IDisposable
{
    private readonly IDisposable subscription;
    internal DomainFactDispatcher Dispatcher { get; } = new();
    internal FacilityShopPurchaseJournal Journal { get; }
    internal bool HideNextAcknowledgement { get; set; }
    internal bool BlockRecovery { get; set; }
    internal int HiddenAcknowledgements { get; private set; }
    internal FacilityShopFactEditorFixture(MigratedProducerOutcomeEditorFixture outcomes,
        MigratedProducerOutcomeProbeTransaction probe, IFacilityShopPurchaseDelivery delivery = null)
    {
        subscription = Dispatcher.Subscribe("fixture:facility-shop.purchase",
            new MigratedProducerDomainFactProbeObserver<FacilityShopPurchaseFact>(
                FacilityShopPurchaseFactMapping.Capture, outcomes.Transaction,
                new GameplayDomainFactCommitGroup(outcomes.Recorder, outcomes.Ledger), probe));
        var transport = new FaultingTransport(this);
        Journal = new FacilityShopPurchaseJournal(transport, transport, delivery ?? new UnavailableDelivery());
    }
    internal static GameplayResultKey ReadKey(FacilityShopPurchaseResult result) =>
        GameplayDomainFactDeliveryQuery.TryReadKey(result.purchasePublicationKey, out var key) ? key : default;

    // Only for legacy controlled delivery fixtures, never a production purchase or training record.
    internal static FacilityShopPurchaseFact FromResult(FacilityShopPurchaseResult result)
    {
        if (!result.success || !ReadKey(result).IsValid)
            throw new ArgumentException("fixture-purchase-result-required");
        string identity = ReadKey(result).OperationId.Value;
        return new FacilityShopPurchaseFact(new FacilityShopPurchaseFactData
        {
            operationId = identity, paymentSourceId = identity, sequence = 1, absoluteDay = 1,
            shopSource = "fixture", offerTypeId = result.offerTypeId, offerDataId = result.dataId,
            offerName = result.purchasedOffer.DisplayName, quotedCost = result.purchasedOffer.Cost,
            paidCost = result.cost, balanceBefore = result.balanceBefore, balanceAfter = result.balanceAfter,
            paymentTransactionId = result.paymentTransactionId,
            itemDefinitionId = result.purchasedOffer switch
            {
                FacilityBuildingOffer building => FacilityInstallationKitItemIds.ForBuilding(building.Building),
                FacilityBlueprintOffer blueprint => blueprint.Blueprint.PhysicalItemId,
                _ => throw new ArgumentException("fixture-purchase-offer-required")
            }
        });
    }
    private sealed class UnavailableDelivery : IFacilityShopPurchaseDelivery
    {
        public bool TryAcceptPurchase(FacilityShopPurchaseFact fact, string key, out string failure)
        { failure = "fixture-delivery-not-in-scope"; return false; }
    }
    private sealed class FaultingTransport : IDomainFactPublisher, IDomainFactRecoveryPublisher
    {
        private readonly FacilityShopFactEditorFixture owner;
        internal FaultingTransport(FacilityShopFactEditorFixture owner) => this.owner = owner;
        public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure) =>
            owner.Dispatcher.TryReserve(fact, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact)
        {
            var result = owner.Dispatcher.Publish(lease, fact);
            if (!owner.HideNextAcknowledgement || !result.MayBeCommitted) return result;
            owner.HideNextAcknowledgement = false; owner.HiddenAcknowledgements++;
            return DomainFactDelivery.Uncertain("fixture-native-acknowledgement-lost");
        }
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> facts, DomainFactDelivery[] results, out string failure)
        {
            if (owner.BlockRecovery) { failure = "fixture-recovery-unavailable"; return false; }
            return owner.Dispatcher.ReplayBatch(facts, results, out failure);
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure) =>
            owner.Dispatcher.PublishBatch(facts, results, out failure);
        public DomainFactDelivery Replay<T>(in T fact) => owner.Dispatcher.Replay(fact);
        public void Cancel(in DomainFactLease lease) => owner.Dispatcher.Cancel(lease);
    }
    public void Dispose() { subscription.Dispose(); Dispatcher.Dispose(); }
}
#endif
