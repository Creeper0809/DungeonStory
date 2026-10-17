using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Coordinates replacement output admission, body CAS, installation and recovery.
/// Persistent phases and proofs remain on the existing surgery order and parts.
/// </summary>
internal sealed class SurgicalPartReplacementTransaction : ISurgicalPartReplacementRuntime
{
    private readonly SurgicalPartInstallationRuntime installations;
    private readonly IPhysicalItemMassQuery physicalMass;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;

    internal SurgicalPartReplacementTransaction(
        SurgicalPartInstallationRuntime installations,
        IPhysicalItemMassQuery physicalMass,
        IPhysicalItemBatchDispositionService batchDispositions)
    {
        this.installations = installations ?? throw new ArgumentNullException(nameof(installations));
        this.physicalMass = physicalMass ?? throw new ArgumentNullException(nameof(physicalMass));
        this.batchDispositions = batchDispositions
            ?? throw new ArgumentNullException(nameof(batchDispositions));
    }

    bool ISurgicalPartReplacementRuntime.TryReserveReplacementOutput(
        SurgeryOrder order,
        AnatomyNodeHealthState currentNode,
        Vector2Int outputPosition,
        IFacilityBufferMassAdmissionService admission,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (order == null
            || currentNode == null
            || admission == null
            || order.replacementPhase is not
                (SurgicalPartReplacementPhase.None
                or SurgicalPartReplacementPhase.OutputReservationPending)
            || order.replacementPhase ==
                    SurgicalPartReplacementPhase.OutputReservationPending
                && !string.Equals(
                    order.replacementIncomingPartId,
                    order.selectedPartInstanceId,
                    StringComparison.Ordinal)
            || !order.OwnsMaterialAuthority
            || string.IsNullOrWhiteSpace(currentNode.installedPartId)
            || !installations.TryGet(
                currentNode.installedPartId,
                out SurgicalPartInstance previous)
            || !previous.installed
            || !string.Equals(
                previous.installedSubjectId,
                order.subject?.subjectId,
                StringComparison.Ordinal)
            || !string.Equals(
                previous.nodeId,
                order.targetNodeId,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(previous.itemDefinitionId)
            || string.IsNullOrWhiteSpace(previous.physicalItemInstanceId)
            || !string.Equals(
                currentNode.installedPartId,
                order.replacementExpectedOldPartId,
                StringComparison.Ordinal)
            || !installations.TryValidateReservationForOrder(
                order.selectedPartInstanceId,
                order.orderId,
                out _))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                currentNode?.installedPartId ?? string.Empty,
                "replacement-owner-invalid");
            return false;
        }

        string operationId = SurgicalPartReplacementIdentity
            .FormatOperationId(order.orderId);
        string batchCommitId = SurgicalPartReplacementIdentity
            .FormatBatchCommitId(order.orderId);
        if (order.replacementReservationAttempt <= 0)
        {
            order.replacementReservationAttempt = 1;
        }
        if (string.IsNullOrEmpty(order.replacementPublicationOperationId))
        {
            order.replacementPublicationOperationId =
                SurgicalPartReplacementIdentity.FormatPublicationOperationId(
                    order.orderId,
                    order.replacementReservationAttempt);
        }
        bool preservePendingIntent = order.replacementPhase ==
            SurgicalPartReplacementPhase.OutputReservationPending;
        order.replacementDetachedCurrentHealth = currentNode.currentHealth;
        order.replacementDetachedMaxHealth = currentNode.maxHealth;
        if (!TryCreateReplacementOutputRequest(
                order,
                previous,
                outputPosition,
                operationId,
                batchCommitId,
                outcomeFingerprint: string.Empty,
                out FacilityBufferPlannedOutputRequest request,
                out failure))
        {
            if (!preservePendingIntent)
            {
                order.replacementDetachedCurrentHealth = 0f;
                order.replacementDetachedMaxHealth = 0f;
            }
            return false;
        }

        if (!admission.TryReservePlannedOutput(
                request,
                out FacilityBufferPlannedOutputToken token,
                out FacilityBufferMassAdmissionFailureCode code,
                out string reason))
        {
            failure = new DomainFailure(
                code == FacilityBufferMassAdmissionFailureCode
                    .CapacityUnavailable
                    ? FailureCode.ProductionOutputSpaceUnavailable
                    : FailureCode.ProductionOutputUnavailable,
                order.orderId,
                reason ?? string.Empty);
            if (!preservePendingIntent)
            {
                order.replacementDetachedCurrentHealth = 0f;
                order.replacementDetachedMaxHealth = 0f;
            }
            return false;
        }

        order.replacementPhase = SurgicalPartReplacementPhase.OutputReserved;
        order.replacementOperationId = operationId;
        order.replacementExpectedOldPartId = previous.partInstanceId;
        order.replacementIncomingPartId = order.selectedPartInstanceId;
        order.replacementAdmissionTokenId = token.TokenId;
        order.replacementPublicationOperationId =
            request.PublicationOperationId;
        order.replacementBatchCommitId = batchCommitId;
        order.replacementOutcomeFingerprint = request.OutcomeFingerprint;
        order.replacementPlannedOutputFingerprint =
            token.PlannedOutput.Fingerprint;
        order.replacementOutputX = outputPosition.x;
        order.replacementOutputY = outputPosition.y;
        order.replacementOutputMassGrams = token.ReservedMassGrams;
        return true;
    }

    bool ISurgicalPartReplacementRuntime.TryCommitReplacement(
        SurgeryOrder order,
        CharacterActor character,
        SurgicalPartKind incomingKind,
        float efficiency,
        IAnatomyHealthRuntime anatomy,
        IFacilityBufferMassAdmissionService admission,
        IFacilityBufferPlannedOutputPublicationService publication,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (order == null
            || character == null
            || anatomy == null
            || admission == null
            || publication == null
            || order.replacementPhase is SurgicalPartReplacementPhase.None
                or SurgicalPartReplacementPhase.Completed
            || !installations.TryGet(
                order.replacementExpectedOldPartId,
                out SurgicalPartInstance previous)
            || !installations.TryGet(
                order.replacementIncomingPartId,
                out SurgicalPartInstance incoming))
        {
            failure = ReplacementFailure(
                order?.orderId,
                "replacement-transaction-invalid");
            return false;
        }

        string subjectId = character.Identity?.PersistentId ?? string.Empty;
        bool bodyCommitted = order.replacementPhase is
            SurgicalPartReplacementPhase.BodyCommitted
            or SurgicalPartReplacementPhase.OutputPublished;
        if (bodyCommitted)
        {
            AnatomyNodeHealthState bodyOwner = anatomy
                .GetAnatomySnapshot(character)
                .Nodes.FirstOrDefault(node => node != null
                    && string.Equals(
                        node.nodeId,
                        order.targetNodeId,
                        StringComparison.Ordinal));
            if (bodyOwner == null
                || !string.Equals(
                    bodyOwner.installedPartId,
                    incoming.partInstanceId,
                    StringComparison.Ordinal)
                || bodyOwner.installedPartKind != incomingKind)
            {
                failure = ReplacementFailure(
                    order.orderId,
                    "replacement-body-owner-conflict");
                return false;
            }
        }
        if (!installations.TryValidateForInstallation(
                incoming.partInstanceId,
                order.orderId,
                subjectId,
                allowExpiredCommittedPart: bodyCommitted,
                out incoming,
                out failure))
        {
            return false;
        }

        if (order.replacementPhase ==
            SurgicalPartReplacementPhase.OutputReservationPending)
        {
            AnatomyNodeHealthState current = anatomy
                .GetAnatomySnapshot(character)
                .Nodes.FirstOrDefault(node => node != null
                    && string.Equals(
                        node.nodeId,
                        order.targetNodeId,
                        StringComparison.Ordinal));
            if (!((ISurgicalPartReplacementRuntime)this)
                    .TryReserveReplacementOutput(
                        order,
                        current,
                        new Vector2Int(
                            order.replacementOutputX,
                            order.replacementOutputY),
                        admission,
                        out failure))
            {
                return false;
            }
        }

        FacilityBufferPlannedOutputToken token = default;
        FacilityBufferMassAdmissionTokenStatus tokenStatus =
            FacilityBufferMassAdmissionTokenStatus.Released;
        if (order.replacementPhase ==
            SurgicalPartReplacementPhase.OutputReserved)
        {
            if (!TryGetOrRestoreReplacementToken(
                    order,
                    previous,
                    admission,
                    out token,
                    out tokenStatus,
                    out failure))
            {
                return false;
            }
            bool preserveDurability =
                incoming.detachedDurabilityMaximum > 0f;
            if (!anatomy.TryReplaceNodePart(
                    character,
                    order.targetNodeId,
                    previous.partInstanceId,
                    order.replacementDetachedCurrentHealth,
                    order.replacementDetachedMaxHealth,
                    incoming.partInstanceId,
                    incomingKind,
                    efficiency,
                    incoming.detachedDurabilityCurrent,
                    preserveDurability,
                    out AnatomyNodeHealthState replaced,
                    out failure))
            {
                AnatomyNodeHealthState current = anatomy
                    .GetAnatomySnapshot(character)
                    .Nodes.FirstOrDefault(node => node != null
                        && string.Equals(
                            node.nodeId,
                            order.targetNodeId,
                            StringComparison.Ordinal));
                if (current != null
                    && string.Equals(
                        current.installedPartId,
                        previous.partInstanceId,
                        StringComparison.Ordinal)
                    && (current.currentHealth !=
                            order.replacementDetachedCurrentHealth
                        || current.maxHealth !=
                            order.replacementDetachedMaxHealth))
                {
                    DomainFailure originalFailure = failure;
                    if (!TryRefreshReplacementReservation(
                            order,
                            current,
                            token.Request.DropPosition,
                            admission,
                            out DomainFailure refreshFailure))
                    {
                        failure = refreshFailure;
                        return false;
                    }
                    failure = originalFailure;
                }
                return false;
            }
            if (replaced.currentHealth !=
                    order.replacementDetachedCurrentHealth
                || replaced.maxHealth != order.replacementDetachedMaxHealth)
            {
                failure = ReplacementFailure(
                    order.orderId,
                    "replacement-detached-health-conflict");
                return false;
            }
            order.replacementPhase = SurgicalPartReplacementPhase.BodyCommitted;
        }

        if (!installations.TryPrepareForInstallation(
                incoming.partInstanceId,
                order.orderId,
                subjectId,
                allowExpiredCommittedPart: true,
                out incoming,
                out failure))
        {
            return false;
        }
        if (!SurgicalPartInstallationOutbox.TryFinalizePending(
                incoming,
                batchDispositions,
                out string installationFailure))
        {
            failure = ReplacementFailure(order.orderId, installationFailure);
            return false;
        }
        character.Stats.RefreshDerivedMaximumHealthPreservingCurrent();

        if (publication.TryCaptureBatch(
                order.replacementBatchCommitId,
                allowAcknowledged: true,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot restored,
                out bool acknowledged,
                out _,
                out string captureFailure))
        {
            if (!ValidateReplacementPublication(
                    order,
                    previous,
                    restored,
                    out string restoredJoinFailure))
            {
                failure = ReplacementFailure(
                    order.orderId,
                    restoredJoinFailure);
                return false;
            }
            AdoptReplacementPublication(order, previous, restored.Stacks.Single());

            if (admission.TryGetPlannedOutputToken(
                    order.replacementAdmissionTokenId,
                    out token,
                    out tokenStatus))
            {
                if (tokenStatus == FacilityBufferMassAdmissionTokenStatus.Released
                    || !ReplacementTokenMatches(order, previous, token))
                {
                    failure = ReplacementFailure(
                        order.orderId,
                        "replacement-admission-state-conflict");
                    return false;
                }
                if (tokenStatus == FacilityBufferMassAdmissionTokenStatus.Reserved)
                {
                    if (!TryCreateReplacementPublicationReceipt(
                            token,
                            restored,
                            out FacilityBufferPlannedOutputPublicationReceipt
                                restoredReceipt,
                            out string restoredReceiptFailure)
                        || !admission.TryCommitPlannedOutput(
                            token,
                            restoredReceipt,
                            out _,
                            out _,
                            out restoredReceiptFailure))
                    {
                        failure = ReplacementFailure(
                            order.orderId,
                            restoredReceiptFailure);
                        return false;
                    }
                }
            }

            if (!acknowledged
                && !publication.TryAcknowledgeRestoreCandidate(
                    restored,
                    out _,
                    out string restoreAcknowledgementFailure))
            {
                failure = ReplacementFailure(
                    order.orderId,
                    restoreAcknowledgementFailure);
                return false;
            }
            order.replacementPhase = SurgicalPartReplacementPhase.Completed;
            return true;
        }
        if (!captureFailure.StartsWith(
                "planned-output-batch-missing:",
                StringComparison.Ordinal))
        {
            failure = ReplacementFailure(order.orderId, captureFailure);
            return false;
        }
        if (order.replacementPhase ==
            SurgicalPartReplacementPhase.OutputPublished)
        {
            failure = ReplacementFailure(
                order.orderId,
                "replacement-published-output-missing");
            return false;
        }

        if (string.IsNullOrEmpty(token.TokenId)
            && !TryGetOrRestoreReplacementToken(
                order,
                previous,
                admission,
                out token,
                out tokenStatus,
                out failure))
        {
            return false;
        }
        if (!publication.TryPublishFullBatch(
                token,
                out FacilityBufferPlannedOutputPublicationReceipt published,
                out _,
                out string publicationFailure))
        {
            failure = ReplacementFailure(order.orderId, publicationFailure);
            return false;
        }
        if (!ValidateReplacementPublication(
                order,
                previous,
                published,
                out string joinFailure))
        {
            failure = ReplacementFailure(order.orderId, joinFailure);
            return false;
        }
        AdoptReplacementPublication(order, previous, published.Stacks.Single());

        if (tokenStatus != FacilityBufferMassAdmissionTokenStatus.Routed
            && !admission.TryCommitPlannedOutput(
                token,
                published,
                out _,
                out _,
                out string commitFailure))
        {
            failure = ReplacementFailure(order.orderId, commitFailure);
            return false;
        }
        if (!publication.TryAcknowledgePublishedBatch(
                published,
                out _,
                out string acknowledgementFailure))
        {
            failure = ReplacementFailure(
                order.orderId,
                acknowledgementFailure);
            return false;
        }

        order.replacementPhase = SurgicalPartReplacementPhase.Completed;
        return true;
    }

    bool ISurgicalPartReplacementRuntime.TryAbortReplacement(
        SurgeryOrder order,
        IFacilityBufferMassAdmissionService admission,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null)
        {
            return true;
        }
        if (order.replacementPhase == SurgicalPartReplacementPhase.None)
        {
            ClearReplacement(order);
            return true;
        }
        if (order.replacementPhase !=
                SurgicalPartReplacementPhase.OutputReserved
            || admission == null)
        {
            failureReason = "replacement-abort-not-safe";
            return false;
        }
        if (admission.TryGetPlannedOutputToken(
                order.replacementAdmissionTokenId,
                out FacilityBufferPlannedOutputToken token,
                out FacilityBufferMassAdmissionTokenStatus status)
            && status != FacilityBufferMassAdmissionTokenStatus.Released
            && !admission.TryReleasePlannedOutput(
                token,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out failureReason))
        {
            return false;
        }
        ClearReplacement(order);
        return true;
    }

    private bool TryGetOrRestoreReplacementToken(
        SurgeryOrder order,
        SurgicalPartInstance previous,
        IFacilityBufferMassAdmissionService admission,
        out FacilityBufferPlannedOutputToken token,
        out FacilityBufferMassAdmissionTokenStatus status,
        out DomainFailure failure)
    {
        token = default;
        status = FacilityBufferMassAdmissionTokenStatus.Released;
        failure = DomainFailure.None;
        if (admission.TryGetPlannedOutputToken(
                order.replacementAdmissionTokenId,
                out token,
                out status))
        {
            if (status != FacilityBufferMassAdmissionTokenStatus.Released
                && ReplacementTokenMatches(order, previous, token))
            {
                return true;
            }
            failure = ReplacementFailure(
                order.orderId,
                "replacement-admission-state-conflict");
            return false;
        }

        Vector2Int outputPosition = new(
            order.replacementOutputX,
            order.replacementOutputY);
        if (!TryCreateReplacementOutputRequest(
                order,
                previous,
                outputPosition,
                order.replacementOperationId,
                order.replacementBatchCommitId,
                order.replacementOutcomeFingerprint,
                out FacilityBufferPlannedOutputRequest request,
                out failure))
        {
            return false;
        }
        if (!admission.TryReservePlannedOutput(
                request,
                out token,
                out FacilityBufferMassAdmissionFailureCode code,
                out string reason))
        {
            failure = new DomainFailure(
                code == FacilityBufferMassAdmissionFailureCode
                    .CapacityUnavailable
                    ? FailureCode.ProductionOutputSpaceUnavailable
                    : FailureCode.ProductionOutputUnavailable,
                order.orderId,
                reason ?? string.Empty);
            return false;
        }
        if (token.ReservedMassGrams != order.replacementOutputMassGrams
            || !string.Equals(
                token.PlannedOutput.Fingerprint,
                order.replacementPlannedOutputFingerprint,
                StringComparison.Ordinal))
        {
            admission.TryReleasePlannedOutput(
                token,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _);
            failure = ReplacementFailure(
                order.orderId,
                "replacement-restored-reservation-drift");
            return false;
        }
        order.replacementAdmissionTokenId = token.TokenId;
        status = FacilityBufferMassAdmissionTokenStatus.Reserved;
        return true;
    }

    private bool TryRefreshReplacementReservation(
        SurgeryOrder order,
        AnatomyNodeHealthState current,
        Vector2Int outputPosition,
        IFacilityBufferMassAdmissionService admission,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string expectedOldPartId = order.replacementExpectedOldPartId;
        string incomingPartId = order.replacementIncomingPartId;
        int nextAttempt = checked(order.replacementReservationAttempt + 1);
        if (!((ISurgicalPartReplacementRuntime)this).TryAbortReplacement(
                order,
                admission,
                out string abortFailure))
        {
            failure = ReplacementFailure(order.orderId, abortFailure);
            return false;
        }
        order.replacementPhase =
            SurgicalPartReplacementPhase.OutputReservationPending;
        order.replacementOperationId = SurgicalPartReplacementIdentity
            .FormatOperationId(order.orderId);
        order.replacementExpectedOldPartId = expectedOldPartId;
        order.replacementIncomingPartId = incomingPartId;
        order.replacementReservationAttempt = nextAttempt;
        order.replacementPublicationOperationId =
            SurgicalPartReplacementIdentity.FormatPublicationOperationId(
                order.orderId,
                nextAttempt);
        order.replacementBatchCommitId = SurgicalPartReplacementIdentity
            .FormatBatchCommitId(order.orderId);
        order.replacementOutputX = outputPosition.x;
        order.replacementOutputY = outputPosition.y;
        order.replacementDetachedCurrentHealth = current.currentHealth;
        order.replacementDetachedMaxHealth = current.maxHealth;
        return ((ISurgicalPartReplacementRuntime)this).TryReserveReplacementOutput(
            order,
            current,
            outputPosition,
            admission,
            out failure);
    }

    private bool TryCreateReplacementOutputRequest(
        SurgeryOrder order,
        SurgicalPartInstance previous,
        Vector2Int outputPosition,
        string operationId,
        string batchCommitId,
        string outcomeFingerprint,
        out FacilityBufferPlannedOutputRequest request,
        out DomainFailure failure)
    {
        request = default;
        failure = DomainFailure.None;
        try
        {
            FacilityBufferCapacityProfile profile =
                new FacilityBufferCapacityProfile(
                    order.materialDestinationId,
                    outputPosition,
                    SurgeryMaterialDestinationAuthority.OwnerDomain,
                    order.orderId,
                    order.facilityId,
                    new PhysicalMassGrams(order.materialBufferCapacityGrams),
                    SurgeryMaterialDestinationAuthority
                        .InputBufferCapacitySchemaRevision);
            List<ItemInstanceComponentSaveData> components =
                CreateRecoveryPhysicalComponents(
                    previous,
                    order,
                    operationId,
                    order.replacementDetachedCurrentHealth,
                    order.replacementDetachedMaxHealth);
            PhysicalItemMassSubject subject =
                PhysicalItemMassSubjectAdapter.Create(
                    physicalMass,
                    (ItemDefinitionId)previous.itemDefinitionId,
                    previous.physicalItemInstanceId,
                    components);
            string computedOutcome = CreateReplacementOutcomeFingerprint(
                order,
                previous,
                order.replacementDetachedCurrentHealth,
                order.replacementDetachedMaxHealth,
                components);
            if (!string.IsNullOrEmpty(outcomeFingerprint)
                && !string.Equals(
                    outcomeFingerprint,
                    computedOutcome,
                    StringComparison.Ordinal))
            {
                failure = ReplacementFailure(
                    order.orderId,
                    "replacement-outcome-fingerprint-drift");
                return false;
            }
            string frozenOutcome = computedOutcome;
            request = new FacilityBufferPlannedOutputRequest(
                order.replacementPublicationOperationId,
                batchCommitId,
                frozenOutcome,
                order.materialDestinationId,
                outputPosition,
                profile.OwnerDomain,
                profile.OwnerOperationId,
                profile.OwnerFacilityId,
                profile.CapacityRevision,
                new[]
                {
                    new FacilityBufferPlannedOutputSlice(
                        SurgicalPartReplacementIdentity.OutputLineId,
                        subject,
                        1,
                        components)
                });
            if (order.replacementReservationAttempt <= 0
                || !string.Equals(
                    order.replacementPublicationOperationId,
                    SurgicalPartReplacementIdentity.FormatPublicationOperationId(
                        order.orderId,
                        order.replacementReservationAttempt),
                    StringComparison.Ordinal))
            {
                failure = ReplacementFailure(
                    order.orderId,
                    "replacement-publication-operation-drift");
                request = default;
                return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                order.orderId,
                exception.Message);
            return false;
        }
    }

    private static bool ValidateReplacementPublication(
        SurgeryOrder order,
        SurgicalPartInstance previous,
        FacilityBufferPlannedOutputPublicationReceipt published,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (published.Stacks.Count == 1
            && string.Equals(
                published.AdmissionTokenId,
                order.replacementAdmissionTokenId,
                StringComparison.Ordinal)
            && string.Equals(
                published.BatchCommitId,
                order.replacementBatchCommitId,
                StringComparison.Ordinal)
            && string.Equals(
                published.OutcomeFingerprint,
                order.replacementOutcomeFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                published.DestinationId,
                order.materialDestinationId,
                StringComparison.Ordinal)
            && string.Equals(
                published.Stacks[0].OutputLineId,
                SurgicalPartReplacementIdentity.OutputLineId,
                StringComparison.Ordinal)
            && string.Equals(
                published.Stacks[0].ItemDefinitionId.Value,
                previous.itemDefinitionId,
                StringComparison.Ordinal)
            && string.Equals(
                published.Stacks[0].ItemInstanceId,
                previous.physicalItemInstanceId,
                StringComparison.Ordinal)
            && published.Stacks[0].Quantity == 1
            && published.Stacks[0].MassGrams ==
                order.replacementOutputMassGrams)
        {
            return true;
        }
        failureReason = "replacement-publication-join-invalid";
        return false;
    }

    private static bool ValidateReplacementPublication(
        SurgeryOrder order,
        SurgicalPartInstance previous,
        FacilityBufferPlannedOutputRestoreBatchSnapshot restored,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (restored != null
            && restored.Stacks.Count == 1
            && restored.TotalQuantity == 1
            && restored.TotalMassGrams == order.replacementOutputMassGrams
            && string.Equals(restored.BatchCommitId,
                order.replacementBatchCommitId, StringComparison.Ordinal)
            && string.Equals(restored.OutcomeFingerprint,
                order.replacementOutcomeFingerprint, StringComparison.Ordinal)
            && string.Equals(restored.PlannedOutputFingerprint,
                order.replacementPlannedOutputFingerprint,
                StringComparison.Ordinal)
            && string.Equals(restored.Stacks[0].OutputLineId,
                SurgicalPartReplacementIdentity.OutputLineId,
                StringComparison.Ordinal)
            && string.Equals(restored.Stacks[0].ItemId,
                previous.itemDefinitionId, StringComparison.Ordinal)
            && string.Equals(restored.Stacks[0].ItemInstanceId,
                previous.physicalItemInstanceId, StringComparison.Ordinal)
            && restored.Stacks[0].Quantity == 1
            && restored.Stacks[0].MassGrams ==
                order.replacementOutputMassGrams
            && restored.Stacks[0].State ==
                WorldItemStackState.FacilityOutputBuffer
            && restored.Stacks[0].Position == new Vector2Int(
                order.replacementOutputX,
                order.replacementOutputY)
            && string.Equals(restored.Stacks[0].DestinationId,
                order.materialDestinationId, StringComparison.Ordinal))
        {
            return true;
        }
        failureReason = "replacement-restored-publication-join-invalid";
        return false;
    }

    private static bool TryCreateReplacementPublicationReceipt(
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
            failureReason = "replacement-physical-ahead-conflict";
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

    private static void AdoptReplacementPublication(
        SurgeryOrder order,
        SurgicalPartInstance previous,
        FacilityBufferPublishedOutputStackReceipt output) =>
        AdoptReplacementPublication(
            order,
            previous,
            output.StackId,
            output.ItemInstanceId);

    private static void AdoptReplacementPublication(
        SurgeryOrder order,
        SurgicalPartInstance previous,
        FacilityBufferPlannedOutputRestoreStackSnapshot output) =>
        AdoptReplacementPublication(
            order,
            previous,
            output.StackId,
            output.ItemInstanceId);

    private static void AdoptReplacementPublication(
        SurgeryOrder order,
        SurgicalPartInstance previous,
        string stackId,
        string itemInstanceId)
    {
        previous.worldStackId = stackId;
        previous.storedFacilityId = string.Empty;
        previous.reservedOrderId = order.orderId;
        previous.installed = false;
        previous.installedSubjectId = string.Empty;
        previous.detachedDurabilityCurrent =
            order.replacementDetachedCurrentHealth;
        previous.detachedDurabilityMaximum =
            order.replacementDetachedMaxHealth;
        previous.recoveryOperationId = order.replacementOperationId;
        previous.recoveryOrderId = order.orderId;
        previous.recoveryCommitId = order.replacementBatchCommitId;
        SurgicalPartInstallationRuntime.ClearInstallationIntent(previous);
        order.replacementOutputStackId = stackId;
        order.replacementOutputItemInstanceId = itemInstanceId;
        order.replacementPhase = SurgicalPartReplacementPhase.OutputPublished;
    }

    private static bool ReplacementTokenMatches(
        SurgeryOrder order,
        SurgicalPartInstance previous,
        FacilityBufferPlannedOutputToken token) =>
        string.Equals(token.TokenId, order.replacementAdmissionTokenId,
            StringComparison.Ordinal)
        && string.Equals(token.Request.BatchCommitId,
            order.replacementBatchCommitId, StringComparison.Ordinal)
        && string.Equals(token.Request.OutcomeFingerprint,
            order.replacementOutcomeFingerprint, StringComparison.Ordinal)
        && string.Equals(token.Request.PublicationOperationId,
            order.replacementPublicationOperationId, StringComparison.Ordinal)
        && string.Equals(token.Request.DestinationId,
            order.materialDestinationId, StringComparison.Ordinal)
        && token.Request.DropPosition == new Vector2Int(
            order.replacementOutputX,
            order.replacementOutputY)
        && token.ReservedMassGrams == order.replacementOutputMassGrams
        && string.Equals(token.PlannedOutput.Fingerprint,
            order.replacementPlannedOutputFingerprint,
            StringComparison.Ordinal)
        && token.PlannedOutput.Slices.Count == 1
        && string.Equals(token.PlannedOutput.Slices[0].ItemDefinitionId.Value,
            previous.itemDefinitionId, StringComparison.Ordinal)
        && string.Equals(token.PlannedOutput.Slices[0].Source.Subject?.ItemInstanceId,
            previous.physicalItemInstanceId, StringComparison.Ordinal);

    private static List<ItemInstanceComponentSaveData>
        CreateRecoveryPhysicalComponents(
            SurgicalPartInstance part,
            SurgeryOrder order,
            string operationId,
            float currentHealth,
            float maxHealth)
    {
        List<ItemInstanceComponentSaveData> components =
            CreateBasePhysicalComponents(part);
        components.Add(SurgicalPartRecoveryComponentCodec.Create(
            part,
            order.orderId,
            operationId,
            currentHealth,
            maxHealth));
        return components;
    }

    private static List<ItemInstanceComponentSaveData>
        CreateBasePhysicalComponents(SurgicalPartInstance part)
    {
        List<ItemInstanceComponentSaveData> components = new();
        if (!string.IsNullOrWhiteSpace(part.sourceProductionCommitId))
        {
            components.Add(SurgicalPartPreparedOutputComponentCodec.Create(
                new SurgicalPartPreparedOutput
                {
                    ItemId = part.itemDefinitionId,
                    PhysicalItemInstanceId = part.physicalItemInstanceId,
                    PartInstanceId = part.partInstanceId,
                    NodeId = part.nodeId,
                    DisplayName = part.displayName,
                    Kind = part.kind,
                    Quality = part.quality,
                    CommitId = part.sourceProductionCommitId,
                    IsReplay = true
                }));
        }
        return components;
    }

    private static string CreateReplacementOutcomeFingerprint(
        SurgeryOrder order,
        SurgicalPartInstance part,
        float currentHealth,
        float maxHealth,
        IEnumerable<ItemInstanceComponentSaveData> components)
    {
        string canonical = string.Join("|", new[]
        {
            "surgical-part-replacement-v1",
            order.orderId,
            order.subject?.subjectId ?? string.Empty,
            order.targetNodeId,
            order.selectedPartInstanceId,
            part.partInstanceId,
            part.itemDefinitionId,
            part.physicalItemInstanceId,
            currentHealth.ToString("R", CultureInfo.InvariantCulture),
            maxHealth.ToString("R", CultureInfo.InvariantCulture),
            string.Join(";", (components
                    ?? Array.Empty<ItemInstanceComponentSaveData>())
                .Select(component => component.ToCanonicalString())
                .OrderBy(value => value, StringComparer.Ordinal))
        });
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        StringBuilder hex = new(digest.Length * 2);
        foreach (byte value in digest)
            hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return hex.ToString();
    }

    internal static bool ReplacementOutcomeFingerprintMatches(
        SurgeryOrder order,
        SurgicalPartInstance part)
    {
        if (order == null || part == null)
            return false;
        try
        {
            return string.Equals(
                order.replacementOutcomeFingerprint,
                CreateReplacementOutcomeFingerprintForValidation(order, part),
                StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            return false;
        }
    }

    internal static string CreateReplacementOutcomeFingerprintForValidation(
        SurgeryOrder order,
        SurgicalPartInstance part)
    {
        List<ItemInstanceComponentSaveData> components =
            CreateRecoveryPhysicalComponents(
                part,
                order,
                order.replacementOperationId,
                order.replacementDetachedCurrentHealth,
                order.replacementDetachedMaxHealth);
        return CreateReplacementOutcomeFingerprint(
            order,
            part,
            order.replacementDetachedCurrentHealth,
            order.replacementDetachedMaxHealth,
            components);
    }

    private static DomainFailure ReplacementFailure(
        string orderId,
        string detail) => new(
        FailureCode.ProductionOutputUnavailable,
        orderId ?? string.Empty,
        detail ?? string.Empty);

    private static void ClearReplacement(SurgeryOrder order)
    {
        order.replacementPhase = SurgicalPartReplacementPhase.None;
        order.replacementOperationId = string.Empty;
        order.replacementExpectedOldPartId = string.Empty;
        order.replacementIncomingPartId = string.Empty;
        order.replacementAdmissionTokenId = string.Empty;
        order.replacementPublicationOperationId = string.Empty;
        order.replacementReservationAttempt = 0;
        order.replacementBatchCommitId = string.Empty;
        order.replacementOutcomeFingerprint = string.Empty;
        order.replacementPlannedOutputFingerprint = string.Empty;
        order.replacementOutputX = 0;
        order.replacementOutputY = 0;
        order.replacementOutputStackId = string.Empty;
        order.replacementOutputItemInstanceId = string.Empty;
        order.replacementOutputMassGrams = 0L;
        order.replacementDetachedCurrentHealth = 0f;
        order.replacementDetachedMaxHealth = 0f;
    }

}
