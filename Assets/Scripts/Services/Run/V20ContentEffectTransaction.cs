using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;
using static V20ContentEffectEvidence;

internal sealed class V20ContentEffectTransaction
{
    private readonly IWorldDropZoneQuery dropZones;
    private readonly IWorldItemStackRuntime items;
    private readonly IGameMoneyAccount money;
    private readonly IItemReservationService reservations;
    private readonly IStockQuery stock;
    private readonly IIdempotentAtomicItemConsumptionService atomicItems;
    private readonly IPhysicalItemExactSourcePublicationService exactSources;
    private readonly V20ContentTypedEffectRuntime typedEffects;

    internal V20ContentEffectTransaction(
        IWorldDropZoneQuery dropZones,
        IWorldItemStackRuntime items,
        IGameMoneyAccount money,
        IItemReservationService reservations,
        IStockQuery stock,
        IIdempotentAtomicItemConsumptionService atomicItems,
        IPhysicalItemExactSourcePublicationService exactSources,
        V20ContentTypedEffectRuntime typedEffects)
    {
        this.dropZones = dropZones;
        this.items = items;
        this.money = money;
        this.reservations = reservations;
        this.stock = stock;
        this.atomicItems = atomicItems;
        this.exactSources = exactSources;
        this.typedEffects = typedEffects;
    }

    internal V21ContentEffectExecutionRegistry Handlers => typedEffects.Handlers;

    internal bool TryPreflightEffects(
        ContentResolutionRequest request,
        IReadOnlyList<V20ResolvedEventResult> resolutions,
        out EffectCommitPlan plan,
        out DomainFailure failure)
    {
        plan = new EffectCommitPlan(request.ActionId);
        failure = DomainFailure.None;
        foreach (V20ContentEffect effect in resolutions.SelectMany(value => value.Effects))
        {
            if (effect == null || !effect.IsValid) continue;
            if (!Handlers.TryGet(effect.kind, out _))
            {
                failure = new DomainFailure(FailureCode.ServiceFeatureMissing, $"content-effect-handler:{effect?.kind}");
                return false;
            }
        }
        V20ContentEffect[] effects = resolutions.SelectMany(value => value.Effects)
            .Where(value => value != null && value.IsValid)
            .ToArray();
        int requiredReceipts = (effects.Any(value => value.kind == V20ContentEffectKind.ItemConsume
                && Mathf.RoundToInt(value.amount) > 0) ? 1 : 0)
            + (effects.Any(value => value.kind == V20ContentEffectKind.ItemGrant
                && Mathf.RoundToInt(value.amount) > 0) ? 1 : 0);
        if (requiredReceipts > 0 && (items is not IPhysicalEffectReceiptQuery receiptQuery
            || receiptQuery.AvailablePhysicalEffectReceiptCapacity < requiredReceipts))
        {
            failure = new DomainFailure(FailureCode.ItemTransferConsumptionFailed,
                "physical-effect-receipt-capacity-exhausted");
            return false;
        }
        plan.MoneyDelta = effects
            .Where(value => value.kind == V20ContentEffectKind.Money)
            .Sum(value => Mathf.RoundToInt(value.amount));
        if (plan.MoneyDelta != 0
            && (money is not IIdempotentGameMoneyAccount
                || money is not IGameMoneyTransactionQuery))
        {
            failure = new DomainFailure(
                FailureCode.ServiceFeatureMissing,
                "content-money-transaction-authority");
            return false;
        }
        if (plan.MoneyDelta < 0 && !money.CanSpend(-plan.MoneyDelta))
        {
            failure = new DomainFailure(
                FailureCode.InsufficientGold,
                money.Balance.ToString(),
                (-plan.MoneyDelta).ToString());
            return false;
        }
        if (!V21ContentEffectCommitPreflight.TryPlanItemCosts(
                effects,
                stock.GetAllStacks(),
                out IReadOnlyList<ReservedItemConsumption> itemCosts,
                out string missingItemId))
        {
            failure = new DomainFailure(
                FailureCode.ProductionMaterialsMissing,
                missingItemId);
            return false;
        }
        plan.ItemCosts.AddRange(itemCosts);
        if (plan.ItemCosts.Count > 0
            && !reservations.TryReserveQuantities(
                plan.ItemCosts,
                plan.ReservationOwnerId,
                ItemReservationPurpose.DirectPlayerOrder,
                $"content-resolution:{plan.ReservationOwnerId}:costs"))
        {
            failure = new DomainFailure(FailureCode.ItemTransferStackUnavailable);
            return false;
        }

        plan.Grants.AddRange(effects.Where(value =>
            value.kind == V20ContentEffectKind.ItemGrant
            && Mathf.RoundToInt(value.amount) > 0));
        Vector2Int grantDropoff = default;
        if (plan.Grants.Count > 0
            && (!dropZones.TryGetDeliveryDropoff(out grantDropoff)
                || plan.Grants.Any(value => !items.CatalogProvider.TryGetDefinition(
                    value.targetId,
                    out _))))
        {
            plan.Release(reservations);
            failure = new DomainFailure(FailureCode.ProductionOutputUnavailable);
            return false;
        }
        plan.Dropoff = grantDropoff;

        if (!typedEffects.TryPreflight(request.AbsoluteDay, resolutions, out failure))
        {
            plan.Release(reservations);
            return false;
        }
        return true;
    }

    internal bool TryCommitEffects(
        ContentResolutionRequest request,
        IReadOnlyList<V20ResolvedEventResult> resolutions,
        EffectCommitPlan plan,
        out DomainFailure failure,
        out V20ContentResolutionRecoveryPhase recoveryPhase,
        out V20ContentEffectCommitReferences childCommits)
    {
        failure = DomainFailure.None;
        recoveryPhase = V20ContentResolutionRecoveryPhase.Prepared;
        childCommits = new V20ContentEffectCommitReferences
        {
            ItemConsumptionOperationId = plan.ItemCosts.Count > 0
                ? plan.ReservationOwnerId
                : string.Empty,
            ItemGrantBatchCommitId = plan.Grants.Count > 0
                ? plan.CreateGrantPublicationPlan().BatchCommitId
                : string.Empty
        };
        PhysicalItemExactSourcePublicationTransaction grantTransaction = default;
        bool hasPreparedGrants = plan.Grants.Count > 0;
        if (hasPreparedGrants
            && !exactSources.TryPrepare(
                plan.CreateGrantPublicationPlan(),
                out grantTransaction,
                out _))
        {
            plan.Release(reservations);
            failure = new DomainFailure(FailureCode.ProductionOutputUnavailable);
            return false;
        }
        if (hasPreparedGrants
            && !string.Equals(
                grantTransaction.BatchCommitId,
                childCommits.ItemGrantBatchCommitId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Prepared content grant did not preserve its deterministic batch identity.");
        }
        EconomyTransactionContext transaction = new(
            plan.MoneyDelta >= 0
                ? EconomyTransactionKind.ContractIncome
                : EconomyTransactionKind.LegacyExpense,
            $"content:{request.ActionId}",
            description: request.Kind.ToString());
        IIdempotentGameMoneyAccount idempotentMoney =
            money as IIdempotentGameMoneyAccount;
        bool moneyCommitted = plan.MoneyDelta > 0
            ? idempotentMoney.TryCreditOnce(
                plan.MoneyDelta,
                transaction,
                out _)
            : plan.MoneyDelta < 0
                ? idempotentMoney.TrySpendOnce(
                    -plan.MoneyDelta,
                    transaction,
                    out _,
                    out _)
                : true;
        if (!moneyCommitted)
        {
            RollbackPreparedGrantsOrThrow(
                grantTransaction,
                hasPreparedGrants,
                "money-commit-failed");
            plan.Release(reservations);
            failure = new DomainFailure(
                FailureCode.InsufficientGold,
                money.Balance.ToString(),
                (-plan.MoneyDelta).ToString());
            return false;
        }
        if (plan.MoneyDelta != 0)
        {
            if (money is not IGameMoneyTransactionQuery moneyQuery
                || !moneyQuery.TryGetSuccessfulTransaction(
                    transaction.kind,
                    transaction.sourceId,
                    out EconomyTransactionRecord moneyRecord)
                || moneyRecord == null
                || moneyRecord.amount != plan.MoneyDelta
                || string.IsNullOrWhiteSpace(moneyRecord.transactionId))
            {
                throw new InvalidOperationException(
                    "Committed content money effect is missing its exact transaction record.");
            }
            childCommits.MoneyTransactionId = moneyRecord.transactionId;
        }
        PhysicalEffectReceiptSaveData consumptionReceipt = null;
        if (plan.ItemCosts.Count > 0 && !atomicItems.TryConsumeReservedOnce(
                plan.ItemCosts,
                plan.ReservationOwnerId,
                out consumptionReceipt,
                out failure))
        {
            if (plan.MoneyDelta > 0)
            {
                money.TrySpend(plan.MoneyDelta, transaction, out _);
            }
            else if (plan.MoneyDelta < 0)
            {
                money.Add(-plan.MoneyDelta, transaction);
            }
            RollbackPreparedGrantsOrThrow(
                grantTransaction,
                hasPreparedGrants,
                "item-consumption-failed");
            plan.Release(reservations);
            return false;
        }
        childCommits.ItemConsumptionCommitId = consumptionReceipt?.commitId ?? string.Empty;
        recoveryPhase = V20ContentResolutionRecoveryPhase.ItemCostsCommitted;
        PhysicalItemExactSourcePublicationReceipt grantReceipt = default;
        if (hasPreparedGrants
            && !exactSources.TryCommitReleased(
                grantTransaction,
                plan.Dropoff,
                "content-resolution-grant",
                out grantReceipt,
                out string grantCommitFailure))
        {
            // An uncommitted prepared output is reversible; retire it before a
            // costs-only checkpoint. A partially committed transaction remains
            // guarded and is resumed by TryPrepare on the same exact plan.
            bool rolledBack = exactSources.TryRollback(grantTransaction,
                "content-grant-commit-failed", out string rollbackFailure);
            throw new InvalidOperationException(
                "Content item costs committed but exact grant publication could not finalize: "
                + grantCommitFailure + (rolledBack ? "; uncommitted grant retired" : "; forward retry required: " + rollbackFailure));
        }
        if (hasPreparedGrants
            && !string.Equals(
                grantReceipt.BatchCommitId,
                childCommits.ItemGrantBatchCommitId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Committed content grant receipt changed its prepared batch identity.");
        }
        childCommits.ItemGrantReceiptId = grantReceipt.RecoveryCommitId ?? string.Empty;
        recoveryPhase = V20ContentResolutionRecoveryPhase.ItemGrantsCommitted;
        typedEffects.ApplyTypedDomainEffects(request.AbsoluteDay, resolutions);
        recoveryPhase = V20ContentResolutionRecoveryPhase.TypedEffectsApplied;
        return true;
    }

    internal static bool RequiresForwardRecovery(
        EffectCommitPlan plan,
        IReadOnlyList<V20ResolvedEventResult> resolutions,
        V21ContentEffectExecutionRegistry handlers = null)
    {
        handlers ??= V21ContentEffectExecutionRegistry.Default;
        if (plan == null)
            throw new ArgumentNullException(nameof(plan));
        if (plan.MoneyDelta != 0
            || plan.ItemCosts.Count > 0
            || plan.Grants.Count > 0)
        {
            return true;
        }

        return (resolutions ?? Array.Empty<V20ResolvedEventResult>())
            .SelectMany(value => value.Effects)
            .Any(effect => effect != null && effect.IsValid && handlers.Require(effect.kind).RequiresForwardRecovery);
    }

    private void RollbackPreparedGrantsOrThrow(
        PhysicalItemExactSourcePublicationTransaction transaction,
        bool prepared,
        string reasonCode)
    {
        if (!prepared)
            return;
        if (!exactSources.TryRollback(
                transaction,
                reasonCode,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Prepared content grant rollback failed: " + failureReason);
        }
    }

    internal sealed class EffectCommitPlan
    {
        public EffectCommitPlan(string actionId)
        {
            string normalized = actionId?.Trim() ?? string.Empty;
            ReservationOwnerId = "content-resolution:" + normalized;
        }

        public string ReservationOwnerId { get; }
        public int MoneyDelta { get; set; }
        public Vector2Int Dropoff { get; set; }
        public List<ReservedItemConsumption> ItemCosts { get; } = new();
        public List<V20ContentEffect> Grants { get; } = new();

        public PhysicalItemExactSourcePublicationPlan CreateGrantPublicationPlan() =>
            V20ContentRecoveryEvidence.CreateGrantPlan(ReservationOwnerId, Dropoff, Grants);

        public void Release(IItemReservationService service)
        {
            foreach (ReservedItemConsumption cost in ItemCosts)
                service.Release(cost.StackId, ReservationOwnerId);
        }
    }
}
