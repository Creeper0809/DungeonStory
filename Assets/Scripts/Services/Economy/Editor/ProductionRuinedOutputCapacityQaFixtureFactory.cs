#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

internal readonly struct ProductionRuinedOutputCapacityQaExpectations
{
    public const string RecipeId = "recipe:qa:ruined-output-proof";
    public const string InputItemId = "resource:grass-straw";
    public const string OutputItemId = "feed:silage";
    public const string WasteItemId = "waste:plant-rot";
    public const int InputQuantity = 30;
    public const long InputUnitMassGrams = 80L;
    public const long WipInputMassGrams = 2_400L;
    public const long CleanWaterMassGrams = 600L;
    public const long WastewaterMassGrams = 300L;
    public const long AvailableMassGrams = 3_000L;
    public const long WasteUnitMassGrams = 600L;
    public const int RecoverableWasteQuantity = 4;
    public const long RecoverableWasteMassGrams = 2_400L;
    public const long DeclaredLossMassGrams = 300L;
    public const int OutputBufferCycleCapacity = 4;
    public const long RequiredMinimumCapacityGrams = 9_600L;
}

/// <summary>
/// Shared semantic authority for the multi-unit ruined-output QA case. Every
/// call returns fresh mutable Unity objects and receipts; only the scalar
/// expectations above are shared.
/// </summary>
internal static class ProductionRuinedOutputCapacityQaFixtureFactory
{
    public static ProductionRecipeSO CreateRecipe(
        ProductionRecipeSO authoredSilage)
    {
        if (authoredSilage == null
            || !string.Equals(
                authoredSilage.RecipeId,
                "recipe:silage",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The ruined-output QA fixture requires the authored silage recipe.");
        }

        ProductionRecipeSO recipe = ScriptableObject
            .CreateInstance<ProductionRecipeSO>();
        recipe.Configure(
            ProductionRuinedOutputCapacityQaExpectations.RecipeId,
            "QA ruined-output proof",
            "Exercises a multi-unit ruined WIP envelope without changing authored assets.",
            authoredSilage.FacilityTag,
            authoredSilage.WorkTypeId.Value,
            string.Empty,
            authoredSilage.RequiredWork,
            new[]
            {
                new ItemAmountDefinition(
                    ProductionRuinedOutputCapacityQaExpectations.InputItemId,
                    ProductionRuinedOutputCapacityQaExpectations.InputQuantity)
            },
            new[]
            {
                new ProductionOutputDefinition(
                    "output:main",
                    ProductionOutputRole.Main,
                    ProductionRuinedOutputCapacityQaExpectations.OutputItemId,
                    3)
            });
        recipe.ConfigureProficiency(
            BuiltInCharacterProficiencyIds.FoodProduction);
        recipe.ConfigureWorkshop(
            authoredSilage.WorkstationTag,
            authoredSilage.RequiredSupportTags,
            ProductionProcessKind.PassiveBatch,
            authoredSilage.BatchSupportTag,
            authoredSilage.PreparationWork,
            authoredSilage.FinishingWork,
            authoredSilage.ProcessingGameHours,
            authoredSilage.OptimalTemperatureMinimum,
            authoredSilage.OptimalTemperatureMaximum,
            authoredSilage.WarningTemperatureMinimum,
            authoredSilage.WarningTemperatureMaximum,
            cleanWater: 1.2f,
            wastewater: 0.6f,
            failedBatchItemId:
                ProductionRuinedOutputCapacityQaExpectations.WasteItemId,
            wastewaterKind:
                ProcessWastewaterComposition.FermentationEffluent);
        recipe.ConfigureProcessClass(authoredSilage.ProcessClass);
        return recipe;
    }

    public static void ValidateAuthority(
        ProductionRecipeSO recipe,
        IPhysicalItemMassQuery massQuery)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));
        if (massQuery == null)
            throw new ArgumentNullException(nameof(massQuery));

        ItemAmountDefinition[] inputs = recipe.Inputs
            .Where(value => value != null)
            .ToArray();
        long inputUnitMass = massQuery.GetDefinitionUnitMass(
            (ItemDefinitionId)
                ProductionRuinedOutputCapacityQaExpectations.InputItemId).Value;
        long wasteUnitMass = massQuery.GetDefinitionUnitMass(
            (ItemDefinitionId)
                ProductionRuinedOutputCapacityQaExpectations.WasteItemId).Value;
        long cleanWater = ProductionFluidMassRules.ToMassGrams(
            recipe.CleanWaterPerCycle);
        long wastewater = ProductionFluidMassRules.ToMassGrams(
            recipe.WastewaterPerCycle);
        if (!string.Equals(
                recipe.RecipeId,
                ProductionRuinedOutputCapacityQaExpectations.RecipeId,
                StringComparison.Ordinal)
            || inputs.Length != 1
            || !string.Equals(
                inputs[0].ItemId,
                ProductionRuinedOutputCapacityQaExpectations.InputItemId,
                StringComparison.Ordinal)
            || inputs[0].Amount !=
                ProductionRuinedOutputCapacityQaExpectations.InputQuantity
            || inputUnitMass !=
                ProductionRuinedOutputCapacityQaExpectations.InputUnitMassGrams
            || checked(inputUnitMass * inputs[0].Amount) !=
                ProductionRuinedOutputCapacityQaExpectations.WipInputMassGrams
            || !string.Equals(
                recipe.SpoilageItemId,
                ProductionRuinedOutputCapacityQaExpectations.WasteItemId,
                StringComparison.Ordinal)
            || wasteUnitMass !=
                ProductionRuinedOutputCapacityQaExpectations.WasteUnitMassGrams
            || cleanWater !=
                ProductionRuinedOutputCapacityQaExpectations.CleanWaterMassGrams
            || wastewater !=
                ProductionRuinedOutputCapacityQaExpectations.WastewaterMassGrams
            || recipe.ProcessKind != ProductionProcessKind.PassiveBatch
            || recipe.WastewaterComposition !=
                ProcessWastewaterComposition.FermentationEffluent)
        {
            throw new InvalidOperationException(
                "The ruined-output QA fixture drifted from its authored mass authority.");
        }
    }

    public static void ApplyRuinedState(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        IPhysicalItemMassQuery massQuery)
    {
        if (record == null)
            throw new ArgumentNullException(nameof(record));
        ValidateAuthority(recipe, massQuery);

        record.SetMaterialsConsumed(true);
        record.SetWipInput(new ProductionWipInputReceipt(
            "production-wip:" + record.billId.Value + ":00000001",
            ProductionRuinedOutputCapacityQaExpectations.InputQuantity,
            ProductionRuinedOutputCapacityQaExpectations.WipInputMassGrams));
        record.SetProcessFluidConsumed(true);
        record.SetProcessFluid(new ProductionProcessFluidReceipt(
            ProductionRuinedOutputCapacityQaExpectations.CleanWaterMassGrams,
            ProductionRuinedOutputCapacityQaExpectations.WastewaterMassGrams,
            wastewaterComponents: new[]
            {
                new ProcessWastewaterComponent(
                    ProcessWastewaterComposition.FermentationEffluent,
                    ProcessWastewaterSourceKind.Recipe,
                    recipe.RecipeId,
                    recipe.WastewaterPerCycle)
            }));
        record.SetBatchIntegrity(0f);
    }
}
#endif
