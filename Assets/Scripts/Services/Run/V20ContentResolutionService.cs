using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;
using static V20ContentEffectEvidence;
using EffectCommitPlan = V20ContentEffectTransaction.EffectCommitPlan;
using RetirementSchedulePlan = V20RetirementSchedulePlanner.RetirementSchedulePlan;
using static V20ContentResolutionJournal;
using static V20ContentEffectTransaction;

public sealed class V20ContentResolutionService :
    IContentResolutionService,
    IObservedCareerLifeEventCommand,
    IRetirementScheduleContentQuery,
    IFactionContractDeliveryQuery,
    IFactionContractDeliveryResolutionCommand,
    IDungeonSaveCaptureGuard
{
    private readonly V20CampaignRuntime live;
    private readonly V20StoryContentCatalog catalog;
    private readonly IContentRequirementEvaluator requirements;
    private readonly ICareerPersistence careers;
    private readonly IItemReservationService reservations;
    private readonly IWorldItemStackRuntime items;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IPhysicalItemBatchDispositionService physicalDispositions;
    private readonly IPhysicalFacilityItemBatchTransferGateway
        factionContractTransfers;
    private readonly IEconomyProjectInputOwnerPort factionContractInputOwners;
    private readonly RunAdministrativeSealDurableEquipmentRuntime
        administrativeSealEquipment;
    private readonly ISocialRitualFactPublisher factPublisher;
    private readonly V20ContentResolutionRequestPlanner requestPlanner;
    private readonly V20FactionContractQuery factionContractQuery;
    private readonly V20ContentEffectTransaction effectTransaction;
    private readonly V20ContentEffectRollback effectRollback;
    private readonly V20RetirementSchedulePlanner retirementPlanner;
    private readonly V20ContentResolutionJournal journal;
    private readonly V20ObservedLifeEventCommands observedLifeEvents;

    private static V21ContentEffectExecutionRegistry RequireRegistry(V20CampaignRuntime live,
        V21ContentEffectExecutionRegistry configured)
    {
        if (configured != null && !ReferenceEquals(configured, live.EffectHandlers))
            throw new ArgumentException("Campaign and content resolution must share the same effect registry.");
        return live.EffectHandlers;
    }

    public V20ContentResolutionService(
        V20CampaignRuntime live,
        V20StoryContentCatalog catalog,
        ICharacterNarrativeCatalog narrativeCatalog,
        IContentRequirementEvaluator requirements,
        ICharacterWorldQuery characters,
        ICareerPersistence careers,
        ICareerService careerQuery,
        IGameCalendar calendar,
        IKinshipQuery kinship,
        ICharacterNarrativeQuery narrativeQuery,
        ICharacterProficiencyQuery proficiencyQuery,
        ICharacterNarrativeCommand narrative,
        IGriefTraumaService grief,
        ICharacterBodyHealthCommand bodyHealth,
        IPopulationHealthService populationHealth,
        IDiseaseDefinitionCatalog diseases,
        IStockQuery stock,
        IItemReservationService reservations,
        IIdempotentAtomicItemConsumptionService atomicItems,
        IWorldItemStackRuntime items,
        IPhysicalItemExactSourcePublicationService exactSources,
        IWorldDropZoneQuery dropZones,
        IGameMoneyAccount money,
        IFacilityCapabilityQuery facilities,
        IPhysicalItemBatchDispositionService physicalDispositions,
        IPhysicalFacilityItemBatchTransferGateway factionContractTransfers,
        IEconomyProjectInputOwnerPort factionContractInputOwners,
        RunAdministrativeSealDurableEquipmentRuntime administrativeSealEquipment,
        ISocialRitualFactPublisher factPublisher,
        V21ContentEffectExecutionRegistry effectHandlers = null)
    {
        this.live = live ?? throw new ArgumentNullException(nameof(live));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        narrativeCatalog = narrativeCatalog
            ?? throw new ArgumentNullException(nameof(narrativeCatalog));
        this.requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
        characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.careers = careers ?? throw new ArgumentNullException(nameof(careers));
        careerQuery = careerQuery
            ?? throw new ArgumentNullException(nameof(careerQuery));
        calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        kinship = kinship
            ?? throw new ArgumentNullException(nameof(kinship));
        narrativeQuery = narrativeQuery
            ?? throw new ArgumentNullException(nameof(narrativeQuery));
        proficiencyQuery = proficiencyQuery
            ?? throw new ArgumentNullException(nameof(proficiencyQuery));
        narrative = narrative ?? throw new ArgumentNullException(nameof(narrative));
        grief = grief ?? throw new ArgumentNullException(nameof(grief));
        bodyHealth = bodyHealth ?? throw new ArgumentNullException(nameof(bodyHealth));
        populationHealth = populationHealth
            ?? throw new ArgumentNullException(nameof(populationHealth));
        diseases = diseases ?? throw new ArgumentNullException(nameof(diseases));
        stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
        atomicItems = atomicItems
            ?? throw new ArgumentNullException(nameof(atomicItems));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        exactSources = exactSources
            ?? throw new ArgumentNullException(nameof(exactSources));
        dropZones = dropZones ?? throw new ArgumentNullException(nameof(dropZones));
        money = money ?? throw new ArgumentNullException(nameof(money));
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        this.physicalDispositions = physicalDispositions
            ?? throw new ArgumentNullException(nameof(physicalDispositions));
        this.factionContractTransfers = factionContractTransfers
            ?? throw new ArgumentNullException(nameof(factionContractTransfers));
        this.factionContractInputOwners = factionContractInputOwners
            ?? throw new ArgumentNullException(nameof(factionContractInputOwners));
        this.administrativeSealEquipment = administrativeSealEquipment
            ?? throw new ArgumentNullException(nameof(administrativeSealEquipment));
        this.factPublisher = factPublisher
            ?? throw new ArgumentNullException(nameof(factPublisher));
        requestPlanner = new V20ContentResolutionRequestPlanner(
            this.live, this.catalog, narrativeCatalog,
            narrativeQuery, dropZones);
        factionContractQuery = new V20FactionContractQuery(
            this.live, this.catalog, this.items, facilities, dropZones);
        var typedEffects = new V20ContentTypedEffectRuntime(
            characters, bodyHealth, grief, populationHealth,
            narrative, narrativeCatalog, narrativeQuery, diseases, RequireRegistry(this.live, effectHandlers), calendar);
        effectRollback = new V20ContentEffectRollback(
            this.live, characters, this.careers, bodyHealth, grief,
            populationHealth, narrative, this.items, money, typedEffects.Handlers);
        effectTransaction = new V20ContentEffectTransaction(
            dropZones, this.items, money,
            this.reservations, stock, atomicItems, exactSources, typedEffects, effectRollback);
        retirementPlanner = new V20RetirementSchedulePlanner(this.requirements, this.careers);
        journal = new V20ContentResolutionJournal(
            this.live, this.careers, exactSources, this.items, money,
            this.factPublisher, typedEffects, effectRollback, retirementPlanner);
        observedLifeEvents = new V20ObservedLifeEventCommands(
            this.live, this.catalog, characters, careerQuery, calendar,
            kinship, proficiencyQuery, narrative, this.factPublisher,
            physicalDispositions, this.reservations, effectTransaction, effectRollback, journal);
    }

    internal V20ContentResolutionService(
        V20CampaignRuntime live,
        V20StoryContentCatalog catalog,
        IContentRequirementEvaluator requirements,
        ICharacterWorldQuery characters,
        IStockQuery stock,
        IIdempotentAtomicItemConsumptionService atomicItems,
        ISocialRitualFactPublisher factPublisher,
        ICharacterNarrativeCommand narrative = null,
        IPhysicalItemExactSourcePublicationService exactSources = null,
        ICharacterNarrativeCatalog narrativeCatalog = null,
        IItemReservationService reservations = null,
        IWorldItemStackRuntime items = null,
        IWorldDropZoneQuery dropZones = null,
        IGameMoneyAccount money = null,
        ICharacterBodyHealthCommand bodyHealth = null,
        ICareerPersistence careers = null,
        ICareerService careerQuery = null,
        IPopulationHealthService populationHealth = null,
        IDiseaseDefinitionCatalog diseases = null,
        IGriefTraumaService grief = null,
        V21ContentEffectExecutionRegistry effectHandlers = null,
        IGameCalendar calendar = null)
    {
        this.live = live ?? throw new ArgumentNullException(nameof(live));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.requirements = requirements
            ?? throw new ArgumentNullException(nameof(requirements));
        characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        this.careers = careers;
        IKinshipQuery kinship = null;
        ICharacterNarrativeQuery narrativeQuery = narrative as ICharacterNarrativeQuery;
        ICharacterProficiencyQuery proficiencyQuery = null;
        stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.reservations = reservations;
        atomicItems = atomicItems
            ?? throw new ArgumentNullException(nameof(atomicItems));
        this.items = items;
        facilities = null;
        physicalDispositions = null;
        factionContractTransfers = null;
        factionContractInputOwners = null;
        administrativeSealEquipment = null;
        this.factPublisher = factPublisher
            ?? throw new ArgumentNullException(nameof(factPublisher));
        requestPlanner = new V20ContentResolutionRequestPlanner(
            this.live, this.catalog, narrativeCatalog,
            narrativeQuery, dropZones);
        factionContractQuery = new V20FactionContractQuery(
            this.live, this.catalog, this.items, facilities, dropZones);
        var typedEffects = new V20ContentTypedEffectRuntime(
            characters, bodyHealth, grief, populationHealth,
            narrative, narrativeCatalog, narrativeQuery, diseases, RequireRegistry(this.live, effectHandlers), calendar);
        effectRollback = new V20ContentEffectRollback(
            this.live, characters, this.careers, bodyHealth, grief,
            populationHealth, narrative, this.items, money, typedEffects.Handlers);
        effectTransaction = new V20ContentEffectTransaction(
            dropZones, this.items, money,
            this.reservations, stock, atomicItems, exactSources, typedEffects, effectRollback);
        retirementPlanner = new V20RetirementSchedulePlanner(this.requirements, this.careers);
        journal = new V20ContentResolutionJournal(
            this.live, this.careers, exactSources, this.items, money,
            this.factPublisher, typedEffects, effectRollback, retirementPlanner);
        observedLifeEvents = new V20ObservedLifeEventCommands(
            this.live, this.catalog, characters, careerQuery, calendar,
            kinship, proficiencyQuery, narrative, this.factPublisher,
            physicalDispositions, this.reservations, effectTransaction, effectRollback, journal);
    }

    public bool CanScheduleRetirement(CharacterId characterId)
    {
        return retirementPlanner.CanScheduleRetirement(characterId);
    }

    internal void RequireCanCommitObservedFuneralLifeEvent(
        ObservedFuneralLifeEventReceipt receipt)
    {
        observedLifeEvents.RequireCanCommitObservedFuneralLifeEvent(receipt);
    }

    internal IReadOnlyList<V20ResolvedEventResult>
        CommitObservedFuneralLifeEvent(
            ObservedFuneralLifeEventReceipt receipt,
            DomainFactLease sourceLease)
    {
        return observedLifeEvents.CommitObservedFuneralLifeEvent(receipt, sourceLease);
    }

    [GameplayInternalOnly(
        "Commits the source-owned apprentice-mistake occurrence.",
        "ProductionRecipeExecutionReceiptAuthority only")]
    bool IObservedCareerLifeEventCommand.TryCaptureProductionDeclaredLoss(
        ProductionDeclaredLossCycleReceipt source,
        out bool stateChanged,
        out string failureReason)
    {
        return ((IObservedCareerLifeEventCommand)observedLifeEvents)
            .TryCaptureProductionDeclaredLoss(
                source, out stateChanged, out failureReason);
    }

    [GameplayInternalOnly(
        "Commits the source-owned last-lesson occurrence.",
        "CareerApplicationAdapter only")]
    bool IObservedCareerLifeEventCommand.TryCaptureLastLesson(
        CareerMentorshipSnapshot mentorship,
        CharacterCareerSnapshot retirement,
        CombatEquipmentInstance protectiveEquipment,
        CareerMentorshipAwardCommitReceipt award,
        int absoluteDay,
        out bool stateChanged,
        out string failureReason)
    {
        return ((IObservedCareerLifeEventCommand)observedLifeEvents)
            .TryCaptureLastLesson(
                mentorship, retirement, protectiveEquipment, award,
                absoluteDay, out stateChanged, out failureReason);
    }

    [GameplayInternalOnly(
        "Commits the source-owned quiet-promotion occurrence.",
        "ObservedProficiencyPromotionApplicationAdapter only")]
    bool IObservedCareerLifeEventCommand.TryCaptureQuietPromotion(
        CharacterProficiencyAwardCommitReceipt source,
        out bool stateChanged,
        out string failureReason)
    {
        return ((IObservedCareerLifeEventCommand)observedLifeEvents)
            .TryCaptureQuietPromotion(
                source, out stateChanged, out failureReason);
    }

    public bool TryExecute(
        ContentResolutionRequest request,
        out ContentResolutionResult result,
        out DomainFailure failure)
    {
        factPublisher.AssertIdle();
        result = null;
        failure = DomainFailure.None;
        string requestedActionId = request?.ActionId?.Trim() ?? string.Empty;
        V20ContentResolutionRecoverySaveData requestedRecovery =
            requestedActionId.Length == 0
                ? null
                : live.CaptureMilestones().pendingContentResolutions
                    .SingleOrDefault(value => value != null && string.Equals(
                        value.actionId,
                        requestedActionId,
                        StringComparison.Ordinal));
        IReadOnlyList<V20ResolvedEventResult> recoveredResults = null;
        if (!journal.TryRecoverPendingContentResolutions(out failure,
                (actionId, values) => { if (actionId == requestedActionId) recoveredResults = values; }))
        {
            return false;
        }
        if (request == null || string.IsNullOrWhiteSpace(request.ActionId))
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        if (requestedRecovery != null)
        {
            V20ResolvedEventResult[] recoveredResolutions = (recoveredResults
                ?? throw new InvalidOperationException("Completed recovery did not return its frozen resolutions.")).ToArray();
            ContentResolutionRequest recoveredRequest = new()
            {
                ActionId = requestedRecovery.actionId,
                AbsoluteDay = requestedRecovery.absoluteDay,
                Kind = requestedRecovery.requestKind,
                InstanceId = recoveredResolutions
                    .Select(value => value.InstanceId)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            };
            result = new ContentResolutionResult
            {
                ActionId = requestedRecovery.actionId,
                Resolutions = recoveredResolutions,
                Disposition = IsPendingGuestRequestChoice(
                        live.CreateContentResolutionCandidate(physicalDispositions),
                        recoveredRequest)
                    ? ContentResolutionDisposition.AcceptedPending
                    : ContentResolutionDisposition.Terminal
            };
            return true;
        }
        if (request.Kind == ContentResolutionRequestKind.FactionContractOutcome
            && request.ContractSucceeded
            && !TryValidateLiveFactionDeliveryReceipt(
                request.FactionId,
                out failure))
        {
            return false;
        }

        RunMilestoneEvaluationSnapshot world = request.Requirements
            ?? request.DailyContext?.Requirements;
        if (!requestPlanner.TryResolveRequestRequirements(
                request,
                world,
                out V20ContentRequirementSet requestedRequirements,
                out IReadOnlyList<string> requestedParticipants,
                out failure)
            || !requirements.TryEvaluate(
                requestedRequirements,
                world,
                requestedParticipants,
                out failure))
        {
            return false;
        }

        V20CampaignRuntime candidate = live.CreateContentResolutionCandidate(physicalDispositions);
        var campaignObservations = new List<(V20ContentEffect, V20ContentEffectOwnerSnapshot, V20ContentEffectOwnerSnapshot)>();
        candidate.ObserveCampaignEffects((effect, before, after) => campaignObservations.Add((effect, before, after)));
        IReadOnlyList<V20ResolvedEventResult> resolved;
        try
        {
            if (!requestPlanner.TryResolveCandidate(candidate, request, out resolved, out failure))
                return false;
            RecordCommittedChoice(candidate, request, resolved);
            resolved = requestPlanner.ExpandAmbitionCompletionRewards(candidate, resolved);
            resolved = CaptureCampaignApplications(resolved, campaignObservations, effectTransaction.Handlers);
        }
        catch (ContentEffectHandlerMissingException exception)
        {
            failure = new DomainFailure(FailureCode.ServiceFeatureMissing, "content-effect-handler:" + exception.Kind);
            return false;
        }
        foreach (V20ResolvedEventResult resolution in resolved)
        {
            if (!requirements.TryEvaluate(
                    requestPlanner.RequirementsForResolvedEffect(resolution),
                    world,
                    resolution.ParticipantCharacterIds,
                    out failure))
            {
                return false;
            }
        }

        if (!effectTransaction.TryPreflightEffects(
                request,
                resolved,
                out EffectCommitPlan plan,
                out failure))
        {
            return false;
        }
        if (!retirementPlanner.TryPrepareRetirementSchedule(
                request,
                resolved,
                out RetirementSchedulePlan retirementPlan,
                out failure))
        {
            plan.Release(reservations);
            return false;
        }

        BuildableObject administrationOffice = null;
        if (RequiresAdministrativeSeal(request.Kind))
        {
            administrationOffice = facilities
                .FindOperational(FacilityCapabilityKind.Administration)
                .FirstOrDefault();
            if (administrationOffice == null)
            {
                plan.Release(reservations);
                failure = new DomainFailure(
                    FailureCode.ServiceFeatureMissing,
                    FacilityCapabilityKind.Administration.ToString());
                return false;
            }
        }

        bool acceptedMaterialOwnerEnsured = false;
        string acceptedMaterialDestinationId = string.Empty;
        if ((request.Kind is ContentResolutionRequestKind.FactionContractAccept
                or ContentResolutionRequestKind.SeasonalFactionContractAccept)
            && !TryEnsureAcceptedMaterialInputOwner(
                candidate,
                request.FactionId,
                out acceptedMaterialOwnerEnsured,
                out acceptedMaterialDestinationId,
                out failure))
        {
            plan.Release(reservations);
            return false;
        }

        SeasonalEventAggregateState seasonal;
        SocietyEventAggregateState society;
        FactionCampaignAggregateState factions;
        RunMilestoneAggregateState milestones;
        try
        {
            seasonal = live.PrepareSeasonal(candidate.CaptureSeasonal());
            society = live.PrepareSociety(candidate.CaptureSociety());
            factions = live.PrepareFactions(candidate.CaptureFactions(), seasonal);
            milestones = live.PrepareMilestones(candidate.CaptureMilestones());
        }
        catch
        {
            CompensateAcceptedMaterialOwnerOrThrow(
                acceptedMaterialOwnerEnsured,
                acceptedMaterialDestinationId,
                "candidate-prepare-failed");
            throw;
        }

        V20ContentEffectOwnerSnapshot ownersBefore;
        try
        {
            ownersBefore = effectRollback.CaptureEffectOwners(resolved);
        }
        catch
        {
            CompensateAcceptedMaterialOwnerOrThrow(
                acceptedMaterialOwnerEnsured,
                acceptedMaterialDestinationId,
                "effect-owner-capture-failed");
            plan.Release(reservations);
            throw;
        }
        string stateBeforeFingerprint =
            CaptureEffectOwnerFingerprint(ownersBefore);
        string stateAfterFingerprint = string.Empty;

        bool requiresForwardRecovery = RequiresForwardRecovery(plan, resolved, effectTransaction.Handlers);
        DomainFailure effectFailure = DomainFailure.None;
        V20ContentResolutionRecoveryPhase effectPhase =
            V20ContentResolutionRecoveryPhase.Prepared;
        V20ContentEffectCommitReferences childCommits = new();
        Exception transactionFailure = null;
        bool durableRecoveryPending = false;
        bool recoveryPublished = false;
        DurableFacilityEquipmentUseEvidence administrativeEquipment = null;
        bool CommitResolution()
        {
            try
            {
                if (!effectTransaction.TryCommitEffects(
                        request,
                        resolved,
                        plan,
                        out effectFailure,
                        out effectPhase,
                        out childCommits))
                {
                    return false;
                }
                childCommits.SetDurableEquipment(administrativeEquipment);
                resolved = childCommits.AppliedResolutions;

                CharacterCareerAggregate latestCareer = null;
                if (retirementPlan.IsRequired)
                {
                    latestCareer = careers.PrepareRestore(careers.Capture());
                    string retirementFailure =
                        "character is no longer an eligible living elder";
                    if (!retirementPlanner.IsEligibleLivingElder(retirementPlan.CharacterId)
                        || !latestCareer.TryScheduleRetirement(
                            retirementPlan.CharacterId,
                            retirementPlan.EventId,
                            retirementPlan.ChoiceId,
                            retirementPlan.DecisionAbsoluteDay,
                            retirementPlan.DueAbsoluteDay,
                            out retirementFailure))
                    {
                        throw new InvalidOperationException(
                            "Retirement eligibility changed after external effects; "
                            + "latest career state was preserved. "
                            + retirementFailure);
                    }
                }

                V20ContentEffectOwnerSnapshot ownersAfter = effectRollback.CaptureEffectOwners(resolved);
                ownersAfter.Seasonal = seasonal.Data;
                ownersAfter.Society = society.Data;
                ownersAfter.Factions = factions.Data;
                ownersAfter.Milestones = milestones.Data;
                if (latestCareer != null)
                    ownersAfter.Careers = latestCareer.CaptureWorld();
                resolved = AttachApplicationEvidence(
                    resolved,
                    ownersBefore,
                    ownersAfter,
                    childCommits, effectTransaction.Handlers);
                stateAfterFingerprint = CaptureEffectOwnerFingerprint(
                    ownersAfter);

                if (requiresForwardRecovery)
                {
                    UpsertRecovery(
                        milestones,
                        CreateRecovery(
                            request,
                            resolved,
                            plan.Dropoff,
                            V20ContentResolutionRecoveryPhase.TypedEffectsApplied,
                            childCommits,
                            stateBeforeFingerprint,
                            stateAfterFingerprint));
                }
                if (latestCareer != null)
                {
                    live.PublishContentResolution(
                        seasonal,
                        society,
                        factions,
                        milestones,
                        latestCareer);
                }
                else
                {
                    live.PublishContentResolution(
                        seasonal,
                        society,
                        factions,
                        milestones);
                }
                recoveryPublished = requiresForwardRecovery;
                journal.PublishResolvedFacts(
                    request.ActionId,
                    request.AbsoluteDay,
                    resolved,
                    childCommits,
                    stateBeforeFingerprint,
                    stateAfterFingerprint);
                if (requiresForwardRecovery)
                {
                    journal.AdvanceContentResolutionRecovery(
                        request.ActionId,
                        V20ContentResolutionRecoveryPhase.OutcomesCommitted);
                    journal.RemoveContentResolutionRecovery(request.ActionId);
                }
                return true;
            }
            catch (Exception exception)
            {
                transactionFailure = exception;
                if (requiresForwardRecovery && effectPhase
                    >= V20ContentResolutionRecoveryPhase.ItemCostsCommitted)
                {
                    if (!recoveryPublished)
                    {
                        if (effectPhase
                            >= V20ContentResolutionRecoveryPhase.ItemGrantsCommitted)
                        {
                            effectRollback.RestoreTypedEffectOwners(ownersBefore);
                            resolved = resolved.Select(value => value.WithApplications(value.Applications
                                .Where(application => effectTransaction.Handlers.Require(value.Effects[application.effectIndex].kind).Stage
                                    is not (V20ContentEffectStage.Typed or V20ContentEffectStage.Retirement)).ToArray())).ToArray();
                            effectPhase = V20ContentResolutionRecoveryPhase
                                .ItemGrantsCommitted;
                        }
                        UpsertRecovery(
                            milestones,
                            CreateRecovery(
                                request,
                                resolved,
                                plan.Dropoff,
                                effectPhase,
                                childCommits,
                                stateBeforeFingerprint,
                                string.Empty));
                        live.PublishContentResolution(
                            seasonal,
                            society,
                            factions,
                            milestones);
                        recoveryPublished = true;
                    }
                    durableRecoveryPending = true;
                    return true;
                }
                return false;
            }
        }

        bool transactionCommitted;
        try
        {
            transactionCommitted = administrationOffice != null
                ? administrativeSealEquipment.TryCommitResolution(
                    administrationOffice.RequirePersistentInstanceId(),
                    administrationOffice.centerPos,
                    context =>
                    {
                        administrativeEquipment =
                            new DurableFacilityEquipmentUseEvidence(context);
                        return CommitResolution();
                    },
                    out _,
                    out _)
                : CommitResolution();
        }
        catch (Exception exception)
        {
            transactionFailure ??= exception;
            transactionCommitted = false;
        }

        if (durableRecoveryPending)
        {
            plan.Release(reservations);
            if (transactionFailure != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo
                    .Capture(transactionFailure)
                    .Throw();
            }
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "content-resolution-recovery-pending");
            return false;
        }

        if (!transactionCommitted)
        {
            effectRollback.RestoreEffectOwners(ownersBefore, request.ActionId);
            CompensateAcceptedMaterialOwnerOrThrow(
                acceptedMaterialOwnerEnsured,
                acceptedMaterialDestinationId,
                "content-transaction-failed");
            plan.Release(reservations);
            if (transactionFailure != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo
                    .Capture(transactionFailure)
                    .Throw();
            }
            failure = effectFailure.IsFailure
                ? effectFailure
                : new DomainFailure(
                    FailureCode.ServiceFeatureMissing,
                    DurableToolItemRules.AdministrativeSeal);
            return false;
        }
        result = new ContentResolutionResult
        {
            ActionId = request.ActionId.Trim(),
            Resolutions = resolved,
            Disposition = IsPendingGuestRequestChoice(candidate, request)
                ? ContentResolutionDisposition.AcceptedPending
                : ContentResolutionDisposition.Terminal
        };
        if (request.Kind is ContentResolutionRequestKind.FactionContractAccept
                or ContentResolutionRequestKind.SeasonalFactionContractAccept)
            AdvanceFactionContractDeliveries(
                Math.Max(1, request.AbsoluteDay),
                request.Requirements);
        return true;
    }

    private static bool RequiresAdministrativeSeal(
        ContentResolutionRequestKind kind) =>
        kind is ContentResolutionRequestKind.FactionChapterChoice
            or ContentResolutionRequestKind.FactionContractAccept
            or ContentResolutionRequestKind.SeasonalFactionContractAccept
            or ContentResolutionRequestKind.FactionContractOutcome;

    private static void RecordCommittedChoice(
        V20CampaignRuntime candidate,
        ContentResolutionRequest request,
        IReadOnlyList<V20ResolvedEventResult> resolved)
    {
        if (request.Kind is not (ContentResolutionRequestKind.SocietyChoice
            or ContentResolutionRequestKind.GuestRequestDeliveryOutcome
            or ContentResolutionRequestKind.FactionChapterChoice))
        {
            return;
        }

        if (request.Kind == ContentResolutionRequestKind.GuestRequestDeliveryOutcome
            && string.Equals(
                request.ChoiceId,
                GuestRequestDeliveryOutbox.ExpiredDisposition,
                StringComparison.Ordinal))
        {
            return;
        }

        if (request.Kind == ContentResolutionRequestKind.SocietyChoice
            && (resolved?.Count ?? 0) == 0)
        {
            return;
        }

        if (resolved == null || resolved.Count != 1)
        {
            throw new InvalidOperationException(
                "Explicit content choice must resolve exactly one authored decision.");
        }

        V20ResolvedEventResult decision = resolved[0];
        bool society = request.Kind is ContentResolutionRequestKind.SocietyChoice
            or ContentResolutionRequestKind.GuestRequestDeliveryOutcome;
        candidate.RecordCommittedChoice(
            society
                ? CommittedRunChoiceKind.SocietyEventChoice
                : CommittedRunChoiceKind.FactionChapterChoice,
            society
                ? V20CampaignRuntime.SocietyChoiceOwnerId
                : decision.ContextFactionId,
            decision.DefinitionId,
            society ? request.InstanceId : decision.DefinitionId,
            decision.ResolutionId,
            request.ActionId);
    }

    private static bool IsPendingGuestRequestChoice(
        V20CampaignRuntime candidate,
        ContentResolutionRequest request) =>
        request.Kind == ContentResolutionRequestKind.SocietyChoice
        && candidate.ActiveSocietyEvents.Any(value => value != null
            && string.Equals(
                value.instanceId,
                request.InstanceId?.Trim(),
                StringComparison.Ordinal)
            && ((value.guestDelivery?.phase
                        ?? GuestRequestDeliveryPhase.None)
                    != GuestRequestDeliveryPhase.None
                || (value.observedIncidentResponse?.phase
                        ?? ObservedIncidentResponsePhase.None)
                    != ObservedIncidentResponsePhase.None));

    public bool RequiresAdvance => live.CaptureFactions().factions
        .Where(value => value != null)
        .Any(value => FactionContractDeliveryOutbox.HasPending(value)
            || value.deliveryTerminalCleanupPending
            || (!string.IsNullOrWhiteSpace(value.activeContractId)
                && catalog.Contracts.Any(contract =>
                    contract != null
                    && string.Equals(
                        contract.StableId,
                        value.activeContractId,
                        StringComparison.Ordinal)
                    && FactionContractMaterialRules.HasMaterialRequirements(
                        contract))));

    public void ValidateBeforeCapture()
    {
        factPublisher.AssertIdle();
        foreach (var recovery in live.CaptureMilestones().pendingContentResolutions)
            V20ContentRecoveryEvidence.RequirePhysicalReceipts(recovery, items as IPhysicalEffectReceiptQuery);
        FactionCampaignStateSaveData[] states = live.CaptureFactions().factions
            .Where(value => value != null)
            .ToArray();
        Dictionary<string, FactionCampaignStateSaveData> campaignPending = states
            .Where(FactionContractDeliveryOutbox.HasPending)
            .ToDictionary(
                value => value.deliveryOperationId,
                value => value,
                StringComparer.Ordinal);

        foreach (FactionCampaignStateSaveData state in campaignPending.Values)
        {
            if (!FactionContractDeliveryOutbox.HasCanonicalPending(state)
                || !factionContractTransfers.TryGetPending(
                    state.deliveryOperationId,
                    out PhysicalItemBatchDispositionReceipt physical)
                || !FactionContractDeliveryOutbox.ReceiptMatchesSaved(
                    state,
                    physical))
            {
                throw new InvalidOperationException(
                    "Faction-contract save capture crossed a campaign/physical "
                    + $"receipt transition for '{state.factionId}'.");
            }
        }

        foreach (FactionCampaignStateSaveData state in states.Where(value =>
                     !string.IsNullOrEmpty(value.activeContractOccurrenceId)
                     && !string.IsNullOrEmpty(value.activeContractId)))
        {
            FactionContractDefinitionSO contract = catalog.Contracts
                .FirstOrDefault(value => value != null
                    && string.Equals(
                        value.StableId,
                        state.activeContractId,
                        StringComparison.Ordinal)
                    && !string.IsNullOrEmpty(value.seasonalEventId));
            if (contract == null
                || !FactionContractMaterialRules.HasMaterialRequirements(contract))
                continue;
            string operationId = FactionContractDeliveryOutbox.FormatOperationId(
                contract.StableId,
                state.activeContractOccurrenceId);
            if (factionContractTransfers.TryGetPending(operationId, out _)
                && !campaignPending.ContainsKey(operationId))
            {
                throw new InvalidOperationException(
                    "Faction-contract save capture crossed the physical-commit/"
                    + $"campaign-record transition for seasonal occurrence "
                    + $"'{state.activeContractOccurrenceId}'.");
            }
        }

        foreach (FactionContractDefinitionSO contract in catalog.Contracts
                     .Where(value => string.IsNullOrEmpty(value.seasonalEventId))
                     .Where(FactionContractMaterialRules.HasMaterialRequirements))
        {
            string operationId = FactionContractDeliveryOutbox.FormatOperationId(
                contract.StableId);
            if (factionContractTransfers.TryGetPending(operationId, out _)
                && !campaignPending.ContainsKey(operationId))
            {
                throw new InvalidOperationException(
                    "Faction-contract save capture crossed the physical-commit/"
                    + $"campaign-record transition for '{contract.StableId}'.");
            }
        }

        SocietyEventWorldSaveData societySnapshot = live.CaptureSociety();
        _ = live.PrepareSociety(societySnapshot);
        foreach (V20ActiveEventSaveData active in societySnapshot.activeEvents
                     .Where(value => value != null
                         && (value.guestDelivery?.phase
                                 ?? GuestRequestDeliveryPhase.None)
                             != GuestRequestDeliveryPhase.None))
        {
            GuestRequestDeliverySaveData delivery = active.guestDelivery;
            GuestRequestDefinitionSO definition = catalog.GuestRequests
                .Single(value => string.Equals(
                    value.StableId,
                    active.definitionId,
                    StringComparison.Ordinal));
            if (delivery.inputOwnerActive
                && !factionContractInputOwners.TryValidate(
                    EconomyProjectInputOwnerAuthority.GuestRequestDomain,
                    active.instanceId,
                    delivery.destinationId,
                    new Vector2Int(
                        delivery.destinationX,
                        delivery.destinationY),
                    EconomyProjectInputOwnerAnchorKind.LiveFacility,
                    delivery.venueFacilityInstanceId,
                    GuestRequestDeliveryMaterialRules
                        .BuildMaterialRequirements(definition),
                    delivery.inputCapacityGrams,
                    delivery.inputMassAuthorityRevision,
                    delivery.inputCapacityFingerprint,
                    out string ownerFailure))
            {
                throw new InvalidOperationException(
                    $"Guest-request save capture crossed input owner '{active.instanceId}': "
                    + ownerFailure);
            }

            string operationId = GuestRequestDeliveryOutbox.FormatOperationId(
                active.instanceId);
            bool hasPhysical = factionContractTransfers.TryGetPending(
                operationId,
                out PhysicalItemBatchDispositionReceipt physicalReceipt);
            bool hasSaved = GuestRequestDeliveryOutbox.IsCanonicalReceipt(
                delivery);
            if (hasPhysical != hasSaved
                || hasSaved && !GuestRequestDeliveryOutbox.MatchesSaved(
                    delivery,
                    physicalReceipt))
            {
                throw new InvalidOperationException(
                    $"Guest-request save capture crossed physical receipt '{operationId}' for occurrence '{active.instanceId}'.");
            }
        }
    }

    public IReadOnlyList<FactionContractView> GetContracts(string factionId)
    {
        return factionContractQuery.GetContracts(factionId);
    }

    public void AdvanceFactionContractDeliveries(
        int absoluteDay,
        RunMilestoneEvaluationSnapshot requirements)
    {
        if (requirements == null)
            return;
        foreach (FactionCampaignStateSaveData snapshot in
                 live.CaptureFactions().factions
                     .Where(value => value != null)
                     .OrderBy(value => value.factionId, StringComparer.Ordinal))
        {
            AdvanceFactionContractDelivery(
                snapshot.factionId,
                Math.Max(1, absoluteDay),
                requirements);
        }
    }

    private void AdvanceFactionContractDelivery(
        string factionId,
        int absoluteDay,
        RunMilestoneEvaluationSnapshot requirements)
    {
        if (!live.TryGetFaction(
                factionId,
                out FactionCampaignStateSaveData state))
            return;

        if (state.deliveryTerminalCleanupPending)
        {
            if (!TryRetireFactionContractInputOwner(
                    state,
                    out string retireFailure))
            {
                PublishDeliveryFailure(factionId, retireFailure);
                return;
            }
            state = RequireLiveFaction(factionId);
        }

        if (state.deliveryCommitPhase ==
            FactionContractDeliveryCommitPhase.RewardPublished)
        {
            if (!TryGetMatchingPendingReceipt(
                    state,
                    out PhysicalItemBatchDispositionReceipt receipt,
                    out string receiptFailure))
            {
                PublishDeliveryFailure(factionId, receiptFailure);
                return;
            }
            if (!factionContractTransfers.Acknowledge(
                    receipt.CommitId,
                    out string acknowledgementFailure))
            {
                PublishDeliveryFailure(factionId, acknowledgementFailure);
                return;
            }
            PublishFactionMutation(
                factionId,
                (V20CampaignRuntime candidate, out string mutationFailure) =>
                    ((IFactionCampaignDeliveryCommand)candidate)
                        .TryClearDeliveryOutbox(
                            factionId,
                            out mutationFailure),
                "faction-contract-outbox-clear");
            return;
        }

        if (state.deliveryCommitPhase ==
            FactionContractDeliveryCommitPhase.PhysicalCommitted)
        {
            ContentResolutionRequest outcome = new()
            {
                ActionId = V21ContentAlertActionIds.FactionContractOutcome(
                    factionId,
                    succeeded: true),
                Kind = ContentResolutionRequestKind.FactionContractOutcome,
                FactionId = factionId,
                ContractSucceeded = true,
                AbsoluteDay = absoluteDay,
                Requirements = requirements
            };
            if (!TryExecute(outcome, out _, out DomainFailure outcomeFailure))
                PublishDeliveryFailure(
                    factionId,
                    outcomeFailure.Code.ToString());
            return;
        }

        if (string.IsNullOrWhiteSpace(state.activeContractId))
            return;
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(
                value.StableId,
                state.activeContractId,
                StringComparison.Ordinal));
        IReadOnlyDictionary<string, int> material =
            FactionContractMaterialRules.BuildMaterialRequirements(contract);
        if (material.Count == 0)
            return;
        if (!state.activeContractInputOwnerActive
            || string.IsNullOrWhiteSpace(state.activeContractDestinationId))
        {
            PublishDeliveryFailure(
                factionId,
                "faction-contract-input-owner-inactive");
            return;
        }

        string operationId = FactionContractDeliveryOutbox.FormatOperationId(
            contract.StableId,
            state.activeContractOccurrenceId);
        if (factionContractTransfers.TryGetPending(
                operationId,
                out PhysicalItemBatchDispositionReceipt existingReceipt))
        {
            PublishFactionMutation(
                factionId,
                (V20CampaignRuntime candidate, out string mutationFailure) =>
                    ((IFactionCampaignDeliveryCommand)candidate)
                        .TryRecordDeliveryReceipt(
                            factionId,
                            FactionContractDeliveryReceipt.FromPhysical(
                                existingReceipt),
                            out mutationFailure),
                "faction-contract-existing-receipt-record");
            return;
        }

        Vector2Int destination = new(
            state.activeContractDestinationX,
            state.activeContractDestinationY);
        bool requestFailed = false;
        string requestFailure = string.Empty;
        foreach (KeyValuePair<string, int> required in material)
        {
            int assigned = factionContractQuery.CountFactionContractQuantity(
                state.activeContractDestinationId,
                required.Key,
                stack => stack.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored
                    or WorldItemStackState.FacilityOutputBuffer
                    or WorldItemStackState.Carried
                    or WorldItemStackState.InTransit
                    or WorldItemStackState.FacilityBuffer);
            int missing = Math.Max(0, required.Value - assigned);
            if (missing <= 0)
                continue;
            if (!items.TryRequestItemDelivery(
                    required.Key,
                    missing,
                    destination,
                    state.activeContractDestinationId,
                    out int requested,
                    out requestFailure)
                || requested != missing)
            {
                requestFailed = true;
                if (string.IsNullOrWhiteSpace(requestFailure))
                {
                    requestFailure =
                        $"faction-contract-delivery-request-partial:"
                        + $"{required.Key}:{requested}/{missing}";
                }
                break;
            }
        }
        if (requestFailed)
        {
            PublishDeliveryFailure(factionId, requestFailure);
            return;
        }

        bool allArrived = material.All(required =>
            factionContractQuery.CountFactionContractQuantity(
                state.activeContractDestinationId,
                required.Key,
                stack => stack.State == WorldItemStackState.FacilityBuffer)
            >= required.Value);
        if (!allArrived)
        {
            PublishDeliveryFailure(factionId, string.Empty);
            return;
        }

        if (absoluteDay > state.activeContractDeadlineAbsoluteDay)
        {
            PublishDeliveryFailure(
                factionId,
                "faction-contract-deadline-expired-before-transfer");
            return;
        }
        if (!this.requirements.TryEvaluate(
                FactionContractMaterialRules.BuildNonMaterialRequirements(
                    contract),
                requirements,
                Array.Empty<string>(),
                out DomainFailure eligibilityFailure))
        {
            PublishDeliveryFailure(
                factionId,
                "faction-contract-non-material-condition:"
                + eligibilityFailure.Code);
            return;
        }

        if (!factionContractTransfers.TryCommitTransferPending(
                state.activeContractDestinationId,
                material,
                operationId,
                FactionContractDeliveryOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt transferReceipt,
                out string transferFailure))
        {
            PublishDeliveryFailure(factionId, transferFailure);
            return;
        }
        PublishFactionMutation(
            factionId,
            (V20CampaignRuntime candidate, out string mutationFailure) =>
                ((IFactionCampaignDeliveryCommand)candidate)
                    .TryRecordDeliveryReceipt(
                        factionId,
                        FactionContractDeliveryReceipt.FromPhysical(
                            transferReceipt),
                        out mutationFailure),
            "faction-contract-receipt-record");
    }

    private bool TryEnsureAcceptedMaterialInputOwner(
        V20CampaignRuntime candidate,
        string factionId,
        out bool ensured,
        out string destinationId,
        out DomainFailure failure)
    {
        ensured = false;
        destinationId = string.Empty;
        failure = DomainFailure.None;
        if (!candidate.TryGetFaction(
                factionId,
                out FactionCampaignStateSaveData state))
            return true;
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(
                value.StableId,
                state.activeContractId,
                StringComparison.Ordinal));
        IReadOnlyDictionary<string, int> material =
            FactionContractMaterialRules.BuildMaterialRequirements(contract);
        if (material.Count == 0)
            return true;

        destinationId = state.activeContractDestinationId;
        if (!factionContractInputOwners.TryEnsure(
                EconomyProjectInputOwnerAuthority.FactionContractDomain,
                state.activeContractDestinationOwnerId,
                destinationId,
                new Vector2Int(
                    state.activeContractDestinationX,
                    state.activeContractDestinationY),
                EconomyProjectInputOwnerAnchorKind.ReservedTarget,
                string.Empty,
                material,
                0L,
                0L,
                string.Empty,
                out EconomyProjectInputOwnerProjection projection,
                out string ownerFailure))
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable);
            return false;
        }
        ensured = true;
        if (!((IFactionCampaignDeliveryCommand)candidate)
                .TrySetInputOwnerProjection(
                    factionId,
                    projection.CapacityGrams,
                    projection.MassAuthorityRevision,
                    projection.Fingerprint,
                    out string projectionFailure))
        {
            CompensateAcceptedMaterialOwnerOrThrow(
                true,
                destinationId,
                projectionFailure);
            ensured = false;
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable);
            return false;
        }
        return true;
    }

    private void CompensateAcceptedMaterialOwnerOrThrow(
        bool ensured,
        string destinationId,
        string reason)
    {
        if (!ensured)
            return;
        if (!factionContractInputOwners.TryRetireDestination(
                EconomyProjectInputOwnerAuthority.FactionContractDomain,
                destinationId,
                EconomyProjectInputOwnerAuthority.FactionContractTerminalReason,
                out string failureReason))
        {
            throw new InvalidOperationException(
                $"Faction-contract accept failed ({reason}) and input-owner compensation failed: "
                + failureReason);
        }
    }

    private bool TryValidateLiveFactionDeliveryReceipt(
        string factionId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!live.TryGetFaction(
                factionId,
                out FactionCampaignStateSaveData state))
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(
                value.StableId,
                state.activeContractId,
                StringComparison.Ordinal));
        if (!FactionContractMaterialRules.HasMaterialRequirements(contract))
            return true;
        if (!TryGetMatchingPendingReceipt(state, out _, out _))
        {
            failure = new DomainFailure(FailureCode.ItemTransferStackUnavailable);
            return false;
        }
        return true;
    }

    private bool TryGetMatchingPendingReceipt(
        FactionCampaignStateSaveData state,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        if (!FactionContractDeliveryOutbox.HasCanonicalPending(state)
            || !factionContractTransfers.TryGetPending(
                state.deliveryOperationId,
                out receipt)
            || !FactionContractDeliveryOutbox.ReceiptMatchesSaved(
                state,
                receipt))
        {
            failureReason =
                "faction-contract-delivery-receipt-missing-or-mismatch";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private bool TryRetireFactionContractInputOwner(
        FactionCampaignStateSaveData state,
        out string failureReason)
    {
        if (state == null || !state.activeContractInputOwnerActive)
        {
            failureReason = string.Empty;
            return true;
        }
        if (!factionContractInputOwners.TryRetireDestination(
                EconomyProjectInputOwnerAuthority.FactionContractDomain,
                state.activeContractDestinationId,
                EconomyProjectInputOwnerAuthority.FactionContractTerminalReason,
                out failureReason))
            return false;
        PublishFactionMutation(
            state.factionId,
            (V20CampaignRuntime candidate, out string mutationFailure) =>
                ((IFactionCampaignDeliveryCommand)candidate)
                    .TryMarkInputOwnerRetired(
                        state.factionId,
                        out mutationFailure),
            "faction-contract-input-owner-retire");
        return true;
    }

    private delegate bool FactionCandidateMutation(
        V20CampaignRuntime candidate,
        out string failure);

    private void PublishFactionMutation(
        string factionId,
        FactionCandidateMutation mutation,
        string operation)
    {
        V20CampaignRuntime candidate = live.CreateContentResolutionCandidate(physicalDispositions);
        if (!mutation(candidate, out string failure))
            throw new InvalidOperationException(
                $"{operation} rejected faction '{factionId}': {failure}");
        live.PublishFactions(live.PrepareFactions(candidate.CaptureFactions()));
    }

    private void PublishDeliveryFailure(string factionId, string reason)
    {
        FactionCampaignStateSaveData state = RequireLiveFaction(factionId);
        string canonical = reason?.Trim() ?? string.Empty;
        if (string.Equals(
                state.deliveryFailureReason,
                canonical,
                StringComparison.Ordinal))
            return;
        PublishFactionMutation(
            factionId,
            (V20CampaignRuntime candidate, out string mutationFailure) =>
                ((IFactionCampaignDeliveryCommand)candidate)
                    .TrySetDeliveryFailure(
                        factionId,
                        canonical,
                        out mutationFailure),
            "faction-contract-delivery-status");
    }

    private FactionCampaignStateSaveData RequireLiveFaction(string factionId)
    {
        if (!live.TryGetFaction(factionId, out FactionCampaignStateSaveData state))
            throw new InvalidOperationException(
                $"Unknown faction contract campaign '{factionId}'.");
        return state;
    }
}
