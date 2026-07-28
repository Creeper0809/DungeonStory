using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface IProductionBuildingPanelPresenter
{
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
    private readonly Dictionary<string, string> feedbackByFacility =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public ProductionBuildingPanelPresenter(
        IProductionBillRuntime bills,
        IResourceEconomyContentCatalog catalog)
    {
        this.bills = bills ?? throw new ArgumentNullException(nameof(bills));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
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

        ProductionRecipeSO[] recipes = catalog.Recipes
            .Where(recipe =>
                recipe != null
                && recipe.RecipeId.StartsWith("recipe:", StringComparison.Ordinal)
                && building.SupportsWork(recipe.WorkTypeId)
                && building.HasSemanticTag(recipe.FacilityTag))
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
                58f);
            created.Add(recipeRow);

            AddRecipeText(
                recipeRow.transform,
                $"{recipe.DisplayName}\n"
                + $"{FormatInputs(recipe)} → {FormatOutputs(recipe)}"
                + $" · 작업 {recipe.RequiredWork:0.#}",
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
        fillRect.anchorMax = new Vector2(bill.ProgressRatio, 1f);
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
