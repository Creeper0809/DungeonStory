using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

[Serializable]
public class BlueprintResearchTask
{
    [SerializeField] private FacilityBlueprintSO blueprint;
    [SerializeField] private float progress;

    public BlueprintResearchTask(FacilityBlueprintSO blueprint)
    {
        this.blueprint = blueprint;
        progress = 0f;
    }

    public FacilityBlueprintSO Blueprint => blueprint;
    public float Progress => progress;
    public float RequiredWork => blueprint != null ? Mathf.Max(1f, blueprint.researchWorkRequired) : 1f;
    public float ProgressRatio => Mathf.Clamp01(progress / RequiredWork);
    public bool IsCompleted => blueprint != null && progress >= RequiredWork;

    public float AddProgress(float amount)
    {
        if (blueprint == null || IsCompleted)
        {
            return 0f;
        }

        float before = progress;
        progress = Mathf.Min(RequiredWork, progress + Mathf.Max(0f, amount));
        return progress - before;
    }

    internal void RestoreProgress(float value)
    {
        progress = Mathf.Clamp(value, 0f, RequiredWork);
    }
}

public class BlueprintResearchState : IBuildingUnlockStateView
{
    private readonly ResearchProjectRuntimeState projects = new ResearchProjectRuntimeState();
    private readonly List<BlueprintResearchTask> tasks = new List<BlueprintResearchTask>();
    private readonly HashSet<int> completedBlueprintIds = new HashSet<int>();
    private readonly HashSet<int> unlockedBuildingIds = new HashSet<int>();
    private readonly HashSet<string> unlockedRecipeIds = new HashSet<string>();
    private readonly IReadOnlyList<BlueprintResearchTask> tasksView;
    private readonly IReadOnlyCollection<int> completedBlueprintIdsView;
    private readonly IReadOnlyCollection<int> unlockedBuildingIdsView;
    private readonly IReadOnlyCollection<string> unlockedRecipeIdsView;

    public BlueprintResearchState()
    {
        tasksView = ReadOnlyView.List(tasks);
        completedBlueprintIdsView = ReadOnlyView.Collection(completedBlueprintIds);
        unlockedBuildingIdsView = ReadOnlyView.Collection(unlockedBuildingIds);
        unlockedRecipeIdsView = ReadOnlyView.Collection(unlockedRecipeIds);
    }

    public IReadOnlyList<BlueprintResearchTask> Tasks => tasksView;
    public IReadOnlyCollection<int> CompletedBlueprintIds => completedBlueprintIdsView;
    public IReadOnlyCollection<int> UnlockedBuildingIds => unlockedBuildingIdsView;
    public IReadOnlyCollection<string> UnlockedRecipeIds => unlockedRecipeIdsView;
    public ResearchProjectRuntimeState Projects => projects;

    public bool HasActiveTask => TryGetActiveTask(out _);

    public bool EnqueueBlueprint(FacilityBlueprintSO blueprint)
    {
        if (blueprint == null || completedBlueprintIds.Contains(blueprint.id))
        {
            return false;
        }

        if (tasks.Any((task) => task.Blueprint == blueprint || task.Blueprint?.id == blueprint.id))
        {
            return false;
        }

        tasks.Add(new BlueprintResearchTask(blueprint));
        return true;
    }

    public bool TryGetActiveTask(out BlueprintResearchTask task)
    {
        task = tasks.FirstOrDefault((candidate) => candidate != null && !candidate.IsCompleted);
        return task != null;
    }

    public bool IsCompleted(FacilityBlueprintSO blueprint)
    {
        return blueprint != null && completedBlueprintIds.Contains(blueprint.id);
    }

    public bool TryCancelBlueprint(FacilityBlueprintSO blueprint)
    {
        if (blueprint == null)
        {
            return false;
        }

        BlueprintResearchTask task = tasks.FirstOrDefault((candidate) =>
            candidate != null
            && candidate.Blueprint != null
            && candidate.Blueprint.id == blueprint.id
            && !candidate.IsCompleted);
        return task != null && tasks.Remove(task);
    }

    public void MarkCompleted(FacilityBlueprintSO blueprint)
    {
        if (blueprint == null)
        {
            return;
        }

        completedBlueprintIds.Add(blueprint.id);
    }

    public bool UnlockRecipe(string recipeId)
    {
        return !string.IsNullOrWhiteSpace(recipeId) && unlockedRecipeIds.Add(recipeId);
    }

    public bool UnlockBuilding(int buildingId)
    {
        return buildingId >= 0 && unlockedBuildingIds.Add(buildingId);
    }

    public bool IsBuildingUnlocked(int buildingId)
    {
        return buildingId >= 0 && unlockedBuildingIds.Contains(buildingId);
    }

    public void ClearForRestore()
    {
        tasks.Clear();
        completedBlueprintIds.Clear();
        unlockedBuildingIds.Clear();
        unlockedRecipeIds.Clear();
        projects.ClearForRestore();
    }

    public bool RestoreTask(FacilityBlueprintSO blueprint, float progress)
    {
        if (!EnqueueBlueprint(blueprint))
        {
            return false;
        }

        BlueprintResearchTask task = tasks[tasks.Count - 1];
        task.RestoreProgress(progress);
        return true;
    }

    public void RestoreCompletedBlueprintId(int blueprintId)
    {
        if (blueprintId >= 0)
        {
            completedBlueprintIds.Add(blueprintId);
        }
    }

    public void RestoreUnlockedBuildingId(int buildingId)
    {
        UnlockBuilding(buildingId);
    }
}

public readonly struct BlueprintResearchWorkResult
{
    public BlueprintResearchWorkResult(
        bool success,
        FacilityBlueprintSO blueprint,
        float addedProgress,
        float totalProgress,
        float requiredWork,
        bool completed,
        string message)
    {
        Success = success;
        Blueprint = blueprint;
        Project = null;
        AddedProgress = Mathf.Max(0f, addedProgress);
        TotalProgress = Mathf.Max(0f, totalProgress);
        RequiredWork = Mathf.Max(1f, requiredWork);
        Completed = completed;
        Message = message ?? string.Empty;
    }

    public static BlueprintResearchWorkResult ForProject(
        bool success,
        ResearchProjectSO project,
        float addedProgress,
        float totalProgress,
        float requiredWork,
        bool completed,
        string message)
    {
        return new BlueprintResearchWorkResult(
            success,
            project,
            addedProgress,
            totalProgress,
            requiredWork,
            completed,
            message,
            projectResult: true);
    }

    private BlueprintResearchWorkResult(
        bool success,
        ResearchProjectSO project,
        float addedProgress,
        float totalProgress,
        float requiredWork,
        bool completed,
        string message,
        bool projectResult)
    {
        Success = success;
        Blueprint = project?.Blueprint;
        Project = project;
        AddedProgress = Mathf.Max(0f, addedProgress);
        TotalProgress = Mathf.Max(0f, totalProgress);
        RequiredWork = Mathf.Max(1f, requiredWork);
        Completed = completed;
        Message = message ?? string.Empty;
    }

    public bool Success { get; }
    public FacilityBlueprintSO Blueprint { get; }
    public ResearchProjectSO Project { get; }
    public float AddedProgress { get; }
    public float TotalProgress { get; }
    public float RequiredWork { get; }
    public float ProgressRatio => Mathf.Clamp01(TotalProgress / RequiredWork);
    public bool Completed { get; }
    public string Message { get; }
}

public struct BlueprintResearchCompletedEvent
{
    public FacilityBlueprintSO blueprint;
    public ResearchProjectSO project;
    public BlueprintResearchUnlockResult unlockResult;

    public BlueprintResearchCompletedEvent(FacilityBlueprintSO blueprint, BlueprintResearchUnlockResult unlockResult)
    {
        this.blueprint = blueprint;
        project = null;
        this.unlockResult = unlockResult;
    }

    public BlueprintResearchCompletedEvent(
        ResearchProjectSO project,
        BlueprintResearchUnlockResult unlockResult)
    {
        this.project = project;
        blueprint = project?.Blueprint;
        this.unlockResult = unlockResult;
    }
}

public static class BlueprintResearchService
{
    private const float BaseResearchWorkPerSecond = 4f;

    public static float CalculateResearchWork(CharacterActor researcher, BuildableObject researchFacility, float seconds)
    {
        float characterMultiplier = researcher != null
            ? Mathf.Max(0.05f, researcher.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Research))
            : 1f;
        float facilityMultiplier = GetFacilityResearchMultiplier(researchFacility);
        float baseWork = Mathf.Max(0f, seconds) * BaseResearchWorkPerSecond * characterMultiplier * facilityMultiplier;
        return baseWork + CharacterSkillRuntimeEffects.GetResearchWorkBonus(researcher, seconds);
    }

    public static float GetFacilityResearchMultiplier(BuildableObject researchFacility)
    {
        if (researchFacility == null || researchFacility.Facility == null)
        {
            return 1f;
        }

        float multiplier = 1f;
        if (researchFacility.Facility.SupportsRole(FacilityRole.Research))
        {
            multiplier += 0.15f;
        }

        if (researchFacility.Facility.SupportsRole(FacilityRole.Mana))
        {
            multiplier += 0.05f;
        }

        if (researchFacility.Facility.requiredWorkers > 0)
        {
            multiplier += Mathf.Min(0.1f, researchFacility.Facility.requiredWorkers * 0.05f);
        }

        return multiplier;
    }

    public static BlueprintResearchUnlockResult ApplyCompletion(
        FacilityBlueprintSO blueprint,
        BlueprintResearchState state,
        FacilityShopUnlockState shopUnlockState,
        IFacilityShopCatalog facilityShopCatalog)
    {
        if (blueprint == null)
        {
            return new BlueprintResearchUnlockResult(null, Array.Empty<BlueprintUnlockRecord>());
        }

        if (facilityShopCatalog == null)
        {
            throw new ArgumentNullException(nameof(facilityShopCatalog));
        }

        state?.MarkCompleted(blueprint);

        BlueprintUnlockContext context = new BlueprintUnlockContext(
            state,
            shopUnlockState,
            facilityShopCatalog);
        List<BlueprintUnlockRecord> appliedUnlocks = new List<BlueprintUnlockRecord>();
        foreach (BlueprintUnlock unlock in blueprint.Unlocks)
        {
            if (unlock == null || !unlock.IsConfigured)
            {
                continue;
            }

            BlueprintUnlockRecord applied = unlock.Apply(context);
            if (applied.IsApplied)
            {
                appliedUnlocks.Add(applied);
            }
        }

        return new BlueprintResearchUnlockResult(blueprint, appliedUnlocks);
    }

    public static BlueprintResearchUnlockResult ApplyCompletion(
        ResearchProjectSO project,
        BlueprintResearchState state,
        FacilityShopUnlockState shopUnlockState,
        IFacilityShopCatalog facilityShopCatalog)
    {
        if (project == null)
        {
            return new BlueprintResearchUnlockResult(null, Array.Empty<BlueprintUnlockRecord>());
        }

        if (facilityShopCatalog == null)
        {
            throw new ArgumentNullException(nameof(facilityShopCatalog));
        }

        state?.Projects.Complete(project.ProjectId);
        if (project.Blueprint != null)
        {
            state?.MarkCompleted(project.Blueprint);
        }

        BlueprintUnlockContext context = new BlueprintUnlockContext(
            state,
            shopUnlockState,
            facilityShopCatalog);
        List<BlueprintUnlockRecord> appliedUnlocks = new List<BlueprintUnlockRecord>();
        foreach (BlueprintUnlock unlock in project.Unlocks)
        {
            if (unlock == null || !unlock.IsConfigured)
            {
                continue;
            }

            BlueprintUnlockRecord applied = unlock.Apply(context);
            if (applied.IsApplied)
            {
                appliedUnlocks.Add(applied);
            }
        }

        return new BlueprintResearchUnlockResult(project.Blueprint, appliedUnlocks);
    }
}

public class BlueprintResearchRuntime : MonoBehaviour
{
    [SerializeField] private bool raiseAlertOnResearchComplete = true;

    private readonly BlueprintResearchState state = new BlueprintResearchState();
    private IFacilityShopUnlockStateService shopUnlockStateService;
    private IFacilityShopCatalog facilityShopCatalog;
    private IFacilityCandidateCache facilityCandidateCache;
    private IWorkforceReplanService workforceReplanService;
    private IGameEventBus gameEventBus;
    private IWorldItemStackRuntime itemStackRuntime;
    private IResearchProjectCatalog projectCatalog;
    private IResearchBlueprintArchiveQuery blueprintArchiveQuery;
    private IWorldDropZoneQuery worldDropZoneQuery;
    private float nextArchiveDeliveryRefresh;
    private readonly HashSet<string> pendingKnowledgeDeliveries =
        new HashSet<string>(StringComparer.Ordinal);
    private IDisposable shopPurchasedSubscription;

    public BlueprintResearchState State => state;
    public bool HasActiveResearch =>
        TryResolveActiveProject(out _, out _)
        || state.HasActiveTask;
    public IResearchProjectCatalog ProjectCatalog => projectCatalog;
    public FacilityShopUnlockState ShopUnlockState => ResolveShopUnlockStateService().GetUnlockState();

    [Inject]
    public void Construct(
        IFacilityShopUnlockStateService shopUnlockStateService,
        IFacilityShopCatalog facilityShopCatalog,
        IFacilityCandidateCache facilityCandidateCache,
        IWorkforceReplanService workforceReplanService,
        IGameEventBus gameEventBus,
        IWorldItemStackRuntime itemStackRuntime = null,
        IResearchProjectCatalog projectCatalog = null,
        IResearchBlueprintArchiveQuery blueprintArchiveQuery = null,
        IWorldDropZoneQuery worldDropZoneQuery = null)
    {
        this.shopUnlockStateService = shopUnlockStateService
            ?? throw new ArgumentNullException(nameof(shopUnlockStateService));
        this.facilityShopCatalog = facilityShopCatalog
            ?? throw new ArgumentNullException(nameof(facilityShopCatalog));
        this.facilityCandidateCache = facilityCandidateCache
            ?? throw new ArgumentNullException(nameof(facilityCandidateCache));
        this.workforceReplanService = workforceReplanService
            ?? throw new ArgumentNullException(nameof(workforceReplanService));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.itemStackRuntime = itemStackRuntime;
        this.projectCatalog = projectCatalog;
        this.blueprintArchiveQuery = blueprintArchiveQuery;
        this.worldDropZoneQuery = worldDropZoneQuery;
        SubscribeToScopedEvents();
    }

    public bool EnqueueBlueprint(FacilityBlueprintSO blueprint)
    {
        if (blueprint != null
            && projectCatalog != null
            && projectCatalog.TryGetForBlueprint(blueprint.id, out ResearchProjectSO project))
        {
            return EnqueueProject(project.ProjectId).Succeeded;
        }

        bool queued = state.EnqueueBlueprint(blueprint);
        if (queued)
        {
            NotifyResearchAvailabilityChanged(prioritizeResearch: true);
            gameEventBus.RaiseAlert(
                "연구 대기",
                $"{blueprint.DisplayName} 분석 가능",
                EventAlertImportance.Low,
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
            float projectWork = DungeonDebugRuntimeRules.IsEnabled(DungeonDebugCheat.InstantWork)
                ? project.RequiredWork
                : BlueprintResearchService.CalculateResearchWork(researcher, researchFacility, seconds);
            projectWork += TryConsumeKnowledgeResidue(researchFacility);
            float projectAdded = projectProgress.Add(projectWork, project);
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

        float work = DungeonDebugRuntimeRules.IsEnabled(DungeonDebugCheat.InstantWork)
            ? task.RequiredWork
            : BlueprintResearchService.CalculateResearchWork(researcher, researchFacility, seconds);
        work += TryConsumeKnowledgeResidue(researchFacility);
        float added = task.AddProgress(work);
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
            CompleteTask(task.Blueprint);
        }

        return result;
    }

    public ResearchQueueCommandResult EnqueueProject(ResearchProjectId projectId)
    {
        if (projectCatalog == null || !projectCatalog.TryGet(projectId, out ResearchProjectSO project))
        {
            return new ResearchQueueCommandResult(false, "연구 프로젝트를 찾을 수 없습니다.");
        }
        if (state.Projects.IsCompleted(projectId))
        {
            return new ResearchQueueCommandResult(false, "이미 완료된 연구입니다.");
        }
        if (project.BlueprintRule == ResearchBlueprintRule.Required
            && !HasArchivedBlueprint(project, out string blueprintBlocker))
        {
            return new ResearchQueueCommandResult(false, blueprintBlocker);
        }

        List<ResearchProjectSO> ordered = new List<ResearchProjectSO>();
        CollectQueueDependencies(project, ordered, new HashSet<string>(StringComparer.Ordinal));
        ResearchProjectSO blockedDependency = ordered.FirstOrDefault(candidate =>
            candidate.BlueprintRule == ResearchBlueprintRule.Required
            && !HasArchivedBlueprint(candidate, out _));
        if (blockedDependency != null)
        {
            HasArchivedBlueprint(blockedDependency, out string dependencyBlocker);
            return new ResearchQueueCommandResult(
                false,
                $"{blockedDependency.DisplayName}: {dependencyBlocker}");
        }

        List<ResearchProjectId> added = new List<ResearchProjectId>();
        foreach (ResearchProjectSO candidate in ordered)
        {
            if (state.Projects.IsCompleted(candidate.ProjectId)
                || state.Projects.ContainsInQueue(candidate.ProjectId))
            {
                continue;
            }
            state.Projects.AddQueueEntry(candidate.ProjectId);
            added.Add(candidate.ProjectId);
        }

        if (added.Count == 0)
        {
            return new ResearchQueueCommandResult(false, "이미 연구 큐에 등록되어 있습니다.");
        }

        TryResolveActiveProject(out _, out _);
        NotifyResearchAvailabilityChanged(prioritizeResearch: true);
        return new ResearchQueueCommandResult(
            true,
            $"{project.DisplayName} 연구 경로를 큐에 등록했습니다.",
            added);
    }

    public ResearchQueueCommandResult RemoveProject(ResearchProjectId projectId)
    {
        bool removed = state.Projects.RemoveQueueEntry(projectId);
        if (removed)
        {
            TryResolveActiveProject(out _, out _);
            NotifyResearchAvailabilityChanged();
        }
        return new ResearchQueueCommandResult(
            removed,
            removed ? "연구 큐에서 제거했습니다. 진행률은 보존됩니다." : "큐에 등록된 연구가 아닙니다.");
    }

    public ResearchQueueCommandResult MoveProject(int fromIndex, int toIndex)
    {
        ResearchQueueEntry[] before = state.Projects.Queue.ToArray();
        if (!state.Projects.MovePending(fromIndex, toIndex))
        {
            return new ResearchQueueCommandResult(false, "활성 연구는 이동할 수 없습니다.");
        }

        if (!IsQueueOrderValid())
        {
            int currentIndex = state.Projects.Queue
                .Select((entry, index) => (entry, index))
                .First(pair => pair.entry == before[fromIndex])
                .index;
            state.Projects.MovePending(currentIndex, fromIndex);
            return new ResearchQueueCommandResult(false, "선행 연구보다 앞으로 이동할 수 없습니다.");
        }

        return new ResearchQueueCommandResult(true, "연구 대기 순서를 변경했습니다.");
    }

    public ResearchNodeState GetNodeState(
        ResearchProjectSO project,
        out string blocker)
    {
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

        bool archived = HasArchivedBlueprint(project, out string blueprintBlocker);
        bool prerequisitesComplete = ArePrerequisitesCompleted(project);
        if (project.BlueprintRule == ResearchBlueprintRule.Shortcut && archived)
        {
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
                blueprintArchiveQuery?.GetStatus(project.Blueprint) ?? default;
            return status.IsInTransit
                ? ResearchNodeState.BlueprintInTransit
                : ResearchNodeState.Locked;
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
            if (blueprintArchiveQuery != null
                && blueprintArchiveQuery.TryGetPreferredArchive(
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
            $"research:{researchFacility.BuildingData?.id ?? researchFacility.id}:{researchFacility.centerPos.x}:{researchFacility.centerPos.y}";
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
                gameEventBus.RaiseAlert(
                    "시설 키트 배송 지연",
                    $"{FacilityShopService.GetBuildingName(building)} 설치 키트를 하차장에 놓지 못했습니다.",
                    EventAlertImportance.High,
                    "상점");
                return;
            }

            gameEventBus.RaiseAlert(
                "시설 키트 도착",
                $"{FacilityShopService.GetBuildingName(building)} 설치 키트가 하차장에 도착했습니다.",
                EventAlertImportance.Low,
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
            gameEventBus.RaiseAlert(
                "설계도 배송 지연",
                $"{blueprint.DisplayName} 설계도를 하차장에 놓지 못했습니다.",
                EventAlertImportance.High,
                "연구");
            return;
        }

        gameEventBus.RaiseAlert(
            "설계도 도착",
            $"{blueprint.DisplayName} 설계도가 하차장에 도착했습니다.",
            EventAlertImportance.Low,
            "연구");
    }

    private void Update()
    {
        if (Time.unscaledTime < nextArchiveDeliveryRefresh)
        {
            return;
        }

        nextArchiveDeliveryRefresh = Time.unscaledTime + 1f;
        RequestBlueprintArchiveDeliveries();
        TryResolveActiveProject(out _, out _);
    }

    private void RequestBlueprintArchiveDeliveries()
    {
        if (projectCatalog == null
            || blueprintArchiveQuery == null
            || itemStackRuntime == null)
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
        project = null;
        blocker = string.Empty;
        if (projectCatalog == null || projectCatalog.Projects.Count == 0)
        {
            return false;
        }

        ResearchProjectId currentId = state.Projects.ActiveProjectId;
        if (currentId.IsValid
            && projectCatalog.TryGet(currentId, out ResearchProjectSO current))
        {
            blocker = GetExecutionBlocker(current);
            ResearchQueueEntry currentEntry = state.Projects.Queue
                .FirstOrDefault(entry => entry.ProjectId.Equals(currentId));
            currentEntry?.SetSuspended(blocker);
            if (string.IsNullOrWhiteSpace(blocker))
            {
                project = current;
                return true;
            }
            state.Projects.SetActive(default);
        }

        foreach (ResearchQueueEntry entry in state.Projects.Queue)
        {
            if (!projectCatalog.TryGet(entry.ProjectId, out ResearchProjectSO candidate))
            {
                entry.SetSuspended("연구 정의가 사라졌습니다.");
                continue;
            }

            string candidateBlocker = GetExecutionBlocker(candidate);
            entry.SetSuspended(candidateBlocker);
            if (!string.IsNullOrWhiteSpace(candidateBlocker))
            {
                continue;
            }

            state.Projects.SetActive(candidate.ProjectId);
            project = candidate;
            return true;
        }

        state.Projects.SetActive(default);
        blocker = state.Projects.Queue.FirstOrDefault()?.SuspendedReason
            ?? "실행 가능한 연구가 없습니다.";
        return false;
    }

    private string GetExecutionBlocker(ResearchProjectSO project)
    {
        if (project == null)
        {
            return "연구 정의가 없습니다.";
        }
        bool archived = HasArchivedBlueprint(project, out string blueprintBlocker);
        if (project.BlueprintRule == ResearchBlueprintRule.Shortcut && archived)
        {
            return string.Empty;
        }
        ResearchProjectSO missing = project.Prerequisites
            .FirstOrDefault(required => !state.Projects.IsCompleted(required.ProjectId));
        if (missing != null)
        {
            return $"선행 연구 대기: {missing.DisplayName}";
        }
        if (project.BlueprintRule == ResearchBlueprintRule.Required && !archived)
        {
            return blueprintBlocker;
        }
        return string.Empty;
    }

    private bool ArePrerequisitesCompleted(ResearchProjectSO project)
    {
        return project != null && project.Prerequisites.All(required =>
            required != null && state.Projects.IsCompleted(required.ProjectId));
    }

    private bool HasArchivedBlueprint(
        ResearchProjectSO project,
        out string blocker)
    {
        if (project == null || project.BlueprintRule == ResearchBlueprintRule.None)
        {
            blocker = string.Empty;
            return true;
        }
        if (blueprintArchiveQuery == null)
        {
            blocker = "연구 설계도 보관 상태를 확인할 수 없습니다.";
            return false;
        }
        ResearchBlueprintArchiveStatus status =
            blueprintArchiveQuery.GetStatus(project.Blueprint);
        blocker = status.Blocker;
        return status.IsArchived;
    }

    private void CollectQueueDependencies(
        ResearchProjectSO project,
        ICollection<ResearchProjectSO> ordered,
        ISet<string> visited)
    {
        if (project == null || !visited.Add(project.ProjectId.Value))
        {
            return;
        }

        bool shortcutActive = project.BlueprintRule == ResearchBlueprintRule.Shortcut
            && HasArchivedBlueprint(project, out _);
        if (!shortcutActive)
        {
            foreach (ResearchProjectSO prerequisite in project.Prerequisites
                         .OrderBy(candidate => candidate.ProjectId.Value, StringComparer.Ordinal))
            {
                CollectQueueDependencies(prerequisite, ordered, visited);
            }
        }
        ordered.Add(project);
    }

    private bool IsQueueOrderValid()
    {
        Dictionary<string, int> indexById = state.Projects.Queue
            .Select((entry, index) => (entry, index))
            .ToDictionary(pair => pair.entry.ProjectId.Value, pair => pair.index, StringComparer.Ordinal);
        foreach (ResearchQueueEntry entry in state.Projects.Queue)
        {
            if (!projectCatalog.TryGet(entry.ProjectId, out ResearchProjectSO project))
            {
                return false;
            }
            bool shortcutActive = project.BlueprintRule == ResearchBlueprintRule.Shortcut
                && HasArchivedBlueprint(project, out _);
            if (shortcutActive)
            {
                continue;
            }
            foreach (ResearchProjectSO prerequisite in project.Prerequisites)
            {
                if (state.Projects.IsCompleted(prerequisite.ProjectId))
                {
                    continue;
                }
                if (!indexById.TryGetValue(prerequisite.ProjectId.Value, out int prerequisiteIndex)
                    || prerequisiteIndex >= indexById[project.ProjectId.Value])
                {
                    return false;
                }
            }
        }
        return true;
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
        gameEventBus.Publish(new BlueprintResearchCompletedEvent(project, unlockResult));
        if (notifyAvailability)
        {
            NotifyResearchAvailabilityChanged();
        }

        if (emitAlert && raiseAlertOnResearchComplete)
        {
            List<string> lines = new List<string> { $"{project.DisplayName} 연구 완료" };
            lines.AddRange(unlockResult.FormatSummaryLines());
            gameEventBus.RaiseAlert(
                "연구 완료",
                string.Join("\n", lines),
                EventAlertImportance.Medium,
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
        gameEventBus.Publish(new BlueprintResearchCompletedEvent(blueprint, unlockResult));
        if (notifyAvailability)
        {
            NotifyResearchAvailabilityChanged();
        }

        if (emitAlert)
        {
            gameEventBus.RaiseAlert(
                "연구 완료",
                FormatUnlockResult(unlockResult),
                EventAlertImportance.Medium,
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
        if (!isActiveAndEnabled || shopPurchasedSubscription != null || gameEventBus == null)
        {
            return;
        }

        shopPurchasedSubscription =
            gameEventBus.Subscribe<FacilityShopPurchasedEvent>(OnTriggerEvent);
    }
}
