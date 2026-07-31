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

public sealed class ProductionBuildingPanelPresenter :
    IProductionBuildingPanelPresenter
{
    private readonly IProductionBillRuntime bills;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionWorkshopRuntime workshops;
    private readonly IBlueprintResearchRuntimeProvider researchProvider;
    private readonly IElectricalNetworkRuntime power;
    private readonly IWaterNetworkRuntime water;
    private readonly IWastewaterNetworkRuntime wastewater;
    private readonly IEnvironmentalFieldRuntime environment;
    private readonly Dictionary<string, string> feedbackByFacility =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private GameObject worldLinkRoot;
    private Material worldLinkMaterial;

    public ProductionBuildingPanelPresenter(
        IProductionBillRuntime bills,
        IResourceEconomyContentCatalog catalog,
        IProductionWorkshopRuntime workshops = null,
        IBlueprintResearchRuntimeProvider researchProvider = null,
        IElectricalNetworkRuntime power = null,
        IWaterNetworkRuntime water = null,
        IWastewaterNetworkRuntime wastewater = null,
        IEnvironmentalFieldRuntime environment = null)
    {
        this.bills = bills ?? throw new ArgumentNullException(nameof(bills));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.workshops = workshops;
        this.researchProvider = researchProvider;
        this.power = power;
        this.water = water;
        this.wastewater = wastewater;
        this.environment = environment;
    }

    public void ShowWorldLinks(BuildableObject building)
    {
        ClearWorldLinks();
        if (building == null || workshops == null)
        {
            return;
        }

        IReadOnlyList<ProductionSupportLinkSnapshot> links;
        if (building.BuildingData.GetProductionWorkstationAbility() != null)
        {
            links = workshops.GetLinks(building);
        }
        else if (workshops.TryGetLinkForSupport(
                     building,
                     out ProductionSupportLinkSnapshot supportLink))
        {
            links = new[] { supportLink };
        }
        else
        {
            return;
        }

        if (links.Count == 0)
        {
            return;
        }

        worldLinkRoot = new GameObject("ProductionWorkshopConnections");
        for (int index = 0; index < links.Count; index++)
        {
            ProductionSupportLinkSnapshot link = links[index];
            if (link?.Workstation == null || link.Support == null)
            {
                continue;
            }

            GameObject lineObject = new GameObject($"Connection_{index}");
            lineObject.transform.SetParent(worldLinkRoot.transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.07f;
            line.endWidth = 0.07f;
            line.numCapVertices = 3;
            line.startColor = new Color(0.96f, 0.72f, 0.22f, 0.9f);
            line.endColor = new Color(0.4f, 0.85f, 0.95f, 0.9f);
            line.sortingOrder = 60;
            Material material = GetWorldLinkMaterial();
            if (material != null)
            {
                line.sharedMaterial = material;
            }
            Vector3 start = link.Workstation.transform.position;
            Vector3 end = link.Support.transform.position;
            start.z = -0.5f;
            end.z = -0.5f;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }
    }

    public void ClearWorldLinks()
    {
        if (worldLinkRoot == null)
        {
            return;
        }

        UnityEngine.Object.Destroy(worldLinkRoot);
        worldLinkRoot = null;
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
        IReadOnlyList<ProductionBillSnapshot> queue = bills.GetBills(building);
        if (recipes.Length == 0 && queue.Count == 0)
        {
            return created;
        }

        AddText(
            parent,
            "생산",
            font,
            21f,
            DungeonUiTheme.TextPrimary,
            34f,
            created);
        AddText(
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
            AddText(
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
            GameObject progress = CreateProgress(
                parent,
                bill,
                font,
                index + 1);
            created.Add(progress);

            GameObject actions = CreateRow(
                parent,
                $"ProductionBillActions_{index}",
                38f);
            created.Add(actions);
            AddButton(
                actions.transform,
                bill.Status == ProductionBillStatus.Suspended
                    ? "재개"
                    : "일시 중지",
                font,
                bill.Status == ProductionBillStatus.Suspended,
                () =>
                {
                    ProductionBillCommandResult result = bills.SetSuspended(
                        bill.BillId,
                        bill.Status != ProductionBillStatus.Suspended);
                    feedbackByFacility[facilityKey] = result.Message;
                    showFeedback?.Invoke(result.Message);
                    refresh?.Invoke();
                });
            AddButton(
                actions.transform,
                "취소",
                font,
                false,
                () =>
                {
                    ProductionBillCommandResult result = bills.RemoveBill(
                        bill.BillId,
                        returnMaterials: true);
                    feedbackByFacility[facilityKey] = result.Message;
                    showFeedback?.Invoke(result.Message);
                    refresh?.Invoke();
                });
        }

        AddText(
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
            GameObject recipeRow = CreateRow(
                parent,
                $"ProductionRecipe_{index}",
                82f);
            created.Add(recipeRow);

            AddRecipeText(
                recipeRow.transform,
                $"{recipe.DisplayName}\n"
                + $"{FormatInputs(recipe)} → {FormatOutputs(recipe)}"
                + $" · 작업 {recipe.RequiredWork:0.#}",
                font);
            AddRecipeProcessText(
                recipeRow.transform,
                FormatProcess(recipe) + FormatSupportState(building, recipe),
                font);
            AddButton(
                recipeRow.transform,
                "1회 제작",
                font,
                false,
                () =>
                {
                    ProductionBillCommandResult result = bills.AddBill(
                        building,
                        recipe.RecipeId,
                        ProductionOrderMode.RepeatCount,
                        1);
                    feedbackByFacility[facilityKey] = result.Message;
                    showFeedback?.Invoke(result.Message);
                    refresh?.Invoke();
                });
        }

        return created;
    }

    private void RenderSupportDetail(
        Transform parent,
        BuildableObject support,
        BuildingProductionSupportAbility ability,
        TMP_FontAsset font,
        ICollection<GameObject> created)
    {
        AddText(
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
            AddText(
                parent,
                "같은 닫힌 방에 호환되는 주 작업대가 없습니다.",
                font,
                14f,
                DungeonUiTheme.Warning,
                42f,
                created);
            return;
        }

        AddText(
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
        AddText(
            parent,
            $"기능: {features}\n{FormatSupportUtilities(ability)}",
            font,
            14f,
            DungeonUiTheme.TextSecondary,
            48f,
            created);

        string nodeId = IndustrialInfrastructureIdentity.GetNodeId(support);
        ProductionBillSnapshot[] active = bills.GetBills(link.Workstation)
            .Where(bill => string.Equals(
                bill.OccupiedSupportNodeId,
                nodeId,
                StringComparison.Ordinal))
            .ToArray();
        if (active.Length == 0)
        {
            AddText(
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
            AddText(
                parent,
                $"{batch.RecipeName}: {batch.RemainingProcessingHours:0.#}시간 남음"
                + $" · 건전도 {batch.BatchIntegrity:0.#}"
                + (string.IsNullOrWhiteSpace(batch.BlockedReason)
                    ? string.Empty
                    : $"\n{batch.BlockedReason}"),
                font,
                14f,
                string.IsNullOrWhiteSpace(batch.BlockedReason)
                    ? DungeonUiTheme.TextSecondary
                    : DungeonUiTheme.Warning,
                44f,
                created);
        }
    }

    private Material GetWorldLinkMaterial()
    {
        if (worldLinkMaterial != null)
        {
            return worldLinkMaterial;
        }

        Shader shader = Shader.Find(
            "Universal Render Pipeline/2D/Sprite-Unlit-Default");
        shader ??= Shader.Find("Sprites/Default");
        if (shader != null)
        {
            worldLinkMaterial = new Material(shader)
            {
                name = "ProductionWorkshopConnectionMaterial"
            };
        }
        return worldLinkMaterial;
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
        return $"{building.id}:{building.centerPos.x}:{building.centerPos.y}";
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
            && (researchProvider == null
                || !researchProvider.TryGetRuntime(
                    out BlueprintResearchRuntime research)
                || !research.State.Projects.IsCompleted(
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
                int occupied = bills.GetBills(building).Count(bill =>
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
                && environment != null
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

    private static GameObject CreateProgress(
        Transform parent,
        ProductionBillSnapshot bill,
        TMP_FontAsset font,
        int queueIndex)
    {
        GameObject root = new GameObject(
            $"ProductionBill_{queueIndex}",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredHeight = 58f;
        root.GetComponent<Image>().color = DungeonUiTheme.Panel;

        GameObject fillObject = new GameObject(
            "Fill",
            typeof(RectTransform),
            typeof(Image));
        fillObject.transform.SetParent(root.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        float visibleProgress = bill.Status == ProductionBillStatus.Processing
            || bill.Status == ProductionBillStatus.WaitingForUtilities
                ? bill.ProcessingProgressRatio
                : bill.ProgressRatio;
        fillRect.anchorMax = new Vector2(visibleProgress, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fill = fillObject.GetComponent<Image>();
        fill.color = DungeonUiTheme.Accent;
        fill.raycastTarget = false;

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(root.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 3f);
        labelRect.offsetMax = new Vector2(-8f, -3f);
        TMP_Text text = labelObject.GetComponent<TMP_Text>();
        text.text = $"{queueIndex}. {bill.RecipeName} · "
            + $"{FormatStatus(bill.Status)} · {bill.ProgressRatio:P0}"
            + (string.IsNullOrWhiteSpace(bill.BlockedReason)
                ? string.Empty
                : $"\n{bill.BlockedReason}");
        text.font = font;
        text.fontSize = 15f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 15f;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return root;
    }

    private static string FormatStatus(ProductionBillStatus status)
    {
        return status switch
        {
            ProductionBillStatus.WaitingForMaterials => "재료 운반 대기",
            ProductionBillStatus.Ready => "작업 가능",
            ProductionBillStatus.InProgress => "제작 중",
            ProductionBillStatus.Suspended => "일시 중지",
            ProductionBillStatus.Completed => "완료",
            ProductionBillStatus.Cancelled => "취소됨",
            ProductionBillStatus.WaitingForSupports => "연결 시설 대기",
            ProductionBillStatus.WaitingForUtilities => "설비 대기",
            ProductionBillStatus.Processing => "시간 공정 중",
            ProductionBillStatus.WaitingForFinishing => "마감 작업 대기",
            _ => status.ToString()
        };
    }

    private static GameObject CreateRow(
        Transform parent,
        string name,
        float height)
    {
        GameObject row = new GameObject(
            name,
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        row.GetComponent<LayoutElement>().preferredHeight = height;
        return row;
    }

    private static void AddRecipeText(
        Transform parent,
        string value,
        TMP_FontAsset font)
    {
        GameObject textObject = new GameObject(
            "ProductionRecipeLabel",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        textObject.GetComponent<LayoutElement>().preferredWidth = 330f;
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.font = font;
        text.fontSize = 14f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 14f;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
    }

    private static void AddRecipeProcessText(
        Transform parent,
        string value,
        TMP_FontAsset font)
    {
        GameObject textObject = new GameObject(
            "ProductionProcessLabel",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        textObject.GetComponent<LayoutElement>().preferredWidth = 245f;
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.font = font;
        text.fontSize = 13f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 9f;
        text.fontSizeMax = 13f;
        text.color = DungeonUiTheme.TextSecondary;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
    }

    private static void AddButton(
        Transform parent,
        string label,
        TMP_FontAsset font,
        bool selected,
        Action action)
    {
        GameObject buttonObject = new GameObject(
            "ProductionButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<LayoutElement>().preferredWidth = 118f;
        Button button = buttonObject.GetComponent<Button>();
        DungeonUiTheme.StyleButton(button, selected);
        button.onClick.AddListener(() => action?.Invoke());

        GameObject textObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(5f, 2f);
        rect.offsetMax = new Vector2(-5f, -2f);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = label;
        text.font = font;
        text.fontSize = 14f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 14f;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
    }

    private static void AddText(
        Transform parent,
        string value,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        float height,
        ICollection<GameObject> created)
    {
        GameObject textObject = new GameObject(
            "ProductionText",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        textObject.GetComponent<LayoutElement>().preferredHeight = height;
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        created.Add(textObject);
    }
}
