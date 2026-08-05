using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class ShopCrimeRuntime
{
    private readonly Shop owner;
    private IFacilityCrimeRiskEvaluator evaluator;
    private IRandomStream random;

    public ShopCrimeRuntime(Shop owner)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public void Configure(
        IFacilityCrimeRiskEvaluator crimeRiskEvaluator,
        IRandomStream randomStream)
    {
        evaluator = crimeRiskEvaluator
            ?? throw new ArgumentNullException(nameof(crimeRiskEvaluator));
        random = randomStream
            ?? throw new ArgumentNullException(nameof(randomStream));
    }

    public float GetCheckoutChance(
        IBuildingVisitorPort actor,
        int cartItemCount,
        int cartValue)
    {
        EnsureConfigured();
        return evaluator.CalculateShopliftingChance(new FacilityCrimeRiskContext(
            owner,
            actor,
            owner.HasServingWorker,
            owner.HasWaitingCheckout,
            owner.CurrentUserCount,
            cartItemCount,
            cartValue,
            owner.CurrentStock,
            owner.IsDamaged));
    }

    public bool TryResolve(
        IBuildingVisitorPort actor,
        IReadOnlyList<RemainStock> cart)
    {
        if (!Shop.CreatesRevenueFor(actor))
        {
            return false;
        }

        EnsureConfigured();
        int cartItemCount = cart?.Count ?? 0;
        int cartValue = GetCartValue(cart);
        float chance = GetCheckoutChance(actor, cartItemCount, cartValue);
        if (!evaluator.ShouldTriggerCrime(chance, random.NextFloat()))
        {
            return false;
        }

        RemainStock stolenStock = cart?.FirstOrDefault(stock =>
            stock != null && stock.stock > 0);
        if (stolenStock == null)
        {
            return false;
        }

        stolenStock.stock--;
        owner.NotifyStockChanged();
        StockCategory category = owner.GetStockCategoryForSaleItem(stolenStock.id);
        owner.PublishStockConsumed(actor, category);
        if (!owner.TryGetSaleItem(stolenStock.id, out SaleItem saleItem)
            || saleItem == null
            || !saleItem.ItemDefinitionId.IsValid)
        {
            throw new InvalidOperationException(
                $"Stolen sale item '{stolenStock.id}' has no authored physical item definition.");
        }

        AddCustomerCarriedStock(
            actor,
            saleItem.ItemDefinitionId.Value,
            $"theft:{stolenStock.id}:{owner.CurrentGameFrame}");

        int lossValue = Mathf.Max(0, stolenStock.cost);
        string detail = BuildCrimeDetail(actor, stolenStock, lossValue, chance);
        owner.PublishShopliftingCrime(actor, detail, lossValue);
        actor?.RecordActivity(owner, new BuildingActivitySnapshot(
            BuildingActivityKinds.Social,
            BuildingActivityOutcomes.Damaged,
            detail,
            actionId: "crime:shoplifting",
            reasonCode: "shoplifting",
            value: lossValue,
            quantity: 1,
            bubbleEligible: true));
        return true;
    }

    private string BuildCrimeDetail(
        IBuildingVisitorPort actor,
        RemainStock stolenStock,
        int lossValue,
        float chance)
    {
        string actorName = actor?.VisitorSnapshot.DisplayName ?? "Unknown customer";
        string itemName = stolenStock != null
            && !string.IsNullOrWhiteSpace(stolenStock.itemName)
                ? stolenStock.itemName
                : "item";
        return $"{actorName} stole {itemName} from {owner.DisplayNameForActivity} "
            + $"(loss {lossValue}, chance {chance:0.##}).";
    }

    private static int GetCartValue(IReadOnlyList<RemainStock> cart)
    {
        return cart?.Where(stock => stock != null)
            .Sum(stock => Mathf.Max(0, stock.cost)) ?? 0;
    }

    private static void AddCustomerCarriedStock(
        IBuildingVisitorPort actor,
        string itemDefinitionId,
        string sourceId)
    {
        if (actor == null)
        {
            return;
        }

        actor.AddCarriedItem(
            sourceId,
            itemDefinitionId,
            1);
    }

    private void EnsureConfigured()
    {
        if (evaluator == null || random == null)
        {
            throw new InvalidOperationException(
                "Shop crime runtime requires evaluator and random stream injection.");
        }
    }
}
