using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
    Fuel = 1 << 13,
    Feed = 1 << 14
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum FacilitySupplyKind
{
    Fuel = 0,
    Feed = 1
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class FacilitySupplyProfile
{
    public FacilitySupplyKind kind;
    public ResourceIngredientTag requiredTags;
    [Min(0f)] public float minimumValue;
    [Min(1)] public int bufferCapacity = 8;
    public List<string> allowedItemIds = new List<string>();
    public List<string> forbiddenItemIds = new List<string>();
    public List<string> priorityItemIds = new List<string>();

    public bool Allows(ResourceItemDefinitionSO item)
    {
        if (item == null || forbiddenItemIds.Contains(item.ItemId))
        {
            return false;
        }

        bool explicitlyAllowed = allowedItemIds.Count > 0
            && allowedItemIds.Contains(item.ItemId);
        bool tagsMatch = requiredTags == ResourceIngredientTag.None
            || (item.IngredientTags & requiredTags) == requiredTags;
        float value = kind == FacilitySupplyKind.Fuel
            ? item.FuelValue
            : item.FacilityNutritionValue;
        return (allowedItemIds.Count == 0 ? tagsMatch : explicitlyAllowed)
            && value >= Mathf.Max(0f, minimumValue);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CombatMaterialFamily
{
    Wood = 0,
    Stone = 1,
    Bone = 2,
    Metal = 3,
    Textile = 4,
    Leather = 5
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

public interface IResourceEconomyContentCatalog
{
    IReadOnlyList<ResourceItemDefinitionSO> Items { get; }
    IReadOnlyList<ProductionRecipeSO> Recipes { get; }
    IReadOnlyList<CropDefinitionSO> Crops { get; }
    IReadOnlyList<CraftMaterialDefinitionSO> Materials { get; }
    IReadOnlyList<SubstanceDefinitionView> Substances { get; }
    bool TryGetItem(string itemId, out ResourceItemDefinitionSO definition);
    bool TryGetRecipe(string recipeId, out ProductionRecipeSO definition);
    bool TryGetCrop(string cropId, out CropDefinitionSO definition);
    bool TryGetMaterial(string materialId, out CraftMaterialDefinitionSO definition);
    bool TryGetSubstance(string substanceId, out SubstanceDefinitionView definition);
}

public sealed class ResourceUsageEntry
{
    public string ItemId { get; set; } = string.Empty;
    public IReadOnlyList<string> ProducerIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ConsumerIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> RequiredResearchIds { get; set; } = Array.Empty<string>();
    public int ReservedQuantity { get; set; }
    public IReadOnlyList<ProductionConsumerLink> ConsumerLinks { get; set; } =
        Array.Empty<ProductionConsumerLink>();
    public int DirectBranchCount { get; set; }
    public int LongestProductionDepth { get; set; }
}

public interface IResourceUsageIndex
{
    ResourceUsageEntry Get(string itemId);
    IReadOnlyList<string> ValidateContentGraph();
    void InvalidateReservations();
}

public interface IProductionDependencyCatalog
{
    ResourceUsageEntry GetDependency(string itemId);
    IReadOnlyList<ProductionConsumerLink> GetConsumers(string itemId);
    int GetLongestProductionDepth(string itemId);
    IReadOnlyList<string> ValidateProductionGraph();
}
