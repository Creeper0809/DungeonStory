using System;
using System.Linq;

public static class CodexRecipeRecorder
{
    public static void RecordResearch(
        CodexState state,
        BlueprintResearchUnlockResult unlockResult,
        BlueprintResearchState researchState,
        IFacilitySynthesisRecipeQuery synthesisRecipeQuery,
        IFacilityShopCatalog facilityShopCatalog)
    {
        if (state == null)
        {
            return;
        }

        if (facilityShopCatalog == null)
        {
            throw new ArgumentNullException(nameof(facilityShopCatalog));
        }

        foreach (BlueprintUnlockRecord unlock in unlockResult.Unlocks ?? Array.Empty<BlueprintUnlockRecord>())
        {
            if (unlock.Facility == null)
            {
                continue;
            }

            CodexObservationRecorder.ObserveFacility(state, unlock.Facility, CodexInfoSource.Research);
            if (!string.IsNullOrWhiteSpace(unlock.CodexDetail))
            {
                CodexFacilityInfoWriter.Add(
                    state,
                    unlock.Facility,
                    unlock.CodexDetail,
                    CodexInfoSource.Research);
            }
        }

        ImportSynthesisRecipes(state, researchState, synthesisRecipeQuery);
    }

    public static void RecordSynthesis(
        CodexState state,
        FacilitySynthesisResult result,
        BlueprintResearchState researchState,
        IFacilitySynthesisRecipeQuery synthesisRecipeQuery)
    {
        if (state == null || result.Recipe == null)
        {
            return;
        }

        CodexObservationRecorder.ObserveFacility(state, result.ResultBuilding, CodexInfoSource.Synthesis);
        AddRecipeInfo(state, result.Recipe, true, CodexInfoSource.Synthesis);
        ImportSynthesisRecipes(state, researchState, synthesisRecipeQuery);
    }

    public static void ImportSynthesisRecipes(
        CodexState state,
        BlueprintResearchState researchState,
        IFacilitySynthesisRecipeQuery synthesisRecipeQuery)
    {
        if (state == null)
        {
            return;
        }

        if (synthesisRecipeQuery == null)
        {
            throw new ArgumentNullException(nameof(synthesisRecipeQuery));
        }

        foreach (FacilitySynthesisRecipeSO recipe in synthesisRecipeQuery.GetAllRecipes())
        {
            bool visible = synthesisRecipeQuery.IsVisible(recipe, researchState);
            if (visible)
            {
                AddRecipeInfo(state, recipe, true, CodexInfoSource.System);
            }
            else if (recipe.IsSpecial)
            {
                AddSpecialRecipeHint(state, recipe);
            }
        }
    }

    private static void AddRecipeInfo(
        CodexState state,
        FacilitySynthesisRecipeSO recipe,
        bool reveal,
        CodexInfoSource source)
    {
        if (state == null || recipe == null || !recipe.HasValidData)
        {
            return;
        }

        string materials = string.Join(" + ", recipe.materialBuildings.Select(FacilityShopService.GetBuildingName));
        string resultName = FacilityShopService.GetBuildingName(recipe.resultBuilding);
        string line = reveal
            ? $"조합식: {materials} -> {resultName}"
            : BuildSpecialRecipeHint(recipe);

        CodexFacilityInfoWriter.Add(state, recipe.resultBuilding, line, source);
        foreach (BuildingSO material in recipe.materialBuildings)
        {
            CodexFacilityInfoWriter.Add(state, material, line, source);
        }
    }

    private static void AddSpecialRecipeHint(CodexState state, FacilitySynthesisRecipeSO recipe)
    {
        if (state == null || recipe == null || !recipe.HasValidData)
        {
            return;
        }

        string hintId = $"special_recipe_hint:{recipe.recipeId}";
        CodexEntryRecord entry = state.GetOrCreate(
            CodexEntryCategory.Facility,
            hintId,
            "미확인 특수 조합식");
        entry.AddInfo(BuildSpecialRecipeHint(recipe), CodexInfoSource.System);
    }

    private static string BuildSpecialRecipeHint(FacilitySynthesisRecipeSO recipe)
    {
        string concept = recipe.resultBuilding != null && recipe.resultBuilding.Defense != null
            ? CodexTextFormatter.FormatDefenseConcept(recipe.resultBuilding.Defense.concept)
            : "특수";
        return $"특수 조합식 힌트: {concept} 계열 연구 필요";
    }
}
