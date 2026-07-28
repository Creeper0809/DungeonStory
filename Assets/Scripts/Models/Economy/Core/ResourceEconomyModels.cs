using System;
using System.Collections.Generic;
using UnityEngine;

public enum ResourceItemKind
{
    Raw = 0,
    Intermediate = 1,
    Food = 2,
    Medicine = 3,
    Substance = 4,
    AnimalProduct = 5,
    Waste = 6,
    Ammunition = 7,
    FinishedGood = 8
}

[Flags]
public enum ResourceIngredientTag
{
    None = 0,
    Plant = 1 << 0,
    Fungus = 1 << 1,
    Milk = 1 << 2,
    Egg = 1 << 3,
    Meat = 1 << 4,
    Blood = 1 << 5,
    Fat = 1 << 6,
    Fiber = 1 << 7,
    Wood = 1 << 8,
    Mineral = 1 << 9,
    Arcane = 1 << 10,
    Spoiled = 1 << 11,
    Forbidden = 1 << 12,
    Fuel = 1 << 13
}

public enum ProductionOrderMode
{
    RepeatCount = 0,
    MaintainStock = 1
}

public enum CombatMaterialFamily
{
    Wood = 0,
    Stone = 1,
    Bone = 2,
    Metal = 3,
    Textile = 4,
    Leather = 5
}

public enum SubstanceUseClass
{
    Medicine = 0,
    NonAddictive = 1,
    Addictive = 2,
    Recreational = 3
}

public enum CharacterDietPolicyKind
{
    Free = 0,
    Vegan = 1,
    Vegetarian = 2,
    CarnivorePreferred = 3,
    StrictTaboo = 4
}

public enum SubstancePolicyMode
{
    Forbidden = 0,
    MedicalOnly = 1,
    CombatOnly = 2,
    MoodThreshold = 3,
    Scheduled = 4
}

public static class ResourceMealClassification
{
    public static MealDietClass Classify(ResourceIngredientTag tags)
    {
        bool hasMeat = (tags & (ResourceIngredientTag.Meat
            | ResourceIngredientTag.Blood
            | ResourceIngredientTag.Fat)) != 0;
        bool hasPlant = (tags & (ResourceIngredientTag.Plant
            | ResourceIngredientTag.Fungus)) != 0;
        bool hasVegetarianAnimalProduct = (tags & (ResourceIngredientTag.Milk
            | ResourceIngredientTag.Egg)) != 0;

        if (hasMeat)
        {
            return hasPlant || hasVegetarianAnimalProduct
                ? MealDietClass.Mixed
                : MealDietClass.Carnivore;
        }

        return hasVegetarianAnimalProduct
            ? MealDietClass.Vegetarian
            : MealDietClass.Vegan;
    }

    public static bool IsAllowed(
        CharacterDietPolicyKind policy,
        MealDietClass dietClass,
        bool containsForbiddenIngredient)
    {
        return policy switch
        {
            CharacterDietPolicyKind.Vegan =>
                dietClass == MealDietClass.Vegan,
            CharacterDietPolicyKind.Vegetarian =>
                dietClass is MealDietClass.Vegan or MealDietClass.Vegetarian,
            CharacterDietPolicyKind.CarnivorePreferred =>
                dietClass is MealDietClass.Carnivore or MealDietClass.Mixed,
            CharacterDietPolicyKind.StrictTaboo =>
                !containsForbiddenIngredient,
            _ => true
        };
    }
}

[Serializable]
public sealed class ItemAmountDefinition
{
    [SerializeField] private string itemId = string.Empty;
    [Min(1), SerializeField] private int amount = 1;

    public string ItemId => itemId?.Trim() ?? string.Empty;
    public int Amount => Mathf.Max(1, amount);

    public ItemAmountDefinition()
    {
    }

    public ItemAmountDefinition(string itemId, int amount)
    {
        this.itemId = itemId?.Trim() ?? string.Empty;
        this.amount = Mathf.Max(1, amount);
    }
}

[Serializable]
public sealed class ProductionOutputDefinition
{
    [SerializeField] private string itemId = string.Empty;
    [Min(1), SerializeField] private int amount = 1;
    [Range(0f, 1f), SerializeField] private float probability = 1f;

    public string ItemId => itemId?.Trim() ?? string.Empty;
    public int Amount => Mathf.Max(1, amount);
    public float Probability => Mathf.Clamp01(probability);

    public ProductionOutputDefinition()
    {
    }

    public ProductionOutputDefinition(string itemId, int amount, float probability = 1f)
    {
        this.itemId = itemId?.Trim() ?? string.Empty;
        this.amount = Mathf.Max(1, amount);
        this.probability = Mathf.Clamp01(probability);
    }
}

public interface IResourceEconomyContentCatalog
{
    IReadOnlyList<ResourceItemDefinitionSO> Items { get; }
    IReadOnlyList<ProductionRecipeSO> Recipes { get; }
    IReadOnlyList<CropDefinitionSO> Crops { get; }
    IReadOnlyList<CraftMaterialDefinitionSO> Materials { get; }
    IReadOnlyList<SubstanceDefinitionSO> Substances { get; }
    bool TryGetItem(string itemId, out ResourceItemDefinitionSO definition);
    bool TryGetRecipe(string recipeId, out ProductionRecipeSO definition);
    bool TryGetCrop(string cropId, out CropDefinitionSO definition);
    bool TryGetMaterial(string materialId, out CraftMaterialDefinitionSO definition);
    bool TryGetSubstance(string substanceId, out SubstanceDefinitionSO definition);
}

public sealed class ResourceUsageEntry
{
    public string ItemId { get; set; } = string.Empty;
    public IReadOnlyList<string> ProducerIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ConsumerIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> RequiredResearchIds { get; set; } = Array.Empty<string>();
    public int ReservedQuantity { get; set; }
}

public interface IResourceUsageIndex
{
    ResourceUsageEntry Get(string itemId);
    IReadOnlyList<string> ValidateContentGraph();
    void InvalidateReservations();
}
