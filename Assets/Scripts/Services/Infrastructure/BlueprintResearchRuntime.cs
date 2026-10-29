using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

// Unity lifecycle/application adapter over DungeonStory.Research state and rules.
public class BlueprintResearchRuntime : MonoBehaviour, IFacilityShopFulfillmentQuery, IFacilityShopPurchaseDelivery
{
    [SerializeField] private bool raiseAlertOnResearchComplete = true;

    private BlueprintResearchState state = new BlueprintResearchState();
    private IFacilityShopUnlockStateAuthority shopUnlockStateService;
    private IFacilityShopCatalog facilityShopCatalog;
    private IFacilityCandidateCache facilityCandidateCache;
    private IWorkforceReplanService workforceReplanService;
    private IWorldItemStackRuntime itemStackRuntime;
    private BlueprintResearchProjectCoordinator projectCoordinator;
    private IWorldDropZoneQuery worldDropZoneQuery;
    private BlueprintResearchApplicationAdapter applicationAdapter;
    private IFacilityBufferDestinationClaimQuery archiveDestinationClaimQuery;
    private IFacilityBufferMassCapacityQuery archiveCapacityQuery;
    private IFacilityBufferDestinationLifecycleCommand archiveDestinations;
    private IFacilityBufferDestinationReleaseService archiveReleases;
    private IPhysicalItemMassQuery physicalMass;
    private float nextArchiveDeliveryRefresh;
    private int projectedRestoreRevision;
    private ExtremeTraitRuntime extremeTraits;
    private IRunSeedProvider runSeedProvider;
    private IGameClock gameClock;
    private IGameCalendar calendar;
    private CharacterIdentityEventPublisher identityEvents;
    private BlueprintResearchOutcomeTransaction gameplayOutcomes;
    private string pendingPublicationFailure = string.Empty;

    [Inject]
    public void ConstructGameplayOutcomes(BlueprintResearchOutcomeTransaction outcomes)
    {
        gameplayOutcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
    }

    public BlueprintResearchState State => state.CreateSnapshot();
    internal BlueprintResearchState OwnedState => state;
#if UNITY_EDITOR
    public BlueprintResearchState StateForDebug => state;
#endif
    public bool HasActiveResearch =>
        TryResolveActiveProject(out _, out _)
        || state.HasActiveTask;
    public IResearchProjectCatalog ProjectCatalog => projectCatalog;
    public IResearchFacilityCapacityQuery ResearchFacilityCapacity =>
        researchFacilityCapacity;
    private IResearchProjectCatalog projectCatalog =>
        projectCoordinator.ProjectCatalog;
    private IResearchBlueprintArchiveQuery blueprintArchiveQuery =>
        projectCoordinator.BlueprintArchiveQuery;
    private IResearchFacilityCapacityQuery researchFacilityCapacity =>
        projectCoordinator.ResearchFacilityCapacity;
    public FacilityShopUnlockState ShopUnlockState => OwnedShopUnlockState.CreateSnapshot();
    internal FacilityShopUnlockState OwnedShopUnlockState =>
        ResolveShopUnlockStateService().GetOwnedUnlockState();

    [Inject]
    public void ConstructRuntime(
        IFacilityShopUnlockStateAuthority shopUnlockStateService,
        IFacilityShopCatalog facilityShopCatalog,
        IFacilityCandidateCache facilityCandidateCache,
        IWorkforceReplanService workforceReplanService,
        IWorldItemStackRuntime itemStackRuntime,
        BlueprintResearchProjectCoordinator projectCoordinator,
        IWorldDropZoneQuery worldDropZoneQuery,
        BlueprintResearchApplicationAdapter applicationAdapter)
    {
        this.shopUnlockStateService = shopUnlockStateService
            ?? throw new ArgumentNullException(nameof(shopUnlockStateService));
        this.facilityShopCatalog = facilityShopCatalog
            ?? throw new ArgumentNullException(nameof(facilityShopCatalog));
        this.facilityCandidateCache = facilityCandidateCache
            ?? throw new ArgumentNullException(nameof(facilityCandidateCache));
        this.workforceReplanService = workforceReplanService
            ?? throw new ArgumentNullException(nameof(workforceReplanService));
        this.itemStackRuntime = itemStackRuntime;
        this.projectCoordinator = projectCoordinator
            ?? throw new ArgumentNullException(nameof(projectCoordinator));
        this.worldDropZoneQuery = worldDropZoneQuery;
        this.applicationAdapter = applicationAdapter
            ?? throw new ArgumentNullException(nameof(applicationAdapter));
        state = this.applicationAdapter.CreateState();
        projectedRestoreRevision =
            this.applicationAdapter.PublishedRestoreRevision;
    }

    [Inject]
    public void ConstructFounderTraitRuntime(
        ExtremeTraitRuntime extremeTraits,
        IRunSeedProvider runSeedProvider,
        IGameClock gameClock,
        IGameCalendar calendar,
        CharacterIdentityEventPublisher identityEvents)
    {
        this.extremeTraits = extremeTraits
            ?? throw new ArgumentNullException(nameof(extremeTraits));
        this.runSeedProvider = runSeedProvider
            ?? throw new ArgumentNullException(nameof(runSeedProvider));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.calendar = calendar
            ?? throw new ArgumentNullException(nameof(calendar));
        this.identityEvents = identityEvents
            ?? throw new ArgumentNullException(nameof(identityEvents));
    }

    [Inject]
    public void ConstructArchiveDestinationAuthority(
        IFacilityBufferDestinationClaimQuery destinationClaimQuery,
        IFacilityBufferMassCapacityQuery capacityQuery,
        IFacilityBufferDestinationLifecycleCommand destinations,
        IFacilityBufferDestinationReleaseService releases,
        IPhysicalItemMassQuery mass)
    {
        archiveDestinationClaimQuery = destinationClaimQuery
            ?? throw new ArgumentNullException(nameof(destinationClaimQuery));
        archiveCapacityQuery = capacityQuery
            ?? throw new ArgumentNullException(nameof(capacityQuery));
        archiveDestinations = destinations
            ?? throw new ArgumentNullException(nameof(destinations));
        archiveReleases = releases
            ?? throw new ArgumentNullException(nameof(releases));
        physicalMass = mass
            ?? throw new ArgumentNullException(nameof(mass));
    }

    public bool EnqueueBlueprint(FacilityBlueprintSO blueprint)
    {
        if (blueprint != null
            && projectCatalog.TryGetForBlueprint(blueprint.id, out ResearchProjectSO project))
        {
            return EnqueueProject(project.ProjectId).Succeeded;
        }

        bool queued = state.EnqueueBlueprint(blueprint);
        if (queued)
        {
            NotifyResearchAvailabilityChanged(prioritizeResearch: true);
            applicationAdapter.RaiseLowAlert(
                "연구 대기",
                $"{blueprint.DisplayName} 분석 가능",
                "연구");
        }

        return queued;
    }

    public BlueprintResearchWorkResult ApplyResearchWork(
        CharacterActor researcher,
        BuildableObject researchFacility,
        float seconds) =>
        ApplyResearchWorkInternal(
            researcher,
            researchFacility,
            seconds,
            approvedWorkUnits: false);

    public BlueprintResearchWorkResult ApplyApprovedResearchWork(
        CharacterActor researcher,
        BuildableObject researchFacility,
        float approvedWorkUnits,
        DurableFacilityEquipmentUseContext equipment = null) =>
        ApplyResearchWorkInternal(
            researcher,
            researchFacility,
            approvedWorkUnits,
            approvedWorkUnits: true,
            equipment: equipment);

    private BlueprintResearchWorkResult ApplyResearchWorkInternal(
        CharacterActor researcher,
        BuildableObject researchFacility,
        float amount,
        bool approvedWorkUnits,
        DurableFacilityEquipmentUseContext equipment = null)
    {
        if (!RetryPendingResearchPublication())
            return new BlueprintResearchWorkResult(false, null, 0, 0, 1, false, pendingPublicationFailure);
        if (researchFacility == null || !researchFacility.SupportsWork(BuiltInWorkTypeIds.Research))
        {
            return new BlueprintResearchWorkResult(false, null, 0f, 0f, 1f, false, "연구 가능한 시설이 아닙니다");
        }

        if (TryResolveActiveProject(out ResearchProjectSO project, out _))
        {
            float projectWork = applicationAdapter.IsInstantWorkEnabled
                ? project.RequiredWork
                : approvedWorkUnits
                    ? BlueprintResearchService.CalculateApprovedResearchWork(
                        researcher,
                        amount)
                    : BlueprintResearchService.CalculateResearchWork(
                        researcher,
                        researchFacility,
                        amount);
            return ApplyOutcomeWork(researcher, researchFacility, project, null, projectWork, equipment);
        }

        if (!state.TryGetActiveTask(out BlueprintResearchTask task))
        {
            return new BlueprintResearchWorkResult(
                false,
                (FacilityBlueprintSO)null,
                0f,
                0f,
                1f,
                false,
                "실행 가능한 연구가 없습니다");
        }

        float work = applicationAdapter.IsInstantWorkEnabled
            ? task.RequiredWork
            : approvedWorkUnits
                ? BlueprintResearchService.CalculateApprovedResearchWork(
                    researcher,
                    amount)
                : BlueprintResearchService.CalculateResearchWork(
                    researcher,
                    researchFacility,
                    amount);
        return ApplyOutcomeWork(researcher, researchFacility, null, task.Blueprint, work, equipment);
    }

    private BlueprintResearchWorkResult ApplyOutcomeWork(
        CharacterActor researcher, BuildableObject facility,
        ResearchProjectSO project, FacilityBlueprintSO blueprint, float work,
        DurableFacilityEquipmentUseContext equipment)
    {
        if (gameplayOutcomes == null)
            throw new InvalidOperationException("Research requires its gameplay-outcome transaction.");
        string researcherId = string.Empty;
        if (researcher != null)
        {
            if (!CharacterPersistentIdentity.TryGet(researcher, out CharacterId id))
                throw new InvalidOperationException("Researcher requires a persistent identity.");
            researcherId = id.Value;
        }
        if (!gameplayOutcomes.TryApply(
                state, OwnedShopUnlockState, ResolveFacilityShopCatalog(), project, blueprint,
                work, CurrentAbsoluteDay, researcherId, researcher?.Identity.DisplayName,
                facility.RequirePersistentInstanceId().Value,
                FacilityShopService.GetBuildingName(facility.BuildingData),
                out BlueprintResearchWorkResult result,
                out BlueprintResearchUnlockResult unlocks,
                equipment == null ? null : new ResearchEquipmentUseSnapshot(equipment)))
            return result;
        if (result.PublicationPending) return result;

        string projectId = project != null ? project.ProjectId.Value : $"blueprint:{blueprint.id}";
        NotifyCommittedResearch(() => PublishResearchProgress(
            researcher, projectId, work, result.AddedProgress));
        if (result.Completed)
        {
            NotifyCommittedResearch(() => PublishResearchOutcome(
                researcher, projectId, "completed", CharacterCommandOrigin.Autonomous));
            NotifyResearchCompletion(project, blueprint, unlocks, true, true,
                result.CommittedResultKey);
        }
        return result;
    }

    private static void NotifyCommittedResearch(Action notify)
    {
        try { notify(); }
        catch (Exception exception) when (exception is not OutOfMemoryException
            and not StackOverflowException and not AccessViolationException)
        {
            Debug.LogError("research-post-commit-notification-failed:" + exception);
        }
    }

    // Checks the current active project without resolving or advancing the queue.
    public bool CanUseForbiddenResearchLeap(CharacterActor researcher)
    {
        if (projectCoordinator == null || extremeTraits == null || runSeedProvider == null
            || gameClock == null || gameplayOutcomes == null)
            return false;

        ResearchProjectId activeProjectId = state.Projects.ActiveProjectId;
        return activeProjectId.IsValid
            && projectCatalog.TryGet(activeProjectId, out ResearchProjectSO project)
            && project != null
            && extremeTraits.CanUseForbiddenResearchLeap(researcher, project.ProjectId.Value);
    }

    public bool TryForbiddenResearchLeap(
        CharacterActor researcher,
        out ExtremeRiskResolution resolution,
        out string failureReason)
    {
        resolution = default;
        failureReason = string.Empty;
        if (!RetryPendingResearchPublication())
        {
            failureReason = pendingPublicationFailure;
            return false;
        }
        if (researcher == null)
        {
            failureReason = "연구자가 필요합니다.";
            return false;
        }
        if (extremeTraits == null || runSeedProvider == null || gameClock == null)
        {
            failureReason = "금단의 도약 런타임이 구성되지 않았습니다.";
            return false;
        }
        if (!TryResolveActiveProject(out ResearchProjectSO project, out string blocker))
        {
            failureReason = string.IsNullOrWhiteSpace(blocker)
                ? "활성 연구 프로젝트가 없습니다."
                : blocker;
            return false;
        }
        if (gameplayOutcomes == null)
            throw new InvalidOperationException("Research outcome transaction is not configured.");
        if (!gameplayOutcomes.TryApplyForbiddenLeap(
                state, OwnedShopUnlockState, ResolveFacilityShopCatalog(), project,
                extremeTraits, researcher, unchecked((ulong)(uint)runSeedProvider.RunSeed),
                gameClock.Time, CurrentAbsoluteDay, out resolution,
                out BlueprintResearchWorkResult result, out BlueprintResearchUnlockResult unlocks))
        {
            failureReason = result.Message;
            return false;
        }

        if (result.PublicationPending) return true;
        float requestedDelta = resolution.ProgressDelta * project.RequiredWork;
        string riskOutcome = resolution.Outcome.ToString().ToLowerInvariant();
        NotifyCommittedResearch(() => PublishResearchProgress(
            researcher,
            project.ProjectId.Value,
            Mathf.Abs(requestedDelta),
            result.AddedProgress));
        NotifyCommittedResearch(() => PublishResearchOutcome(
            researcher,
            project.ProjectId.Value,
            riskOutcome,
            CharacterCommandOrigin.DirectPlayerOrder));
        if (result.Completed)
            NotifyResearchCompletion(project, null, unlocks, true, true,
                result.CommittedResultKey);
        return true;
    }

    private void PublishResearchProgress(
        CharacterActor researcher,
        string projectId,
        float approvedWork,
        float progressDelta)
    {
        if (identityEvents == null
            || researcher == null
            || !CharacterPersistentIdentity.TryGet(researcher, out CharacterId id))
            return;
        identityEvents.Publish(new ResearchProgressEvent(
            id,
            projectId,
            approvedWork,
            progressDelta,
            CurrentAbsoluteDay));
    }

    private void PublishResearchOutcome(
        CharacterActor researcher,
        string projectId,
        string outcomeId,
        CharacterCommandOrigin origin)
    {
        if (identityEvents == null
            || researcher == null
            || !CharacterPersistentIdentity.TryGet(researcher, out CharacterId id))
            return;
        identityEvents.Publish(new ResearchOutcomeEvent(
            id,
            projectId,
            outcomeId,
            origin,
            CurrentAbsoluteDay));
    }

    private int CurrentAbsoluteDay => calendar.Day;

    public ResearchQueueCommandResult EnqueueProject(ResearchProjectId projectId)
    {
        ResearchQueueCommandResult result = projectCoordinator.Enqueue(
            state,
            projectId);
        if (result.Succeeded)
        {
            NotifyResearchAvailabilityChanged(prioritizeResearch: true);
        }
        return result;
    }

    public ResearchQueueCommandResult RemoveProject(ResearchProjectId projectId)
    {
        ResearchQueueCommandResult result = projectCoordinator.Remove(
            state,
            projectId);
        if (result.Succeeded)
        {
            NotifyResearchAvailabilityChanged();
        }
        return result;
    }

    public ResearchQueueCommandResult MoveProject(int fromIndex, int toIndex)
    {
        return projectCoordinator.Move(state, fromIndex, toIndex);
    }
    public ResearchNodeState GetNodeState(
        ResearchProjectSO project,
        out string blocker)
    {
        if (projectCoordinator != null)
        {
            return projectCoordinator.EvaluateNodeState(
                state,
                project,
                out blocker);
        }

        blocker = string.Empty;
        if (project == null)
        {
            blocker = "연구 정의가 없습니다.";
            return ResearchNodeState.Locked;
        }
        if (state.Projects.IsCompleted(project.ProjectId))
        {
            return ResearchNodeState.Completed;
        }
        if (state.Projects.ActiveProjectId.Equals(project.ProjectId))
        {
            return ResearchNodeState.Active;
        }
        ResearchQueueEntry queued = state.Projects.Queue
            .FirstOrDefault(entry => entry.ProjectId.Equals(project.ProjectId));
        if (queued != null)
        {
            blocker = queued.SuspendedReason;
            return queued.IsSuspended ? ResearchNodeState.Suspended : ResearchNodeState.Queued;
        }

        bool archived = projectCoordinator.HasArchivedBlueprint(
            project,
            out string blueprintBlocker);
        bool prerequisitesComplete =
            projectCoordinator.ArePrerequisitesCompleted(state, project);
        if (project.BlueprintRule == ResearchBlueprintRule.Shortcut && archived)
        {
            if (!researchFacilityCapacity.MeetsRequirements(
                    project,
                    out string shortcutFacilityBlocker))
            {
                blocker = shortcutFacilityBlocker;
                return ResearchNodeState.Locked;
            }
            return ResearchNodeState.ShortcutAvailable;
        }
        if (!prerequisitesComplete)
        {
            string[] missingNames = project.Prerequisites
                .Where(required => !state.Projects.IsCompleted(required.ProjectId))
                .Select(required => required.DisplayName)
                .ToArray();
            blocker = $"선행 연구 필요: {string.Join(", ", missingNames)}";
            return ResearchNodeState.Locked;
        }
        if (project.BlueprintRule == ResearchBlueprintRule.Required && !archived)
        {
            blocker = blueprintBlocker;
            ResearchBlueprintArchiveStatus status =
                blueprintArchiveQuery.GetStatus(project.Blueprint);
            return status.IsInTransit
                ? ResearchNodeState.BlueprintInTransit
                : ResearchNodeState.Locked;
        }
        if (!researchFacilityCapacity.MeetsRequirements(
                project,
                out string facilityBlocker))
        {
            blocker = facilityBlocker;
            return ResearchNodeState.Locked;
        }
        return ResearchNodeState.Available;
    }

    public bool TryGetActiveProject(
        out ResearchProjectSO project,
        out string blocker)
    {
        return TryResolveActiveProject(out project, out blocker);
    }

    public void RefreshProjectQueueAfterRestore()
    {
        TryResolveActiveProject(out _, out _);
        NotifyResearchAvailabilityChanged();
    }

    internal void ReplaceStateFromRestore(BlueprintResearchState restored)
    {
        state.ReplaceFrom(restored);
    }

#if UNITY_EDITOR
    public void ReplaceWithEmptyStateForDebug()
    {
        state.ReplaceFrom(new BlueprintResearchState());
    }
#endif

    public bool TryCancelBlueprint(FacilityBlueprintSO blueprint, out string message)
    {
        if (blueprint == null)
        {
            message = "설계도 정보가 없습니다";
            return false;
        }

        bool cancelled = state.TryCancelBlueprint(blueprint);
        if (cancelled)
        {
            NotifyResearchAvailabilityChanged();
        }

        message = cancelled
            ? $"{blueprint.DisplayName} 연구를 취소했습니다"
            : "취소할 수 있는 진행 중 연구가 없습니다";
        return cancelled;
    }

    public int CompleteAllBlueprintsImmediately()
    {
        if (projectCatalog != null && projectCatalog.Projects.Count > 0)
        {
            int projectCount = 0;
            foreach (ResearchProjectSO project in projectCatalog.Projects)
            {
                if (state.Projects.IsCompleted(project.ProjectId))
                {
                    continue;
                }
                if (!TryCompleteImmediately(project, null, out string failure))
                    throw new InvalidOperationException(failure);
                projectCount++;
            }
            if (projectCount > 0)
            {
                NotifyCommittedResearch(() => NotifyResearchAvailabilityChanged());
            }
            return projectCount;
        }

        int completedCount = 0;
        foreach (FacilityBlueprintSO blueprint in ResolveFacilityShopCatalog().Blueprints
                     .Where(candidate => candidate != null)
                     .OrderBy(candidate => candidate.id))
        {
            if (state.IsCompleted(blueprint))
            {
                continue;
            }

            if (!TryCompleteImmediately(null, blueprint, out string failure))
                throw new InvalidOperationException(failure);
            completedCount++;
        }

        if (completedCount > 0)
        {
            NotifyCommittedResearch(() => NotifyResearchAvailabilityChanged());
        }

        return completedCount;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Completes one authored project through the same unlock publication path
    /// as normal research. Verification fixtures must not call the low-level
    /// restore mutator because that can leave completed-project and unlock state
    /// inconsistent before a save checkpoint is captured.
    /// </summary>
    public bool TryCompleteProjectImmediatelyForVerification(
        ResearchProjectId projectId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!projectId.IsValid)
        {
            failureReason = "research-project-id-invalid";
            return false;
        }
        if (projectCatalog == null
            || !projectCatalog.TryGet(projectId, out ResearchProjectSO project)
            || project == null)
        {
            failureReason = "research-project-not-authored:" + projectId.Value;
            return false;
        }

        if (!state.Projects.IsCompleted(projectId)
            && !TryCompleteImmediately(project, null, out failureReason))
            return false;
        bool buildingUnlocksExact = project.Unlocks
            .OfType<BlueprintBuildingUnlock>()
            .All(unlock => state.IsBuildingUnlocked(unlock.buildingId));
        bool recipeUnlocksExact = project.Unlocks
            .OfType<BlueprintRecipeUnlock>()
            .All(unlock => state.UnlockedRecipeIds.Contains(unlock.recipeId));
        if (!state.Projects.IsCompleted(projectId)
            || !buildingUnlocksExact
            || !recipeUnlocksExact)
        {
            failureReason = "research-project-completion-not-exact:"
                + projectId.Value;
            return false;
        }
        return true;
    }
#endif

    public bool TryAcceptPurchase(FacilityShopPurchaseFact fact, string publicationKey, out string failure)
    {
        failure = string.Empty;
        if (fact.IsProposal || string.IsNullOrWhiteSpace(publicationKey) || applicationAdapter == null)
        { failure = "facility-shop-delivery-not-ready"; return false; }
        FacilityShopPurchaseFactData purchase = fact.Snapshot();
        purchase.Validate();
        bool building = purchase.offerTypeId == FacilityShopOfferTypeIds.Building;
        if (!building && purchase.offerTypeId != FacilityShopOfferTypeIds.Blueprint)
        { failure = "facility-shop-delivery-offer-type-unsupported"; return false; }
        // This acknowledged command owns delivery enqueue. UI notifications are not a command transport.
        if (state.TryGetShopFulfillment(publicationKey, out var existing)
            && (existing.OfferTypeId != purchase.offerTypeId || existing.OfferDataId != purchase.offerDataId
                || existing.ItemDefinitionId != purchase.itemDefinitionId || existing.ItemDisplayName != purchase.offerName))
        { failure = "facility-shop-delivery-purchase-conflict"; return false; }
        state.EnqueueShopFulfillment(new FacilityShopFulfillmentRecord
        {
            PurchaseResultKey = publicationKey, OfferTypeId = purchase.offerTypeId,
            OfferDataId = purchase.offerDataId, ItemDefinitionId = purchase.itemDefinitionId,
            ItemDisplayName = purchase.offerName, Phase = FacilityShopFulfillmentPhase.Pending
        });
        if (building && state.UnlockBuilding(purchase.offerDataId))
            NotifyResearchAvailabilityChanged();
        RetryPendingShopFulfillments();
        if (!state.TryGetShopFulfillment(publicationKey, out var delivery))
        { failure = "facility-shop-delivery-enqueue-missing"; return false; }
        if (delivery.Phase != FacilityShopFulfillmentPhase.Delivered)
            applicationAdapter.RaiseHighAlert("상점 물품 배송 지연",
                purchase.offerName + " 배송을 재시도합니다.", "상점");
        return true;
    }

    public bool TryGetShopFulfillment(
        string purchaseResultKey,
        out FacilityShopFulfillmentRecord record) =>
        state.TryGetShopFulfillment(purchaseResultKey, out record);

    public int RetryPendingShopFulfillments()
    {
        if (itemStackRuntime == null || worldDropZoneQuery == null)
            return 0;
        int completed = 0;
        foreach (FacilityShopFulfillmentRecord delivery in state
                     .CaptureShopFulfillments()
                     .Where(value => value.Phase
                         != FacilityShopFulfillmentPhase.Delivered))
        {
            if (delivery.Phase == FacilityShopFulfillmentPhase.Pending)
            {
                if (!worldDropZoneQuery.TryGetDeliveryDropoff(
                        out Vector2Int dropoff)
                    || !itemStackRuntime.SpawnUniqueItemAt(
                        delivery.ItemDefinitionId,
                        dropoff,
                        WorldItemStackState.Loose,
                        string.Empty,
                        out string stackId)
                    || string.IsNullOrWhiteSpace(stackId))
                    continue;
                state.MarkShopFulfillmentSpawned(
                    delivery.PurchaseResultKey, stackId);
                delivery.StackId = stackId;
            }
            WorldItemStackSnapshot stack = itemStackRuntime.GetAllStacks()
                .FirstOrDefault(value => value != null
                    && string.Equals(value.StackId, delivery.StackId,
                        StringComparison.Ordinal));
            if (stack == null
                || stack.ItemId != delivery.ItemDefinitionId
                || string.IsNullOrWhiteSpace(stack.ItemInstanceId))
                continue;
            state.MarkShopFulfillmentDelivered(
                delivery.PurchaseResultKey, stack.ItemInstanceId);
            completed++;
            applicationAdapter.RaiseLowAlert(
                "상점 물품 도착",
                delivery.ItemDisplayName + " 하차장 배송 완료.",
                "상점");
        }
        return completed;
    }

    private void Update()
    {
        // Scene MonoBehaviours can receive an Update between PlayMode state
        // transition and VContainer injection (and during domain-reload teardown).
        // No research authority exists until the application adapter is present.
        if (applicationAdapter == null || projectCoordinator == null)
        {
            return;
        }

        EnsureRestoreProjectionCurrent();
        float unscaledTime = applicationAdapter.UnscaledTime;
        if (unscaledTime < nextArchiveDeliveryRefresh)
        {
            return;
        }

        nextArchiveDeliveryRefresh = unscaledTime + 1f;
        RetryPendingResearchPublication();
        RetryPendingShopFulfillments();
        RequestBlueprintArchiveDeliveries();
        TryResolveActiveProject(out _, out _);
    }

    private void EnsureRestoreProjectionCurrent()
    {
        if (applicationAdapter == null)
        {
            return;
        }

        int publishedRevision = applicationAdapter.PublishedRestoreRevision;
        if (projectedRestoreRevision == publishedRevision)
        {
            return;
        }

        projectedRestoreRevision = publishedRevision;
        TryResolveActiveProject(out _, out _);
        NotifyResearchAvailabilityChanged();
    }

    private void RequestBlueprintArchiveDeliveries()
    {
        if (itemStackRuntime == null)
        {
            return;
        }

        ReconcileArchiveDestinationClaims();

        bool dispatchRequired = false;
        foreach (ResearchProjectSO project in projectCatalog.Projects
                     .Where(candidate => candidate?.Blueprint != null))
        {
            ResearchBlueprintArchiveStatus status =
                blueprintArchiveQuery.GetStatus(project.Blueprint);
            if (status.IsArchived
                || !blueprintArchiveQuery.TryGetPreferredArchive(
                    project.Blueprint,
                    out BuildableObject archive,
                    out string destinationId))
            {
                continue;
            }

            WorldItemStackSnapshot[] assignedStacks = itemStackRuntime
                .GetAllStacks()
                .Where(stack => stack != null
                    && stack.Quantity > 0
                    && string.Equals(
                        stack.ItemId,
                        project.Blueprint.PhysicalItemId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.DestinationId,
                        destinationId,
                        StringComparison.Ordinal))
                .ToArray();
            bool alreadyAssigned = assignedStacks.Length > 0;
            if (!alreadyAssigned)
            {
                itemStackRuntime.TryRequestItemDelivery(
                    project.Blueprint.PhysicalItemId,
                    1,
                    archive.centerPos,
                    destinationId,
                    out int requestedQuantity,
                    out _);
                dispatchRequired |= requestedQuantity > 0;
                continue;
            }

            bool owned = assignedStacks.Any(stack => stack.HasReservations)
                || itemStackRuntime.GetCommittedHaulDeliveryQuantity(
                    destinationId,
                    project.Blueprint.PhysicalItemId) > 0;
            dispatchRequired |= !owned;
        }

        if (dispatchRequired)
        {
            // Delivery creation already marks the exact destination stacks as
            // priority. A non-forced wake can legitimately find every worker
            // busy, so retry only while the delivery has no reservation or
            // pickup commitment. Ownership stops the periodic dispatch.
            workforceReplanService?.RequestOneHaulerToReplan(
                clearFailures: true,
                forceInterrupt: false);
        }
    }

    private void ReconcileArchiveDestinationClaims()
    {
        if (archiveDestinationClaimQuery == null
            || archiveCapacityQuery == null
            || archiveDestinations == null
            || archiveReleases == null
            || physicalMass == null)
        {
            throw new InvalidOperationException(
                $"{nameof(BlueprintResearchRuntime)} requires exact research archive destination authority injection.");
        }

        FacilityBufferDestinationClaim[] desiredClaims =
            ResearchBlueprintArchiveDestinationAuthority.BuildClaims(
                blueprintArchiveQuery.GetValidArchives());
        FacilityBufferCapacityProfile[] desiredProfiles =
            ResearchBlueprintArchiveDestinationAuthority.BuildProfiles(
                blueprintArchiveQuery.GetValidArchives(),
                facilityShopCatalog.Blueprints,
                physicalMass);
        FacilityBufferDestinationClaim[] existingClaims =
            archiveDestinationClaimQuery.CaptureClaims()
            .Where(claim => claim != null
                && string.Equals(
                    claim.OwnerDomain,
                    ResearchBlueprintArchiveDestinationAuthority.OwnerDomain,
                    StringComparison.Ordinal))
            .OrderBy(claim => claim.DestinationId, StringComparer.Ordinal)
            .ToArray();
        FacilityBufferCapacityProfile[] existingProfiles =
            archiveCapacityQuery.CaptureProfiles()
            .Where(profile => profile != null
                && string.Equals(
                    profile.OwnerDomain,
                    ResearchBlueprintArchiveDestinationAuthority.OwnerDomain,
                    StringComparison.Ordinal))
            .OrderBy(profile => profile.DestinationId, StringComparer.Ordinal)
            .ToArray();
        bool unchanged = existingClaims.Length == desiredClaims.Length
            && existingProfiles.Length == desiredProfiles.Length
            && existingClaims.Zip(
                    desiredClaims,
                    ResearchBlueprintArchiveDestinationAuthority.ClaimsMatch)
                .All(matches => matches)
            && existingProfiles.Zip(
                    desiredProfiles,
                    ResearchBlueprintArchiveDestinationAuthority.ProfilesMatch)
                .All(matches => matches);
        if (unchanged)
        {
            return;
        }

        Dictionary<string, FacilityBufferDestinationClaim> desiredById =
            desiredClaims.ToDictionary(
                claim => claim.DestinationId,
                StringComparer.Ordinal);
        foreach (FacilityBufferDestinationClaim previous in existingClaims)
        {
            if (!desiredById.TryGetValue(
                    previous.DestinationId,
                    out FacilityBufferDestinationClaim current)
                || !ResearchBlueprintArchiveDestinationAuthority.ClaimsMatch(
                    previous,
                    current))
            {
                if (!archiveReleases.TryReleaseAtOwnerPosition(
                    previous.DestinationId,
                    previous.DropPosition,
                    "research-blueprint-archive-authority-retired",
                    out _,
                    out string releaseFailure))
                {
                    throw new InvalidOperationException(
                        "Research archive physical release failed before authority retirement: "
                        + releaseFailure);
                }
            }
        }

        if (!archiveDestinations.TryReplaceOwnedAuthorities(
                ResearchBlueprintArchiveDestinationAuthority.OwnerDomain,
                desiredClaims,
                desiredProfiles,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Research archive destination reconciliation failed: "
                + failureReason);
        }
    }

    private bool TryResolveActiveProject(
        out ResearchProjectSO project,
        out string blocker)
    {
        return projectCoordinator.TryResolveActive(
            state,
            out project,
            out blocker);
    }

    private bool TryCompleteImmediately(
        ResearchProjectSO project, FacilityBlueprintSO blueprint, out string failureReason)
    {
        if (!RetryPendingResearchPublication())
        { failureReason = pendingPublicationFailure; return false; }
        if (gameplayOutcomes == null)
            throw new InvalidOperationException("Research outcome transaction is not configured.");
        bool success = gameplayOutcomes.TryCompleteImmediately(
            state, OwnedShopUnlockState, ResolveFacilityShopCatalog(), project, blueprint,
            CurrentAbsoluteDay, out BlueprintResearchWorkResult result, out BlueprintResearchUnlockResult unlocks);
        failureReason = success ? string.Empty : result.Message;
        if (success && !result.PublicationPending) NotifyResearchCompletion(project, blueprint, unlocks,
            false, false, result.CommittedResultKey);
        return success;
    }

    private bool RetryPendingResearchPublication()
    {
        if (!state.HasPendingPublication) return true;
        if (gameplayOutcomes == null)
            throw new InvalidOperationException("Research fact publication is not configured.");
        var pending = new ResearchWorkAppliedFact(state.PendingPublication.fact);
        ResearchProjectSO project = null;
        FacilityBlueprintSO blueprint = null;
        BlueprintResearchUnlockResult recoveredUnlocks = default;
        // Resolve notification subjects before consuming the pending acknowledgement.
        if (pending.Cause != ResearchWorkCause.KnowledgeProgress && pending.Completed)
        {
            if (pending.IsProject)
            {
                if (!projectCatalog.TryGet(new ResearchProjectId(pending.SubjectId), out project) || project == null)
                    return PendingResearchFailure("research-recovery-project-missing:" + pending.SubjectId);
                blueprint = project.Blueprint;
            }
            else
            {
                blueprint = ResolveFacilityShopCatalog().Blueprints.SingleOrDefault(value => value != null
                    && value.id == pending.BlueprintId);
                if (blueprint == null) return PendingResearchFailure("research-recovery-blueprint-missing:" + pending.BlueprintId);
            }
            try
            {
                recoveredUnlocks = RestoreCompletionPresentation(blueprint, pending.Unlocks,
                    id => FacilityShopService.FindBuildingById(ResolveFacilityShopCatalog(), id));
            }
            catch (InvalidOperationException exception) { return PendingResearchFailure(exception.Message); }
        }
        if (!gameplayOutcomes.TryRecoverPending(state, out var recovered, out string key, out string failure))
            return PendingResearchFailure(failure);
        pendingPublicationFailure = string.Empty;
        if (recovered == null || recovered.Cause == ResearchWorkCause.KnowledgeProgress) return true;
        if (!string.IsNullOrEmpty(recovered.ResearcherId) && recovered.Cause != ResearchWorkCause.ImmediateCompletion)
        {
            var actor = new CharacterId(recovered.ResearcherId);
            NotifyCommittedResearch(() => identityEvents.Publish(new ResearchProgressEvent(actor,
                recovered.SubjectId, recovered.ApprovedWork, recovered.After - recovered.Before, recovered.AbsoluteDay)));
            if (recovered.Cause == ResearchWorkCause.ForbiddenLeap || recovered.Completed)
                NotifyCommittedResearch(() => identityEvents.Publish(new ResearchOutcomeEvent(actor, recovered.SubjectId,
                    recovered.Cause == ResearchWorkCause.ForbiddenLeap ? recovered.Leap.Outcome.ToString().ToLowerInvariant() : "completed",
                    recovered.Cause == ResearchWorkCause.ForbiddenLeap ? CharacterCommandOrigin.DirectPlayerOrder : CharacterCommandOrigin.Autonomous,
                    recovered.AbsoluteDay)));
        }
        if (recovered.Completed)
            NotifyResearchCompletion(project, blueprint, recoveredUnlocks,
                recovered.Cause != ResearchWorkCause.ImmediateCompletion, recovered.Cause != ResearchWorkCause.ImmediateCompletion, key);
        return true;
    }

    // Resolve presentation definitions before acknowledging the saved fact. This
    // does not reapply unlocks or change the frozen evidence seen by observers.
    internal static BlueprintResearchUnlockResult RestoreCompletionPresentation(FacilityBlueprintSO blueprint,
        IReadOnlyList<BlueprintUnlockRecord> frozen, Func<int, BuildingSO> findBuilding)
    {
        var records = new List<BlueprintUnlockRecord>(frozen.Count);
        foreach (var record in frozen)
        {
            BuildingSO facility = null;
            if (record.UnlockTypeId == BlueprintUnlockTypeIds.Building
                || record.UnlockTypeId == BlueprintUnlockTypeIds.BasicPurchase)
            {
                if (!int.TryParse(record.ValueId, System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out int id)
                    || (facility = findBuilding(id)) == null)
                    throw new InvalidOperationException("research-recovery-unlock-definition-missing:" + record.ValueId);
            }
            records.Add(new BlueprintUnlockRecord(record.UnlockTypeId, record.CategoryLabel,
                record.ValueId, record.DisplayName, facility, record.CodexDetail));
        }
        return new BlueprintResearchUnlockResult(blueprint, records);
    }

    private bool PendingResearchFailure(string failure)
    {
        if (pendingPublicationFailure != failure)
            Debug.LogError("research-publication-pending:" + failure);
        pendingPublicationFailure = failure;
        return false;
    }

    private void NotifyResearchCompletion(
        ResearchProjectSO project, FacilityBlueprintSO blueprint, BlueprintResearchUnlockResult unlocks,
        bool notifyAvailability, bool emitAlert,
        string committedResearchResultKey)
    {
        NotifyCommittedResearch(() => applicationAdapter.Publish(project != null
            ? new BlueprintResearchCompletedEvent(project, unlocks,
                committedResearchResultKey)
            : new BlueprintResearchCompletedEvent(blueprint, unlocks,
                committedResearchResultKey)));
        if (notifyAvailability)
            NotifyCommittedResearch(() => NotifyResearchAvailabilityChanged());
        if (emitAlert && raiseAlertOnResearchComplete)
            NotifyCommittedResearch(() => applicationAdapter.RaiseMediumAlert(
                "연구 완료", (project != null ? project.DisplayName : blueprint.DisplayName)
                    + " 연구 완료\n" + string.Join("\n", unlocks.FormatSummaryLines()), "연구"));
    }

    private void NotifyResearchAvailabilityChanged(bool prioritizeResearch = false)
    {
        facilityCandidateCache?.MarkDynamicStateDirty();
        if (prioritizeResearch && HasActiveResearch)
        {
            workforceReplanService?.RequestOneWorkerToReplanFor(BuiltInWorkTypeIds.Research);
            return;
        }

        workforceReplanService?.RequestIdleWorkersToReplan();
    }

    private IFacilityShopUnlockStateAuthority ResolveShopUnlockStateService()
    {
        return shopUnlockStateService
            ?? throw new InvalidOperationException($"{nameof(BlueprintResearchRuntime)} requires VContainer injection of {nameof(IFacilityShopUnlockStateAuthority)}.");
    }

    internal void UnlockAllForDebug()
    {
        FacilityShopUnlockState shopState = OwnedShopUnlockState;
        IFacilityShopCatalog catalog = ResolveFacilityShopCatalog();
        foreach (BuildingSO building in catalog.Buildings.Where(value => value != null))
        {
            state.UnlockBuilding(building.id);
            shopState.UnlockBasicPurchaseById(building.id);
        }
        foreach (FacilityBlueprintSO blueprint in catalog.Blueprints.Where(value => value != null))
        {
            state.MarkCompleted(blueprint);
            shopState.MarkBlueprintAcquired(blueprint);
        }
    }

    private IFacilityShopCatalog ResolveFacilityShopCatalog()
    {
        return facilityShopCatalog
            ?? throw new InvalidOperationException($"{nameof(BlueprintResearchRuntime)} requires VContainer injection of {nameof(IFacilityShopCatalog)}.");
    }

}
