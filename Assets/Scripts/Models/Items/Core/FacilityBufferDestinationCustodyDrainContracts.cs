using System;
using System.Collections.Generic;
using UnityEngine;

public enum FacilityBufferDestinationCustodyDrainPhase
{
    Prepared = 0,
    ReleasingActors = 1,
    ReleasingOperationAuthority = 2,
    ReleasingDestination = 3,
    EffectCommittedAwaitingOwnerAck = 4,
    OwnerAcknowledgedAwaitingCheckpointGc = 5
}

public enum FacilityBufferDestinationCustodyDrainStatus
{
    None = 0,
    Applied = 1,
    Replay = 2,
    Deferred = 3,
    Conflict = 4
}

/// <summary>
/// Owner-neutral immutable command for draining every physical custody edge
/// attached to one FacilityBuffer destination. Domain owners keep only the
/// returned join identity; Items remains the authority for physical progress.
/// </summary>
public sealed class FacilityBufferDestinationCustodyDrainDescriptor
{
    public FacilityBufferDestinationCustodyDrainDescriptor(
        string parentOperationId,
        string stepOperationId,
        string ownerStableId,
        string ownerSubjectId,
        string ownerFacilityId,
        string sourceDestinationId,
        Vector2Int ownerPosition,
        string sourceAuthorityFingerprint)
    {
        ParentOperationId = parentOperationId ?? string.Empty;
        StepOperationId = stepOperationId ?? string.Empty;
        OwnerStableId = ownerStableId ?? string.Empty;
        OwnerSubjectId = ownerSubjectId ?? string.Empty;
        OwnerFacilityId = ownerFacilityId ?? string.Empty;
        SourceDestinationId = sourceDestinationId ?? string.Empty;
        OwnerPosition = ownerPosition;
        SourceAuthorityFingerprint = sourceAuthorityFingerprint ?? string.Empty;
    }

    public string ParentOperationId { get; }
    public string StepOperationId { get; }
    public string OwnerStableId { get; }
    public string OwnerSubjectId { get; }
    public string OwnerFacilityId { get; }
    public string SourceDestinationId { get; }
    public Vector2Int OwnerPosition { get; }
    public string SourceAuthorityFingerprint { get; }
}

public sealed class FacilityBufferDestinationCustodyDrainSnapshot
{
    public FacilityBufferDestinationCustodyDrainSnapshot(
        string parentOperationId,
        string stepOperationId,
        string ownerStableId,
        string ownerSubjectId,
        string ownerFacilityId,
        string sourceDestinationId,
        string sourceAuthorityFingerprint,
        string requestFingerprint,
        int ownerGridX,
        int ownerGridY,
        FacilityBufferDestinationCustodyDrainPhase phase,
        int sourceActorCount,
        int completedActorCount,
        int sourceOperationCount,
        int releasedOperationCount,
        int inputQuantity,
        long inputMassGrams,
        int releasedQuantity,
        long releasedMassGrams,
        string commitId,
        string receiptFingerprint)
    {
        ParentOperationId = parentOperationId ?? string.Empty;
        StepOperationId = stepOperationId ?? string.Empty;
        OwnerStableId = ownerStableId ?? string.Empty;
        OwnerSubjectId = ownerSubjectId ?? string.Empty;
        OwnerFacilityId = ownerFacilityId ?? string.Empty;
        SourceDestinationId = sourceDestinationId ?? string.Empty;
        SourceAuthorityFingerprint = sourceAuthorityFingerprint ?? string.Empty;
        RequestFingerprint = requestFingerprint ?? string.Empty;
        OwnerGridX = ownerGridX;
        OwnerGridY = ownerGridY;
        Phase = phase;
        SourceActorCount = sourceActorCount;
        CompletedActorCount = completedActorCount;
        SourceOperationCount = sourceOperationCount;
        ReleasedOperationCount = releasedOperationCount;
        InputQuantity = inputQuantity;
        InputMassGrams = inputMassGrams;
        ReleasedQuantity = releasedQuantity;
        ReleasedMassGrams = releasedMassGrams;
        CommitId = commitId ?? string.Empty;
        ReceiptFingerprint = receiptFingerprint ?? string.Empty;
    }

    public string ParentOperationId { get; }
    public string StepOperationId { get; }
    public string OwnerStableId { get; }
    public string OwnerSubjectId { get; }
    public string OwnerFacilityId { get; }
    public string SourceDestinationId { get; }
    public string SourceAuthorityFingerprint { get; }
    public string RequestFingerprint { get; }
    public int OwnerGridX { get; }
    public int OwnerGridY { get; }
    public FacilityBufferDestinationCustodyDrainPhase Phase { get; }
    public int SourceActorCount { get; }
    public int CompletedActorCount { get; }
    public int SourceOperationCount { get; }
    public int ReleasedOperationCount { get; }
    public int InputQuantity { get; }
    public long InputMassGrams { get; }
    public int ReleasedQuantity { get; }
    public long ReleasedMassGrams { get; }
    public string CommitId { get; }
    public string ReceiptFingerprint { get; }

    public bool EffectCommitted => Phase is
        FacilityBufferDestinationCustodyDrainPhase
            .EffectCommittedAwaitingOwnerAck
        or FacilityBufferDestinationCustodyDrainPhase
            .OwnerAcknowledgedAwaitingCheckpointGc;

    public bool OwnerAcknowledged => Phase ==
        FacilityBufferDestinationCustodyDrainPhase
            .OwnerAcknowledgedAwaitingCheckpointGc;
}

public readonly struct FacilityBufferDestinationCustodyDrainResult
{
    public FacilityBufferDestinationCustodyDrainResult(
        FacilityBufferDestinationCustodyDrainStatus status,
        FacilityBufferDestinationCustodyDrainSnapshot snapshot,
        string failureReason)
    {
        Status = status;
        Snapshot = snapshot;
        FailureReason = failureReason ?? string.Empty;
    }

    public FacilityBufferDestinationCustodyDrainStatus Status { get; }
    public FacilityBufferDestinationCustodyDrainSnapshot Snapshot { get; }
    public string FailureReason { get; }
    public bool Succeeded => Snapshot != null && (Status is
        FacilityBufferDestinationCustodyDrainStatus.Applied
        or FacilityBufferDestinationCustodyDrainStatus.Replay);
}

public interface IFacilityBufferDestinationCustodyDrainService
{
    bool RequiresImmediateRecoveryBeforeGameplayTick { get; }

    [GameplayInternalOnly(
        "Freezes and persists one owner-neutral FacilityBuffer destination custody drain before any physical release.",
        "Registered domain terminal-lifecycle adapters only")]
    FacilityBufferDestinationCustodyDrainResult TryPrepare(
        FacilityBufferDestinationCustodyDrainDescriptor descriptor);

    [GameplayInternalOnly(
        "Advances one durable FacilityBuffer destination custody effect using the Items-owned capture barrier.",
        "Registered domain terminal-lifecycle adapters only")]
    FacilityBufferDestinationCustodyDrainResult TryAdvance(
        string stepOperationId,
        string requestFingerprint);

    [GameplayInternalOnly(
        "Acknowledges an exact owner-neutral FacilityBuffer custody receipt after the domain owner persists its join.",
        "Registered domain terminal-lifecycle adapters only")]
    FacilityBufferDestinationCustodyDrainResult TryAcknowledge(
        string stepOperationId,
        string receiptFingerprint);

    bool TryCapture(
        string stepOperationId,
        out FacilityBufferDestinationCustodyDrainSnapshot snapshot);
}

public interface IFacilityBufferDestinationCustodyDrainRestoreCandidateQuery
{
    bool IsCandidateAvailable { get; }
    IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot> Drains { get; }

    bool TryGetDrain(
        string stepOperationId,
        out FacilityBufferDestinationCustodyDrainSnapshot snapshot);
}

public interface IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate
{
}

public interface IFacilityBufferDestinationCustodyDrainLiveQuery
{
    IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot> Drains { get; }
}

/// <summary>
/// Domain-neutral transactional collector for owner-acknowledged
/// FacilityBuffer custody tombstones. Domain owners never receive the
/// Production-specific row type hidden behind this port.
/// </summary>
public interface IFacilityBufferDestinationCustodyDrainCheckpointGcPort
{
    bool TryPrepareCheckpointGarbageCollection(
        IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot> snapshots,
        out IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate candidate,
        out string failureReason);

    bool TryPublishCheckpointGarbageCollection(
        IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate candidate,
        out string failureReason);

    void RollbackCheckpointGarbageCollection(
        IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate candidate);

    void CompleteCheckpointGarbageCollection(
        IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate candidate);
}

/// <summary>
/// Canonical, allocation-bounded projection shared by live Items custody,
/// detached restore candidates and cross-section save validation. Keeping the
/// phase map here prevents domain owners from inventing owner-specific terminal
/// semantics when new content adopts the FacilityBuffer contract.
/// </summary>
public static class FacilityBufferDestinationCustodyDrainProjection
{
    public static FacilityBufferDestinationCustodyDrainSnapshot ProjectValidated(
        ProductionInputDestinationCustodyDrainSaveData value)
    {
        if (!ProductionInputDestinationCustodyDrainContract.IsValidSave(value))
        {
            throw new InvalidOperationException(
                "FacilityBuffer custody drain save row is invalid.");
        }

        return new FacilityBufferDestinationCustodyDrainSnapshot(
            value.parentOperationId,
            value.stepOperationId,
            value.ownerStableId,
            value.billId,
            value.facilityId,
            value.sourceDestinationId,
            value.sourceClaimFingerprint,
            value.requestFingerprint,
            value.ownerGridX,
            value.ownerGridY,
            ProjectPhase(value.phase),
            value.sourceActors.Count,
            value.completedActorIds.Count,
            value.sourceOperations.Count,
            value.releasedOperationIds.Count,
            value.inputQuantity,
            value.inputMassGrams,
            value.releasedQuantity,
            value.releasedMassGrams,
            value.commitId,
            value.receiptFingerprint);
    }

    public static bool AreExactEqual(
        FacilityBufferDestinationCustodyDrainSnapshot left,
        FacilityBufferDestinationCustodyDrainSnapshot right) =>
        left != null
        && right != null
        && string.Equals(left.ParentOperationId, right.ParentOperationId,
            StringComparison.Ordinal)
        && string.Equals(left.StepOperationId, right.StepOperationId,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerStableId, right.OwnerStableId,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerSubjectId, right.OwnerSubjectId,
            StringComparison.Ordinal)
        && string.Equals(left.OwnerFacilityId, right.OwnerFacilityId,
            StringComparison.Ordinal)
        && string.Equals(left.SourceDestinationId, right.SourceDestinationId,
            StringComparison.Ordinal)
        && string.Equals(
            left.SourceAuthorityFingerprint,
            right.SourceAuthorityFingerprint,
            StringComparison.Ordinal)
        && string.Equals(left.RequestFingerprint, right.RequestFingerprint,
            StringComparison.Ordinal)
        && left.OwnerGridX == right.OwnerGridX
        && left.OwnerGridY == right.OwnerGridY
        && left.Phase == right.Phase
        && left.SourceActorCount == right.SourceActorCount
        && left.CompletedActorCount == right.CompletedActorCount
        && left.SourceOperationCount == right.SourceOperationCount
        && left.ReleasedOperationCount == right.ReleasedOperationCount
        && left.InputQuantity == right.InputQuantity
        && left.InputMassGrams == right.InputMassGrams
        && left.ReleasedQuantity == right.ReleasedQuantity
        && left.ReleasedMassGrams == right.ReleasedMassGrams
        && string.Equals(left.CommitId, right.CommitId,
            StringComparison.Ordinal)
        && string.Equals(left.ReceiptFingerprint, right.ReceiptFingerprint,
            StringComparison.Ordinal);

    private static FacilityBufferDestinationCustodyDrainPhase ProjectPhase(
        ProductionInputDestinationCustodyDrainPhase phase) => phase switch
    {
        ProductionInputDestinationCustodyDrainPhase.Prepared =>
            FacilityBufferDestinationCustodyDrainPhase.Prepared,
        ProductionInputDestinationCustodyDrainPhase.ReleasingActors =>
            FacilityBufferDestinationCustodyDrainPhase.ReleasingActors,
        ProductionInputDestinationCustodyDrainPhase
            .ReleasingOperationAuthority =>
            FacilityBufferDestinationCustodyDrainPhase
                .ReleasingOperationAuthority,
        ProductionInputDestinationCustodyDrainPhase.ReleasingDestination =>
            FacilityBufferDestinationCustodyDrainPhase.ReleasingDestination,
        ProductionInputDestinationCustodyDrainPhase
            .EffectCommittedAwaitingBillAck =>
            FacilityBufferDestinationCustodyDrainPhase
                .EffectCommittedAwaitingOwnerAck,
        ProductionInputDestinationCustodyDrainPhase
            .BillAcknowledgedAwaitingCheckpointGc =>
            FacilityBufferDestinationCustodyDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc,
        _ => throw new InvalidOperationException(
            "Unknown input destination custody drain phase: " + phase)
    };
}
