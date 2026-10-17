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
    IV20ObservedMealIncidentDiagnostic,
    IDungeonSaveRestoreCompletedHook
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
    private readonly V20StoryContentCatalog storyCatalog;
    private readonly ICharacterLifeQuery life;
    private readonly ICareerService careers;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly ICharacterBodyHealthQuery bodyHealth;
    private readonly ICharacterLifetimeQuery characterLifetime;
    private IDisposable dayStartedSubscription;
    private IDisposable mealConsumedSubscription;
    private IDisposable facilityCrimeSubscription;
    private IDisposable visitorFacilityCombatDamageSubscription;
    private IDisposable characterDeathSubscription;
    private IDisposable socialConflictCommittedSubscription;
    private readonly V20ObservedIncidentResponseRuntime incidentResponseRuntime;

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
        ISocietyObservedIncidentResponseCommand incidentResponses = observedMealIncidents
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
        _ = consumables
            ?? throw new ArgumentNullException(nameof(consumables));
        _ = medicalCommands
            ?? throw new ArgumentNullException(nameof(medicalCommands));
        _ = medicalQuery
            ?? throw new ArgumentNullException(nameof(medicalQuery));
        this.characterLifetime = characterLifetime
            ?? throw new ArgumentNullException(nameof(characterLifetime));
        _ = exteriorIncidents
            ?? throw new ArgumentNullException(nameof(exteriorIncidents));
        incidentResponseRuntime = new V20ObservedIncidentResponseRuntime(
            contentResolution, calendar, runSeed, events, milestoneProjector,
            societyEvents, incidentResponses, storyCatalog, facilities, bodyHealth,
            consumables, medicalCommands, medicalQuery, characterLifetime,
            exteriorIncidents);
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
        incidentResponseRuntime.ResetTransferAttempts();
    }

    public void Tick() => incidentResponseRuntime.Tick();

    public void OnRestoreCompleted() => incidentResponseRuntime.ResetTransferAttempts();

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
                    incidentResponseRuntime.BuildCurrentEventContext(absoluteDay));
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
                    incidentResponseRuntime.BuildCurrentEventContext(absoluteDay));
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
            V20DailyEventContext context = incidentResponseRuntime.BuildCurrentEventContext(absoluteDay);
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

    private void OnMealConsumed(PhysicalMealConsumedEvent consumed)
    {
        LastObservedMealIncidentCallbackFailure = DomainFailure.None;
        LastObservedMealIncidentResult = NotEligibleMealResult(
            "observed-meal-incident-callback-not-completed");
        try
        {
            CharacterActor actor = consumed.Actor;
            incidentResponseRuntime.TryCaptureReplacementMealReceipt(consumed, actor);
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
            V20DailyEventContext context = incidentResponseRuntime.BuildCurrentEventContext(absoluteDay);
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
                incidentResponseRuntime.ReleaseObservedIncidentResponseProvenance(
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
        V20ObservedIncidentResponseRuntime.CopySnapshot(snapshot, context.Requirements);
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


}
