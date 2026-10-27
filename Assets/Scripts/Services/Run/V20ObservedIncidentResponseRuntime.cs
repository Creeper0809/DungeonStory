using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;

internal sealed class V20ObservedIncidentResponseRuntime
{
    private readonly SocietyResponseMovementProvenanceRuntime responseMovements;
    private readonly IContentResolutionService contentResolution;
    private readonly IGameCalendar calendar;
    private readonly IRunSeedProvider runSeed;
    private readonly IGameEventBus events;
    private readonly IV20MilestoneWorldSnapshotQuery milestoneProjector;
    private readonly ISocietyEventQuery societyEvents;
    private readonly ISocietyObservedIncidentResponseCommand incidentResponses;
    private readonly V20StoryContentCatalog storyCatalog;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly ICharacterBodyHealthQuery bodyHealth;
    private readonly ICharacterConsumablesApplication consumables;
    private readonly ICharacterMedicalCommand medicalCommands;
    private readonly ICharacterMedicalQuery medicalQuery;
    private readonly ICharacterLifetimeQuery characterLifetime;
    private readonly Func<IExteriorIncidentRuntime> exteriorIncidents;
    private bool advancingIncidentResponses;
    private readonly Dictionary<string, StartedIncidentTransferAttempt> startedIncidentTransferOperations =
        new(StringComparer.Ordinal);

    private readonly struct StartedIncidentTransferAttempt
    {
        internal readonly AbilityMove Movement;
        internal readonly int Version;

        internal StartedIncidentTransferAttempt(AbilityMove movement, int version)
        {
            Movement = movement;
            Version = version;
        }
    }

    internal V20ObservedIncidentResponseRuntime(
        IContentResolutionService contentResolution,
        IGameCalendar calendar,
        IRunSeedProvider runSeed,
        IGameEventBus events,
        IV20MilestoneWorldSnapshotQuery milestoneProjector,
        ISocietyEventQuery societyEvents,
        ISocietyObservedIncidentResponseCommand incidentResponses,
        V20StoryContentCatalog storyCatalog,
        IFacilityCapabilityQuery facilities,
        ICharacterBodyHealthQuery bodyHealth,
        ICharacterConsumablesApplication consumables,
        ICharacterMedicalCommand medicalCommands,
        ICharacterMedicalQuery medicalQuery,
        ICharacterLifetimeQuery characterLifetime,
        Func<IExteriorIncidentRuntime> exteriorIncidents,
        SocietyResponseMovementProvenanceRuntime responseMovements)
    {
        this.responseMovements = responseMovements ?? throw new ArgumentNullException(nameof(responseMovements));
        this.contentResolution = contentResolution ?? throw new ArgumentNullException(nameof(contentResolution));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.runSeed = runSeed ?? throw new ArgumentNullException(nameof(runSeed));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.milestoneProjector = milestoneProjector ?? throw new ArgumentNullException(nameof(milestoneProjector));
        this.societyEvents = societyEvents ?? throw new ArgumentNullException(nameof(societyEvents));
        this.incidentResponses = incidentResponses ?? throw new ArgumentNullException(nameof(incidentResponses));
        this.storyCatalog = storyCatalog ?? throw new ArgumentNullException(nameof(storyCatalog));
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        this.bodyHealth = bodyHealth ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.consumables = consumables ?? throw new ArgumentNullException(nameof(consumables));
        this.medicalCommands = medicalCommands ?? throw new ArgumentNullException(nameof(medicalCommands));
        this.medicalQuery = medicalQuery ?? throw new ArgumentNullException(nameof(medicalQuery));
        this.characterLifetime = characterLifetime ?? throw new ArgumentNullException(nameof(characterLifetime));
        this.exteriorIncidents = exteriorIncidents ?? throw new ArgumentNullException(nameof(exteriorIncidents));
    }

    public void Tick()
    {
        if (advancingIncidentResponses)
        {
            return;
        }
        advancingIncidentResponses = true;
        try
        {
            V20ActiveEventSaveData[] pending = societyEvents.ActiveSocietyEvents
                         .Where(value => value?.hasObservedMealIncident == true
                             && value.observedIncidentResponse?.phase is
                                 ObservedIncidentResponsePhase.Accepted
                                 or ObservedIncidentResponsePhase
                                     .ExternalOperationStarted
                                 or ObservedIncidentResponsePhase
                                     .ReceiptCommitted)
                         .OrderBy(
                             value => value.instanceId,
                             StringComparer.Ordinal)
                         .ToArray();
            ForgetAbandonedTransferAttempts(pending);
            foreach (V20ActiveEventSaveData active in pending)
            {
                AdvanceObservedIncidentResponse(active);
            }
        }
        finally
        {
            advancingIncidentResponses = false;
        }
    }

    private void AdvanceObservedIncidentResponse(
        V20ActiveEventSaveData active)
    {
        ObservedIncidentResponseSaveData response =
            active.observedIncidentResponse;
        if (response.phase == ObservedIncidentResponsePhase.ReceiptCommitted)
        {
            TryFinalizeObservedIncidentResponse(active, response);
            return;
        }
        if (string.Equals(response.choiceId, "replace", StringComparison.Ordinal))
        {
            AdvanceReplacementMealResponse(active, response);
            return;
        }
        if (string.Equals(
                response.choiceId,
                "emergency-care",
                StringComparison.Ordinal))
        {
            AdvanceEmergencyCareResponse(active, response);
            return;
        }
        if (string.Equals(response.choiceId, "transfer", StringComparison.Ordinal))
        {
            AdvanceIsolationRecoveryTransferResponse(active, response);
        }
    }

    private void AdvanceReplacementMealResponse(
        V20ActiveEventSaveData active,
        ObservedIncidentResponseSaveData response)
    {
        CharacterId customerId = new(
            active.observedMealIncident.targetCharacterId);
        BuildingInstanceId facilityId = new(
            active.observedMealIncident.facilityInstanceId);
        ConsumableOperationId operationId = new(response.operationId);
        bool completed = consumables.TryConsumePermittedMeal(
            operationId,
            customerId,
            facilityId,
            out CharacterConsumablesMealResult mealResult);
        if (response.phase == ObservedIncidentResponsePhase.Accepted)
        {
            if (!completed && !mealResult.IsAcceptedPending)
            {
                TryFailObservedIncidentResponse(
                    active,
                    response,
                    "replacement-meal-start-failed:"
                        + mealResult.FailureCode,
                    cancelled: false);
                return;
            }
            if (!incidentResponses.TryRecordObservedIncidentResponseStarted(
                    active.instanceId,
                    response.operationId,
                    response.operationId,
                    out _))
            {
                return;
            }
        }
        if (!completed)
        {
            if (!mealResult.IsAcceptedPending)
            {
                TryFailObservedIncidentResponse(
                    active,
                    response,
                    "replacement-meal-operation-failed:"
                        + mealResult.FailureCode,
                    cancelled: false);
            }
            return;
        }
        if (!mealResult.OperationId.Equals(operationId)
            || !mealResult.ItemStackId.IsValid
            || mealResult.PolicyViolation
            || mealResult.Contaminated)
        {
            TryFailObservedIncidentResponse(
                active,
                response,
                "replacement-meal-receipt-invalid",
                cancelled: false);
            return;
        }
        if (incidentResponses.TryRecordObservedIncidentResponseReceipt(
                active.instanceId,
                response.operationId,
                response.operationId,
                FormatReplacementMealReceiptId(
                    response.operationId,
                    mealResult.ItemStackId),
                out _))
        {
            TryFinalizeObservedIncidentResponse(active, response);
        }
    }

    private void AdvanceEmergencyCareResponse(
        V20ActiveEventSaveData active,
        ObservedIncidentResponseSaveData response)
    {
        CharacterId patientId = new(
            active.observedMealIncident.targetCharacterId);
        if (response.phase == ObservedIncidentResponsePhase.Accepted)
        {
            CharacterActor patient = milestoneProjector.LivingCharacters
                .FirstOrDefault(actor => actor != null
                    && CharacterPersistentIdentity.TryGet(
                        actor,
                        out CharacterId id)
                    && id.Equals(patientId));
            if (patient == null || patient.IsDead)
            {
                TryFailObservedIncidentResponse(
                    active,
                    response,
                    "emergency-care-patient-unavailable",
                    cancelled: patient?.IsDead == true);
                return;
            }
            if (!medicalCommands.TryRequestTreatment(
                    patient,
                    out CharacterMedicalOrder requested,
                    out DomainFailure requestFailure)
                || requested == null
                || string.IsNullOrWhiteSpace(requested.orderId)
                || !string.Equals(
                    requested.patientId,
                    patientId.Value,
                    StringComparison.Ordinal))
            {
                TryFailObservedIncidentResponse(
                    active,
                    response,
                    "emergency-care-request-failed:"
                        + (requestFailure.IsFailure
                            ? requestFailure.Code.ToString()
                            : "invalid-order"),
                    cancelled: false);
                return;
            }
            requested.societyResponseOperationId = response.operationId;
            requested.societyResponseReceiptId = string.Empty;
            if (!incidentResponses.TryRecordObservedIncidentResponseStarted(
                    active.instanceId,
                    response.operationId,
                    requested.orderId,
                    out string startFailure))
            {
                requested.societyResponseOperationId = string.Empty;
                requested.societyResponseReceiptId = string.Empty;
                TryFailObservedIncidentResponse(
                    active,
                    response,
                    "emergency-care-owner-commit-failed:" + startFailure,
                    cancelled: false);
                return;
            }
            response = societyEvents.ActiveSocietyEvents
                .First(value => string.Equals(
                    value.instanceId,
                    active.instanceId,
                    StringComparison.Ordinal))
                .observedIncidentResponse;
        }

        if (!medicalQuery.TryGetOrder(
                response.externalOperationId,
                out CharacterMedicalOrder order)
            || order == null
            || !string.Equals(
                order.patientId,
                patientId.Value,
                StringComparison.Ordinal)
            || !string.Equals(
                order.societyResponseOperationId,
                response.operationId,
                StringComparison.Ordinal))
        {
            TryFailObservedIncidentResponse(
                active,
                response,
                "emergency-care-order-missing-or-conflicted",
                cancelled: false);
            return;
        }
        if (order.state == CharacterMedicalOrderState.Cancelled)
        {
            TryFailObservedIncidentResponse(
                active,
                response,
                "emergency-care-order-cancelled",
                cancelled: true);
            return;
        }
        if (!order.stabilized)
        {
            return;
        }
        string receiptId = $"medical-stabilized:{order.orderId}";
        order.societyResponseReceiptId = receiptId;
        if (incidentResponses.TryRecordObservedIncidentResponseReceipt(
                active.instanceId,
                response.operationId,
                order.orderId,
                receiptId,
                out string receiptFailure))
        {
            TryFinalizeObservedIncidentResponse(active, response);
            return;
        }
        TryFailObservedIncidentResponse(
            active,
            response,
            "emergency-care-receipt-commit-failed:" + receiptFailure,
            cancelled: false);
    }

    private void AdvanceIsolationRecoveryTransferResponse(
        V20ActiveEventSaveData active,
        ObservedIncidentResponseSaveData response)
    {
        CharacterId customerId = new(
            active.observedMealIncident.targetCharacterId);
        CharacterActor customer = milestoneProjector.LivingCharacters
            .FirstOrDefault(actor => actor != null
                && actor.Identity?.CharacterType == CharacterType.Customer
                && CharacterPersistentIdentity.TryGet(
                    actor,
                    out CharacterId id)
                && id.Equals(customerId));
        if (customer == null || customer.IsDead)
        {
            TryFailObservedIncidentResponse(
                active,
                response,
                customer?.IsDead == true
                    ? "isolation-transfer-customer-dead"
                    : "isolation-transfer-customer-unavailable",
                cancelled: customer?.IsDead == true);
            return;
        }

        bool startedMedicalTransfer = response.phase
                != ObservedIncidentResponsePhase.Accepted
            && V20ObservedEventEvidenceRules.TryClassifyObservedIncidentTransferExternalOperationId(
                    active,
                    response.externalOperationId,
                    out bool medicalOrder)
            && medicalOrder;
        bool useMedicalTransfer = response.phase
                == ObservedIncidentResponsePhase.Accepted
            ? bodyHealth.GetSnapshot(customer).Downed
            : startedMedicalTransfer;
        if (useMedicalTransfer)
        {
            if (response.phase == ObservedIncidentResponsePhase.Accepted)
            {
                if (!medicalCommands.TryRequestTreatment(
                        customer,
                        out CharacterMedicalOrder requested,
                        out DomainFailure requestFailure)
                    || requested == null
                    || !requested.IsActive
                    || string.IsNullOrWhiteSpace(requested.orderId)
                    || !V20ObservedEventEvidenceRules.TryClassifyObservedIncidentTransferExternalOperationId(
                            active,
                            requested.orderId,
                            out bool requestedMedicalOrder)
                    || !requestedMedicalOrder
                    || !string.Equals(
                        requested.patientId,
                        customerId.Value,
                        StringComparison.Ordinal))
                {
                    TryFailObservedIncidentResponse(
                        active,
                        response,
                        "isolation-transfer-treatment-request-failed:"
                            + (requestFailure.IsFailure
                                ? requestFailure.Code.ToString()
                                : "invalid-order"),
                        cancelled: false);
                    return;
                }

                requested.societyResponseOperationId = response.operationId;
                requested.societyResponseReceiptId = string.Empty;
                if (!incidentResponses.TryRecordObservedIncidentResponseStarted(
                        active.instanceId,
                        response.operationId,
                        requested.orderId,
                        out string startFailure))
                {
                    requested.societyResponseOperationId = string.Empty;
                    requested.societyResponseReceiptId = string.Empty;
                    TryFailObservedIncidentResponse(
                        active,
                        response,
                        "isolation-transfer-owner-commit-failed:"
                            + startFailure,
                        cancelled: false);
                    return;
                }
                response = societyEvents.ActiveSocietyEvents
                    .First(value => string.Equals(
                        value.instanceId,
                        active.instanceId,
                        StringComparison.Ordinal))
                    .observedIncidentResponse;
                BuildableObject selectedTreatmentFacility = null;
                string lastAssignmentFailure = string.Empty;
                foreach (BuildableObject facility in facilities
                             .FindOperational(
                                 FacilityCapabilityKind.IsolationRecovery)
                             .Where(value => value != null
                                 && value.PersistentInstanceId.IsValid)
                             .OrderBy(value => Mathf.Abs(
                                     value.centerPos.x - customer.GetNowXY().x)
                                 + Mathf.Abs(
                                     value.centerPos.y - customer.GetNowXY().y))
                             .ThenBy(
                                 value => value.PersistentInstanceId.Value,
                                 StringComparer.Ordinal))
                {
                    if (medicalCommands.TryAssignSpecificTreatmentFacility(
                            requested.orderId,
                            facility,
                            out DomainFailure assignmentFailure))
                    {
                        selectedTreatmentFacility = facility;
                        break;
                    }
                    lastAssignmentFailure = assignmentFailure.IsFailure
                        ? assignmentFailure.Code.ToString()
                        : "invalid-assignment";
                }
                if (selectedTreatmentFacility == null)
                {
                    TryFailObservedIncidentResponse(
                        active,
                        response,
                        string.IsNullOrEmpty(lastAssignmentFailure)
                            ? "isolation-recovery-facility-unavailable"
                            : "isolation-transfer-treatment-facility-"
                                + "assignment-failed:"
                                + lastAssignmentFailure,
                        cancelled: false);
                    return;
                }
            }

            if (!medicalQuery.TryGetOrder(
                    response.externalOperationId,
                    out CharacterMedicalOrder order)
                || order == null
                || !string.Equals(
                    order.orderId,
                    response.externalOperationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    order.patientId,
                    customerId.Value,
                    StringComparison.Ordinal)
                || !string.Equals(
                    order.societyResponseOperationId,
                    response.operationId,
                    StringComparison.Ordinal))
            {
                TryFailObservedIncidentResponse(
                    active,
                    response,
                    "isolation-transfer-medical-order-missing-or-conflicted",
                    cancelled: false);
                return;
            }
            if (order.state == CharacterMedicalOrderState.Cancelled)
            {
                TryFailObservedIncidentResponse(
                    active,
                    response,
                    "isolation-transfer-medical-order-cancelled",
                    cancelled: true);
                return;
            }
            if (!medicalQuery.TryGetPatient(
                    order,
                    out CharacterActor trackedPatient)
                || trackedPatient == null
                || trackedPatient.IsDead
                || !CharacterPersistentIdentity.TryGet(
                    trackedPatient,
                    out CharacterId trackedPatientId)
                || !trackedPatientId.Equals(customerId))
            {
                TryFailObservedIncidentResponse(
                    active,
                    response,
                    trackedPatient?.IsDead == true
                        ? "isolation-transfer-customer-dead"
                        : "isolation-transfer-medical-patient-mismatch",
                    cancelled: trackedPatient?.IsDead == true);
                return;
            }
            if (!medicalQuery.TryGetTreatmentFacility(
                    order,
                    out BuildableObject treatmentFacility)
                || treatmentFacility == null)
            {
                TryFailObservedIncidentResponse(
                    active,
                    response,
                    "isolation-recovery-facility-unavailable-after-start",
                    cancelled: false);
                return;
            }
            if (!treatmentFacility.PersistentInstanceId.IsValid
                || !string.Equals(
                    order.treatmentFacilityId,
                    treatmentFacility.PersistentInstanceId.Value,
                    StringComparison.Ordinal))
            {
                TryFailObservedIncidentResponse(
                    active,
                    response,
                    "isolation-transfer-medical-order-facility-mismatch",
                    cancelled: false);
                return;
            }
            if (!facilities
                    .FindOperational(FacilityCapabilityKind.IsolationRecovery)
                    .Any(value => value != null
                        && value.PersistentInstanceId.Equals(
                            treatmentFacility.PersistentInstanceId)))
            {
                TryFailObservedIncidentResponse(
                    active,
                    response,
                    "isolation-recovery-facility-unavailable-after-start",
                    cancelled: false);
                return;
            }

            bool treatmentPlacementCompleted = order.state is
                    CharacterMedicalOrderState.Treating
                    or CharacterMedicalOrderState.Recovering
                    or CharacterMedicalOrderState.Completed;
            if (!treatmentPlacementCompleted)
            {
                return;
            }
            if (order.carried
                || order.PatientPosition != treatmentFacility.centerPos
                || order.BedPosition != treatmentFacility.centerPos)
            {
                TryFailObservedIncidentResponse(
                    active,
                    response,
                    "isolation-transfer-medical-order-destination-mismatch",
                    cancelled: false);
                return;
            }

            string receiptId = V20ObservedEventEvidenceRules.FormatObservedIncidentTransferReceiptId(
                    order.orderId,
                    customerId);
            order.societyResponseReceiptId = receiptId;
            if (incidentResponses.TryRecordObservedIncidentResponseReceipt(
                    active.instanceId,
                    response.operationId,
                    order.orderId,
                    receiptId,
                    out string receiptFailure))
            {
                TryFinalizeObservedIncidentResponse(active, response);
                return;
            }
            TryFailObservedIncidentResponse(
                active,
                response,
                "isolation-transfer-medical-receipt-commit-failed:"
                    + receiptFailure,
                cancelled: false);
            return;
        }

        AbilityMove move = customer.GetAbility<AbilityMove>();
        if (move == null)
        {
            TryFailObservedIncidentResponse(
                active,
                response,
                "isolation-transfer-customer-unavailable",
                cancelled: false);
            return;
        }

        if (response.phase == ObservedIncidentResponsePhase.Accepted)
        {
            string lastMoveFailure = string.Empty;
            foreach (BuildableObject facility in facilities
                         .FindOperational(
                             FacilityCapabilityKind.IsolationRecovery)
                         .Where(value => value != null
                             && value.PersistentInstanceId.IsValid)
                         .OrderBy(value => Mathf.Abs(
                                 value.centerPos.x - customer.GetNowXY().x)
                             + Mathf.Abs(
                                 value.centerPos.y - customer.GetNowXY().y))
                         .ThenBy(
                             value => value.PersistentInstanceId.Value,
                             StringComparer.Ordinal))
            {
                if (!facility.TryGetNearestWorkAccessGridPosition(
                        facility.Grid,
                        customer.GetNowXY(),
                        out Vector2Int candidateDestination))
                {
                    lastMoveFailure = "isolation-transfer-access-cell-unavailable";
                    continue;
                }
                string externalOperationId = V20ObservedEventEvidenceRules.FormatObservedIncidentTransferOperationId(
                        response.operationId,
                        facility.PersistentInstanceId,
                        new CoreGridCell(
                            candidateDestination.x,
                            candidateDestination.y));
                if (!responseMovements.TryStartSystemMove(move,
                        response.operationId,
                        externalOperationId,
                        facility.PersistentInstanceId,
                        candidateDestination,
                        DoorAccessOverrideKind.None,
                        out string moveFailure))
                {
                    lastMoveFailure = string.IsNullOrWhiteSpace(moveFailure)
                        ? "isolation-transfer-route-unavailable"
                        : "isolation-transfer-route-unavailable:"
                            + moveFailure.Trim();
                    continue;
                }
                if (!incidentResponses.TryRecordObservedIncidentResponseStarted(
                        active.instanceId,
                        response.operationId,
                        externalOperationId,
                        out _))
                {
                    move.CancelActiveMovement(
                        "isolation-transfer-owner-commit-failed");
                    return;
                }
                startedIncidentTransferOperations[externalOperationId] =
                    new StartedIncidentTransferAttempt(
                        move, move.MovementOperationVersionForDiagnostics);
                response = societyEvents.ActiveSocietyEvents
                    .First(value => string.Equals(
                        value.instanceId,
                        active.instanceId,
                        StringComparison.Ordinal))
                    .observedIncidentResponse;
                break;
            }
            if (response.phase == ObservedIncidentResponsePhase.Accepted)
            {
                TryFailObservedIncidentResponse(
                    active,
                    response,
                    string.IsNullOrEmpty(lastMoveFailure)
                        ? "isolation-recovery-facility-unavailable"
                        : lastMoveFailure,
                    cancelled: false);
                return;
            }
        }

        if (!V20ObservedEventEvidenceRules.TryParseObservedIncidentTransferOperationId(
                response.operationId,
                response.externalOperationId,
                out BuildingInstanceId facilityId,
                out CoreGridCell savedDestination))
        {
            TryFailObservedIncidentResponse(
                active,
                response,
                "isolation-transfer-operation-invalid",
                cancelled: false);
            return;
        }
        Vector2Int destination = new(savedDestination.X, savedDestination.Y);
        BuildableObject selectedFacility = facilities
            .FindOperational(FacilityCapabilityKind.IsolationRecovery)
            .SingleOrDefault(value => value != null
                && value.PersistentInstanceId.Equals(facilityId));
        if (selectedFacility == null
            || !selectedFacility.IsWorkAccessGridPosition(
                selectedFacility.Grid,
                destination))
        {
            if (OwnsTransferMovement(move, response, facilityId, destination)
                && move.IsSystemMoveInProgressTo(destination))
            {
                move.CancelActiveMovement("isolation-transfer-facility-lost");
            }
            startedIncidentTransferOperations.Remove(
                response.externalOperationId);
            TryFailObservedIncidentResponse(
                active,
                response,
                "isolation-recovery-facility-unavailable-after-start",
                cancelled: false);
            return;
        }

        if (customer.GetNowXY() == destination)
        {
            string receiptId = V20ObservedEventEvidenceRules.FormatObservedIncidentTransferReceiptId(
                    response.externalOperationId,
                    customerId);
            startedIncidentTransferOperations.Remove(
                response.externalOperationId);
            if (!responseMovements.TryRecordReceipt(move,
                    response.operationId,
                    response.externalOperationId,
                    receiptId,
                    out string movementReceiptFailure))
            {
                TryFailObservedIncidentResponse(
                    active,
                    response,
                    movementReceiptFailure,
                    cancelled: false);
                return;
            }
            if (incidentResponses.TryRecordObservedIncidentResponseReceipt(
                active.instanceId,
                response.operationId,
                response.externalOperationId,
                receiptId,
                out string receiptFailure))
            {
                TryFinalizeObservedIncidentResponse(active, response);
                return;
            }
            TryFailObservedIncidentResponse(
                active,
                response,
                "isolation-transfer-receipt-commit-failed:" + receiptFailure,
                cancelled: false);
            return;
        }
        if (OwnsTransferMovement(move, response, facilityId, destination)
            && move.IsSystemMoveInProgressTo(destination))
        {
            return;
        }

        bool startedHere = TryTakeStartedTransfer(
            response.externalOperationId,
            move,
            out int startedMovementVersion);
        if (startedHere
            && move.MovementOperationVersionForDiagnostics
                != startedMovementVersion)
        {
            TryFailObservedIncidentResponse(
                active,
                response,
                "isolation-transfer-movement-preempted",
                cancelled: true);
            return;
        }
        if (startedHere
            && move.LastGridMoveFailureReason == GridMoveFailureReason.Cancelled)
        {
            TryFailObservedIncidentResponse(
                active,
                response,
                "isolation-transfer-movement-cancelled",
                cancelled: true);
            return;
        }
        if (startedHere
            && move.LastGridMoveFailureReason != GridMoveFailureReason.None)
        {
            TryFailObservedIncidentResponse(
                active,
                response,
                "isolation-transfer-movement-failed:"
                    + move.LastGridMoveFailureReason,
                cancelled: false);
            return;
        }
        if (startedHere)
        {
            TryFailObservedIncidentResponse(
                active,
                response,
                "isolation-transfer-movement-ended-before-arrival",
                cancelled: false);
            return;
        }
        if (!responseMovements.TryStartSystemMove(move,
                response.operationId,
                response.externalOperationId,
                facilityId,
                destination,
                DoorAccessOverrideKind.None,
                out string resumeFailure))
        {
            TryFailObservedIncidentResponse(
                active,
                response,
                "isolation-transfer-resume-failed:"
                    + (string.IsNullOrWhiteSpace(resumeFailure)
                        ? "route-unavailable"
                        : resumeFailure.Trim()),
                cancelled: false);
            return;
        }
        startedIncidentTransferOperations[response.externalOperationId] =
            new StartedIncidentTransferAttempt(
                move, move.MovementOperationVersionForDiagnostics);
    }

    private void TryFailObservedIncidentResponse(
        V20ActiveEventSaveData active,
        ObservedIncidentResponseSaveData response,
        string reason,
        bool cancelled)
    {
        startedIncidentTransferOperations.Remove(
            response.externalOperationId);
        bool ownedBeforeTerminal = societyEvents.ActiveSocietyEvents.Any(value =>
            value != null
            && string.Equals(
                value.instanceId,
                active.instanceId,
                StringComparison.Ordinal)
            && string.Equals(
                value.observedIncidentResponse?.operationId,
                response.operationId,
                StringComparison.Ordinal));
        if (!ownedBeforeTerminal
            || !incidentResponses.TryRecordObservedIncidentResponseFailure(
            active.instanceId,
            response.operationId,
            reason,
            cancelled,
            out _))
        {
            return;
        }

        ReleaseObservedIncidentResponseProvenance(active, response);
        V20ActiveEventSaveData resolved = societyEvents
            .RecentResolvedSocietyEvents
            .Single(value => value != null
                && string.Equals(
                    value.instanceId,
                    active.instanceId,
                    StringComparison.Ordinal));
        V20AuthoredContentSO definition =
            ((ISocietyEventCatalog)storyCatalog).Require(
                resolved.definitionId);
        events.Publish(new V20ContentEffectsResolvedEvent(new V20ResolvedEventResult(
                definition.StableId,
                resolved.resolutionId,
                Array.Empty<V20ContentEffect>(),
                resolved.participantCharacterIds.AsReadOnly(),
                resolved.contextFactionId,
                resolved.instanceId,
                definition.DisplayName,
                resolved.riskTier,
                resolved.riskReason,
                resolved.terminalActionLabel,
                resolved.terminalOutcomeNarrative,
                resolved.terminalEffectKinds.AsReadOnly(),
                resolved.observedMealIncident?.observedCause
                    ?? string.Empty),
             physicalEffectsApplied: false));
    }

    internal void ReleaseObservedIncidentResponseProvenance(
        V20ActiveEventSaveData occurrence,
        ObservedIncidentResponseSaveData response,
        CharacterActor exactTarget = null)
    {
        if (response == null)
        {
            return;
        }
        startedIncidentTransferOperations.Remove(response.externalOperationId);
        if (!string.IsNullOrEmpty(response.externalOperationId)
            && medicalQuery.TryGetOrder(
                response.externalOperationId,
                out CharacterMedicalOrder order)
            && order != null
            && string.Equals(
                order.societyResponseOperationId,
                response.operationId,
                StringComparison.Ordinal))
        {
            order.societyResponseOperationId = string.Empty;
            order.societyResponseReceiptId = string.Empty;
        }
        string targetId = occurrence?.observedMealIncident?.targetCharacterId
            ?? string.Empty;
        CharacterActor target = exactTarget;
        bool exactTargetMatches = target != null
            && CharacterPersistentIdentity.TryGet(
                target,
                out CharacterId exactTargetId)
            && string.Equals(
                exactTargetId.Value,
                targetId,
                StringComparison.Ordinal);
        if (!exactTargetMatches)
        {
            target = characterLifetime.AllCharacters
                .FirstOrDefault(value => value != null
                && CharacterPersistentIdentity.TryGet(
                    value,
                    out CharacterId id)
                && string.Equals(
                    id.Value,
                    targetId,
                    StringComparison.Ordinal));
        }
        responseMovements.GetState(target?.GetAbility<AbilityMove>())?
            .TryReleaseSocietyResponseMovementProvenance(
                response.operationId,
                response.externalOperationId);
    }

    private void TryFinalizeObservedIncidentResponse(
        V20ActiveEventSaveData active,
        ObservedIncidentResponseSaveData response)
    {
        int absoluteDay = Math.Max(1, calendar.Day);
        V20DailyEventContext context = BuildCurrentEventContext(absoluteDay);
        if (!contentResolution.TryExecute(
                new ContentResolutionRequest
                {
                    ActionId = response.operationId,
                    Kind = ContentResolutionRequestKind.SocietyChoice,
                    InstanceId = active.instanceId,
                    ChoiceId = response.choiceId,
                    AbsoluteDay = absoluteDay,
                    Requirements = context.Requirements
                },
                out ContentResolutionResult contentResult,
                out _))
        {
            return;
        }
        ReleaseObservedIncidentResponseProvenance(active, response);
        foreach (V20ResolvedEventResult resolved in contentResult.Resolutions)
        {
            events.Publish(new V20ContentEffectsResolvedEvent(resolved,
                physicalEffectsApplied: true));
        }
    }

    private static string FormatReplacementMealReceiptId(
        string operationId,
        ItemStackId itemStackId) =>
        $"physical-meal-consumed:{operationId}:{itemStackId.Value}";

    internal V20DailyEventContext BuildCurrentEventContext(int absoluteDay)
    {
        RunMilestoneEvaluationSnapshot world = milestoneProjector.Build(
            absoluteDay);
        V20DailyEventContext context = new()
        {
            AbsoluteDay = absoluteDay,
            RunSeed = runSeed.RunSeed,
            Season = GameCalendarRules.Project(absoluteDay, 0).Season,
            Generation = Mathf.Max(
                0,
                Mathf.FloorToInt(world.WorldMetrics.TryGetValue(
                    V20WorldMetricKind.CompletedGenerations,
                    out float generations)
                        ? generations
                        : 0f))
        };
        CopySnapshot(world, context.Requirements);
        IExteriorIncidentRuntime exterior = exteriorIncidents()
            ?? throw new InvalidOperationException("Observed incidents require the Exterior actor owner query.");
        IReadOnlyList<ExteriorIncidentRuntimeState> states = exterior.IncidentStates
            ?? throw new InvalidOperationException("Exterior incident states are unavailable.");
        foreach (ExteriorIncidentRuntimeState incident in states)
        {
            if (incident == null)
                throw new InvalidOperationException("Exterior incident owner query contains a null state.");
            if (incident.IsTerminal) continue;
            if (incident.actorIds == null)
                throw new InvalidOperationException("Active Exterior incident has no actor owner collection.");
            foreach (string rawId in incident.actorIds)
            {
                CharacterId id = new(rawId);
                if (!id.IsValid || !string.Equals(rawId, id.Value, StringComparison.Ordinal)
                    || !context.ActiveExteriorIncidentCharacterIds.Add(id.Value))
                    throw new InvalidOperationException("Active Exterior incident actor owner is invalid or duplicated.");
            }
        }
        return context;
    }

    internal void TryCaptureReplacementMealReceipt(
        PhysicalMealConsumedEvent consumed,
        CharacterActor actor)
    {
        V20ActiveEventSaveData active = societyEvents.ActiveSocietyEvents
            .FirstOrDefault(value => value?.hasObservedMealIncident == true
                && value.observedIncidentResponse?.phase
                    == ObservedIncidentResponsePhase.ExternalOperationStarted
                && string.Equals(
                    value.observedIncidentResponse.choiceId,
                    "replace",
                    StringComparison.Ordinal)
                && string.Equals(
                    value.observedIncidentResponse.externalOperationId,
                    consumed.OperationId.Value,
                    StringComparison.Ordinal));
        if (active == null)
        {
            return;
        }

        ObservedIncidentResponseSaveData response =
            active.observedIncidentResponse;
        bool actorMatches = actor?.Identity?.CharacterType
                == CharacterType.Customer
            && CharacterPersistentIdentity.TryGet(
                actor,
                out CharacterId customerId)
            && string.Equals(
                customerId.Value,
                active.observedMealIncident.targetCharacterId,
                StringComparison.Ordinal);
        bool facilityMatches = consumed.Facility != null
            && consumed.Facility.PersistentInstanceId.IsValid
            && string.Equals(
                consumed.Facility.PersistentInstanceId.Value,
                active.observedMealIncident.facilityInstanceId,
                StringComparison.Ordinal);
        if (!actorMatches
            || !facilityMatches
            || !consumed.Result.Success
            || !consumed.Result.OperationId.Equals(consumed.OperationId)
            || !consumed.Result.ItemStackId.IsValid
            || consumed.Result.PolicyViolation
            || consumed.Result.Contaminated)
        {
            TryFailObservedIncidentResponse(
                active,
                response,
                "replacement-meal-physical-receipt-conflict",
                cancelled: false);
            return;
        }
        if (incidentResponses.TryRecordObservedIncidentResponseReceipt(
                active.instanceId,
                response.operationId,
                consumed.OperationId.Value,
                FormatReplacementMealReceiptId(
                    response.operationId,
                    consumed.Result.ItemStackId),
                out _))
        {
            TryFinalizeObservedIncidentResponse(active, response);
        }
    }

    internal static void CopySnapshot(
        RunMilestoneEvaluationSnapshot source,
        RunMilestoneEvaluationSnapshot destination)
    {
        foreach (int id in source.CompletedResearchIds)
            destination.CompletedResearchIds.Add(id);
        foreach (string flag in source.WorldFlags)
            destination.WorldFlags.Add(flag);
        foreach (KeyValuePair<V20WorldMetricKind, float> pair in source.WorldMetrics)
            destination.WorldMetrics[pair.Key] = pair.Value;
        foreach (KeyValuePair<string, int> pair in source.ItemQuantities)
            destination.ItemQuantities[pair.Key] = pair.Value;
        foreach (KeyValuePair<string, int> pair in source.FacilityCounts)
            destination.FacilityCounts[pair.Key] = pair.Value;
        foreach (KeyValuePair<string, int> pair in source.OperationalFacilityCounts)
            destination.OperationalFacilityCounts[pair.Key] = pair.Value;
        foreach (KeyValuePair<string, FactionCampaignStateSaveData> pair in source.Factions)
            destination.Factions[pair.Key] = pair.Value;
        destination.EligibleCharacterCount = source.EligibleCharacterCount;
    }

    internal void ResetTransferAttempts() => startedIncidentTransferOperations.Clear();

    private void ForgetAbandonedTransferAttempts(IReadOnlyList<V20ActiveEventSaveData> pending)
    {
        foreach (string operationId in startedIncidentTransferOperations.Keys.ToArray())
        {
            if (!pending.Any(value => string.Equals(
                    value.observedIncidentResponse.externalOperationId, operationId, StringComparison.Ordinal)))
                startedIncidentTransferOperations.Remove(operationId);
        }
    }

    private bool TryTakeStartedTransfer(string operationId, AbilityMove movement, out int version)
    {
        version = default;
        if (!startedIncidentTransferOperations.TryGetValue(operationId, out StartedIncidentTransferAttempt attempt))
            return false;
        startedIncidentTransferOperations.Remove(operationId);
        if (!ReferenceEquals(attempt.Movement, movement))
            return false;
        version = attempt.Version;
        return true;
    }

    private bool OwnsTransferMovement(
        AbilityMove movement,
        ObservedIncidentResponseSaveData response,
        BuildingInstanceId facilityId,
        Vector2Int destination)
    {
        var state = responseMovements.GetState(movement);
        return state != null && response != null
            && string.Equals(state.SocietyResponseMovementOperationId, response.operationId, StringComparison.Ordinal)
            && string.Equals(state.SocietyResponseMovementExternalOperationId, response.externalOperationId, StringComparison.Ordinal)
            && string.Equals(state.SocietyResponseMovementFacilityId, facilityId.Value, StringComparison.Ordinal)
            && state.SocietyResponseMovementDestination == destination;
    }
}
