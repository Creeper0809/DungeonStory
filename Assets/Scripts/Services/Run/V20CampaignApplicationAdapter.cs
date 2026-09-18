using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using VContainer.Unity;

public readonly struct V20ContentEffectsResolvedEvent
{
    public V20ContentEffectsResolvedEvent(
        string definitionId,
        string resolutionId,
        IReadOnlyList<V20ContentEffect> effects,
        bool physicalEffectsApplied,
        string occurrenceInstanceId = "")
    {
        DefinitionId = definitionId ?? string.Empty;
        ResolutionId = resolutionId ?? string.Empty;
        Effects = effects ?? Array.Empty<V20ContentEffect>();
        PhysicalEffectsApplied = physicalEffectsApplied;
        OccurrenceInstanceId = occurrenceInstanceId?.Trim()
            ?? string.Empty;
    }

    public string DefinitionId { get; }
    public string ResolutionId { get; }
    public IReadOnlyList<V20ContentEffect> Effects { get; }
    public bool PhysicalEffectsApplied { get; }
    public string OccurrenceInstanceId { get; }
}

public static class V20SocietyEventAlertProjection
{
    public static EventAlertImportance ToAlertImportance(
        ExperienceEventRiskTier riskTier) => riskTier switch
        {
            ExperienceEventRiskTier.None => EventAlertImportance.Low,
            ExperienceEventRiskTier.Recoverable => EventAlertImportance.Medium,
            ExperienceEventRiskTier.Serious => EventAlertImportance.High,
            ExperienceEventRiskTier.Lethal => EventAlertImportance.High,
            _ => throw new ArgumentOutOfRangeException(
                nameof(riskTier),
                riskTier,
                "Unknown society-event risk tier.")
        };

    public static string FormatRisk(
        ExperienceEventRiskTier riskTier,
        string riskReason)
    {
        SocietyEventRiskContract.RequireValid(
            riskTier,
            riskReason,
            "society-event-alert");
        string label = riskTier switch
        {
            ExperienceEventRiskTier.None => "불이익 없음",
            ExperienceEventRiskTier.Recoverable => "회복 가능한 손실",
            ExperienceEventRiskTier.Serious => "부상·질병·장기 손실",
            ExperienceEventRiskTier.Lethal => "실제 사망 가능",
            _ => throw new ArgumentOutOfRangeException(nameof(riskTier))
        };
        return $"예상 결과 위험: {label}\n위험 근거: {riskReason}";
    }

    public static string BuildTerminalResultSummary(
        V20ResolvedEventResult resolved)
    {
        if (!resolved.IsSocietyTerminal
            || string.IsNullOrWhiteSpace(resolved.TerminalActionLabel)
            || string.IsNullOrWhiteSpace(
                resolved.TerminalOutcomeNarrative))
        {
            throw new InvalidOperationException(
                "A society terminal result requires its frozen action and authored narrative.");
        }
        string effectTypes = resolved.TerminalEffectKinds.Count == 0
            ? "없음"
            : string.Join(", ", resolved.TerminalEffectKinds.Select(
                DescribeEffectKind));
        return $"처리: {resolved.TerminalActionLabel}\n"
            + $"해결 상태: {resolved.ResolutionId}\n"
            + $"작성 결과 서술: {resolved.TerminalOutcomeNarrative}\n"
            + $"적용 효과 유형: {effectTypes}";
    }

    public static void PublishResolved(
        IGameEventBus events,
        V20ResolvedEventResult resolved,
        bool physicalEffectsApplied)
    {
        if (events == null)
        {
            throw new ArgumentNullException(nameof(events));
        }
        if (resolved.IsSocietyTerminal)
        {
            string risk = FormatRisk(resolved.RiskTier, resolved.RiskReason);
            string observedCause = string.IsNullOrWhiteSpace(
                    resolved.ObservedCause)
                ? string.Empty
                : $"\n확인된 발생 사실: {resolved.ObservedCause}";
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                resolved.DisplayName,
                $"사회 사건 처리가 확정되었습니다.{observedCause}\n{risk}",
                ToAlertImportance(resolved.RiskTier),
                "V21 사회 사건",
                Array.Empty<EventAlertChoice>(),
                resolved.InstanceId,
                isResolved: true,
                resultSummary: BuildTerminalResultSummary(resolved))));
        }
        events.Publish(new V20ContentEffectsResolvedEvent(
            resolved.DefinitionId,
            resolved.ResolutionId,
            resolved.Effects,
            physicalEffectsApplied,
            resolved.OccurrenceInstanceId));
    }

    private static string DescribeEffectKind(V20ContentEffectKind kind) =>
        kind switch
        {
            V20ContentEffectKind.Mood => "기분",
            V20ContentEffectKind.Trauma => "트라우마",
            V20ContentEffectKind.SkillExperience => "기술 경험치",
            V20ContentEffectKind.Health => "건강",
            V20ContentEffectKind.Relationship => "관계",
            V20ContentEffectKind.FactionRapport => "세력 우호",
            V20ContentEffectKind.FactionGrievance => "세력 불만",
            V20ContentEffectKind.FactionObligation => "세력 의무",
            V20ContentEffectKind.Money => "자금",
            V20ContentEffectKind.ItemGrant => "물품 획득",
            V20ContentEffectKind.ItemConsume => "물품 소비",
            V20ContentEffectKind.WorldFlag => "세계 상태",
            V20ContentEffectKind.WorkDelayDays => "작업 지연",
            V20ContentEffectKind.Threat => "위협",
            V20ContentEffectKind.DiseaseExposure => "질병 노출",
            V20ContentEffectKind.AmbitionProgress => "야망 진척",
            V20ContentEffectKind.MilestonePressure => "이정표 압박",
            V20ContentEffectKind.RetirementSchedule => "은퇴 일정",
            _ => throw new InvalidOperationException(
                $"Unknown committed society effect type '{kind}'.")
        };
}

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
    string LastObservedWorkAccidentOperationId { get; }
    bool LastObservedWorkAccidentAlertPublished { get; }
    DomainFailure LastObservedWorkAccidentCallbackFailure { get; }
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
    private readonly V20MilestoneWorldSnapshotProjector milestoneProjector;
    private readonly ICharacterNarrativeQuery narratives;
    private readonly ICharacterNarrativeCatalog narrativeCatalog;
    private readonly ICharacterCultureGameplayQuery cultureGameplay;
    private readonly ISeasonalEventQuery seasonalEvents;
    private readonly ISocietyEventQuery societyEvents;
    private readonly ISocietyObservedMealIncidentCommand observedMealIncidents;
    private readonly ISocietyObservedIncidentResponseCommand incidentResponses;
    private readonly IFactionCampaignQuery factionCampaigns;
    private readonly V20StoryContentCatalog storyCatalog;
    private readonly IReproductionService reproduction;
    private readonly ICharacterLifeQuery life;
    private readonly ICareerService careers;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IKinshipQuery kinship;
    private readonly IGriefTraumaService grief;
    private readonly IPopulationHealthQuery populationHealth;
    private readonly IDiseaseDefinitionCatalog diseases;
    private readonly ICharacterBodyHealthQuery bodyHealth;
    private readonly ICharacterConsumablesApplication consumables;
    private readonly ICharacterMedicalCommand medicalCommands;
    private readonly ICharacterMedicalQuery medicalQuery;
    private readonly ICharacterLifetimeQuery characterLifetime;
    private IDisposable dayStartedSubscription;
    private IDisposable mealConsumedSubscription;
    private IDisposable facilityCrimeSubscription;
    private IDisposable visitorFacilityCombatDamageSubscription;
    private IDisposable characterDeathSubscription;
    private IDisposable socialConflictCommittedSubscription;
    private IDisposable workAccidentCommittedSubscription;
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
    public string LastObservedWorkAccidentOperationId { get; private set; } =
        string.Empty;
    public bool LastObservedWorkAccidentAlertPublished { get; private set; }
    public DomainFailure LastObservedWorkAccidentCallbackFailure
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
        V20MilestoneWorldSnapshotProjector milestoneProjector,
        ICharacterNarrativeQuery narratives,
        ICharacterNarrativeCatalog narrativeCatalog,
        ICharacterCultureGameplayQuery cultureGameplay,
        ISeasonalEventQuery seasonalEvents,
        ISocietyEventQuery societyEvents,
        ISocietyObservedMealIncidentCommand observedMealIncidents,
        IFactionCampaignQuery factionCampaigns,
        V20StoryContentCatalog storyCatalog,
        IReproductionService reproduction,
        ICharacterLifeQuery life,
        ICareerService careers,
        IFacilityCapabilityQuery facilities,
        IKinshipQuery kinship,
        IGriefTraumaService grief,
        IPopulationHealthQuery populationHealth,
        IDiseaseDefinitionCatalog diseases,
        ICharacterBodyHealthQuery bodyHealth,
        ICharacterConsumablesApplication consumables,
        ICharacterMedicalCommand medicalCommands,
        ICharacterMedicalQuery medicalQuery,
        ICharacterLifetimeQuery characterLifetime)
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
        this.seasonalEvents = seasonalEvents
            ?? throw new ArgumentNullException(nameof(seasonalEvents));
        this.societyEvents = societyEvents
            ?? throw new ArgumentNullException(nameof(societyEvents));
        this.observedMealIncidents = observedMealIncidents
            ?? throw new ArgumentNullException(nameof(observedMealIncidents));
        incidentResponses = observedMealIncidents
                as ISocietyObservedIncidentResponseCommand
            ?? throw new ArgumentException(
                "Observed incident command must own response state.",
                nameof(observedMealIncidents));
        this.factionCampaigns = factionCampaigns
            ?? throw new ArgumentNullException(nameof(factionCampaigns));
        this.storyCatalog = storyCatalog
            ?? throw new ArgumentNullException(nameof(storyCatalog));
        this.reproduction = reproduction
            ?? throw new ArgumentNullException(nameof(reproduction));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.careers = careers ?? throw new ArgumentNullException(nameof(careers));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.kinship = kinship ?? throw new ArgumentNullException(nameof(kinship));
        this.grief = grief ?? throw new ArgumentNullException(nameof(grief));
        this.populationHealth = populationHealth
            ?? throw new ArgumentNullException(nameof(populationHealth));
        this.diseases = diseases ?? throw new ArgumentNullException(nameof(diseases));
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
        workAccidentCommittedSubscription ??=
            events.Subscribe<CharacterInjuredIdentityEvent>(
                OnWorkAccidentCommitted);
    }

    public void Dispose()
    {
        dayStartedSubscription?.Dispose();
        mealConsumedSubscription?.Dispose();
        facilityCrimeSubscription?.Dispose();
        visitorFacilityCombatDamageSubscription?.Dispose();
        characterDeathSubscription?.Dispose();
        socialConflictCommittedSubscription?.Dispose();
        workAccidentCommittedSubscription?.Dispose();
        dayStartedSubscription = null;
        mealConsumedSubscription = null;
        facilityCrimeSubscription = null;
        visitorFacilityCombatDamageSubscription = null;
        characterDeathSubscription = null;
        socialConflictCommittedSubscription = null;
        workAccidentCommittedSubscription = null;
        startedIncidentTransferOperations.Clear();
    }

    private void OnWorkAccidentCommitted(
        CharacterInjuredIdentityEvent committed)
    {
        if (!committed.HasWorkAccidentEvidence)
        {
            return;
        }

        LastObservedWorkAccidentOperationId =
            committed.WorkAccidentOperationId;
        LastObservedWorkAccidentAlertPublished = false;
        LastObservedWorkAccidentCallbackFailure = DomainFailure.None;
        try
        {
            int currentDay = Math.Max(0, calendar.Day);
            if (committed.AbsoluteDay != currentDay)
            {
                LastObservedWorkAccidentCallbackFailure = new DomainFailure(
                    FailureCode.ExternalInfluenceUnavailable,
                    "work-accident-alert-source-day-mismatch",
                    $"Committed day {committed.AbsoluteDay} does not match current day {currentDay}.");
                return;
            }

            string sourceId = "work-accident:"
                + committed.WorkAccidentOperationId;
            string detail =
                $"피해 대상: {committed.WorkAccidentWorkerDisplayName} ({committed.Character.Value})\n"
                + $"작업: {WorkTaskCatalog.GetDisplayName(committed.WorkAccidentWorkTypeId)}\n"
                + $"현장: {committed.WorkAccidentFacilityDisplayName} "
                + $"({committed.WorkAccidentFacilityInstanceId.Value}, "
                + $"{committed.WorkAccidentLocation.X},{committed.WorkAccidentLocation.Y})\n"
                + $"실제 피해: {committed.WorkAccidentDamagedAnatomyNodeId} "
                + $"-{committed.AppliedDamage:0.###}\n"
                + $"발생 당시 확인된 원인: {committed.WorkAccidentObservedCause}\n"
                + "필요 대응: 의료·회복 화면에서 손상 부위를 실제로 치료하거나 회복 상태를 확인해야 합니다. "
                + "이 알림을 닫아도 피해는 복구되지 않습니다.";
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                "작업 사고",
                detail,
                EventAlertImportance.Medium,
                "V21 사회 사건",
                Array.Empty<EventAlertChoice>(),
                sourceId)));
            LastObservedWorkAccidentAlertPublished = true;
        }
        catch (Exception exception)
        {
            // Anatomy damage is authoritative and has already committed. Alert
            // projection can report failure but must not replay or compensate it.
            LastObservedWorkAccidentCallbackFailure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "work-accident-alert-callback",
                exception.Message);
        }
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
                PublishActionableAlerts();
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
        V20SocietyEventAlertProjection.PublishResolved(
            events,
            new V20ResolvedEventResult(
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
             physicalEffectsApplied: false);
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
            V20SocietyEventAlertProjection.PublishResolved(
                events,
                resolved,
                physicalEffectsApplied: true);
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
                PublishActionableAlerts();
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
                PublishActionableAlerts();
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
                PublishActionableAlerts();
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
                V20SocietyEventAlertProjection.PublishResolved(
                    events,
                    resolved,
                    physicalEffectsApplied: false);
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
                V20SocietyEventAlertProjection.PublishResolved(
                    events,
                    resolved,
                    physicalEffectsApplied: true);
        }
        PublishActionableAlerts();
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
        bool publishedTerminalBeforeComposition = composeEndlessCrisis
            && crisisBeforeComposition.Phase
                == EndlessCrisisLifecyclePhase.None
            && crisisBeforeComposition.InstanceId.Length > 0;
        if (publishedTerminalBeforeComposition)
        {
            PublishEndlessCrisisAlert(
                absoluteDay,
                crisisBeforeComposition);
        }
        if (composeEndlessCrisis)
        {
            endless.ComposeNextEndlessCrisis(started.day, runSeed.RunSeed);
        }
        EndlessCrisisSnapshot crisisAfterComposition =
            endlessQuery.CurrentEndlessCrisis;
        if (!publishedTerminalBeforeComposition
            || !string.Equals(
                crisisBeforeComposition.InstanceId,
                crisisAfterComposition.InstanceId,
                StringComparison.Ordinal))
        {
            PublishEndlessCrisisAlert(
                absoluteDay,
                crisisAfterComposition);
        }
    }

    private void PublishEndlessCrisisAlert(
        int absoluteDay,
        EndlessCrisisSnapshot crisis)
    {
        if (crisis.InstanceId.Length == 0)
        {
            return;
        }

        string axes = string.Join(
            ", ",
            crisis.Axes.Select(value =>
                $"{value.DisplayName} {value.Multiplier * 100f:0}%"));
        string detail;
        bool resolved = false;
        string result = string.Empty;
        switch (crisis.Phase)
        {
            case EndlessCrisisLifecyclePhase.Active:
                string response = crisis.DirectResponseOwnerId.Length > 0
                    ? "\n이 위기에서 시작된 침입 대응이 진행 중입니다."
                    : crisis.PendingInvasionSourceOwnerId.Length > 0
                        ? "\n이 위기의 침입 후보가 대기 중입니다."
                        : string.Empty;
                detail = $"활성 압력: {axes}\n"
                    + $"압력 종료까지 {Math.Max(0, crisis.PressureEndAbsoluteDay - absoluteDay)}일."
                    + response;
                break;
            case EndlessCrisisLifecyclePhase.AwaitingDirectResponse:
                detail = $"압력 종료: {axes}\n"
                    + "이 위기에서 시작된 침입 대응이 끝나야 수복 기간이 시작됩니다. "
                    + "발생한 피해는 자동 복구되지 않습니다.";
                break;
            case EndlessCrisisLifecyclePhase.Recovery:
                detail = $"수복 중: {axes}\n"
                    + $"남은 수복 창 {Math.Max(0, crisis.RecoveryEndAbsoluteDay - absoluteDay)}일. "
                    + $"{crisis.RecoveryEndAbsoluteDay}일 이후 정기 평가에서 다음 위기가 가능해집니다. "
                    + "남은 피해·재고·관계는 직접 수복해야 합니다.";
                break;
            case EndlessCrisisLifecyclePhase.None:
                resolved = true;
                detail = $"위기와 수복 창이 종료되었습니다: {axes}";
                result = $"압력 종료일: {crisis.PressureEndAbsoluteDay}\n"
                    + $"마지막 실제 종료일: {crisis.LastActualEndAbsoluteDay}\n"
                    + $"수복 종료일: {crisis.RecoveryEndAbsoluteDay}\n"
                    + "발생한 피해와 소모는 유지됩니다.";
                break;
            default:
                throw new InvalidOperationException(
                    "Endless crisis alert received an unsupported phase.");
        }

        events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
            "엔드리스 위기",
            detail,
            EventAlertImportance.High,
            "V20 엔드리스 위기",
            Array.Empty<EventAlertChoice>(),
            crisis.InstanceId,
            isResolved: resolved,
            resultSummary: result)));
    }

    private void PublishActionableAlerts()
    {
        PublishSeasonalContractAlerts();
        foreach (V20ActiveEventSaveData active in societyEvents.ActiveSocietyEvents
                     .Where(value => value != null
                         && !value.resolved
                         && (value.guestDelivery?.phase
                                 ?? GuestRequestDeliveryPhase.None)
                             == GuestRequestDeliveryPhase.None)
                     .OrderBy(value => value.instanceId, StringComparer.Ordinal))
        {
            V20AuthoredContentSO definition =
                ((ISocietyEventCatalog)storyCatalog).Require(active.definitionId);
            bool retirement = HasRetirementSchedule(definition);
            bool responseOwned = (active.observedIncidentResponse?.phase
                    ?? ObservedIncidentResponsePhase.None)
                != ObservedIncidentResponsePhase.None;
            EventAlertChoice[] choices = responseOwned
                ? Array.Empty<EventAlertChoice>()
                : SocietyChoices(
                        definition,
                        active.guestDelivery)
                    .Select(choice => new EventAlertChoice(
                        choice.title,
                        SocietyChoiceOutcome(definition, choice, retirement),
                        V21ContentAlertActionIds.Society(
                            active.instanceId,
                            choice.choiceId)))
                    .ToArray();
            string participant = ParticipantNames(active);
            string guestRequirements = definition is GuestRequestDefinitionSO guest
                ? FormatGuestFulfillRequirements(guest.serviceRequirements)
                : string.Empty;
            string delivery = FormatGuestDeliveryState(active.guestDelivery);
            string incidentResponse = FormatObservedIncidentResponseState(
                active.observedIncidentResponse);
            string risk = V20SocietyEventAlertProjection.FormatRisk(
                active.riskTier,
                active.riskReason);
            string introduction = active.hasObservedMealIncident
                ? $"확인된 발생 사실: "
                    + active.observedMealIncident.observedCause
                : definition.Description;
            string displayName = active.hasObservedMealIncident
                && active.observedMealIncident.incidentKind
                    == ServiceIncidentKind.Contamination
                    ? "오염 식사 섭취"
                    : definition.DisplayName;
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                displayName,
                $"{introduction}{participant}{guestRequirements}{delivery}"
                    + $"{incidentResponse}\n{risk}\n기한: {active.deadlineAbsoluteDay}일",
                V20SocietyEventAlertProjection.ToAlertImportance(
                    active.riskTier),
                "V21 사회 사건",
                choices,
                active.instanceId)));
        }

        foreach (FactionCampaignStateSaveData faction in factionCampaigns.Factions
                     .Where(value => value != null
                         && value.currentChapter >= 1
                         && value.currentChapter <= 6)
                     .OrderBy(value => value.factionId, StringComparer.Ordinal))
        {
            FactionChapterDefinitionSO chapter = storyCatalog.Chapters.Single(value =>
                string.Equals(value.factionId, faction.factionId, StringComparison.Ordinal)
                && value.chapterNumber == faction.currentChapter);
            EventAlertChoice[] choices = chapter.choices.Select(choice =>
                new EventAlertChoice(
                    choice.title,
                    choice.outcomeText,
                    V21ContentAlertActionIds.FactionChapter(
                        faction.factionId,
                        choice.choiceId))).ToArray();
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                chapter.DisplayName,
                chapter.Description,
                EventAlertImportance.High,
                "V21 세력 장",
                choices,
                $"{chapter.StableId}:{faction.currentChapter}")));
        }

        foreach (ReproductionProcess process in reproduction.Processes
                     .Where(value => value != null
                         && value.Status == ReproductionProcessStatus.Planned)
                     .OrderBy(value => value.ProcessId, StringComparer.Ordinal))
        {
            string parents = process.SecondParentId.IsValid
                ? $"{process.FirstParentId.Value} + {process.SecondParentId.Value}"
                : process.FirstParentId.Value;
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                "번식 계획 승인",
                $"{parents}\n방식: {process.Mode}\n지원 시설: {process.SupportFacilityInstanceId}",
                EventAlertImportance.High,
                "V21 번식",
                BuildReproductionStartChoices(process),
                process.ProcessId)));
        }

        foreach (CharacterLifeRecord patient in life.Records
                     .Where(value => value != null
                         && (value.LifeStage == CharacterLifeStage.Elder
                             || value.AgeConditions.Count > 0))
                     .OrderBy(value => value.CharacterId.Value, StringComparer.Ordinal))
        {
            List<EventAlertChoice> restorative = new();
            AddAgeTreatmentChoice(
                restorative,
                patient.CharacterId,
                AgeTreatmentKind.OrganRegeneration,
                "장기 재생",
                "building:8868");
            AddAgeTreatmentChoice(
                restorative,
                patient.CharacterId,
                AgeTreatmentKind.BloodRejuvenation,
                "혈액 회춘",
                "building:8869");
            AddAgeTreatmentChoice(
                restorative,
                patient.CharacterId,
                AgeTreatmentKind.RuneHibernation,
                "룬 동면",
                "building:8870");
            AddAgeTreatmentChoice(
                restorative,
                patient.CharacterId,
                AgeTreatmentKind.WholeBodyRegeneration,
                "전신 재생",
                "building:8871");
            if (restorative.Count > 0)
            {
                events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                    "노화 치료 계획",
                    $"환자: {patient.CharacterId.Value}\n활성 노화 질환: {patient.AgeConditions.Count}",
                    EventAlertImportance.Medium,
                    "V21 의료",
                    restorative,
                    $"age-treatment:{patient.CharacterId.Value}")));
            }

            List<EventAlertChoice> stasis = new();
            AddAgeTreatmentChoice(
                stasis,
                patient.CharacterId,
                AgeTreatmentKind.TemporalStasis,
                "시간 고정",
                "building:8872");
            if (stasis.Count > 0)
            {
                events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                    "시간 고정 치료",
                    $"환자: {patient.CharacterId.Value}\n지속 동력과 촉매가 필요한 장기 유지 치료입니다.",
                    EventAlertImportance.Medium,
                    "V21 의료",
                    stasis,
                    $"temporal-stasis:{patient.CharacterId.Value}")));
            }
        }

        CharacterId[] livingIds = milestoneProjector.LivingCharacters
            .Select(CharacterPersistentIdentity.Require)
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        bool traitAnalyzerOperational = facilities.FindOperational(
            FacilityCapabilityKind.Medical,
            "building:8879").Count > 0;
        if (traitAnalyzerOperational)
        {
            foreach (CharacterId subjectId in livingIds)
            {
                if (!narratives.TryGet(
                        subjectId,
                        out CharacterNarrativeSnapshot narrative)
                    || narrative.HeritableTraitsAnalyzed
                    || narrative.LatentHeritableTraitIds.Count == 0)
                {
                    continue;
                }
                events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                    "잠재 형질 분석 가능",
                    $"대상: {subjectId.Value}\n형질 분석기와 검사 키트 1개가 필요합니다.",
                    EventAlertImportance.Medium,
                    "V21 유전",
                    new[]
                    {
                        new EventAlertChoice(
                            "형질 검사",
                            "검사 키트 1개를 사용해 잠재 유전 특성을 공개합니다.",
                            V21ContentAlertActionIds.TraitAnalysis(subjectId))
                    },
                    $"trait-analysis:{subjectId.Value}")));
            }
        }
        PublishCulturalPracticeAlerts(livingIds);
        foreach (CharacterTombstoneSaveData tombstone in kinship.Tombstones
                     .Where(value => value != null
                         && calendar.Day >= value.deathAbsoluteDay
                         && calendar.Day - value.deathAbsoluteDay <= 7)
                     .OrderBy(value => value.deathAbsoluteDay)
                     .ThenBy(value => value.characterId, StringComparer.Ordinal))
        {
            CharacterId deceasedId = new(tombstone.characterId);
            if (!livingIds.Any(value => grief.TryGet(
                    value,
                    out CharacterGriefAggregate state)
                && state.NeedsFuneral(deceasedId)))
            {
                continue;
            }
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                "장례 준비",
                $"사망자: {deceasedId.Value}\n사망일: {tombstone.deathAbsoluteDay}일\n7일 안에 장례를 치르면 남은 슬픔을 줄입니다.",
                EventAlertImportance.High,
                "V21 장례",
                new[]
                {
                    new EventAlertChoice(
                        "장례 개최",
                        "문화에 맞는 추모 시설과 장례 준비품을 사용합니다.",
                        V21ContentAlertActionIds.Funeral(deceasedId))
                },
                $"funeral:{deceasedId.Value}")));
        }

        bool counselingOperational = facilities.FindOperational(
            FacilityCapabilityKind.Medical,
            "building:8885").Count > 0;
        if (counselingOperational)
        {
            foreach (CharacterId patientId in livingIds)
            {
                if (!grief.TryGet(patientId, out CharacterGriefAggregate state)
                    || state.Trauma <= 0f)
                {
                    continue;
                }
                events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                    "상담 치료 가능",
                    $"대상: {patientId.Value}\n현재 트라우마: {state.Trauma:0.#}",
                    EventAlertImportance.Medium,
                    "V21 상담",
                    new[]
                    {
                        new EventAlertChoice(
                            "상담 시작",
                            "상담실과 트라우마 치료 꾸러미를 사용합니다.",
                            V21ContentAlertActionIds.Counseling(patientId))
                    },
                    $"counsel:{patientId.Value}")));
            }
        }

        PublishDiseaseResponseAlerts(livingIds);
    }

    private static string FormatObservedIncidentResponseState(
        ObservedIncidentResponseSaveData response)
    {
        if (response == null
            || response.phase == ObservedIncidentResponsePhase.None)
        {
            return string.Empty;
        }
        string state = response.phase switch
        {
            ObservedIncidentResponsePhase.Accepted => "현장 명령 수락 대기",
            ObservedIncidentResponsePhase.ExternalOperationStarted =>
                "실제 현장 작업 진행 중",
            ObservedIncidentResponsePhase.ReceiptCommitted =>
                "실제 완료 receipt 확인됨",
            ObservedIncidentResponsePhase.Failed => "현장 작업 실패",
            ObservedIncidentResponsePhase.Cancelled => "현장 작업 취소",
            ObservedIncidentResponsePhase.EffectsPublished => "결과 반영 완료",
            _ => throw new ArgumentOutOfRangeException(nameof(response.phase))
        };
        string failure = string.IsNullOrEmpty(response.failureReason)
            ? string.Empty
            : $"\n실패 근거: {response.failureReason}";
        return $"\n대응: {response.choiceId}\n대응 상태: {state}{failure}";
    }

    private void PublishSeasonalContractAlerts()
    {
        foreach (V20ActiveEventSaveData occurrence in seasonalEvents
                     .ActiveSeasonalEvents
                     .Where(value => value != null && !value.resolved)
                     .OrderBy(value => value.instanceId, StringComparer.Ordinal))
        {
            FactionContractDefinitionSO contract = storyCatalog.Contracts
                .SingleOrDefault(value => value != null
                    && string.Equals(
                        value.seasonalEventId,
                        occurrence.definitionId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.factionId,
                        occurrence.contextFactionId,
                        StringComparison.Ordinal));
            if (contract == null
                || !factionCampaigns.TryGetFaction(
                    occurrence.contextFactionId,
                    out FactionCampaignStateSaveData faction)
                || string.Equals(
                        faction.lastSeasonalContractId,
                        contract.StableId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        faction.lastSeasonalOccurrenceId,
                        occurrence.instanceId,
                        StringComparison.Ordinal))
            {
                continue;
            }

            string requirements = string.Join(
                ", ",
                FactionContractMaterialRules.BuildMaterialRequirements(contract)
                    .Select(value => $"{value.Key} {value.Value}개"));
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                contract.DisplayName,
                $"{contract.Description}\n납품: {requirements}\n기한: {occurrence.deadlineAbsoluteDay}일",
                EventAlertImportance.High,
                "V21 계절 세력 요청",
                new[]
                {
                    new EventAlertChoice(
                        "실제 납품 계약을 수락한다",
                        "기한 안에 물자를 실제 인도하면 우호와 의무 토큰을 얻고, 수락 후 실패하면 불만이 오른다.",
                        V21ContentAlertActionIds
                            .SeasonalFactionContractAccept(
                                occurrence.instanceId,
                                contract.StableId))
                },
                occurrence.instanceId)));
        }
    }

    private string ParticipantNames(V20ActiveEventSaveData active)
    {
        string[] participantIds = (active?.participantCharacterIds
                ?? new List<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (participantIds.Length == 0)
        {
            return string.Empty;
        }

        string names = string.Join(
            ", ",
            participantIds.Select(participantId =>
            {
                CharacterActor actor = milestoneProjector.LivingCharacters
                    .FirstOrDefault(value => value?.Identity != null
                        && string.Equals(
                            value.Identity.PersistentId,
                            participantId,
                            StringComparison.Ordinal));
                return string.IsNullOrWhiteSpace(actor?.Identity?.DisplayName)
                    ? participantId
                    : actor.Identity.DisplayName;
            }));
        return "\n대상: " + names;
    }

    private static bool HasRetirementSchedule(V20AuthoredContentSO definition) =>
        SocietyChoices(definition, null)
            .Where(value => value != null)
            .SelectMany(value => value.effects ?? new List<V20ContentEffect>())
            .Any(value => value != null
                && value.kind == V20ContentEffectKind.RetirementSchedule);

    private static string RetirementChoiceOutcome(V20ChoiceDefinition choice)
    {
        V20ContentEffect schedule = (choice?.effects
                ?? new List<V20ContentEffect>())
            .Single(value => value != null
                && value.kind == V20ContentEffectKind.RetirementSchedule);
        string timing = schedule.durationDays == 0
            ? "선택 당일 은퇴"
            : $"선택 후 {schedule.durationDays}일에 은퇴";
        return $"{choice.outcomeText}\n은퇴 시점: {timing}";
    }

    private static string SocietyChoiceOutcome(
        V20AuthoredContentSO definition,
        V20ChoiceDefinition choice,
        bool retirement)
    {
        string outcome = retirement
            ? RetirementChoiceOutcome(choice)
            : choice?.outcomeText ?? string.Empty;
        V20ContentRequirementSet requirements = definition is GuestRequestDefinitionSO guest
            && string.Equals(choice?.choiceId, "fulfill", StringComparison.Ordinal)
            ? guest.serviceRequirements
            : choice?.requirements;
        string items = FormatItemRequirements(requirements);
        if (string.IsNullOrWhiteSpace(items))
        {
            return outcome;
        }

        string label = definition is GuestRequestDefinitionSO
            ? "이행 시 요구 물품"
            : "요구 물품";
        return $"{outcome}\n{label}: {items}";
    }

    private static string FormatGuestFulfillRequirements(
        V20ContentRequirementSet requirements)
    {
        string items = FormatItemRequirements(requirements);
        return string.IsNullOrWhiteSpace(items)
            ? string.Empty
            : "\n요청 이행 시 요구 물품: " + items;
    }

    private static string FormatItemRequirements(
        V20ContentRequirementSet requirements) => string.Join(
        ", ",
        (requirements?.items ?? new List<V20ItemAmountRequirement>())
        .Where(value => value != null
            && !string.IsNullOrWhiteSpace(value.itemDefinitionId)
            && value.amount > 0)
        .OrderBy(value => value.itemDefinitionId, StringComparer.Ordinal)
        .ThenBy(value => value.amount)
        .ThenBy(value => value.consume)
        .Select(value => $"{value.itemDefinitionId} x{value.amount} "
            + (value.consume ? "(소비)" : "(보유 필요)")));

    private void PublishCulturalPracticeAlerts(
        IReadOnlyList<CharacterId> livingIds)
    {
        int absoluteDay = Math.Max(1, calendar.Day);
        foreach (CulturalPracticeDefinitionSO practice in
                 narrativeCatalog.Practices
                     .Where(value => value != null
                         && (absoluteDay - 1) % 10
                             == (int)(PersistentEntityId.GetStableHash32(
                                 value.StableId) % 10u))
                     .OrderBy(value => value.StableId, StringComparer.Ordinal))
        {
            CharacterId[] members = livingIds
                .Where(id => narratives.TryGet(
                        id,
                        out CharacterNarrativeSnapshot narrative)
                    && string.Equals(
                        narrative.CultureId.Value,
                        practice.cultureId,
                        StringComparison.Ordinal)
                    && narratives.CanPerformPractice(
                        id,
                        practice.StableId,
                        absoluteDay,
                        out _))
                .Take(4)
                .ToArray();
            if (members.Length == 0)
            {
                continue;
            }

            List<EventAlertChoice> choices = new()
            {
                new EventAlertChoice(
                    "관습 수행",
                    "해당 문화 구성원이 준비물을 사용해 관습을 수행합니다.",
                    V21ContentAlertActionIds.CulturalPractice(
                        practice.StableId,
                        members))
            };
            choices.Add(new EventAlertChoice(
                "이번에는 생략",
                "준비를 포기하고 이 관습을 지키지 못한 결과를 적용합니다.",
                V21ContentAlertActionIds.CulturalPracticeNeglect(
                    practice.StableId,
                    members)));
            CharacterId guest = livingIds.FirstOrDefault(id =>
                !members.Contains(id)
                && narratives.TryGet(
                    id,
                    out CharacterNarrativeSnapshot narrative)
                && !string.Equals(
                    narrative.CultureId.Value,
                    practice.cultureId,
                    StringComparison.Ordinal)
                && narratives.CanPerformPractice(
                    id,
                    practice.StableId,
                    absoluteDay,
                    out _));
            if (guest.IsValid)
            {
                choices.Add(new EventAlertChoice(
                    "타문화 주민과 함께 수행",
                    "타문화 주민 한 명이 실제로 참여하며 해당 문화의 동화 일수를 얻습니다.",
                    V21ContentAlertActionIds.CulturalPractice(
                        practice.StableId,
                        members.Take(3).Append(guest))));
            }

            string requirements = string.Join(
                ", ",
                (practice.requirements?.items
                    ?? new List<V20ItemAmountRequirement>())
                .Where(value => value != null)
                .Select(value =>
                    $"{value.itemDefinitionId} x{value.amount}"));
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                practice.DisplayName,
                $"{practice.Description}\n준비물: {requirements}\n재사용 대기: 10일",
                EventAlertImportance.Medium,
                "V21 문화 관습",
                choices,
                $"practice:{practice.StableId}:{absoluteDay}")));
        }
    }

    private void PublishDiseaseResponseAlerts(IReadOnlyList<CharacterId> livingIds)
    {
        foreach (CharacterId patientId in livingIds)
        {
            if (!populationHealth.TryGetCharacterSnapshot(
                    patientId,
                    out PopulationCharacterHealthSnapshot health))
                continue;
            foreach (ActiveDiseaseSnapshot active in health.ActiveDiseases
                         .Where(value => calendar.Day >= value.SymptomDay
                             && calendar.Day < value.RecoveryDay)
                         .OrderBy(value => value.DiseaseId, StringComparer.Ordinal))
            {
                DiseaseDefinition disease = diseases.Require(active.DiseaseId);
                List<EventAlertChoice> choices = new();
                foreach (string responseId in disease.FieldResponseIds)
                {
                    string definitionId = responseId.StartsWith(
                            "response:vaccine:",
                            StringComparison.Ordinal)
                        ? "building:8876"
                        : responseId.StartsWith(
                            "response:isolate:",
                            StringComparison.Ordinal)
                            ? "building:8874"
                            : "building:8873";
                    BuildableObject facility = facilities.FindOperational(
                            FacilityCapabilityKind.Medical,
                            definitionId)
                        .FirstOrDefault();
                    if (facility == null) continue;
                    choices.Add(new EventAlertChoice(
                        responseId,
                        "가동 시설과 해당 대응 물자를 다시 검사한 뒤 질병 대응을 수행합니다.",
                        V21ContentAlertActionIds.DiseaseResponse(
                            patientId,
                            disease.Id,
                            responseId,
                            facility.PersistentInstanceId.Value)));
                }
                if (choices.Count == 0) continue;
                events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                    $"질병 대응: {disease.DisplayName}",
                    $"환자: {patientId.Value}\n증상: {disease.SymptomProfileId}\n중증도: {active.Severity:0.#}",
                    EventAlertImportance.High,
                    "V21 질병 대응",
                    choices,
                    $"disease-response:{patientId.Value}:{disease.Id}")));
            }
        }
    }

    private void AddAgeTreatmentChoice(
        ICollection<EventAlertChoice> choices,
        CharacterId patientId,
        AgeTreatmentKind treatment,
        string label,
        string buildingDefinitionId)
    {
        BuildableObject facility = facilities.FindOperational(
                FacilityCapabilityKind.Medical,
                buildingDefinitionId)
            .FirstOrDefault();
        if (facility == null)
        {
            return;
        }
        choices.Add(new EventAlertChoice(
            label,
            "환자·의료진·가동 시설·물리 의료품을 요구하는 수술 주문을 생성합니다.",
            V21ContentAlertActionIds.AgeTreatment(
                patientId,
                treatment,
                facility.PersistentInstanceId.Value)));
    }

    private static IReadOnlyList<V20ChoiceDefinition> SocietyChoices(
        V20AuthoredContentSO definition,
        GuestRequestDeliverySaveData delivery)
    {
        if (definition is GuestRequestDefinitionSO)
        {
            if (delivery?.phase is GuestRequestDeliveryPhase.AwaitingVenue
                    or GuestRequestDeliveryPhase.Delivering)
            {
                return new[]
                {
                    new V20ChoiceDefinition
                    {
                        choiceId = "decline",
                        title = "요청 취소",
                        outcomeText = "운반 중인 물품을 회수한 뒤 요청을 거절합니다."
                    }
                };
            }
            if (delivery?.phase is GuestRequestDeliveryPhase.ReleasePending
                    or GuestRequestDeliveryPhase.PhysicalCommitted
                    or GuestRequestDeliveryPhase.RewardPublished)
            {
                return Array.Empty<V20ChoiceDefinition>();
            }
            return new[]
            {
                new V20ChoiceDefinition
                {
                    choiceId = "fulfill",
                    title = "요청 이행",
                    outcomeText = "요구 시설과 물품을 사용해 요청을 이행합니다."
                },
                new V20ChoiceDefinition
                {
                    choiceId = "decline",
                    title = "거절",
                    outcomeText = "요청을 거절하고 실패 결과를 적용합니다."
                }
            };
        }
        return definition switch
        {
            LifeEventDefinitionSO life => life.choices,
            ServiceIncidentDefinitionSO incident => incident.responses,
            _ => Array.Empty<V20ChoiceDefinition>()
        };
    }

    private static string FormatGuestDeliveryState(
        GuestRequestDeliverySaveData delivery)
    {
        if (delivery == null
            || delivery.phase == GuestRequestDeliveryPhase.None)
        {
            return string.Empty;
        }
        string venue = string.IsNullOrEmpty(delivery.venueFacilityInstanceId)
            ? "시설 배정 대기"
            : $"시설 {delivery.venueFacilityInstanceId} "
                + $"({delivery.destinationX}, {delivery.destinationY})";
        string reason = string.IsNullOrWhiteSpace(delivery.failureReason)
            ? string.Empty
            : " · " + delivery.failureReason;
        return $"\n납품 진행: {delivery.phase} · {venue}{reason}";
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

    private static IReadOnlyList<EventAlertChoice> BuildReproductionStartChoices(
        ReproductionProcess process)
    {
        if (process.Mode == ReproductionMode.GolemAssembly)
        {
            return new[]
            {
                new EventAlertChoice(
                    "시작",
                    "시설과 물품을 다시 검증하고 조립 과정을 시작합니다.",
                    V21ContentAlertActionIds.ReproductionStart(
                        process.ProcessId))
            };
        }
        return new[]
        {
            new EventAlertChoice(
                "일반 시작",
                "시설과 필수 물품을 다시 검증하고 번식 과정을 시작합니다.",
                V21ContentAlertActionIds.ReproductionStart(
                    process.ProcessId)),
            new EventAlertChoice(
                "생식 치료 후 시작",
                "생식 치료제 1개를 추가 소비해 수태 성공률과 임신 안정성을 높입니다.",
                V21ContentAlertActionIds.ReproductionStart(
                    process.ProcessId,
                    useFertilityTreatment: true))
        };
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
