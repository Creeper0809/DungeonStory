using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public readonly struct FactionContractDeliveryTarget
{
    public FactionContractDeliveryTarget(
        string destinationId,
        Vector2Int position)
    {
        DestinationId = destinationId?.Trim() ?? string.Empty;
        Position = position;
    }

    public string DestinationId { get; }
    public Vector2Int Position { get; }
    public bool IsValid => EconomyProjectInputOwnerAuthority.IsCanonical(
        DestinationId);
}

public readonly struct FactionContractDeliveryReceipt
{
    public FactionContractDeliveryReceipt(
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        string requestFingerprint,
        IReadOnlyList<string> sourceStackIds,
        int quantity,
        long inputMassGrams)
    {
        Kind = kind;
        OperationId = operationId ?? string.Empty;
        ReasonCode = reasonCode ?? string.Empty;
        RequestFingerprint = requestFingerprint ?? string.Empty;
        SourceStackIds = (sourceStackIds ?? Array.Empty<string>()).ToArray();
        Quantity = quantity;
        InputMassGrams = inputMassGrams;
        CommitId = $"physical-batch-disposition:{(int)kind}:{OperationId}:"
            + $"{quantity}:{inputMassGrams}";
    }

    public static FactionContractDeliveryReceipt FromPhysical(
        PhysicalItemBatchDispositionReceipt receipt) =>
        new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            receipt.RequestFingerprint,
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams);

    public PhysicalItemDispositionKind Kind { get; }
    public string OperationId { get; }
    public string ReasonCode { get; }
    public string RequestFingerprint { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public int Quantity { get; }
    public long InputMassGrams { get; }
    public string CommitId { get; }
    public bool IsCommitted => Kind == PhysicalItemDispositionKind.Transfer
        && (OperationId?.Length ?? 0) > 0
        && (ReasonCode?.Length ?? 0) > 0
        && (SourceStackIds?.Count ?? 0) > 0
        && Quantity > 0
        && InputMassGrams > 0L;
}

public sealed class FactionContractItemProgressView
{
    public FactionContractItemProgressView(
        string itemId,
        string displayName,
        int required,
        int assigned,
        int hauling,
        int arrived,
        int delivered)
    {
        ItemId = itemId ?? string.Empty;
        DisplayName = displayName ?? ItemId;
        Required = Mathf.Max(0, required);
        Assigned = Mathf.Max(0, assigned);
        Hauling = Mathf.Max(0, hauling);
        Arrived = Mathf.Max(0, arrived);
        Delivered = Mathf.Max(0, delivered);
    }

    public string ItemId { get; }
    public string DisplayName { get; }
    public int Required { get; }
    public int Assigned { get; }
    public int Hauling { get; }
    public int Arrived { get; }
    public int Delivered { get; }
}

public sealed class FactionContractView
{
    public string ContractId { get; internal set; } = string.Empty;
    public string OccurrenceId { get; internal set; } = string.Empty;
    public string AcceptActionId { get; internal set; } = string.Empty;
    public string DisplayName { get; internal set; } = string.Empty;
    public string Description { get; internal set; } = string.Empty;
    public V20FactionContractKind Kind { get; internal set; }
    public int DeadlineDays { get; internal set; }
    public int DeadlineAbsoluteDay { get; internal set; }
    public bool IsActive { get; internal set; }
    public bool IsCompleted { get; internal set; }
    public bool IsFailed { get; internal set; }
    public bool CanAccept { get; internal set; }
    public string DisabledReason { get; internal set; } = string.Empty;
    public string EligibilityReason { get; internal set; } = string.Empty;
    public string StatusReason { get; internal set; } = string.Empty;
    public IReadOnlyList<string> CompletionConditions { get; internal set; } =
        Array.Empty<string>();
    public IReadOnlyList<string> SuccessEffects { get; internal set; } =
        Array.Empty<string>();
    public IReadOnlyList<string> FailureEffects { get; internal set; } =
        Array.Empty<string>();
    public IReadOnlyList<FactionContractItemProgressView> Items { get; internal set; } =
        Array.Empty<FactionContractItemProgressView>();
}

public interface IFactionContractDeliveryQuery
{
    IReadOnlyList<FactionContractView> GetContracts(string factionId);
}

public interface IFactionContractDeliveryResolutionCommand
{
    bool RequiresAdvance { get; }

    void AdvanceFactionContractDeliveries(
        int absoluteDay,
        RunMilestoneEvaluationSnapshot requirements);
}

public interface IFactionCampaignDeliveryCommand
{
    bool TryAcceptContract(
        string factionId,
        string contractId,
        int absoluteDay,
        FactionContractDeliveryTarget target,
        out string failure);
    bool TryAcceptSeasonalContract(
        string occurrenceId,
        string contractId,
        FactionContractDeliveryTarget target,
        out string failure);
    bool TrySetInputOwnerProjection(
        string factionId,
        long capacityGrams,
        long massAuthorityRevision,
        string capacityFingerprint,
        out string failure);
    bool TryRecordDeliveryReceipt(
        string factionId,
        FactionContractDeliveryReceipt receipt,
        out string failure);
    bool TryResolveDeliveredContract(
        string factionId,
        RunMilestoneEvaluationSnapshot requirements,
        out V20ResolvedEventResult result,
        out string failure);
    bool TrySetDeliveryFailure(
        string factionId,
        string failureReason,
        out string failure);
    bool TryMarkInputOwnerRetired(
        string factionId,
        out string failure);
    bool TryClearDeliveryOutbox(
        string factionId,
        out string failure);
}
