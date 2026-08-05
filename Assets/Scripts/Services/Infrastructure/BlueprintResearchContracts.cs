using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

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
    public float RequiredWork => blueprint != null
        ? ResearchProgressRules.ClampRequiredWork(blueprint.researchWorkRequired)
        : 1f;
    public float ProgressRatio => ResearchProgressRules.ProgressRatio(progress, RequiredWork);
    public bool IsCompleted => blueprint != null && progress >= RequiredWork;

    internal float AddProgress(float amount)
    {
        if (blueprint == null || IsCompleted)
        {
            return 0f;
        }

        return ResearchProgressRules.AddProgress(
            ref progress,
            amount,
            RequiredWork);
    }

    internal void RestoreProgress(float value)
    {
        progress = ResearchProgressRules.ClampProgress(value, RequiredWork);
    }

    internal BlueprintResearchTask DeepClone()
    {
        BlueprintResearchTask clone = new BlueprintResearchTask(blueprint);
        clone.progress = progress;
        return clone;
    }
}

public sealed class BlueprintResearchAggregateState
{
    internal readonly ResearchProjectRuntimeState Projects;
    internal readonly List<BlueprintResearchTask> Tasks;
    internal readonly HashSet<int> CompletedBlueprintIds;
    internal readonly HashSet<int> UnlockedBuildingIds;
    internal readonly HashSet<string> UnlockedRecipeIds;
    internal readonly IReadOnlyList<BlueprintResearchTask> TasksView;
    internal readonly IReadOnlyCollection<int> CompletedBlueprintIdsView;
    internal readonly IReadOnlyCollection<int> UnlockedBuildingIdsView;
    internal readonly IReadOnlyCollection<string> UnlockedRecipeIdsView;

    public BlueprintResearchAggregateState()
        : this(
            new ResearchProjectRuntimeState(),
            new List<BlueprintResearchTask>(),
            new HashSet<int>(),
            new HashSet<int>(),
            new HashSet<string>(StringComparer.Ordinal))
    {
    }

    private BlueprintResearchAggregateState(
        ResearchProjectRuntimeState projects,
        List<BlueprintResearchTask> tasks,
        HashSet<int> completedBlueprintIds,
        HashSet<int> unlockedBuildingIds,
        HashSet<string> unlockedRecipeIds)
    {
        Projects = projects;
        Tasks = tasks;
        CompletedBlueprintIds = completedBlueprintIds;
        UnlockedBuildingIds = unlockedBuildingIds;
        UnlockedRecipeIds = unlockedRecipeIds;
        TasksView = ReadOnlyView.List(Tasks);
        CompletedBlueprintIdsView = ReadOnlyView.Collection(CompletedBlueprintIds);
        UnlockedBuildingIdsView = ReadOnlyView.Collection(UnlockedBuildingIds);
        UnlockedRecipeIdsView = ReadOnlyView.Collection(UnlockedRecipeIds);
    }

    public BlueprintResearchAggregateState DeepClone()
    {
        return new BlueprintResearchAggregateState(
            Projects.DeepClone(),
            Tasks.Select(task => task?.DeepClone()).ToList(),
            new HashSet<int>(CompletedBlueprintIds),
            new HashSet<int>(UnlockedBuildingIds),
            new HashSet<string>(UnlockedRecipeIds, StringComparer.Ordinal));
    }
}

public class BlueprintResearchState : IBuildingUnlockStateView
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private BlueprintResearchAggregateState localState;

    public BlueprintResearchState()
    {
        localState = new BlueprintResearchAggregateState();
    }

    internal BlueprintResearchState(
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public IReadOnlyList<BlueprintResearchTask> Tasks => Current.TasksView;
    public IReadOnlyCollection<int> CompletedBlueprintIds => Current.CompletedBlueprintIdsView;
    public IReadOnlyCollection<int> UnlockedBuildingIds => Current.UnlockedBuildingIdsView;
    public IReadOnlyCollection<string> UnlockedRecipeIds => Current.UnlockedRecipeIdsView;

    // ResearchProjectRuntimeState exposes mutable progress and queue entries for
    // legacy callers. Returning the writable slot guarantees those references
    // never point into the live root while a restore candidate is active.
    public ResearchProjectRuntimeState Projects => Writable.Projects;

    public bool HasActiveTask => TryGetActiveTask(out _);

    public bool EnqueueBlueprint(FacilityBlueprintSO blueprint)
    {
        BlueprintResearchAggregateState state = Writable;
        if (blueprint == null || state.CompletedBlueprintIds.Contains(blueprint.id))
        {
            return false;
        }

        if (state.Tasks.Any((task) =>
                task.Blueprint == blueprint || task.Blueprint?.id == blueprint.id))
        {
            return false;
        }

        state.Tasks.Add(new BlueprintResearchTask(blueprint));
        return true;
    }

    public bool TryGetActiveTask(out BlueprintResearchTask task)
    {
        task = Writable.Tasks.FirstOrDefault((candidate) =>
            candidate != null && !candidate.IsCompleted);
        return task != null;
    }

    public bool IsCompleted(FacilityBlueprintSO blueprint)
    {
        return blueprint != null && Current.CompletedBlueprintIds.Contains(blueprint.id);
    }

    public bool TryCancelBlueprint(FacilityBlueprintSO blueprint)
    {
        if (blueprint == null)
        {
            return false;
        }

        BlueprintResearchAggregateState state = Writable;
        BlueprintResearchTask task = state.Tasks.FirstOrDefault((candidate) =>
            candidate != null
            && candidate.Blueprint != null
            && candidate.Blueprint.id == blueprint.id
            && !candidate.IsCompleted);
        return task != null && state.Tasks.Remove(task);
    }

    public void MarkCompleted(FacilityBlueprintSO blueprint)
    {
        if (blueprint == null)
        {
            return;
        }

        Writable.CompletedBlueprintIds.Add(blueprint.id);
    }

    public bool UnlockRecipe(string recipeId)
    {
        return !string.IsNullOrWhiteSpace(recipeId)
            && Writable.UnlockedRecipeIds.Add(recipeId);
    }

    public bool UnlockBuilding(int buildingId)
    {
        return buildingId >= 0 && Writable.UnlockedBuildingIds.Add(buildingId);
    }

    public bool IsBuildingUnlocked(int buildingId)
    {
        return buildingId >= 0 && Current.UnlockedBuildingIds.Contains(buildingId);
    }

    public bool RestoreTask(FacilityBlueprintSO blueprint, float progress)
    {
        if (!EnqueueBlueprint(blueprint))
        {
            return false;
        }

        List<BlueprintResearchTask> tasks = Writable.Tasks;
        BlueprintResearchTask task = tasks[tasks.Count - 1];
        task.RestoreProgress(progress);
        return true;
    }

    public void RestoreCompletedBlueprintId(int blueprintId)
    {
        if (blueprintId >= 0)
        {
            Writable.CompletedBlueprintIds.Add(blueprintId);
        }
    }

    public void RestoreUnlockedBuildingId(int buildingId)
    {
        UnlockBuilding(buildingId);
    }

    internal BlueprintResearchAggregateState CaptureAggregateClone()
    {
        return Current.DeepClone();
    }

    internal void ReplaceFrom(BlueprintResearchState source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        ReplaceAggregate(source.CaptureAggregateClone());
    }

    private BlueprintResearchAggregateState Current =>
        aggregateRootStore != null
            ? aggregateRootStore.GetOrCreate(
                () => new BlueprintResearchAggregateState())
            : localState;

    private BlueprintResearchAggregateState Writable =>
        aggregateRootStore != null
            ? aggregateRootStore.GetOrCreateWritable(
                () => new BlueprintResearchAggregateState(),
                state => state.DeepClone())
            : localState;

    private void ReplaceAggregate(BlueprintResearchAggregateState state)
    {
        if (aggregateRootStore != null)
        {
            aggregateRootStore.Replace(state);
            return;
        }

        localState = state ?? throw new ArgumentNullException(nameof(state));
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
    public static float CalculateResearchWork(CharacterActor researcher, BuildableObject researchFacility, float seconds)
    {
        float characterMultiplier = researcher != null
            ? Mathf.Max(0.05f, researcher.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Research))
            : 1f;
        float facilityMultiplier = GetFacilityResearchMultiplier(researchFacility);
        return ResearchProgressRules.CalculateResearchWork(
            seconds,
            characterMultiplier,
            facilityMultiplier,
            CharacterSkillRuntimeEffects.GetResearchWorkBonus(researcher, seconds));
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

        BlueprintUnlockContext context = CreateUnlockContext(
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

        BlueprintUnlockContext context = CreateUnlockContext(
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

    private static BlueprintUnlockContext CreateUnlockContext(
        BlueprintResearchState state,
        FacilityShopUnlockState shopUnlockState,
        IFacilityShopCatalog facilityShopCatalog)
    {
        return new BlueprintUnlockContext(
            buildingId => ApplyBuildingUnlock(
                buildingId,
                state,
                facilityShopCatalog),
            buildingId => ApplyBasicPurchaseUnlock(
                buildingId,
                shopUnlockState,
                facilityShopCatalog),
            recipeId => ApplyRecipeUnlock(recipeId, state));
    }

    private static BlueprintUnlockRecord ApplyBuildingUnlock(
        int buildingId,
        BlueprintResearchState state,
        IFacilityShopCatalog facilityShopCatalog)
    {
        BuildingSO building = FacilityShopService.FindBuildingById(
            facilityShopCatalog,
            buildingId);
        if (building == null
            || state == null
            || !state.UnlockBuilding(building.id))
        {
            return default;
        }

        return new BlueprintUnlockRecord(
            BlueprintUnlockTypeIds.Building,
            "시설 해금",
            buildingId.ToString(),
            FacilityShopService.GetBuildingName(building),
            building);
    }

    private static BlueprintUnlockRecord ApplyBasicPurchaseUnlock(
        int buildingId,
        FacilityShopUnlockState shopUnlockState,
        IFacilityShopCatalog facilityShopCatalog)
    {
        BuildingSO building = FacilityShopService.FindBuildingById(
            facilityShopCatalog,
            buildingId);
        if (building == null
            || shopUnlockState == null
            || !shopUnlockState.UnlockBasicPurchase(building))
        {
            return default;
        }

        return new BlueprintUnlockRecord(
            BlueprintUnlockTypeIds.BasicPurchase,
            "기본 구매",
            buildingId.ToString(),
            FacilityShopService.GetBuildingName(building),
            building,
            "기본 구매: 연구 완료 후 구매 가능");
    }

    private static BlueprintUnlockRecord ApplyRecipeUnlock(
        string recipeId,
        BlueprintResearchState state)
    {
        if (state == null || !state.UnlockRecipe(recipeId))
        {
            return default;
        }

        return new BlueprintUnlockRecord(
            BlueprintUnlockTypeIds.Recipe,
            "조합식",
            recipeId,
            recipeId);
    }
}
