using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// First destructive-drain participant for generic production bills. It
/// freezes the live bill and its Items-owned input-destination custody source
/// without mutation, persists both child and producer only after the upper
/// journal owner exists, and exposes only the producer's terminal receipt.
/// </summary>
public sealed class ProductionGenericBillTerminalDestructiveDrainParticipant :
    IProductionFacilityDestructiveDrainParticipant,
    IProductionFacilityDestructiveDrainDurablePrepareParticipant
{
    public const int CurrentContractVersion = 1;

    private static readonly IReadOnlyList<string> Dependencies =
        Array.AsReadOnly(Array.Empty<string>());

    private const string ChildStepSuffix = ":input-destination-custody";

    private readonly IProductionOutputDestinationLifecycleQuery lifecycle;
    private readonly IProductionFacilityDestructiveDrainPreparedOutputQuery
        billOwners;
    private readonly IProductionGenericBillTerminalDrainQuery producerQuery;
    private readonly IProductionGenericBillTerminalDrainCommand producer;
    private readonly IProductionInputDestinationCustodyDrainService inputDrain;
    private readonly IProductionAssemblyBridge facilities;

    internal ProductionGenericBillTerminalDestructiveDrainParticipant(
        IProductionOutputDestinationLifecycleQuery lifecycle,
        IProductionFacilityDestructiveDrainPreparedOutputQuery billOwners,
        ProductionGenericBillTerminalDrainOutbox producer,
        IProductionInputDestinationCustodyDrainService inputDrain,
        IProductionAssemblyBridge facilities)
        : this(
            lifecycle,
            billOwners,
            producer,
            producer,
            inputDrain,
            facilities)
    {
    }

    public ProductionGenericBillTerminalDestructiveDrainParticipant(
        IProductionOutputDestinationLifecycleQuery lifecycle,
        IProductionFacilityDestructiveDrainPreparedOutputQuery billOwners,
        IProductionGenericBillTerminalDrainQuery producerQuery,
        IProductionGenericBillTerminalDrainCommand producer,
        IProductionInputDestinationCustodyDrainService inputDrain,
        IProductionAssemblyBridge facilities)
    {
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
        this.billOwners = billOwners
            ?? throw new ArgumentNullException(nameof(billOwners));
        this.producerQuery = producerQuery
            ?? throw new ArgumentNullException(nameof(producerQuery));
        this.producer = producer
            ?? throw new ArgumentNullException(nameof(producer));
        this.inputDrain = inputDrain
            ?? throw new ArgumentNullException(nameof(inputDrain));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
    }

    public string ParticipantId =>
        ProductionFacilityDestructiveDrainParticipantIds.GenericProductionBills;

    public int ContractVersion => CurrentContractVersion;

    public IReadOnlyList<string> DependsOnParticipantIds => Dependencies;

    public ProductionFacilityDestructiveDrainParticipantPlan Prepare(
        ProductionFacilityDestructiveDrainPrepareContext context)
    {
        RequireDestination(context.FacilityId, context.DestinationId);
        ProductionOutputDestinationLifecycleContribution contribution =
            CaptureContribution(context.FacilityId);
        ProductionFacilityHandle facility = ResolveFacility(context.FacilityId);
        ProductionFacilityDestructiveDrainPreparedOutputOwner[] sources =
            CaptureOwnedBills(context.FacilityId);
        if (contribution.HasAuthority != (sources.Length > 0))
        {
            throw new InvalidOperationException(
                "production-generic-terminal-lifecycle-owner-count-conflict");
        }

        ProductionFacilityDestructiveDrainOwnerPlan[] owners = sources
            .Select(source => CaptureRequest(
                context.OperationId,
                facility,
                source,
                out _))
            .Select(request => new ProductionFacilityDestructiveDrainOwnerPlan(
                request.OwnerStableId,
                ProductionFacilityDestructiveDrainDisposition.Terminalize,
                string.Empty,
                request.RequestFingerprint))
            .OrderBy(value => value.OwnerStableId, StringComparer.Ordinal)
            .ToArray();
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
        "Persists the frozen Items child request and generic-bill producer only after the upper destructive-drain owner is durable.",
        "Production facility destructive-drain runtime only")]
    public bool TryPrepareDurable(
        ProductionFacilityDestructiveDrainStepContext context,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryValidateStepContext(context, out failureReason))
            return false;

        ProductionGenericBillTerminalDrainRequest request;
        ProductionInputDestinationCustodyDrainRequest childRequest;
        try
        {
            ProductionOutputDestinationLifecycleContribution contribution =
                CaptureContribution(context.FacilityId);
            if (!string.Equals(
                    contribution.DurableSemanticFingerprint,
                    context.ExpectedDurableContributionFingerprint,
                    StringComparison.Ordinal))
            {
                failureReason =
                    "production-generic-terminal-durable-contribution-drift";
                return false;
            }

            ProductionFacilityHandle facility = ResolveFacility(context.FacilityId);
            ProductionFacilityDestructiveDrainPreparedOutputOwner source =
                CaptureOwnedBills(context.FacilityId).SingleOrDefault(value =>
                    string.Equals(
                        ProductionFacilityDestructiveDrainOwnerStableIds
                            .GenericBill(value.BillId.Value),
                        context.Owner.ownerStableId,
                        StringComparison.Ordinal));
            if (source.BillId.IsValid == false)
            {
                failureReason =
                    "production-generic-terminal-durable-bill-owner-missing";
                return false;
            }
            request = CaptureRequest(
                context.OperationId,
                facility,
                source,
                out childRequest);
        }
        catch (Exception exception)
        {
            failureReason =
                "production-generic-terminal-durable-capture-failed:"
                + exception.GetType().Name;
            return false;
        }

        if (!string.Equals(request.StepOperationId,
                context.Owner.stepOperationId, StringComparison.Ordinal)
            || !string.Equals(request.OwnerStableId,
                context.Owner.ownerStableId, StringComparison.Ordinal)
            || !string.Equals(request.RequestFingerprint,
                context.Owner.requestFingerprint, StringComparison.Ordinal))
        {
            failureReason =
                "production-generic-terminal-durable-prepare-plan-drift";
            return false;
        }

        if (!string.Equals(childRequest.StepOperationId,
                request.InputDestinationDrainStepOperationId,
                StringComparison.Ordinal)
            || !string.Equals(childRequest.RequestFingerprint,
                request.InputDestinationDrainRequestFingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "production-generic-terminal-durable-child-plan-drift";
            return false;
        }

        ProductionGenericBillTerminalDrainResult prepared;
        try
        {
            prepared = producer.TryPrepare(request);
        }
        catch (Exception exception)
        {
            failureReason =
                "production-generic-terminal-durable-producer-prepare-failed:"
                + exception.GetType().Name;
            return false;
        }
        if (prepared.Status is ProductionGenericBillTerminalDrainStatus.Deferred
            or ProductionGenericBillTerminalDrainStatus.Conflict)
        {
            failureReason = string.IsNullOrEmpty(prepared.FailureReason)
                ? "production-generic-terminal-durable-producer-prepare-rejected"
                : prepared.FailureReason;
            return false;
        }
        if (!producerQuery.TryCapture(
                request.StepOperationId,
                out ProductionGenericBillTerminalDrainSaveData producerState)
            || !IsExactProducerRequest(producerState, request))
        {
            failureReason =
                "production-generic-terminal-durable-producer-evidence-invalid";
            return false;
        }

        ProductionInputDestinationCustodyDrainResult childPrepared;
        try
        {
            childPrepared = inputDrain.TryPrepare(childRequest);
        }
        catch (Exception exception)
        {
            failureReason =
                "production-generic-terminal-durable-child-prepare-failed:"
                + exception.GetType().Name;
            return false;
        }
        if (childPrepared.Status is
                ProductionInputDestinationCustodyDrainStatus.Deferred
            or ProductionInputDestinationCustodyDrainStatus.Conflict)
        {
            failureReason = string.IsNullOrEmpty(childPrepared.FailureReason)
                ? "production-generic-terminal-durable-child-prepare-rejected"
                : childPrepared.FailureReason;
            return false;
        }
        if (!inputDrain.TryCapture(
                childRequest.StepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData childState)
            || !IsExactChildState(childState, childRequest))
        {
            failureReason =
                "production-generic-terminal-durable-child-evidence-invalid";
            return false;
        }
        return true;
    }

    [GameplayInternalOnly(
        "Synchronously advances the immediate-recovery Items child and then the generic-bill terminal producer to a typed boundary.",
        "Production facility destructive-drain runtime only")]
    public ProductionFacilityDestructiveDrainStepResult TryCommit(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (!TryValidateStepContext(context, out _)
            || context.Owner.phase !=
                ProductionFacilityDestructiveDrainStepPhase.Planned
            || !string.IsNullOrEmpty(context.Owner.commitId)
            || !string.IsNullOrEmpty(context.Owner.receiptFingerprint)
            || !inputDrain.RequiresImmediateRecoveryBeforeGameplayTick
            || !producerQuery.TryCapture(
                context.Owner.stepOperationId,
                out ProductionGenericBillTerminalDrainSaveData state)
            || !IsExactProducerState(state, context))
        {
            return UpperConflict(current);
        }

        ProductionFacilityDestructiveDrainStepResult childBoundary =
            DriveChildToTerminal(context, state);
        if (childBoundary.Status is
                ProductionFacilityDestructiveDrainStepStatus.Deferred
            or ProductionFacilityDestructiveDrainStepStatus.Conflict)
        {
            return childBoundary;
        }

        int remainingProducerSteps = 4;
        while (remainingProducerSteps-- > 0)
        {
            ProductionGenericBillTerminalDrainResult result;
            try
            {
                result = producer.TryProgress(context.Owner.stepOperationId);
            }
            catch
            {
                return UpperConflict(CaptureCurrentFingerprint(context.FacilityId));
            }
            ProductionFacilityDestructiveDrainStepResult mapped = ToUpperStep(
                context,
                result,
                requireJournalReceiptMatch: false);
            if (mapped.Status !=
                ProductionFacilityDestructiveDrainStepStatus.Deferred)
            {
                return mapped;
            }
            if (result.Status == ProductionGenericBillTerminalDrainStatus.Deferred)
                return mapped;
        }
        return UpperConflict(CaptureCurrentFingerprint(context.FacilityId));
    }

    [GameplayInternalOnly(
        "Acknowledges the producer receipt only when it exactly matches the journal-owned terminal receipt.",
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

        ProductionGenericBillTerminalDrainResult result;
        try
        {
            result = producer.TryAcknowledge(
                context.Owner.stepOperationId,
                context.Owner.receiptFingerprint);
        }
        catch
        {
            return UpperConflict(CaptureCurrentFingerprint(context.FacilityId));
        }
        return ToUpperStep(context, result, requireJournalReceiptMatch: true);
    }

    public ProductionFacilityDestructiveDrainRecoveryResult Recover(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (!TryValidateStepContext(context, out _))
            return RecoveryConflict(current);
        if (!producerQuery.TryCapture(
                context.Owner.stepOperationId,
                out ProductionGenericBillTerminalDrainSaveData state))
        {
            if (inputDrain.TryCapture(
                    context.Owner.stepOperationId + ChildStepSuffix,
                    out _))
            {
                return RecoveryConflict(current);
            }
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
        if (!IsExactProducerState(state, context))
            return RecoveryConflict(current);

        bool producerTerminal = IsProducerTerminal(state.phase);
        bool journalTerminal = context.Owner.phase is
            ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck
            or ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged;
        if (journalTerminal
            && (!producerTerminal
                || !string.Equals(state.commitId,
                    context.Owner.commitId, StringComparison.Ordinal)
                || !string.Equals(state.receiptFingerprint,
                    context.Owner.receiptFingerprint, StringComparison.Ordinal)))
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
                if (state.phase == ProductionGenericBillTerminalDrainPhase
                        .BillTerminalCommittedAwaitingOwnerAcknowledgement)
                {
                    return new ProductionFacilityDestructiveDrainRecoveryResult(
                        ProductionFacilityDestructiveDrainRecoveryAction
                            .ResumeAcknowledge,
                        UpperReplay(
                            state.commitId,
                            state.receiptFingerprint,
                            current));
                }
                if (state.phase == ProductionGenericBillTerminalDrainPhase
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
                return state.phase == ProductionGenericBillTerminalDrainPhase
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

    private ProductionFacilityDestructiveDrainStepResult DriveChildToTerminal(
        ProductionFacilityDestructiveDrainStepContext context,
        ProductionGenericBillTerminalDrainSaveData producerState)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (!inputDrain.TryCapture(
                producerState.inputDestinationDrainStepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData child)
            || !IsExactChildState(child, producerState))
        {
            return UpperConflict(current);
        }

        int remainingSteps = checked(
            (child.sourceActors?.Count ?? 0)
            + (child.sourceOperations?.Count ?? 0)
            + 4);
        while (remainingSteps-- > 0)
        {
            ProductionInputDestinationCustodyDrainResult result;
            try
            {
                result = inputDrain.TryCommit(
                    child.stepOperationId,
                    child.requestFingerprint);
            }
            catch
            {
                return UpperConflict(CaptureCurrentFingerprint(context.FacilityId));
            }
            if (result.Status ==
                ProductionInputDestinationCustodyDrainStatus.Conflict)
            {
                return UpperConflict(CaptureCurrentFingerprint(context.FacilityId));
            }
            if (result.Status ==
                ProductionInputDestinationCustodyDrainStatus.Deferred)
            {
                return string.IsNullOrEmpty(result.CommitId)
                    && string.IsNullOrEmpty(result.ReceiptFingerprint)
                    ? UpperDeferred(CaptureCurrentFingerprint(context.FacilityId))
                    : UpperConflict(CaptureCurrentFingerprint(context.FacilityId));
            }
            if (!inputDrain.TryCapture(child.stepOperationId, out child)
                || !IsExactChildState(child, producerState))
            {
                return UpperConflict(CaptureCurrentFingerprint(context.FacilityId));
            }

            bool hasReceipt = HasCanonicalTerminalFields(
                result.CommitId,
                result.ReceiptFingerprint);
            if (hasReceipt)
            {
                return IsChildTerminal(child.phase)
                    && string.Equals(child.commitId,
                        result.CommitId, StringComparison.Ordinal)
                    && string.Equals(child.receiptFingerprint,
                        result.ReceiptFingerprint, StringComparison.Ordinal)
                    ? UpperReplay(
                        result.CommitId,
                        result.ReceiptFingerprint,
                        CaptureCurrentFingerprint(context.FacilityId))
                    : UpperConflict(CaptureCurrentFingerprint(context.FacilityId));
            }
            if (!string.IsNullOrEmpty(result.CommitId)
                || !string.IsNullOrEmpty(result.ReceiptFingerprint))
            {
                return UpperConflict(CaptureCurrentFingerprint(context.FacilityId));
            }
        }
        return UpperConflict(CaptureCurrentFingerprint(context.FacilityId));
    }

    private ProductionGenericBillTerminalDrainRequest CaptureRequest(
        ProductionFacilityDestructiveDrainOperationId operationId,
        ProductionFacilityHandle facility,
        ProductionFacilityDestructiveDrainPreparedOutputOwner source,
        out ProductionInputDestinationCustodyDrainRequest childRequest)
    {
        childRequest = null;
        string failureReason = string.Empty;
        if (!source.BillId.IsValid
            || !source.FacilityId.Equals(facility.InstanceId))
        {
            throw new InvalidOperationException(
                "production-generic-terminal-source-owner-invalid");
        }
        if (!producerQuery.TryCaptureLiveBill(
                source.BillId,
                out ProductionBillSaveData bill,
                out string billFingerprint,
                out failureReason))
        {
            throw new InvalidOperationException(string.IsNullOrEmpty(failureReason)
                ? "production-generic-terminal-source-owner-invalid"
                : failureReason);
        }
        if (!Matches(source, bill)
            || !string.Equals(
                billFingerprint,
                ProductionGenericBillTerminalDrainCanonical
                    .CreateSourceBillFingerprint(bill),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "production-generic-terminal-live-source-conflict");
        }

        string owner = ProductionFacilityDestructiveDrainOwnerStableIds
            .GenericBill(source.BillId.Value);
        string step = ProductionFacilityDestructiveDrainCanonical
            .BuildStepOperationId(operationId, ParticipantId, owner);
        childRequest = CaptureChildRequest(
            operationId,
            facility,
            bill,
            owner,
            step);
        string requestFingerprint = ProductionGenericBillTerminalDrainCanonical
            .CreateRequestFingerprint(
                operationId.Value,
                step,
                owner,
                bill,
                childRequest.StepOperationId,
                childRequest.RequestFingerprint);
        return new ProductionGenericBillTerminalDrainRequest(
            operationId.Value,
            step,
            owner,
            bill,
            childRequest.StepOperationId,
            childRequest.RequestFingerprint,
            requestFingerprint);
    }

    private ProductionInputDestinationCustodyDrainRequest CaptureChildRequest(
        ProductionFacilityDestructiveDrainOperationId operationId,
        ProductionFacilityHandle facility,
        ProductionBillSaveData bill,
        string ownerStableId,
        string producerStepOperationId)
    {
        string sourceBillFingerprint = ProductionGenericBillTerminalDrainCanonical
            .CreateSourceBillFingerprint(bill);
        string childStepOperationId = producerStepOperationId + ChildStepSuffix;
        if (!inputDrain.TryCaptureRequest(
                operationId.Value,
                childStepOperationId,
                ownerStableId,
                bill.billId,
                facility.InstanceId.Value,
                bill.materialDestinationId,
                facility.Position,
                sourceBillFingerprint,
                out ProductionInputDestinationCustodyDrainRequest request,
                out string failureReason))
        {
            throw new InvalidOperationException(failureReason);
        }
        if (!IsExactChildRequest(
                request,
                operationId.Value,
                childStepOperationId,
                ownerStableId,
                bill,
                facility,
                sourceBillFingerprint))
        {
            throw new InvalidOperationException(
                "production-generic-terminal-captured-child-request-invalid");
        }
        return request;
    }

    private ProductionFacilityDestructiveDrainPreparedOutputOwner[]
        CaptureOwnedBills(BuildingInstanceId facilityId)
    {
        ProductionFacilityDestructiveDrainPreparedOutputOwner[] values =
            (billOwners.CapturePreparedOutputOwners(facilityId)
                ?? Array.Empty<
                    ProductionFacilityDestructiveDrainPreparedOutputOwner>())
            .OrderBy(value => value.BillId.Value, StringComparer.Ordinal)
            .ToArray();
        if (values.Any(value => !value.FacilityId.Equals(facilityId))
            || values.Select(value => value.BillId.Value)
                .Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            throw new InvalidOperationException(
                "production-generic-terminal-owned-bills-invalid-or-duplicate");
        }
        return values;
    }

    private ProductionOutputDestinationLifecycleContribution
        CaptureContribution(BuildingInstanceId facilityId)
    {
        ProductionOutputDestinationLifecycleContribution[] matches = lifecycle
            .Capture(facilityId).Contributions
            .Where(value => value != null
                && string.Equals(value.ContributorId,
                    ParticipantId, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "production-generic-terminal-lifecycle-contribution-missing");
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
                "production-generic-terminal-live-facility-resolution-conflict:"
                + facilityId.Value + ":"
                + matches.Length.ToString(CultureInfo.InvariantCulture));
        }
        return matches[0];
    }

    private bool TryValidateStepContext(
        ProductionFacilityDestructiveDrainStepContext context,
        out string failureReason)
    {
        failureReason = string.Empty;
        string ownerPrefix = "bill:";
        if (!string.Equals(context.ParticipantId, ParticipantId,
                StringComparison.Ordinal)
            || !context.Owner.ownerStableId.StartsWith(
                ownerPrefix, StringComparison.Ordinal)
            || context.Owner.disposition !=
                ProductionFacilityDestructiveDrainDisposition.Terminalize
            || !string.IsNullOrEmpty(context.Owner.targetDestinationId)
            || !string.Equals(
                context.Owner.stepOperationId,
                ProductionFacilityDestructiveDrainCanonical.BuildStepOperationId(
                    context.OperationId,
                    ParticipantId,
                    context.Owner.ownerStableId),
                StringComparison.Ordinal)
            || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                context.Owner.requestFingerprint))
        {
            failureReason = "production-generic-terminal-step-context-invalid";
            return false;
        }
        return true;
    }

    private ProductionFacilityDestructiveDrainStepResult ToUpperStep(
        ProductionFacilityDestructiveDrainStepContext context,
        ProductionGenericBillTerminalDrainResult result,
        bool requireJournalReceiptMatch)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (result.Status == ProductionGenericBillTerminalDrainStatus.Conflict)
            return UpperConflict(current);
        if (result.Status == ProductionGenericBillTerminalDrainStatus.Deferred)
        {
            return string.IsNullOrEmpty(result.CommitId)
                && string.IsNullOrEmpty(result.ReceiptFingerprint)
                ? UpperDeferred(current)
                : UpperConflict(current);
        }
        if (result.Status is not ProductionGenericBillTerminalDrainStatus.Applied
            and not ProductionGenericBillTerminalDrainStatus.Replay
            || !producerQuery.TryCapture(
                context.Owner.stepOperationId,
                out ProductionGenericBillTerminalDrainSaveData state)
            || !IsExactProducerState(state, context))
        {
            return UpperConflict(current);
        }

        bool hasReceipt = HasCanonicalTerminalFields(
            result.CommitId,
            result.ReceiptFingerprint);
        if (!hasReceipt)
        {
            return string.IsNullOrEmpty(result.CommitId)
                && string.IsNullOrEmpty(result.ReceiptFingerprint)
                && !IsProducerTerminal(state.phase)
                ? UpperDeferred(current)
                : UpperConflict(current);
        }
        if (!IsProducerTerminal(state.phase)
            || !string.Equals(state.commitId,
                result.CommitId, StringComparison.Ordinal)
            || !string.Equals(state.receiptFingerprint,
                result.ReceiptFingerprint, StringComparison.Ordinal)
            || requireJournalReceiptMatch
                && (!string.Equals(result.CommitId,
                        context.Owner.commitId, StringComparison.Ordinal)
                    || !string.Equals(result.ReceiptFingerprint,
                        context.Owner.receiptFingerprint,
                        StringComparison.Ordinal)))
        {
            return UpperConflict(current);
        }
        return new ProductionFacilityDestructiveDrainStepResult(
            result.Status == ProductionGenericBillTerminalDrainStatus.Applied
                ? ProductionFacilityDestructiveDrainStepStatus.Applied
                : ProductionFacilityDestructiveDrainStepStatus.Replay,
            result.CommitId,
            result.ReceiptFingerprint,
            current);
    }

    private static bool Matches(
        ProductionFacilityDestructiveDrainPreparedOutputOwner owner,
        ProductionBillSaveData bill) => bill != null
        && string.Equals(bill.billId, owner.BillId.Value,
            StringComparison.Ordinal)
        && string.Equals(bill.buildingInstanceId, owner.FacilityId.Value,
            StringComparison.Ordinal)
        && string.Equals(bill.recipeId, owner.RecipeId,
            StringComparison.Ordinal)
        && bill.cycleSequence == owner.CycleSequence
        && string.Equals(bill.outputDestinationId, owner.DestinationId,
            StringComparison.Ordinal);

    private static bool IsExactChildRequest(
        ProductionInputDestinationCustodyDrainRequest request,
        string parentOperationId,
        string childStepOperationId,
        string ownerStableId,
        ProductionBillSaveData bill,
        ProductionFacilityHandle facility,
        string sourceBillFingerprint)
    {
        if (request == null
            || !string.Equals(request.ParentOperationId,
                parentOperationId, StringComparison.Ordinal)
            || !string.Equals(request.StepOperationId,
                childStepOperationId, StringComparison.Ordinal)
            || !string.Equals(request.OwnerStableId,
                ownerStableId, StringComparison.Ordinal)
            || !string.Equals(request.BillId, bill.billId,
                StringComparison.Ordinal)
            || !string.Equals(request.FacilityId,
                facility.InstanceId.Value, StringComparison.Ordinal)
            || !string.Equals(request.SourceDestinationId,
                bill.materialDestinationId, StringComparison.Ordinal)
            || request.OwnerGridX != facility.Position.x
            || request.OwnerGridY != facility.Position.y
            || !string.Equals(request.SourceClaimFingerprint,
                sourceBillFingerprint, StringComparison.Ordinal))
        {
            return false;
        }
        string expected = ProductionInputDestinationCustodyDrainFingerprint
            .CreateRequest(
                request.ParentOperationId,
                request.StepOperationId,
                request.OwnerStableId,
                request.BillId,
                request.FacilityId,
                request.SourceDestinationId,
                request.OwnerGridX,
                request.OwnerGridY,
                request.SourceClaimFingerprint,
                request.SourceOwnershipFingerprint,
                request.SourceStacks,
                request.SourceOperations,
                request.SourceActors,
                request.InputQuantity,
                request.InputMassGrams);
        return string.Equals(expected,
            request.RequestFingerprint, StringComparison.Ordinal);
    }

    private static bool IsExactChildState(
        ProductionInputDestinationCustodyDrainSaveData state,
        ProductionInputDestinationCustodyDrainRequest request) =>
        state != null
        && ProductionInputDestinationCustodyDrainContract.IsValidSave(state)
        && string.Equals(state.parentOperationId,
            request.ParentOperationId, StringComparison.Ordinal)
        && string.Equals(state.stepOperationId,
            request.StepOperationId, StringComparison.Ordinal)
        && string.Equals(state.ownerStableId,
            request.OwnerStableId, StringComparison.Ordinal)
        && string.Equals(state.requestFingerprint,
            request.RequestFingerprint, StringComparison.Ordinal)
        && string.Equals(state.sourceClaimFingerprint,
            request.SourceClaimFingerprint, StringComparison.Ordinal)
        && string.Equals(state.sourceOwnershipFingerprint,
            request.SourceOwnershipFingerprint, StringComparison.Ordinal);

    private static bool IsExactChildState(
        ProductionInputDestinationCustodyDrainSaveData child,
        ProductionGenericBillTerminalDrainSaveData producerState) =>
        child != null
        && ProductionInputDestinationCustodyDrainContract.IsValidSave(child)
        && string.Equals(child.parentOperationId,
            producerState.parentOperationId, StringComparison.Ordinal)
        && string.Equals(child.stepOperationId,
            producerState.inputDestinationDrainStepOperationId,
            StringComparison.Ordinal)
        && string.Equals(child.ownerStableId,
            producerState.ownerStableId, StringComparison.Ordinal)
        && string.Equals(child.billId,
            producerState.billId, StringComparison.Ordinal)
        && string.Equals(child.facilityId,
            producerState.facilityId, StringComparison.Ordinal)
        && string.Equals(child.sourceDestinationId,
            producerState.inputDestinationId, StringComparison.Ordinal)
        && string.Equals(child.requestFingerprint,
            producerState.inputDestinationDrainRequestFingerprint,
            StringComparison.Ordinal);

    private static bool IsExactProducerRequest(
        ProductionGenericBillTerminalDrainSaveData state,
        ProductionGenericBillTerminalDrainRequest request) => state != null
        && ProductionGenericBillTerminalDrainCanonical.IsValidSave(state)
        && string.Equals(state.parentOperationId,
            request.ParentOperationId, StringComparison.Ordinal)
        && string.Equals(state.stepOperationId,
            request.StepOperationId, StringComparison.Ordinal)
        && string.Equals(state.ownerStableId,
            request.OwnerStableId, StringComparison.Ordinal)
        && string.Equals(state.requestFingerprint,
            request.RequestFingerprint, StringComparison.Ordinal)
        && string.Equals(state.inputDestinationDrainStepOperationId,
            request.InputDestinationDrainStepOperationId,
            StringComparison.Ordinal)
        && string.Equals(state.inputDestinationDrainRequestFingerprint,
            request.InputDestinationDrainRequestFingerprint,
            StringComparison.Ordinal)
        && string.Equals(state.sourceBillFingerprint,
            ProductionGenericBillTerminalDrainCanonical
                .CreateSourceBillFingerprint(request.SourceBill),
            StringComparison.Ordinal);

    private static bool IsExactProducerState(
        ProductionGenericBillTerminalDrainSaveData state,
        ProductionFacilityDestructiveDrainStepContext context) => state != null
        && ProductionGenericBillTerminalDrainCanonical.IsValidSave(state)
        && string.Equals(state.parentOperationId,
            context.OperationId.Value, StringComparison.Ordinal)
        && string.Equals(state.stepOperationId,
            context.Owner.stepOperationId, StringComparison.Ordinal)
        && string.Equals(state.ownerStableId,
            context.Owner.ownerStableId, StringComparison.Ordinal)
        && string.Equals(state.facilityId,
            context.FacilityId.Value, StringComparison.Ordinal)
        && string.Equals(state.requestFingerprint,
            context.Owner.requestFingerprint, StringComparison.Ordinal)
        && string.Equals(state.inputDestinationDrainStepOperationId,
            context.Owner.stepOperationId + ChildStepSuffix,
            StringComparison.Ordinal);

    private string CreatePlanFingerprint(
        string contributionFingerprint,
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerPlan> owners)
    {
        string canonical = string.Join(
            "\n",
            new[]
            {
                "production-generic-bill-terminal-participant-plan@1",
                ParticipantId,
                ContractVersion.ToString(CultureInfo.InvariantCulture),
                contributionFingerprint
            }.Concat((owners
                    ?? Array.Empty<ProductionFacilityDestructiveDrainOwnerPlan>())
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
                "production-generic-terminal-destination-facility-mismatch");
        }
    }

    private static bool HasCanonicalTerminalFields(
        string commitId,
        string receiptFingerprint) =>
        ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(commitId)
        && ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
            receiptFingerprint);

    private static bool IsProducerTerminal(
        ProductionGenericBillTerminalDrainPhase phase) => phase is
            ProductionGenericBillTerminalDrainPhase
                .BillTerminalCommittedAwaitingOwnerAcknowledgement
            or ProductionGenericBillTerminalDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc;

    private static bool IsChildTerminal(
        ProductionInputDestinationCustodyDrainPhase phase) => phase is
            ProductionInputDestinationCustodyDrainPhase
                .EffectCommittedAwaitingBillAck
            or ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc;

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
}
