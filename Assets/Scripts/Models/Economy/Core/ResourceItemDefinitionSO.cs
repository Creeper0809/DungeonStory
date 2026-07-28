using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Economy/Resource Item", order = 0)]
public sealed class ResourceItemDefinitionSO : DataScriptableObject
{
    public const string ResourcePath = "SO/Economy/Items";

    [SerializeField] private string itemId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [TextArea, SerializeField] private string description = string.Empty;
    [SerializeField] private StockCategory stockCategory = StockCategory.General;
    [SerializeField] private ResourceItemKind kind;
    [SerializeField] private ResourceIngredientTag ingredientTags;
    [Min(0), SerializeField] private int unitPrice = 1;
    [Min(0.01f), SerializeField] private float unitWeight = 1f;
    [Min(1), SerializeField] private int maxStack = 75;
    [SerializeField] private Sprite sprite;
    [SerializeField] private string requiredResearchId = string.Empty;
    [SerializeField] private MealQualityTier mealQuality = MealQualityTier.Simple;
    [Min(0f), SerializeField] private float nutrition;
    [SerializeField] private float mealMood;
    [Min(0f), SerializeField] private float freshnessSeconds;
    [SerializeField] private bool preserved;
    [SerializeField] private bool supportsInjuryTreatment;
    [Min(0.1f), SerializeField] private float treatmentPotency = 1f;
    [Min(0f), SerializeField] private float infectionReduction;
    [Min(0f), SerializeField] private float detoxReduction;
    [Min(0f), SerializeField] private float painReduction;

    public string ItemId => itemId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ItemId : displayName.Trim();
    public string Description => description?.Trim() ?? string.Empty;
    public StockCategory StockCategory => stockCategory;
    public ResourceItemKind Kind => kind;
    public ResourceIngredientTag IngredientTags => ingredientTags;
    public int UnitPrice => Mathf.Max(0, unitPrice);
    public float UnitWeight => Mathf.Max(0.01f, unitWeight);
    public int MaxStack => Mathf.Max(1, maxStack);
    public Sprite Sprite => sprite;
    public string RequiredResearchId => requiredResearchId?.Trim() ?? string.Empty;
    public bool IsMeal => kind == ResourceItemKind.Food;
    public MealDietClass MealDietClass => ResourceMealClassification.Classify(ingredientTags);
    public MealQualityTier MealQuality => mealQuality;
    public float Nutrition => Mathf.Max(0f, nutrition);
    public float MealMood => mealMood;
    public float FreshnessSeconds => Mathf.Max(0f, freshnessSeconds);
    public bool Preserved => preserved;
    public bool SupportsInjuryTreatment => supportsInjuryTreatment;
    public float TreatmentPotency => Mathf.Max(0.1f, treatmentPotency);
    public float InfectionReduction => Mathf.Max(0f, infectionReduction);
    public float DetoxReduction => Mathf.Max(0f, detoxReduction);
    public float PainReduction => Mathf.Max(0f, painReduction);

    public DungeonItemDefinition ToDungeonItemDefinition()
    {
        return new DungeonItemDefinition(
            ItemId,
            DisplayName,
            Description,
            stockCategory,
            UnitPrice,
            sprite,
            UnitWeight,
            MaxStack);
    }

#if UNITY_EDITOR
    public void Configure(
        string stableId,
        string name,
        string itemDescription,
        StockCategory category,
        ResourceItemKind itemKind,
        ResourceIngredientTag tags,
        int price,
        float weight,
        int stackLimit,
        string researchId)
    {
        itemId = stableId?.Trim() ?? string.Empty;
        displayName = name?.Trim() ?? string.Empty;
        description = itemDescription?.Trim() ?? string.Empty;
        stockCategory = category;
        kind = itemKind;
        ingredientTags = tags;
        unitPrice = Mathf.Max(0, price);
        unitWeight = Mathf.Max(0.01f, weight);
        maxStack = Mathf.Max(1, stackLimit);
        requiredResearchId = researchId?.Trim() ?? string.Empty;
    }

    public void ConfigureMeal(
        MealQualityTier quality,
        float nutritionAmount,
        float moodAmount,
        float shelfLifeSeconds,
        bool isPreserved)
    {
        mealQuality = quality;
        nutrition = Mathf.Max(0f, nutritionAmount);
        mealMood = moodAmount;
        freshnessSeconds = Mathf.Max(0f, shelfLifeSeconds);
        preserved = isPreserved;
    }

    public void ConfigureMedicine(
        bool canTreatInjuries,
        float potency,
        float infection,
        float detox,
        float pain)
    {
        supportsInjuryTreatment = canTreatInjuries;
        treatmentPotency = Mathf.Max(0.1f, potency);
        infectionReduction = Mathf.Max(0f, infection);
        detoxReduction = Mathf.Max(0f, detox);
        painReduction = Mathf.Max(0f, pain);
    }
#endif
}
