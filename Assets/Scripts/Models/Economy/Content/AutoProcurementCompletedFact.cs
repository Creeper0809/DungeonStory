using System;
using System.Collections.Generic;

/// <summary>
/// Immutable economy-owned snapshot of one completed auto-procurement result.
/// </summary>
public sealed class AutoProcurementCompletedFact
{
    public readonly struct Stack
    {
        internal Stack(string stackId, string itemInstanceId, int quantity)
        {
            StackId = stackId;
            ItemInstanceId = itemInstanceId;
            Quantity = quantity;
        }

        public string StackId { get; }
        public string ItemInstanceId { get; }
        public int Quantity { get; }
    }

    private readonly IReadOnlyList<Stack> physicalStacks;

    public AutoProcurementCompletedFact(
        string offerKey,
        AutoProcurementResult result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));
        if (result.day < 0)
            throw new ArgumentOutOfRangeException(nameof(result));
        if (result.quantity < 0
            || result.cost < 0
            || result.attemptedQuantity < 0
            || result.attemptedCost < 0
            || result.committedQuantity < 0
            || result.committedCost < 0)
        {
            throw new ArgumentException(
                "Auto-procurement quantities and costs cannot be negative.",
                nameof(result));
        }

        IReadOnlyList<AutoProcurementPhysicalStackRef> source =
            result.physicalStacks == null
                ? Array.Empty<AutoProcurementPhysicalStackRef>()
                : result.physicalStacks;
        var copiedStacks = new Stack[source.Count];
        for (int index = 0; index < source.Count; index++)
        {
            AutoProcurementPhysicalStackRef value = source[index];
            if (value == null
                || !(new ItemStackId(value.stackId)).IsValid
                || value.quantity <= 0)
            {
                throw new ArgumentException(
                    "Auto-procurement physical stack evidence is invalid.",
                    nameof(result));
            }
            copiedStacks[index] = new Stack(
                value.stackId ?? string.Empty,
                value.itemInstanceId ?? string.Empty,
                value.quantity);
        }

        OfferKey = offerKey ?? string.Empty;
        Day = result.day;
        RuleId = result.ruleId ?? string.Empty;
        ItemLabel = result.itemLabel ?? string.Empty;
        Quantity = result.quantity;
        Cost = result.cost;
        Purchased = result.purchased;
        Reason = result.reason ?? string.Empty;
        AttemptedItemId = result.attemptedItemId ?? string.Empty;
        AttemptedQuantity = result.attemptedQuantity;
        AttemptedCost = result.attemptedCost;
        CommittedItemId = result.committedItemId ?? string.Empty;
        CommittedQuantity = result.committedQuantity;
        CommittedCost = result.committedCost;
        ChildPurchaseResultKey = result.childPurchaseResultKey ?? string.Empty;
        physicalStacks = Array.AsReadOnly(copiedStacks);
    }

    public string OfferKey { get; }
    public int Day { get; }
    public string RuleId { get; }
    public string ItemLabel { get; }
    public int Quantity { get; }
    public int Cost { get; }
    public bool Purchased { get; }
    public string Reason { get; }
    public string AttemptedItemId { get; }
    public int AttemptedQuantity { get; }
    public int AttemptedCost { get; }
    public string CommittedItemId { get; }
    public int CommittedQuantity { get; }
    public int CommittedCost { get; }
    public string ChildPurchaseResultKey { get; }
    public IReadOnlyList<Stack> PhysicalStacks =>
        physicalStacks ?? Array.Empty<Stack>();

    public AutoProcurementResult CaptureResult()
    {
        var result = new AutoProcurementResult
        {
            day = Day,
            ruleId = RuleId,
            itemLabel = ItemLabel,
            quantity = Quantity,
            cost = Cost,
            purchased = Purchased,
            reason = Reason,
            attemptedItemId = AttemptedItemId,
            attemptedQuantity = AttemptedQuantity,
            attemptedCost = AttemptedCost,
            committedItemId = CommittedItemId,
            committedQuantity = CommittedQuantity,
            committedCost = CommittedCost,
            childPurchaseResultKey = ChildPurchaseResultKey,
            physicalStacks = new List<AutoProcurementPhysicalStackRef>(
                PhysicalStacks.Count)
        };
        for (int index = 0; index < PhysicalStacks.Count; index++)
        {
            Stack value = PhysicalStacks[index];
            result.physicalStacks.Add(new AutoProcurementPhysicalStackRef
            {
                stackId = value.StackId,
                itemInstanceId = value.ItemInstanceId,
                quantity = value.Quantity
            });
        }
        return result;
    }
}
