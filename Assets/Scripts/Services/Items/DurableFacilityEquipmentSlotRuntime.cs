using System;
using System.Collections.Generic;
using System.Linq;

public interface IDurableFacilityEquipmentPhysicalPort
{
    IReadOnlyList<WorldItemStackSnapshot> CaptureDestinationStacks(
        string destinationId);

    int GetCommittedDeliveryQuantity(
        string destinationId,
        ItemDefinitionId itemId);

    IReadOnlyList<WorldItemStackSnapshot> CaptureSupplyCandidates(
        ItemDefinitionId itemId);

    bool TryRequestExactStackDelivery(
        string stackId,
        int quantity,
        UnityEngine.Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason);
}

public interface IDurableFacilityEquipmentComponentMutationPort
{
    bool TryReplaceComponentExact(
        string stackId,
        long expectedContentRevision,
        ItemInstanceComponentSaveData replacement,
        out WorldItemStackSnapshot after,
        out string failureReason);

    bool TryRestoreComponentExact(
        string stackId,
        ItemInstanceComponentSaveData expectedCurrent,
        ItemInstanceComponentSaveData replacement,
        out WorldItemStackSnapshot after,
        out string failureReason);
}

public sealed class DurableFacilityEquipmentPhysicalPort :
    IDurableFacilityEquipmentPhysicalPort,
    IDurableFacilityEquipmentComponentMutationPort
{
    private readonly IWorldItemStackRuntime items;

    public DurableFacilityEquipmentPhysicalPort(IWorldItemStackRuntime items)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public IReadOnlyList<WorldItemStackSnapshot> CaptureDestinationStacks(
        string destinationId) => items.GetAllStacks()
        .Where(value => value != null
            && string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal))
        .OrderBy(value => value.StackId, StringComparer.Ordinal)
        .ToArray();

    public int GetCommittedDeliveryQuantity(
        string destinationId,
        ItemDefinitionId itemId) =>
        items.GetCommittedHaulDeliveryQuantity(
            destinationId,
            itemId.Value);

    public IReadOnlyList<WorldItemStackSnapshot> CaptureSupplyCandidates(
        ItemDefinitionId itemId) => items.GetAllStacks()
        .Where(value => value != null
            && string.Equals(
                value.ItemId,
                itemId.Value,
                StringComparison.Ordinal)
            && value.Quantity > 0
            && value.State is WorldItemStackState.Loose
                or WorldItemStackState.Stored
                or WorldItemStackState.FacilityOutputBuffer)
        .OrderBy(value => value.StackId, StringComparer.Ordinal)
        .ToArray();

    public bool TryRequestExactStackDelivery(
        string stackId,
        int quantity,
        UnityEngine.Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason) => items.TryRequestStackDelivery(
        stackId,
        quantity,
        destinationPosition,
        destinationId,
        out requested,
        out failureReason);

    public bool TryReplaceComponentExact(
        string stackId,
        long expectedContentRevision,
        ItemInstanceComponentSaveData replacement,
        out WorldItemStackSnapshot after,
        out string failureReason)
    {
        after = null;
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(stackId)
            || !string.Equals(stackId, stackId.Trim(), StringComparison.Ordinal)
            || expectedContentRevision < 0L
            || replacement == null
            || string.IsNullOrWhiteSpace(replacement.componentTypeId))
        {
            failureReason = "durable-equipment-component-mutation-input-invalid";
            return false;
        }
        WorldItemStackSnapshot[] before = items.GetAllStacks()
            .Where(value => value != null
                && string.Equals(value.StackId, stackId, StringComparison.Ordinal))
            .ToArray();
        if (before.Length != 1)
        {
            failureReason = "durable-equipment-component-stack-missing-or-duplicate";
            return false;
        }
        if (before[0].ContentRevision != expectedContentRevision)
        {
            failureReason = "durable-equipment-component-revision-drift";
            return false;
        }
        if (!items.TrySetInstanceComponent(stackId, replacement))
        {
            failureReason = "durable-equipment-component-mutation-rejected";
            return false;
        }
        WorldItemStackSnapshot[] published = items.GetAllStacks()
            .Where(value => value != null
                && string.Equals(value.StackId, stackId, StringComparison.Ordinal))
            .ToArray();
        if (published.Length != 1
            || published[0].ContentRevision <= expectedContentRevision)
        {
            failureReason = "durable-equipment-component-publication-invalid";
            return false;
        }
        after = published[0];
        return true;
    }

    public bool TryRestoreComponentExact(
        string stackId,
        ItemInstanceComponentSaveData expectedCurrent,
        ItemInstanceComponentSaveData replacement,
        out WorldItemStackSnapshot after,
        out string failureReason)
    {
        after = null;
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(stackId)
            || !string.Equals(stackId, stackId.Trim(), StringComparison.Ordinal)
            || expectedCurrent == null
            || replacement == null
            || string.IsNullOrWhiteSpace(expectedCurrent.componentTypeId)
            || !string.Equals(
                expectedCurrent.componentTypeId,
                replacement.componentTypeId,
                StringComparison.Ordinal))
        {
            failureReason = "durable-equipment-component-restore-input-invalid";
            return false;
        }
        WorldItemStackSnapshot[] before = items.GetAllStacks()
            .Where(value => value != null
                && string.Equals(value.StackId, stackId, StringComparison.Ordinal))
            .ToArray();
        ItemInstanceComponentSaveData actual = before.Length == 1
            ? (before[0].Components ?? Array.Empty<ItemInstanceComponentSaveData>())
                .SingleOrDefault(value => value != null
                    && string.Equals(
                        value.componentTypeId,
                        expectedCurrent.componentTypeId,
                        StringComparison.Ordinal))
            : null;
        if (actual == null
            || !string.Equals(
                actual.ToCanonicalString(),
                expectedCurrent.ToCanonicalString(),
                StringComparison.Ordinal))
        {
            failureReason = "durable-equipment-component-restore-drift";
            return false;
        }
        if (!items.TrySetInstanceComponent(stackId, replacement))
        {
            failureReason = "durable-equipment-component-restore-rejected";
            return false;
        }
        WorldItemStackSnapshot[] published = items.GetAllStacks()
            .Where(value => value != null
                && string.Equals(value.StackId, stackId, StringComparison.Ordinal))
            .ToArray();
        if (published.Length != 1)
        {
            failureReason = "durable-equipment-component-restore-publication-invalid";
            return false;
        }
        after = published[0];
        return true;
    }
}

public sealed class DurableFacilityEquipmentSlotRuntime :
    IDurableFacilityEquipmentSlotCommand,
    IDurableFacilityEquipmentSlotQuery,
    IDurableFacilityEquipmentSlotPersistence,
    IDurableFacilityEquipmentSlotCheckpointGcPort,
    IDungeonRestoreTransactionParticipant
{
    private const string RestoreParticipantId =
        "222.world.durable-facility-equipment-slots";
    private readonly IDurableFacilityEquipmentPolicyQuery policies;
    private readonly IDurableFacilityEquipmentCapacityProjectionQuery capacity;
    private readonly IDurableFacilityEquipmentUsabilityQuery usability;
    private readonly IDurableFacilityEquipmentPhysicalPort physical;
    private readonly IFacilityBufferDestinationLifecycleCommand lifecycle;
    private readonly IFacilityBufferMassCapacityQuery capacityAuthority;
    private readonly IFacilityBufferDestinationCustodyDrainService drains;
    private readonly IDurableFacilityEquipmentAdmissionFenceCommand fences;
    private Dictionary<DurableFacilityEquipmentSlotKey, SlotState>
        active = new();
    private Dictionary<long, SlotState> bySequence = new();
    private long nextAssignmentSequence = 1L;
    private long revision = 1L;
    private DurableFacilityEquipmentRestoreCandidate stagedRestore;
    private DurableFacilityEquipmentRestoreCandidate previousRestore;
    private IReadOnlyList<DurableFacilityEquipmentAdmissionFenceRecord>
        previousFences;
    private bool restoreTransactionActive;
    private bool restorePublished;
    private CheckpointGcCandidate checkpointGcCandidate;

    public DurableFacilityEquipmentSlotRuntime(
        IDurableFacilityEquipmentPolicyQuery policies,
        IDurableFacilityEquipmentCapacityProjectionQuery capacity,
        IDurableFacilityEquipmentUsabilityQuery usability,
        IDurableFacilityEquipmentPhysicalPort physical,
        IFacilityBufferDestinationLifecycleCommand lifecycle,
        IFacilityBufferMassCapacityQuery capacityAuthority,
        IFacilityBufferDestinationCustodyDrainService drains,
        IDurableFacilityEquipmentAdmissionFenceCommand fences)
    {
        this.policies = policies ?? throw new ArgumentNullException(nameof(policies));
        this.capacity = capacity ?? throw new ArgumentNullException(nameof(capacity));
        this.usability = usability ?? throw new ArgumentNullException(nameof(usability));
        this.physical = physical ?? throw new ArgumentNullException(nameof(physical));
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        this.capacityAuthority = capacityAuthority
            ?? throw new ArgumentNullException(nameof(capacityAuthority));
        this.drains = drains ?? throw new ArgumentNullException(nameof(drains));
        this.fences = fences ?? throw new ArgumentNullException(nameof(fences));
        if (!drains.RequiresImmediateRecoveryBeforeGameplayTick)
        {
            throw new InvalidOperationException(
                "Durable equipment slots require immediate custody-drain recovery.");
        }
    }

    [GameplayInternalOnly(
        "Reconciles one registered durable facility-equipment assignment.",
        "Registered facility-equipment assignment producers only")]
    public DurableFacilityEquipmentSlotResult TryReconcile(
        DurableFacilityEquipmentAssignment desired)
    {
        if (!TryValidateRegisteredAssignment(desired, out string failureReason))
            return Conflict(null, failureReason);

        string desiredFingerprint =
            DurableFacilityEquipmentFingerprint.CreateAssignment(desired);
        if (active.TryGetValue(desired.Key, out SlotState existing))
        {
            if (existing.Phase ==
                    DurableFacilityEquipmentSlotLifecyclePhase.Active
                && string.Equals(
                    existing.AssignmentFingerprint,
                    desiredFingerprint,
                    StringComparison.Ordinal))
            {
                if (!TryPublishAuthorities(excludedSequence: 0L,
                        out failureReason))
                {
                    return Conflict(Capture(existing), failureReason);
                }
                return Replay(Capture(existing));
            }

            DurableFacilityEquipmentSlotResult closed =
                TryClose(desired.Key, "assignment-replaced");
            if (!closed.Succeeded
                || closed.Snapshot.LifecyclePhase !=
                    DurableFacilityEquipmentSlotLifecyclePhase
                        .ClosedAwaitingCheckpointGc)
            {
                return Deferred(
                    Capture(existing),
                    "durable-equipment-previous-assignment-draining");
            }
        }

        if (!capacity.TryProjectMaximumMass(
                desired,
                out DurableFacilityEquipmentCapacityProjection projection,
                out failureReason))
        {
            return Conflict(null, failureReason);
        }

        long sequence = nextAssignmentSequence;
        if (!TryGetNext(
                nextAssignmentSequence,
                out long nextSequence)
            || !TryGetNext(revision, out long nextRevision))
        {
            return Conflict(null, "durable-equipment-assignment-sequence-overflow");
        }
        SlotState created = new(
            desired,
            sequence,
            desiredFingerprint,
            projection);
        active.Add(desired.Key, created);
        bySequence.Add(sequence, created);
        if (!TryPublishAuthorities(excludedSequence: 0L, out failureReason))
        {
            active.Remove(desired.Key);
            bySequence.Remove(sequence);
            return Conflict(null, failureReason);
        }
        nextAssignmentSequence = nextSequence;
        revision = nextRevision;
        return Applied(Capture(created));
    }

    [GameplayInternalOnly(
        "Begins or resumes a common physical custody drain for one slot.",
        "Registered facility lifecycle and equipment-exhaustion handlers only")]
    public DurableFacilityEquipmentSlotResult TryClose(
        DurableFacilityEquipmentSlotKey key,
        string reasonCode)
    {
        if (!key.IsValid || !Canonical(reasonCode))
            return Conflict(null, "durable-equipment-close-input-invalid");
        if (!active.TryGetValue(key, out SlotState state))
            return Conflict(null, "durable-equipment-active-slot-missing:" + key);
        if (state.Phase == DurableFacilityEquipmentSlotLifecyclePhase.Active)
        {
            if (!TryGetNext(revision, out long nextRevision))
            {
                return Conflict(
                    Capture(state),
                    "durable-equipment-slot-revision-overflow");
            }
            FacilityBufferDestinationAdmissionFenceSubject fenceSubject =
                CreateFenceSubject(state);
            string fenceOperationId =
                DurableFacilityEquipmentSlotIdentity
                    .BuildDrainParentOperationId(
                        state.Assignment.Key,
                        state.Sequence);
            if (!fences.TryOpen(
                    fenceSubject,
                    fenceOperationId,
                    out string fenceFailure))
            {
                return Conflict(Capture(state), fenceFailure);
            }
            state.Phase =
                DurableFacilityEquipmentSlotLifecyclePhase.CloseRequested;
            state.CloseReasonCode = reasonCode;
            revision = nextRevision;
        }
        return AdvanceClose(state);
    }

    [GameplayInternalOnly(
        "Requests only the remaining exact registered equipment quantity.",
        "Registered facility-equipment assignment producers only")]
    public DurableFacilityEquipmentSlotResult TryEnsureSupply(
        DurableFacilityEquipmentSlotKey key)
    {
        if (!active.TryGetValue(key, out SlotState state))
            return Conflict(null, "durable-equipment-active-slot-missing:" + key);
        if (state.Phase != DurableFacilityEquipmentSlotLifecyclePhase.Active)
        {
            return Deferred(
                Capture(state),
                "durable-equipment-slot-draining");
        }

        RequirementObservation[] observations;
        try
        {
            observations = ObserveRequirements(state);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(
                Capture(state),
                "durable-equipment-observation-invalid:" + exception.Message);
        }
        if (observations.Any(value => value.ExhaustedBufferedQuantity > 0))
            return TryClose(key, "equipment-exhausted");
        if (observations.Any(value => value.IncompatibleBufferedQuantity > 0))
        {
            return Conflict(
                Capture(state),
                "durable-equipment-incompatible-buffered-item");
        }

        bool requestedAny = false;
        foreach (RequirementObservation observation in observations)
        {
            if (observation.CommittedDeliveryQuantity
                > observation.AssignedStackQuantity)
            {
                return Conflict(
                    Capture(state),
                    "durable-equipment-delivery-commitment-without-physical-custody");
            }
            int routed = observation.AssignedStackQuantity;
            int remaining = Math.Max(
                0,
                observation.Requirement.RequiredQuantity - routed);
            if (remaining == 0)
                continue;
            int requested = 0;
            string requestFailure = string.Empty;
            foreach (WorldItemStackSnapshot candidate in physical
                         .CaptureSupplyCandidates(observation.Requirement.ItemId)
                         ?? Array.Empty<WorldItemStackSnapshot>())
            {
                if (candidate == null
                    || string.Equals(
                        candidate.DestinationId,
                        state.DestinationId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                DurableFacilityEquipmentUseSubject subject =
                    DurableFacilityEquipmentUseSubjectCapture.Capture(candidate);
                if (!usability.TryEvaluate(
                        state.Assignment.UsabilityPolicyKind,
                        observation.Requirement,
                        subject,
                        out DurableFacilityEquipmentUsabilityResult result,
                        out string usabilityFailure))
                {
                    return Conflict(
                        Capture(state),
                        Canonical(usabilityFailure)
                            ? usabilityFailure
                            : "durable-equipment-supply-usability-failed");
                }
                if (!result.IsUsable)
                    continue;
                int candidateRequest = Math.Min(
                    remaining - requested,
                    Math.Max(0, candidate.Quantity));
                if (candidateRequest <= 0)
                    break;
                if (!physical.TryRequestExactStackDelivery(
                        candidate.StackId,
                        candidateRequest,
                        state.Assignment.DropPosition,
                        state.DestinationId,
                        out int exactRequested,
                        out requestFailure))
                {
                    continue;
                }
                if (exactRequested <= 0 || exactRequested > candidateRequest)
                {
                    return Conflict(
                        Capture(state),
                        "durable-equipment-delivery-request-quantity-invalid");
                }
                requested = checked(requested + exactRequested);
                if (requested == remaining)
                    break;
            }
            if (requested == 0)
            {
                return Deferred(
                    Capture(state),
                    Canonical(requestFailure)
                        ? requestFailure
                        : "durable-equipment-usable-supply-unavailable");
            }
            requestedAny = true;
        }
        DurableFacilityEquipmentSlotSnapshot snapshot = Capture(state);
        return requestedAny ? Applied(snapshot) : Replay(snapshot);
    }

    [GameplayInternalOnly(
        "Resumes every pending common durable-equipment custody drain.",
        "Items recovery entry point before ordinary gameplay ticks")]
    public IReadOnlyList<DurableFacilityEquipmentSlotResult> TryAdvancePending()
    {
        return active.Values
            .Where(value => value.Phase !=
                DurableFacilityEquipmentSlotLifecyclePhase.Active)
            .OrderBy(value => value.Sequence)
            .Select(AdvanceClose)
            .ToArray();
    }

    public bool TryCapture(
        DurableFacilityEquipmentSlotKey key,
        out DurableFacilityEquipmentSlotSnapshot snapshot)
    {
        if (!active.TryGetValue(key, out SlotState state))
        {
            snapshot = null;
            return false;
        }
        snapshot = Capture(state);
        return true;
    }

    public IReadOnlyList<DurableFacilityEquipmentSlotSnapshot> CaptureAll() =>
        bySequence.Values
            .OrderBy(value => value.Sequence)
            .Select(Capture)
            .ToArray();

    public string ParticipantId => RestoreParticipantId;

    public DungeonDurableFacilityEquipmentSaveData CaptureSaveData() => new()
    {
        version = DungeonDurableFacilityEquipmentSaveData.CurrentVersion,
        nextAssignmentSequence = nextAssignmentSequence,
        revision = revision,
        slots = bySequence.Values
            .OrderBy(value => value.Sequence)
            .Select(CapturePersistent)
            .Select(DurableFacilityEquipmentRestoreProjection.Capture)
            .ToList()
    };

    public void PublishRestoreCandidate(
        DurableFacilityEquipmentRestoreCandidate candidate)
    {
        DurableFacilityEquipmentRestoreCandidate required = candidate
            ?? throw new ArgumentNullException(nameof(candidate));
        if (restoreTransactionActive)
        {
            if (stagedRestore != null)
            {
                throw new InvalidOperationException(
                    "A durable facility-equipment restore candidate is already staged.");
            }
            if (!TryPublishAuthorities(
                    required,
                    out string failureReason))
            {
                throw new InvalidOperationException(
                    "Durable facility-equipment restore authority projection failed: "
                    + failureReason);
            }
            stagedRestore = required;
            return;
        }

        DurableFacilityEquipmentRestoreCandidate previous =
            CaptureRestoreImage();
        IReadOnlyList<DurableFacilityEquipmentAdmissionFenceRecord>
            oldFences = fences.CaptureAll();
        if (!TryPublishAuthorities(required, out string directFailure))
        {
            throw new InvalidOperationException(
                "Durable facility-equipment direct restore authority projection failed: "
                + directFailure);
        }
        try
        {
            if (!fences.TryReplaceAll(
                    BuildFenceRecords(required),
                    out string fenceFailure))
            {
                throw new InvalidOperationException(fenceFailure);
            }
            ApplyRestoreImage(required);
        }
        catch
        {
            if (!TryPublishAuthorities(previous, out string rollbackFailure))
            {
                throw new InvalidOperationException(
                    "Durable facility-equipment direct restore authority rollback failed: "
                    + rollbackFailure);
            }
            if (!fences.TryReplaceAll(oldFences, out string fenceRollback))
            {
                throw new InvalidOperationException(
                    "Durable facility-equipment direct restore fence rollback failed: "
                    + fenceRollback);
            }
            throw;
        }
    }

    public void BeginRestoreCandidate()
    {
        if (restoreTransactionActive)
        {
            throw new InvalidOperationException(
                "Durable facility-equipment restore transaction is already active.");
        }
        previousRestore = CaptureRestoreImage();
        previousFences = fences.CaptureAll();
        stagedRestore = null;
        restoreTransactionActive = true;
        restorePublished = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!restoreTransactionActive
            || restorePublished
            || stagedRestore == null)
        {
            throw new InvalidOperationException(
                "Durable facility-equipment restore transaction is not ready to publish.");
        }
        if (!fences.TryReplaceAll(
                BuildFenceRecords(stagedRestore),
                out string failureReason))
        {
            throw new InvalidOperationException(failureReason);
        }
        try
        {
            ApplyRestoreImage(stagedRestore);
        }
        catch
        {
            if (!fences.TryReplaceAll(
                    previousFences,
                    out string rollbackFailure))
            {
                throw new InvalidOperationException(
                    "Durable facility-equipment partial restore fence rollback failed: "
                    + rollbackFailure);
            }
            throw;
        }
        restorePublished = true;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        if (restorePublished && previousRestore != null)
        {
            ApplyRestoreImage(previousRestore);
            if (!fences.TryReplaceAll(
                    previousFences,
                    out string failureReason))
            {
                throw new InvalidOperationException(
                    "Durable facility-equipment restore fence rollback failed: "
                    + failureReason);
            }
        }
        ClearRestoreTransaction();
    }

    public void CompleteRestoreCandidate()
    {
        if (!restoreTransactionActive || !restorePublished)
        {
            throw new InvalidOperationException(
                "Durable facility-equipment restore transaction cannot complete.");
        }
        ClearRestoreTransaction();
    }

    public void DiscardRestoreCandidate()
    {
        if (restorePublished)
            RollbackPublishedRestoreCandidate();
        else
            ClearRestoreTransaction();
    }

    public bool TryPrepareCheckpointGarbageCollection(
        out IDurableFacilityEquipmentSlotCheckpointGcCandidate candidate,
        out string failureReason)
    {
        candidate = null;
        failureReason = string.Empty;
        if (restoreTransactionActive || checkpointGcCandidate != null)
        {
            failureReason =
                "durable-equipment-slot-checkpoint-gc-runtime-busy";
            return false;
        }
        DurableFacilityEquipmentSlotSnapshot[] closed = bySequence.Values
            .Where(value => value.Phase ==
                DurableFacilityEquipmentSlotLifecyclePhase
                    .ClosedAwaitingCheckpointGc)
            .OrderBy(value => value.Sequence)
            .Select(CapturePersistent)
            .ToArray();
        if (closed.Any(value => !value.AuthoritiesRevoked
                || value.Drain == null
                || !value.Drain.OwnerAcknowledged))
        {
            failureReason =
                "durable-equipment-slot-checkpoint-gc-upper-invalid";
            return false;
        }
        Dictionary<long, SlotState> replacement = bySequence
            .Where(pair => pair.Value.Phase !=
                DurableFacilityEquipmentSlotLifecyclePhase
                    .ClosedAwaitingCheckpointGc)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        long nextRevision = revision;
        if (closed.Length > 0 && !TryGetNext(revision, out nextRevision))
        {
            failureReason =
                "durable-equipment-slot-checkpoint-gc-revision-overflow";
            return false;
        }
        checkpointGcCandidate = new CheckpointGcCandidate(
            bySequence,
            replacement,
            revision,
            nextRevision,
            closed);
        candidate = checkpointGcCandidate;
        return true;
    }

    public bool TryPublishCheckpointGarbageCollection(
        IDurableFacilityEquipmentSlotCheckpointGcCandidate candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryRequireCheckpointCandidate(candidate, out CheckpointGcCandidate exact,
                out failureReason))
        {
            return false;
        }
        if (exact.Published)
            return true;
        if (!ReferenceEquals(bySequence, exact.Before)
            || revision != exact.BeforeRevision
            || restoreTransactionActive)
        {
            failureReason =
                "durable-equipment-slot-checkpoint-gc-upper-drift";
            return false;
        }
        bySequence = exact.After;
        revision = exact.AfterRevision;
        exact.Published = true;
        return true;
    }

    public void RollbackCheckpointGarbageCollection(
        IDurableFacilityEquipmentSlotCheckpointGcCandidate candidate)
    {
        if (!TryRequireCheckpointCandidate(candidate, out CheckpointGcCandidate exact,
                out string failureReason))
        {
            throw new InvalidOperationException(failureReason);
        }
        if (exact.Published)
        {
            if (!ReferenceEquals(bySequence, exact.After)
                || revision != exact.AfterRevision)
            {
                throw new InvalidOperationException(
                    "durable-equipment-slot-checkpoint-gc-rollback-drift");
            }
            bySequence = exact.Before;
            revision = exact.BeforeRevision;
            exact.Published = false;
        }
        exact.Completed = true;
        checkpointGcCandidate = null;
    }

    public void CompleteCheckpointGarbageCollection(
        IDurableFacilityEquipmentSlotCheckpointGcCandidate candidate)
    {
        if (!TryRequireCheckpointCandidate(candidate, out CheckpointGcCandidate exact,
                out string failureReason)
            || !exact.Published)
        {
            throw new InvalidOperationException(
                failureReason.Length == 0
                    ? "durable-equipment-slot-checkpoint-gc-not-published"
                    : failureReason);
        }
        exact.Completed = true;
        checkpointGcCandidate = null;
    }

    private DurableFacilityEquipmentRestoreCandidate CaptureRestoreImage() =>
        new(
            nextAssignmentSequence,
            revision,
            bySequence.Values
                .OrderBy(value => value.Sequence)
                .Select(CapturePersistent)
                .ToArray());

    private void ApplyRestoreImage(
        DurableFacilityEquipmentRestoreCandidate candidate)
    {
        Dictionary<DurableFacilityEquipmentSlotKey, SlotState>
            replacementActive = new();
        Dictionary<long, SlotState> replacementBySequence = new();
        foreach (DurableFacilityEquipmentSlotSnapshot snapshot in
                 candidate.Slots)
        {
            SlotState state = new(snapshot);
            replacementBySequence.Add(state.Sequence, state);
            if (state.Phase !=
                DurableFacilityEquipmentSlotLifecyclePhase
                    .ClosedAwaitingCheckpointGc)
            {
                replacementActive.Add(state.Assignment.Key, state);
            }
        }
        active = replacementActive;
        bySequence = replacementBySequence;
        nextAssignmentSequence = candidate.NextAssignmentSequence;
        revision = candidate.Revision;
    }

    private bool TryPublishAuthorities(
        DurableFacilityEquipmentRestoreCandidate candidate,
        out string failureReason)
    {
        DurableFacilityEquipmentSlotSnapshot[] included = candidate.Slots
            .Where(value => !value.AuthoritiesRevoked
                && value.LifecyclePhase !=
                    DurableFacilityEquipmentSlotLifecyclePhase
                        .ClosedAwaitingCheckpointGc)
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        FacilityBufferDestinationClaim[] claims = included
            .Select(value => new FacilityBufferDestinationClaim(
                value.DestinationId,
                value.DropPosition,
                DurableFacilityEquipmentSlotIdentity.AuthorityOwnerDomain,
                value.OwnerOperationId,
                value.OwnerFacilityId.Value,
                FacilityBufferDestinationAnchorKind.LiveFacility,
                FacilityBufferDestinationAdmissionPolicy.ExactGramRequired))
            .ToArray();
        FacilityBufferCapacityProfile[] profiles = included
            .Select(value => new FacilityBufferCapacityProfile(
                value.DestinationId,
                value.DropPosition,
                DurableFacilityEquipmentSlotIdentity.AuthorityOwnerDomain,
                value.OwnerOperationId,
                value.OwnerFacilityId.Value,
                value.Capacity,
                value.SourceAuthorityRevision))
            .ToArray();
        return lifecycle.TryReplaceOwnedAuthorities(
            DurableFacilityEquipmentSlotIdentity.AuthorityOwnerDomain,
            claims,
            profiles,
            out failureReason);
    }

    private static IReadOnlyList<DurableFacilityEquipmentAdmissionFenceRecord>
        BuildFenceRecords(
            DurableFacilityEquipmentRestoreCandidate candidate) =>
        candidate.Slots
            .Where(value => value.LifecyclePhase is
                DurableFacilityEquipmentSlotLifecyclePhase.CloseRequested
                or DurableFacilityEquipmentSlotLifecyclePhase.Draining)
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .Select(value =>
                new DurableFacilityEquipmentAdmissionFenceRecord(
                    new FacilityBufferDestinationAdmissionFenceSubject(
                        value.DestinationId,
                        DurableFacilityEquipmentSlotIdentity
                            .AuthorityOwnerDomain,
                        value.OwnerOperationId,
                        value.OwnerFacilityId.Value),
                    DurableFacilityEquipmentSlotIdentity
                        .BuildDrainParentOperationId(
                            value.Key,
                            value.AssignmentSequence)))
            .ToArray();

    private void ClearRestoreTransaction()
    {
        stagedRestore = null;
        previousRestore = null;
        previousFences = null;
        restoreTransactionActive = false;
        restorePublished = false;
    }

    private DurableFacilityEquipmentSlotResult AdvanceClose(SlotState state)
    {
        FacilityBufferDestinationCustodyDrainSnapshot child = state.Drain;
        if (child == null)
        {
            if (!TryGetNext(revision, out long preparedRevision))
            {
                return Conflict(
                    Capture(state),
                    "durable-equipment-slot-revision-overflow");
            }
            if (!capacityAuthority.TryGetCapacityAuthorityFingerprint(
                    state.DestinationId,
                    state.Assignment.DropPosition,
                    out string authorityFingerprint))
            {
                return Deferred(
                    Capture(state),
                    "durable-equipment-capacity-authority-fingerprint-missing");
            }
            FacilityBufferDestinationCustodyDrainDescriptor descriptor = new(
                DurableFacilityEquipmentSlotIdentity.BuildDrainParentOperationId(
                    state.Assignment.Key,
                    state.Sequence),
                DurableFacilityEquipmentSlotIdentity.BuildDrainStepOperationId(
                    state.Assignment.Key,
                    state.Sequence),
                DurableFacilityEquipmentSlotIdentity.BuildOwnerStableId(
                    state.Assignment.Key,
                    state.Sequence),
                state.Assignment.Key.OwnerSubjectId,
                state.Assignment.OwnerFacilityId.Value,
                state.DestinationId,
                state.Assignment.DropPosition,
                authorityFingerprint);
            FacilityBufferDestinationCustodyDrainResult prepared =
                drains.TryPrepare(descriptor);
            if (!prepared.Succeeded || prepared.Snapshot == null)
                return FromChildFailure(state, prepared);
            state.Drain = prepared.Snapshot;
            state.Phase = DurableFacilityEquipmentSlotLifecyclePhase.Draining;
            revision = preparedRevision;
            child = prepared.Snapshot;
        }

        if (!child.EffectCommitted)
        {
            if (!TryGetNext(revision, out long advancedRevision))
            {
                return Conflict(
                    Capture(state),
                    "durable-equipment-slot-revision-overflow");
            }
            FacilityBufferDestinationCustodyDrainResult advanced =
                drains.TryAdvance(
                    child.StepOperationId,
                    child.RequestFingerprint);
            if (!advanced.Succeeded || advanced.Snapshot == null)
                return FromChildFailure(state, advanced);
            state.Drain = advanced.Snapshot;
            child = advanced.Snapshot;
            revision = advancedRevision;
            if (!child.EffectCommitted)
            {
                return Deferred(
                    Capture(state),
                    "durable-equipment-custody-drain-in-progress");
            }
        }

        if (!state.AuthoritiesRevoked)
        {
            if (!TryGetNext(revision, out long revokedRevision))
            {
                return Conflict(
                    Capture(state),
                    "durable-equipment-slot-revision-overflow");
            }
            if (!TryPublishAuthorities(
                    state.Sequence,
                    out string revokeFailure))
            {
                return Conflict(Capture(state), revokeFailure);
            }
            state.AuthoritiesRevoked = true;
            revision = revokedRevision;
        }

        if (!child.OwnerAcknowledged)
        {
            if (!TryGetNext(revision, out long acknowledgedRevision))
            {
                return Conflict(
                    Capture(state),
                    "durable-equipment-slot-revision-overflow");
            }
            FacilityBufferDestinationCustodyDrainResult acknowledged =
                drains.TryAcknowledge(
                    child.StepOperationId,
                    child.ReceiptFingerprint);
            if (!acknowledged.Succeeded || acknowledged.Snapshot == null)
                return FromChildFailure(state, acknowledged);
            state.Drain = acknowledged.Snapshot;
            child = acknowledged.Snapshot;
            revision = acknowledgedRevision;
        }
        if (!child.OwnerAcknowledged)
        {
            return Deferred(
                Capture(state),
                "durable-equipment-custody-owner-ack-pending");
        }

        if (!TryGetNext(revision, out long closedRevision))
        {
            return Conflict(
                Capture(state),
                "durable-equipment-slot-revision-overflow");
        }
        if (!fences.TryClose(
                CreateFenceSubject(state),
                DurableFacilityEquipmentSlotIdentity.BuildDrainParentOperationId(
                    state.Assignment.Key,
                    state.Sequence),
                out string fenceCloseFailure))
        {
            return Conflict(Capture(state), fenceCloseFailure);
        }

        state.Phase = DurableFacilityEquipmentSlotLifecyclePhase
            .ClosedAwaitingCheckpointGc;
        active.Remove(state.Assignment.Key);
        revision = closedRevision;
        return Applied(Capture(state));
    }

    private bool TryValidateRegisteredAssignment(
        DurableFacilityEquipmentAssignment desired,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (desired == null
            || !policies.TryGetPolicy(
                desired.PolicyId,
                out DurableFacilityEquipmentPolicy policy))
        {
            failureReason = "durable-equipment-policy-unregistered:"
                + (desired?.PolicyId ?? string.Empty);
            return false;
        }
        DurableFacilityEquipmentAssignment canonical = policy.CreateAssignment(
            desired.Key.OwnerSubjectId,
            desired.OwnerFacilityId,
            desired.DropPosition);
        if (!string.Equals(
                DurableFacilityEquipmentFingerprint.CreateAssignment(canonical),
                DurableFacilityEquipmentFingerprint.CreateAssignment(desired),
                StringComparison.Ordinal))
        {
            failureReason = "durable-equipment-assignment-policy-drift:"
                + desired.PolicyId;
            return false;
        }
        return true;
    }

    private bool TryPublishAuthorities(
        long excludedSequence,
        out string failureReason)
    {
        SlotState[] included = active.Values
            .Where(value => !value.AuthoritiesRevoked
                && value.Sequence != excludedSequence)
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
        FacilityBufferDestinationClaim[] claims = included
            .Select(value => new FacilityBufferDestinationClaim(
                value.DestinationId,
                value.Assignment.DropPosition,
                DurableFacilityEquipmentSlotIdentity.AuthorityOwnerDomain,
                value.OwnerOperationId,
                value.Assignment.OwnerFacilityId.Value,
                FacilityBufferDestinationAnchorKind.LiveFacility,
                FacilityBufferDestinationAdmissionPolicy.ExactGramRequired))
            .ToArray();
        FacilityBufferCapacityProfile[] profiles = included
            .Select(value => new FacilityBufferCapacityProfile(
                value.DestinationId,
                value.Assignment.DropPosition,
                DurableFacilityEquipmentSlotIdentity.AuthorityOwnerDomain,
                value.OwnerOperationId,
                value.Assignment.OwnerFacilityId.Value,
                value.Projection.MaximumMass,
                value.Projection.SourceAuthorityRevision))
            .ToArray();
        return lifecycle.TryReplaceOwnedAuthorities(
            DurableFacilityEquipmentSlotIdentity.AuthorityOwnerDomain,
            claims,
            profiles,
            out failureReason);
    }

    private DurableFacilityEquipmentSlotSnapshot Capture(SlotState state)
    {
        RequirementObservation[] observations = ObserveRequirements(state);
        DurableFacilityEquipmentRequirementStatus[] statuses = observations
            .Select(value => new DurableFacilityEquipmentRequirementStatus(
                value.Requirement,
                value.PendingQuantity,
                value.BufferedUsableQuantity))
            .ToArray();
        return new DurableFacilityEquipmentSlotSnapshot(
            state.Assignment,
            state.Sequence,
            state.DestinationId,
            state.OwnerOperationId,
            state.AssignmentFingerprint,
            state.Projection,
            statuses,
            state.Phase,
            state.CloseReasonCode,
            state.Drain,
            state.AuthoritiesRevoked);
    }

    private static DurableFacilityEquipmentSlotSnapshot CapturePersistent(
        SlotState state) => new(
        state.Assignment,
        state.Sequence,
        state.DestinationId,
        state.OwnerOperationId,
        state.AssignmentFingerprint,
        state.Projection,
        state.Assignment.Requirements.Select(value =>
            new DurableFacilityEquipmentRequirementStatus(value, 0, 0)),
        state.Phase,
        state.CloseReasonCode,
        state.Drain,
        state.AuthoritiesRevoked);

    private static bool TryGetNext(long current, out long next)
    {
        try
        {
            next = checked(current + 1L);
            return true;
        }
        catch (OverflowException)
        {
            next = 0L;
            return false;
        }
    }

    private bool TryRequireCheckpointCandidate(
        IDurableFacilityEquipmentSlotCheckpointGcCandidate candidate,
        out CheckpointGcCandidate exact,
        out string failureReason)
    {
        exact = candidate as CheckpointGcCandidate;
        if (exact == null
            || !ReferenceEquals(exact, checkpointGcCandidate)
            || exact.Completed)
        {
            failureReason =
                "durable-equipment-slot-checkpoint-gc-candidate-invalid";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private RequirementObservation[] ObserveRequirements(SlotState state)
    {
        IReadOnlyList<WorldItemStackSnapshot> stacks =
            physical.CaptureDestinationStacks(state.DestinationId)
            ?? Array.Empty<WorldItemStackSnapshot>();
        return state.Assignment.Requirements.Select(requirement =>
        {
            WorldItemStackSnapshot[] matching = stacks
                .Where(value => value != null
                    && string.Equals(
                        value.ItemId,
                        requirement.ItemId.Value,
                        StringComparison.Ordinal))
                .ToArray();
            int assigned = matching.Sum(value => Math.Max(0, value.Quantity));
            int committed = Math.Max(
                0,
                physical.GetCommittedDeliveryQuantity(
                    state.DestinationId,
                    requirement.ItemId));
            int usable = 0;
            int exhausted = 0;
            int incompatible = 0;
            foreach (WorldItemStackSnapshot buffered in matching.Where(value =>
                         value.State == WorldItemStackState.FacilityBuffer))
            {
                DurableFacilityEquipmentUseSubject subject =
                    DurableFacilityEquipmentUseSubjectCapture.Capture(buffered);
                if (!usability.TryEvaluate(
                        state.Assignment.UsabilityPolicyKind,
                        requirement,
                        subject,
                        out DurableFacilityEquipmentUsabilityResult result,
                        out string failureReason))
                {
                    throw new InvalidOperationException(failureReason);
                }
                int quantity = Math.Max(0, buffered.Quantity);
                if (result.IsUsable)
                    usable = checked(usable + quantity);
                else if (result.Disposition ==
                         DurableFacilityEquipmentUsabilityDisposition.Exhausted)
                    exhausted = checked(exhausted + quantity);
                else
                    incompatible = checked(incompatible + quantity);
            }
            int bufferedTotal = matching
                .Where(value => value.State == WorldItemStackState.FacilityBuffer)
                .Sum(value => Math.Max(0, value.Quantity));
            int pending = Math.Max(
                0,
                checked(assigned - bufferedTotal));
            return new RequirementObservation(
                requirement,
                assigned,
                committed,
                pending,
                usable,
                exhausted,
                incompatible);
        }).ToArray();
    }

    private DurableFacilityEquipmentSlotResult FromChildFailure(
        SlotState state,
        FacilityBufferDestinationCustodyDrainResult result) =>
        result.Status == FacilityBufferDestinationCustodyDrainStatus.Deferred
            ? Deferred(
                Capture(state),
                Canonical(result.FailureReason)
                    ? result.FailureReason
                    : "durable-equipment-child-deferred")
            : Conflict(
                Capture(state),
                Canonical(result.FailureReason)
                    ? result.FailureReason
                    : "durable-equipment-child-conflict");

    private static DurableFacilityEquipmentSlotResult Applied(
        DurableFacilityEquipmentSlotSnapshot snapshot) => new(
        DurableFacilityEquipmentSlotStatus.Applied,
        snapshot,
        string.Empty);

    private static DurableFacilityEquipmentSlotResult Replay(
        DurableFacilityEquipmentSlotSnapshot snapshot) => new(
        DurableFacilityEquipmentSlotStatus.Replay,
        snapshot,
        string.Empty);

    private static DurableFacilityEquipmentSlotResult Deferred(
        DurableFacilityEquipmentSlotSnapshot snapshot,
        string reason) => new(
        DurableFacilityEquipmentSlotStatus.Deferred,
        snapshot,
        Canonical(reason) ? reason : "durable-equipment-deferred");

    private static DurableFacilityEquipmentSlotResult Conflict(
        DurableFacilityEquipmentSlotSnapshot snapshot,
        string reason) => new(
        DurableFacilityEquipmentSlotStatus.Conflict,
        snapshot,
        Canonical(reason) ? reason : "durable-equipment-conflict");

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static FacilityBufferDestinationAdmissionFenceSubject
        CreateFenceSubject(SlotState state) => new(
        state.DestinationId,
        DurableFacilityEquipmentSlotIdentity.AuthorityOwnerDomain,
        state.OwnerOperationId,
        state.Assignment.OwnerFacilityId.Value);

    private sealed class SlotState
    {
        internal SlotState(
            DurableFacilityEquipmentAssignment assignment,
            long sequence,
            string assignmentFingerprint,
            DurableFacilityEquipmentCapacityProjection projection)
        {
            Assignment = assignment;
            Sequence = sequence;
            AssignmentFingerprint = assignmentFingerprint;
            Projection = projection;
            DestinationId =
                DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                    assignment.Key,
                    sequence);
            OwnerOperationId =
                DurableFacilityEquipmentSlotIdentity.BuildOwnerOperationId(
                    assignment.Key,
                    sequence);
            Phase = DurableFacilityEquipmentSlotLifecyclePhase.Active;
            CloseReasonCode = string.Empty;
        }

        internal SlotState(DurableFacilityEquipmentSlotSnapshot snapshot)
        {
            DurableFacilityEquipmentSlotSnapshot required = snapshot
                ?? throw new ArgumentNullException(nameof(snapshot));
            Assignment = required.Assignment;
            Sequence = required.AssignmentSequence;
            AssignmentFingerprint = required.AssignmentFingerprint;
            Projection = required.CapacityProjection;
            DestinationId = required.DestinationId;
            OwnerOperationId = required.OwnerOperationId;
            Phase = required.LifecyclePhase;
            CloseReasonCode = required.CloseReasonCode;
            Drain = required.Drain;
            AuthoritiesRevoked = required.AuthoritiesRevoked;
        }

        internal DurableFacilityEquipmentAssignment Assignment { get; }
        internal long Sequence { get; }
        internal string AssignmentFingerprint { get; }
        internal DurableFacilityEquipmentCapacityProjection Projection { get; }
        internal string DestinationId { get; }
        internal string OwnerOperationId { get; }
        internal DurableFacilityEquipmentSlotLifecyclePhase Phase { get; set; }
        internal string CloseReasonCode { get; set; }
        internal FacilityBufferDestinationCustodyDrainSnapshot Drain { get; set; }
        internal bool AuthoritiesRevoked { get; set; }
    }

    private sealed class CheckpointGcCandidate :
        IDurableFacilityEquipmentSlotCheckpointGcCandidate
    {
        internal CheckpointGcCandidate(
            Dictionary<long, SlotState> before,
            Dictionary<long, SlotState> after,
            long beforeRevision,
            long afterRevision,
            IReadOnlyList<DurableFacilityEquipmentSlotSnapshot> closedSlots)
        {
            Before = before ?? throw new ArgumentNullException(nameof(before));
            After = after ?? throw new ArgumentNullException(nameof(after));
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
            ClosedSlots = closedSlots
                ?? throw new ArgumentNullException(nameof(closedSlots));
        }

        internal Dictionary<long, SlotState> Before { get; }
        internal Dictionary<long, SlotState> After { get; }
        internal long BeforeRevision { get; }
        internal long AfterRevision { get; }
        public IReadOnlyList<DurableFacilityEquipmentSlotSnapshot> ClosedSlots
            { get; }
        internal bool Published { get; set; }
        internal bool Completed { get; set; }
    }

    private readonly struct RequirementObservation
    {
        internal RequirementObservation(
            DurableFacilityEquipmentRequirement requirement,
            int assignedStackQuantity,
            int committedDeliveryQuantity,
            int pendingQuantity,
            int bufferedUsableQuantity,
            int exhaustedBufferedQuantity,
            int incompatibleBufferedQuantity)
        {
            Requirement = requirement;
            AssignedStackQuantity = assignedStackQuantity;
            CommittedDeliveryQuantity = committedDeliveryQuantity;
            PendingQuantity = pendingQuantity;
            BufferedUsableQuantity = bufferedUsableQuantity;
            ExhaustedBufferedQuantity = exhaustedBufferedQuantity;
            IncompatibleBufferedQuantity = incompatibleBufferedQuantity;
        }

        internal DurableFacilityEquipmentRequirement Requirement { get; }
        internal int AssignedStackQuantity { get; }
        internal int CommittedDeliveryQuantity { get; }
        internal int PendingQuantity { get; }
        internal int BufferedUsableQuantity { get; }
        internal int ExhaustedBufferedQuantity { get; }
        internal int IncompatibleBufferedQuantity { get; }
    }
}
