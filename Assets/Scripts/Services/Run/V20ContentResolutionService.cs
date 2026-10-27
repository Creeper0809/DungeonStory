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
    private readonly V20FactionContractDeliveryRuntime factionDeliveries;
    private readonly V20ContentEffectTransaction effectTransaction;
    private readonly V20ContentEffectRollback effectRollback;
    private readonly V20RetirementSchedulePlanner retirementPlanner;
    private readonly V20ContentResolutionJournal journal;
    private readonly V20ObservedLifeEventCommands observedLifeEvents;

    internal V20ContentResolutionService(
        V20CampaignRuntime live,
        V20StoryContentCatalog catalog,
        IContentRequirementEvaluator requirements,
        ICareerPersistence careers,
        IItemReservationService reservations,
        IWorldItemStackRuntime items,
        IFacilityCapabilityQuery facilities,
        IPhysicalItemBatchDispositionService physicalDispositions,
        IPhysicalFacilityItemBatchTransferGateway factionContractTransfers,
        IEconomyProjectInputOwnerPort factionContractInputOwners,
        RunAdministrativeSealDurableEquipmentRuntime administrativeSealEquipment,
        ISocialRitualFactPublisher factPublisher,
        V20ContentResolutionRequestPlanner requestPlanner,
        V20FactionContractQuery factionContractQuery,
        V20FactionContractDeliveryRuntime factionDeliveries,
        V20ContentEffectTransaction effectTransaction,
        V20ContentEffectRollback effectRollback,
        V20RetirementSchedulePlanner retirementPlanner,
        V20ContentResolutionJournal journal,
        V20ObservedLifeEventCommands observedLifeEvents)
    {
        this.live = live;
        this.catalog = catalog;
        this.requirements = requirements;
        this.careers = careers;
        this.reservations = reservations;
        this.items = items;
        this.facilities = facilities;
        this.physicalDispositions = physicalDispositions;
        this.factionContractTransfers = factionContractTransfers;
        this.factionContractInputOwners = factionContractInputOwners;
        this.administrativeSealEquipment = administrativeSealEquipment;
        this.factPublisher = factPublisher;
        this.requestPlanner = requestPlanner;
        this.factionContractQuery = factionContractQuery;
        this.factionDeliveries = factionDeliveries;
        this.effectTransaction = effectTransaction;
        this.effectRollback = effectRollback;
        this.retirementPlanner = retirementPlanner;
        this.journal = journal;
        this.observedLifeEvents = observedLifeEvents;
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
            && !factionDeliveries.TryValidateLiveFactionDeliveryReceipt(
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
            && !factionDeliveries.TryEnsureAcceptedMaterialInputOwner(
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
            factionDeliveries.CompensateAcceptedMaterialOwnerOrThrow(
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
            factionDeliveries.CompensateAcceptedMaterialOwnerOrThrow(
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
        bool resolutionEffectsStarted = false;
        DurableFacilityEquipmentUseEvidence administrativeEquipment = null;
        bool CommitResolution()
        {
            resolutionEffectsStarted = true;
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
            // 인장 공급 대기는 효과 실행 전이다. 전체 물품을 되돌리면 방금 만든
            // 실제 배송 요청도 사라져 재시도가 영원히 공급 대기에 머문다.
            if (resolutionEffectsStarted)
                effectRollback.RestoreEffectOwners(ownersBefore, request.ActionId);
            factionDeliveries.CompensateAcceptedMaterialOwnerOrThrow(
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

    public bool RequiresAdvance => factionDeliveries.RequiresAdvance;

    public void ValidateBeforeCapture()
    {
        factPublisher.AssertIdle();
        foreach (var recovery in live.CaptureMilestones().pendingContentResolutions)
            V20ContentRecoveryEvidence.RequirePhysicalReceipts(recovery, items as IPhysicalEffectReceiptQuery);
        factionDeliveries.ValidateBeforeCapture();

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

    public void AdvanceFactionContractDeliveries(int absoluteDay, RunMilestoneEvaluationSnapshot requirements) =>
        factionDeliveries.AdvanceFactionContractDeliveries(absoluteDay, requirements, request =>
        {
            bool succeeded = TryExecute(request, out _, out DomainFailure failure);
            return (succeeded, failure);
        });

}
