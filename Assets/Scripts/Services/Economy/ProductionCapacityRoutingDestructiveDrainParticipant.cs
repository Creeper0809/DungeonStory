using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ProductionCapacityRoutingDestructiveDrainParticipant :
    IProductionFacilityDestructiveDrainParticipant,
    IProductionFacilityDestructiveDrainDurablePrepareParticipant,
    IProductionFacilityDestructiveDrainCheckpointGcParticipant
{
    public const int CurrentContractVersion = 1;

    private static readonly IReadOnlyList<string> Dependencies =
        Array.AsReadOnly(new[]
        {
            ProductionFacilityDestructiveDrainParticipantIds.ApparelWorkOrders,
            ProductionFacilityDestructiveDrainParticipantIds
                .CombatEquipmentCrafting,
            ProductionFacilityDestructiveDrainParticipantIds
                .GenericProductionBills
        });

    private readonly IProductionOutputDestinationLifecycleQuery lifecycle;
    private readonly IProductionPreparedOutputRoutingAuthority routing;
    private readonly IProductionPreparedOutputRoutingBatchQuery batches;
    private readonly IProductionCapacityRoutingPhysicalSourceQuery physical;
    private readonly IProductionCapacityRoutingDrainOutbox producer;
    private readonly IProductionCapacityRoutingHaulPlanFence haulFence;
    private readonly IProductionCapacityRoutingDrainExecutionCoordinator executor;
    private CheckpointGcCandidate activeCheckpointGcCandidate;

    public ProductionCapacityRoutingDestructiveDrainParticipant(
        IProductionOutputDestinationLifecycleQuery lifecycle,
        IProductionPreparedOutputRoutingAuthority routing,
        IProductionPreparedOutputRoutingBatchQuery batches,
        IProductionCapacityRoutingPhysicalSourceQuery physical,
        IProductionCapacityRoutingDrainOutbox producer,
        IProductionCapacityRoutingHaulPlanFence haulFence,
        IProductionCapacityRoutingDrainExecutionCoordinator executor)
    {
        this.lifecycle = lifecycle
            ?? throw new ArgumentNullException(nameof(lifecycle));
        this.routing = routing ?? throw new ArgumentNullException(nameof(routing));
        this.batches = batches ?? throw new ArgumentNullException(nameof(batches));
        this.physical = physical
            ?? throw new ArgumentNullException(nameof(physical));
        this.producer = producer
            ?? throw new ArgumentNullException(nameof(producer));
        this.haulFence = haulFence
            ?? throw new ArgumentNullException(nameof(haulFence));
        this.executor = executor
            ?? throw new ArgumentNullException(nameof(executor));
    }

    public string ParticipantId =>
        ProductionFacilityDestructiveDrainParticipantIds.CapacityRoutingOutbox;

    public string CheckpointGcParticipantId => ParticipantId;

    public int ContractVersion => CurrentContractVersion;

    public IReadOnlyList<string> DependsOnParticipantIds => Dependencies;

    public ProductionFacilityDestructiveDrainParticipantPlan Prepare(
        ProductionFacilityDestructiveDrainPrepareContext context)
    {
        ProductionOutputDestinationLifecycleContribution contribution =
            CaptureContribution(context.FacilityId);
        ProductionFacilityDestructiveDrainOwnerPlan[] owners =
            CaptureOwnedBatches(context.FacilityId)
                .Select(batch => BuildRequest(
                    context.OperationId,
                    context.FacilityId,
                    context.DestinationId,
                    contribution.DurableSemanticFingerprint,
                    batch))
                .Select(request => new
                    ProductionFacilityDestructiveDrainOwnerPlan(
                        request.OwnerStableId,
                        ProductionFacilityDestructiveDrainDisposition
                            .Terminalize,
                        string.Empty,
                        request.RequestFingerprint))
                .OrderBy(value => value.OwnerStableId, StringComparer.Ordinal)
                .ToArray();
        string planFingerprint = CreatePlanFingerprint(
            contribution.DurableSemanticFingerprint,
            owners);
        return new ProductionFacilityDestructiveDrainParticipantPlan(
            ParticipantId,
            ContractVersion,
            contribution.DurableSemanticFingerprint,
            planFingerprint,
            owners);
    }

    [GameplayInternalOnly(
        "Persists the frozen capacity source only after the upper destructive-drain owner exists.",
        "Production facility destructive-drain runtime only")]
    public bool TryPrepareDurable(
        ProductionFacilityDestructiveDrainStepContext context,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryCaptureOwnedBatch(
                context,
                out ProductionPreparedOutputRoutingBatchSnapshot batch,
                out failureReason))
        {
            return false;
        }
        ProductionCapacityRoutingDrainRequest request = BuildRequest(
            context.OperationId,
            context.FacilityId,
            ProductionOutputDestinationId.FromFacility(context.FacilityId),
            context.ExpectedDurableContributionFingerprint,
            batch);
        if (!string.Equals(
                request.OwnerStableId,
                context.Owner.ownerStableId,
                StringComparison.Ordinal)
            || !string.Equals(
                request.RequestFingerprint,
                context.Owner.requestFingerprint,
                StringComparison.Ordinal))
        {
            failureReason =
                "production-capacity-routing-durable-prepare-plan-drift";
            return false;
        }

        ProductionCapacityRoutingDrainResult prepared =
            producer.TryPrepare(request);
        if (prepared.Status is ProductionCapacityRoutingDrainStatus.Conflict
            or ProductionCapacityRoutingDrainStatus.Deferred)
        {
            failureReason = prepared.FailureReason;
            return false;
        }
        if (!haulFence.TryReleaseUnpickedPlans(
                request.BatchCommitId,
                request.SourceActorCarries,
                out failureReason))
        {
            return false;
        }
        return true;
    }

    [GameplayInternalOnly(
        "Commits one replay-safe capacity-routing producer step through the shared exact route lifecycle.",
        "Production facility destructive-drain runtime only")]
    public ProductionFacilityDestructiveDrainStepResult TryCommit(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        ProductionCapacityRoutingDrainResult result = executor.TryProgress(
            context.Owner.stepOperationId,
            context.Owner.requestFingerprint);
        return ToUpperStep(context.FacilityId, result);
    }

    [GameplayInternalOnly(
        "Acknowledges the producer receipt only after the journal owner records the exact same receipt.",
        "Production facility destructive-drain runtime only")]
    public ProductionFacilityDestructiveDrainStepResult TryAcknowledge(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        ProductionCapacityRoutingDrainResult result = producer.TryAcknowledge(
            context.Owner.stepOperationId,
            context.Owner.receiptFingerprint);
        return ToUpperStep(context.FacilityId, result);
    }

    public ProductionFacilityDestructiveDrainCheckpointGcResult
        PrepareCheckpointGarbageCollection(
            ProductionFacilityDestructiveDrainCheckpointGcContext context,
            IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData> entries,
            out IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        candidate = null;
        if (activeCheckpointGcCandidate != null)
            return GcFailure(context, "capacity-routing-gc-already-prepared");
        if (producer is not IProductionCapacityRoutingDrainCheckpointGcOutbox gc
            || executor is not IProductionCapacityRoutingCheckpointGcAbsenceQuery
                absence)
        {
            return GcFailure(
                context,
                "capacity-routing-gc-capability-missing",
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
            return GcFailure(context, "capacity-routing-gc-entries-invalid");
        }

        List<ProductionCapacityRoutingDrainSaveData> rows = new();
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
                return GcFailure(context, "capacity-routing-gc-journal-conflict");
            }
            foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in
                     matches[0].owners
                     ?? new List<ProductionFacilityDestructiveDrainOwnerSaveData>())
            {
                if (owner == null
                    || owner.phase != ProductionFacilityDestructiveDrainStepPhase
                        .OwnerAcknowledged
                    || !producer.TryCapture(
                        owner.stepOperationId,
                        out ProductionCapacityRoutingDrainSaveData row)
                    || row.phase != ProductionCapacityRoutingDrainPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc
                    || !string.Equals(
                        row.receiptFingerprint,
                        owner.receiptFingerprint,
                        StringComparison.Ordinal))
                {
                    return GcFailure(
                        context,
                        "capacity-routing-gc-row-conflict",
                        ProductionFacilityDestructiveDrainCheckpointGcReason
                            .LiveAuthorityChanged);
                }
                rows.Add(row);
            }
        }

        if (!gc.TryPrepareCheckpointGarbageCollection(
                rows,
                out IProductionCapacityRoutingDrainCheckpointGcCandidate lower,
                out string failureReason))
        {
            return GcFailure(context, failureReason);
        }
        activeCheckpointGcCandidate = new CheckpointGcCandidate(
            context,
            source.Select(entry => entry.operationId).ToArray(),
            rows.Select(row => row.batchCommitId).ToArray(),
            gc,
            lower,
            absence);
        candidate = activeCheckpointGcCandidate;
        return GcApplied(context, source.Length);
    }

    public ProductionFacilityDestructiveDrainCheckpointGcResult
        PublishCheckpointGarbageCollection(
            IProductionFacilityDestructiveDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        foreach (string batchCommitId in exact.BatchCommitIds)
        {
            if (!exact.Absence.TryVerifyRoutingAuthorityAbsent(
                    batchCommitId,
                    out string absenceFailure))
            {
                return GcFailure(
                    exact.Context,
                    absenceFailure,
                    ProductionFacilityDestructiveDrainCheckpointGcReason
                        .LiveAuthorityChanged);
            }
        }
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
        string current = CaptureContribution(context.FacilityId)
            .DurableSemanticFingerprint;
        if (!producer.TryCapture(
                context.Owner.stepOperationId,
                out ProductionCapacityRoutingDrainSaveData state))
        {
            if (context.Owner.phase ==
                ProductionFacilityDestructiveDrainStepPhase.Planned)
            {
                return new ProductionFacilityDestructiveDrainRecoveryResult(
                    ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit,
                    UpperDeferred(current));
            }
            if (context.Owner.phase ==
                ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged)
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
        if (!string.Equals(
                state.requestFingerprint,
                context.Owner.requestFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                state.ownerStableId,
                context.Owner.ownerStableId,
                StringComparison.Ordinal))
        {
            return RecoveryConflict(current);
        }
        if (state.phase is ProductionCapacityRoutingDrainPhase
                .EffectCommittedAwaitingOwnerAck
            or ProductionCapacityRoutingDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc
            && !HasValidTerminalReceipt(state))
        {
            return RecoveryConflict(current);
        }
        if (context.Owner.phase is ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck
            or ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged
            && (!string.Equals(
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
                if (state.phase is ProductionCapacityRoutingDrainPhase
                        .EffectCommittedAwaitingOwnerAck
                    or ProductionCapacityRoutingDrainPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc)
                {
                    return new ProductionFacilityDestructiveDrainRecoveryResult(
                        ProductionFacilityDestructiveDrainRecoveryAction
                            .ResumeCommit,
                        UpperReplay(
                            state.commitId,
                            state.receiptFingerprint,
                            current));
                }
                return new ProductionFacilityDestructiveDrainRecoveryResult(
                    ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit,
                    UpperDeferred(current));

            case ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck:
                if (state.phase == ProductionCapacityRoutingDrainPhase
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
                if (state.phase == ProductionCapacityRoutingDrainPhase
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
                return state.phase == ProductionCapacityRoutingDrainPhase
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

    private ProductionCapacityRoutingDrainRequest BuildRequest(
        ProductionFacilityDestructiveDrainOperationId operationId,
        BuildingInstanceId facilityId,
        ProductionOutputDestinationId destinationId,
        string sourceOwnershipFingerprint,
        ProductionPreparedOutputRoutingBatchSnapshot batch)
    {
        if (!physical.TryCapture(
                batch.BatchCommitId,
                destinationId.Value,
                out ProductionCapacityRoutingPhysicalSourceSnapshot source,
                out string failureReason))
        {
            throw new InvalidOperationException(failureReason);
        }

        ProductionCapacityRoutingDrainLineSaveData[] lines = batch.Lines
            .Select(value => new ProductionCapacityRoutingDrainLineSaveData
            {
                lineCommitId = value.LineCommitId,
                outputLineId = value.OutputLineId,
                itemId = value.ItemId,
                componentFingerprint = value.ComponentFingerprint,
                outputCapabilityId = value.OutputCapabilityId,
                outputCapabilityVersion = value.OutputCapabilityVersion,
                outputComponentCodecId = value.OutputComponentCodecId,
                outputComponentCodecVersion = value.OutputComponentCodecVersion,
                outputCapabilityFingerprint = value.OutputCapabilityFingerprint,
                originalQuantity = value.OriginalQuantity,
                originalMassGrams = value.OriginalMassGrams,
                remainingQuantity = value.RemainingQuantity,
                remainingMassGrams = value.RemainingMassGrams,
                routedQuantity = value.RoutedQuantity,
                routedMassGrams = value.RoutedMassGrams
            })
            .OrderBy(value => value.lineCommitId, StringComparer.Ordinal)
            .ToArray();
        ProductionCapacityRoutingDrainRouteSaveData[] routes =
            batch.RouteOperations.Select(value =>
                new ProductionCapacityRoutingDrainRouteSaveData
                {
                    routeOperationId = value.RouteOperationId,
                    requestFingerprint = value.RequestFingerprint,
                    physicalReceiptFingerprint =
                        value.PhysicalReceiptFingerprint,
                    phase = (int)value.Phase,
                    currentDeliveryRevision = value.CurrentDeliveryRevision,
                    currentDeliveryRevisionFingerprint =
                        value.CurrentDeliveryRevisionFingerprint,
                    currentTargetDestinationId =
                        value.CurrentTargetDestinationId,
                    currentTargetAuthorityFingerprint =
                        value.CurrentTargetAuthorityFingerprint
                })
                .OrderBy(value => value.routeOperationId, StringComparer.Ordinal)
                .ToArray();
        ProductionCapacityRoutingDrainSliceSaveData[] slices =
            batch.PhysicalReceipts
                .SelectMany(receipt => receipt.Slices.Select(value =>
                    new ProductionCapacityRoutingDrainSliceSaveData
                    {
                        routeOperationId = receipt.RouteOperationId,
                        sourceStackId = value.SourceStackId,
                        routedStackId = value.RoutedStackId,
                        outputLineId = value.OutputLineId,
                        lineCommitId = value.LineCommitId,
                        itemId = value.ItemId,
                        sourceOffsetQuantity = value.SourceOffsetQuantity,
                        routedOffsetQuantity = value.RoutedOffsetQuantity,
                        routedQuantity = value.RoutedQuantity,
                        routedMassGrams = value.RoutedMassGrams,
                        componentFingerprint = value.ComponentFingerprint
                    }))
                .OrderBy(
                    ProductionCapacityRoutingDrainFingerprint.SliceKey,
                    StringComparer.Ordinal)
                .ToArray();
        int quantity = checked(lines.Sum(value => value.originalQuantity));
        long grams = lines.Aggregate(
            0L,
            (current, value) => checked(current + value.originalMassGrams));
        if (quantity != source.TotalQuantity || grams != source.TotalMassGrams)
        {
            throw new InvalidOperationException(
                "production-capacity-routing-batch-physical-total-mismatch");
        }

        string owner = ProductionFacilityDestructiveDrainOwnerStableIds
            .RoutingBatch(batch.BatchCommitId);
        string step = ProductionFacilityDestructiveDrainCanonical
            .BuildStepOperationId(operationId, ParticipantId, owner);
        string requestFingerprint = ProductionCapacityRoutingDrainFingerprint
            .CreateRequest(
                step,
                owner,
                facilityId.Value,
                destinationId.Value,
                batch.BatchCommitId,
                batch.OutcomeFingerprint,
                batch.RoutingFingerprint,
                sourceOwnershipFingerprint,
                lines,
                routes,
                slices,
                source.ActorCarries,
                source.CustodyStackIds,
                quantity,
                grams);
        return new ProductionCapacityRoutingDrainRequest(
            step,
            owner,
            facilityId.Value,
            destinationId.Value,
            batch.BatchCommitId,
            batch.OutcomeFingerprint,
            batch.RoutingFingerprint,
            sourceOwnershipFingerprint,
            lines,
            routes,
            slices,
            source.ActorCarries,
            source.CustodyStackIds,
            quantity,
            grams,
            requestFingerprint);
    }

    private IReadOnlyList<ProductionPreparedOutputRoutingBatchSnapshot>
        CaptureOwnedBatches(BuildingInstanceId facilityId)
    {
        string[] ids = routing.CaptureAll()
            .Where(value => string.Equals(
                value.OwnerFacilityId,
                facilityId.Value,
                StringComparison.Ordinal))
            .Select(value => value.BatchCommitId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        List<ProductionPreparedOutputRoutingBatchSnapshot> result = new(ids.Length);
        foreach (string id in ids)
        {
            if (!batches.TryCaptureBatch(
                    id,
                    out ProductionPreparedOutputRoutingBatchSnapshot batch)
                || !string.Equals(
                    batch.OwnerFacilityId,
                    facilityId.Value,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "production-capacity-routing-owned-batch-missing:" + id);
            }
            result.Add(batch);
        }
        return result;
    }

    private bool TryCaptureOwnedBatch(
        ProductionFacilityDestructiveDrainStepContext context,
        out ProductionPreparedOutputRoutingBatchSnapshot batch,
        out string failureReason)
    {
        batch = null;
        failureReason = string.Empty;
        const string prefix = "routing-batch:";
        if (!context.Owner.ownerStableId.StartsWith(
                prefix,
                StringComparison.Ordinal))
        {
            failureReason =
                "production-capacity-routing-owner-kind-invalid";
            return false;
        }
        string batchId = context.Owner.ownerStableId.Substring(prefix.Length);
        if (!batches.TryCaptureBatch(batchId, out batch)
            || !string.Equals(
                batch.OwnerFacilityId,
                context.FacilityId.Value,
                StringComparison.Ordinal))
        {
            failureReason =
                "production-capacity-routing-owner-batch-missing";
            return false;
        }
        return true;
    }

    private ProductionOutputDestinationLifecycleContribution CaptureContribution(
        BuildingInstanceId facilityId)
    {
        ProductionOutputDestinationLifecycleSnapshot snapshot =
            lifecycle.Capture(facilityId);
        ProductionOutputDestinationLifecycleContribution[] matches =
            snapshot.Contributions.Where(value => string.Equals(
                    value.ContributorId,
                    ParticipantId,
                    StringComparison.Ordinal))
                .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "production-capacity-routing-lifecycle-contribution-missing");
        }
        return matches[0];
    }

    private string CreatePlanFingerprint(
        string contributionFingerprint,
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerPlan> owners)
    {
        string canonical = string.Join(
            "\n",
            new[]
            {
                "production-capacity-routing-participant-plan@1",
                ParticipantId,
                ContractVersion.ToString(),
                contributionFingerprint
            }.Concat((owners
                    ?? Array.Empty<ProductionFacilityDestructiveDrainOwnerPlan>())
                .OrderBy(value => value.OwnerStableId, StringComparer.Ordinal)
                .Select(value => string.Join(
                    "|",
                    value.OwnerStableId,
                    ((int)value.Disposition).ToString(),
                    value.TargetDestinationId,
                    value.RequestFingerprint))));
        return ProductionFacilityDestructiveDrainCanonical
            .ComputeFingerprint(canonical);
    }

    private ProductionFacilityDestructiveDrainStepResult ToUpperStep(
        BuildingInstanceId facilityId,
        ProductionCapacityRoutingDrainResult result)
    {
        string current = CaptureContribution(facilityId)
            .DurableSemanticFingerprint;
        return result.Status switch
        {
            ProductionCapacityRoutingDrainStatus.Applied
                when HasValidTerminalReceipt(result) => new
                ProductionFacilityDestructiveDrainStepResult(
                    ProductionFacilityDestructiveDrainStepStatus.Applied,
                    result.CommitId,
                    result.ReceiptFingerprint,
                    current),
            ProductionCapacityRoutingDrainStatus.Replay
                when HasValidTerminalReceipt(result) => new
                ProductionFacilityDestructiveDrainStepResult(
                    ProductionFacilityDestructiveDrainStepStatus.Replay,
                    result.CommitId,
                    result.ReceiptFingerprint,
                    current),
            ProductionCapacityRoutingDrainStatus.Deferred => new
                ProductionFacilityDestructiveDrainStepResult(
                    ProductionFacilityDestructiveDrainStepStatus.Deferred,
                    string.Empty,
                    string.Empty,
                    current),
            _ => new ProductionFacilityDestructiveDrainStepResult(
                ProductionFacilityDestructiveDrainStepStatus.Conflict,
                string.Empty,
                string.Empty,
                current)
        };
    }

    private static bool HasValidTerminalReceipt(
        ProductionCapacityRoutingDrainResult result) =>
        ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(
            result.CommitId)
        && ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
            result.ReceiptFingerprint);

    private static bool HasValidTerminalReceipt(
        ProductionCapacityRoutingDrainSaveData state) =>
        state != null
        && ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(
            state.commitId)
        && ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
            state.receiptFingerprint);

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

    private static ProductionFacilityDestructiveDrainRecoveryResult
        RecoveryConflict(string current) => new(
        ProductionFacilityDestructiveDrainRecoveryAction.Conflict,
        new ProductionFacilityDestructiveDrainStepResult(
            ProductionFacilityDestructiveDrainStepStatus.Conflict,
            string.Empty,
            string.Empty,
            current));

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
                "production-capacity-routing-checkpoint-gc-candidate-conflict");
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
            IReadOnlyList<string> batchCommitIds,
            IProductionCapacityRoutingDrainCheckpointGcOutbox port,
            IProductionCapacityRoutingDrainCheckpointGcCandidate lowerCandidate,
            IProductionCapacityRoutingCheckpointGcAbsenceQuery absence)
        {
            Context = context;
            OperationIds = operationIds
                ?? throw new ArgumentNullException(nameof(operationIds));
            BatchCommitIds = batchCommitIds
                ?? throw new ArgumentNullException(nameof(batchCommitIds));
            Port = port ?? throw new ArgumentNullException(nameof(port));
            LowerCandidate = lowerCandidate
                ?? throw new ArgumentNullException(nameof(lowerCandidate));
            Absence = absence ?? throw new ArgumentNullException(nameof(absence));
        }

        public string ParticipantId =>
            ProductionFacilityDestructiveDrainParticipantIds.CapacityRoutingOutbox;
        public long CheckpointSequence => Context.CheckpointSequence;
        public string SerializedByteDigest => Context.SerializedByteDigest;
        public IReadOnlyList<string> OperationIds { get; }
        internal ProductionFacilityDestructiveDrainCheckpointGcContext Context
        { get; }
        internal IReadOnlyList<string> BatchCommitIds { get; }
        internal IProductionCapacityRoutingDrainCheckpointGcOutbox Port { get; }
        internal IProductionCapacityRoutingDrainCheckpointGcCandidate LowerCandidate
        { get; }
        internal IProductionCapacityRoutingCheckpointGcAbsenceQuery Absence { get; }
        internal bool PublishAttempted { get; set; }
        internal bool Published { get; set; }
        internal bool Completed { get; set; }
    }
}
