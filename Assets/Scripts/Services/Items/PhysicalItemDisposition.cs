using System;
using DungeonStory.Foundation;

public readonly struct PhysicalItemDispositionReceipt
{
    internal PhysicalItemDispositionReceipt(
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        WorldItemStackSnapshot consumed,
        long inputMassGrams)
    {
        Kind = kind;
        OperationId = operationId;
        ReasonCode = reasonCode;
        StackId = consumed?.StackId ?? string.Empty;
        ItemId = consumed?.ItemId ?? string.Empty;
        ItemInstanceId = consumed?.ItemInstanceId ?? string.Empty;
        Quantity = consumed?.Quantity ?? 0;
        InputMassGrams = inputMassGrams;
        Consumed = consumed;
        CommitId = $"physical-disposition:{(int)kind}:{operationId}:{StackId}:{Quantity}";
    }

    public PhysicalItemDispositionKind Kind { get; }
    public string OperationId { get; }
    public string ReasonCode { get; }
    public string StackId { get; }
    public string ItemId { get; }
    public string ItemInstanceId { get; }
    public int Quantity { get; }
    public long InputMassGrams { get; }
    public WorldItemStackSnapshot Consumed { get; }
    public string CommitId { get; }
    public bool IsCommitted => Kind is PhysicalItemDispositionKind.Transfer
            or PhysicalItemDispositionKind.Sink
        && OperationId?.Length > 0
        && ReasonCode?.Length > 0
        && StackId?.Length > 0
        && ItemId?.Length > 0
        && Quantity > 0
        && InputMassGrams > 0L
        && CommitId?.Length > 0;
}

/// <summary>
/// Typed terminal-removal boundary for physical lots. Source creation and
/// Transform are intentionally rejected here: creation belongs to a source
/// command and mass-changing recipes belong to IPhysicalItemTransformService.
/// </summary>
public static class PhysicalItemDispositionExtensions
{
    public static bool TryCommitPhysicalDisposition(
        this IWorldItemStackRuntime items,
        string stackId,
        int quantity,
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        out PhysicalItemDispositionReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        string canonicalStackId = stackId ?? string.Empty;
        string canonicalOperation = operationId ?? string.Empty;
        string canonicalReason = reasonCode ?? string.Empty;
        if (items == null
            || quantity <= 0
            || kind is not (PhysicalItemDispositionKind.Transfer
                or PhysicalItemDispositionKind.Sink)
            || canonicalStackId.Length == 0
            || canonicalOperation.Length == 0
            || canonicalReason.Length == 0
            || !IsCanonical(canonicalStackId)
            || !IsCanonical(canonicalOperation)
            || !IsCanonical(canonicalReason))
        {
            failureReason = "physical-disposition-invalid-request";
            return false;
        }

        WorldItemStackSnapshot source = null;
        foreach (WorldItemStackSnapshot candidate in items.GetAllStacks())
        {
            if (candidate != null
                && string.Equals(
                    candidate.StackId,
                    canonicalStackId,
                    StringComparison.Ordinal))
            {
                source = candidate;
                break;
            }
        }
        if (source == null || source.AvailableQuantity < quantity)
        {
            failureReason = "physical-disposition-source-unavailable";
            return false;
        }

        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            items.MassQuery,
            (ItemDefinitionId)source.ItemId,
            source.ItemInstanceId,
            source.Components);
        long inputMassGrams = items.MassQuery.GetQuantityMass(
            (ItemDefinitionId)source.ItemId,
            subject,
            quantity).Value;
        if (!items.TryConsumeStackQuantity(
                canonicalStackId,
                quantity,
                out WorldItemStackSnapshot consumed)
            || consumed == null
            || consumed.Quantity != quantity
            || !string.Equals(
                consumed.ItemId,
                source.ItemId,
                StringComparison.Ordinal)
            || !string.Equals(
                consumed.ItemInstanceId,
                source.ItemInstanceId,
                StringComparison.Ordinal))
        {
            failureReason = "physical-disposition-commit-failed";
            return false;
        }

        receipt = new PhysicalItemDispositionReceipt(
            kind,
            canonicalOperation,
            canonicalReason,
            consumed,
            inputMassGrams);
        if (!receipt.IsCommitted)
        {
            throw new InvalidOperationException(
                $"Physical disposition '{canonicalOperation}' committed an invalid receipt.");
        }
        return true;
    }

    private static bool IsCanonical(string value) =>
        string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
