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
    private IMigratedProducerOutcomeTransaction outcomeTransactions;
    private IGameSessionStateProvider gameDataProvider;

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

    public void ConfigureOutcomeTransactions(
        IMigratedProducerOutcomeTransaction outcomeTransactions,
        IGameSessionStateProvider gameDataProvider)
    {
        this.outcomeTransactions = outcomeTransactions
            ?? throw new ArgumentNullException(nameof(outcomeTransactions));
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
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

        if (outcomeTransactions == null
            || gameDataProvider == null
            || !gameDataProvider.TryGetSessionState(out GameSessionState gameData)
            || gameData?.day == null)
        {
            throw new InvalidOperationException(
                "Shoplifting outcome requires the migrated transaction and current session day.");
        }
        if (!owner.TryPreviewExactRetailLotOperationId(
                stolenStock.id,
                out string previewOperationId))
        {
            return false;
        }

        string facilityId = owner.RequirePersistentInstanceId().Value;
        string actorId = actor?.VisitorSnapshot.PersistentId ?? string.Empty;
        StockCategory category = owner.GetStockCategoryForSaleItem(stolenStock.id);
        int absoluteDay = Mathf.Max(0, gameData.day.Value);
        string outcomeIdentity = "facility-crime:shoplifting:"
            + previewOperationId
            + ":facility=" + facilityId
            + ":actor=" + actorId;
        if (!outcomeTransactions.TryReserveSingleSubject(
                MigratedProducerOutcomeKind.FacilityCrimeEvent,
                outcomeIdentity,
                absoluteDay,
                GameplayOutcomeStatus.Succeeded,
                out PreparedMigratedProducerOutcome prepared,
                out string reserveFailure))
        {
            throw new InvalidOperationException(
                "Shoplifting outcome reservation failed: " + reserveFailure);
        }

        RetailStockLotSnapshot stolenLot;
        string unitOperationId;
        bool lotTaken;
        try
        {
            lotTaken = owner.TryTakeExactRetailLot(
                stolenStock.id,
                out stolenLot,
                out unitOperationId,
                out _);
        }
        catch
        {
            outcomeTransactions.Cancel(prepared);
            throw;
        }
        if (!lotTaken)
        {
            outcomeTransactions.Cancel(prepared);
            return false;
        }

        if (!string.Equals(
                previewOperationId,
                unitOperationId,
                StringComparison.Ordinal))
        {
            outcomeTransactions.Cancel(prepared);
            RestoreStolenLotOrThrow(stolenLot, "shoplifting-operation-drift");
            throw new InvalidOperationException(
                "Shoplifting operation identity drifted before mutation.");
        }
        if (stolenLot == null
            || stolenLot.quantity != 1
            || string.IsNullOrWhiteSpace(stolenLot.itemDefinitionId))
        {
            outcomeTransactions.Cancel(prepared);
            RestoreStolenLotOrThrow(stolenLot, "shoplifting-lot-invalid");
            throw new InvalidOperationException(
                $"Stolen sale item '{stolenStock.id}' produced no exact physical lot receipt.");
        }

        int lossValue = Mathf.Max(0, stolenStock.cost);
        string detail = BuildCrimeDetail(actor, stolenStock, lossValue, chance);
        string commitOperationId = $"shoplifting-commit:{unitOperationId}";
        bool outcomeCommitted = false;
        bool rollbackAttempted = false;
        try
        {
            MigratedProducerOutcomeCommitResult committed =
                outcomeTransactions.CommitSingleSubject(
                    prepared,
                    new MigratedProducerOutcomeSubject(
                        MigratedProducerOutcomeIds.FacilityKind,
                        facilityId,
                        owner.DisplayNameForActivity,
                        MigratedProducerOutcomeIds.FacilityRole),
                    "시설 절도 확정: operation=" + commitOperationId
                    + "; actor=" + actorId
                    + "; item=" + stolenLot.itemDefinitionId
                    + "; loss=" + lossValue
                    + "; day=" + absoluteDay);
            if (!committed.DurablyCommitted)
            {
                rollbackAttempted = true;
                RestoreStolenLotOrThrow(
                    stolenLot,
                    "shoplifting-outcome-rejected");
                throw new InvalidOperationException(
                    "Shoplifting outcome commit failed: "
                    + committed.DetailCode);
            }
            outcomeCommitted = true;

            if (!owner.TryCommitExactRetailExternalSink(
                    stolenLot,
                    out string sinkFailure))
            {
                throw new InvalidOperationException(
                    "Shoplifting terminal sink failed after durable outcome '"
                    + commitOperationId + "': " + sinkFailure);
            }
        }
        catch
        {
            if (!outcomeCommitted && !rollbackAttempted)
            {
                outcomeTransactions.Cancel(prepared);
                RestoreStolenLotOrThrow(
                    stolenLot,
                    "shoplifting-outcome-exception");
            }
            throw;
        }

        PublishPostCommit(
            () => owner.PublishStockConsumed(actor, category),
            "shoplifting-stock-consumed");
        PublishPostCommit(
            () => owner.PublishShopliftingCrime(
                actor,
                detail,
                lossValue,
                commitOperationId,
                stolenLot),
            "shoplifting-crime-event");
        PublishPostCommit(
            () => actor?.RecordActivity(owner, new BuildingActivitySnapshot(
                BuildingActivityKinds.Social,
                BuildingActivityOutcomes.Damaged,
                detail,
                actionId: "crime:shoplifting",
                reasonCode: "shoplifting",
                value: lossValue,
                quantity: 1,
                bubbleEligible: true)),
            "shoplifting-activity");
        return true;
    }

    private void RestoreStolenLotOrThrow(
        RetailStockLotSnapshot stolenLot,
        string reason)
    {
        if (!owner.TryRestoreTakenExactRetailLot(
                stolenLot,
                out string restoreFailure))
        {
            throw new InvalidOperationException(
                "Shoplifting rollback '" + reason
                + "' could not restore exact lot '"
                + stolenLot?.sourceOperationId + "': " + restoreFailure);
        }
    }

    private static void PublishPostCommit(Action observer, string label)
    {
        try
        {
            observer?.Invoke();
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(label + ":" + exception.GetType().Name);
        }
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

    private void EnsureConfigured()
    {
        if (evaluator == null || random == null)
        {
            throw new InvalidOperationException(
                "Shop crime runtime requires evaluator and random stream injection.");
        }
    }
}
