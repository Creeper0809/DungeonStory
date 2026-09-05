using System;
using System.Collections.Generic;

public enum SurgeryMaterialTerminalAdvanceStatus
{
    None = 0,
    ReadyForOwnerClosure = 1,
    Deferred = 2,
    Conflict = 3
}

public readonly struct SurgeryMaterialTerminalAdvanceResult
{
    public SurgeryMaterialTerminalAdvanceResult(
        SurgeryMaterialTerminalAdvanceStatus status,
        string failureReason)
    {
        Status = status;
        FailureReason = failureReason ?? string.Empty;
    }

    public SurgeryMaterialTerminalAdvanceStatus Status { get; }
    public string FailureReason { get; }
    public bool IsReadyForOwnerClosure => Status ==
        SurgeryMaterialTerminalAdvanceStatus.ReadyForOwnerClosure;
}

public interface ISurgeryMaterialTerminalRuntime
{
    SurgeryMaterialTerminalAdvanceResult TryBeginOrResume(
        SurgeryOrder order,
        SurgeryOrderState terminalTarget);
}

/// <summary>
/// Medical owner adapter for the Items-owned destination-custody drain. The
/// Surgery aggregate stores only the exact child join and terminal target; all
/// stack, lease, intent and carried-cargo progress remains in Items.
/// </summary>
public sealed class SurgeryMaterialTerminalRuntime :
    ISurgeryMaterialTerminalRuntime
{
    private readonly IFacilityBufferDestinationCustodyDrainService drains;
    private readonly IFacilityBufferDestinationClaimQuery claims;
    private readonly ISurgeryMaterialDestinationRuntime materialDestinations;

    public SurgeryMaterialTerminalRuntime(
        IFacilityBufferDestinationCustodyDrainService drains,
        IFacilityBufferDestinationClaimQuery claims,
        ISurgeryMaterialDestinationRuntime materialDestinations)
    {
        this.drains = drains ?? throw new ArgumentNullException(nameof(drains));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.materialDestinations = materialDestinations
            ?? throw new ArgumentNullException(nameof(materialDestinations));
        if (!this.drains.RequiresImmediateRecoveryBeforeGameplayTick)
        {
            throw new ArgumentException(
                "Surgery terminal custody requires immediate pre-gameplay recovery.",
                nameof(drains));
        }
    }

    [GameplayInternalOnly(
        "Begins or resumes the exact Surgery material destination custody drain before closing its owner order.",
        "SurgeryRuntime terminal lifecycle only")]
    public SurgeryMaterialTerminalAdvanceResult TryBeginOrResume(
        SurgeryOrder order,
        SurgeryOrderState terminalTarget)
    {
        if (order == null || !IsTerminalTarget(terminalTarget))
        {
            return Conflict("surgery-material-terminal-order-or-target-invalid");
        }

        FacilityBufferDestinationCustodyDrainSnapshot child;
        if (order.materialTerminalDrainPhase ==
            SurgeryMaterialTerminalDrainPhase.None)
        {
            FacilityBufferDestinationClaim claim;
            try
            {
                if (!SurgeryMaterialDestinationAuthority.TryGetOwnedClaim(
                        claims,
                        order,
                        out claim))
                {
                    return Conflict(
                        "surgery-material-terminal-claim-missing:"
                        + order.orderId);
                }
            }
            catch (InvalidOperationException)
            {
                return Conflict(
                    "surgery-material-terminal-claim-cardinality-invalid:"
                    + order.orderId);
            }

            FacilityBufferDestinationCustodyDrainDescriptor descriptor = new(
                SurgeryMaterialTerminalIdentity.FormatParentOperationId(
                    order.orderId),
                SurgeryMaterialTerminalIdentity.FormatStepOperationId(
                    order.orderId),
                SurgeryMaterialTerminalIdentity.FormatOwnerStableId(
                    order.orderId),
                order.orderId,
                order.facilityId,
                order.materialDestinationId,
                claim.DropPosition,
                order.materialCapacityFingerprint);
            FacilityBufferDestinationCustodyDrainResult prepared =
                drains.TryPrepare(descriptor);
            if (!prepared.Succeeded || prepared.Snapshot == null)
            {
                return FromChildFailure(prepared);
            }

            child = prepared.Snapshot;
            PersistPreparedJoin(order, terminalTarget, child);
        }
        else
        {
            if (order.materialTerminalTargetState != terminalTarget)
            {
                return Conflict(
                    "surgery-material-terminal-target-conflict:"
                    + order.orderId);
            }
            if (!drains.TryCapture(
                    order.materialTerminalStepOperationId,
                    out child))
            {
                return Conflict(
                    "surgery-material-terminal-child-missing:"
                    + order.orderId);
            }
        }

        HashSet<string> observedProgress = new(StringComparer.Ordinal);
        while (true)
        {
            if (child.EffectCommitted
                && order.materialTerminalDrainPhase ==
                    SurgeryMaterialTerminalDrainPhase.Prepared)
            {
                order.materialTerminalCommitId = child.CommitId;
                order.materialTerminalReceiptFingerprint =
                    child.ReceiptFingerprint;
                order.materialTerminalDrainPhase =
                    SurgeryMaterialTerminalDrainPhase
                        .EffectCommittedAwaitingAck;
            }
            if (!SurgeryMaterialTerminalJoin.TryValidate(
                    order,
                    child,
                    out string joinFailure))
            {
                return Conflict(joinFailure);
            }

            if (child.Phase < FacilityBufferDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingOwnerAck)
            {
                string cursor = CreateProgressCursor(child);
                if (!observedProgress.Add(cursor))
                {
                    return Conflict(
                        "surgery-material-terminal-child-made-no-progress:"
                        + order.orderId);
                }
                FacilityBufferDestinationCustodyDrainResult advanced =
                    drains.TryAdvance(
                        order.materialTerminalStepOperationId,
                        order.materialTerminalRequestFingerprint);
                if (!advanced.Succeeded || advanced.Snapshot == null)
                {
                    return FromChildFailure(advanced);
                }
                child = advanced.Snapshot;
                continue;
            }

            if (child.Phase == FacilityBufferDestinationCustodyDrainPhase
                .EffectCommittedAwaitingOwnerAck)
            {
                FacilityBufferDestinationCustodyDrainResult acknowledged =
                    drains.TryAcknowledge(
                        order.materialTerminalStepOperationId,
                        order.materialTerminalReceiptFingerprint);
                if (!acknowledged.Succeeded || acknowledged.Snapshot == null)
                {
                    return FromChildFailure(acknowledged);
                }
                child = acknowledged.Snapshot;
                order.materialTerminalDrainPhase =
                    SurgeryMaterialTerminalDrainPhase
                        .OwnerAcknowledgedAwaitingClosure;
            }

            if (!SurgeryMaterialTerminalJoin.TryValidate(
                    order,
                    child,
                    out joinFailure))
            {
                return Conflict(joinFailure);
            }
            if (!child.OwnerAcknowledged)
            {
                return Deferred(
                    "surgery-material-terminal-child-not-acknowledged:"
                    + order.orderId);
            }

            order.materialTerminalDrainPhase =
                SurgeryMaterialTerminalDrainPhase
                    .OwnerAcknowledgedAwaitingClosure;
            if (!materialDestinations.TryRevoke(
                    order,
                    out string revokeFailure))
            {
                return Deferred(
                    "surgery-material-terminal-authority-revoke-deferred:"
                    + revokeFailure);
            }

            return new SurgeryMaterialTerminalAdvanceResult(
                SurgeryMaterialTerminalAdvanceStatus.ReadyForOwnerClosure,
                string.Empty);
        }
    }

    private static void PersistPreparedJoin(
        SurgeryOrder order,
        SurgeryOrderState terminalTarget,
        FacilityBufferDestinationCustodyDrainSnapshot child)
    {
        order.materialTerminalTargetState = terminalTarget;
        order.materialTerminalParentOperationId = child.ParentOperationId;
        order.materialTerminalStepOperationId = child.StepOperationId;
        order.materialTerminalRequestFingerprint = child.RequestFingerprint;
        order.materialTerminalCommitId = string.Empty;
        order.materialTerminalReceiptFingerprint = string.Empty;
        order.materialTerminalInputQuantity = child.InputQuantity;
        order.materialTerminalInputMassGrams = child.InputMassGrams;
        order.materialTerminalOwnerX = child.OwnerGridX;
        order.materialTerminalOwnerY = child.OwnerGridY;
        order.materialTerminalDrainPhase =
            SurgeryMaterialTerminalDrainPhase.Prepared;
        order.state = SurgeryOrderState.TerminalDraining;
    }

    private static bool IsTerminalTarget(SurgeryOrderState value) => value is
        SurgeryOrderState.Completed
        or SurgeryOrderState.Failed
        or SurgeryOrderState.Cancelled;

    private static string CreateProgressCursor(
        FacilityBufferDestinationCustodyDrainSnapshot value) =>
        ((int)value.Phase).ToString(System.Globalization.CultureInfo.InvariantCulture)
        + ":" + value.CompletedActorCount.ToString(
            System.Globalization.CultureInfo.InvariantCulture)
        + ":" + value.ReleasedOperationCount.ToString(
            System.Globalization.CultureInfo.InvariantCulture);

    private static SurgeryMaterialTerminalAdvanceResult FromChildFailure(
        FacilityBufferDestinationCustodyDrainResult child) => child.Status ==
            FacilityBufferDestinationCustodyDrainStatus.Deferred
        ? Deferred(child.FailureReason)
        : Conflict(child.FailureReason);

    private static SurgeryMaterialTerminalAdvanceResult Deferred(
        string failureReason) => new(
        SurgeryMaterialTerminalAdvanceStatus.Deferred,
        failureReason);

    private static SurgeryMaterialTerminalAdvanceResult Conflict(
        string failureReason) => new(
        SurgeryMaterialTerminalAdvanceStatus.Conflict,
        failureReason);
}

internal static class SurgeryMaterialTerminalJoin
{
    internal static bool TryValidate(
        SurgeryOrder order,
        FacilityBufferDestinationCustodyDrainSnapshot child,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || child == null)
        {
            failureReason = "surgery-material-terminal-join-missing";
            return false;
        }
        if (!string.Equals(
                child.ParentOperationId,
                order.materialTerminalParentOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                child.StepOperationId,
                order.materialTerminalStepOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                child.OwnerStableId,
                SurgeryMaterialTerminalIdentity.FormatOwnerStableId(
                    order.orderId),
                StringComparison.Ordinal)
            || !string.Equals(
                child.OwnerSubjectId,
                order.orderId,
                StringComparison.Ordinal)
            || !string.Equals(
                child.OwnerFacilityId,
                order.facilityId,
                StringComparison.Ordinal)
            || !string.Equals(
                child.SourceDestinationId,
                order.materialDestinationId,
                StringComparison.Ordinal)
            || !string.Equals(
                child.SourceAuthorityFingerprint,
                order.materialCapacityFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                child.RequestFingerprint,
                order.materialTerminalRequestFingerprint,
                StringComparison.Ordinal)
            || child.OwnerGridX != order.materialTerminalOwnerX
            || child.OwnerGridY != order.materialTerminalOwnerY
            || child.InputQuantity != order.materialTerminalInputQuantity
            || child.InputMassGrams != order.materialTerminalInputMassGrams
            || child.ReleasedQuantity > child.InputQuantity
            || child.ReleasedMassGrams > child.InputMassGrams
            || child.CompletedActorCount < 0
            || child.CompletedActorCount > child.SourceActorCount
            || child.ReleasedOperationCount < 0
            || child.ReleasedOperationCount > child.SourceOperationCount)
        {
            failureReason = "surgery-material-terminal-child-join-mismatch:"
                + order.orderId;
            return false;
        }

        bool childEffectCommitted = child.EffectCommitted;
        bool orderEffectCommitted = order.materialTerminalDrainPhase is
            SurgeryMaterialTerminalDrainPhase.EffectCommittedAwaitingAck
            or SurgeryMaterialTerminalDrainPhase
                .OwnerAcknowledgedAwaitingClosure
            or SurgeryMaterialTerminalDrainPhase.ClosedAwaitingCheckpointGc;
        if (childEffectCommitted != orderEffectCommitted
            || orderEffectCommitted
            && (!string.Equals(
                    child.CommitId,
                    order.materialTerminalCommitId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    child.ReceiptFingerprint,
                    order.materialTerminalReceiptFingerprint,
                    StringComparison.Ordinal)
                || child.ReleasedQuantity != child.InputQuantity
                || child.ReleasedMassGrams != child.InputMassGrams))
        {
            failureReason = "surgery-material-terminal-effect-join-mismatch:"
                + order.orderId;
            return false;
        }

        bool orderAcknowledged = order.materialTerminalDrainPhase is
            SurgeryMaterialTerminalDrainPhase
                .OwnerAcknowledgedAwaitingClosure
            or SurgeryMaterialTerminalDrainPhase.ClosedAwaitingCheckpointGc;
        if (child.OwnerAcknowledged != orderAcknowledged)
        {
            failureReason =
                "surgery-material-terminal-ack-join-mismatch:"
                + order.orderId;
            return false;
        }
        return true;
    }
}
