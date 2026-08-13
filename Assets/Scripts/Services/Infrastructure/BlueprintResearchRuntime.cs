using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

// Unity lifecycle/application adapter over DungeonStory.Research state and rules.
public class BlueprintResearchRuntime : MonoBehaviour
{
    [SerializeField] private bool raiseAlertOnResearchComplete = true;

    private BlueprintResearchState state = new BlueprintResearchState();
    private IFacilityShopUnlockStateService shopUnlockStateService;
    private IFacilityShopCatalog facilityShopCatalog;
    private IFacilityCandidateCache facilityCandidateCache;
    private IWorkforceReplanService workforceReplanService;
    private IWorldItemStackRuntime itemStackRuntime;
    private BlueprintResearchProjectCoordinator projectCoordinator;
    private IWorldDropZoneQuery worldDropZoneQuery;
    private BlueprintResearchApplicationAdapter applicationAdapter;
    private float nextArchiveDeliveryRefresh;
    private int projectedRestoreRevision;
    private readonly HashSet<string> pendingKnowledgeDeliveries =
        new HashSet<string>(StringComparer.Ordinal);
    private IDisposable shopPurchasedSubscription;
    private ExtremeTraitRuntime extremeTraits;
    private IRunSeedProvider runSeedProvider;
    private IGameClock gameClock;
    private CharacterIdentityEventPublisher identityEvents;

    public BlueprintResearchState State => state;
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
    public FacilityShopUnlockState ShopUnlockState => ResolveShopUnlockStateService().GetUnlockState();

    [Inject]
    public void ConstructRuntime(
        IFacilityShopUnlockStateService shopUnlockStateService,
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
        SubscribeToScopedEvents();
    }

    [Inject]
    public void ConstructFounderTraitRuntime(
        ExtremeTraitRuntime extremeTraits,
        IRunSeedProvider runSeedProvider,
        IGameClock gameClock,
        CharacterIdentityEventPublisher identityEvents)
    {
        this.extremeTraits = extremeTraits
            ?? throw new ArgumentNullException(nameof(extremeTraits));
        this.runSeedProvider = runSeedProvider
            ?? throw new ArgumentNullException(nameof(runSeedProvider));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.identityEvents = identityEvents
            ?? throw new ArgumentNullException(nameof(identityEvents));
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

    public BlueprintResearchWorkResult ApplyResearchWork(CharacterActor researcher, BuildableObject researchFacility, float seconds)
    {
        if (researchFacility == null || !researchFacility.SupportsWork(BuiltInWorkTypeIds.Research))
        {
            return new BlueprintResearchWorkResult(false, null, 0f, 0f, 1f, false, "연구 가능한 시설이 아닙니다");
        }

        if (TryResolveActiveProject(out ResearchProjectSO project, out _))
        {
            ResearchProjectProgressState projectProgress =
                state.Projects.GetProgress(project.ProjectId);
            float projectWork = applicationAdapter.IsInstantWorkEnabled
                ? project.RequiredWork
                : BlueprintResearchService.CalculateResearchWork(researcher, researchFacility, seconds);
            projectWork += TryConsumeKnowledgeResidue(researchFacility);
            float projectAdded = projectProgress.Add(projectWork, project);
            PublishResearchProgress(
                researcher,
                project.ProjectId.Value,
                projectWork,
                projectAdded);
            bool projectCompleted = projectProgress.Progress >= project.RequiredWork;
            BlueprintResearchWorkResult projectResult = BlueprintResearchWorkResult.ForProject(
                true,
                project,
                projectAdded,
                projectProgress.Progress,
                project.RequiredWork,
                projectCompleted,
                projectCompleted ? "연구 완료" : "연구 진행");
            if (projectCompleted)
            {
                PublishResearchOutcome(
                    researcher,
                    project.ProjectId.Value,
                    "completed",
                    CharacterCommandOrigin.Autonomous);
                CompleteProject(project);
            }
            return projectResult;
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
            : BlueprintResearchService.CalculateResearchWork(researcher, researchFacility, seconds);
        work += TryConsumeKnowledgeResidue(researchFacility);
        float added = task.AddProgress(work);
        PublishResearchProgress(
            researcher,
            $"blueprint:{task.Blueprint?.id ?? 0}",
            work,
            added);
        bool completed = task.IsCompleted;
        BlueprintResearchWorkResult result = new BlueprintResearchWorkResult(
            true,
            task.Blueprint,
            added,
            task.Progress,
            task.RequiredWork,
            completed,
            completed ? "연구 완료" : "연구 진행");


        if (completed)
        {
            PublishResearchOutcome(
                researcher,
                $"blueprint:{task.Blueprint?.id ?? 0}",
                "completed",
                CharacterCommandOrigin.Autonomous);
            CompleteTask(task.Blueprint);
        }

        return result;
    }

    public bool TryForbiddenResearchLeap(
        CharacterActor researcher,
        out ExtremeRiskResolution resolution,
        out string failureReason)
    {
        resolution = default;
        failureReason = string.Empty;
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
        if (!extremeTraits.TryResolveForbiddenResearchLeap(
                researcher,
                project.ProjectId.Value,
                unchecked((ulong)(uint)runSeedProvider.RunSeed),
                gameClock.Time,
                out resolution))
        {
            failureReason = "이 프로젝트에서는 금단의 도약을 사용할 수 없습니다.";
            return false;
        }

        ResearchProjectProgressState progress = state.Projects.GetProgress(project.ProjectId);
        float before = progress.Progress;
        float requestedDelta = resolution.ProgressDelta * project.RequiredWork;
        if (requestedDelta >= 0f)
            progress.Add(requestedDelta, project);
        else
            progress.Restore(Mathf.Max(0f, progress.Progress + requestedDelta), project);
        PublishResearchProgress(
            researcher,
            project.ProjectId.Value,
            Mathf.Abs(requestedDelta),
            progress.Progress - before);
        PublishResearchOutcome(
            researcher,
            project.ProjectId.Value,
            resolution.Outcome.ToString().ToLowerInvariant(),
            CharacterCommandOrigin.DirectPlayerOrder);
        if (progress.Progress >= project.RequiredWork)
            CompleteProject(project);
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

    private int CurrentAbsoluteDay => gameClock == null
        ? 0
        : Mathf.Max(0, Mathf.FloorToInt(gameClock.Time / GameCalendarRules.SecondsPerDay));

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

    public int EnsureAcquiredBlueprintItemsMaterialized()
    {
        if (itemStackRuntime == null || facilityShopCatalog == null)
        {
            return 0;
        }

        int materialized = 0;
        HashSet<string> existingItemIds = itemStackRuntime.GetAllStacks()
            .Where(stack => stack != null && stack.Quantity > 0)
            .Select(stack => stack.ItemId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (int blueprintId in ShopUnlockState.AcquiredBlueprintIds)
        {
            FacilityBlueprintSO blueprint = facilityShopCatalog.Blueprints
                .FirstOrDefault(candidate => candidate != null && candidate.id == blueprintId);
            if (blueprint == null || existingItemIds.Contains(blueprint.PhysicalItemId))
            {
                continue;
            }

            bool spawned = false;
            if (blueprintArchiveQuery.TryGetPreferredArchive(
                    blueprint,
                    out BuildableObject archive,
                    out string destinationId))
            {
                spawned = itemStackRuntime.SpawnUniqueItemAt(
                    blueprint.PhysicalItemId,
                    archive.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    destinationId,
                    out _);
            }
            else if (worldDropZoneQuery != null
                     && worldDropZoneQuery.TryGetDeliveryDropoff(out Vector2Int dropoff))
            {
                spawned = itemStackRuntime.SpawnUniqueItemAt(
                    blueprint.PhysicalItemId,
                    dropoff,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out _);
            }

            if (spawned)
            {
                existingItemIds.Add(blueprint.PhysicalItemId);
                materialized++;
            }
        }

        return materialized;
    }

    private float TryConsumeKnowledgeResidue(BuildableObject researchFacility)
    {
        if (itemStackRuntime == null || researchFacility == null)
        {
            return 0f;
        }

        string destinationId =
            $"research:{researchFacility.RequirePersistentInstanceId().Value}";
        Dictionary<StockCategory, int> cost = new Dictionary<StockCategory, int>
        {
            [StockCategory.Knowledge] = 1
        };
        if (itemStackRuntime.TryConsumeFacilityBuffer(
                destinationId,
                cost,
                out _))
        {
            pendingKnowledgeDeliveries.Remove(destinationId);
            return 12f;
        }

        bool hasOutstandingDelivery = itemStackRuntime.GetAllStacks().Any(stack =>
            stack != null
            && stack.Quantity > 0
            && stack.StockCategory == StockCategory.Knowledge
            && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal));
        if (hasOutstandingDelivery)
        {
            pendingKnowledgeDeliveries.Add(destinationId);
            return 0f;
        }

        pendingKnowledgeDeliveries.Remove(destinationId);
        if (itemStackRuntime.TryRequestFacilityDelivery(
                StockCategory.Knowledge,
                1,
                researchFacility.centerPos,
                destinationId,
                out int requested,
                out _)
            && requested > 0)
        {
            pendingKnowledgeDeliveries.Add(destinationId);
        }

        return 0f;
    }

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
                CompleteProject(project, notifyAvailability: false, emitAlert: false);
                projectCount++;
            }
            if (projectCount > 0)
            {
                NotifyResearchAvailabilityChanged();
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

            state.TryCancelBlueprint(blueprint);
            CompleteTask(blueprint, notifyAvailability: false, emitAlert: false);
            completedCount++;
        }

        if (completedCount > 0)
        {
            NotifyResearchAvailabilityChanged();
        }

        return completedCount;
    }

    public void OnTriggerEvent(FacilityShopPurchasedEvent eventType)
    {
        if (!eventType.result.success)
        {
            return;
        }

        if (eventType.result.TryGetBuilding(out BuildingSO building))
        {
            if (state.UnlockBuilding(building.id))
            {
                NotifyResearchAvailabilityChanged();
            }

            if (itemStackRuntime == null
                || worldDropZoneQuery == null
                || !worldDropZoneQuery.TryGetDeliveryDropoff(
                    out Vector2Int kitDropoff)
                || !itemStackRuntime.SpawnUniqueItemAt(
                    FacilityInstallationKitItemIds.ForBuilding(building),
                    kitDropoff,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out _))
            {
                applicationAdapter.RaiseHighAlert(
                    "시설 키트 배송 지연",
                    $"{FacilityShopService.GetBuildingName(building)} 설치 키트를 하차장에 놓지 못했습니다.",
                    "상점");
                return;
            }

            applicationAdapter.RaiseLowAlert(
                "시설 키트 도착",
                $"{FacilityShopService.GetBuildingName(building)} 설치 키트가 하차장에 도착했습니다.",
                "상점");
            return;
        }

        if (!eventType.result.TryGetBlueprint(out FacilityBlueprintSO blueprint))
        {
            return;
        }

        if (itemStackRuntime == null
            || worldDropZoneQuery == null
            || !worldDropZoneQuery.TryGetDeliveryDropoff(out Vector2Int dropoff)
            || !itemStackRuntime.SpawnUniqueItemAt(
                blueprint.PhysicalItemId,
                dropoff,
                WorldItemStackState.Loose,
                string.Empty,
                out _))
        {
            applicationAdapter.RaiseHighAlert(
                "설계도 배송 지연",
                $"{blueprint.DisplayName} 설계도를 하차장에 놓지 못했습니다.",
                "연구");
            return;
        }

        applicationAdapter.RaiseLowAlert(
            "설계도 도착",
            $"{blueprint.DisplayName} 설계도가 하차장에 도착했습니다.",
            "연구");
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

            bool alreadyAssigned = itemStackRuntime.GetAllStacks().Any(stack =>
                stack != null
                && stack.Quantity > 0
                && string.Equals(
                    stack.ItemId,
                    project.Blueprint.PhysicalItemId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal));
            if (!alreadyAssigned)
            {
                itemStackRuntime.TryRequestItemDelivery(
                    project.Blueprint.PhysicalItemId,
                    1,
                    archive.centerPos,
                    destinationId,
                    out _,
                    out _);
            }
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

    private void CompleteTask(FacilityBlueprintSO blueprint)
    {
        CompleteTask(
            blueprint,
            notifyAvailability: true,
            emitAlert: raiseAlertOnResearchComplete);
    }

    private void CompleteProject(
        ResearchProjectSO project,
        bool notifyAvailability = true,
        bool emitAlert = true)
    {
        if (project == null)
        {
            return;
        }

        BlueprintResearchUnlockResult unlockResult = BlueprintResearchService.ApplyCompletion(
            project,
            state,
            ShopUnlockState,
            ResolveFacilityShopCatalog());
        applicationAdapter.Publish(
            new BlueprintResearchCompletedEvent(project, unlockResult));
        if (notifyAvailability)
        {
            NotifyResearchAvailabilityChanged();
        }

        if (emitAlert && raiseAlertOnResearchComplete)
        {
            List<string> lines = new List<string> { $"{project.DisplayName} 연구 완료" };
            lines.AddRange(unlockResult.FormatSummaryLines());
            applicationAdapter.RaiseMediumAlert(
                "연구 완료",
                string.Join("\n", lines),
                "연구");
        }
    }

    private void CompleteTask(
        FacilityBlueprintSO blueprint,
        bool notifyAvailability,
        bool emitAlert)
    {
        BlueprintResearchUnlockResult unlockResult = BlueprintResearchService.ApplyCompletion(
            blueprint,
            state,
            ShopUnlockState,
            ResolveFacilityShopCatalog());
        applicationAdapter.Publish(
            new BlueprintResearchCompletedEvent(blueprint, unlockResult));
        if (notifyAvailability)
        {
            NotifyResearchAvailabilityChanged();
        }

        if (emitAlert)
        {
            applicationAdapter.RaiseMediumAlert(
                "연구 완료",
                FormatUnlockResult(unlockResult),
                "연구");
        }
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

    private static string FormatUnlockResult(BlueprintResearchUnlockResult result)
    {
        if (result.Blueprint == null)
        {
            return "연구 완료";
        }

        List<string> lines = new List<string> { $"{result.Blueprint.DisplayName} 분석 완료" };
        lines.AddRange(result.FormatSummaryLines());
        return string.Join("\n", lines);
    }

    private IFacilityShopUnlockStateService ResolveShopUnlockStateService()
    {
        return shopUnlockStateService
            ?? throw new InvalidOperationException($"{nameof(BlueprintResearchRuntime)} requires VContainer injection of {nameof(IFacilityShopUnlockStateService)}.");
    }

    private IFacilityShopCatalog ResolveFacilityShopCatalog()
    {
        return facilityShopCatalog
            ?? throw new InvalidOperationException($"{nameof(BlueprintResearchRuntime)} requires VContainer injection of {nameof(IFacilityShopCatalog)}.");
    }

    private void OnEnable()
    {
        SubscribeToScopedEvents();
    }

    private void OnDisable()
    {
        shopPurchasedSubscription?.Dispose();
        shopPurchasedSubscription = null;
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled
            || shopPurchasedSubscription != null
            || applicationAdapter == null)
        {
            return;
        }

        shopPurchasedSubscription =
            applicationAdapter.SubscribeToShopPurchased(OnTriggerEvent);
    }
}
