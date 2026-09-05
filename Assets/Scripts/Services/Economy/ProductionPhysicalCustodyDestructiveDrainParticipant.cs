using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Final destructive-drain participant for Items-owned output-buffer and
/// in-flight physical custody. It freezes one destination-wide source vector
/// after capacity routing has terminalized, then only projects terminal Items
/// receipts into the upper production journal.
/// </summary>
public sealed class ProductionPhysicalCustodyDestructiveDrainParticipant :
    IProductionFacilityDestructiveDrainParticipant,
    IProductionFacilityDestructiveDrainDurablePrepareParticipant,
    IProductionFacilityDestructiveDrainCheckpointGcParticipant
{
    public const int CurrentContractVersion = 1;

    private static readonly IReadOnlyList<string> Dependencies =
        Array.AsReadOnly(new[]
        {
            ProductionFacilityDestructiveDrainParticipantIds
                .CapacityRoutingOutbox
        });

    private readonly IProductionOutputDestinationLifecycleQuery lifecycle;
    private readonly IProductionPhysicalCustodyDrainPort physical;
    private readonly IProductionAssemblyBridge facilities;
    private CheckpointGcCandidate activeCheckpointGcCandidate;

    public ProductionPhysicalCustodyDestructiveDrainParticipant(
        IProductionOutputDestinationLifecycleQuery lifecycle,
        IProductionPhysicalCustodyDrainPort physical,
        IProductionAssemblyBridge facilities)
    {
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
        this.physical = physical
            ?? throw new ArgumentNullException(nameof(physical));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
    }

    public string ParticipantId =>
        ProductionFacilityDestructiveDrainParticipantIds
            .PhysicalCustodyCarryRecovery;

    public string CheckpointGcParticipantId => ParticipantId;

    public int ContractVersion => CurrentContractVersion;

    public IReadOnlyList<string> DependsOnParticipantIds => Dependencies;

    public ProductionFacilityDestructiveDrainParticipantPlan Prepare(
        ProductionFacilityDestructiveDrainPrepareContext context)
    {
        RequireDestination(context.FacilityId, context.DestinationId);
        ProductionOutputDestinationLifecycleContribution contribution =
            CaptureContribution(context.FacilityId);
        if (!contribution.HasAuthority)
        {
            ProductionFacilityDestructiveDrainOwnerPlan[] emptyOwners =
                Array.Empty<ProductionFacilityDestructiveDrainOwnerPlan>();
            return new ProductionFacilityDestructiveDrainParticipantPlan(
                ParticipantId,
                ContractVersion,
                contribution.DurableSemanticFingerprint,
                CreatePlanFingerprint(
                    contribution.DurableSemanticFingerprint,
                    emptyOwners),
                emptyOwners);
        }
        ProductionFacilityHandle facility = ResolveFacility(context.FacilityId);
        string owner = ProductionFacilityDestructiveDrainOwnerStableIds
            .PhysicalDestination(context.DestinationId.Value);
        string step = ProductionFacilityDestructiveDrainCanonical
            .BuildStepOperationId(context.OperationId, ParticipantId, owner);
        ProductionPhysicalCustodyDrainRequest request = CaptureRequest(
            step,
            owner,
            context.DestinationId,
            facility,
            contribution.DurableSemanticFingerprint);
        ProductionFacilityDestructiveDrainOwnerPlan ownerPlan = new(
            owner,
            ProductionFacilityDestructiveDrainDisposition.Terminalize,
            string.Empty,
            request.RequestFingerprint);
        ProductionFacilityDestructiveDrainOwnerPlan[] owners = { ownerPlan };
        return new ProductionFacilityDestructiveDrainParticipantPlan(
            ParticipantId,
            ContractVersion,
            contribution.DurableSemanticFingerprint,
            CreatePlanFingerprint(
                contribution.DurableSemanticFingerprint,
                owners),
            owners);
    }

    [GameplayInternalOnly(
        "Persists the exact Items custody source vector only after the upper destructive-drain owner is durable.",
        "Production facility destructive-drain runtime only")]
    public bool TryPrepareDurable(
        ProductionFacilityDestructiveDrainStepContext context,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryValidateStepContext(context, out failureReason))
            return false;

        ProductionOutputDestinationLifecycleContribution contribution;
        ProductionFacilityHandle facility;
        try
        {
            contribution = CaptureContribution(context.FacilityId);
            facility = ResolveFacility(context.FacilityId);
        }
        catch (Exception exception)
        {
            failureReason =
                "production-physical-custody-durable-prepare-capture-failed:"
                + exception.GetType().Name;
            return false;
        }
        if (!string.Equals(
                contribution.DurableSemanticFingerprint,
                context.ExpectedDurableContributionFingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "production-physical-custody-durable-contribution-drift";
            return false;
        }

        ProductionOutputDestinationId destination =
            ProductionOutputDestinationId.FromFacility(context.FacilityId);
        string captureFailure;
        ProductionPhysicalCustodyDrainRequest request;
        try
        {
            if (!physical.TryCaptureRequest(
                    context.Owner.stepOperationId,
                    context.Owner.ownerStableId,
                    destination.Value,
                    facility.Position.x,
                    facility.Position.y,
                    context.ExpectedDurableContributionFingerprint,
                    out request,
                    out captureFailure))
            {
                failureReason = captureFailure;
                return false;
            }
        }
        catch (Exception exception)
        {
            failureReason =
                "production-physical-custody-durable-request-capture-failed:"
                + exception.GetType().Name;
            return false;
        }
        if (!IsExactRequest(
                request,
                context.Owner.stepOperationId,
                context.Owner.ownerStableId,
                destination.Value,
                facility,
                context.ExpectedDurableContributionFingerprint)
            || !string.Equals(
                request.RequestFingerprint,
                context.Owner.requestFingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "production-physical-custody-durable-prepare-plan-drift";
            return false;
        }

        ProductionPhysicalCustodyDrainResult prepared;
        try
        {
            prepared = physical.TryPrepare(request);
        }
        catch (Exception exception)
        {
            failureReason =
                "production-physical-custody-durable-prepare-failed:"
                + exception.GetType().Name;
            return false;
        }
        if (prepared.Status is ProductionPhysicalCustodyDrainStatus.Deferred
            or ProductionPhysicalCustodyDrainStatus.Conflict)
        {
            failureReason = string.IsNullOrEmpty(prepared.FailureReason)
                ? "production-physical-custody-durable-prepare-rejected"
                : prepared.FailureReason;
            return false;
        }
        if (!HasValidOptionalTerminalFields(prepared)
            || !physical.TryCapture(
                request.StepOperationId,
                out ProductionPhysicalCustodyDrainSaveData stored)
            || !IsExactProducerRequest(stored, request)
            || HasCanonicalTerminalFields(
                    prepared.CommitId,
                    prepared.ReceiptFingerprint)
                && (!IsProducerTerminal(stored.phase)
                    || !HasValidTerminalState(stored)
                    || !string.Equals(
                        stored.commitId,
                        prepared.CommitId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        stored.receiptFingerprint,
                        prepared.ReceiptFingerprint,
                        StringComparison.Ordinal)))
        {
            failureReason =
                "production-physical-custody-durable-prepare-evidence-invalid";
            return false;
        }
        return true;
    }

    [GameplayInternalOnly(
        "Advances one replay-safe Items physical-custody release step and exposes only terminal producer receipts.",
        "Production facility destructive-drain runtime only")]
    public ProductionFacilityDestructiveDrainStepResult TryCommit(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (!TryValidateStepContext(context, out _)
            || context.Owner.phase !=
                ProductionFacilityDestructiveDrainStepPhase.Planned
            || !string.IsNullOrEmpty(context.Owner.commitId)
            || !string.IsNullOrEmpty(context.Owner.receiptFingerprint))
        {
            return UpperConflict(current);
        }

        ProductionPhysicalCustodyDrainResult result;
        try
        {
            result = physical.TryCommit(
                context.Owner.stepOperationId,
                context.Owner.requestFingerprint);
        }
        catch
        {
            return UpperConflict(CaptureCurrentFingerprint(context.FacilityId));
        }
        return ToUpperStep(
            context,
            result,
            requireJournalReceiptMatch: false);
    }

    [GameplayInternalOnly(
        "Acknowledges the Items producer receipt only when it exactly matches the journal-owned terminal receipt.",
        "Production facility destructive-drain runtime only")]
    public ProductionFacilityDestructiveDrainStepResult TryAcknowledge(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (!TryValidateStepContext(context, out _)
            || context.Owner.phase != ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck
            || !HasCanonicalTerminalFields(
                context.Owner.commitId,
                context.Owner.receiptFingerprint))
        {
            return UpperConflict(current);
        }

        ProductionPhysicalCustodyDrainResult result;
        try
        {
            result = physical.TryAcknowledge(
                context.Owner.stepOperationId,
                context.Owner.receiptFingerprint);
        }
        catch
        {
            return UpperConflict(CaptureCurrentFingerprint(context.FacilityId));
        }
        return ToUpperStep(
            context,
            result,
            requireJournalReceiptMatch: true);
    }

    public ProductionFacilityDestructiveDrainCheckpointGcResult
        PrepareCheckpointGarbageCollection(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData> entries,
            out IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        candidate = null;
        if (activeCheckpointGcCandidate != null)
            return GcFailure(context, "physical-gc-already-prepared");
        if (physical is not IProductionPhysicalCustodyDrainCheckpointGcPort gc)
        {
            return GcFailure(
                context,
                "physical-gc-port-missing",
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .MissingParticipant);
        }

        ProductionFacilityDestructiveDrainEntrySaveData[] source =
            (entries ?? Array.Empty<ProductionFacilityDestructiveDrainEntrySaveData>())
            .ToArray();
        if (source.Any(entry => entry == null)
            || source.Select(entry => entry.operationId)
                .Distinct(StringComparer.Ordinal).Count() != source.Length)
        {
            return GcFailure(context, "physical-gc-entries-invalid");
        }

        List<ProductionPhysicalCustodyDrainSaveData> rows = new();
        foreach (ProductionFacilityDestructiveDrainEntrySaveData entry in source)
        {
            ProductionFacilityDestructiveDrainParticipantSaveData[] matches =
                (entry.participants
                    ?? new List<ProductionFacilityDestructiveDrainParticipantSaveData>())
                .Where(value => value != null && string.Equals(
                    value.participantId,
                    ParticipantId,
                    StringComparison.Ordinal))
                .ToArray();
            if (entry.phase != ProductionFacilityDestructiveDrainPhase
                    .WorldRemovedAwaitingCheckpointGc
                || matches.Length != 1)
            {
                return GcFailure(context, "physical-gc-journal-conflict");
            }
            foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in
                     matches[0].owners
                     ?? new List<ProductionFacilityDestructiveDrainOwnerSaveData>())
            {
                if (owner == null
                    || owner.phase != ProductionFacilityDestructiveDrainStepPhase
                        .OwnerAcknowledged
                    || !physical.TryCapture(
                        owner.stepOperationId,
                        out ProductionPhysicalCustodyDrainSaveData row)
                    || row.phase != ProductionPhysicalCustodyDrainPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc
                    || !string.Equals(
                        row.receiptFingerprint,
                        owner.receiptFingerprint,
                        StringComparison.Ordinal))
                {
                    return GcFailure(
                        context,
                        "physical-gc-row-conflict",
                        ProductionFacilityDestructiveDrainCheckpointGcReason
                            .LiveAuthorityChanged);
                }
                rows.Add(row);
            }
        }

        if (!gc.TryPrepareCheckpointGarbageCollection(
                rows,
                out IProductionPhysicalCustodyDrainCheckpointGcCandidate lower,
                out string failureReason))
        {
            return GcFailure(context, failureReason);
        }
        activeCheckpointGcCandidate = new CheckpointGcCandidate(
            context,
            source.Select(entry => entry.operationId).ToArray(),
            gc,
            lower);
        candidate = activeCheckpointGcCandidate;
        return GcApplied(context, source.Length);
    }

    public ProductionFacilityDestructiveDrainCheckpointGcResult
        PublishCheckpointGarbageCollection(
            IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        exact.PublishAttempted = true;
        if (!exact.Port.TryPublishCheckpointGarbageCollection(
                exact.LowerCandidate,
                out string failureReason))
        {
            return GcFailure(
                exact.Context,
                failureReason,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .ParticipantPublishFailed);
        }
        exact.Published = true;
        return GcApplied(exact.Context, exact.OperationIds.Count);
    }

    public void RollbackCheckpointGarbageCollection(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (!exact.PublishAttempted)
            return;
        exact.Port.RollbackCheckpointGarbageCollection(exact.LowerCandidate);
        exact.PublishAttempted = false;
        exact.Published = false;
    }

    public void CompleteCheckpointGarbageCollection(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        exact.Port.CompleteCheckpointGarbageCollection(exact.LowerCandidate);
        exact.Completed = true;
        activeCheckpointGcCandidate = null;
    }

    public ProductionFacilityDestructiveDrainRecoveryResult Recover(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (!TryValidateStepContext(context, out _))
            return RecoveryConflict(current);
        if (!physical.TryCapture(
                context.Owner.stepOperationId,
                out ProductionPhysicalCustodyDrainSaveData state))
        {
            if (context.Owner.phase ==
                ProductionFacilityDestructiveDrainStepPhase.Planned)
            {
                return new ProductionFacilityDestructiveDrainRecoveryResult(
                    ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit,
                    UpperDeferred(current));
            }
            if (context.Owner.phase ==
                    ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged
                && HasCanonicalTerminalFields(
                    context.Owner.commitId,
                    context.Owner.receiptFingerprint))
            {
                return new ProductionFacilityDestructiveDrainRecoveryResult(
                    ProductionFacilityDestructiveDrainRecoveryAction
                        .AlreadyAcknowledged,
                    UpperReplay(
                        context.Owner.commitId,
                        context.Owner.receiptFingerprint,
                        current));
            }
            return RecoveryConflict(current);
        }

        ProductionFacilityHandle facility;
        try
        {
            facility = ResolveFacility(context.FacilityId);
        }
        catch
        {
            return RecoveryConflict(current);
        }
        if (!IsExactProducerState(state, context, facility))
            return RecoveryConflict(current);

        bool producerTerminal = IsProducerTerminal(state.phase);
        if (producerTerminal
                ? !HasValidTerminalState(state)
                : !HasNoTerminalState(state))
            return RecoveryConflict(current);
        bool journalTerminal = context.Owner.phase is
            ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck
            or ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged;
        if (journalTerminal
            && (!producerTerminal
                || !string.Equals(
                    state.commitId,
                    context.Owner.commitId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    state.receiptFingerprint,
                    context.Owner.receiptFingerprint,
                    StringComparison.Ordinal)))
        {
            return RecoveryConflict(current);
        }

        switch (context.Owner.phase)
        {
            case ProductionFacilityDestructiveDrainStepPhase.Planned:
                return producerTerminal
                    ? new ProductionFacilityDestructiveDrainRecoveryResult(
                        ProductionFacilityDestructiveDrainRecoveryAction
                            .ResumeCommit,
                        UpperReplay(
                            state.commitId,
                            state.receiptFingerprint,
                            current))
                    : new ProductionFacilityDestructiveDrainRecoveryResult(
                        ProductionFacilityDestructiveDrainRecoveryAction
                            .ResumeCommit,
                        UpperDeferred(current));

            case ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck:
                if (state.phase == ProductionPhysicalCustodyDrainPhase
                        .EffectCommittedAwaitingOwnerAck)
                {
                    return new ProductionFacilityDestructiveDrainRecoveryResult(
                        ProductionFacilityDestructiveDrainRecoveryAction
                            .ResumeAcknowledge,
                        UpperReplay(
                            state.commitId,
                            state.receiptFingerprint,
                            current));
                }
                if (state.phase == ProductionPhysicalCustodyDrainPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc)
                {
                    return new ProductionFacilityDestructiveDrainRecoveryResult(
                        ProductionFacilityDestructiveDrainRecoveryAction
                            .AlreadyAcknowledged,
                        UpperReplay(
                            state.commitId,
                            state.receiptFingerprint,
                            current));
                }
                return RecoveryConflict(current);

            case ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged:
                return state.phase == ProductionPhysicalCustodyDrainPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc
                    ? new ProductionFacilityDestructiveDrainRecoveryResult(
                        ProductionFacilityDestructiveDrainRecoveryAction
                            .AlreadyAcknowledged,
                        UpperReplay(
                            state.commitId,
                            state.receiptFingerprint,
                            current))
                    : RecoveryConflict(current);

            default:
                return RecoveryConflict(current);
        }
    }

    private ProductionPhysicalCustodyDrainRequest CaptureRequest(
        string stepOperationId,
        string ownerStableId,
        ProductionOutputDestinationId destinationId,
        ProductionFacilityHandle facility,
        string sourceOwnershipFingerprint)
    {
        if (!physical.TryCaptureRequest(
                stepOperationId,
                ownerStableId,
                destinationId.Value,
                facility.Position.x,
                facility.Position.y,
                sourceOwnershipFingerprint,
                out ProductionPhysicalCustodyDrainRequest request,
                out string failureReason))
        {
            throw new InvalidOperationException(failureReason);
        }
        if (!IsExactRequest(
                request,
                stepOperationId,
                ownerStableId,
                destinationId.Value,
                facility,
                sourceOwnershipFingerprint))
        {
            throw new InvalidOperationException(
                "production-physical-custody-captured-request-invalid");
        }
        return request;
    }

    private ProductionOutputDestinationLifecycleContribution
        CaptureContribution(BuildingInstanceId facilityId)
    {
        ProductionOutputDestinationLifecycleSnapshot snapshot =
            lifecycle.Capture(facilityId);
        ProductionOutputDestinationLifecycleContribution[] matches = snapshot
            .Contributions.Where(value => value != null
                && string.Equals(
                    value.ContributorId,
                    ParticipantId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "production-physical-custody-lifecycle-contribution-missing");
        }
        return matches[0];
    }

    private string CaptureCurrentFingerprint(BuildingInstanceId facilityId) =>
        CaptureContribution(facilityId).DurableSemanticFingerprint;

    private ProductionFacilityHandle ResolveFacility(
        BuildingInstanceId facilityId)
    {
        ProductionFacilityHandle[] matches = (facilities.Facilities
                ?? Array.Empty<ProductionFacilityHandle>())
            .Where(value => value != null
                && !value.IsDestroyed
                && value.InstanceId.Equals(facilityId))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "production-physical-custody-live-facility-resolution-conflict:"
                + facilityId.Value + ":"
                + matches.Length.ToString(CultureInfo.InvariantCulture));
        }
        return matches[0];
    }

    private ProductionFacilityDestructiveDrainStepResult ToUpperStep(
        ProductionFacilityDestructiveDrainStepContext context,
        ProductionPhysicalCustodyDrainResult result,
        bool requireJournalReceiptMatch)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (result.Status == ProductionPhysicalCustodyDrainStatus.Deferred)
        {
            return string.IsNullOrEmpty(result.CommitId)
                && string.IsNullOrEmpty(result.ReceiptFingerprint)
                ? UpperDeferred(current)
                : UpperConflict(current);
        }
        if (result.Status == ProductionPhysicalCustodyDrainStatus.Conflict)
            return UpperConflict(current);
        if (result.Status is not ProductionPhysicalCustodyDrainStatus.Applied
            and not ProductionPhysicalCustodyDrainStatus.Replay)
        {
            return UpperConflict(current);
        }

        ProductionPhysicalCustodyDrainSaveData state;
        ProductionFacilityHandle facility;
        try
        {
            facility = ResolveFacility(context.FacilityId);
        }
        catch
        {
            return UpperConflict(current);
        }
        if (!physical.TryCapture(context.Owner.stepOperationId, out state)
            || !IsExactProducerState(state, context, facility))
        {
            return UpperConflict(current);
        }

        bool noTerminalFields = string.IsNullOrEmpty(result.CommitId)
            && string.IsNullOrEmpty(result.ReceiptFingerprint);
        if (noTerminalFields)
        {
            return IsProducerTerminal(state.phase)
                || !HasNoTerminalState(state)
                ? UpperConflict(current)
                : UpperDeferred(current);
        }
        if (!HasCanonicalTerminalFields(
                result.CommitId,
                result.ReceiptFingerprint)
            || requireJournalReceiptMatch
                && (!string.Equals(
                        result.CommitId,
                        context.Owner.commitId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        result.ReceiptFingerprint,
                        context.Owner.receiptFingerprint,
                        StringComparison.Ordinal))
            || !IsProducerTerminal(state.phase)
            || !HasValidTerminalState(state)
            || !string.Equals(
                state.requestFingerprint,
                context.Owner.requestFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                state.commitId,
                result.CommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                state.receiptFingerprint,
                result.ReceiptFingerprint,
                StringComparison.Ordinal))
        {
            return UpperConflict(current);
        }

        return new ProductionFacilityDestructiveDrainStepResult(
            result.Status == ProductionPhysicalCustodyDrainStatus.Applied
                ? ProductionFacilityDestructiveDrainStepStatus.Applied
                : ProductionFacilityDestructiveDrainStepStatus.Replay,
            result.CommitId,
            result.ReceiptFingerprint,
            current);
    }

    private bool TryValidateStepContext(
        ProductionFacilityDestructiveDrainStepContext context,
        out string failureReason)
    {
        failureReason = string.Empty;
        ProductionOutputDestinationId destination =
            ProductionOutputDestinationId.FromFacility(context.FacilityId);
        string owner = ProductionFacilityDestructiveDrainOwnerStableIds
            .PhysicalDestination(destination.Value);
        string step = ProductionFacilityDestructiveDrainCanonical
            .BuildStepOperationId(context.OperationId, ParticipantId, owner);
        if (!string.Equals(context.ParticipantId, ParticipantId,
                StringComparison.Ordinal)
            || !string.Equals(context.Owner.ownerStableId, owner,
                StringComparison.Ordinal)
            || context.Owner.disposition !=
                ProductionFacilityDestructiveDrainDisposition.Terminalize
            || !string.IsNullOrEmpty(context.Owner.targetDestinationId)
            || !string.Equals(context.Owner.stepOperationId, step,
                StringComparison.Ordinal)
            || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                context.Owner.requestFingerprint))
        {
            failureReason =
                "production-physical-custody-step-context-invalid";
            return false;
        }
        return true;
    }

    private static bool IsExactRequest(
        ProductionPhysicalCustodyDrainRequest request,
        string stepOperationId,
        string ownerStableId,
        string destinationId,
        ProductionFacilityHandle facility,
        string sourceOwnershipFingerprint)
    {
        if (request == null
            || !string.Equals(request.StepOperationId, stepOperationId,
                StringComparison.Ordinal)
            || !string.Equals(request.OwnerStableId, ownerStableId,
                StringComparison.Ordinal)
            || !string.Equals(request.SourceDestinationId, destinationId,
                StringComparison.Ordinal)
            || request.OwnerGridX != facility.Position.x
            || request.OwnerGridY != facility.Position.y
            || !string.Equals(
                request.SourceOwnershipFingerprint,
                sourceOwnershipFingerprint,
                StringComparison.Ordinal)
            || request.InputQuantity <= 0
            || request.InputMassGrams <= 0L)
        {
            return false;
        }
        string expected = ProductionPhysicalCustodyDrainFingerprint.CreateRequest(
            request.StepOperationId,
            request.OwnerStableId,
            request.SourceDestinationId,
            request.OwnerGridX,
            request.OwnerGridY,
            request.SourceOwnershipFingerprint,
            request.SourceStackIds,
            request.SourceActorIds,
            request.SourceHaulIntentOperationIds,
            request.InputQuantity,
            request.InputMassGrams);
        return string.Equals(
            expected,
            request.RequestFingerprint,
            StringComparison.Ordinal);
    }

    private static bool IsExactProducerRequest(
        ProductionPhysicalCustodyDrainSaveData state,
        ProductionPhysicalCustodyDrainRequest request) =>
        state != null
        && string.Equals(state.stepOperationId, request.StepOperationId,
            StringComparison.Ordinal)
        && string.Equals(state.ownerStableId, request.OwnerStableId,
            StringComparison.Ordinal)
        && string.Equals(state.sourceDestinationId,
            request.SourceDestinationId, StringComparison.Ordinal)
        && state.ownerGridX == request.OwnerGridX
        && state.ownerGridY == request.OwnerGridY
        && string.Equals(state.requestFingerprint,
            request.RequestFingerprint, StringComparison.Ordinal)
        && string.Equals(state.sourceOwnershipFingerprint,
            request.SourceOwnershipFingerprint, StringComparison.Ordinal)
        && SequenceEqual(state.sourceStackIds, request.SourceStackIds)
        && SequenceEqual(state.sourceActorIds, request.SourceActorIds)
        && SequenceEqual(
            state.sourceHaulIntentOperationIds,
            request.SourceHaulIntentOperationIds)
        && state.inputQuantity == request.InputQuantity
        && state.inputMassGrams == request.InputMassGrams;

    private static bool IsExactProducerState(
        ProductionPhysicalCustodyDrainSaveData state,
        ProductionFacilityDestructiveDrainStepContext context,
        ProductionFacilityHandle facility)
    {
        ProductionOutputDestinationId destination =
            ProductionOutputDestinationId.FromFacility(context.FacilityId);
        if (state == null
            || !string.Equals(state.stepOperationId,
                context.Owner.stepOperationId, StringComparison.Ordinal)
            || !string.Equals(state.ownerStableId,
                context.Owner.ownerStableId, StringComparison.Ordinal)
            || !string.Equals(state.sourceDestinationId,
                destination.Value, StringComparison.Ordinal)
            || state.ownerGridX != facility.Position.x
            || state.ownerGridY != facility.Position.y
            || !string.Equals(state.requestFingerprint,
                context.Owner.requestFingerprint, StringComparison.Ordinal)
            // The stored source fingerprint is the immutable pre-effect
            // authority captured into requestFingerprint. Once the producer
            // commits, the journal intentionally advances its expected
            // contribution to the post-effect value before acknowledgement.
            // Comparing those two different epochs makes every legitimate
            // terminal replay conflict. The planned epoch still requires the
            // exact live source fingerprint; terminal epochs are authenticated
            // by the canonical request and receipt checks below.
            || context.Owner.phase ==
                ProductionFacilityDestructiveDrainStepPhase.Planned
            && !string.Equals(state.sourceOwnershipFingerprint,
                context.ExpectedDurableContributionFingerprint,
                StringComparison.Ordinal)
            || state.inputQuantity <= 0
            || state.inputMassGrams <= 0L)
        {
            return false;
        }
        string expected = ProductionPhysicalCustodyDrainFingerprint.CreateRequest(
            state.stepOperationId,
            state.ownerStableId,
            state.sourceDestinationId,
            state.ownerGridX,
            state.ownerGridY,
            state.sourceOwnershipFingerprint,
            state.sourceStackIds,
            state.sourceActorIds,
            state.sourceHaulIntentOperationIds,
            state.inputQuantity,
            state.inputMassGrams);
        return string.Equals(
            expected,
            state.requestFingerprint,
            StringComparison.Ordinal);
    }

    private static bool HasValidOptionalTerminalFields(
        ProductionPhysicalCustodyDrainResult result) =>
        string.IsNullOrEmpty(result.CommitId)
            && string.IsNullOrEmpty(result.ReceiptFingerprint)
        || HasCanonicalTerminalFields(
            result.CommitId,
            result.ReceiptFingerprint);

    private static bool HasValidTerminalState(
        ProductionPhysicalCustodyDrainSaveData state) =>
        state != null
        && ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
            state.resultFingerprint)
        && HasCanonicalTerminalFields(
            state.commitId,
            state.receiptFingerprint);

    private static bool HasNoTerminalState(
        ProductionPhysicalCustodyDrainSaveData state) =>
        state != null
        && string.IsNullOrEmpty(state.resultFingerprint)
        && string.IsNullOrEmpty(state.commitId)
        && string.IsNullOrEmpty(state.receiptFingerprint);

    private static bool HasCanonicalTerminalFields(
        string commitId,
        string receiptFingerprint) =>
        ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(commitId)
        && ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
            receiptFingerprint);

    private static bool IsProducerTerminal(
        ProductionPhysicalCustodyDrainPhase phase) =>
        phase is ProductionPhysicalCustodyDrainPhase
                .EffectCommittedAwaitingOwnerAck
            or ProductionPhysicalCustodyDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc;

    private static bool SequenceEqual(
        IEnumerable<string> left,
        IEnumerable<string> right) => (left ?? Array.Empty<string>())
        .SequenceEqual(right ?? Array.Empty<string>(), StringComparer.Ordinal);

    private string CreatePlanFingerprint(
        string contributionFingerprint,
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerPlan> owners)
    {
        string canonical = string.Join(
            "\n",
            new[]
            {
                "production-physical-custody-participant-plan@1",
                ParticipantId,
                ContractVersion.ToString(CultureInfo.InvariantCulture),
                contributionFingerprint
            }.Concat((owners
                    ?? Array.Empty<
                        ProductionFacilityDestructiveDrainOwnerPlan>())
                .OrderBy(value => value.OwnerStableId, StringComparer.Ordinal)
                .Select(value => string.Join(
                    "|",
                    value.OwnerStableId,
                    ((int)value.Disposition).ToString(
                        CultureInfo.InvariantCulture),
                    value.TargetDestinationId,
                    value.RequestFingerprint))));
        return ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
            canonical);
    }

    private static void RequireDestination(
        BuildingInstanceId facilityId,
        ProductionOutputDestinationId destinationId)
    {
        if (!destinationId.Equals(
                ProductionOutputDestinationId.FromFacility(facilityId)))
        {
            throw new InvalidOperationException(
                "production-physical-custody-destination-facility-mismatch");
        }
    }

    private static ProductionFacilityDestructiveDrainStepResult UpperDeferred(
        string current) => new(
        ProductionFacilityDestructiveDrainStepStatus.Deferred,
        string.Empty,
        string.Empty,
        current);

    private static ProductionFacilityDestructiveDrainStepResult UpperReplay(
        string commitId,
        string receiptFingerprint,
        string current) => new(
        ProductionFacilityDestructiveDrainStepStatus.Replay,
        commitId,
        receiptFingerprint,
        current);

    private static ProductionFacilityDestructiveDrainStepResult UpperConflict(
        string current) => new(
        ProductionFacilityDestructiveDrainStepStatus.Conflict,
        string.Empty,
        string.Empty,
        current);

    private static ProductionFacilityDestructiveDrainRecoveryResult
        RecoveryConflict(string current) => new(
        ProductionFacilityDestructiveDrainRecoveryAction.Conflict,
        UpperConflict(current));

    private CheckpointGcCandidate RequireCheckpointGcCandidate(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        if (candidate is not CheckpointGcCandidate exact
            || exact.Completed
            || !ReferenceEquals(exact, activeCheckpointGcCandidate)
            || !string.Equals(
                exact.ParticipantId,
                ParticipantId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "production-physical-custody-checkpoint-gc-candidate-conflict");
        }
        return exact;
    }

    private static ProductionFacilityDestructiveDrainCheckpointGcResult GcApplied(
        ProductionFacilityDestructiveDrainCheckpointGcContext context,
        int operationCount) => new(
        ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied,
        ProductionFacilityDestructiveDrainCheckpointGcReason.None,
        context.CheckpointSequence,
        string.Empty,
        operationCount);

    private static ProductionFacilityDestructiveDrainCheckpointGcResult GcFailure(
        ProductionFacilityDestructiveDrainCheckpointGcContext context,
        string message,
        ProductionFacilityDestructiveDrainCheckpointGcReason reason =
            ProductionFacilityDestructiveDrainCheckpointGcReason
                .ParticipantPrepareFailed) => new(
        ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
        reason,
        context.CheckpointSequence,
        message ?? string.Empty);

    private sealed class CheckpointGcCandidate :
        IProductionFacilityDestructiveDrainCheckpointGcCandidate
    {
        internal CheckpointGcCandidate(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            IReadOnlyList<string> operationIds,
            IProductionPhysicalCustodyDrainCheckpointGcPort port,
            IProductionPhysicalCustodyDrainCheckpointGcCandidate lowerCandidate)
        {
            Context = context;
            OperationIds = operationIds
                ?? throw new ArgumentNullException(nameof(operationIds));
            Port = port ?? throw new ArgumentNullException(nameof(port));
            LowerCandidate = lowerCandidate
                ?? throw new ArgumentNullException(nameof(lowerCandidate));
        }

        public string ParticipantId =>
            ProductionFacilityDestructiveDrainParticipantIds
                .PhysicalCustodyCarryRecovery;
        public long CheckpointSequence => Context.CheckpointSequence;
        public string SerializedByteDigest => Context.SerializedByteDigest;
        public IReadOnlyList<string> OperationIds { get; }
        internal ProductionFacilityDestructiveDrainCheckpointGcContext Context
        { get; }
        internal IProductionPhysicalCustodyDrainCheckpointGcPort Port { get; }
        internal IProductionPhysicalCustodyDrainCheckpointGcCandidate LowerCandidate
        { get; }
        internal bool PublishAttempted { get; set; }
        internal bool Published { get; set; }
        internal bool Completed { get; set; }
    }
}
