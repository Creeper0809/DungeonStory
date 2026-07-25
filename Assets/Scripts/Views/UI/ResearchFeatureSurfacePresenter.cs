using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ResearchFeatureSurfaceModel
{
    public bool IsAvailable { get; set; }
    public string UnavailableMessage { get; set; } = string.Empty;
    public string ProgressSummary { get; set; } = string.Empty;
    public string ActiveResearchText { get; set; } = string.Empty;
    public string WorkStatusDetail { get; set; } = string.Empty;
    public int ActiveBlueprintId { get; set; } = -1;
    public string ActiveBlueprintName { get; set; } = string.Empty;
    public IReadOnlyList<ResearchFeatureBlueprintRow> Blueprints { get; set; }
        = Array.Empty<ResearchFeatureBlueprintRow>();
}

public sealed class ResearchFeatureBlueprintRow
{
    public int BlueprintId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool Acquired { get; set; }
    public bool Queued { get; set; }
    public bool Completed { get; set; }
}

public readonly struct ResearchFeatureCommandResult
{
    public ResearchFeatureCommandResult(bool succeeded, string message)
    {
        Succeeded = succeeded;
        Message = message ?? string.Empty;
    }

    public bool Succeeded { get; }
    public string Message { get; }
}

public interface IResearchFeatureQueryService
{
    ResearchFeatureSurfaceModel Capture();
}

public interface IResearchFeatureCommandService
{
    ResearchFeatureCommandResult Enqueue(int blueprintId);
    ResearchFeatureCommandResult Cancel(int blueprintId);
}

public sealed class ResearchFeatureQueryService : IResearchFeatureQueryService
{
    private readonly IBlueprintResearchRuntimeProvider runtimeProvider;
    private readonly IFacilityShopCatalog catalog;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IStaffWorkforceQueryService workforceQuery;

    public ResearchFeatureQueryService(
        IBlueprintResearchRuntimeProvider runtimeProvider,
        IFacilityShopCatalog catalog,
        IBuildingWorldQuery buildingWorld,
        IStaffWorkforceQueryService workforceQuery)
    {
        this.runtimeProvider = runtimeProvider
            ?? throw new ArgumentNullException(nameof(runtimeProvider));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.workforceQuery = workforceQuery
            ?? throw new ArgumentNullException(nameof(workforceQuery));
    }

    public ResearchFeatureSurfaceModel Capture()
    {
        if (!runtimeProvider.TryGetRuntime(out BlueprintResearchRuntime runtime))
        {
            return new ResearchFeatureSurfaceModel
            {
                UnavailableMessage = "설계도 연구 런타임이 현재 씬에 없습니다."
            };
        }

        BlueprintResearchState state = runtime.State;
        bool hasActive = state.TryGetActiveTask(out BlueprintResearchTask activeTask);
        BuildableObject facility = FindResearchFacility();
        List<AbilityWork> workers = workforceQuery.FindActiveWorkers()
            .Where((actor) => actor != null && !actor.IsDead)
            .Select((actor) => actor.GetComponent<AbilityWork>())
            .Where((work) =>
                work != null
                && work.WorkPriorities.IsEnabled(BuiltInWorkTypeIds.Research))
            .ToList();
        int assigned = workers.Count(work =>
            work.IsAssignedWork(BuiltInWorkTypeIds.Research)
            && work.assignedShop == facility);

        return new ResearchFeatureSurfaceModel
        {
            IsAvailable = true,
            ProgressSummary =
                $"대기/완료 포함 작업 {state.Tasks.Count}개 / 완료 {state.CompletedBlueprintIds.Count}개",
            ActiveResearchText = hasActive
                ? $"{GetBlueprintName(activeTask.Blueprint)} {activeTask.ProgressRatio:P0} " +
                  $"({activeTask.Progress:0.#}/{activeTask.RequiredWork:0.#})"
                : "진행 중 연구 없음",
            WorkStatusDetail =
                $"{GetBlocker(hasActive, facility, workers, assigned)} / " +
                $"시설 {(facility != null ? GetBuildingName(facility) : "없음")} / " +
                $"연구 허용 {workers.Count}명 / 현재 배정 {assigned}명",
            ActiveBlueprintId = hasActive ? activeTask.Blueprint?.id ?? -1 : -1,
            ActiveBlueprintName = hasActive
                ? GetBlueprintName(activeTask.Blueprint)
                : string.Empty,
            Blueprints = catalog.Blueprints
                .Where(blueprint => blueprint != null)
                .OrderBy(blueprint => blueprint.id)
                .Take(ResearchFeatureSurfacePresenter.MaxVisibleCardsPerSection)
                .Select(blueprint => CreateBlueprintRow(
                    blueprint,
                    state,
                    runtime.ShopUnlockState))
                .ToArray()
        };
    }

    private ResearchFeatureBlueprintRow CreateBlueprintRow(
        FacilityBlueprintSO blueprint,
        BlueprintResearchState state,
        FacilityShopUnlockState unlockState)
    {
        bool acquired = unlockState.IsBlueprintAcquired(blueprint);
        bool queued = state.Tasks.Any(task =>
            task != null
            && task.Blueprint != null
            && task.Blueprint.id == blueprint.id
            && !task.IsCompleted);
        bool completed = state.IsCompleted(blueprint);
        string status = completed
            ? "연구 완료"
            : queued
                ? "연구 큐 등록"
                : acquired
                    ? "연구 가능"
                    : "상점 구매 필요";
        return new ResearchFeatureBlueprintRow
        {
            BlueprintId = blueprint.id,
            Name = GetBlueprintName(blueprint),
            Detail =
                $"{status} / 구매 {blueprint.defaultCost}G / 연구 {blueprint.researchWorkRequired:0.#}\n" +
                FormatRewardPreview(blueprint),
            Acquired = acquired,
            Queued = queued,
            Completed = completed
        };
    }

    private string FormatRewardPreview(FacilityBlueprintSO blueprint)
    {
        Dictionary<int, BuildingSO> buildings = catalog.Buildings
            .Where(building => building != null)
            .GroupBy(building => building.id)
            .ToDictionary(group => group.Key, group => group.First());
        List<string> rewards = new List<string>();
        foreach (BlueprintUnlock unlock in blueprint.Unlocks)
        {
            switch (unlock)
            {
                case BlueprintBuildingUnlock buildingUnlock
                    when buildings.TryGetValue(
                        buildingUnlock.buildingId,
                        out BuildingSO directBuilding):
                    rewards.Add(FacilityShopService.GetBuildingName(directBuilding));
                    break;
                case BlueprintBasicPurchaseUnlock purchaseUnlock
                    when buildings.TryGetValue(
                        purchaseUnlock.buildingId,
                        out BuildingSO purchaseBuilding):
                    rewards.Add(
                        $"{FacilityShopService.GetBuildingName(purchaseBuilding)} 구매");
                    break;
                case BlueprintRecipeUnlock recipeUnlock
                    when !string.IsNullOrWhiteSpace(recipeUnlock.recipeId):
                    rewards.Add($"조합식 {recipeUnlock.recipeId}");
                    break;
            }
        }

        string[] distinct = rewards.Distinct(StringComparer.Ordinal).ToArray();
        if (distinct.Length == 0)
        {
            return string.IsNullOrWhiteSpace(blueprint.description)
                ? "해금 보상 없음"
                : blueprint.description;
        }

        string summary = string.Join(", ", distinct.Take(4));
        return distinct.Length > 4
            ? $"해금: {summary} 외 {distinct.Length - 4}개"
            : $"해금: {summary}";
    }

    private BuildableObject FindResearchFacility()
    {
        return buildingWorld.Buildings
            .FirstOrDefault(building =>
                building != null
                && !building.isDestroy
                && building.SupportsWork(BuiltInWorkTypeIds.Research));
    }

    private static string GetBlocker(
        bool hasActive,
        BuildableObject facility,
        IReadOnlyCollection<AbilityWork> workers,
        int assigned)
    {
        if (!hasActive)
        {
            return "활성 연구 없음";
        }

        if (facility == null)
        {
            return "연구 시설 필요";
        }

        if (workers.Count == 0)
        {
            return "연구 허용 직원 필요";
        }

        if (assigned > 0)
        {
            return "연구 진행 중";
        }

        return workers.Any(work =>
            work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Research)
                == WorkPriorityLevel.Priority1)
            ? "연구 최우선 대기"
            : "다른 우선 업무 대기";
    }

    private static string GetBlueprintName(FacilityBlueprintSO blueprint)
    {
        return blueprint != null
            && !string.IsNullOrWhiteSpace(blueprint.DisplayName)
            ? blueprint.DisplayName
            : "설계도";
    }

    private static string GetBuildingName(BuildableObject building)
    {
        return building.BuildingData != null
            && !string.IsNullOrWhiteSpace(building.BuildingData.objectName)
            ? building.BuildingData.objectName
            : building.name;
    }
}

public sealed class ResearchFeatureCommandService :
    IResearchFeatureCommandService
{
    private readonly IBlueprintResearchRuntimeProvider runtimeProvider;
    private readonly IFacilityShopCatalog catalog;

    public ResearchFeatureCommandService(
        IBlueprintResearchRuntimeProvider runtimeProvider,
        IFacilityShopCatalog catalog)
    {
        this.runtimeProvider = runtimeProvider
            ?? throw new ArgumentNullException(nameof(runtimeProvider));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public ResearchFeatureCommandResult Enqueue(int blueprintId)
    {
        if (!TryResolve(
                blueprintId,
                out BlueprintResearchRuntime runtime,
                out FacilityBlueprintSO blueprint,
                out ResearchFeatureCommandResult failure))
        {
            return failure;
        }

        if (!runtime.ShopUnlockState.IsBlueprintAcquired(blueprint))
        {
            return new ResearchFeatureCommandResult(
                false,
                $"{blueprint.DisplayName} 연구 잠김: 상점에서 설계도를 먼저 구매해야 합니다.");
        }

        if (runtime.State.IsCompleted(blueprint))
        {
            return new ResearchFeatureCommandResult(
                false,
                $"{blueprint.DisplayName}은 이미 연구 완료되었습니다.");
        }

        if (runtime.State.Tasks.Any(task =>
            task != null
            && task.Blueprint != null
            && task.Blueprint.id == blueprint.id
            && !task.IsCompleted))
        {
            return new ResearchFeatureCommandResult(
                false,
                $"{blueprint.DisplayName}은 이미 연구 큐에 있습니다.");
        }

        bool started = runtime.EnqueueBlueprint(blueprint);
        return new ResearchFeatureCommandResult(
            started,
            $"{(started ? "연구 시작" : "연구 시작 실패")}: {blueprint.DisplayName}");
    }

    public ResearchFeatureCommandResult Cancel(int blueprintId)
    {
        if (!TryResolve(
                blueprintId,
                out BlueprintResearchRuntime runtime,
                out FacilityBlueprintSO blueprint,
                out ResearchFeatureCommandResult failure))
        {
            return failure;
        }

        bool cancelled = runtime.TryCancelBlueprint(blueprint, out string message);
        return new ResearchFeatureCommandResult(
            cancelled,
            $"{(cancelled ? "연구 취소" : "취소 실패")}: {message}");
    }

    private bool TryResolve(
        int blueprintId,
        out BlueprintResearchRuntime runtime,
        out FacilityBlueprintSO blueprint,
        out ResearchFeatureCommandResult failure)
    {
        blueprint = catalog.Blueprints.FirstOrDefault(candidate =>
            candidate != null && candidate.id == blueprintId);
        if (blueprint == null)
        {
            runtime = null;
            failure = new ResearchFeatureCommandResult(
                false,
                "선택한 설계도를 찾을 수 없습니다.");
            return false;
        }

        if (!runtimeProvider.TryGetRuntime(out runtime))
        {
            failure = new ResearchFeatureCommandResult(
                false,
                "연구 시설이 준비되지 않았습니다.");
            return false;
        }

        failure = default;
        return true;
    }
}

public sealed class ResearchFeatureSurfacePresenter :
    IFeatureSurfaceTabPresenter
{
    internal const int MaxVisibleCardsPerSection = 8;

    private readonly IResearchFeatureQueryService queryService;
    private readonly IResearchFeatureCommandService commandService;

    public ResearchFeatureSurfacePresenter(
        IResearchFeatureQueryService queryService,
        IResearchFeatureCommandService commandService)
    {
        this.queryService = queryService
            ?? throw new ArgumentNullException(nameof(queryService));
        this.commandService = commandService
            ?? throw new ArgumentNullException(nameof(commandService));
    }

    public TabId Id => TabId.Research;

    public void Present(IFeatureSurfaceView view)
    {
        if (view == null)
        {
            throw new ArgumentNullException(nameof(view));
        }

        ResearchFeatureSurfaceModel model = queryService.Capture();
        if (!model.IsAvailable)
        {
            view.AddLabel(model.UnavailableMessage, 20f, 64f);
            return;
        }

        view.AddSection("연구 진행", model.ProgressSummary);
        view.AddLabel(model.ActiveResearchText, 20f, 38f);
        view.AddDataCard(
            "P0State_ResearchWorkSource",
            "연구 작업 상태",
            model.WorkStatusDetail,
            "확인",
            () => view.ShowFeedback(model.WorkStatusDetail),
            66f);

        if (model.ActiveBlueprintId >= 0)
        {
            view.AddDataCard(
                $"P0Action_ResearchCancel_{model.ActiveBlueprintId}",
                "활성 연구 취소",
                $"{model.ActiveBlueprintName} 연구를 큐에서 제거합니다.",
                "취소",
                () => view.ShowFeedback(
                    commandService.Cancel(model.ActiveBlueprintId).Message),
                66f);
        }

        view.AddSection(
            "설계도 목록",
            "상점에서 구매한 설계도는 여기서 연구 큐에 올릴 수 있습니다.");
        foreach (ResearchFeatureBlueprintRow row in model.Blueprints)
        {
            ResearchFeatureBlueprintRow captured = row;
            view.AddDataCard(
                $"P0Action_ResearchStart_{captured.BlueprintId}",
                captured.Name,
                captured.Detail,
                captured.Acquired && !captured.Queued && !captured.Completed
                    ? "연구 시작"
                    : "상태 확인",
                () => view.ShowFeedback(
                    commandService.Enqueue(captured.BlueprintId).Message),
                108f);
        }
    }
}
