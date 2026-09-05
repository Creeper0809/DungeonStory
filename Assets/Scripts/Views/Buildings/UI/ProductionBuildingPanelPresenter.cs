using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface IProductionBuildingPanelPresenter
{
    void ShowWorldLinks(BuildableObject building);
    void ClearWorldLinks();
    IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh);
}

public sealed class ProductionPanelOrderContext
{
    public ProductionPanelOrderContext(
        IProductionBillQuery billQuery,
        IProductionBillOrderCommand billCommands,
        IResourceEconomyContentCatalog catalog,
        IProductionDependencyCatalog dependencies,
        IProductionBillWorkExecution workExecution,
        ICharacterWorldQuery characterWorld,
        IInGameNarrativeTextQuery narrativeText)
    {
        BillQuery = billQuery ?? throw new ArgumentNullException(nameof(billQuery));
        BillCommands = billCommands
            ?? throw new ArgumentNullException(nameof(billCommands));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Dependencies = dependencies
            ?? throw new ArgumentNullException(nameof(dependencies));
        WorkExecution = workExecution
            ?? throw new ArgumentNullException(nameof(workExecution));
        CharacterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        NarrativeText = narrativeText
            ?? throw new ArgumentNullException(nameof(narrativeText));
    }

    public IProductionBillQuery BillQuery { get; }
    public IProductionBillOrderCommand BillCommands { get; }
    public IResourceEconomyContentCatalog Catalog { get; }
    public IProductionDependencyCatalog Dependencies { get; }
    public IProductionBillWorkExecution WorkExecution { get; }
    public ICharacterWorldQuery CharacterWorld { get; }
    public IInGameNarrativeTextQuery NarrativeText { get; }
}

public sealed class ProductionPanelFacilityContext
{
    public ProductionPanelFacilityContext(
        IProductionWorkshopRuntime workshops,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IPowerInfrastructureQuery power)
    {
        Workshops = workshops
            ?? throw new ArgumentNullException(nameof(workshops));
        progressionRuntimes = progressionRuntimes
            ?? throw new ArgumentNullException(nameof(progressionRuntimes));
        Research = progressionRuntimes.BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(ProductionPanelFacilityContext)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        Power = power ?? throw new ArgumentNullException(nameof(power));
    }

    public IProductionWorkshopRuntime Workshops { get; }
    public BlueprintResearchRuntime Research { get; }
    public IPowerInfrastructureQuery Power { get; }
}

public sealed class ProductionPanelEnvironmentContext
{
    public ProductionPanelEnvironmentContext(
        IFluidInfrastructureTransaction water,
        IFluidWastewaterTransaction wastewater,
        IEnvironmentalFieldQuery environment,
        IDomainFailureLocalizer failureLocalizer,
        IProductionUiTextQuery productionUiText)
    {
        Water = water ?? throw new ArgumentNullException(nameof(water));
        Wastewater = wastewater
            ?? throw new ArgumentNullException(nameof(wastewater));
        Environment = environment
            ?? throw new ArgumentNullException(nameof(environment));
        FailureLocalizer = failureLocalizer
            ?? throw new ArgumentNullException(nameof(failureLocalizer));
        ProductionUiText = productionUiText
            ?? throw new ArgumentNullException(nameof(productionUiText));
    }

    public IFluidInfrastructureTransaction Water { get; }
    public IFluidWastewaterTransaction Wastewater { get; }
    public IEnvironmentalFieldQuery Environment { get; }
    public IDomainFailureLocalizer FailureLocalizer { get; }
    public IProductionUiTextQuery ProductionUiText { get; }
}

public sealed class ProductionBuildingPanelPresenter :
    IProductionBuildingPanelPresenter
{
    private readonly IProductionBillQuery billQuery;
    private readonly IProductionBillOrderCommand billCommands;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionDependencyCatalog dependencies;
    private readonly IProductionBillWorkExecution workExecution;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IProductionWorkshopRuntime workshops;
    private readonly BlueprintResearchRuntime research;
    private readonly IPowerInfrastructureQuery power;
    private readonly IFluidInfrastructureTransaction water;
    private readonly IFluidWastewaterTransaction wastewater;
    private readonly IEnvironmentalFieldQuery environment;
    private readonly IDomainFailureLocalizer failureLocalizer;
    private readonly IInGameNarrativeTextQuery narrativeText;
    private readonly ProductionRoutePanelPresenter routePanel;
    private readonly Dictionary<string, string> feedbackByFacility =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly ProductionWorkshopLinkRenderer worldLinks =
        new ProductionWorkshopLinkRenderer();

    public ProductionBuildingPanelPresenter(
        ProductionPanelOrderContext orders,
        ProductionPanelFacilityContext facility,
        ProductionPanelEnvironmentContext surroundings)
    {
        orders = orders ?? throw new ArgumentNullException(nameof(orders));
        facility = facility ?? throw new ArgumentNullException(nameof(facility));
        surroundings = surroundings
            ?? throw new ArgumentNullException(nameof(surroundings));
        billQuery = orders.BillQuery;
        billCommands = orders.BillCommands;
        catalog = orders.Catalog;
        dependencies = orders.Dependencies;
        workExecution = orders.WorkExecution;
        characterWorld = orders.CharacterWorld;
        narrativeText = orders.NarrativeText;
        workshops = facility.Workshops;
        research = facility.Research;
        power = facility.Power;
        water = surroundings.Water;
        wastewater = surroundings.Wastewater;
        environment = surroundings.Environment;
        failureLocalizer = surroundings.FailureLocalizer;
        routePanel = new ProductionRoutePanelPresenter(
            billCommands,
            catalog,
            dependencies,
            surroundings.ProductionUiText);
    }

    public void ShowWorldLinks(BuildableObject building)
    {
        worldLinks.Show(building, workshops);
    }

    public void ClearWorldLinks()
    {
        worldLinks.Clear();
    }

    public IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh)
    {
        List<GameObject> created = new List<GameObject>();
        if (parent == null || building == null || building.BuildingData == null)
        {
            return created;
        }

        BuildingProductionSupportAbility supportAbility =
            building.BuildingData.GetProductionSupportAbility();
        if (supportAbility != null)
        {
            RenderSupportDetail(
                parent,
                building,
                supportAbility,
                font,
                created);
            return created;
        }

        ProductionRecipeSO[] recipes = catalog.Recipes
            .Where(recipe =>
                recipe != null
                && recipe.RecipeId.StartsWith("recipe:", StringComparison.Ordinal)
                && building.SupportsWork(recipe.WorkTypeId)
                && building.MatchesProductionWorkstation(recipe))
            .OrderBy(recipe => recipe.DisplayName, StringComparer.Ordinal)
            .ToArray();
        IReadOnlyList<ProductionBillSnapshot> queue = billQuery.GetBills(building);
        if (recipes.Length == 0 && queue.Count == 0)
        {
            return created;
        }

        ProductionBuildingViewFactory.AddText(
            parent,
            "생산",
            font,
            21f,
            DungeonUiTheme.TextPrimary,
            34f,
            created);
        ProductionBuildingViewFactory.AddText(
            parent,
            $"대기열 {queue.Count}건 · 조합 {recipes.Length}개",
            font,
            15f,
            DungeonUiTheme.TextSecondary,
            28f,
            created);
        string facilityKey = GetFacilityKey(building);
        if (feedbackByFacility.TryGetValue(
                facilityKey,
                out string feedback)
            && !string.IsNullOrWhiteSpace(feedback))
        {
            ProductionBuildingViewFactory.AddText(
                parent,
                feedback,
                font,
                14f,
                DungeonUiTheme.Warning,
                30f,
                created);
        }

        for (int index = 0; index < queue.Count; index++)
        {
            ProductionBillSnapshot bill = queue[index];
            GameObject progress = ProductionBuildingViewFactory.CreateProgress(
                parent,
                bill,
                font,
                index + 1,
                bill.BlockedFailure.IsFailure
                    ? failureLocalizer.Localize(bill.BlockedFailure)
                    : string.Empty);
            created.Add(progress);

            GameObject actions = ProductionBuildingViewFactory.CreateRow(
                parent,
                $"ProductionBillActions_{index}",
                32f);
            created.Add(actions);
            ProductionBuildingViewFactory.AddButton(
                actions.transform,
                bill.Status == ProductionBillStatus.Suspended
                    ? "재개"
                    : "일시 중지",
                font,
                bill.Status == ProductionBillStatus.Suspended,
                () =>
                {
                    ProductionBillCommandResult result = billCommands.SetSuspended(
                        bill.BillId,
                        bill.Status != ProductionBillStatus.Suspended);
                    string message = FormatResult(result);
                    feedbackByFacility[facilityKey] = message;
                    showFeedback?.Invoke(message);
                    refresh?.Invoke();
                });
            ProductionBuildingViewFactory.AddButton(
                actions.transform,
                "취소",
                font,
                false,
                () =>
                {
                    ProductionBillCommandResult result = billCommands.RemoveBill(
                        bill.BillId,
                        returnMaterials: true);
                    string message = FormatResult(result);
                    feedbackByFacility[facilityKey] = message;
                    showFeedback?.Invoke(message);
                    refresh?.Invoke();
                });

            ProductionBuildingViewFactory.AddButton(
                actions.transform,
                FormatWorkerPolicy(bill.WorkerPolicy),
                font,
                false,
                () => ApplyResult(
                    billCommands.SetWorkerPolicy(
                        bill.BillId,
                        NextWorkerPolicy(bill.WorkerPolicy)),
                    facilityKey,
                    showFeedback,
                    refresh),
                $"ProductionWorkerPolicy_{index}");

            RenderProductionLimitBreakControls(
                actions.transform,
                bill,
                font,
                facilityKey,
                showFeedback,
                refresh,
                index);

            GameObject modes = ProductionBuildingViewFactory.CreateRow(
                parent,
                $"ProductionBillModes_{index}",
                32f);
            created.Add(modes);
            ProductionBuildingViewFactory.AddButton(
                modes.transform,
                "횟수",
                font,
                bill.Mode == ProductionOrderMode.RepeatCount,
                () => ApplyResult(
                    billCommands.SetOrderMode(
                        bill.BillId,
                        ProductionOrderMode.RepeatCount,
                        Mathf.Max(1, bill.RemainingCycles)),
                    facilityKey,
                    showFeedback,
                    refresh),
                $"ProductionRepeatCount_{index}");
            ProductionBuildingViewFactory.AddButton(
                modes.transform,
                "무한 반복",
                font,
                bill.Mode == ProductionOrderMode.RepeatForever,
                () => ApplyResult(
                    billCommands.SetOrderMode(
                        bill.BillId,
                        ProductionOrderMode.RepeatForever,
                        0),
                    facilityKey,
                    showFeedback,
                    refresh),
                $"ProductionRepeatForever_{index}");
            ProductionBuildingViewFactory.AddButton(
                modes.transform,
                bill.HasStockSensor
                    ? bill.HasUnacknowledgedStockSensorUnlock
                        ? "목표 재고 해금됨"
                        : "목표 재고 10"
                    : "감지반 설치",
                font,
                bill.Mode == ProductionOrderMode.MaintainStock,
                () => ApplyResult(
                    bill.HasStockSensor
                        ? EnableTargetStock(building, bill.BillId)
                        : billCommands.RequestStockSensorInstallation(building),
                    facilityKey,
                    showFeedback,
                    refresh),
                $"ProductionTargetStockTab_{index}");
            ProductionBuildingViewFactory.AddButton(
                modes.transform,
                $"분기 {FormatDistributionMode(bill.DistributionMode)}",
                font,
                false,
                () => ApplyResult(
                    billCommands.SetDistributionPolicy(
                        bill.BillId,
                        NextDistributionMode(bill.DistributionMode),
                        routePanel.BuildRoutePolicies(bill)),
                    facilityKey,
                    showFeedback,
                    refresh),
                $"ProductionDistributionMode_{index}");

            routePanel.Render(
                parent,
                bill,
                index,
                font,
                created,
                result => ApplyResult(
                    result,
                    facilityKey,
                    showFeedback,
                    refresh));
        }

        ProductionBuildingViewFactory.AddText(
            parent,
            "생산 조합",
            font,
            18f,
            DungeonUiTheme.TextPrimary,
            30f,
            created);
        for (int index = 0; index < recipes.Length; index++)
        {
            ProductionRecipeSO recipe = recipes[index];
            GameObject recipeRow = ProductionBuildingViewFactory.CreateRow(
                parent,
                $"ProductionRecipe_{index}",
                118f);
            created.Add(recipeRow);

            ProductionBuildingViewFactory.AddRecipeText(
                recipeRow.transform,
                $"{recipe.DisplayName}\n"
                + $"{narrativeText.GetRequired(InGameNarrativeTextKind.ProductionRecipe, recipe.RecipeId)}\n"
                + $"{FormatInputs(recipe)} → {FormatOutputs(recipe)}"
                + $" · 작업 {recipe.RequiredWork:0.#}",
                font);
            ProductionBuildingViewFactory.AddRecipeProcessText(
                recipeRow.transform,
                FormatProcess(recipe)
                + FormatSupportState(building, recipe)
                + FormatBranches(recipe),
                font);
            ProductionBuildingViewFactory.AddButton(
                recipeRow.transform,
                "1회 제작",
                font,
                false,
                () =>
                {
                    ProductionBillCommandResult result = billCommands.AddBill(
                        building,
                        recipe.RecipeId,
                        ProductionOrderMode.RepeatCount,
                        1);
                    string message = FormatResult(result);
                    feedbackByFacility[facilityKey] = message;
                    showFeedback?.Invoke(message);
                    refresh?.Invoke();
                });
        }

        return created;
    }

    private void RenderProductionLimitBreakControls(
        Transform parent,
        ProductionBillSnapshot bill,
        TMP_FontAsset font,
        string facilityKey,
        Action<string> showFeedback,
        Action refresh,
        int index)
    {
        CharacterActor[] candidates = characterWorld.Characters
            .Where(actor => actor != null
                && !actor.IsDead
                && actor.Progression.ResolveSelectedTraits()
                    .Any(trait => trait != null && trait.id == 305))
            .OrderBy(
                actor => actor.Identity?.PersistentId ?? string.Empty,
                StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0)
            return;

        if (!string.IsNullOrWhiteSpace(bill.EmergencyWorkerId))
        {
            CharacterActor selected = candidates.FirstOrDefault(actor =>
                string.Equals(
                    actor.Identity?.PersistentId,
                    bill.EmergencyWorkerId,
                    StringComparison.Ordinal));
            if (selected == null)
                return;
            ProductionBuildingViewFactory.AddButton(
                parent,
                $"한계 돌파 해제 · {selected.Identity.DisplayName}",
                font,
                true,
                () => ApplyExtremeResult(
                    workExecution.TrySetEmergencyProduction(
                        selected,
                        bill.BillId,
                        enabled: false,
                        out string reason),
                    reason,
                    facilityKey,
                    showFeedback,
                    refresh),
                $"ProductionLimitBreakDisable_{index}");
            return;
        }

        foreach (CharacterActor candidate in candidates)
        {
            CharacterActor captured = candidate;
            ProductionBuildingViewFactory.AddButton(
                parent,
                $"한계 돌파 · {captured.Identity.DisplayName}",
                font,
                false,
                () => ApplyExtremeResult(
                    workExecution.TrySetEmergencyProduction(
                        captured,
                        bill.BillId,
                        enabled: true,
                        out string reason),
                    reason,
                    facilityKey,
                    showFeedback,
                    refresh),
                $"ProductionLimitBreak_{index}_{captured.Identity.PersistentId}");
        }
    }

    private void ApplyExtremeResult(
        bool succeeded,
        string failureReason,
        string facilityKey,
        Action<string> showFeedback,
        Action refresh)
    {
        string message = succeeded
            ? "생산 주문의 한계 돌파 작업자를 갱신했습니다."
            : string.IsNullOrWhiteSpace(failureReason)
                ? "한계 돌파 명령이 거부되었습니다."
                : failureReason;
        feedbackByFacility[facilityKey] = message;
        showFeedback?.Invoke(message);
        refresh?.Invoke();
    }

    private static string FormatWorkerPolicy(
        WorkerSelectionPolicySaveData policy)
    {
        WorkerSelectionPolicySaveData normalized = policy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone();
        if (normalized.mode == WorkerSelectionMode.RuleSet)
            return "작업자: 민첩 7+";
        return normalized.sortMode == WorkerCandidateSortMode.BestExpectedQuality
            ? "작업자: 품질 우선"
            : "작업자: 속도 우선";
    }

    private static WorkerSelectionPolicySaveData NextWorkerPolicy(
        WorkerSelectionPolicySaveData policy)
    {
        WorkerSelectionPolicySaveData normalized = policy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone();
        if (normalized.mode == WorkerSelectionMode.Anyone
            && normalized.sortMode == WorkerCandidateSortMode.Fastest)
        {
            return WorkerSelectionPolicySaveData.Anyone(
                WorkerCandidateSortMode.BestExpectedQuality);
        }
        if (normalized.mode == WorkerSelectionMode.Anyone)
        {
            return new WorkerSelectionPolicySaveData
            {
                mode = WorkerSelectionMode.RuleSet,
                matchMode = WorkerRequirementMatchMode.All,
                sortMode = WorkerCandidateSortMode.BestExpectedQuality,
                minimumSkillId = BuiltInCharacterProficiencyIds.Crafting.Value,
                minimumSkillExperience = 400
            };
        }
        return WorkerSelectionPolicySaveData.Anyone(
            WorkerCandidateSortMode.Fastest);
    }

    private void RenderSupportDetail(
        Transform parent,
        BuildableObject support,
        BuildingProductionSupportAbility ability,
        TMP_FontAsset font,
        ICollection<GameObject> created)
    {
        ProductionBuildingViewFactory.AddText(
            parent,
            "연결 생산 설비",
            font,
            21f,
            DungeonUiTheme.TextPrimary,
            34f,
            created);
        if (workshops == null
            || !workshops.TryGetLinkForSupport(
                support,
                out ProductionSupportLinkSnapshot link))
        {
            ProductionBuildingViewFactory.AddText(
                parent,
                "같은 닫힌 방에 호환되는 주 작업대가 없습니다.",
                font,
                14f,
                DungeonUiTheme.Warning,
                42f,
                created);
            return;
        }

        ProductionBuildingViewFactory.AddText(
            parent,
            $"연결: {link.Workstation.BuildingData?.objectName ?? link.WorkstationTag}",
            font,
            16f,
            DungeonUiTheme.TextPrimary,
            30f,
            created);
        string features = string.Join(
            ", ",
            link.FeatureTags ?? Array.Empty<string>());
        ProductionBuildingViewFactory.AddText(
            parent,
            $"기능: {features}\n{FormatSupportUtilities(ability)}",
            font,
            14f,
            DungeonUiTheme.TextSecondary,
            48f,
            created);

        string nodeId = IndustrialInfrastructureIdentity.GetNodeId(support);
        ProductionBillSnapshot[] active = billQuery.GetBills(link.Workstation)
            .Where(bill => string.Equals(
                bill.OccupiedSupportNodeId,
                nodeId,
                StringComparison.Ordinal))
            .ToArray();
        if (active.Length == 0)
        {
            ProductionBuildingViewFactory.AddText(
                parent,
                $"배치 용량 {ability.BatchCapacity} · 현재 비어 있음",
                font,
                14f,
                DungeonUiTheme.TextSecondary,
                28f,
                created);
            return;
        }

        foreach (ProductionBillSnapshot batch in active)
        {
            string blockedMessage = batch.BlockedFailure.IsFailure
                ? failureLocalizer.Localize(batch.BlockedFailure)
                : string.Empty;
            ProductionBuildingViewFactory.AddText(
                parent,
                $"{batch.RecipeName}: {batch.RemainingProcessingHours:0.#}시간 남음"
                + $" · 건전도 {batch.BatchIntegrity:0.#}"
                + (string.IsNullOrWhiteSpace(blockedMessage)
                    ? string.Empty
                    : $"\n{blockedMessage}"),
                font,
                14f,
                string.IsNullOrWhiteSpace(blockedMessage)
                    ? DungeonUiTheme.TextSecondary
                    : DungeonUiTheme.Warning,
                44f,
                created);
        }
    }

    private static string FormatSupportUtilities(
        BuildingProductionSupportAbility ability)
    {
        List<string> requirements = new List<string>();
        if (ability.requiresPower)
        {
            requirements.Add("전력");
        }
        if (ability.cleanWaterPerCycle > 0f)
        {
            requirements.Add($"상수 {ability.cleanWaterPerCycle:0.##}");
        }
        if (ability.wastewaterPerCycle > 0f)
        {
            requirements.Add($"배수 {ability.wastewaterPerCycle:0.##}");
        }
        if (ability.requiresFuel)
        {
            requirements.Add("물리 연료");
        }
        if (ability.allowsManualWaterFallback)
        {
            requirements.Add("물통 대체 가능");
        }
        return requirements.Count == 0
            ? "유틸리티: 불필요"
            : $"유틸리티: {string.Join(", ", requirements)}";
    }

    private static string GetFacilityKey(BuildableObject building)
    {
        return building.RequirePersistentInstanceId().Value;
    }

    private string FormatInputs(ProductionRecipeSO recipe)
    {
        return FormatAmounts(
            recipe.Inputs,
            input => input.ItemId,
            input => input.Amount);
    }

    private string FormatOutputs(ProductionRecipeSO recipe)
    {
        return FormatAmounts(
            recipe.Outputs,
            output => output.ItemId,
            output => output.Amount);
    }

    private static string FormatProcess(ProductionRecipeSO recipe)
    {
        if (recipe == null
            || recipe.ProcessKind == ProductionProcessKind.WorkOnly)
        {
            return $"작업 {recipe?.RequiredWork ?? 0f:0.#}";
        }

        return $"준비 {recipe.PreparationWork:0.#}"
            + $" → 처리 {recipe.ProcessingGameHours:0.#}시간"
            + (recipe.FinishingWork > 0f
                ? $" → 마감 {recipe.FinishingWork:0.#}"
                : string.Empty);
    }

    private string FormatSupportState(
        BuildableObject building,
        ProductionRecipeSO recipe)
    {
        if (recipe == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(recipe.RequiredResearchId)
            && (!research.State.Projects.IsCompleted(
                    new ResearchProjectId(recipe.RequiredResearchId))))
        {
            return $"\n연구 부족: {recipe.RequiredResearchId}";
        }

        if (recipe.RequiredSupportTags.Count == 0)
        {
            return FormatRecipeUtilityState(building, recipe);
        }

        if (workshops == null)
        {
            return $"\n필요 연결: {string.Join(", ", recipe.RequiredSupportTags)}";
        }

        if (!workshops.HasRequiredSupports(
                building,
                recipe.RequiredSupportTags,
                out string reason))
        {
            return $"\n{reason}";
        }

        HashSet<string> checkedSupports =
            new HashSet<string>(StringComparer.Ordinal);
        foreach (string feature in recipe.RequiredSupportTags)
        {
            if (!workshops.TryResolveSupport(
                    building,
                    feature,
                    null,
                    out BuildableObject support,
                    out BuildingProductionSupportAbility ability))
            {
                return $"\n연결 시설 부족: {feature}";
            }

            string nodeId =
                IndustrialInfrastructureIdentity.GetNodeId(support);
            if (!checkedSupports.Add(nodeId))
            {
                continue;
            }

            string utilityState = FormatSupportUtilityBlock(
                support,
                ability);
            if (!string.IsNullOrWhiteSpace(utilityState))
            {
                return $"\n{utilityState}";
            }
        }

        string recipeUtilityState =
            FormatRecipeUtilityState(building, recipe);
        if (!string.IsNullOrWhiteSpace(recipeUtilityState))
        {
            return recipeUtilityState;
        }

        if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch)
        {
            ProductionSupportLinkSnapshot[] candidates = workshops
                .GetLinks(building)
                .Where(link =>
                    link.Support?.BuildingData.GetProductionSupportAbility()
                        is BuildingProductionSupportAbility ability
                    && ability.kind == ProductionSupportKind.BatchProcessor
                    && ability.Provides(recipe.BatchSupportTag))
                .ToArray();
            bool hasCapacity = candidates.Any(link =>
            {
                BuildingProductionSupportAbility ability =
                    link.Support.BuildingData.GetProductionSupportAbility();
                string nodeId =
                    IndustrialInfrastructureIdentity.GetNodeId(link.Support);
                int occupied = billQuery.GetBills(building).Count(bill =>
                    string.Equals(
                        bill.OccupiedSupportNodeId,
                        nodeId,
                        StringComparison.Ordinal));
                return occupied < ability.BatchCapacity;
            });
            if (!hasCapacity)
            {
                return "\n배치 용량 부족";
            }

            BuildableObject temperatureTarget =
                candidates.FirstOrDefault()?.Support;
            if (temperatureTarget != null
                && environment.TryGetCell(
                    temperatureTarget.centerPos,
                    out EnvironmentalCellSnapshot cell))
            {
                float temperature = cell.TemperatureC;
                if (temperature < recipe.WarningTemperatureMinimum
                    || temperature > recipe.WarningTemperatureMaximum)
                {
                    return $"\n온도 부적합: {temperature:0.#}°C (공정 정지)";
                }
                if (temperature < recipe.OptimalTemperatureMinimum
                    || temperature > recipe.OptimalTemperatureMaximum)
                {
                    return $"\n온도 주의: {temperature:0.#}°C (속도 50%)";
                }
            }
        }

        return "\n제작 가능 · 연결 시설 준비됨";
    }

    private string FormatRecipeUtilityState(
        BuildableObject building,
        ProductionRecipeSO recipe)
    {
        if (recipe.WastewaterPerCycle > 0f
            && (wastewater == null
                || !wastewater.CanAcceptWastewater(
                    building,
                    recipe.WastewaterPerCycle,
                    out _)))
        {
            return "\n배수 불가";
        }
        if (recipe.CleanWaterPerCycle > 0f
            && (water == null
                || !water.CanConsume(
                    building,
                    WorldWaterQuality.Clean,
                    recipe.CleanWaterPerCycle,
                    out _))
            && !recipe.AllowsManualWaterFallback)
        {
            return "\n상수 부족";
        }
        return string.Empty;
    }

    private string FormatSupportUtilityBlock(
        BuildableObject support,
        BuildingProductionSupportAbility ability)
    {
        if (ability.requiresPower
            && (power == null || !power.IsPowered(support)))
        {
            return "전력 부족";
        }
        if (ability.wastewaterPerCycle > 0f
            && (wastewater == null
                || !wastewater.CanAcceptWastewater(
                    support,
                    ability.wastewaterPerCycle,
                    out _)))
        {
            return "배수 불가";
        }
        if (ability.cleanWaterPerCycle > 0f
            && (water == null
                || !water.CanConsume(
                    support,
                    WorldWaterQuality.Clean,
                    ability.cleanWaterPerCycle,
                    out _))
            && !ability.allowsManualWaterFallback)
        {
            return "상수 부족";
        }
        return string.Empty;
    }

    private string FormatAmounts<T>(
        IReadOnlyList<T> values,
        Func<T, string> getItemId,
        Func<T, int> getAmount)
        where T : class
    {
        string[] labels = (values ?? Array.Empty<T>())
            .Where(value => value != null)
            .Select(value =>
            {
                string itemId = getItemId(value);
                string itemName = catalog.TryGetItem(
                    itemId,
                    out ResourceItemDefinitionSO definition)
                        ? definition.DisplayName
                        : itemId;
                return $"{itemName} {getAmount(value)}";
            })
            .ToArray();
        return labels.Length > 0 ? string.Join(" + ", labels) : "없음";
    }

    private string FormatBranches(ProductionRecipeSO recipe)
    {
        if (dependencies == null || recipe == null)
        {
            return string.Empty;
        }

        string[] branches = recipe.Outputs
            .Where(output => output != null)
            .SelectMany(output => dependencies.GetConsumers(output.ItemId))
            .Where(link => link != null && link.IsRealConsumer)
            .Select(link => string.IsNullOrWhiteSpace(link.displayName)
                ? link.consumerId
                : link.displayName)
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToArray();
        return branches.Length == 0
            ? string.Empty
            : $"\n분기: {string.Join(" · ", branches)}";
    }

    private static ProductionDistributionMode NextDistributionMode(
        ProductionDistributionMode mode) => mode switch
    {
        ProductionDistributionMode.DemandWeighted =>
            ProductionDistributionMode.StrictPriority,
        ProductionDistributionMode.StrictPriority =>
            ProductionDistributionMode.FixedRatio,
        _ => ProductionDistributionMode.DemandWeighted
    };

    private static string FormatDistributionMode(
        ProductionDistributionMode mode) => mode switch
    {
        ProductionDistributionMode.StrictPriority => "우선",
        ProductionDistributionMode.FixedRatio => "비율",
        _ => "수요"
    };

    private void ApplyResult(
        ProductionBillCommandResult result,
        string facilityKey,
        Action<string> showFeedback,
        Action refresh)
    {
        string message = FormatResult(result);
        feedbackByFacility[facilityKey] = message;
        showFeedback?.Invoke(message);
        refresh?.Invoke();
    }

    private string FormatResult(ProductionBillCommandResult result) =>
        result.Succeeded
            ? result.Outcome.ToString()
            : failureLocalizer.Localize(result.Failure);

    private ProductionBillCommandResult EnableTargetStock(
        BuildableObject building,
        ProductionBillId billId)
    {
        billCommands.AcknowledgeStockSensorUnlock(building);
        return billCommands.SetOrderMode(
            billId,
            ProductionOrderMode.MaintainStock,
            10);
    }

}
