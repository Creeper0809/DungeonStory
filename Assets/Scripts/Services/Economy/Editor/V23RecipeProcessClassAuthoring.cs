#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V23RecipeProcessClassAuthoring
{
    private const string ContentRoot = "Assets/Resources/SO";

    public static ProductionProcessClass Resolve(
        string facilityOrWorkstationTag,
        string workTypeId,
        ProductionFlowRole flowRole,
        string outputItemId = null)
    {
        if (flowRole == ProductionFlowRole.Source)
            return ProductionProcessClass.Gathering;

        string tag = NormalizeWorkstationTag(facilityOrWorkstationTag);
        if (string.Equals(tag, "v3:subterranean", StringComparison.Ordinal)
            || string.Equals(tag, "subterranean", StringComparison.Ordinal))
        {
            return outputItemId switch
            {
                "supply:nitrate-fertilizer" => ProductionProcessClass.Chemical,
                "supply:mushroom-substrate" =>
                    ProductionProcessClass.CookingSimpleMixing,
                _ => throw new InvalidOperationException(
                    $"No authored subterranean process-class mapping exists for "
                    + $"output '{outputItemId}', work type '{workTypeId}', "
                    + $"and flow '{flowRole}'.")
            };
        }
        return tag switch
        {
            "incinerator" => ProductionProcessClass.CuttingGrindingWashing,

            "mill" or "stonecutter" =>
                ProductionProcessClass.CuttingGrindingWashing,

            "brewery" or "cookbench" or "feedbench" or "smoker"
                or "composter" =>
                ProductionProcessClass.CookingSimpleMixing,

            "charcoal-kiln" or "loom" or "sawmill" or "tannery"
                or "v19:seasonal-storage" or "v22:manual-spinning"
                or "v22:powered-spinning" or "v22:powered-weaving"
                or "v22:tailoring" or "v3:armor-tailoring"
                or "v3:bow-jig" or "v3:dining-operations" or "v3:fungal"
                or "v3:logistics" or "v3:show" or "v3:treated-lumber" =>
                ProductionProcessClass.SpinningWeavingWoodworking,

            "forge" or "furnace" or "v3:chain" or "v3:machine-parts"
                or "v3:maintenance" or "v3:plate-jig"
                or "v3:prison-labor" or "v3:restraint" or "v3:windlass" =>
                ProductionProcessClass.ForgingHeavyAssembly,

            "alchemy" or "distillery" or "v19:crop-pathology"
                or "v19:pest-control" or "v3:powder-mill"
                or "v3:sanitation" =>
                ProductionProcessClass.Chemical,

            "jeweler" or "m06" or "v19:career-records"
                or "v19:cultivar-breeding" or "v19:greenhouse"
                or "v19:memorial" or "v19:room-assignment"
                or "v19:seasonal-calendar" or "v19:trait-analysis"
                or "v19:weather-observation" or "v21:ballistics-range"
                or "v3:ammo-press" or "v3:appraisal" or "v3:breeding"
                or "v3:defense-ammo" or "v3:factory-layout"
                or "v3:heraldry" or "v3:material-test" or "v3:metrology"
                or "v3:precision-fitting" or "v3:precision-parts"
                or "v3:prototype" or "v3:restoration" or "v3:retail"
                or "v3:signals" =>
                ProductionProcessClass.Precision,

            "apothecary" or "v19:blood-rejuvenation" or "v19:counseling"
                or "v19:cross-lineage" or "v19:isolation"
                or "v19:obstetrics" or "v19:organ-regeneration"
                or "v19:regenerative-culture" or "v19:rune-hibernation"
                or "v19:temporal-stasis" or "v19:vaccine"
                or "v19:whole-body-regeneration" or "v3:growth-frame"
                or "v3:construct-core-engineering" =>
                ProductionProcessClass.Medical,

            "arcane-forge" or "arcane-loom" or "v3:ritual"
                or "v3:rune-conductor" or "v3:rune-control" =>
                ProductionProcessClass.Rune,

            "steelworks" or "v21:blacksteel-annex" or "v3:cooling"
                or "v3:irrigation" or "v3:powered-tools" =>
                ProductionProcessClass.HeavyIndustrial,

            _ => throw new InvalidOperationException(
                $"No authored process-class mapping exists for facility "
                + $"'{facilityOrWorkstationTag}', work type '{workTypeId}', "
                + $"and flow '{flowRole}'.")
        };
    }

    private static string NormalizeWorkstationTag(string value)
    {
        string tag = value?.Trim() ?? string.Empty;
        const string prefix = "workstation:";
        return tag.StartsWith(prefix, StringComparison.Ordinal)
            ? tag.Substring(prefix.Length)
            : tag;
    }

    public static void NormalizeRecipeWorkUnder(
        string recipeRoot,
        bool requireEveryRecipeAuthored = false,
        bool recalculateAuthoredBalanceWork = false)
    {
        if (string.IsNullOrWhiteSpace(recipeRoot))
            throw new ArgumentException("Recipe root is required.", nameof(recipeRoot));

        V23BalanceWorkCalculator calculator = null;
        if (recalculateAuthoredBalanceWork)
        {
            ScriptableObject[] definitions = AssetDatabase.FindAssets(
                    "t:ScriptableObject",
                    new[] { ContentRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadMainAssetAtPath)
                .OfType<ScriptableObject>()
                .Distinct()
                .ToArray();
            calculator = new V23BalanceWorkCalculator(
                new ResourceMaterialEconomicProfileCatalog(
                    new AssetDatabaseContentSource(definitions)));
        }

        foreach (ProductionRecipeSO recipe in AssetDatabase.FindAssets(
                     "t:ProductionRecipeSO",
                     new[] { recipeRoot })
                 .Select(AssetDatabase.GUIDToAssetPath)
                 .Select(AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>)
                 .Where(value => value != null)
                 .OrderBy(value => value.RecipeId, StringComparer.Ordinal))
        {
            if (!recipe.HasAuthoredProcessClass)
            {
                if (requireEveryRecipeAuthored)
                {
                    throw new InvalidOperationException(
                        $"Recipe '{recipe.RecipeId}' cannot be normalized before its "
                        + "production process class is authored.");
                }
                continue;
            }
            if (recalculateAuthoredBalanceWork)
            {
                recipe.ConfigureBalanceWork(calculator.CalculateRecipe(recipe));
                EditorUtility.SetDirty(recipe);
            }
        }
    }

    private sealed class AssetDatabaseContentSource : IGameContentDefinitionSource
    {
        private readonly ScriptableObject[] definitions;

        public AssetDatabaseContentSource(ScriptableObject[] definitions)
        {
            this.definitions = definitions ?? Array.Empty<ScriptableObject>();
        }

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject =>
            definitions.OfType<T>().ToArray();

        public T RequireSingle<T>() where T : ScriptableObject
        {
            T[] values = definitions.OfType<T>().ToArray();
            return values.Length == 1
                ? values[0]
                : throw new InvalidOperationException(
                    $"Expected one {typeof(T).Name}, found {values.Length}.");
        }
    }
}
#endif
