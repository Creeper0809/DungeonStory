using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;
using DungeonStory.Foundation.Facts;
using static ApparelPhysicalTransactionSaveRules;

public sealed class ApparelPhysicalTransaction : IApparelPhysicalTransaction
{
    public const string OutputLineId =
        ApparelPhysicalTransactionSaveRules.OutputLineId;
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
    private readonly ApparelPhysicalPublicationRuntime observations;
    private readonly IGameCalendar calendar;

#if UNITY_EDITOR
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
        IProductionOutputMaximumMassRegistry outputMaximumMass) =>
        throw new InvalidOperationException(
            "Apparel physical transactions require their mandatory domain fact publication owner and game calendar.");
#endif

    [Inject]
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
        IProductionOutputMaximumMassRegistry outputMaximumMass,
        ApparelPhysicalPublicationRuntime observations,
        IGameCalendar calendar)
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
        this.observations = observations
            ?? throw new ArgumentNullException(nameof(observations));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
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
#if UNITY_EDITOR
    public ApparelPhysicalTransactionResult ExecuteCraftOrResume(
        ApparelWorkOrderSaveData order,
        BuildableObject facility,
        string outputItemId,
        ItemInstanceComponentSaveData frozenOutputComponent,
        bool markForSale)
    {
        CraftsmanshipQualityTier quality =
            ApparelItemStateCodec.TryRead(
                new[] { frozenOutputComponent },
                out ApparelInstanceState state)
                ? state.craftsmanshipQuality
                : CraftsmanshipQualityTier.Normal;
        return ExecuteCraftOrResume(
            order,
            facility,
            outputItemId,
            frozenOutputComponent,
            markForSale,
            "character:qa:apparel-maker",
            "시험 재봉사",
            quality,
            markForSale);
    }
#endif

    public ApparelPhysicalTransactionResult ExecuteCraftOrResume(
        ApparelWorkOrderSaveData order,
        BuildableObject facility,
        string outputItemId,
        ItemInstanceComponentSaveData frozenOutputComponent,
        bool markForSale,
        string makerCharacterId,
        string makerDisplayName,
        CraftsmanshipQualityTier quality,
        bool rejectedBelowMinimum)
    {
        bool legacyFrozen = order != null
            && !string.IsNullOrEmpty(order.craftOutputBatchCommitId)
            && order.craftQualityOutcomeSchemaVersion == 0;
        if (order == null
            || facility == null
            || order.kind != ApparelWorkOrderKind.Craft
            || !Canonical(order.orderId)
            || !Canonical(outputItemId)
            || frozenOutputComponent == null
            || !legacyFrozen
                && (!DomainFactValueSyntax.IsIdentity(makerCharacterId)
                    || string.IsNullOrWhiteSpace(makerDisplayName)
                    || !Enum.IsDefined(
                        typeof(CraftsmanshipQualityTier), quality)))
        {
            return Conflict("apparel-craft-physical-request-invalid");
        }

        ApparelPhysicalPublicationRuntime.Preparation observation = null;
        try
        {
            ProductionFacilityHandle handle = facilityHandles.CaptureFacility(facility);
            if (handle == null
                || handle.IsDestroyed
                || !handle.InstanceId.IsValid
                || !DomainFactValueSyntax.IsIdentity(handle.InstanceId.Value)
                || !DomainFactValueSyntax.IsIdentity(order.orderId)
                || !DomainFactValueSyntax.IsIdentity(outputItemId)
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
                outputMassGrams,
                makerCharacterId,
                makerDisplayName,
                quality,
                rejectedBelowMinimum);
            int absoluteDay = CurrentAbsoluteDay();
            if (!observations.TryPrepare(new ApparelPhysicalCompletedFact(new ApparelPhysicalCompletedFactData
            {
                operationId = batchCommitId, orderId = order.orderId, facilityId = handle.InstanceId.Value,
                facilityName = FacilityShopService.GetBuildingName(facility.BuildingData), outputItemId = outputItemId,
                kind = (int)ApparelPhysicalChangeKind.Craft, attemptIndex = order.qualityAttemptIndex,
                absoluteDay = absoluteDay, hasProductQuality = !legacyFrozen,
                makerId = legacyFrozen ? string.Empty : makerCharacterId,
                makerName = legacyFrozen ? string.Empty : makerDisplayName.Trim(),
                quality = legacyFrozen ? 0 : (int)quality, rejectedBelowMinimum = !legacyFrozen && rejectedBelowMinimum
            }), out observation, out string observationFailure))
                return Pending("apparel-craft-observation-reserve:" + observationFailure);
            if (observation.HasCompletedFact && !order.craftOutputAcknowledged)
                return Conflict("apparel-craft-completed-fact-owner-regressed");
            if (!AdoptFrozenAuthority(
                    order,
                    batchCommitId,
                    outcomeFingerprint,
                    componentFingerprint,
                    outputCapability,
                    maximumMassProof,
                    capacity,
                    outputMassGrams,
                    makerCharacterId,
                    makerDisplayName,
                    quality,
                    rejectedBelowMinimum,
                    out string frozenFailure))
            {
                return Conflict(frozenFailure);
            }

            if (order.craftOutputAcknowledged)
            {
                ApparelPhysicalTransactionResult replay = TryReturnTerminalReplay(
                    order,
                    outputItemId,
                    handle,
                    markForSale);
                return replay.IsCompleted
                    ? FinalizeObservation(observation, replay)
                    : replay;
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

            ApparelPhysicalTransactionResult completed = new(
                ApparelPhysicalTransactionStatus.Completed,
                output.StackId,
                output.ItemInstanceId,
                order.craftInputMassGrams,
                order.craftOutputMassGrams,
                string.Empty);
            return FinalizeObservation(observation, completed);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            return Conflict("apparel-craft-physical-exception:" + exception.Message);
        }
        finally
        {
            observation?.Dispose();
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

        ApparelPhysicalPublicationRuntime.Preparation observation = null;
        try
        {
            ProductionFacilityHandle handle = facilityHandles.CaptureFacility(facility);
            if (handle == null
                || handle.IsDestroyed
                || !handle.InstanceId.IsValid
                || !DomainFactValueSyntax.IsIdentity(handle.InstanceId.Value)
                || !DomainFactValueSyntax.IsIdentity(order.orderId)
                || !DomainFactValueSyntax.IsIdentity(recoveryItemId)
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
            int absoluteDay = CurrentAbsoluteDay();
            if (!observations.TryPrepare(new ApparelPhysicalCompletedFact(new ApparelPhysicalCompletedFactData
            {
                operationId = inputOperationId, orderId = order.orderId, facilityId = handle.InstanceId.Value,
                facilityName = FacilityShopService.GetBuildingName(facility.BuildingData), outputItemId = recoveryItemId,
                kind = (int)ApparelPhysicalChangeKind.RejectedDismantle, attemptIndex = order.qualityAttemptIndex,
                absoluteDay = absoluteDay
            }), out observation, out string observationFailure))
                return Pending("apparel-rejected-observation-reserve:" + observationFailure);
            if (observation.HasCompletedFact
                && (!order.rejectedRecoveryOutputAcknowledged || !order.rejectedDismantleAcknowledged))
                return Conflict("apparel-rejected-completed-fact-owner-regressed");

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
                ApparelPhysicalTransactionResult replay =
                    TryReturnRejectedTerminalReplay(
                    order,
                    recoveryItemId,
                    outputMassGrams);
                return replay.IsCompleted
                    ? FinalizeObservation(observation, replay)
                    : replay;
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

            ApparelPhysicalTransactionResult completed = new(
                ApparelPhysicalTransactionStatus.Completed,
                order.rejectedRecoveryStackIds?.FirstOrDefault()
                    ?? string.Empty,
                string.Empty,
                order.rejectedDismantleInputMassGrams,
                order.rejectedRecoveryOutputMassGrams,
                string.Empty);
            return FinalizeObservation(observation, completed);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            return Conflict(
                "apparel-rejected-physical-exception:" + exception.Message);
        }
        finally
        {
            observation?.Dispose();
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

    internal static void ClearCraftAttempt(ApparelWorkOrderSaveData order)
    {
        if (order == null)
            return;
        order.craftPublicationAttempt = 0;
        order.craftPublicationOperationId = string.Empty;
        order.craftOutputBatchCommitId = string.Empty;
        order.craftOutcomeFingerprint = string.Empty;
        order.craftOutputComponentFingerprint = string.Empty;
        order.craftQualityOutcomeSchemaVersion = 0;
        order.craftMakerCharacterId = string.Empty;
        order.craftMakerDisplayName = string.Empty;
        order.craftResolvedQuality = CraftsmanshipQualityTier.Normal;
        order.craftRejectedBelowMinimum = false;
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
        out string failureReason) =>
        ApparelPhysicalTransactionSaveRules.ValidateCraftOwnerShape(
            order,
            out failureReason);
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
        string makerCharacterId,
        string makerDisplayName,
        CraftsmanshipQualityTier quality,
        bool rejectedBelowMinimum,
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
                || order.craftOutputMassGrams != outputMassGrams
                || order.craftQualityOutcomeSchemaVersion == 1
                    && (!string.Equals(
                            order.craftMakerCharacterId,
                            makerCharacterId,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            order.craftMakerDisplayName,
                            makerDisplayName,
                            StringComparison.Ordinal)
                        || order.craftResolvedQuality != quality
                        || order.craftRejectedBelowMinimum
                            != rejectedBelowMinimum)))
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
        if (empty)
        {
            order.craftQualityOutcomeSchemaVersion = 1;
            order.craftMakerCharacterId = makerCharacterId.Trim();
            order.craftMakerDisplayName = makerDisplayName.Trim();
            order.craftResolvedQuality = quality;
            order.craftRejectedBelowMinimum = rejectedBelowMinimum;
        }
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

    private static ApparelPhysicalTransactionResult FinalizeObservation(
        ApparelPhysicalPublicationRuntime.Preparation observation,
        in ApparelPhysicalTransactionResult completed)
    {
        DomainFactDelivery delivery = observation.Complete(completed);
        return delivery.Completed ? completed : Pending(delivery.Detail);
    }

    private int CurrentAbsoluteDay() => calendar.Day;

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

}
