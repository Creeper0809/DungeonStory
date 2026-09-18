using System;
using System.Collections.Generic;
using UnityEngine;

public static class MemoryErasureSealItemRules
{
    public const string ItemId = "item:memory-erasure-seal";
    public const int UseQuantity = 1;
}

public enum MemoryErasureSealOrderStage
{
    None = 0,
    Reserved = 1,
    MovingToWarehouse = 2,
    Carrying = 3,
    SuspendedAfterPickup = 4,
    Applying = 5,
    RecoveryPending = 6
}

public enum MemoryErasureSealCommandStatus
{
    Accepted = 1,
    AlreadyCompleted = 2,
    InvalidItem = 3,
    InvalidTarget = 4,
    TraitUnavailable = 5,
    OrderAlreadyActive = 6,
    CharacterUnavailable = 7,
    WarehouseSealUnavailable = 8,
    ActionOwnershipUnavailable = 9
}

public enum MemoryErasureSealUseStatus
{
    Succeeded = 1,
    AlreadyCompleted = 2,
    InterruptedBeforePickup = 3,
    InterruptedAfterPickup = 4,
    TargetChanged = 5,
    PhysicalCommitFailed = 6,
    TraitCommitFailed = 7,
    RecoveryFailed = 8,
    InvalidRequest = 9
}

public sealed class MemoryErasureSealTraitTarget
{
    public MemoryErasureSealTraitTarget(
        string instanceId,
        string displayName,
        string description,
        IReadOnlyList<string> moduleIds)
    {
        InstanceId = instanceId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Description = description ?? string.Empty;
        ModuleIds = moduleIds ?? Array.Empty<string>();
    }

    public string InstanceId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public IReadOnlyList<string> ModuleIds { get; }
}

public readonly struct MemoryErasureSealCommandResult
{
    public MemoryErasureSealCommandResult(
        MemoryErasureSealCommandStatus status,
        string operationId,
        string targetCharacterId,
        string traitInstanceId,
        string detail)
    {
        Status = status;
        OperationId = operationId ?? string.Empty;
        TargetCharacterId = targetCharacterId ?? string.Empty;
        TraitInstanceId = traitInstanceId ?? string.Empty;
        Detail = detail ?? string.Empty;
    }

    public MemoryErasureSealCommandStatus Status { get; }
    public string OperationId { get; }
    public string TargetCharacterId { get; }
    public string TraitInstanceId { get; }
    public string Detail { get; }
    public bool Accepted => Status == MemoryErasureSealCommandStatus.Accepted;
}

public readonly struct MemoryErasureSealOrderSnapshot
{
    public MemoryErasureSealOrderSnapshot(
        string operationId,
        string targetCharacterId,
        string traitInstanceId,
        MemoryErasureSealOrderStage stage,
        string sourceStackId,
        string detail)
    {
        OperationId = operationId ?? string.Empty;
        TargetCharacterId = targetCharacterId ?? string.Empty;
        TraitInstanceId = traitInstanceId ?? string.Empty;
        Stage = stage;
        SourceStackId = sourceStackId ?? string.Empty;
        Detail = detail ?? string.Empty;
    }

    public string OperationId { get; }
    public string TargetCharacterId { get; }
    public string TraitInstanceId { get; }
    public MemoryErasureSealOrderStage Stage { get; }
    public string SourceStackId { get; }
    public string Detail { get; }
    public bool IsActive => Stage != MemoryErasureSealOrderStage.None;
}

public readonly struct MemoryErasureSealUseResult
{
    public MemoryErasureSealUseResult(
        MemoryErasureSealUseStatus status,
        string operationId,
        string auditId,
        string targetCharacterId,
        string traitInstanceId,
        string physicalCommitId,
        int previousRevision,
        int committedRevision,
        long absoluteHour,
        string detail)
    {
        Status = status;
        OperationId = operationId ?? string.Empty;
        AuditId = auditId ?? string.Empty;
        TargetCharacterId = targetCharacterId ?? string.Empty;
        TraitInstanceId = traitInstanceId ?? string.Empty;
        PhysicalCommitId = physicalCommitId ?? string.Empty;
        PreviousRevision = previousRevision;
        CommittedRevision = committedRevision;
        AbsoluteHour = absoluteHour;
        Detail = detail ?? string.Empty;
    }

    public MemoryErasureSealUseStatus Status { get; }
    public string OperationId { get; }
    public string AuditId { get; }
    public string TargetCharacterId { get; }
    public string TraitInstanceId { get; }
    public string PhysicalCommitId { get; }
    public int PreviousRevision { get; }
    public int CommittedRevision { get; }
    public long AbsoluteHour { get; }
    public string Detail { get; }
    public bool Succeeded => Status is MemoryErasureSealUseStatus.Succeeded
        or MemoryErasureSealUseStatus.AlreadyCompleted;
}

public readonly struct MemoryErasureSealUseCompletedEvent
{
    public MemoryErasureSealUseCompletedEvent(MemoryErasureSealUseResult result)
    {
        Result = result;
    }

    public MemoryErasureSealUseResult Result { get; }
}

public interface IMemoryErasureSealStoredPickupRuntime
{
    bool TryReserveStoredMemoryErasureSeal(
        CharacterActor actor,
        string ownerOperationId,
        out WorldItemReservedStackQuantity reservation,
        out Vector2Int pickupStandPosition,
        out string failureReason);

    bool TryPickupStoredMemoryErasureSeal(
        CharacterActor actor,
        CharacterCarryInventory carry,
        WorldItemReservedStackQuantity reservation,
        out int pickedUp,
        out string failureReason);

    bool TryFinalizeStoredMemoryErasureSealPickup(
        string ownerOperationId,
        string sourceStackId,
        ItemReservationReleaseReason releaseReason,
        out string failureReason);

    bool TryReturnCarriedMemoryErasureSealToStoredSource(
        CharacterActor actor,
        CharacterCarryInventory carry,
        string ownerOperationId,
        out string failureReason);
}

public interface IMemoryErasureSealTransactionService
{
    MemoryErasureSealUseResult TryErase(
        CharacterActor target,
        string traitInstanceId,
        CharacterCarryInventory carry,
        string carriedStackId,
        string operationId);
}

public interface IMemoryErasureSealCommandService
{
    event Action<MemoryErasureSealOrderSnapshot> OrderChanged;
    event Action<MemoryErasureSealUseResult> UseCompleted;

    IReadOnlyList<MemoryErasureSealTraitTarget> GetActiveTraitTargets(
        CharacterActor target);

    MemoryErasureSealCommandResult TryIssueConfirmedUse(
        string itemId,
        CharacterActor target,
        string traitInstanceId);

    bool TryGetActiveOrder(
        CharacterActor target,
        out MemoryErasureSealOrderSnapshot order);

    bool TryGetLatestResult(
        CharacterActor target,
        out MemoryErasureSealUseResult result);
}
