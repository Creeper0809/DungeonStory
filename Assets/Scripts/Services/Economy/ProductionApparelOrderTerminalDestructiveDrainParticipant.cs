using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Upper destructive-journal participant for apparel work orders. Prepare is
/// read-only; durable prepare persists the producer before any lease or
/// terminal effect; commit exposes only the producer's exact terminal receipt.
/// </summary>
public sealed class ProductionApparelOrderTerminalDestructiveDrainParticipant :
    IProductionFacilityDestructiveDrainParticipant,
    IProductionFacilityDestructiveDrainDurablePrepareParticipant,
    IProductionFacilityDestructiveDrainCheckpointGcParticipant
{
    public const int CurrentContractVersion = 1;

    private static readonly IReadOnlyList<string> Dependencies =
        Array.AsReadOnly(Array.Empty<string>());

    private readonly IProductionOutputDestinationLifecycleQuery lifecycle;
    private readonly IApparelWorkOrderQuery orders;
    private readonly IApparelLeaseAuthorityQuery leases;
    private readonly IProductionApparelOrderTerminalDrainQuery producerQuery;
    private readonly IProductionApparelOrderTerminalDrainCommand producer;
    private readonly IProductionApparelOrderTerminalDrainCheckpointGcPort
        checkpointGc;

    internal ProductionApparelOrderTerminalDestructiveDrainParticipant(
        IProductionOutputDestinationLifecycleQuery lifecycle,
        IApparelWorkOrderQuery orders,
        IApparelLeaseAuthorityQuery leases,
        ProductionApparelOrderTerminalDrainOutbox producer)
        : this(lifecycle, orders, leases, producer, producer)
    {
    }

    public ProductionApparelOrderTerminalDestructiveDrainParticipant(
        IProductionOutputDestinationLifecycleQuery lifecycle,
        IApparelWorkOrderQuery orders,
        IApparelLeaseAuthorityQuery leases,
        IProductionApparelOrderTerminalDrainQuery producerQuery,
        IProductionApparelOrderTerminalDrainCommand producer)
    {
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
        this.orders = orders
            ?? throw new ArgumentNullException(nameof(orders));
        this.leases = leases
            ?? throw new ArgumentNullException(nameof(leases));
        this.producerQuery = producerQuery
            ?? throw new ArgumentNullException(nameof(producerQuery));
        this.producer = producer
            ?? throw new ArgumentNullException(nameof(producer));
        checkpointGc = producer as
            IProductionApparelOrderTerminalDrainCheckpointGcPort;
    }

    public string ParticipantId =>
        ProductionFacilityDestructiveDrainParticipantIds.ApparelWorkOrders;

    public string CheckpointGcParticipantId => ParticipantId;

    public int ContractVersion => CurrentContractVersion;

    public IReadOnlyList<string> DependsOnParticipantIds => Dependencies;

    public ProductionFacilityDestructiveDrainParticipantPlan Prepare(
        ProductionFacilityDestructiveDrainPrepareContext context)
    {
        RequireDestination(context.FacilityId, context.DestinationId);
        ProductionOutputDestinationLifecycleContribution contribution =
            CaptureContribution(context.FacilityId);
        ApparelWorkOrderSaveData[] sources = CaptureOwnedOrders(
            context.FacilityId);
        if (contribution.HasAuthority != (sources.Length > 0)
            || contribution.ActiveRecordCount != sources.Length)
        {
            throw new InvalidOperationException(
                "production-apparel-terminal-lifecycle-owner-count-conflict");
        }

        ProductionFacilityDestructiveDrainOwnerPlan[] owners = sources
            .Select(source => CaptureRequest(context.OperationId, source))
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
                contribution.DurableSemanticFingerprint, owners),
            owners);
    }

    [GameplayInternalOnly(
        "Persists the frozen apparel producer only after the upper destructive journal owner is durable.",
        "Production facility destructive drain runtime only")]
    public bool TryPrepareDurable(
        ProductionFacilityDestructiveDrainStepContext context,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryValidateStepContext(context, out failureReason))
            return false;

        ProductionApparelOrderTerminalDrainRequest request;
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
                    "production-apparel-terminal-durable-contribution-drift";
                return false;
            }
            string orderId = RequireOrderId(context.Owner.ownerStableId);
            ApparelWorkOrderSaveData source = CaptureOwnedOrders(
                    context.FacilityId)
                .SingleOrDefault(value => string.Equals(
                    value.orderId, orderId, StringComparison.Ordinal));
            if (source == null)
            {
                // Producer-ahead is the only legal missing-live retry. The
                // existing frozen record itself is then the durable evidence.
                if (!producerQuery.TryCapture(
                        context.Owner.stepOperationId,
                        out ProductionApparelOrderTerminalDrainSaveData ahead)
                    || !IsExactProducerState(ahead, context))
                {
                    failureReason =
                        "production-apparel-terminal-durable-order-owner-missing";
                    return false;
                }
                return true;
            }
            request = CaptureRequest(context.OperationId, source);
        }
        catch (Exception exception)
        {
            failureReason =
                "production-apparel-terminal-durable-capture-failed:"
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
                "production-apparel-terminal-durable-prepare-plan-drift";
            return false;
        }

        ProductionApparelOrderTerminalDrainResult prepared;
        try
        {
            prepared = producer.TryPrepare(request);
        }
        catch (Exception exception)
        {
            failureReason =
                "production-apparel-terminal-durable-producer-prepare-failed:"
                + exception.GetType().Name;
            return false;
        }
        if (prepared.Status is ProductionApparelOrderTerminalDrainStatus.Deferred
            or ProductionApparelOrderTerminalDrainStatus.Conflict)
        {
            failureReason = string.IsNullOrEmpty(prepared.FailureReason)
                ? "production-apparel-terminal-durable-producer-prepare-rejected"
                : prepared.FailureReason;
            return false;
        }
        if (!producerQuery.TryCapture(
                request.StepOperationId,
                out ProductionApparelOrderTerminalDrainSaveData state)
            || !IsExactProducerRequest(state, request))
        {
            failureReason =
                "production-apparel-terminal-durable-producer-evidence-invalid";
            return false;
        }
        return true;
    }

    [GameplayInternalOnly(
        "Synchronously advances the exact apparel producer to its terminal receipt boundary.",
        "Production facility destructive drain runtime only")]
    public ProductionFacilityDestructiveDrainStepResult TryCommit(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (!TryValidateStepContext(context, out _)
            || context.Owner.phase !=
                ProductionFacilityDestructiveDrainStepPhase.Planned
            || !string.IsNullOrEmpty(context.Owner.commitId)
            || !string.IsNullOrEmpty(context.Owner.receiptFingerprint)
            || !producerQuery.TryCapture(
                context.Owner.stepOperationId,
                out ProductionApparelOrderTerminalDrainSaveData state)
            || !IsExactProducerState(state, context))
        {
            return UpperConflict(current);
        }

        int remainingSteps = 3;
        while (remainingSteps-- > 0)
        {
            ProductionApparelOrderTerminalDrainResult result;
            try
            {
                result = producer.TryProgress(context.Owner.stepOperationId);
            }
            catch
            {
                return UpperConflict(CaptureCurrentFingerprint(
                    context.FacilityId));
            }
            ProductionFacilityDestructiveDrainStepResult mapped = ToUpperStep(
                context, result, requireJournalReceiptMatch: false);
            if (mapped.Status !=
                ProductionFacilityDestructiveDrainStepStatus.Deferred)
                return mapped;
            if (result.Status == ProductionApparelOrderTerminalDrainStatus.Deferred)
                return mapped;
        }
        return UpperConflict(CaptureCurrentFingerprint(context.FacilityId));
    }

    [GameplayInternalOnly(
        "Acknowledges only the exact apparel producer receipt already held by the upper journal.",
        "Production facility destructive drain runtime only")]
    public ProductionFacilityDestructiveDrainStepResult TryAcknowledge(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (!TryValidateStepContext(context, out _)
            || context.Owner.phase != ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck
            || !HasCanonicalTerminalFields(
                context.Owner.commitId, context.Owner.receiptFingerprint))
        {
            return UpperConflict(current);
        }
        ProductionApparelOrderTerminalDrainResult result;
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
                out ProductionApparelOrderTerminalDrainSaveData state))
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
                return new ProductionFacilityDestructiveDrainRecoveryResult(
                    ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit,
                    producerTerminal
                        ? UpperReplay(
                            state.commitId,
                            state.receiptFingerprint,
                            current)
                        : UpperDeferred(current));
            case ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck:
                if (state.phase == ProductionApparelOrderTerminalDrainPhase
                        .SourceOrderTerminalCommittedAwaitingOwnerAcknowledgement)
                {
                    return new ProductionFacilityDestructiveDrainRecoveryResult(
                        ProductionFacilityDestructiveDrainRecoveryAction
                            .ResumeAcknowledge,
                        UpperReplay(
                            state.commitId,
                            state.receiptFingerprint,
                            current));
                }
                if (state.phase == ProductionApparelOrderTerminalDrainPhase
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
                return state.phase == ProductionApparelOrderTerminalDrainPhase
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

    public ProductionFacilityDestructiveDrainCheckpointGcResult
        PrepareCheckpointGarbageCollection(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData>
                entries,
            out IProductionFacilityDestructiveDrainCheckpointGcCandidate
                candidate)
    {
        candidate = null;
        return checkpointGc != null
            ? checkpointGc.PrepareCheckpointGarbageCollection(
                context,
                entries,
                out candidate)
            : new ProductionFacilityDestructiveDrainCheckpointGcResult(
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption,
                ProductionFacilityDestructiveDrainCheckpointGcReason
                    .MissingParticipant,
                context.CheckpointSequence,
                "production-apparel-checkpoint-gc-port-missing");
    }

    public ProductionFacilityDestructiveDrainCheckpointGcResult
        PublishCheckpointGarbageCollection(
            IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        if (checkpointGc == null)
        {
            throw new InvalidOperationException(
                "Apparel checkpoint-GC port is missing.");
        }
        return checkpointGc.PublishCheckpointGarbageCollection(candidate);
    }

    public void RollbackCheckpointGarbageCollection(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        if (checkpointGc == null)
        {
            throw new InvalidOperationException(
                "Apparel checkpoint-GC port is missing.");
        }
        checkpointGc.RollbackCheckpointGarbageCollection(candidate);
    }

    public void CompleteCheckpointGarbageCollection(
        IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        if (checkpointGc == null)
        {
            throw new InvalidOperationException(
                "Apparel checkpoint-GC port is missing.");
        }
        checkpointGc.CompleteCheckpointGarbageCollection(candidate);
    }

    private ProductionApparelOrderTerminalDrainRequest CaptureRequest(
        ProductionFacilityDestructiveDrainOperationId operationId,
        ApparelWorkOrderSaveData source)
    {
        if (!ProductionApparelOrderTerminalDrainCanonical.IsValidSourceOrder(source))
            throw new InvalidOperationException(
                "production-apparel-terminal-source-invalid");
        string owner = ProductionFacilityDestructiveDrainOwnerStableIds
            .ApparelWorkOrder(source.orderId);
        string step = ProductionFacilityDestructiveDrainCanonical
            .BuildStepOperationId(operationId, ParticipantId, owner);
        bool hasLease = leases.TryCapture(
            source.orderId,
            out ApparelLeaseAuthoritySnapshot lease,
            out string leaseFailure);
        string leaseFingerprint;
        if (hasLease)
        {
            if (lease == null
                || !string.Equals(lease.OwnerOperationId,
                    source.orderId, StringComparison.Ordinal)
                || !ProductionApparelOrderTerminalDrainCanonical.IsDigest(
                    lease.Fingerprint))
            {
                throw new InvalidOperationException(
                    "production-apparel-terminal-lease-capture-invalid");
            }
            leaseFingerprint = lease.Fingerprint;
        }
        else
        {
            if (!string.Equals(leaseFailure,
                    "apparel-lease-authority-missing:" + source.orderId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "production-apparel-terminal-lease-capture-conflict");
            }
            leaseFingerprint = ProductionApparelOrderTerminalDrainCanonical
                .CreateNoLeaseAuthorityFingerprint(source.orderId);
        }
        if (!ProductionApparelOrderTerminalDrainCanonical
                .TryCreatePendingEffectIdentity(
                    source,
                    out ProductionApparelOrderPendingEffectIdentity pending,
                    out string pendingFailure))
        {
            throw new InvalidOperationException(pendingFailure);
        }
        string requestFingerprint = ProductionApparelOrderTerminalDrainCanonical
            .CreateRequestFingerprint(
                operationId.Value,
                step,
                owner,
                source,
                hasLease,
                leaseFingerprint,
                pending);
        return new ProductionApparelOrderTerminalDrainRequest(
            operationId.Value,
            step,
            owner,
            source,
            hasLease,
            leaseFingerprint,
            pending,
            requestFingerprint);
    }

    private ApparelWorkOrderSaveData[] CaptureOwnedOrders(
        BuildingInstanceId facilityId)
    {
        ApparelWorkOrderSaveData[] values = (orders.Orders
                ?? Array.Empty<ApparelWorkOrderSaveData>())
            .Where(value => value != null
                && value.state != ApparelWorkOrderState.Completed
                && string.Equals(value.facilityInstanceId,
                    facilityId.Value, StringComparison.Ordinal))
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .Select(ProductionApparelOrderTerminalDrainCanonical.CloneOrder)
            .ToArray();
        if (values.Any(value =>
                !ProductionApparelOrderTerminalDrainCanonical
                    .IsValidSourceOrder(value))
            || values.Select(value => value.orderId)
                .Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            throw new InvalidOperationException(
                "production-apparel-terminal-owned-orders-invalid-or-duplicate");
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
            throw new InvalidOperationException(
                "production-apparel-terminal-lifecycle-contribution-missing");
        return matches[0];
    }

    private string CaptureCurrentFingerprint(BuildingInstanceId facilityId) =>
        CaptureContribution(facilityId).DurableSemanticFingerprint;

    private bool TryValidateStepContext(
        ProductionFacilityDestructiveDrainStepContext context,
        out string failureReason)
    {
        failureReason = string.Empty;
        string prefix = "apparel-order:";
        if (!string.Equals(context.ParticipantId, ParticipantId,
                StringComparison.Ordinal)
            || !context.Owner.ownerStableId.StartsWith(prefix,
                StringComparison.Ordinal)
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
            failureReason = "production-apparel-terminal-step-context-invalid";
            return false;
        }
        return true;
    }

    private ProductionFacilityDestructiveDrainStepResult ToUpperStep(
        ProductionFacilityDestructiveDrainStepContext context,
        ProductionApparelOrderTerminalDrainResult result,
        bool requireJournalReceiptMatch)
    {
        string current = CaptureCurrentFingerprint(context.FacilityId);
        if (result.Status == ProductionApparelOrderTerminalDrainStatus.Conflict)
            return UpperConflict(current);
        if (result.Status == ProductionApparelOrderTerminalDrainStatus.Deferred)
        {
            return string.IsNullOrEmpty(result.CommitId)
                && string.IsNullOrEmpty(result.ReceiptFingerprint)
                ? UpperDeferred(current)
                : UpperConflict(current);
        }
        if (result.Status is not ProductionApparelOrderTerminalDrainStatus.Applied
                and not ProductionApparelOrderTerminalDrainStatus.Replay
            || !producerQuery.TryCapture(
                context.Owner.stepOperationId,
                out ProductionApparelOrderTerminalDrainSaveData state)
            || !IsExactProducerState(state, context))
        {
            return UpperConflict(current);
        }

        bool terminal = HasCanonicalTerminalFields(
            result.CommitId, result.ReceiptFingerprint);
        if (!terminal)
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
            result.Status == ProductionApparelOrderTerminalDrainStatus.Applied
                ? ProductionFacilityDestructiveDrainStepStatus.Applied
                : ProductionFacilityDestructiveDrainStepStatus.Replay,
            result.CommitId,
            result.ReceiptFingerprint,
            current);
    }

    private static bool IsExactProducerRequest(
        ProductionApparelOrderTerminalDrainSaveData state,
        ProductionApparelOrderTerminalDrainRequest request) => state != null
        && ProductionApparelOrderTerminalDrainCanonical.IsValidSave(state)
        && string.Equals(state.parentOperationId,
            request.ParentOperationId, StringComparison.Ordinal)
        && string.Equals(state.stepOperationId,
            request.StepOperationId, StringComparison.Ordinal)
        && string.Equals(state.ownerStableId,
            request.OwnerStableId, StringComparison.Ordinal)
        && string.Equals(state.requestFingerprint,
            request.RequestFingerprint, StringComparison.Ordinal)
        && string.Equals(state.sourceOrderFingerprint,
            ProductionApparelOrderTerminalDrainCanonical
                .CreateSourceOrderFingerprint(request.SourceOrder),
            StringComparison.Ordinal)
        && state.hasLeaseAuthority == request.HasLeaseAuthority
        && string.Equals(state.leaseAuthorityFingerprint,
            request.LeaseAuthorityFingerprint, StringComparison.Ordinal)
        && string.Equals(state.pendingEffect?.identityFingerprint ?? string.Empty,
            request.PendingEffect?.identityFingerprint ?? string.Empty,
            StringComparison.Ordinal);

    private static bool IsExactProducerState(
        ProductionApparelOrderTerminalDrainSaveData state,
        ProductionFacilityDestructiveDrainStepContext context) => state != null
        && ProductionApparelOrderTerminalDrainCanonical.IsValidSave(state)
        && string.Equals(state.parentOperationId,
            context.OperationId.Value, StringComparison.Ordinal)
        && string.Equals(state.stepOperationId,
            context.Owner.stepOperationId, StringComparison.Ordinal)
        && string.Equals(state.ownerStableId,
            context.Owner.ownerStableId, StringComparison.Ordinal)
        && string.Equals(state.requestFingerprint,
            context.Owner.requestFingerprint, StringComparison.Ordinal)
        && string.Equals(state.facilityId,
            context.FacilityId.Value, StringComparison.Ordinal)
        && string.Equals(
            ProductionFacilityDestructiveDrainOwnerStableIds
                .ApparelWorkOrder(state.orderId),
            state.ownerStableId,
            StringComparison.Ordinal);

    private static bool IsProducerTerminal(
        ProductionApparelOrderTerminalDrainPhase phase) => phase is
        ProductionApparelOrderTerminalDrainPhase
            .SourceOrderTerminalCommittedAwaitingOwnerAcknowledgement
        or ProductionApparelOrderTerminalDrainPhase
            .OwnerAcknowledgedAwaitingCheckpointGc;

    private string CreatePlanFingerprint(
        string contributionFingerprint,
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerPlan> owners)
    {
        string canonical = string.Join(
            "\n",
            new[]
            {
                "production-apparel-terminal-participant-plan@1",
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

    private static string RequireOrderId(string ownerStableId)
    {
        const string prefix = "apparel-order:";
        if (string.IsNullOrEmpty(ownerStableId)
            || !ownerStableId.StartsWith(prefix, StringComparison.Ordinal)
            || ownerStableId.Length == prefix.Length)
            throw new InvalidOperationException(
                "production-apparel-terminal-owner-id-invalid");
        return ownerStableId.Substring(prefix.Length);
    }

    private static void RequireDestination(
        BuildingInstanceId facilityId,
        ProductionOutputDestinationId destinationId)
    {
        if (!destinationId.Equals(
                ProductionOutputDestinationId.FromFacility(facilityId)))
            throw new InvalidOperationException(
                "production-apparel-terminal-destination-facility-mismatch");
    }

    private static bool HasCanonicalTerminalFields(
        string commitId,
        string receiptFingerprint) =>
        ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(commitId)
        && ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
            receiptFingerprint);

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
