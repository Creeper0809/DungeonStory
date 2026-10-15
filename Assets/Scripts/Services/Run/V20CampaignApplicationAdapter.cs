using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using VContainer.Unity;

public interface IV20DailyEvaluationDiagnostic
{
    int LastDailyEvaluationAbsoluteDay { get; }
    bool LastDailyEvaluationSucceeded { get; }
    DomainFailure LastDailyEvaluationFailure { get; }
}

public interface IV20ObservedMealIncidentDiagnostic
{
    ObservedMealIncidentCaptureResult LastObservedMealIncidentResult { get; }
    DomainFailure LastObservedMealIncidentCallbackFailure { get; }
    ObservedMealIncidentCaptureResult LastObservedShopliftingIncidentResult
        { get; }
    DomainFailure LastObservedShopliftingIncidentCallbackFailure { get; }
    ObservedMealIncidentCaptureResult LastObservedBrawlIncidentResult { get; }
    DomainFailure LastObservedBrawlIncidentCallbackFailure { get; }
    ObservedMealIncidentCaptureResult LastObservedCulturalConflictIncidentResult
        { get; }
    DomainFailure LastObservedCulturalConflictIncidentCallbackFailure { get; }
}

public sealed class V20CampaignApplicationAdapter :
    IStartable,
    ITickable,
    IDisposable,
    IV20DailyEvaluationDiagnostic,
    IV20ObservedMealIncidentDiagnostic
{
    private readonly IContentResolutionService contentResolution;
    private readonly IRunMilestoneCommand milestones;
    private readonly IRunMilestoneQuery milestoneQuery;
    private readonly IEndlessCrisisCommand endless;
    private readonly IEndlessCrisisQuery endlessQuery;
    private readonly IGameCalendar calendar;
    private readonly IClimateQuery climate;
    private readonly IRunSeedProvider runSeed;
    private readonly IGameEventBus events;
    private readonly IV20MilestoneWorldSnapshotQuery milestoneProjector;
    private readonly ICharacterNarrativeQuery narratives;
    private readonly ICharacterNarrativeCatalog narrativeCatalog;
    private readonly ICharacterCultureGameplayQuery cultureGameplay;
    private readonly ISocietyEventQuery societyEvents;
    private readonly ISocietyObservedMealIncidentCommand observedMealIncidents;
    private readonly ISocietyObservedIncidentResponseCommand incidentResponses;
    private readonly V20StoryContentCatalog storyCatalog;
    private readonly ICharacterLifeQuery life;
    private readonly ICareerService careers;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly ICharacterBodyHealthQuery bodyHealth;
    private readonly ICharacterConsumablesApplication consumables;
    private readonly ICharacterMedicalCommand medicalCommands;
    private readonly ICharacterMedicalQuery medicalQuery;
    private readonly ICharacterLifetimeQuery characterLifetime;
    private readonly Func<IExteriorIncidentRuntime> exteriorIncidents;
    private IDisposable dayStartedSubscription;
    private IDisposable mealConsumedSubscription;
    private IDisposable facilityCrimeSubscription;
    private IDisposable visitorFacilityCombatDamageSubscription;
    private IDisposable characterDeathSubscription;
    private IDisposable socialConflictCommittedSubscription;
    private bool advancingIncidentResponses;
    private readonly Dictionary<string, int> startedIncidentTransferOperations =
        new(StringComparer.Ordinal);

    public int LastDailyEvaluationAbsoluteDay { get; private set; }
    public bool LastDailyEvaluationSucceeded { get; private set; }
    public DomainFailure LastDailyEvaluationFailure { get; private set; }
    public ObservedMealIncidentCaptureResult LastObservedMealIncidentResult
    {
        get;
        private set;
    }
    public DomainFailure LastObservedMealIncidentCallbackFailure
    {
        get;
        private set;
    }
    public ObservedMealIncidentCaptureResult
        LastObservedShopliftingIncidentResult { get; private set; }
    public DomainFailure LastObservedShopliftingIncidentCallbackFailure
        { get; private set; }
    public ObservedMealIncidentCaptureResult LastObservedBrawlIncidentResult
        { get; private set; }
    public DomainFailure LastObservedBrawlIncidentCallbackFailure
        { get; private set; }
    public ObservedMealIncidentCaptureResult
        LastObservedCulturalConflictIncidentResult { get; private set; }
    public DomainFailure LastObservedCulturalConflictIncidentCallbackFailure
        { get; private set; }
    public V20CampaignApplicationAdapter(
        IContentResolutionService contentResolution,
        IRunMilestoneCommand milestones,
        IRunMilestoneQuery milestoneQuery,
        IEndlessCrisisCommand endless,
        IEndlessCrisisQuery endlessQuery,
        IGameCalendar calendar,
        IClimateQuery climate,
        IRunSeedProvider runSeed,
        IGameEventBus events,
        IV20MilestoneWorldSnapshotQuery milestoneProjector,
        ICharacterNarrativeQuery narratives,
        ICharacterNarrativeCatalog narrativeCatalog,
        ICharacterCultureGameplayQuery cultureGameplay,
        ISocietyEventQuery societyEvents,
        ISocietyObservedMealIncidentCommand observedMealIncidents,
        V20StoryContentCatalog storyCatalog,
        ICharacterLifeQuery life,
        ICareerService careers,
        IFacilityCapabilityQuery facilities,
        ICharacterBodyHealthQuery bodyHealth,
        ICharacterConsumablesApplication consumables,
        ICharacterMedicalCommand medicalCommands,
        ICharacterMedicalQuery medicalQuery,
        ICharacterLifetimeQuery characterLifetime,
        Func<IExteriorIncidentRuntime> exteriorIncidents)
    {
        this.contentResolution = contentResolution
            ?? throw new ArgumentNullException(nameof(contentResolution));
        this.milestones = milestones ?? throw new ArgumentNullException(nameof(milestones));
        this.milestoneQuery = milestoneQuery ?? throw new ArgumentNullException(nameof(milestoneQuery));
        this.endless = endless ?? throw new ArgumentNullException(nameof(endless));
        this.endlessQuery = endlessQuery
            ?? throw new ArgumentNullException(nameof(endlessQuery));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.climate = climate ?? throw new ArgumentNullException(nameof(climate));
        this.runSeed = runSeed ?? throw new ArgumentNullException(nameof(runSeed));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.milestoneProjector = milestoneProjector
            ?? throw new ArgumentNullException(nameof(milestoneProjector));
        this.narratives = narratives
            ?? throw new ArgumentNullException(nameof(narratives));
        this.narrativeCatalog = narrativeCatalog
            ?? throw new ArgumentNullException(nameof(narrativeCatalog));
        this.cultureGameplay = cultureGameplay
            ?? throw new ArgumentNullException(nameof(cultureGameplay));
        this.societyEvents = societyEvents
            ?? throw new ArgumentNullException(nameof(societyEvents));
        this.observedMealIncidents = observedMealIncidents
            ?? throw new ArgumentNullException(nameof(observedMealIncidents));
        incidentResponses = observedMealIncidents
                as ISocietyObservedIncidentResponseCommand
            ?? throw new ArgumentException(
                "Observed incident command must own response state.",
                nameof(observedMealIncidents));
        this.storyCatalog = storyCatalog
            ?? throw new ArgumentNullException(nameof(storyCatalog));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.careers = careers ?? throw new ArgumentNullException(nameof(careers));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.bodyHealth = bodyHealth
            ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.consumables = consumables
            ?? throw new ArgumentNullException(nameof(consumables));
        this.medicalCommands = medicalCommands
            ?? throw new ArgumentNullException(nameof(medicalCommands));
        this.medicalQuery = medicalQuery
            ?? throw new ArgumentNullException(nameof(medicalQuery));
        this.characterLifetime = characterLifetime
            ?? throw new ArgumentNullException(nameof(characterLifetime));
        this.exteriorIncidents = exteriorIncidents
            ?? throw new ArgumentNullException(nameof(exteriorIncidents));
    }

    public void Start()
    {
        dayStartedSubscription ??= events.Subscribe<OperatingDayStartedEvent>(
            OnDayStarted);
        mealConsumedSubscription ??= events.Subscribe<PhysicalMealConsumedEvent>(
            OnMealConsumed);
        facilityCrimeSubscription ??= events.Subscribe<FacilityCrimeEvent>(
            OnFacilityCrimeCommitted);
        visitorFacilityCombatDamageSubscription ??=
            events.Subscribe<VisitorFacilityCombatDamageCommittedEvent>(
                OnVisitorFacilityCombatDamageCommitted);
        characterDeathSubscription ??= events.Subscribe<CharacterDeathEvent>(
            OnCharacterDeath);
        socialConflictCommittedSubscription ??=
            events.Subscribe<SocialConflictCommittedReceiptEvent>(
                OnSocialConflictCommitted);
    }

    public void Dispose()
    {
        dayStartedSubscription?.Dispose();
        mealConsumedSubscription?.Dispose();
        facilityCrimeSubscription?.Dispose();
        visitorFacilityCombatDamageSubscription?.Dispose();
        characterDeathSubscription?.Dispose();
        socialConflictCommittedSubscription?.Dispose();
        dayStartedSubscription = null;
        mealConsumedSubscription = null;
        facilityCrimeSubscription = null;
        visitorFacilityCombatDamageSubscription = null;
        characterDeathSubscription = null;
        socialConflictCommittedSubscription = null;
        startedIncidentTransferOperations.Clear();
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
            foreach (V20ActiveEventSaveData active in
                     societyEvents.ActiveSocietyEvents
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
                         .ToArray())
            {
                AdvanceObservedIncidentResponse(active);
            }
        }
        finally
        {
            advancingIncidentResponses = false;
        }
    }

    private void OnSocialConflictCommitted(
        SocialConflictCommittedReceiptEvent committed)
    {
        LastObservedCulturalConflictIncidentCallbackFailure =
            DomainFailure.None;
        LastObservedCulturalConflictIncidentResult = NotEligibleMealResult(
            "observed-cultural-conflict-callback-not-completed");
        try
        {
            CharacterActor customer = milestoneProjector.LivingCharacters
                .FirstOrDefault(actor => actor != null
                    && actor.Identity?.CharacterType == CharacterType.Customer
                    && CharacterPersistentIdentity.TryGet(
                        actor,
                        out CharacterId id)
                    && id.Equals(committed.Customer));
            int absoluteDay = Math.Max(1, calendar.Day);
            if (customer == null
                || committed.AbsoluteDay != absoluteDay
                || committed.Instigator.Equals(committed.Target)
                || committed.InstigatorCulture.Equals(
                    committed.TargetCulture))
            {
                LastObservedCulturalConflictIncidentResult =
                    NotEligibleMealResult(
                        "observed-cultural-conflict-source-not-eligible");
                return;
            }

            LastObservedCulturalConflictIncidentResult =
                observedMealIncidents.CaptureObservedCulturalConflictIncident(
                    new ObservedCulturalConflictIncidentSnapshot(
                        committed.OperationId,
                        committed.Instigator,
                        committed.Target,
                        committed.Customer,
                        committed.InstigatorCulture,
                        committed.TargetCulture,
                        committed.FacilityInstanceId,
                        committed.Location,
                        committed.AbsoluteDay),
                    BuildCurrentEventContext(absoluteDay));
            if (LastObservedCulturalConflictIncidentResult.Disposition is
                ObservedMealIncidentCaptureDisposition.Created
                or ObservedMealIncidentCaptureDisposition.ExactReplay)
            {
                events.Publish(new CampaignIncidentCapturedFact(LastObservedCulturalConflictIncidentResult, absoluteDay));
            }
        }
        catch (Exception exception)
        {
            LastObservedCulturalConflictIncidentCallbackFailure =
                new DomainFailure(
                    FailureCode.ExternalInfluenceUnavailable,
                    "observed-cultural-conflict-incident-callback",
                    exception.Message);
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
            && V20CampaignRuntime
                .TryClassifyObservedIncidentTransferExternalOperationId(
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
                    || !V20CampaignRuntime
                        .TryClassifyObservedIncidentTransferExternalOperationId(
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

            string receiptId = V20CampaignRuntime
                .FormatObservedIncidentTransferReceiptId(
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
                string externalOperationId = V20CampaignRuntime
                    .FormatObservedIncidentTransferOperationId(
                        response.operationId,
                        facility.PersistentInstanceId,
                        new CoreGridCell(
                            candidateDestination.x,
                            candidateDestination.y));
                if (!move.TryStartSocietyResponseSystemMove(
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
                    move.MovementOperationVersionForDiagnostics;
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

        if (!V20CampaignRuntime.TryParseObservedIncidentTransferOperationId(
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
            if (move.IsSystemMoveInProgressTo(destination))
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
            string receiptId = V20CampaignRuntime
                .FormatObservedIncidentTransferReceiptId(
                    response.externalOperationId,
                    customerId);
            startedIncidentTransferOperations.Remove(
                response.externalOperationId);
            if (!move.TryRecordSocietyResponseMovementReceipt(
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
        if (move.IsSystemMoveInProgressTo(destination))
        {
            return;
        }

        bool startedHere = startedIncidentTransferOperations.TryGetValue(
            response.externalOperationId,
            out int startedMovementVersion);
        startedIncidentTransferOperations.Remove(response.externalOperationId);
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
        if (!move.TryStartSocietyResponseSystemMove(
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
            move.MovementOperationVersionForDiagnostics;
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

    private void ReleaseObservedIncidentResponseProvenance(
        V20ActiveEventSaveData occurrence,
        ObservedIncidentResponseSaveData response,
        CharacterActor exactTarget = null)
    {
        if (response == null)
        {
            return;
        }
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
        target?.GetAbility<AbilityMove>()?
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

    private void OnVisitorFacilityCombatDamageCommitted(
        VisitorFacilityCombatDamageCommittedEvent committed)
    {
        LastObservedBrawlIncidentCallbackFailure = DomainFailure.None;
        LastObservedBrawlIncidentResult = NotEligibleMealResult(
            "observed-brawl-incident-callback-not-completed");
        try
        {
            int absoluteDay = Math.Max(1, calendar.Day);
            if (committed.AbsoluteDay != absoluteDay)
            {
                LastObservedBrawlIncidentResult = NotEligibleMealResult(
                    "observed-brawl-source-day-mismatch");
                return;
            }

            LastObservedBrawlIncidentResult =
                observedMealIncidents.CaptureObservedBrawlIncident(
                    new ObservedBrawlIncidentSnapshot(
                        committed.AttackOperationId,
                        committed.CustomerCharacterId,
                        committed.OtherParticipantCharacterId,
                        committed.CustomerWasAttacker,
                        committed.ActualDamage,
                        committed.FacilityInstanceId,
                        committed.Location,
                        committed.AbsoluteDay),
                    BuildCurrentEventContext(absoluteDay));
            if (LastObservedBrawlIncidentResult.Disposition is
                ObservedMealIncidentCaptureDisposition.Created
                or ObservedMealIncidentCaptureDisposition.ExactReplay)
            {
                events.Publish(new CampaignIncidentCapturedFact(LastObservedBrawlIncidentResult, absoluteDay));
            }
        }
        catch (Exception exception)
        {
            // Body health has already committed. Incident projection cannot
            // replay or compensate that combat mutation.
            LastObservedBrawlIncidentCallbackFailure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "observed-brawl-incident-callback",
                exception.Message);
        }
    }

    private void OnFacilityCrimeCommitted(FacilityCrimeEvent crime)
    {
        LastObservedShopliftingIncidentCallbackFailure = DomainFailure.None;
        LastObservedShopliftingIncidentResult = NotEligibleMealResult(
            "observed-shoplifting-incident-callback-not-completed");
        try
        {
            CharacterActor actor = crime.actor;
            RetailStockLotSnapshot lot = crime.committedLot;
            if (crime.kind != FacilityCrimeKind.Shoplifting
                || actor?.Identity?.CharacterType != CharacterType.Customer
                || !CharacterPersistentIdentity.TryGet(
                    actor,
                    out CharacterId targetCharacterId)
                || crime.facility is not Shop shop
                || !shop.PersistentInstanceId.IsValid
                || lot == null
                || lot.quantity != 1
                || lot.unitMassGrams <= 0L)
            {
                LastObservedShopliftingIncidentResult = NotEligibleMealResult(
                    "observed-shoplifting-source-not-eligible");
                return;
            }

            int absoluteDay = Math.Max(1, calendar.Day);
            V20DailyEventContext context = BuildCurrentEventContext(absoluteDay);
            LastObservedShopliftingIncidentResult =
                observedMealIncidents.CaptureObservedShopliftingIncident(
                    new ObservedShopliftingIncidentSnapshot(
                        crime.commitOperationId,
                        lot.sourceOperationId,
                        targetCharacterId,
                        new ItemDefinitionId(lot.itemDefinitionId),
                        lot.saleItemId,
                        lot.itemInstanceId,
                        lot.sourceStackId,
                        lot.quantity,
                        lot.unitMassGrams,
                        lot.componentFingerprint,
                        shop.PersistentInstanceId,
                        new CoreGridCell(shop.centerPos.x, shop.centerPos.y),
                        absoluteDay,
                        crime.lossValue),
                    context);
            if (LastObservedShopliftingIncidentResult.Disposition is
                ObservedMealIncidentCaptureDisposition.Created
                or ObservedMealIncidentCaptureDisposition.ExactReplay)
            {
                events.Publish(new CampaignIncidentCapturedFact(LastObservedShopliftingIncidentResult, absoluteDay));
            }
        }
        catch (Exception exception)
        {
            // The exact retail Sink has already committed. Optional incident
            // capture must never replay or restore that physical mutation.
            LastObservedShopliftingIncidentCallbackFailure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "observed-shoplifting-incident-callback",
                exception.Message);
        }
    }

    private V20DailyEventContext BuildCurrentEventContext(int absoluteDay)
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

    private void OnMealConsumed(PhysicalMealConsumedEvent consumed)
    {
        LastObservedMealIncidentCallbackFailure = DomainFailure.None;
        LastObservedMealIncidentResult = NotEligibleMealResult(
            "observed-meal-incident-callback-not-completed");
        try
        {
            CharacterActor actor = consumed.Actor;
            TryCaptureReplacementMealReceipt(consumed, actor);
            if (!consumed.Result.Success
                || !consumed.OperationId.IsValid
                || !consumed.Result.OperationId.IsValid
                || !consumed.OperationId.Equals(consumed.Result.OperationId)
                || actor?.Identity?.CharacterType != CharacterType.Customer
                || !CharacterPersistentIdentity.TryGet(
                    actor,
                    out CharacterId targetCharacterId))
            {
                LastObservedMealIncidentResult = NotEligibleMealResult(
                    "observed-meal-source-not-eligible");
                return;
            }

            CharacterBodyHealthSnapshot health = bodyHealth.GetSnapshot(actor);
            CharacterVitalsSnapshot vitals = bodyHealth.GetVitals(actor);
            bool observedDead = actor.IsDead || vitals.IsDead;
            if (observedDead
                || !health.Downed
                    && !consumed.Result.Contaminated
                    && !consumed.Result.PolicyViolation)
            {
                LastObservedMealIncidentResult = NotEligibleMealResult(
                    "observed-meal-facts-not-eligible");
                return;
            }

            int absoluteDay = Math.Max(1, calendar.Day);
            V20DailyEventContext context = BuildCurrentEventContext(absoluteDay);
            Vector2Int location = consumed.Facility != null
                ? consumed.Facility.centerPos
                : actor.GetNowXY();
            LastObservedMealIncidentResult =
                observedMealIncidents.CaptureObservedMealIncident(
                    new ObservedMealIncidentSnapshot(
                        consumed.OperationId,
                        targetCharacterId,
                        consumed.Result.ItemDefinitionId,
                        consumed.Result.ItemStackId,
                        consumed.Facility?.PersistentInstanceId ?? default,
                        fieldMeal: consumed.Facility == null,
                        new CoreGridCell(location.x, location.y),
                        absoluteDay,
                        consumed.Result.PolicyViolation,
                        health.Downed,
                        observedDead,
                        observedContaminated: consumed.Result.Contaminated),
                    context);
            if (LastObservedMealIncidentResult.Disposition is
                ObservedMealIncidentCaptureDisposition.Created
                or ObservedMealIncidentCaptureDisposition.ExactReplay)
            {
                events.Publish(new CampaignIncidentCapturedFact(LastObservedMealIncidentResult, absoluteDay));
            }
        }
        catch (Exception exception)
        {
            // Physical meal effects and item commit precede this observer. A
            // presentation or optional evidence failure must never make the
            // consumable operation replay those already-committed effects.
            LastObservedMealIncidentCallbackFailure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "observed-meal-incident-callback",
                exception.Message);
        }
    }

    private void TryCaptureReplacementMealReceipt(
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

    private void OnCharacterDeath(CharacterDeathEvent gameEvent)
    {
        try
        {
            CharacterActor exactTarget = characterLifetime.AllCharacters
                .Where(value => value != null
                    && CharacterPersistentIdentity.TryGet(
                        value,
                        out CharacterId id)
                    && id.Equals(gameEvent.CharacterId))
                .SingleOrDefault();
            if (observedMealIncidents.TryResolveObservedMealIncidentTargetDeath(
                    gameEvent.CharacterId,
                    gameEvent.AbsoluteDay,
                    out V20ResolvedEventResult resolved,
                    out DomainFailure failure))
            {
                V20ActiveEventSaveData occurrence = societyEvents
                    .RecentResolvedSocietyEvents
                    .Single(value => value != null
                        && string.Equals(
                            value.instanceId,
                            resolved.InstanceId,
                            StringComparison.Ordinal));
                ReleaseObservedIncidentResponseProvenance(
                    occurrence,
                    occurrence.observedIncidentResponse,
                    exactTarget);
                events.Publish(new V20ContentEffectsResolvedEvent(resolved,
                    physicalEffectsApplied: false));
            }
            else if (failure.IsFailure)
            {
                LastObservedMealIncidentCallbackFailure = failure;
            }
        }
        catch (Exception exception)
        {
            LastObservedMealIncidentCallbackFailure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "observed-meal-incident-target-death",
                exception.Message);
        }
    }

    private static ObservedMealIncidentCaptureResult NotEligibleMealResult(
        string reason) => new(
        ObservedMealIncidentCaptureDisposition.NotEligible,
        default,
        string.Empty,
        new DomainFailure(FailureCode.ExternalInfluenceUnavailable, reason));

    private void OnDayStarted(OperatingDayStartedEvent started)
    {
        int absoluteDay = Math.Max(1, started.day);
        careers.CompleteDueRetirements(absoluteDay);
        endless.AdvanceEndlessCrisis(absoluteDay);
        RunMilestoneEvaluationSnapshot snapshot =
            milestoneProjector.Build(absoluteDay);
        V20DailyEventContext context = new()
        {
            AbsoluteDay = absoluteDay,
            RunSeed = runSeed.RunSeed,
            Season = GameCalendarRules.Project(absoluteDay, 0).Season,
            WeatherFrontId = climate.WeatherFrontId,
            Generation = Mathf.Max(
                0,
                Mathf.FloorToInt(snapshot.WorldMetrics.TryGetValue(
                    V20WorldMetricKind.CompletedGenerations,
                    out float generations)
                        ? generations
                        : 0f))
        };
        CopySnapshot(snapshot, context.Requirements);
        CharacterActor[] livingCharacters = milestoneProjector.LivingCharacters
            .Where(value => value?.Identity != null)
            .ToArray();
        SpeciesCultureId[] culturesPresent = livingCharacters
            .Select(value => new CharacterId(value.Identity.PersistentId))
            .Where(value => narratives.TryGet(value, out _))
            .Select(value =>
            {
                narratives.TryGet(value, out CharacterNarrativeSnapshot narrative);
                return narrative.CultureId;
            })
            .Where(value => value.IsValid)
            .Distinct()
            .ToArray();
        foreach (CharacterActor actor in livingCharacters)
        {
            CharacterId characterId = new(actor.Identity.PersistentId);
            context.ParticipantCharacterIds.Add(characterId.Value);
            if (life.TryGet(characterId, out CharacterLifeRecord record)
                && record.LifeStage == CharacterLifeStage.Elder
                && contentResolution is IRetirementScheduleContentQuery
                    retirementSchedules
                && retirementSchedules.CanScheduleRetirement(characterId))
            {
                context.RetirementEligibleParticipantIds.Add(
                    characterId.Value);
            }
            context.ParticipantContentWeights[characterId.Value] =
                BuildContentWeights(actor, culturesPresent);
        }

        ContentResolutionRequest request = new()
        {
            ActionId = $"content:daily:{context.AbsoluteDay}",
            Kind = ContentResolutionRequestKind.DailyEvaluation,
            AbsoluteDay = context.AbsoluteDay,
            DailyContext = context,
            Requirements = snapshot
        };
        LastDailyEvaluationAbsoluteDay = context.AbsoluteDay;
        LastDailyEvaluationSucceeded = contentResolution.TryExecute(
                request,
                out ContentResolutionResult contentResult,
                out DomainFailure dailyFailure);
        LastDailyEvaluationFailure = dailyFailure;
        if (LastDailyEvaluationSucceeded)
        {
            foreach (V20ResolvedEventResult resolved in contentResult.Resolutions)
                events.Publish(new V20ContentEffectsResolvedEvent(resolved,
                    physicalEffectsApplied: true));
        }
        events.Publish(new CampaignDailyEvaluatedFact(context.AbsoluteDay, LastDailyEvaluationSucceeded));
        foreach (string milestoneId in milestones.Evaluate(snapshot))
        {
            events.Publish(new V20ContentEffectsResolvedEvent(
                milestoneId,
                "completed",
                Array.Empty<V20ContentEffect>(),
                physicalEffectsApplied: true));
        }
        bool composeEndlessCrisis = milestoneQuery.Phase
                == RunProgressionPhase.EndlessAge
            && started.day % 10 == 0;
        EndlessCrisisSnapshot crisisBeforeComposition =
            endlessQuery.CurrentEndlessCrisis;
        bool hasTerminalBeforeComposition = composeEndlessCrisis
            && crisisBeforeComposition.Phase
                == EndlessCrisisLifecyclePhase.None
            && crisisBeforeComposition.InstanceId.Length > 0;
        if (hasTerminalBeforeComposition)
        {
            events.Publish(new EndlessCrisisEvaluatedFact(absoluteDay, crisisBeforeComposition));
        }
        if (composeEndlessCrisis)
        {
            endless.ComposeNextEndlessCrisis(started.day, runSeed.RunSeed);
        }
        EndlessCrisisSnapshot crisisAfterComposition =
            endlessQuery.CurrentEndlessCrisis;
        if (!hasTerminalBeforeComposition
            || !string.Equals(
                crisisBeforeComposition.InstanceId,
                crisisAfterComposition.InstanceId,
                StringComparison.Ordinal))
        {
            events.Publish(new EndlessCrisisEvaluatedFact(absoluteDay, crisisAfterComposition));
        }
    }

    private Dictionary<string, float> BuildContentWeights(
        CharacterActor actor,
        IReadOnlyCollection<SpeciesCultureId> culturesPresent)
    {
        Dictionary<string, float> result = new(StringComparer.Ordinal);
        foreach (CharacterTraitSO trait in actor?.Progression?.ResolveSelectedTraits()
                     ?? Array.Empty<CharacterTraitSO>())
        {
            IEnumerable<KeyValuePair<string, float>> shared =
                (trait?.identityRules ?? new List<CharacterIdentityRule>())
                .OfType<IncidentWeightRule>()
                .Where(rule => !string.IsNullOrWhiteSpace(rule.incidentId)
                    && !Mathf.Approximately(rule.multiplier, 1f))
                .Select(rule => new KeyValuePair<string, float>(
                    rule.incidentId.Trim(),
                    rule.multiplier));
            IEnumerable<KeyValuePair<string, float>> legacy =
                (trait?.eventWeights ?? new List<CharacterTraitEventWeight>())
                .Where(weight => weight != null && weight.IsValid)
                .Select(weight => new KeyValuePair<string, float>(
                    weight.eventCategoryId.Trim(),
                    weight.multiplier));
            foreach (KeyValuePair<string, float> weight in
                     shared.Any() ? shared : legacy)
            {
                result[weight.Key] = Mathf.Clamp(
                    (result.TryGetValue(weight.Key, out float current)
                        ? current
                        : 1f) * weight.Value,
                    0.1f,
                    10f);
            }
        }
        CharacterId id = new(actor?.Identity?.PersistentId);
        if (id.IsValid
            && narratives.TryGet(id, out CharacterNarrativeSnapshot narrative)
            && narrative.AmbitionStatus == CharacterAmbitionStatus.Active)
        {
            CharacterAmbitionDefinitionSO ambition = narrativeCatalog.Require(
                narrative.ActiveAmbitionId);
            foreach (V20WeightedId weight in ambition.relatedEventWeights
                         ?? new List<V20WeightedId>())
                result[weight.id] = Mathf.Clamp(
                    (result.TryGetValue(weight.id, out float current)
                        ? current
                        : 1f) * weight.weight,
                    0.1f,
                    10f);
        }
        if (id.IsValid)
        {
            MultiplyWeight(
                result,
                "service-incident:culturalinsult",
                cultureGameplay.GetServiceIncidentWeight(
                    id,
                    ServiceIncidentKind.CulturalInsult,
                    culturesPresent));
            MultiplyWeight(
                result,
                "service-incident:forbiddenmeal",
                cultureGameplay.GetServiceIncidentWeight(
                    id,
                    ServiceIncidentKind.ForbiddenMeal,
                    culturesPresent));
        }
        return result;
    }

    private static void MultiplyWeight(
        IDictionary<string, float> weights,
        string id,
        float multiplier) => weights[id] = Mathf.Clamp(
        (weights.TryGetValue(id, out float current) ? current : 1f)
        * multiplier,
        0.1f,
        10f);

    private static void CopySnapshot(
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
        foreach (KeyValuePair<string, FactionCampaignStateSaveData> pair in source.Factions)
            destination.Factions[pair.Key] = pair.Value;
        destination.EligibleCharacterCount = source.EligibleCharacterCount;
    }
}
