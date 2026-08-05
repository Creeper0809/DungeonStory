using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

/// <summary>Explicit empty resource catalog adapter for isolated equipment fixtures.</summary>
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class EmptyResourceEconomyContentCatalog : IResourceEconomyContentCatalog
{
    public static readonly EmptyResourceEconomyContentCatalog Instance = new();
    private static readonly IReadOnlyList<ResourceItemDefinitionSO> NoItems =
        Array.Empty<ResourceItemDefinitionSO>();
    private static readonly IReadOnlyList<ProductionRecipeSO> NoRecipes =
        Array.Empty<ProductionRecipeSO>();
    private static readonly IReadOnlyList<CropDefinitionSO> NoCrops =
        Array.Empty<CropDefinitionSO>();
    private static readonly IReadOnlyList<CraftMaterialDefinitionSO> NoMaterials =
        Array.Empty<CraftMaterialDefinitionSO>();
    private static readonly IReadOnlyList<SubstanceDefinitionView> NoSubstances =
        Array.Empty<SubstanceDefinitionView>();

    private EmptyResourceEconomyContentCatalog()
    {
    }

    public IReadOnlyList<ResourceItemDefinitionSO> Items => NoItems;
    public IReadOnlyList<ProductionRecipeSO> Recipes => NoRecipes;
    public IReadOnlyList<CropDefinitionSO> Crops => NoCrops;
    public IReadOnlyList<CraftMaterialDefinitionSO> Materials => NoMaterials;
    public IReadOnlyList<SubstanceDefinitionView> Substances => NoSubstances;

    public bool TryGetItem(string itemId, out ResourceItemDefinitionSO definition)
    {
        definition = null;
        return false;
    }

    public bool TryGetRecipe(string recipeId, out ProductionRecipeSO definition)
    {
        definition = null;
        return false;
    }

    public bool TryGetCrop(string cropId, out CropDefinitionSO definition)
    {
        definition = null;
        return false;
    }

    public bool TryGetMaterial(string materialId, out CraftMaterialDefinitionSO definition)
    {
        definition = null;
        return false;
    }

    public bool TryGetSubstance(string substanceId, out SubstanceDefinitionView definition)
    {
        definition = null;
        return false;
    }
}
