using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

internal sealed class ShopCrimeRuntime
{
    private readonly Shop owner;
    private IFacilityCrimeRiskEvaluator evaluator;
    private IRandomStream random;
    private ShopliftingFactJournal facts;
    private IGameSessionStateProvider gameDataProvider;

    public ShopCrimeRuntime(Shop owner) => this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    public void Configure(IFacilityCrimeRiskEvaluator crimeRiskEvaluator, IRandomStream randomStream)
    {
        evaluator = crimeRiskEvaluator ?? throw new ArgumentNullException(nameof(crimeRiskEvaluator));
        random = randomStream ?? throw new ArgumentNullException(nameof(randomStream));
    }
    public void ConfigureFacts(ShopliftingFactJournal facts, IGameSessionStateProvider gameDataProvider)
    {
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
    }
    public float GetCheckoutChance(IBuildingVisitorPort actor, int cartItemCount, int cartValue)
    {
        EnsureConfigured();
        return evaluator.CalculateShopliftingChance(new FacilityCrimeRiskContext(owner, actor,
            owner.HasServingWorker, owner.HasWaitingCheckout, owner.CurrentUserCount, cartItemCount,
            cartValue, owner.CurrentStock, owner.IsDamaged));
    }

    public bool TryResolve(IBuildingVisitorPort actor, IReadOnlyList<RemainStock> cart)
    {
        if (!Shop.CreatesRevenueFor(actor)) return false;
        EnsureConfigured();
        float chance = GetCheckoutChance(actor, cart?.Count ?? 0,
            cart?.Where(stock => stock != null).Sum(stock => Mathf.Max(0, stock.cost)) ?? 0);
        if (!evaluator.ShouldTriggerCrime(chance, random.NextFloat())) return false;
        RemainStock stock = cart?.FirstOrDefault(value => value != null && value.stock > 0);
        if (stock == null) return false;
        if (facts == null || gameDataProvider == null
            || !gameDataProvider.TryGetSessionState(out var session) || session?.day == null)
            throw new InvalidOperationException("Shoplifting requires its fact journal and current session day.");
        if (!owner.TryPreviewExactRetailLot(stock.id, out var source, out string operation)) return false;
        int originalQuantity = source.quantity;
        source.quantity = 1;
        var fact = new ShopliftingFact(new ShopliftingFactData
        {
            unitOperationId = operation, facilityId = owner.RequirePersistentInstanceId().Value,
            facilityName = owner.DisplayNameForActivity ?? string.Empty,
            actorId = actor.VisitorSnapshot.PersistentId, actorName = actor.VisitorSnapshot.DisplayName ?? string.Empty,
            itemName = stock.itemName ?? string.Empty, absoluteDay = Mathf.Max(0, session.day.Value),
            lossValue = Mathf.Max(0, stock.cost), stockCategory = (int)owner.GetStockCategoryForSaleItem(stock.id),
            chance = chance, sourceQuantityBefore = originalQuantity, lot = source
        });
        if (!facts.TryBegin(fact, out var publication, out string failure))
            throw new InvalidOperationException("Shoplifting outcome reservation failed: " + failure);
        using (publication)
        {
            RetailStockLotSnapshot taken = null;
            IRetailStockSinkTransaction sink = null;
            try
            {
                if (!owner.TryTakeExactRetailLot(stock.id, out taken, out string actualOperation, out _))
                    return false;
                if (operation != actualOperation || taken == null || taken.quantity != 1
                    || taken.sourceOperationId != source.sourceOperationId || taken.saleItemId != source.saleItemId
                    || taken.itemDefinitionId != source.itemDefinitionId || taken.itemInstanceId != source.itemInstanceId
                    || taken.sourceStackId != source.sourceStackId || taken.unitMassGrams != source.unitMassGrams
                    || taken.componentFingerprint != source.componentFingerprint)
                    throw new InvalidOperationException("shoplifting-exact-lot-drift");
                if (!owner.TryCommitRetailSinkReversible(taken, out sink, out failure) || sink == null)
                    throw new InvalidOperationException("Shoplifting physical sink failed: " + failure);
                DomainFactDelivery delivered = publication.PublishApplied();
                if (!delivered.MayBeCommitted)
                    throw new InvalidOperationException("Shoplifting outcome commit failed: " + delivered.Detail);
                sink.Accept();
            }
            finally
            {
                // Unknown publication ACK retains actual effects. Only confirmed rejection
                // or a pre-publication failure can undo this local owner transaction.
                if (!publication.MayBeApplied)
                {
                    if (sink != null && !sink.TryRollback(out string rollbackFailure))
                        throw new InvalidOperationException("shoplifting-physical-rollback:" + rollbackFailure);
                    if (taken != null && !owner.TryRestoreTakenExactRetailLot(taken, out string stockRollbackFailure))
                        throw new InvalidOperationException("shoplifting-stock-rollback:" + stockRollbackFailure);
                }
            }
        }
        owner.PublishShopliftingCompleted(actor, fact);
        return true;
    }

    private void EnsureConfigured()
    {
        if (evaluator == null || random == null)
            throw new InvalidOperationException("Shop crime runtime requires evaluator and random stream injection.");
    }
}
