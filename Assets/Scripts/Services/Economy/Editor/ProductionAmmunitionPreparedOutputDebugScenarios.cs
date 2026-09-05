#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionAmmunitionPreparedOutputDebugScenarios
{
    private const int ExpectedTotalRecipeCount = 355;
    private const int ExpectedStandardRecipeCount = 245;
    private const int ExpectedPerishableRecipeCount = 22;
    private const int ExpectedRecipeCount = 21;
    private const int ExpectedWorkstationCount = 7;
    private const int ExpectedWorkwearRecipeCount = 60;
    private const int ExpectedSurgicalRecipeCount = 3;
    private const int ExpectedSinkRecipeCount = 4;

    [MenuItem(
        "DungeonStory/V27/Production/Verify Ammunition Prepared Output")]
    public static void RunAll()
    {
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        ResourceItemDefinitionCatalog items = new(content);
        ResourceEconomyContentCatalog economy = new(content);
        ResourceApparelDefinitionCatalog apparel = new(content);
        SurgicalPartProductionOutputMaximumMassCapability surgical = new();
        ProductionPreparedOutputComponentCodec standardMaterializer = new();
        PerishableFoodPreparedOutputMaterializer perishableMaterializer = new();
        StandardDefinitionProductionOutputCapability standard = new(
            economy,
            standardMaterializer);
        PerishableFoodOutputCapability perishable = new(items);
        CombatAmmunitionCraftOutputCapability ammunition = new(items);
        ProductionOutputHandlerRegistry capabilities = new(
            new IProductionOutputCapability[]
            {
                ammunition,
                perishable,
                standard
            });
        CombatAmmunitionPreparedOutputMaterializer ammunitionMaterializer =
            new();
        ProductionPreparedOutputMaterializerRegistry materializers = new(
            new IProductionPreparedOutputMaterializer[]
            {
                ammunitionMaterializer,
                perishableMaterializer,
                standardMaterializer
            },
            capabilities);

        ValidateWholeRecipeCapabilityCensus(
            economy,
            standard,
            perishable,
            ammunition,
            apparel,
            surgical);

        Dictionary<string, ResourceItemDefinitionSO> ammunitionItems = items.All
            .OfType<ResourceItemDefinitionSO>()
            .Where(definition => definition != null
                && definition.TryGetFeature(out AmmunitionItemFeature _))
            .ToDictionary(
                definition => definition.ItemId,
                StringComparer.Ordinal);
        var rows = economy.Recipes
            .Where(recipe => recipe != null)
            .SelectMany(recipe => recipe.CaptureCanonicalOutputs()
                .Where(output => output != null
                    && ammunitionItems.ContainsKey(output.ItemId))
                .Select(output => new
                {
                    Recipe = recipe,
                    Output = output,
                    Definition = ammunitionItems[output.ItemId]
                }))
            .OrderBy(row => row.Recipe.RecipeId, StringComparer.Ordinal)
            .ThenBy(row => row.Output.OutputLineId, StringComparer.Ordinal)
            .ToArray();

        Require(
            rows.Select(row => row.Recipe.RecipeId)
                    .Distinct(StringComparer.Ordinal)
                    .Count() == ExpectedRecipeCount,
            "Authored ammunition recipe census drifted.");
        Require(
            rows.Select(row => row.Recipe.WorkstationTag)
                    .Distinct(StringComparer.Ordinal)
                    .Count() == ExpectedWorkstationCount,
            "Authored ammunition workstation census drifted.");
        Require(
            rows.Length == ExpectedRecipeCount,
            "Ammunition recipes must currently have one physical ammunition output each.");

        foreach (var row in rows)
        {
            ProductionOutputCapabilityDescriptor descriptor =
                capabilities.CaptureDescriptor(
                    row.Output.OutputLineId,
                    row.Output.ItemId);
            Require(
                string.Equals(
                    descriptor.CapabilityId,
                    ProductionOutputCapabilityIds.CombatAmmunitionCraft,
                    StringComparison.Ordinal)
                && descriptor.CapabilityVersion ==
                    ProductionOutputCapabilityIds.CombatAmmunitionCraftVersion
                && string.Equals(
                    descriptor.ComponentCodecId,
                    ProductionOutputCapabilityIds.CombatAmmunitionStateCodec,
                    StringComparison.Ordinal)
                && ProductionPreparedOutputCapabilitySelection
                    .ClassifyPhysicalCapabilities(
                        new[] { descriptor },
                        capabilities.CapabilityContracts)
                    == ProductionOutputCapabilityRoute.PreparedBatch,
                "Ammunition output did not freeze the prepared capability: "
                + row.Recipe.RecipeId);

            ProductionPreparedOutputComponentProjection encoded =
                materializers.Create(descriptor, row.Definition);
            ProductionPreparedOutputComponentProjection decoded =
                materializers.ValidateAndDecode(
                    descriptor,
                    row.Definition,
                    encoded.CanonicalPayload,
                    encoded.Fingerprint);
            Require(
                encoded.MassSubject.Kind ==
                    PhysicalItemMassSubjectKind.GenericDefinition
                && encoded.RuntimeComponents.Count == 0
                && decoded.RuntimeComponents.Count == 0
                && string.Equals(
                    encoded.ItemDefinitionDigest,
                    ResourceItemSemanticDigest.Capture(row.Definition),
                    StringComparison.Ordinal)
                && string.Equals(
                    encoded.Fingerprint,
                    decoded.Fingerprint,
                    StringComparison.Ordinal),
                "Ammunition materializer round-trip drifted: "
                + row.Recipe.RecipeId);

            RequireThrows(
                () => standardMaterializer.Create(row.Definition),
                "Standard definition-only codec accepted ammunition: "
                + row.Output.ItemId);
        }

        Debug.Log(
            "V27_PRODUCTION_AMMUNITION_PREPARED_OUTPUT=PASS"
            + $" recipes={ExpectedRecipeCount}; workstations={ExpectedWorkstationCount};"
            + $" definitions={ammunitionItems.Count};"
            + $" wholeRecipes={ExpectedTotalRecipeCount};"
            + $" standard={ExpectedStandardRecipeCount};"
            + $" perishable={ExpectedPerishableRecipeCount};"
            + $" workwear={ExpectedWorkwearRecipeCount};"
            + $" surgical={ExpectedSurgicalRecipeCount};"
            + $" sinks={ExpectedSinkRecipeCount}; unsupported=0; mixed=0");
    }

    private static void ValidateWholeRecipeCapabilityCensus(
        ResourceEconomyContentCatalog economy,
        StandardDefinitionProductionOutputCapability standard,
        PerishableFoodOutputCapability perishable,
        CombatAmmunitionCraftOutputCapability ammunition,
        IApparelDefinitionCatalog apparel,
        SurgicalPartProductionOutputMaximumMassCapability surgical)
    {
        int standardRecipes = 0;
        int perishableRecipes = 0;
        int ammunitionRecipes = 0;
        int workwearRecipes = 0;
        int surgicalRecipes = 0;
        int sinkRecipes = 0;
        ProductionRecipeSO[] recipes = economy.Recipes
            .Where(recipe => recipe != null)
            .OrderBy(recipe => recipe.RecipeId, StringComparer.Ordinal)
            .ToArray();
        Require(
            recipes.Length == ExpectedTotalRecipeCount,
            $"Authored production recipe census drifted: {recipes.Length}.");

        foreach (ProductionRecipeSO recipe in recipes)
        {
            ProductionOutputDefinition[] outputs = recipe
                .CaptureCanonicalOutputs()
                .Where(output => output != null)
                .ToArray();
            if (outputs.Length == 0)
            {
                sinkRecipes++;
                continue;
            }

            RecipeOutputFamily[] families = outputs
                .Select(output => ClassifyOutput(
                    recipe.RecipeId,
                    output,
                    standard,
                    perishable,
                    ammunition,
                    apparel,
                    surgical))
                .Distinct()
                .ToArray();
            Require(
                families.Length == 1,
                "A recipe spans multiple physical output capability families: "
                + recipe.RecipeId);
            switch (families[0])
            {
                case RecipeOutputFamily.Standard:
                    standardRecipes++;
                    break;
                case RecipeOutputFamily.Perishable:
                    perishableRecipes++;
                    break;
                case RecipeOutputFamily.Ammunition:
                    ammunitionRecipes++;
                    break;
                case RecipeOutputFamily.Workwear:
                    workwearRecipes++;
                    break;
                case RecipeOutputFamily.Surgical:
                    surgicalRecipes++;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unsupported production output family: "
                        + recipe.RecipeId);
            }
        }

        Require(
            standardRecipes == ExpectedStandardRecipeCount
            && perishableRecipes == ExpectedPerishableRecipeCount
            && ammunitionRecipes == ExpectedRecipeCount
            && workwearRecipes == ExpectedWorkwearRecipeCount
            && surgicalRecipes == ExpectedSurgicalRecipeCount
            && sinkRecipes == ExpectedSinkRecipeCount,
            "Whole production output capability census drifted: "
            + $"standard={standardRecipes}; perishable={perishableRecipes}; "
            + $"ammunition={ammunitionRecipes}; "
            + $"workwear={workwearRecipes}; surgical={surgicalRecipes}; "
            + $"sink={sinkRecipes}.");
    }

    private static RecipeOutputFamily ClassifyOutput(
        string recipeId,
        ProductionOutputDefinition output,
        StandardDefinitionProductionOutputCapability standard,
        PerishableFoodOutputCapability perishable,
        CombatAmmunitionCraftOutputCapability ammunition,
        IApparelDefinitionCatalog apparel,
        SurgicalPartProductionOutputMaximumMassCapability surgical)
    {
        bool isStandard = standard.CanHandle(output.ItemId);
        bool isPerishable = perishable.CanHandle(output.ItemId);
        bool isAmmunition = ammunition.CanHandle(output.ItemId);
        bool isWorkwear = apparel.TryGetByItemId(output.ItemId, out _);
        bool isSurgical = surgical.CanHandle(output.ItemId);
        int specialMatches = (isPerishable ? 1 : 0)
            + (isAmmunition ? 1 : 0)
            + (isWorkwear ? 1 : 0)
            + (isSurgical ? 1 : 0);
        Require(
            specialMatches <= 1,
            "Production output has ambiguous nonstandard capability families: "
            + recipeId
            + "/"
            + output.OutputLineId
            + "/"
            + output.ItemId
            + $" matches={specialMatches}.");
        if (isPerishable)
            return RecipeOutputFamily.Perishable;
        if (isAmmunition)
            return RecipeOutputFamily.Ammunition;
        if (isWorkwear)
            return RecipeOutputFamily.Workwear;
        if (isSurgical)
            return RecipeOutputFamily.Surgical;
        Require(
            isStandard,
            "Production output has no supported capability family: "
            + recipeId
            + "/"
            + output.OutputLineId
            + "/"
            + output.ItemId);
        return RecipeOutputFamily.Standard;
    }

    private enum RecipeOutputFamily
    {
        Standard,
        Perishable,
        Ammunition,
        Workwear,
        Surgical
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (ProductionPreparedOutputComponentCodecException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }
}
#endif
