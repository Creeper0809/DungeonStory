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
        AddedProgress = Mathf.Max(0f, addedProgress);
        TotalProgress = Mathf.Max(0f, totalProgress);
        RequiredWork = Mathf.Max(1f, requiredWork);
        Completed = completed;
        Message = message ?? string.Empty;
    }

    public bool Success { get; }
    public FacilityBlueprintSO Blueprint { get; }
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
    public BlueprintResearchUnlockResult unlockResult;

    public BlueprintResearchCompletedEvent(FacilityBlueprintSO blueprint, BlueprintResearchUnlockResult unlockResult)
    {
        this.blueprint = blueprint;
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
    private IDisposable shopPurchasedSubscription;

    public BlueprintResearchState State => state;
    public bool HasActiveResearch => state.HasActiveTask;
    public FacilityShopUnlockState ShopUnlockState => ResolveShopUnlockStateService().GetUnlockState();

    [Inject]
    public void Construct(
        IFacilityShopUnlockStateService shopUnlockStateService,
        IFacilityShopCatalog facilityShopCatalog,
        IFacilityCandidateCache facilityCandidateCache,
        IWorkforceReplanService workforceReplanService,
        IGameEventBus gameEventBus)
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
        SubscribeToScopedEvents();
    }

    public bool EnqueueBlueprint(FacilityBlueprintSO blueprint)
    {
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

        if (!state.TryGetActiveTask(out BlueprintResearchTask task))
        {
            return new BlueprintResearchWorkResult(false, null, 0f, 0f, 1f, false, "연구할 설계도가 없습니다");
        }

        float work = DungeonDebugRuntimeRules.IsEnabled(DungeonDebugCheat.InstantWork)
            ? task.RequiredWork
            : BlueprintResearchService.CalculateResearchWork(researcher, researchFacility, seconds);
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
        if (!eventType.result.success
            || !eventType.result.TryGetBlueprint(out FacilityBlueprintSO blueprint))
        {
            return;
        }

        EnqueueBlueprint(blueprint);
    }

    private void CompleteTask(FacilityBlueprintSO blueprint)
    {
        CompleteTask(
            blueprint,
            notifyAvailability: true,
            emitAlert: raiseAlertOnResearchComplete);
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
