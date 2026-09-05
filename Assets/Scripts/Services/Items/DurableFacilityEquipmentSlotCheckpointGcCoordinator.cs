using System;
using System.Collections.Generic;
using System.Linq;

public sealed class DurableFacilityEquipmentSlotCheckpointGcCoordinator :
    IDurableFacilityEquipmentSlotCheckpointGcCoordinator
{
    private readonly IDurableFacilityEquipmentSlotPersistence persistence;
    private readonly IDurableFacilityEquipmentSlotCheckpointGcPort upper;
    private readonly IFacilityBufferDestinationCustodyDrainLiveQuery childQuery;
    private readonly IFacilityBufferDestinationCustodyDrainCheckpointGcPort childGc;

    public DurableFacilityEquipmentSlotCheckpointGcCoordinator(
        IDurableFacilityEquipmentSlotPersistence persistence,
        IDurableFacilityEquipmentSlotCheckpointGcPort upper,
        IFacilityBufferDestinationCustodyDrainLiveQuery childQuery,
        IFacilityBufferDestinationCustodyDrainCheckpointGcPort childGc)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.upper = upper ?? throw new ArgumentNullException(nameof(upper));
        this.childQuery = childQuery
            ?? throw new ArgumentNullException(nameof(childQuery));
        this.childGc = childGc ?? throw new ArgumentNullException(nameof(childGc));
    }

    public DurableFacilityEquipmentSlotCheckpointGcResult OnDurableSaveCommitted(
        string slotId,
        string serializedByteDigest)
    {
        if (string.IsNullOrEmpty(slotId)
            || serializedByteDigest == null
            || serializedByteDigest.Length != 64)
        {
            return Corruption(
                "durable-equipment-slot-checkpoint-gc-context-invalid");
        }
        if (!upper.TryPrepareCheckpointGarbageCollection(
                out IDurableFacilityEquipmentSlotCheckpointGcCandidate
                    upperCandidate,
                out string failureReason))
        {
            return Deferred(failureReason);
        }

        IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate childCandidate =
            null;
        bool childPrepared = false;
        bool childPublished = false;
        bool upperPublished = false;
        try
        {
            FacilityBufferDestinationCustodyDrainSnapshot[] allChildren =
                childQuery.Drains?.ToArray()
                ?? Array.Empty<FacilityBufferDestinationCustodyDrainSnapshot>();
            DurableFacilityEquipmentCrossAggregateJoin.Validate(
                persistence.CaptureSaveData().slots,
                allChildren);
            Dictionary<string, FacilityBufferDestinationCustodyDrainSnapshot>
                childByStep = allChildren.ToDictionary(
                    value => value.StepOperationId,
                    StringComparer.Ordinal);
            FacilityBufferDestinationCustodyDrainSnapshot[] closedChildren =
                upperCandidate.ClosedSlots.Select(value =>
                {
                    FacilityBufferDestinationCustodyDrainSnapshot expected =
                        value.Drain;
                    if (expected == null
                        || !childByStep.TryGetValue(expected.StepOperationId,
                            out FacilityBufferDestinationCustodyDrainSnapshot child)
                        || child.Phase !=
                            FacilityBufferDestinationCustodyDrainPhase
                                .OwnerAcknowledgedAwaitingCheckpointGc
                        || !FacilityBufferDestinationCustodyDrainProjection
                            .AreExactEqual(expected, child))
                    {
                        throw new InvalidOperationException(
                            "durable-equipment-slot-checkpoint-gc-child-invalid:"
                            + (expected?.StepOperationId ?? string.Empty));
                    }
                    return child;
                }).ToArray();

            if (closedChildren.Length == 0)
            {
                if (!upper.TryPublishCheckpointGarbageCollection(
                        upperCandidate,
                        out failureReason))
                {
                    upper.RollbackCheckpointGarbageCollection(upperCandidate);
                    return Corruption(failureReason);
                }
                upperPublished = true;
                upper.CompleteCheckpointGarbageCollection(upperCandidate);
                return new DurableFacilityEquipmentSlotCheckpointGcResult(
                    DurableFacilityEquipmentSlotCheckpointGcStatus
                        .AlreadyApplied,
                    "No durable facility-equipment receipts require collection.");
            }

            if (!childGc.TryPrepareCheckpointGarbageCollection(
                    closedChildren,
                    out childCandidate,
                    out failureReason))
            {
                upper.RollbackCheckpointGarbageCollection(upperCandidate);
                return Deferred(failureReason);
            }
            childPrepared = true;
            if (!childGc.TryPublishCheckpointGarbageCollection(
                    childCandidate,
                    out failureReason))
            {
                childGc.RollbackCheckpointGarbageCollection(childCandidate);
                childGc.CompleteCheckpointGarbageCollection(childCandidate);
                upper.RollbackCheckpointGarbageCollection(upperCandidate);
                return Corruption(failureReason);
            }
            childPublished = true;
            if (!upper.TryPublishCheckpointGarbageCollection(
                    upperCandidate,
                    out failureReason))
            {
                childGc.RollbackCheckpointGarbageCollection(childCandidate);
                childGc.CompleteCheckpointGarbageCollection(childCandidate);
                childPublished = false;
                upper.RollbackCheckpointGarbageCollection(upperCandidate);
                return Corruption(failureReason);
            }
            upperPublished = true;

            upper.CompleteCheckpointGarbageCollection(upperCandidate);
            childGc.CompleteCheckpointGarbageCollection(childCandidate);
            return new DurableFacilityEquipmentSlotCheckpointGcResult(
                DurableFacilityEquipmentSlotCheckpointGcStatus.Applied,
                $"Collected {closedChildren.Length} durable facility-equipment receipt(s).");
        }
        catch (Exception exception)
        {
            try
            {
                bool upperHandled = false;
                if (upperPublished)
                {
                    upper.RollbackCheckpointGarbageCollection(upperCandidate);
                    upperHandled = true;
                }
                if (childPrepared && childCandidate != null)
                {
                    if (childPublished)
                    {
                        childGc.RollbackCheckpointGarbageCollection(
                            childCandidate);
                    }
                    childGc.CompleteCheckpointGarbageCollection(childCandidate);
                }
                if (!upperHandled)
                    upper.RollbackCheckpointGarbageCollection(upperCandidate);
            }
            catch (Exception rollbackException)
            {
                return Corruption(
                    exception.Message + "; rollback=" + rollbackException.Message);
            }
            return Corruption(exception.Message);
        }
    }

    private static DurableFacilityEquipmentSlotCheckpointGcResult Deferred(
        string message) => new(
        DurableFacilityEquipmentSlotCheckpointGcStatus.Deferred,
        message);

    private static DurableFacilityEquipmentSlotCheckpointGcResult Corruption(
        string message) => new(
        DurableFacilityEquipmentSlotCheckpointGcStatus.Corruption,
        message);
}

public sealed class DurableFacilityEquipmentSlotCheckpointGcDurableSaveParticipant :
    IDungeonDurableSaveCommitParticipant
{
    public const string Id =
        "320.durable-facility-equipment-slot-checkpoint-gc";
    private readonly IDurableFacilityEquipmentSlotCheckpointGcCoordinator
        coordinator;

    public DurableFacilityEquipmentSlotCheckpointGcDurableSaveParticipant(
        IDurableFacilityEquipmentSlotCheckpointGcCoordinator coordinator)
    {
        this.coordinator = coordinator
            ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public string ParticipantId => Id;
    public int Order => 320;

    public DungeonDurableSaveCommitResult OnDurableSaveCommitted(
        DungeonDurableSaveCommitContext context)
    {
        DurableFacilityEquipmentSlotCheckpointGcResult result = coordinator
            .OnDurableSaveCommitted(
                context.SlotId,
                context.SerializedByteDigest);
        DungeonDurableSaveCommitStatus status = result.Status switch
        {
            DurableFacilityEquipmentSlotCheckpointGcStatus.Applied =>
                DungeonDurableSaveCommitStatus.Applied,
            DurableFacilityEquipmentSlotCheckpointGcStatus.AlreadyApplied =>
                DungeonDurableSaveCommitStatus.AlreadyApplied,
            DurableFacilityEquipmentSlotCheckpointGcStatus.Deferred =>
                DungeonDurableSaveCommitStatus.Deferred,
            DurableFacilityEquipmentSlotCheckpointGcStatus.Corruption =>
                DungeonDurableSaveCommitStatus.Corruption,
            _ => DungeonDurableSaveCommitStatus.Corruption
        };
        return new DungeonDurableSaveCommitResult(status, Id, result.Message);
    }
}
