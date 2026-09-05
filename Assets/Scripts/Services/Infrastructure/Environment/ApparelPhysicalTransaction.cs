using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public enum ApparelPhysicalTransactionStatus
{
    Completed = 0,
    WaitingForOutputSpace = 1,
    PendingFinalization = 2,
    Conflict = 3
}

public readonly struct ApparelPhysicalTransactionResult
{
    public ApparelPhysicalTransactionResult(
        ApparelPhysicalTransactionStatus status,
        string outputStackId,
        string outputInstanceId,
        long inputMassGrams,
        long outputMassGrams,
        string failureReason)
    {
        Status = status;
        OutputStackId = outputStackId ?? string.Empty;
        OutputInstanceId = outputInstanceId ?? string.Empty;
        InputMassGrams = Math.Max(0L, inputMassGrams);
        OutputMassGrams = Math.Max(0L, outputMassGrams);
        FailureReason = failureReason ?? string.Empty;
    }

    public ApparelPhysicalTransactionStatus Status { get; }
    public string OutputStackId { get; }
    public string OutputInstanceId { get; }
    public long InputMassGrams { get; }
    public long OutputMassGrams { get; }
    public string FailureReason { get; }
    public bool IsCompleted => Status == ApparelPhysicalTransactionStatus.Completed;
}

public interface IApparelPhysicalTransaction
{
    bool TryValidateCraftOutputCapability(
        ApparelWorkOrderSaveData order,
        string expectedOutputItemId,
        out DomainFailure failure);

    ApparelPhysicalTransactionResult ExecuteCraftOrResume(
        ApparelWorkOrderSaveData order,
        BuildableObject facility,
        string outputItemId,
        ItemInstanceComponentSaveData frozenOutputComponent,
        bool markForSale);

    ApparelPhysicalTransactionResult ExecuteRejectedDismantleOrResume(
        ApparelWorkOrderSaveData order,
        BuildableObject facility,
        string recoveryItemId) => new(
            ApparelPhysicalTransactionStatus.Conflict,
            string.Empty,
            string.Empty,
            0L,
            0L,
            "apparel-rejected-physical-capability-unavailable");
}

/// <summary>
/// Composes Apparel's owner state with the existing Items-owned pending input,
/// facility gram admission, and atomic full-batch publication authorities. The
/// work-order record is the durable cross-aggregate join; this service never
/// uses direct spawn/delete rollback.
/// </summary>
public sealed class ApparelPhysicalTransaction : IApparelPhysicalTransaction
{
    private const string OutcomeSchema = "apparel-craft-output@2";
    public const string OutputLineId = "output:apparel-crafted-item";
    public const string RejectedRecoveryOutputLineId =
        "output:apparel-rejected-recovery";
    private const string InputReasonCode = "apparel-craft-input-incorporated";

    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalItemBatchDispositionService dispositions;
    private readonly IReservedPhysicalItemBatchDispositionService
        reservedDispositions;
    private readonly IItemQuantityReservationService quantityReservations;
    private readonly IProductionFacilityHandleQuery facilityHandles;
    private readonly IProductionOutputDestinationAuthorityRuntime destinations;
    private readonly IProductionOutputBufferCapacityProjector capacityProjector;
    private readonly IFacilityBufferMassAdmissionService admission;
    private readonly IFacilityBufferPlannedOutputPublicationService publication;
    private readonly IItemInstanceRepository instances;
    private readonly IProductionOutputCapabilityRegistry outputCapabilities;
    private readonly IProductionOutputMaximumMassRegistry outputMaximumMass;

    public ApparelPhysicalTransaction(
        IWorldItemStackRuntime items,
        IPhysicalItemBatchDispositionService dispositions,
        IReservedPhysicalItemBatchDispositionService reservedDispositions,
        IItemQuantityReservationService quantityReservations,
        IProductionFacilityHandleQuery facilityHandles,
        IProductionOutputDestinationAuthorityRuntime destinations,
        IProductionOutputBufferCapacityProjector capacityProjector,
        IFacilityBufferMassAdmissionService admission,
        IFacilityBufferPlannedOutputPublicationService publication,
        IItemInstanceRepository instances,
        IProductionOutputCapabilityRegistry outputCapabilities,
        IProductionOutputMaximumMassRegistry outputMaximumMass)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.dispositions = dispositions
            ?? throw new ArgumentNullException(nameof(dispositions));
        this.reservedDispositions = reservedDispositions
            ?? throw new ArgumentNullException(nameof(reservedDispositions));
        this.quantityReservations = quantityReservations
            ?? throw new ArgumentNullException(nameof(quantityReservations));
        this.facilityHandles = facilityHandles
            ?? throw new ArgumentNullException(nameof(facilityHandles));
        this.destinations = destinations
            ?? throw new ArgumentNullException(nameof(destinations));
        this.capacityProjector = capacityProjector
            ?? throw new ArgumentNullException(nameof(capacityProjector));
        this.admission = admission
            ?? throw new ArgumentNullException(nameof(admission));
        this.publication = publication
            ?? throw new ArgumentNullException(nameof(publication));
        this.instances = instances
            ?? throw new ArgumentNullException(nameof(instances));
        this.outputCapabilities = outputCapabilities
            ?? throw new ArgumentNullException(nameof(outputCapabilities));
        this.outputMaximumMass = outputMaximumMass
            ?? throw new ArgumentNullException(nameof(outputMaximumMass));
    }

    public bool TryValidateCraftOutputCapability(
        ApparelWorkOrderSaveData order,
        string expectedOutputItemId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        ProductionOutputCapabilitySaveData frozen =
            order?.craftOutputCapability;
        if (order == null
            || order.kind != ApparelWorkOrderKind.Craft
            || frozen == null
            || frozen.IsEmpty
            || !Canonical(expectedOutputItemId)
            || !string.Equals(
                frozen.outputLineId,
                OutputLineId,
                StringComparison.Ordinal)
            || !string.Equals(
                frozen.itemId,
                expectedOutputItemId,
                StringComparison.Ordinal)
            || !string.Equals(
                frozen.capabilityId,
                ProductionOutputCapabilityIds.ApparelWorkOrder,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                expectedOutputItemId ?? string.Empty,
                "apparel-output-capability-owner-mismatch");
            return false;
        }
        return outputCapabilities.TryValidateExact(
            frozen.ToDescriptor(),
            out _,
            out failure);
    }

    [GameplayInternalOnly(
        "Apparel craft completion publishes one admitted physical output and owns the matching pending material debit.",
        "ApparelWorkOrderRuntime craft resolver only")]
    public ApparelPhysicalTransactionResult ExecuteCraftOrResume(
        ApparelWorkOrderSaveData order,
        BuildableObject facility,
        string outputItemId,
        ItemInstanceComponentSaveData frozenOutputComponent,
        bool markForSale)
    {
        if (order == null
            || facility == null
            || order.kind != ApparelWorkOrderKind.Craft
            || !Canonical(order.orderId)
            || !Canonical(outputItemId)
            || frozenOutputComponent == null)
        {
            return Conflict("apparel-craft-physical-request-invalid");
        }

        try
        {
            ProductionFacilityHandle handle = facilityHandles.CaptureFacility(facility);
            if (handle == null
                || handle.IsDestroyed
                || !handle.InstanceId.IsValid
                || !string.Equals(
                    handle.InstanceId.Value,
                    order.facilityInstanceId,
                    StringComparison.Ordinal))
            {
                return Conflict("apparel-craft-facility-authority-invalid");
            }

            ProductionOutputCapabilityDescriptor outputCapability =
                outputCapabilities.CaptureDeclaredDescriptor(
                    OutputLineId,
                    outputItemId,
                    ProductionOutputCapabilityIds.ApparelWorkOrder);
            if (order.craftOutputCapability is { IsEmpty: false }
                && !TryValidateCraftOutputCapability(
                    order,
                    outputItemId,
                    out DomainFailure capabilityFailure))
            {
                return Conflict(
                    "apparel-craft-output-capability:"
                    + capabilityFailure.ToString());
            }

            string componentFingerprint = CreateComponentFingerprint(
                frozenOutputComponent);
            string instanceId = order.craftOutputInstanceId;
            if (string.IsNullOrEmpty(instanceId))
            {
                instanceId = instances.AllocateItemInstanceId().Value;
                order.craftOutputInstanceId = instanceId;
            }
            if (!Canonical(instanceId))
                return Conflict("apparel-craft-output-instance-invalid");

            PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
                items.MassQuery,
                (ItemDefinitionId)outputItemId,
                instanceId,
                new[] { frozenOutputComponent });
            long outputMassGrams = items.MassQuery.GetQuantityMass(
                (ItemDefinitionId)outputItemId,
                subject,
                1).Value;
            if (outputMassGrams <= 0L)
                return Conflict("apparel-craft-output-mass-invalid");

            ProductionOutputBatchMaximumMassProof maximumMassProof = new(
                new[]
                {
                    outputMaximumMass.CaptureDeclared(outputCapability, 1)
                });
            if (outputMassGrams > maximumMassProof.MaximumBatchMassGrams)
            {
                return Conflict(
                    "apparel-craft-output-mass-exceeds-capability-maximum");
            }

            ProductionOutputBufferCapacitySourceSnapshot capacity =
                capacityProjector.CaptureSource(handle, maximumMassProof);
            if (!destinations.TryEnsureCapacitySource(
                    handle,
                    capacity,
                    out FacilityBufferCapacityProfile profile,
                    out string destinationFailure))
            {
                return Pending("apparel-craft-output-authority:" + destinationFailure);
            }

            string batchCommitId = "apparel-craft-output-batch:"
                + order.orderId + ":"
                + order.qualityAttemptIndex.ToString("D4", CultureInfo.InvariantCulture);
            string outcomeFingerprint = CreateOutcomeFingerprint(
                order,
                outputItemId,
                instanceId,
                componentFingerprint,
                outputCapability.Fingerprint,
                markForSale,
                outputMassGrams);
            if (!AdoptFrozenAuthority(
                    order,
                    batchCommitId,
                    outcomeFingerprint,
                    componentFingerprint,
                    outputCapability,
                    maximumMassProof,
                    capacity,
                    outputMassGrams,
                    out string frozenFailure))
            {
                return Conflict(frozenFailure);
            }

            if (order.craftOutputAcknowledged)
            {
                return TryReturnTerminalReplay(
                    order,
                    outputItemId,
                    handle,
                    markForSale);
            }

            FacilityBufferPlannedOutputToken token;
            if (!TryGetOrReserveToken(
                    order,
                    handle,
                    profile,
                    subject,
                    frozenOutputComponent,
                    out token,
                    out FacilityBufferMassAdmissionFailureCode reserveCode,
                    out string reserveFailure))
            {
                return reserveCode ==
                        FacilityBufferMassAdmissionFailureCode.CapacityUnavailable
                    ? Waiting(reserveFailure)
                    : Pending(reserveFailure);
            }

            if (!order.craftInputAcknowledged
                && !TryCommitOrJoinInput(order, out string inputFailure))
            {
                ReleaseUnpublishedToken(order, token);
                return Conflict(inputFailure);
            }

            bool restoredPublication = publication.TryCapturePendingBatch(
                order.craftOutputBatchCommitId,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot restored,
                out _,
                out string captureFailure);
            FacilityBufferPlannedOutputPublicationReceipt published;
            if (restoredPublication)
            {
                if (!TryCreatePublicationReceipt(
                        token,
                        restored,
                        out published,
                        out string restoreFailure))
                {
                    return Conflict(restoreFailure);
                }
            }
            else
            {
                if (!captureFailure.StartsWith(
                        "planned-output-batch-missing:",
                        StringComparison.Ordinal)
                    && !string.IsNullOrEmpty(captureFailure))
                {
                    return Conflict(
                        "apparel-craft-output-capture-conflict:" + captureFailure);
                }
                if (!publication.TryPublishFullBatch(
                        token,
                        out published,
                        out _,
                        out string publicationFailure))
                {
                    ReleaseUnpublishedToken(order, token);
                    return Pending(
                        "apparel-craft-output-publication:" + publicationFailure);
                }
            }

            order.craftOutputPublished = true;
            order.craftPlannedOutputFingerprint =
                published.PlannedOutputFingerprint;
            if (!admission.TryCommitPlannedOutput(
                    token,
                    published,
                    out FacilityBufferPlannedOutputReceipt committed,
                    out _,
                    out string commitFailure)
                || committed.CommittedMassGrams != outputMassGrams)
            {
                return Pending("apparel-craft-output-admission:" + commitFailure);
            }
            order.craftAdmissionCommitted = true;

            if (!TryAdoptPublishedOutput(
                    order,
                    outputItemId,
                    published,
                    out WorldItemStackSnapshot output,
                    out string outputFailure))
            {
                return Conflict(outputFailure);
            }

            if (!order.craftInputAcknowledged)
            {
                if (!dispositions.Acknowledge(
                        order.craftInputCommitId,
                        out string inputAcknowledgementFailure))
                {
                    return Pending(
                        "apparel-craft-input-acknowledgement:"
                        + inputAcknowledgementFailure);
                }
                order.craftInputAcknowledged = true;
            }

            if (!order.craftOutputAcknowledged)
            {
                FacilityBufferAcknowledgedOutputReleaseTarget releaseTarget =
                    FacilityBufferAcknowledgedOutputReleaseTarget.Unassigned;
                bool releaseForNaturalHaul = !markForSale;
                bool acknowledged = restoredPublication
                    ? releaseForNaturalHaul
                        ? publication.TryAcknowledgeAndReleaseRestoreCandidate(
                            restored,
                            releaseTarget,
                            out _,
                            out string outputAcknowledgementFailure)
                        : publication.TryAcknowledgeRestoreCandidate(
                            restored,
                            out _,
                            out outputAcknowledgementFailure)
                    : releaseForNaturalHaul
                        ? publication.TryAcknowledgeAndReleasePublishedBatch(
                            published,
                            releaseTarget,
                            out _,
                            out outputAcknowledgementFailure)
                        : publication.TryAcknowledgePublishedBatch(
                            published,
                            out _,
                            out outputAcknowledgementFailure);
                if (!acknowledged)
                {
                    return Pending(
                        "apparel-craft-output-acknowledgement:"
                        + outputAcknowledgementFailure);
                }
                order.craftOutputAcknowledged = true;
            }

            if (markForSale && !order.craftMarketRouted)
            {
                if (!items.TryRouteStackToDestination(
                        output.StackId,
                        WorldItemStackState.FacilityOutputBuffer,
                        QualityRejectedOutputRules.MarketDestinationId,
                        handle.Position,
                        out string marketFailure))
                {
                    return Pending("apparel-craft-market-route:" + marketFailure);
                }
                order.craftMarketRouted = true;
            }

            return new ApparelPhysicalTransactionResult(
                ApparelPhysicalTransactionStatus.Completed,
                output.StackId,
                output.ItemInstanceId,
                order.craftInputMassGrams,
                order.craftOutputMassGrams,
                string.Empty);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            return Conflict("apparel-craft-physical-exception:" + exception.Message);
        }
    }

    [GameplayInternalOnly(
        "Quality-rejected Apparel dismantle reserves its complete recovery batch before moving the exact rejected instance into Transfer WIP.",
        "ApparelWorkOrderRuntime rejected-output resolver only")]
    public ApparelPhysicalTransactionResult ExecuteRejectedDismantleOrResume(
        ApparelWorkOrderSaveData order,
        BuildableObject facility,
        string recoveryItemId)
    {
        if (order == null
            || facility == null
            || !order.dismantlingRejectedOutput
            || !Canonical(order.orderId)
            || !Canonical(order.rejectedOutputStackId)
            || !Canonical(order.rejectedOutputInstanceId)
            || !Canonical(recoveryItemId)
            || !string.Equals(
                order.rejectedRecoveryItemId,
                recoveryItemId,
                StringComparison.Ordinal)
            || order.rejectedMaterialAmount < 0)
        {
            return Conflict("apparel-rejected-physical-request-invalid");
        }

        try
        {
            ProductionFacilityHandle handle = facilityHandles.CaptureFacility(facility);
            if (handle == null
                || handle.IsDestroyed
                || !handle.InstanceId.IsValid
                || !string.Equals(
                    handle.InstanceId.Value,
                    order.facilityInstanceId,
                    StringComparison.Ordinal))
            {
                return Conflict("apparel-rejected-facility-authority-invalid");
            }

            int quantity = order.rejectedMaterialAmount;
            PhysicalItemMassSubject recoverySubject =
                PhysicalItemMassSubjectAdapter.Create(
                    items.MassQuery,
                    (ItemDefinitionId)recoveryItemId,
                    string.Empty,
                    Array.Empty<ItemInstanceComponentSaveData>());
            long outputMassGrams = quantity == 0
                ? 0L
                : items.MassQuery.GetQuantityMass(
                    (ItemDefinitionId)recoveryItemId,
                    recoverySubject,
                    quantity).Value;
            if (quantity > 0 && outputMassGrams <= 0L)
                return Conflict("apparel-rejected-recovery-mass-invalid");

            ProductionOutputBatchMaximumMassProof maximumMassProof = null;
            ProductionOutputCapabilityDescriptor rejectedOutputCapability = default;
            if (quantity > 0)
            {
                ProductionOutputMaximumMassProjection maximumProjection;
                if (order.rejectedRecoveryOutputCapability is { IsEmpty: false })
                {
                    rejectedOutputCapability =
                        order.rejectedRecoveryOutputCapability.ToDescriptor();
                    if (!string.Equals(
                            rejectedOutputCapability.OutputLineId,
                            RejectedRecoveryOutputLineId,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            rejectedOutputCapability.ItemId,
                            recoveryItemId,
                            StringComparison.Ordinal))
                    {
                        return Conflict(
                            "apparel-rejected-output-capability-owner-mismatch");
                    }
                    if (!outputCapabilities.TryValidateExact(
                            rejectedOutputCapability,
                            out _,
                            out DomainFailure capabilityFailure))
                    {
                        return Conflict(
                            "apparel-rejected-output-capability:"
                            + capabilityFailure.ToString());
                    }
                    maximumProjection = outputMaximumMass.CaptureDeclared(
                        rejectedOutputCapability,
                        quantity);
                }
                else
                {
                    maximumProjection = outputMaximumMass.CaptureAutomatic(
                        RejectedRecoveryOutputLineId,
                        recoveryItemId,
                        quantity);
                    rejectedOutputCapability = maximumProjection.Descriptor;
                    if (!outputCapabilities.TryValidateExact(
                            rejectedOutputCapability,
                            out _,
                            out DomainFailure capabilityFailure))
                    {
                        return Conflict(
                            "apparel-rejected-output-capability:"
                            + capabilityFailure.ToString());
                    }
                }
                maximumMassProof = new ProductionOutputBatchMaximumMassProof(
                    new[] { maximumProjection });
                if (outputMassGrams > maximumMassProof.MaximumBatchMassGrams)
                {
                    return Conflict(
                        "apparel-rejected-output-mass-exceeds-capability-maximum");
                }
            }

            string inputOperationId = ApparelRejectedDismantleOutbox
                .FormatOperationId(order.orderId, order.qualityAttemptIndex);
            string batchCommitId = quantity == 0
                ? string.Empty
                : ApparelRejectedDismantleOutbox.FormatRecoveryCommitId(
                    ApparelRejectedDismantleOutbox.FormatRecoveryOperationId(
                        order.orderId,
                        order.qualityAttemptIndex),
                    recoveryItemId,
                    quantity);
            string outcomeFingerprint = CreateRejectedOutcomeFingerprint(
                order,
                recoveryItemId,
                quantity,
                outputMassGrams);

            if (order.rejectedRecoveryOutputAcknowledged)
            {
                if (quantity > 0
                    && (!string.Equals(
                            order.rejectedRecoveryMaximumMassProofDigest,
                            maximumMassProof.SourceDigest,
                            StringComparison.Ordinal)
                        || order.rejectedRecoveryMaximumBatchMassGrams
                            != maximumMassProof.MaximumBatchMassGrams))
                {
                    return Conflict(
                        "apparel-rejected-maximum-mass-proof-drift");
                }
                return TryReturnRejectedTerminalReplay(
                    order,
                    recoveryItemId,
                    outputMassGrams);
            }

            FacilityBufferPlannedOutputToken token = default;
            FacilityBufferCapacityProfile profile = default;
            ProductionOutputBufferCapacitySourceSnapshot capacity = default;
            if (quantity > 0)
            {
                capacity = capacityProjector.CaptureSource(
                    handle,
                    maximumMassProof);
                if (!destinations.TryEnsureCapacitySource(
                        handle,
                        capacity,
                        out profile,
                        out string destinationFailure))
                {
                    return Pending(
                        "apparel-rejected-output-authority:"
                        + destinationFailure);
                }
                if (!TryGetOrReserveRejectedToken(
                        order,
                        handle,
                        profile,
                        capacity,
                        recoverySubject,
                        recoveryItemId,
                        quantity,
                        outputMassGrams,
                        batchCommitId,
                        outcomeFingerprint,
                        out token,
                        out FacilityBufferMassAdmissionFailureCode reserveCode,
                        out string reserveFailure))
                {
                    return reserveCode ==
                            FacilityBufferMassAdmissionFailureCode
                                .CapacityUnavailable
                        ? Waiting(reserveFailure)
                        : Pending(reserveFailure);
                }
            }

            if (!order.rejectedOutputConsumed
                && !TryCommitOrJoinRejectedInput(
                    order,
                    inputOperationId,
                    out string inputFailure))
            {
                ReleaseUnpublishedRejectedToken(order, token);
                return Conflict(inputFailure);
            }
            if (outputMassGrams > order.rejectedDismantleInputMassGrams)
            {
                ReleaseUnpublishedRejectedToken(order, token);
                return Conflict(
                    "apparel-rejected-recovery-mass-exceeds-input");
            }

            if (quantity == 0)
            {
                order.rejectedRecoveryOperationId =
                    ApparelRejectedDismantleOutbox.FormatRecoveryOperationId(
                        order.orderId,
                        order.qualityAttemptIndex);
                order.rejectedRecoveryCommitId = string.Empty;
                order.rejectedRecoveryOutcomeFingerprint = outcomeFingerprint;
                order.rejectedRecoveryOutputMassGrams = 0L;
                order.rejectedMaterialSpawned = 0;
                order.rejectedRecoveryPublished = true;
                order.rejectedRecoveryAdmissionCommitted = true;
                order.rejectedRecoveryOutputAcknowledged = true;
            }
            else
            {
                if (!AdoptRejectedFrozenAuthority(
                        order,
                        batchCommitId,
                        outcomeFingerprint,
                        rejectedOutputCapability,
                        maximumMassProof,
                        capacity,
                        outputMassGrams,
                        out string frozenFailure))
                {
                    return Conflict(frozenFailure);
                }
                order.rejectedRecoveryOperationId =
                    token.Request.PublicationOperationId;
                order.rejectedRecoveryAdmissionTokenId = token.TokenId;
                order.rejectedRecoveryPlannedOutputFingerprint =
                    token.PlannedOutput.Fingerprint;

                bool restoredPublication = publication.TryCapturePendingBatch(
                    batchCommitId,
                    out FacilityBufferPlannedOutputRestoreBatchSnapshot restored,
                    out _,
                    out string captureFailure);
                FacilityBufferPlannedOutputPublicationReceipt published;
                if (restoredPublication)
                {
                    if (!TryCreatePublicationReceipt(
                            token,
                            restored,
                            out published,
                            out string restoreFailure))
                    {
                        return Conflict(restoreFailure);
                    }
                }
                else
                {
                    if (!captureFailure.StartsWith(
                            "planned-output-batch-missing:",
                            StringComparison.Ordinal)
                        && !string.IsNullOrEmpty(captureFailure))
                    {
                        return Conflict(
                            "apparel-rejected-output-capture-conflict:"
                            + captureFailure);
                    }
                    if (!publication.TryPublishFullBatch(
                            token,
                            out published,
                            out _,
                            out string publicationFailure))
                    {
                        ReleaseUnpublishedRejectedToken(order, token);
                        return Pending(
                            "apparel-rejected-output-publication:"
                            + publicationFailure);
                    }
                }

                order.rejectedRecoveryPublished = true;
                order.rejectedRecoveryPlannedOutputFingerprint =
                    published.PlannedOutputFingerprint;
                if (!admission.TryCommitPlannedOutput(
                        token,
                        published,
                        out FacilityBufferPlannedOutputReceipt committed,
                        out _,
                        out string commitFailure)
                    || committed.CommittedMassGrams != outputMassGrams)
                {
                    return Pending(
                        "apparel-rejected-output-admission:" + commitFailure);
                }
                order.rejectedRecoveryAdmissionCommitted = true;

                if (!TryAdoptRejectedOutputs(
                        order,
                        recoveryItemId,
                        quantity,
                        outputMassGrams,
                        published,
                        out string outputFailure))
                {
                    return Conflict(outputFailure);
                }

                if (!order.rejectedRecoveryOutputAcknowledged)
                {
                    bool acknowledged = restoredPublication
                        ? publication.TryAcknowledgeRestoreCandidate(
                            restored,
                            out _,
                            out string outputAcknowledgementFailure)
                        : publication.TryAcknowledgePublishedBatch(
                            published,
                            out _,
                            out outputAcknowledgementFailure);
                    if (!acknowledged)
                    {
                        return Pending(
                            "apparel-rejected-output-acknowledgement:"
                            + outputAcknowledgementFailure);
                    }
                    order.rejectedRecoveryOutputAcknowledged = true;
                }
            }

            if (!order.rejectedDismantleAcknowledged)
            {
                if (!dispositions.Acknowledge(
                        order.rejectedDismantleCommitId,
                        out string inputAcknowledgementFailure))
                {
                    return Pending(
                        "apparel-rejected-input-acknowledgement:"
                        + inputAcknowledgementFailure);
                }
                order.rejectedDismantleAcknowledged = true;
            }

            return new ApparelPhysicalTransactionResult(
                ApparelPhysicalTransactionStatus.Completed,
                order.rejectedRecoveryStackIds?.FirstOrDefault()
                    ?? string.Empty,
                string.Empty,
                order.rejectedDismantleInputMassGrams,
                order.rejectedRecoveryOutputMassGrams,
                string.Empty);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            return Conflict(
                "apparel-rejected-physical-exception:" + exception.Message);
        }
    }

    private bool TryGetOrReserveRejectedToken(
        ApparelWorkOrderSaveData order,
        ProductionFacilityHandle handle,
        FacilityBufferCapacityProfile profile,
        ProductionOutputBufferCapacitySourceSnapshot capacity,
        PhysicalItemMassSubject recoverySubject,
        string recoveryItemId,
        int quantity,
        long outputMassGrams,
        string batchCommitId,
        string outcomeFingerprint,
        out FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        token = default;
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (order.rejectedOutputConsumed
            && !string.IsNullOrEmpty(order.rejectedRecoveryAdmissionTokenId))
        {
            if (!admission.TryGetPlannedOutputToken(
                    order.rejectedRecoveryAdmissionTokenId,
                    out token,
                    out FacilityBufferMassAdmissionTokenStatus status))
            {
                failureReason = "apparel-rejected-admission-token-missing";
                return false;
            }
            if (status != FacilityBufferMassAdmissionTokenStatus.Released)
            {
                return RejectedTokenMatchesOwner(
                    order,
                    token,
                    profile,
                    handle,
                    out failureReason);
            }
            BeginNextRejectedPublicationAttempt(order);
        }

        string publicationOperationId = FormatRejectedPublicationOperationId(
            order.orderId,
            order.qualityAttemptIndex,
            order.rejectedRecoveryPublicationAttempt);
        FacilityBufferPlannedOutputRequest request = new(
            publicationOperationId,
            batchCommitId,
            outcomeFingerprint,
            profile.DestinationId,
            handle.Position,
            profile.OwnerDomain,
            profile.OwnerOperationId,
            profile.OwnerFacilityId,
            profile.CapacityRevision,
            new[]
            {
                new FacilityBufferPlannedOutputSlice(
                        RejectedRecoveryOutputLineId,
                    recoverySubject,
                    quantity,
                    Array.Empty<ItemInstanceComponentSaveData>(),
                    string.Empty)
            },
            capacity.SourceDigest,
            capacity.RequiredMinimumCapacityGrams,
            profile.AuthorityDigest);
        return admission.TryReservePlannedOutput(
            request,
            out token,
            out failureCode,
            out failureReason);
    }

    private bool TryCommitOrJoinRejectedInput(
        ApparelWorkOrderSaveData order,
        string operationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (dispositions.TryGetPending(
                operationId,
                out PhysicalItemBatchDispositionReceipt existing))
        {
            return AdoptRejectedInputReceipt(
                order,
                operationId,
                existing,
                out failureReason);
        }

        WorldItemStackSnapshot source = items.GetAllStacks()
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.StackId,
                    order.rejectedOutputStackId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.ItemInstanceId,
                    order.rejectedOutputInstanceId,
                    StringComparison.Ordinal)
                && value.State == WorldItemStackState.FacilityOutputBuffer
                && value.Quantity == 1);
        if (source == null)
        {
            failureReason = "apparel-rejected-source-missing";
            return false;
        }

        if (string.IsNullOrEmpty(order.rejectedOutputLeaseId))
        {
            if (!quantityReservations.TryReserve(
                    operationId,
                    string.Empty,
                    ItemReservationPurpose.Equipment,
                    "apparel-rejected:" + order.orderId,
                    new ItemQuantityReservationRequest(
                        (ItemStackId)source.StackId,
                        1,
                        source.ReservationSignature),
                    out ItemQuantityLease created,
                    out DomainFailure reserveFailure))
            {
                failureReason = "apparel-rejected-source-reservation:"
                    + reserveFailure.Code;
                return false;
            }
            order.rejectedOutputLeaseId = created.leaseId;
        }
        else if (!quantityReservations.Revalidate(
                     order.rejectedOutputLeaseId,
                     out ItemQuantityLease lease,
                     out DomainFailure revalidateFailure)
                 || !string.Equals(
                     lease.ownerOperationId,
                     operationId,
                     StringComparison.Ordinal)
                 || lease.remainingQuantity < 1
                 || lease.slices.Count != 1
                 || !string.Equals(
                     lease.slices[0].stackId,
                     source.StackId,
                     StringComparison.Ordinal))
        {
            failureReason = "apparel-rejected-source-lease-invalid:"
                + (revalidateFailure.IsFailure
                    ? revalidateFailure.Code.ToString()
                    : order.rejectedOutputLeaseId);
            return false;
        }

        if (!reservedDispositions.TryCommitReservedTransferPending(
                order.rejectedOutputLeaseId,
                1,
                operationId,
                ApparelRejectedDismantleOutbox.ReasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out failureReason))
        {
            return false;
        }
        return AdoptRejectedInputReceipt(
            order,
            operationId,
            receipt,
            out failureReason);
    }

    private static bool AdoptRejectedInputReceipt(
        ApparelWorkOrderSaveData order,
        string operationId,
        PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        bool valid = receipt.IsCommitted
            && receipt.Kind == PhysicalItemDispositionKind.Transfer
            && string.Equals(
                receipt.OperationId,
                operationId,
                StringComparison.Ordinal)
            && string.Equals(
                receipt.ReasonCode,
                ApparelRejectedDismantleOutbox.ReasonCode,
                StringComparison.Ordinal)
            && string.Equals(
                receipt.RequestFingerprint,
                ApparelRejectedDismantleOutbox.CreateRequestFingerprint(
                    order.rejectedOutputStackId),
                StringComparison.Ordinal)
            && receipt.SourceStackIds.Count == 1
            && string.Equals(
                receipt.SourceStackIds[0],
                order.rejectedOutputStackId,
                StringComparison.Ordinal)
            && receipt.Quantity == 1
            && receipt.InputMassGrams > 0L;
        if (!valid)
        {
            failureReason = "apparel-rejected-input-receipt-conflict";
            return false;
        }
        order.rejectedDismantleOperationId = receipt.OperationId;
        order.rejectedDismantleCommitId = receipt.CommitId;
        order.rejectedDismantleRequestFingerprint = receipt.RequestFingerprint;
        order.rejectedDismantleInputMassGrams = receipt.InputMassGrams;
        order.rejectedOutputConsumed = true;
        failureReason = string.Empty;
        return true;
    }

    private static bool AdoptRejectedFrozenAuthority(
        ApparelWorkOrderSaveData order,
        string batchCommitId,
        string outcomeFingerprint,
        ProductionOutputCapabilityDescriptor outputCapability,
        ProductionOutputBatchMaximumMassProof maximumMassProof,
        ProductionOutputBufferCapacitySourceSnapshot capacity,
        long outputMassGrams,
        out string failureReason)
    {
        bool empty = string.IsNullOrEmpty(order.rejectedRecoveryCommitId)
            && string.IsNullOrEmpty(order.rejectedRecoveryOutcomeFingerprint)
            && string.IsNullOrEmpty(
                order.rejectedRecoveryOutputCapability?.fingerprint)
            && string.IsNullOrEmpty(
                order.rejectedRecoveryMaximumMassProofDigest)
            && order.rejectedRecoveryMaximumBatchMassGrams == 0L
            && string.IsNullOrEmpty(order.rejectedRecoveryCapacitySourceDigest)
            && order.rejectedRecoveryRequiredMinimumCapacityGrams == 0L
            && order.rejectedRecoveryOutputMassGrams == 0L;
        if (!empty
            && (!string.Equals(
                    order.rejectedRecoveryCommitId,
                    batchCommitId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    order.rejectedRecoveryOutcomeFingerprint,
                    outcomeFingerprint,
                    StringComparison.Ordinal)
                || !string.Equals(
                    order.rejectedRecoveryOutputCapability?.fingerprint,
                    outputCapability.Fingerprint,
                    StringComparison.Ordinal)
                || !string.Equals(
                    order.rejectedRecoveryMaximumMassProofDigest,
                    maximumMassProof.SourceDigest,
                    StringComparison.Ordinal)
                || order.rejectedRecoveryMaximumBatchMassGrams
                    != maximumMassProof.MaximumBatchMassGrams
                || !string.Equals(
                    order.rejectedRecoveryCapacitySourceDigest,
                    capacity.SourceDigest,
                    StringComparison.Ordinal)
                || order.rejectedRecoveryRequiredMinimumCapacityGrams
                    != capacity.RequiredMinimumCapacityGrams
                || order.rejectedRecoveryOutputMassGrams
                    != outputMassGrams))
        {
            failureReason = "apparel-rejected-frozen-output-drift";
            return false;
        }
        order.rejectedRecoveryCommitId = batchCommitId;
        order.rejectedRecoveryOutcomeFingerprint = outcomeFingerprint;
        order.rejectedRecoveryOutputCapability =
            ProductionOutputCapabilitySaveData.Freeze(outputCapability);
        order.rejectedRecoveryMaximumMassProofDigest =
            maximumMassProof.SourceDigest;
        order.rejectedRecoveryMaximumBatchMassGrams =
            maximumMassProof.MaximumBatchMassGrams;
        order.rejectedRecoveryCapacitySourceDigest = capacity.SourceDigest;
        order.rejectedRecoveryRequiredMinimumCapacityGrams =
            capacity.RequiredMinimumCapacityGrams;
        order.rejectedRecoveryOutputMassGrams = outputMassGrams;
        failureReason = string.Empty;
        return true;
    }

    private static bool RejectedTokenMatchesOwner(
        ApparelWorkOrderSaveData order,
        FacilityBufferPlannedOutputToken token,
        FacilityBufferCapacityProfile profile,
        ProductionFacilityHandle handle,
        out string failureReason)
    {
        bool matches = string.Equals(
                token.TokenId,
                order.rejectedRecoveryAdmissionTokenId,
                StringComparison.Ordinal)
            && string.Equals(
                token.Request.PublicationOperationId,
                order.rejectedRecoveryOperationId,
                StringComparison.Ordinal)
            && string.Equals(
                token.Request.BatchCommitId,
                order.rejectedRecoveryCommitId,
                StringComparison.Ordinal)
            && string.Equals(
                token.Request.OutcomeFingerprint,
                order.rejectedRecoveryOutcomeFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                token.Request.DestinationId,
                profile.DestinationId,
                StringComparison.Ordinal)
            && token.Request.DropPosition == handle.Position
            && token.ReservedMassGrams
                == order.rejectedRecoveryOutputMassGrams
            && string.Equals(
                token.Request.CapacitySourceDigest,
                order.rejectedRecoveryCapacitySourceDigest,
                StringComparison.Ordinal)
            && token.Request.ExpectedMinimumCapacityGrams
                == order.rejectedRecoveryRequiredMinimumCapacityGrams
            && string.Equals(
                token.PlannedOutput.Fingerprint,
                order.rejectedRecoveryPlannedOutputFingerprint,
                StringComparison.Ordinal);
        failureReason = matches
            ? string.Empty
            : "apparel-rejected-admission-token-conflict";
        return matches;
    }

    private bool TryAdoptRejectedOutputs(
        ApparelWorkOrderSaveData order,
        string recoveryItemId,
        int quantity,
        long outputMassGrams,
        FacilityBufferPlannedOutputPublicationReceipt published,
        out string failureReason)
    {
        string[] stackIds = published.Stacks
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.StackId, StringComparer.Ordinal)
            .Select(value => value.StackId)
            .ToArray();
        if (stackIds.Length == 0
            || published.Stacks.Sum(value => (long)value.Quantity) != quantity
            || published.Stacks.Sum(value => value.MassGrams) != outputMassGrams
            || published.Stacks.Any(value => !string.Equals(
                value.ItemDefinitionId.Value,
                recoveryItemId,
                StringComparison.Ordinal)))
        {
            failureReason = "apparel-rejected-published-output-shape-conflict";
            return false;
        }
        WorldItemStackSnapshot[] outputs = items.GetAllStacks()
            .Where(value => value != null
                && stackIds.Contains(value.StackId, StringComparer.Ordinal))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        if (outputs.Length != stackIds.Length
            || outputs.Any(value => value.State
                    != WorldItemStackState.FacilityOutputBuffer
                || !string.Equals(
                    value.ItemId,
                    recoveryItemId,
                    StringComparison.Ordinal)))
        {
            failureReason = "apparel-rejected-published-output-conflict";
            return false;
        }
        order.rejectedRecoveryStackIds = stackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        order.rejectedMaterialSpawned = quantity;
        failureReason = string.Empty;
        return true;
    }

    private ApparelPhysicalTransactionResult TryReturnRejectedTerminalReplay(
        ApparelWorkOrderSaveData order,
        string recoveryItemId,
        long expectedOutputMassGrams)
    {
        if (!order.rejectedOutputConsumed
            || !order.rejectedDismantleAcknowledged
            || !order.rejectedRecoveryPublished
            || !order.rejectedRecoveryAdmissionCommitted
            || !order.rejectedRecoveryOutputAcknowledged
            || order.rejectedRecoveryOutputMassGrams
                != expectedOutputMassGrams)
        {
            return Conflict("apparel-rejected-terminal-owner-partial");
        }
        string[] expected = (order.rejectedRecoveryStackIds
                ?? new List<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        WorldItemStackSnapshot[] outputs = items.GetAllStacks()
            .Where(value => value != null
                && expected.Contains(value.StackId, StringComparer.Ordinal))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        if (order.rejectedMaterialAmount == 0)
        {
            if (expected.Length != 0)
                return Conflict("apparel-rejected-terminal-zero-output-conflict");
        }
        else if (outputs.Length != expected.Length
                 || outputs.Sum(value => (long)value.Quantity)
                    != order.rejectedMaterialAmount
                 || outputs.Any(value => !string.Equals(
                     value.ItemId,
                     recoveryItemId,
                     StringComparison.Ordinal))
                 || outputs.Sum(value => items.MassQuery.GetQuantityMass(
                         (ItemDefinitionId)value.ItemId,
                         PhysicalItemMassSubjectAdapter.Create(
                             items.MassQuery,
                             (ItemDefinitionId)value.ItemId,
                             value.ItemInstanceId,
                             value.Components),
                         value.Quantity).Value)
                    != expectedOutputMassGrams)
        {
            return Conflict("apparel-rejected-terminal-output-missing");
        }
        return new ApparelPhysicalTransactionResult(
            ApparelPhysicalTransactionStatus.Completed,
            expected.FirstOrDefault() ?? string.Empty,
            string.Empty,
            order.rejectedDismantleInputMassGrams,
            expectedOutputMassGrams,
            string.Empty);
    }

    private void ReleaseUnpublishedRejectedToken(
        ApparelWorkOrderSaveData order,
        FacilityBufferPlannedOutputToken token)
    {
        if (!string.IsNullOrEmpty(token.TokenId))
        {
            admission.TryReleasePlannedOutput(
                token,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _);
        }
        if (order.rejectedOutputConsumed)
            BeginNextRejectedPublicationAttempt(order);
    }

    private static void BeginNextRejectedPublicationAttempt(
        ApparelWorkOrderSaveData order)
    {
        order.rejectedRecoveryPublicationAttempt = checked(
            order.rejectedRecoveryPublicationAttempt + 1);
        order.rejectedRecoveryOperationId = string.Empty;
        order.rejectedRecoveryAdmissionTokenId = string.Empty;
        order.rejectedRecoveryPlannedOutputFingerprint = string.Empty;
    }

    private static string FormatRejectedPublicationOperationId(
        string orderId,
        int qualityAttempt,
        int publicationAttempt) =>
        $"{ApparelRejectedDismantleOutbox.RecoveryOperationPrefix}"
        + $"{orderId}:{Math.Max(0, qualityAttempt):D4}:"
        + $"{Math.Max(0, publicationAttempt):D4}";

    private static string CreateRejectedOutcomeFingerprint(
        ApparelWorkOrderSaveData order,
        string recoveryItemId,
        int quantity,
        long outputMassGrams)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("apparel-rejected-recovery@2");
        digest.Append(order.orderId);
        digest.Append(order.qualityAttemptIndex);
        digest.Append(order.rejectedOutputStackId);
        digest.Append(order.rejectedOutputInstanceId);
        digest.Append(recoveryItemId);
        digest.Append(quantity);
        digest.Append(outputMassGrams);
        return digest.ComputeSha256();
    }

    internal static void ClearCraftAttempt(ApparelWorkOrderSaveData order)
    {
        if (order == null)
            return;
        order.craftPublicationAttempt = 0;
        order.craftPublicationOperationId = string.Empty;
        order.craftOutputBatchCommitId = string.Empty;
        order.craftOutcomeFingerprint = string.Empty;
        order.craftOutputComponentFingerprint = string.Empty;
        order.craftOutputCapability = new ProductionOutputCapabilitySaveData();
        order.craftAdmissionTokenId = string.Empty;
        order.craftMaximumMassProofDigest = string.Empty;
        order.craftMaximumBatchMassGrams = 0L;
        order.craftCapacitySourceDigest = string.Empty;
        order.craftRequiredMinimumCapacityGrams = 0L;
        order.craftPlannedOutputFingerprint = string.Empty;
        order.craftOutputStackId = string.Empty;
        order.craftOutputInstanceId = string.Empty;
        order.craftInputCommitId = string.Empty;
        order.craftInputRequestFingerprint = string.Empty;
        order.craftInputMassGrams = 0L;
        order.craftOutputMassGrams = 0L;
        order.craftInputPending = false;
        order.craftOutputPublished = false;
        order.craftAdmissionCommitted = false;
        order.craftInputAcknowledged = false;
        order.craftOutputAcknowledged = false;
        order.craftMarketRouted = false;
    }

    internal static bool ValidateCraftOwnerShape(
        ApparelWorkOrderSaveData order,
        out string failureReason)
    {
        if (order == null)
        {
            failureReason = "apparel-craft-owner-null";
            return false;
        }
        bool started = order.craftPublicationAttempt != 0
            || !string.IsNullOrEmpty(order.craftPublicationOperationId)
            || !string.IsNullOrEmpty(order.craftOutputBatchCommitId)
            || !string.IsNullOrEmpty(order.craftOutcomeFingerprint)
            || !string.IsNullOrEmpty(order.craftOutputComponentFingerprint)
            || order.craftOutputCapability is { IsEmpty: false }
            || !string.IsNullOrEmpty(order.craftAdmissionTokenId)
            || !string.IsNullOrEmpty(order.craftMaximumMassProofDigest)
            || order.craftMaximumBatchMassGrams != 0L
            || !string.IsNullOrEmpty(order.craftCapacitySourceDigest)
            || order.craftRequiredMinimumCapacityGrams != 0L
            || !string.IsNullOrEmpty(order.craftPlannedOutputFingerprint)
            || !string.IsNullOrEmpty(order.craftOutputStackId)
            || !string.IsNullOrEmpty(order.craftOutputInstanceId)
            || !string.IsNullOrEmpty(order.craftInputCommitId)
            || !string.IsNullOrEmpty(order.craftInputRequestFingerprint)
            || order.craftInputMassGrams != 0L
            || order.craftOutputMassGrams != 0L
            || order.craftInputPending
            || order.craftOutputPublished
            || order.craftAdmissionCommitted
            || order.craftInputAcknowledged
            || order.craftOutputAcknowledged
            || order.craftMarketRouted;
        if (!started)
        {
            bool emptyCapability = order.craftOutputCapability == null
                || order.craftOutputCapability.IsEmpty;
            failureReason = emptyCapability
                ? string.Empty
                : "apparel-craft-owner-capability-without-attempt";
            return emptyCapability;
        }
        ProductionOutputCapabilitySaveData capability =
            order.craftOutputCapability;
        bool capabilityShapeValid = capability != null
            && !capability.IsEmpty
            && string.Equals(
                capability.outputLineId,
                OutputLineId,
                StringComparison.Ordinal)
            && string.Equals(
                capability.capabilityId,
                ProductionOutputCapabilityIds.ApparelWorkOrder,
                StringComparison.Ordinal)
            && Canonical(capability.itemId)
            && capability.capabilityVersion > 0
            && Canonical(capability.componentCodecId)
            && capability.componentCodecVersion > 0
            && Canonical(capability.fingerprint)
            && string.Equals(
                capability.fingerprint,
                ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    capability.outputLineId,
                    capability.itemId,
                    capability.capabilityId,
                    capability.capabilityVersion,
                    capability.componentCodecId,
                    capability.componentCodecVersion),
                StringComparison.Ordinal);
        bool outputIdsPaired = string.IsNullOrEmpty(order.craftOutputStackId)
            == string.IsNullOrEmpty(order.craftOutputInstanceId);
        bool valid = order.kind == ApparelWorkOrderKind.Craft
            && capabilityShapeValid
            && IsSha256(order.craftMaximumMassProofDigest)
            && order.craftMaximumBatchMassGrams > 0L
            && order.craftOutputMassGrams
                <= order.craftMaximumBatchMassGrams
            && order.craftPublicationAttempt >= 0
            && Canonical(order.craftOutputBatchCommitId)
            && Canonical(order.craftOutcomeFingerprint)
            && Canonical(order.craftOutputComponentFingerprint)
            && Canonical(order.craftCapacitySourceDigest)
            && Canonical(order.craftOutputInstanceId)
            && order.craftRequiredMinimumCapacityGrams > 0L
            && order.craftOutputMassGrams > 0L
            && outputIdsPaired
            && (!order.craftInputPending
                || (Canonical(order.craftInputCommitId)
                    && Canonical(order.craftInputRequestFingerprint)
                    && order.craftInputMassGrams > 0L))
            && (!order.craftOutputPublished
                || (Canonical(order.craftAdmissionTokenId)
                    && Canonical(order.craftPublicationOperationId)
                    && Canonical(order.craftPlannedOutputFingerprint)
                    && order.craftInputPending))
            && (!order.craftAdmissionCommitted
                || (order.craftOutputPublished
                    && Canonical(order.craftOutputStackId)))
            && (!order.craftInputAcknowledged
                || order.craftAdmissionCommitted)
            && (!order.craftOutputAcknowledged
                || (order.craftInputAcknowledged
                    && order.craftAdmissionCommitted))
            && (!order.craftMarketRouted || order.craftOutputAcknowledged);
        failureReason = valid
            ? string.Empty
            : "apparel-craft-owner-partial";
        return valid;
    }

    private bool TryGetOrReserveToken(
        ApparelWorkOrderSaveData order,
        ProductionFacilityHandle handle,
        FacilityBufferCapacityProfile profile,
        PhysicalItemMassSubject subject,
        ItemInstanceComponentSaveData component,
        out FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionFailureCode failureCode,
        out string failureReason)
    {
        token = default;
        failureCode = FacilityBufferMassAdmissionFailureCode.None;
        failureReason = string.Empty;
        if (!string.IsNullOrEmpty(order.craftAdmissionTokenId))
        {
            if (!admission.TryGetPlannedOutputToken(
                    order.craftAdmissionTokenId,
                    out token,
                    out FacilityBufferMassAdmissionTokenStatus status))
            {
                failureReason = "apparel-craft-admission-token-missing";
                return false;
            }
            if (status == FacilityBufferMassAdmissionTokenStatus.Released)
            {
                BeginNextPublicationAttempt(order);
            }
            else
            {
                return TokenMatchesOwner(order, token, profile, handle, out failureReason);
            }
        }

        string publicationOperationId = "apparel-craft-output-publication:"
            + order.orderId + ":"
            + order.qualityAttemptIndex.ToString("D4", CultureInfo.InvariantCulture)
            + ":"
            + order.craftPublicationAttempt.ToString("D4", CultureInfo.InvariantCulture);
        order.craftPublicationOperationId = publicationOperationId;
        FacilityBufferPlannedOutputRequest request = new(
            publicationOperationId,
            order.craftOutputBatchCommitId,
            order.craftOutcomeFingerprint,
            profile.DestinationId,
            handle.Position,
            profile.OwnerDomain,
            profile.OwnerOperationId,
            profile.OwnerFacilityId,
            profile.CapacityRevision,
            new[]
            {
                new FacilityBufferPlannedOutputSlice(
                    OutputLineId,
                    subject,
                    1,
                    new[] { component },
                    order.craftOutputComponentFingerprint)
            },
            order.craftCapacitySourceDigest,
            order.craftRequiredMinimumCapacityGrams,
            profile.AuthorityDigest);
        if (!admission.TryReservePlannedOutput(
                request,
                out token,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        order.craftAdmissionTokenId = token.TokenId;
        order.craftPlannedOutputFingerprint = token.PlannedOutput.Fingerprint;
        return true;
    }

    private bool TryCommitOrJoinInput(
        ApparelWorkOrderSaveData order,
        out string failureReason)
    {
        failureReason = string.Empty;
        List<PhysicalItemTransformInput> inputs = new();
        if (order.materialStackIds == null
            || order.materialStackAmounts == null
            || order.materialStackIds.Count != order.materialStackAmounts.Count)
        {
            failureReason = "apparel-craft-input-shape-invalid";
            return false;
        }
        for (int index = 0; index < order.materialStackIds.Count; index++)
        {
            string stackId = order.materialStackIds[index];
            int quantity = order.materialStackAmounts[index];
            if (!Canonical(stackId) || quantity <= 0)
            {
                failureReason = "apparel-craft-input-slice-invalid";
                return false;
            }
            inputs.Add(new PhysicalItemTransformInput(stackId, quantity));
        }
        string operationId = ApparelWorkOrderRuntime
            .BuildCraftMaterialOperationId(order);
        if (order.craftInputPending)
        {
            if (!dispositions.TryGetPending(
                    operationId,
                    out PhysicalItemBatchDispositionReceipt existing))
            {
                failureReason = "apparel-craft-input-pending-receipt-missing";
                return false;
            }
            return AdoptInputReceipt(order, operationId, existing, out failureReason);
        }
        quantityReservations.ReleaseByOwner(
            order.orderId,
            ItemReservationReleaseReason.Completed);
        if (!dispositions.TryCommitPending(
                inputs,
                PhysicalItemDispositionKind.Transfer,
                operationId,
                InputReasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out failureReason))
        {
            return false;
        }
        return AdoptInputReceipt(order, operationId, receipt, out failureReason);
    }

    private static bool AdoptInputReceipt(
        ApparelWorkOrderSaveData order,
        string operationId,
        PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        if (!receipt.IsCommitted
            || receipt.Kind != PhysicalItemDispositionKind.Transfer
            || !string.Equals(receipt.OperationId, operationId, StringComparison.Ordinal)
            || receipt.InputMassGrams <= 0L)
        {
            failureReason = "apparel-craft-input-receipt-conflict";
            return false;
        }
        order.craftInputCommitId = receipt.CommitId;
        order.craftInputRequestFingerprint = receipt.RequestFingerprint;
        order.craftInputMassGrams = receipt.InputMassGrams;
        order.craftInputPending = true;
        failureReason = string.Empty;
        return true;
    }

    private static bool AdoptFrozenAuthority(
        ApparelWorkOrderSaveData order,
        string batchCommitId,
        string outcomeFingerprint,
        string componentFingerprint,
        ProductionOutputCapabilityDescriptor outputCapability,
        ProductionOutputBatchMaximumMassProof maximumMassProof,
        ProductionOutputBufferCapacitySourceSnapshot capacity,
        long outputMassGrams,
        out string failureReason)
    {
        bool empty = string.IsNullOrEmpty(order.craftOutputBatchCommitId);
        if (!empty
            && (!string.Equals(order.craftOutputBatchCommitId, batchCommitId,
                    StringComparison.Ordinal)
                || !string.Equals(order.craftOutcomeFingerprint,
                    outcomeFingerprint, StringComparison.Ordinal)
                || !string.Equals(order.craftOutputComponentFingerprint,
                    componentFingerprint, StringComparison.Ordinal)
                || order.craftOutputCapability == null
                || !string.Equals(
                    order.craftOutputCapability.fingerprint,
                    outputCapability.Fingerprint,
                    StringComparison.Ordinal)
                || !string.Equals(
                    order.craftMaximumMassProofDigest,
                    maximumMassProof.SourceDigest,
                    StringComparison.Ordinal)
                || order.craftMaximumBatchMassGrams
                    != maximumMassProof.MaximumBatchMassGrams
                || !string.Equals(order.craftCapacitySourceDigest,
                    capacity.SourceDigest, StringComparison.Ordinal)
                || order.craftRequiredMinimumCapacityGrams
                    != capacity.RequiredMinimumCapacityGrams
                || order.craftOutputMassGrams != outputMassGrams))
        {
            failureReason = "apparel-craft-frozen-output-drift";
            return false;
        }
        order.craftOutputBatchCommitId = batchCommitId;
        order.craftOutcomeFingerprint = outcomeFingerprint;
        order.craftOutputComponentFingerprint = componentFingerprint;
        order.craftOutputCapability =
            ProductionOutputCapabilitySaveData.Freeze(outputCapability);
        order.craftMaximumMassProofDigest = maximumMassProof.SourceDigest;
        order.craftMaximumBatchMassGrams =
            maximumMassProof.MaximumBatchMassGrams;
        order.craftCapacitySourceDigest = capacity.SourceDigest;
        order.craftRequiredMinimumCapacityGrams =
            capacity.RequiredMinimumCapacityGrams;
        order.craftOutputMassGrams = outputMassGrams;
        failureReason = string.Empty;
        return true;
    }

    private static bool TokenMatchesOwner(
        ApparelWorkOrderSaveData order,
        FacilityBufferPlannedOutputToken token,
        FacilityBufferCapacityProfile profile,
        ProductionFacilityHandle handle,
        out string failureReason)
    {
        bool matches = string.Equals(token.TokenId,
                order.craftAdmissionTokenId, StringComparison.Ordinal)
            && string.Equals(token.Request.PublicationOperationId,
                order.craftPublicationOperationId, StringComparison.Ordinal)
            && string.Equals(token.Request.BatchCommitId,
                order.craftOutputBatchCommitId, StringComparison.Ordinal)
            && string.Equals(token.Request.OutcomeFingerprint,
                order.craftOutcomeFingerprint, StringComparison.Ordinal)
            && string.Equals(token.Request.DestinationId,
                profile.DestinationId, StringComparison.Ordinal)
            && token.Request.DropPosition == handle.Position
            && token.ReservedMassGrams == order.craftOutputMassGrams
            && string.Equals(token.Request.CapacitySourceDigest,
                order.craftCapacitySourceDigest, StringComparison.Ordinal)
            && token.Request.ExpectedMinimumCapacityGrams
                == order.craftRequiredMinimumCapacityGrams
            && string.Equals(token.PlannedOutput.Fingerprint,
                order.craftPlannedOutputFingerprint, StringComparison.Ordinal);
        failureReason = matches
            ? string.Empty
            : "apparel-craft-admission-token-conflict";
        return matches;
    }

    private static bool IsSha256(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');

    private static bool TryCreatePublicationReceipt(
        FacilityBufferPlannedOutputToken token,
        FacilityBufferPlannedOutputRestoreBatchSnapshot restored,
        out FacilityBufferPlannedOutputPublicationReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        if (restored == null
            || !string.Equals(restored.BatchCommitId,
                token.Request.BatchCommitId, StringComparison.Ordinal)
            || !string.Equals(restored.OutcomeFingerprint,
                token.Request.OutcomeFingerprint, StringComparison.Ordinal)
            || !string.Equals(restored.PlannedOutputFingerprint,
                token.PlannedOutput.Fingerprint, StringComparison.Ordinal)
            || restored.TotalMassGrams != token.ReservedMassGrams
            || restored.TotalQuantity != token.PlannedOutput.TotalQuantity
            || restored.Stacks.Any(value => value == null
                || value.State != WorldItemStackState.FacilityOutputBuffer
                || value.Position != token.Request.DropPosition
                || !string.Equals(value.DestinationId,
                    token.Request.DestinationId, StringComparison.Ordinal)))
        {
            failureReason = "apparel-craft-physical-ahead-conflict";
            return false;
        }
        FacilityBufferPublishedOutputStackReceipt[] stacks = restored.Stacks
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.StackOrdinal)
            .Select(value => new FacilityBufferPublishedOutputStackReceipt(
                value.StackId,
                value.OutputLineId,
                (ItemDefinitionId)value.ItemId,
                value.Quantity,
                new PhysicalMassGrams(value.MassGrams),
                value.ItemInstanceId))
            .ToArray();
        receipt = new FacilityBufferPlannedOutputPublicationReceipt(
            token.TokenId,
            token.Request.BatchCommitId,
            token.Request.OutcomeFingerprint,
            token.Request.DestinationId,
            token.Request.DropPosition,
            token.Request.ExpectedOwnerDomain,
            token.Request.ExpectedOwnerOperationId,
            token.Request.ExpectedOwnerFacilityId,
            token.Request.ExpectedCapacityRevision,
            token.PlannedOutput.Fingerprint,
            stacks);
        failureReason = string.Empty;
        return true;
    }

    private bool TryAdoptPublishedOutput(
        ApparelWorkOrderSaveData order,
        string outputItemId,
        FacilityBufferPlannedOutputPublicationReceipt published,
        out WorldItemStackSnapshot output,
        out string failureReason)
    {
        output = null;
        if (published.Stacks.Count != 1
            || published.Stacks[0].Quantity != 1
            || published.Stacks[0].MassGrams != order.craftOutputMassGrams
            || !string.Equals(published.Stacks[0].ItemDefinitionId.Value,
                outputItemId, StringComparison.Ordinal))
        {
            failureReason = "apparel-craft-published-output-shape-conflict";
            return false;
        }
        string stackId = published.Stacks[0].StackId;
        output = items.GetAllStacks().SingleOrDefault(value => value != null
            && string.Equals(value.StackId, stackId, StringComparison.Ordinal));
        if (output == null
            || output.Quantity != 1
            || !string.Equals(output.ItemId, outputItemId, StringComparison.Ordinal)
            || !string.Equals(output.ItemInstanceId,
                order.craftOutputInstanceId, StringComparison.Ordinal)
            || !ApparelItemStateCodec.TryRead(
                output.Components,
                out ApparelInstanceState _)
            || output.Components.Count(component => component != null
                && string.Equals(component.componentTypeId,
                    ItemInstanceComponentIds.Apparel,
                    StringComparison.Ordinal)) != 1
            || !string.Equals(
                CreateComponentFingerprint(
                    output.Components.Single(component => component != null
                        && string.Equals(component.componentTypeId,
                            ItemInstanceComponentIds.Apparel,
                            StringComparison.Ordinal))),
                order.craftOutputComponentFingerprint,
                StringComparison.Ordinal))
        {
            output = null;
            failureReason = "apparel-craft-published-output-conflict";
            return false;
        }
        order.craftOutputStackId = output.StackId;
        order.craftOutputInstanceId = output.ItemInstanceId;
        failureReason = string.Empty;
        return true;
    }

    private ApparelPhysicalTransactionResult TryReturnTerminalReplay(
        ApparelWorkOrderSaveData order,
        string outputItemId,
        ProductionFacilityHandle handle,
        bool markForSale)
    {
        if (!order.craftInputAcknowledged
            || !order.craftAdmissionCommitted
            || !Canonical(order.craftOutputStackId)
            || !Canonical(order.craftOutputInstanceId))
        {
            return Conflict("apparel-craft-terminal-owner-partial");
        }
        WorldItemStackSnapshot output = items.GetAllStacks().SingleOrDefault(value =>
            value != null
            && string.Equals(value.StackId,
                order.craftOutputStackId, StringComparison.Ordinal));
        if (output == null
            || output.Quantity != 1
            || !string.Equals(output.ItemId, outputItemId, StringComparison.Ordinal)
            || !string.Equals(output.ItemInstanceId,
                order.craftOutputInstanceId, StringComparison.Ordinal)
            || (markForSale
                ? output.State != WorldItemStackState.FacilityOutputBuffer
                : output.State != WorldItemStackState.Loose
                    || !string.IsNullOrEmpty(output.DestinationId))
            || items.MassQuery.GetQuantityMass(
                (ItemDefinitionId)output.ItemId,
                PhysicalItemMassSubjectAdapter.Create(
                    items.MassQuery,
                    (ItemDefinitionId)output.ItemId,
                    output.ItemInstanceId,
                    output.Components),
                1).Value != order.craftOutputMassGrams)
        {
            return Conflict("apparel-craft-terminal-output-missing");
        }
        if (markForSale && !order.craftMarketRouted)
        {
            if (!items.TryRouteStackToDestination(
                    output.StackId,
                    WorldItemStackState.FacilityOutputBuffer,
                    QualityRejectedOutputRules.MarketDestinationId,
                    handle.Position,
                    out string marketFailure))
            {
                return Pending("apparel-craft-market-route:" + marketFailure);
            }
            order.craftMarketRouted = true;
        }
        return new ApparelPhysicalTransactionResult(
            ApparelPhysicalTransactionStatus.Completed,
            output.StackId,
            output.ItemInstanceId,
            order.craftInputMassGrams,
            order.craftOutputMassGrams,
            string.Empty);
    }

    private void ReleaseUnpublishedToken(
        ApparelWorkOrderSaveData order,
        FacilityBufferPlannedOutputToken token)
    {
        if (!string.IsNullOrEmpty(token.TokenId))
        {
            admission.TryReleasePlannedOutput(
                token,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _);
        }
        BeginNextPublicationAttempt(order);
    }

    private static void BeginNextPublicationAttempt(ApparelWorkOrderSaveData order)
    {
        order.craftPublicationAttempt = checked(order.craftPublicationAttempt + 1);
        order.craftPublicationOperationId = string.Empty;
        order.craftAdmissionTokenId = string.Empty;
        order.craftPlannedOutputFingerprint = string.Empty;
    }

    private static string CreateOutcomeFingerprint(
        ApparelWorkOrderSaveData order,
        string outputItemId,
        string instanceId,
        string componentFingerprint,
        string capabilityFingerprint,
        bool markForSale,
        long outputMassGrams)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(OutcomeSchema);
        digest.Append(order.orderId);
        digest.Append(order.qualityAttemptIndex);
        digest.Append(outputItemId);
        digest.Append(instanceId);
        digest.Append(componentFingerprint);
        digest.Append(capabilityFingerprint);
        digest.Append(markForSale);
        digest.Append(outputMassGrams);
        return digest.ComputeSha256();
    }

    private static string CreateComponentFingerprint(
        ItemInstanceComponentSaveData component)
    {
        if (component == null)
            throw new ArgumentNullException(nameof(component));
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("apparel-craft-component@1");
        digest.Append(component.ToCanonicalString());
        return digest.ComputeSha256();
    }

    private static ApparelPhysicalTransactionResult Waiting(string reason) => new(
        ApparelPhysicalTransactionStatus.WaitingForOutputSpace,
        string.Empty,
        string.Empty,
        0L,
        0L,
        reason);

    private static ApparelPhysicalTransactionResult Pending(string reason) => new(
        ApparelPhysicalTransactionStatus.PendingFinalization,
        string.Empty,
        string.Empty,
        0L,
        0L,
        reason);

    private static ApparelPhysicalTransactionResult Conflict(string reason) => new(
        ApparelPhysicalTransactionStatus.Conflict,
        string.Empty,
        string.Empty,
        0L,
        0L,
        reason);

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
