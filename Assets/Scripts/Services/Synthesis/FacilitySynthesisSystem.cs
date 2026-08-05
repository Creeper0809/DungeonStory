using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

[Serializable]
public class FacilitySynthesisRecipeSnapshot
{
    public string recipeId;
    public string displayName;
    public string resultName;
    public string[] materialNames = Array.Empty<string>();
    public bool special;
    public bool visible;

    public string ToSummaryText()
    {
        string materials = materialNames != null && materialNames.Length > 0
            ? string.Join(" + ", materialNames)
            : "재료 없음";
        string specialText = special ? " / 특수" : string.Empty;
        return $"{materials} -> {resultName}{specialText}";
    }
}

public readonly struct FacilitySynthesisResult
{
    public FacilitySynthesisResult(
        bool success,
        FacilitySynthesisRecipeSO recipe,
        BuildableObject resultBuilding,
        int inheritedLevel,
        string message)
    {
        Success = success;
        Recipe = recipe;
        ResultBuilding = resultBuilding;
        InheritedLevel = Mathf.Max(1, inheritedLevel);
        Message = message ?? string.Empty;
    }

    public bool Success { get; }
    public FacilitySynthesisRecipeSO Recipe { get; }
    public BuildableObject ResultBuilding { get; }
    public int InheritedLevel { get; }
    public string Message { get; }
}

public struct FacilitySynthesisCompletedEvent
{
    public FacilitySynthesisResult result;

    public FacilitySynthesisCompletedEvent(FacilitySynthesisResult result)
    {
        this.result = result;
    }
}

public static class FacilitySynthesisService
{
    public static bool IsRecipeVisible(
        FacilitySynthesisRecipeSO recipe,
        BlueprintResearchState researchState,
        IMetaProgressionRuntimeReader metaProgressionReader)
    {
        return recipe != null && FacilitySynthesisRules.IsRecipeVisible(
            recipe.HasValidData,
            recipe.publicByDefault,
            recipe.recipeId,
            recipe.requiredResearchRecipeId,
            metaProgressionReader?.IsRecipePreserved(recipe.recipeId) ?? false,
            metaProgressionReader?.IsRecipePreserved(
                recipe.requiredResearchRecipeId) ?? false,
            researchState?.UnlockedRecipeIds);
    }

    public static FacilitySynthesisRecipeSnapshot ToSnapshot(
        FacilitySynthesisRecipeSO recipe,
        BlueprintResearchState researchState,
        IMetaProgressionRuntimeReader metaProgressionReader)
    {
        if (recipe == null)
        {
            return null;
        }

        return new FacilitySynthesisRecipeSnapshot
        {
            recipeId = recipe.recipeId,
            displayName = recipe.DisplayName,
            resultName = FacilityShopService.GetBuildingName(recipe.resultBuilding),
            materialNames = recipe.materialBuildings?
                .Where((building) => building != null)
                .Select(FacilityShopService.GetBuildingName)
                .ToArray()
                ?? Array.Empty<string>(),
            special = recipe.IsSpecial,
            visible = IsRecipeVisible(recipe, researchState, metaProgressionReader)
        };
    }

    public static bool MatchesMaterials(FacilitySynthesisRecipeSO recipe, IReadOnlyList<BuildableObject> materials)
    {
        if (recipe == null || !recipe.HasValidData || materials == null)
        {
            return false;
        }

        return FacilitySynthesisRules.MatchesMaterialIds(
            recipe.MaterialBuildingIds,
            materials
            .Where((building) => building != null)
            .Select((building) => building.id));
    }

    public static int CalculateInheritedLevel(
        FacilitySynthesisRecipeSO recipe,
        IReadOnlyList<BuildableObject> materials)
    {
        return FacilitySynthesisRules.CalculateInheritedLevel(
            materials?.Select(building => building?.FacilityLevel ?? 1),
            recipe?.levelInheritanceRatio ?? 0.75f);
    }
}
