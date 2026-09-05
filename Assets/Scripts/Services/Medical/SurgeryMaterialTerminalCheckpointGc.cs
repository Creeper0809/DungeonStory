using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum SurgeryMaterialTerminalCheckpointGcStatus
{
    Applied = 1,
    AlreadyApplied = 2,
    Deferred = 3,
    Corruption = 4
}

public readonly struct SurgeryMaterialTerminalCheckpointGcResult
{
    public SurgeryMaterialTerminalCheckpointGcResult(
        SurgeryMaterialTerminalCheckpointGcStatus status,
        string message)
    {
        Status = status;
        Message = message ?? string.Empty;
    }

    public SurgeryMaterialTerminalCheckpointGcStatus Status { get; }
    public string Message { get; }
}

public interface ISurgeryMaterialTerminalCheckpointGcCoordinator
{
    SurgeryMaterialTerminalCheckpointGcResult OnDurableSaveCommitted(
        string slotId,
        string serializedByteDigest);
}

internal interface ISurgeryMaterialTerminalCheckpointGcCandidate
{
    IReadOnlyList<SurgeryOrder> ClosedOrders { get; }
}

internal interface ISurgeryMaterialTerminalCheckpointGcAuthority
{
    bool TryPrepare(
        out ISurgeryMaterialTerminalCheckpointGcCandidate candidate,
        out string failureReason);

    bool TryPublish(
        ISurgeryMaterialTerminalCheckpointGcCandidate candidate,
        out string failureReason);

    void Rollback(ISurgeryMaterialTerminalCheckpointGcCandidate candidate);
    void Complete(ISurgeryMaterialTerminalCheckpointGcCandidate candidate);
}

/// <summary>
/// Transactional Medical-side collector. Completed Surgery history remains;
/// only the already-durable terminal join identity is cleared.
/// </summary>
internal sealed class SurgeryMaterialTerminalCheckpointGcAuthority :
    ISurgeryMaterialTerminalCheckpointGcAuthority
{
    private readonly SurgeryAggregateStateStore stateStore;
    private Candidate active;

    public SurgeryMaterialTerminalCheckpointGcAuthority(
        SurgeryAggregateStateStore stateStore)
    {
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public bool TryPrepare(
        out ISurgeryMaterialTerminalCheckpointGcCandidate candidate,
        out string failureReason)
    {
        candidate = null;
        failureReason = string.Empty;
        if (active != null)
        {
            failureReason =
                "surgery-material-terminal-checkpoint-gc-already-active";
            return false;
        }

        SurgeryOrder[] closed = stateStore.State.Orders
            .Where(value => value != null
                && value.materialTerminalDrainPhase ==
                    SurgeryMaterialTerminalDrainPhase
                        .ClosedAwaitingCheckpointGc)
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .Select(SurgeryStateCloner.CloneOrder)
            .ToArray();
        if (closed.Any(value => value.state != value.materialTerminalTargetState
                || value.state is not (SurgeryOrderState.Completed
                    or SurgeryOrderState.Failed
                    or SurgeryOrderState.Cancelled)))
        {
            failureReason =
                "surgery-material-terminal-checkpoint-gc-upper-invalid";
            return false;
        }

        active = new Candidate(closed);
        candidate = active;
        return true;
    }

    public bool TryPublish(
        ISurgeryMaterialTerminalCheckpointGcCandidate candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryRequire(candidate, out Candidate exact, out failureReason))
            return false;
        if (exact.Published)
            return true;

        Dictionary<string, SurgeryOrder> liveById = stateStore.State.Orders
            .Where(value => value != null)
            .ToDictionary(value => value.orderId, StringComparer.Ordinal);
        foreach (SurgeryOrder expected in exact.ClosedOrders)
        {
            if (!liveById.TryGetValue(expected.orderId, out SurgeryOrder live)
                || !OrderExact(live, expected))
            {
                failureReason =
                    "surgery-material-terminal-checkpoint-gc-upper-drift:"
                    + expected.orderId;
                return false;
            }
        }

        foreach (SurgeryOrder expected in exact.ClosedOrders)
            ClearTerminalJoin(liveById[expected.orderId]);
        exact.Published = true;
        return true;
    }

    public void Rollback(ISurgeryMaterialTerminalCheckpointGcCandidate candidate)
    {
        if (!TryRequire(candidate, out Candidate exact, out string failureReason))
            throw new InvalidOperationException(failureReason);
        if (!exact.Published)
        {
            exact.Completed = true;
            active = null;
            return;
        }

        Dictionary<string, SurgeryOrder> liveById = stateStore.State.Orders
            .Where(value => value != null)
            .ToDictionary(value => value.orderId, StringComparer.Ordinal);
        foreach (SurgeryOrder original in exact.ClosedOrders)
        {
            if (!liveById.TryGetValue(original.orderId, out SurgeryOrder live)
                || !OrderExact(live, ClearedClone(original)))
            {
                throw new InvalidOperationException(
                    "surgery-material-terminal-checkpoint-gc-rollback-drift:"
                    + original.orderId);
            }
        }
        foreach (SurgeryOrder original in exact.ClosedOrders)
            RestoreTerminalJoin(liveById[original.orderId], original);
        exact.Published = false;
        exact.Completed = true;
        active = null;
    }

    public void Complete(ISurgeryMaterialTerminalCheckpointGcCandidate candidate)
    {
        if (!TryRequire(candidate, out Candidate exact, out string failureReason))
            throw new InvalidOperationException(failureReason);
        if (!exact.Published)
        {
            throw new InvalidOperationException(
                "surgery-material-terminal-checkpoint-gc-not-published");
        }
        exact.Completed = true;
        active = null;
    }

    private bool TryRequire(
        ISurgeryMaterialTerminalCheckpointGcCandidate candidate,
        out Candidate exact,
        out string failureReason)
    {
        exact = candidate as Candidate;
        if (exact == null || !ReferenceEquals(exact, active) || exact.Completed)
        {
            failureReason =
                "surgery-material-terminal-checkpoint-gc-candidate-invalid";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private static bool OrderExact(SurgeryOrder left, SurgeryOrder right) =>
        left != null
        && right != null
        && string.Equals(
            JsonUtility.ToJson(left),
            JsonUtility.ToJson(right),
            StringComparison.Ordinal);

    private static SurgeryOrder ClearedClone(SurgeryOrder source)
    {
        SurgeryOrder clone = SurgeryStateCloner.CloneOrder(source);
        ClearTerminalJoin(clone);
        return clone;
    }

    internal static void ClearTerminalJoin(SurgeryOrder order)
    {
        order.materialTerminalDrainPhase = SurgeryMaterialTerminalDrainPhase.None;
        order.materialTerminalTargetState = SurgeryOrderState.PatientWaiting;
        order.materialTerminalParentOperationId = string.Empty;
        order.materialTerminalStepOperationId = string.Empty;
        order.materialTerminalRequestFingerprint = string.Empty;
        order.materialTerminalCommitId = string.Empty;
        order.materialTerminalReceiptFingerprint = string.Empty;
        order.materialTerminalInputQuantity = 0;
        order.materialTerminalInputMassGrams = 0L;
        order.materialTerminalOwnerX = 0;
        order.materialTerminalOwnerY = 0;
    }

    private static void RestoreTerminalJoin(
        SurgeryOrder target,
        SurgeryOrder source)
    {
        target.materialTerminalDrainPhase = source.materialTerminalDrainPhase;
        target.materialTerminalTargetState = source.materialTerminalTargetState;
        target.materialTerminalParentOperationId =
            source.materialTerminalParentOperationId;
        target.materialTerminalStepOperationId =
            source.materialTerminalStepOperationId;
        target.materialTerminalRequestFingerprint =
            source.materialTerminalRequestFingerprint;
        target.materialTerminalCommitId = source.materialTerminalCommitId;
        target.materialTerminalReceiptFingerprint =
            source.materialTerminalReceiptFingerprint;
        target.materialTerminalInputQuantity =
            source.materialTerminalInputQuantity;
        target.materialTerminalInputMassGrams =
            source.materialTerminalInputMassGrams;
        target.materialTerminalOwnerX = source.materialTerminalOwnerX;
        target.materialTerminalOwnerY = source.materialTerminalOwnerY;
    }

    private sealed class Candidate :
        ISurgeryMaterialTerminalCheckpointGcCandidate
    {
        internal Candidate(IReadOnlyList<SurgeryOrder> closedOrders)
        {
            ClosedOrders = closedOrders
                ?? throw new ArgumentNullException(nameof(closedOrders));
        }

        public IReadOnlyList<SurgeryOrder> ClosedOrders { get; }
        internal bool Published { get; set; }
        internal bool Completed { get; set; }
    }
}

public sealed class SurgeryMaterialTerminalCheckpointGcCoordinator :
    ISurgeryMaterialTerminalCheckpointGcCoordinator
{
    private readonly ISurgeryMaterialTerminalCheckpointGcAuthority upper;
    private readonly IFacilityBufferDestinationCustodyDrainLiveQuery childQuery;
    private readonly IFacilityBufferDestinationCustodyDrainCheckpointGcPort
        childGc;
    private readonly SurgeryAggregateStateStore stateStore;

    [VContainer.Inject]
    public SurgeryMaterialTerminalCheckpointGcCoordinator(
        SurgeryAggregateStateStore stateStore,
        IFacilityBufferDestinationCustodyDrainLiveQuery childQuery,
        IFacilityBufferDestinationCustodyDrainCheckpointGcPort childGc)
        : this(
            stateStore,
            new SurgeryMaterialTerminalCheckpointGcAuthority(stateStore),
            childQuery,
            childGc)
    {
    }

    internal SurgeryMaterialTerminalCheckpointGcCoordinator(
        SurgeryAggregateStateStore stateStore,
        ISurgeryMaterialTerminalCheckpointGcAuthority upper,
        IFacilityBufferDestinationCustodyDrainLiveQuery childQuery,
        IFacilityBufferDestinationCustodyDrainCheckpointGcPort childGc)
    {
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.upper = upper ?? throw new ArgumentNullException(nameof(upper));
        this.childQuery = childQuery
            ?? throw new ArgumentNullException(nameof(childQuery));
        this.childGc = childGc ?? throw new ArgumentNullException(nameof(childGc));
    }

    public SurgeryMaterialTerminalCheckpointGcResult OnDurableSaveCommitted(
        string slotId,
        string serializedByteDigest)
    {
        if (string.IsNullOrEmpty(slotId)
            || serializedByteDigest == null
            || serializedByteDigest.Length != 64)
        {
            return Corruption(
                "surgery-material-terminal-checkpoint-gc-context-invalid");
        }

        if (!upper.TryPrepare(
                out ISurgeryMaterialTerminalCheckpointGcCandidate upperCandidate,
                out string failureReason))
        {
            return Deferred(failureReason);
        }

        IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate
            childCandidate = null;
        bool childPrepared = false;
        bool childPublished = false;
        try
        {
            FacilityBufferDestinationCustodyDrainSnapshot[] allChildren =
                childQuery.Drains?.ToArray()
                ?? Array.Empty<FacilityBufferDestinationCustodyDrainSnapshot>();
            SurgeryMaterialTerminalCrossAggregateJoin.Validate(
                stateStore.State.Orders,
                allChildren);

            Dictionary<string, FacilityBufferDestinationCustodyDrainSnapshot>
                byStep = allChildren.ToDictionary(
                    value => value.StepOperationId,
                    StringComparer.Ordinal);
            FacilityBufferDestinationCustodyDrainSnapshot[] closedChildren =
                upperCandidate.ClosedOrders.Select(order =>
                {
                    string step = SurgeryMaterialTerminalIdentity
                        .FormatStepOperationId(order.orderId);
                    if (!byStep.TryGetValue(step, out
                            FacilityBufferDestinationCustodyDrainSnapshot child)
                        || child.Phase !=
                            FacilityBufferDestinationCustodyDrainPhase
                                .OwnerAcknowledgedAwaitingCheckpointGc)
                    {
                        throw new InvalidOperationException(
                            "surgery-material-terminal-checkpoint-gc-child-invalid:"
                            + step);
                    }
                    return child;
                }).ToArray();

            if (closedChildren.Length == 0)
            {
                if (!upper.TryPublish(upperCandidate, out failureReason))
                    return Corruption(failureReason);
                upper.Complete(upperCandidate);
                return new SurgeryMaterialTerminalCheckpointGcResult(
                    SurgeryMaterialTerminalCheckpointGcStatus.AlreadyApplied,
                    "No Surgery material terminal receipts require collection.");
            }

            if (!childGc.TryPrepareCheckpointGarbageCollection(
                    closedChildren,
                    out childCandidate,
                    out failureReason))
            {
                upper.Rollback(upperCandidate);
                return Deferred(failureReason);
            }
            childPrepared = true;
            if (!childGc.TryPublishCheckpointGarbageCollection(
                    childCandidate,
                    out failureReason))
            {
                childGc.RollbackCheckpointGarbageCollection(childCandidate);
                childGc.CompleteCheckpointGarbageCollection(childCandidate);
                upper.Rollback(upperCandidate);
                return Corruption(failureReason);
            }
            childPublished = true;
            if (!upper.TryPublish(upperCandidate, out failureReason))
            {
                childGc.RollbackCheckpointGarbageCollection(childCandidate);
                childGc.CompleteCheckpointGarbageCollection(childCandidate);
                childPublished = false;
                upper.Rollback(upperCandidate);
                return Corruption(failureReason);
            }

            upper.Complete(upperCandidate);
            childGc.CompleteCheckpointGarbageCollection(childCandidate);
            return new SurgeryMaterialTerminalCheckpointGcResult(
                SurgeryMaterialTerminalCheckpointGcStatus.Applied,
                $"Collected {closedChildren.Length} Surgery material terminal receipt(s)." );
        }
        catch (Exception exception)
        {
            try
            {
                if (childPrepared && childCandidate != null)
                {
                    if (childPublished)
                    {
                        childGc.RollbackCheckpointGarbageCollection(
                            childCandidate);
                    }
                    childGc.CompleteCheckpointGarbageCollection(childCandidate);
                }
                upper.Rollback(upperCandidate);
            }
            catch (Exception rollbackException)
            {
                return Corruption(
                    exception.Message + "; rollback=" + rollbackException.Message);
            }
            return Corruption(exception.Message);
        }
    }

    private static SurgeryMaterialTerminalCheckpointGcResult Deferred(
        string message) => new(
        SurgeryMaterialTerminalCheckpointGcStatus.Deferred,
        message);

    private static SurgeryMaterialTerminalCheckpointGcResult Corruption(
        string message) => new(
        SurgeryMaterialTerminalCheckpointGcStatus.Corruption,
        message);
}
