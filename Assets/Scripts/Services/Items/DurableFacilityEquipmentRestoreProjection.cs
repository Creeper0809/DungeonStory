using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class DurableFacilityEquipmentRestoreProjection
{
    private readonly IDurableFacilityEquipmentPolicyQuery policies;
    private readonly IDurableFacilityEquipmentCapacityProjectionQuery capacity;
    private readonly IFacilityBufferDestinationCustodyDrainRestoreCandidateQuery
        childDrains;

    public DurableFacilityEquipmentRestoreProjection(
        IDurableFacilityEquipmentPolicyQuery policies,
        IDurableFacilityEquipmentCapacityProjectionQuery capacity,
        IFacilityBufferDestinationCustodyDrainRestoreCandidateQuery childDrains)
    {
        this.policies = policies ?? throw new ArgumentNullException(nameof(policies));
        this.capacity = capacity ?? throw new ArgumentNullException(nameof(capacity));
        this.childDrains = childDrains
            ?? throw new ArgumentNullException(nameof(childDrains));
    }

    public DurableFacilityEquipmentRestoreCandidate Prepare(
        DungeonDurableFacilityEquipmentSaveData source)
    {
        ValidateLocal(source);
        if (!childDrains.IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Durable facility-equipment restore requires the detached Items custody candidate.");
        }

        DurableFacilityEquipmentCrossAggregateJoin.Validate(
            source.slots,
            childDrains.Drains);

        DurableFacilityEquipmentSlotSnapshot[] slots = source.slots
            .Select(PrepareSlot)
            .OrderBy(value => value.AssignmentSequence)
            .ToArray();
        ValidateNoOrphanChildren(slots);
        return new DurableFacilityEquipmentRestoreCandidate(
            source.nextAssignmentSequence,
            source.revision,
            slots);
    }

    public void ValidateLocal(
        DungeonDurableFacilityEquipmentSaveData source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source.version !=
            DungeonDurableFacilityEquipmentSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                "Unsupported durable facility-equipment save version. "
                + "Start a new game for legacy saves.");
        }
        if (source.slots == null
            || source.nextAssignmentSequence <= 0L
            || source.revision <= 0L
            || source.slots.Any(value => value == null)
            || source.slots.Select(value => value.assignmentSequence)
                .Distinct().Count() != source.slots.Count
            || source.slots.Any(value => value.assignmentSequence <= 0L)
            || (source.slots.Count > 0
                && source.nextAssignmentSequence <=
                    source.slots.Max(value => value.assignmentSequence)))
        {
            throw new InvalidOperationException(
                "Durable facility-equipment save root is invalid.");
        }
        foreach (DurableFacilityEquipmentSlotSaveData row in source.slots)
            ValidateLocalRow(row);
        if (source.slots
                .Where(value => value.lifecyclePhase !=
                    DurableFacilityEquipmentSlotLifecyclePhase
                        .ClosedAwaitingCheckpointGc)
                .GroupBy(value => (
                    value.logicalOwnerDomain,
                    value.ownerSubjectId))
                .Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException(
                "Durable facility-equipment save violates active-1 ownership.");
        }
    }

    public static DurableFacilityEquipmentSlotSaveData Capture(
        DurableFacilityEquipmentSlotSnapshot source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return new DurableFacilityEquipmentSlotSaveData
        {
            logicalOwnerDomain = source.Key.LogicalOwnerDomain,
            ownerSubjectId = source.Key.OwnerSubjectId,
            policyId = source.PolicyId,
            policyRevision = source.PolicyRevision,
            capacityPolicyKind = source.CapacityPolicyKind,
            usabilityPolicyKind = source.UsabilityPolicyKind,
            ownerFacilityId = source.OwnerFacilityId.Value,
            dropPositionX = source.DropPosition.x,
            dropPositionY = source.DropPosition.y,
            assignmentSequence = source.AssignmentSequence,
            assignmentFingerprint = source.AssignmentFingerprint,
            maximumMassGrams = source.Capacity.Value,
            sourceAuthorityRevision = source.SourceAuthorityRevision,
            sourceAuthorityFingerprint = source.SourceAuthorityFingerprint,
            lifecyclePhase = source.LifecyclePhase,
            closeReasonCode = source.CloseReasonCode,
            authoritiesRevoked = source.AuthoritiesRevoked,
            drainParentOperationId =
                source.Drain?.ParentOperationId ?? string.Empty,
            drainStepOperationId = source.Drain?.StepOperationId ?? string.Empty,
            drainOwnerStableId =
                source.Drain?.OwnerStableId ?? string.Empty,
            drainOwnerSubjectId =
                source.Drain?.OwnerSubjectId ?? string.Empty,
            drainOwnerFacilityId =
                source.Drain?.OwnerFacilityId ?? string.Empty,
            drainSourceDestinationId =
                source.Drain?.SourceDestinationId ?? string.Empty,
            drainSourceAuthorityFingerprint =
                source.Drain?.SourceAuthorityFingerprint ?? string.Empty,
            drainRequestFingerprint =
                source.Drain?.RequestFingerprint ?? string.Empty,
            drainOwnerGridX = source.Drain?.OwnerGridX ?? 0,
            drainOwnerGridY = source.Drain?.OwnerGridY ?? 0,
            drainPhase = source.Drain?.Phase ?? default,
            drainSourceActorCount = source.Drain?.SourceActorCount ?? 0,
            drainCompletedActorCount =
                source.Drain?.CompletedActorCount ?? 0,
            drainSourceOperationCount =
                source.Drain?.SourceOperationCount ?? 0,
            drainReleasedOperationCount =
                source.Drain?.ReleasedOperationCount ?? 0,
            drainInputQuantity = source.Drain?.InputQuantity ?? 0,
            drainInputMassGrams = source.Drain?.InputMassGrams ?? 0L,
            drainReleasedQuantity = source.Drain?.ReleasedQuantity ?? 0,
            drainReleasedMassGrams =
                source.Drain?.ReleasedMassGrams ?? 0L,
            drainCommitId = source.Drain?.CommitId ?? string.Empty,
            drainReceiptFingerprint =
                source.Drain?.ReceiptFingerprint ?? string.Empty
        };
    }

    private DurableFacilityEquipmentSlotSnapshot PrepareSlot(
        DurableFacilityEquipmentSlotSaveData source)
    {
        if (source == null)
            throw new InvalidOperationException(
                "Durable facility-equipment save contains a null slot.");
        DurableFacilityEquipmentSlotKey key = new(
            source.logicalOwnerDomain,
            source.ownerSubjectId);
        if (!policies.TryGetPolicy(
                source.policyId,
                out DurableFacilityEquipmentPolicy policy))
        {
            throw new InvalidOperationException(
                "Durable facility-equipment policy is not registered: "
                + source.policyId);
        }
        DurableFacilityEquipmentAssignment assignment = policy.CreateAssignment(
            source.ownerSubjectId,
            (BuildingInstanceId)source.ownerFacilityId,
            new Vector2Int(source.dropPositionX, source.dropPositionY));
        string assignmentFingerprint =
            DurableFacilityEquipmentFingerprint.CreateAssignment(assignment);
        if (!assignment.Key.Equals(key)
            || assignment.PolicyRevision != source.policyRevision
            || !string.Equals(
                assignment.CapacityPolicyKind,
                source.capacityPolicyKind,
                StringComparison.Ordinal)
            || !string.Equals(
                assignment.UsabilityPolicyKind,
                source.usabilityPolicyKind,
                StringComparison.Ordinal)
            || !string.Equals(
                assignmentFingerprint,
                source.assignmentFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Durable facility-equipment saved assignment drifted from its registered policy: "
                + source.policyId);
        }
        if (!capacity.TryProjectMaximumMass(
                assignment,
                out DurableFacilityEquipmentCapacityProjection projection,
                out string projectionFailure))
        {
            throw new InvalidOperationException(
                "Durable facility-equipment capacity projection failed: "
                + projectionFailure);
        }
        if (projection.MaximumMass.Value != source.maximumMassGrams
            || projection.SourceAuthorityRevision !=
                source.sourceAuthorityRevision
            || !string.Equals(
                projection.SourceAuthorityFingerprint,
                source.sourceAuthorityFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Durable facility-equipment saved capacity drifted from its source authority: "
                + source.policyId);
        }

        FacilityBufferDestinationCustodyDrainSnapshot child =
            ResolveChild(source, assignment);
        DurableFacilityEquipmentRequirementStatus[] requirements = assignment
            .Requirements
            .Select(value => new DurableFacilityEquipmentRequirementStatus(
                value,
                pendingQuantity: 0,
                bufferedUsableQuantity: 0))
            .ToArray();
        return new DurableFacilityEquipmentSlotSnapshot(
            assignment,
            source.assignmentSequence,
            DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                assignment.Key,
                source.assignmentSequence),
            DurableFacilityEquipmentSlotIdentity.BuildOwnerOperationId(
                assignment.Key,
                source.assignmentSequence),
            assignmentFingerprint,
            projection,
            requirements,
            source.lifecyclePhase,
            source.closeReasonCode,
            child,
            source.authoritiesRevoked);
    }

    private void ValidateLocalRow(
        DurableFacilityEquipmentSlotSaveData source)
    {
        DurableFacilityEquipmentSlotKey key = new(
            source.logicalOwnerDomain,
            source.ownerSubjectId);
        if (!policies.TryGetPolicy(
                source.policyId,
                out DurableFacilityEquipmentPolicy policy))
        {
            throw new InvalidOperationException(
                "Durable facility-equipment policy is not registered: "
                + source.policyId);
        }
        DurableFacilityEquipmentAssignment assignment = policy.CreateAssignment(
            source.ownerSubjectId,
            (BuildingInstanceId)source.ownerFacilityId,
            new Vector2Int(source.dropPositionX, source.dropPositionY));
        string fingerprint =
            DurableFacilityEquipmentFingerprint.CreateAssignment(assignment);
        if (!capacity.TryProjectMaximumMass(
                assignment,
                out DurableFacilityEquipmentCapacityProjection projection,
                out string projectionFailure))
        {
            throw new InvalidOperationException(
                "Durable facility-equipment capacity projection failed: "
                + projectionFailure);
        }
        if (!assignment.Key.Equals(key)
            || assignment.PolicyRevision != source.policyRevision
            || !string.Equals(assignment.CapacityPolicyKind,
                source.capacityPolicyKind, StringComparison.Ordinal)
            || !string.Equals(assignment.UsabilityPolicyKind,
                source.usabilityPolicyKind, StringComparison.Ordinal)
            || !string.Equals(fingerprint, source.assignmentFingerprint,
                StringComparison.Ordinal)
            || projection.MaximumMass.Value != source.maximumMassGrams
            || projection.SourceAuthorityRevision !=
                source.sourceAuthorityRevision
            || !string.Equals(
                projection.SourceAuthorityFingerprint,
                source.sourceAuthorityFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Durable facility-equipment saved assignment or capacity is invalid: "
                + source.policyId);
        }
        if (!Enum.IsDefined(
                typeof(DurableFacilityEquipmentSlotLifecyclePhase),
                source.lifecyclePhase))
        {
            throw new InvalidOperationException(
                "Durable facility-equipment lifecycle phase is unknown.");
        }
        bool reasonEmpty = string.IsNullOrEmpty(source.closeReasonCode);
        bool reasonCanonical = Canonical(source.closeReasonCode);
        string expectedStep =
            DurableFacilityEquipmentSlotIdentity.BuildDrainStepOperationId(
                assignment.Key,
                source.assignmentSequence);
        bool childEmpty = string.IsNullOrEmpty(source.drainParentOperationId)
            && string.IsNullOrEmpty(source.drainStepOperationId)
            && string.IsNullOrEmpty(source.drainOwnerStableId)
            && string.IsNullOrEmpty(source.drainOwnerSubjectId)
            && string.IsNullOrEmpty(source.drainOwnerFacilityId)
            && string.IsNullOrEmpty(source.drainSourceDestinationId)
            && string.IsNullOrEmpty(
                source.drainSourceAuthorityFingerprint)
            && string.IsNullOrEmpty(source.drainRequestFingerprint)
            && source.drainOwnerGridX == 0
            && source.drainOwnerGridY == 0
            && source.drainPhase == default
            && source.drainSourceActorCount == 0
            && source.drainCompletedActorCount == 0
            && source.drainSourceOperationCount == 0
            && source.drainReleasedOperationCount == 0
            && source.drainInputQuantity == 0
            && source.drainInputMassGrams == 0L
            && source.drainReleasedQuantity == 0
            && source.drainReleasedMassGrams == 0L
            && string.IsNullOrEmpty(source.drainCommitId)
            && string.IsNullOrEmpty(source.drainReceiptFingerprint);
        bool childReference = string.Equals(
                source.drainParentOperationId,
                DurableFacilityEquipmentSlotIdentity
                    .BuildDrainParentOperationId(
                        assignment.Key,
                        source.assignmentSequence),
                StringComparison.Ordinal)
            && string.Equals(
                source.drainStepOperationId,
                expectedStep,
                StringComparison.Ordinal)
            && string.Equals(
                source.drainOwnerStableId,
                DurableFacilityEquipmentSlotIdentity.BuildOwnerStableId(
                    assignment.Key,
                    source.assignmentSequence),
                StringComparison.Ordinal)
            && string.Equals(
                source.drainOwnerSubjectId,
                assignment.Key.OwnerSubjectId,
                StringComparison.Ordinal)
            && string.Equals(
                source.drainOwnerFacilityId,
                assignment.OwnerFacilityId.Value,
                StringComparison.Ordinal)
            && string.Equals(
                source.drainSourceDestinationId,
                DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                    assignment.Key,
                    source.assignmentSequence),
                StringComparison.Ordinal)
            && string.Equals(
                source.drainSourceAuthorityFingerprint,
                source.sourceAuthorityFingerprint,
                StringComparison.Ordinal)
            && Canonical(source.drainRequestFingerprint)
            && Enum.IsDefined(
                typeof(FacilityBufferDestinationCustodyDrainPhase),
                source.drainPhase)
            && source.drainSourceActorCount >= 0
            && source.drainCompletedActorCount >= 0
            && source.drainCompletedActorCount <=
                source.drainSourceActorCount
            && source.drainSourceOperationCount >= 0
            && source.drainReleasedOperationCount >= 0
            && source.drainReleasedOperationCount <=
                source.drainSourceOperationCount
            && source.drainInputQuantity >= 0
            && source.drainInputMassGrams >= 0L
            && source.drainReleasedQuantity >= 0
            && source.drainReleasedQuantity <= source.drainInputQuantity
            && source.drainReleasedMassGrams >= 0L
            && source.drainReleasedMassGrams <=
                source.drainInputMassGrams
            && (string.IsNullOrEmpty(source.drainCommitId)
                || Canonical(source.drainCommitId))
            && (string.IsNullOrEmpty(source.drainReceiptFingerprint)
                || Canonical(source.drainReceiptFingerprint));
        bool childEffectCommitted = source.drainPhase is
            FacilityBufferDestinationCustodyDrainPhase
                .EffectCommittedAwaitingOwnerAck
            or FacilityBufferDestinationCustodyDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc;
        bool childOwnerAcknowledged = source.drainPhase ==
            FacilityBufferDestinationCustodyDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc;
        bool lifecycleValid = source.lifecyclePhase switch
        {
            DurableFacilityEquipmentSlotLifecyclePhase.Active =>
                reasonEmpty && childEmpty && !source.authoritiesRevoked,
            DurableFacilityEquipmentSlotLifecyclePhase.CloseRequested =>
                reasonCanonical && childEmpty && !source.authoritiesRevoked,
            DurableFacilityEquipmentSlotLifecyclePhase.Draining =>
                reasonCanonical
                && childReference
                && !childOwnerAcknowledged
                && (!source.authoritiesRevoked || childEffectCommitted),
            DurableFacilityEquipmentSlotLifecyclePhase
                .ClosedAwaitingCheckpointGc =>
                reasonCanonical
                && childReference
                && childOwnerAcknowledged
                && Canonical(source.drainReceiptFingerprint)
                && source.authoritiesRevoked,
            _ => false
        };
        if (!lifecycleValid)
        {
            throw new InvalidOperationException(
                "Durable facility-equipment saved lifecycle is invalid: "
                + source.assignmentSequence);
        }
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private FacilityBufferDestinationCustodyDrainSnapshot ResolveChild(
        DurableFacilityEquipmentSlotSaveData source,
        DurableFacilityEquipmentAssignment assignment)
    {
        if (!Enum.IsDefined(
                typeof(DurableFacilityEquipmentSlotLifecyclePhase),
                source.lifecyclePhase))
        {
            throw new InvalidOperationException(
                "Durable facility-equipment lifecycle phase is unknown.");
        }
        string expectedStep =
            DurableFacilityEquipmentSlotIdentity.BuildDrainStepOperationId(
                assignment.Key,
                source.assignmentSequence);
        bool requiresChild = source.lifecyclePhase is
            DurableFacilityEquipmentSlotLifecyclePhase.Draining
            or DurableFacilityEquipmentSlotLifecyclePhase
                .ClosedAwaitingCheckpointGc;
        if (!requiresChild)
        {
            if (!DrainFieldsEmpty(source)
                || childDrains.TryGetDrain(expectedStep, out _))
            {
                throw new InvalidOperationException(
                    "Durable facility-equipment non-draining slot has a custody child.");
            }
            return null;
        }
        if (!string.Equals(
                source.drainStepOperationId,
                expectedStep,
                StringComparison.Ordinal)
            || !childDrains.TryGetDrain(expectedStep, out
                FacilityBufferDestinationCustodyDrainSnapshot child)
            || child == null)
        {
            throw new InvalidOperationException(
                "Durable facility-equipment draining slot has no exact custody child.");
        }
        string expectedParent =
            DurableFacilityEquipmentSlotIdentity.BuildDrainParentOperationId(
                assignment.Key,
                source.assignmentSequence);
        string expectedOwner =
            DurableFacilityEquipmentSlotIdentity.BuildOwnerStableId(
                assignment.Key,
                source.assignmentSequence);
        string expectedDestination =
            DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                assignment.Key,
                source.assignmentSequence);
        if (!string.Equals(
                child.ParentOperationId,
                source.drainParentOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                child.StepOperationId,
                source.drainStepOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                child.OwnerStableId,
                source.drainOwnerStableId,
                StringComparison.Ordinal)
            || !string.Equals(
                child.OwnerSubjectId,
                source.drainOwnerSubjectId,
                StringComparison.Ordinal)
            || !string.Equals(
                child.OwnerFacilityId,
                source.drainOwnerFacilityId,
                StringComparison.Ordinal)
            || !string.Equals(
                child.SourceDestinationId,
                source.drainSourceDestinationId,
                StringComparison.Ordinal)
            || !string.Equals(
                child.SourceAuthorityFingerprint,
                source.drainSourceAuthorityFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                child.RequestFingerprint,
                source.drainRequestFingerprint,
                StringComparison.Ordinal)
            || child.OwnerGridX != source.drainOwnerGridX
            || child.OwnerGridY != source.drainOwnerGridY
            || child.Phase != source.drainPhase
            || child.SourceActorCount != source.drainSourceActorCount
            || child.CompletedActorCount != source.drainCompletedActorCount
            || child.SourceOperationCount !=
                source.drainSourceOperationCount
            || child.ReleasedOperationCount !=
                source.drainReleasedOperationCount
            || child.InputQuantity != source.drainInputQuantity
            || child.InputMassGrams != source.drainInputMassGrams
            || child.ReleasedQuantity != source.drainReleasedQuantity
            || child.ReleasedMassGrams != source.drainReleasedMassGrams
            || !string.Equals(
                child.CommitId,
                source.drainCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                child.ReceiptFingerprint,
                source.drainReceiptFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                child.ParentOperationId,
                expectedParent,
                StringComparison.Ordinal)
            || !string.Equals(
                child.OwnerStableId,
                expectedOwner,
                StringComparison.Ordinal)
            || !string.Equals(
                child.OwnerSubjectId,
                assignment.Key.OwnerSubjectId,
                StringComparison.Ordinal)
            || !string.Equals(
                child.OwnerFacilityId,
                assignment.OwnerFacilityId.Value,
                StringComparison.Ordinal)
            || !string.Equals(
                child.SourceDestinationId,
                expectedDestination,
                StringComparison.Ordinal)
            || !string.Equals(
                child.SourceAuthorityFingerprint,
                source.sourceAuthorityFingerprint,
                StringComparison.Ordinal)
            || child.OwnerGridX != assignment.DropPosition.x
            || child.OwnerGridY != assignment.DropPosition.y
            || !string.Equals(
                child.RequestFingerprint,
                source.drainRequestFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                child.ReceiptFingerprint,
                source.drainReceiptFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Durable facility-equipment custody child join is not exact: "
                + expectedStep);
        }
        return child;
    }

    private void ValidateNoOrphanChildren(
        IReadOnlyList<DurableFacilityEquipmentSlotSnapshot> slots)
    {
        HashSet<string> ownedSteps = slots
            .Where(value => value.Drain != null)
            .Select(value => value.Drain.StepOperationId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (FacilityBufferDestinationCustodyDrainSnapshot child in
                 childDrains.Drains
                     ?? Array.Empty<
                         FacilityBufferDestinationCustodyDrainSnapshot>())
        {
            if (child != null
                && IsDurableChildClaim(child)
                && !ownedSteps.Contains(child.StepOperationId))
            {
                throw new InvalidOperationException(
                    "Durable facility-equipment custody child has no upper owner: "
                    + child.StepOperationId);
            }
        }
    }

    private static bool DrainFieldsEmpty(
        DurableFacilityEquipmentSlotSaveData source) =>
        string.IsNullOrEmpty(source.drainParentOperationId)
        && string.IsNullOrEmpty(source.drainStepOperationId)
        && string.IsNullOrEmpty(source.drainOwnerStableId)
        && string.IsNullOrEmpty(source.drainOwnerSubjectId)
        && string.IsNullOrEmpty(source.drainOwnerFacilityId)
        && string.IsNullOrEmpty(source.drainSourceDestinationId)
        && string.IsNullOrEmpty(source.drainSourceAuthorityFingerprint)
        && string.IsNullOrEmpty(source.drainRequestFingerprint)
        && source.drainOwnerGridX == 0
        && source.drainOwnerGridY == 0
        && source.drainPhase == default
        && source.drainSourceActorCount == 0
        && source.drainCompletedActorCount == 0
        && source.drainSourceOperationCount == 0
        && source.drainReleasedOperationCount == 0
        && source.drainInputQuantity == 0
        && source.drainInputMassGrams == 0L
        && source.drainReleasedQuantity == 0
        && source.drainReleasedMassGrams == 0L
        && string.IsNullOrEmpty(source.drainCommitId)
        && string.IsNullOrEmpty(source.drainReceiptFingerprint);

    private static bool IsDurableChildClaim(
        FacilityBufferDestinationCustodyDrainSnapshot child) =>
        child.OwnerStableId.StartsWith(
            DurableFacilityEquipmentSlotIdentity.OwnerStableIdPrefix,
            StringComparison.Ordinal)
        || child.ParentOperationId.StartsWith(
            DurableFacilityEquipmentSlotIdentity.DrainParentOperationPrefix,
            StringComparison.Ordinal)
        || child.StepOperationId.StartsWith(
            DurableFacilityEquipmentSlotIdentity.DrainParentOperationPrefix,
            StringComparison.Ordinal)
        || child.SourceDestinationId.StartsWith(
            DurableFacilityEquipmentSlotIdentity.DestinationPrefix,
            StringComparison.Ordinal);
}
