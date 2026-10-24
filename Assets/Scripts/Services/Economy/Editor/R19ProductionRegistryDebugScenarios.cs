#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Balance;
using UnityEditor;
using UnityEngine;

public static class R19ProductionRegistryDebugScenarios
{
    [MenuItem("DungeonStory/V27/Production/Run R19 Registry Scenarios")]
    public static void RunAll()
    {
        VerifyMassExplanationRegistration();
        VerifyOutputCostAllocationRegistration();
        VerifyResourceCatalogRejectsUnregisteredPoliciesAndRetries();
        Debug.Log("R19_PRODUCTION_REGISTRY_SCENARIOS=PASS");
    }

    private static void VerifyMassExplanationRegistration()
    {
        ProductionMassExplanationCapabilityRegistry registry =
            ProductionMassExplanationCapabilityRegistry.CreateDefault();
        string registeredKey = ProcessLossProductionMassExplanationCapability.Id
            + "@" + ProcessLossProductionMassExplanationCapability.Version;
        Require(registry.RegisteredKeys.Contains(registeredKey, StringComparer.Ordinal),
            "Mass explanation registry did not expose its registered key.");
        registry.ValidateRegistered(new ProductionMassExplanationAuthoringSnapshot(
            ProcessLossProductionMassExplanationCapability.Id,
            ProcessLossProductionMassExplanationCapability.Version,
            "registration-check"));
        registry.ValidateRegistered(ProductionMassExplanationAuthoringSnapshot.Empty);
        ExpectInvalidOperation(() => registry.ValidateRegistered(
            new ProductionMassExplanationAuthoringSnapshot(
                "unknown-mass-policy",
                ProcessLossProductionMassExplanationCapability.Version,
                "registration-check")),
            "Mass explanation registry accepted an unknown ID.");
        ExpectInvalidOperation(() => registry.ValidateRegistered(
            new ProductionMassExplanationAuthoringSnapshot(
                ProcessLossProductionMassExplanationCapability.Id,
                ProcessLossProductionMassExplanationCapability.Version + 1,
                "registration-check")),
            "Mass explanation registry accepted an unknown version.");
    }

    private static void VerifyOutputCostAllocationRegistration()
    {
        ProductionOutputCostAllocationCapabilityRegistry registry =
            ProductionOutputCostAllocationCapabilityRegistry.CreateDefault();
        string registeredKey =
            WeightedOutputShareProductionOutputCostAllocationCapability.Id
            + "@" + WeightedOutputShareProductionOutputCostAllocationCapability.Version;
        Require(registry.RegisteredKeys.Contains(registeredKey, StringComparer.Ordinal),
            "Output-cost registry did not expose its registered key.");
        registry.ValidateRegistered(
            new ProductionOutputCostAllocationAuthoringSnapshot(
                WeightedOutputShareProductionOutputCostAllocationCapability.Id,
                WeightedOutputShareProductionOutputCostAllocationCapability.Version,
                "registration-check"));
        registry.ValidateRegistered(
            ProductionOutputCostAllocationAuthoringSnapshot.Empty);
        ExpectInvalidOperation(() => registry.ValidateRegistered(
            new ProductionOutputCostAllocationAuthoringSnapshot(
                "unknown-output-cost-policy",
                WeightedOutputShareProductionOutputCostAllocationCapability.Version,
                "registration-check")),
            "Output-cost registry accepted an unknown ID.");
        ExpectInvalidOperation(() => registry.ValidateRegistered(
            new ProductionOutputCostAllocationAuthoringSnapshot(
                WeightedOutputShareProductionOutputCostAllocationCapability.Id,
                WeightedOutputShareProductionOutputCostAllocationCapability.Version + 1,
                "registration-check")),
            "Output-cost registry accepted an unknown version.");
    }

    private static void VerifyResourceCatalogRejectsUnregisteredPoliciesAndRetries()
    {
        ProductionRecipeSO source = Resources
            .LoadAll<ProductionRecipeSO>(ProductionRecipeSO.ResourcePath)
            .OrderBy(value => value?.RecipeId, StringComparer.Ordinal)
            .FirstOrDefault(value => value != null)
            ?? throw new InvalidOperationException(
                "R19 registry fixture requires one production recipe.");
        ResourceItemDefinitionSO[] items = Resources
            .LoadAll<ResourceItemDefinitionSO>(ItemDefinitionSO.UnifiedResourcePath);
        ProductionRecipeSO recipe = UnityEngine.Object.Instantiate(source);
        ProductionMassExplanationAuthoringSnapshot originalMass =
            recipe.MassExplanation;
        ProductionOutputCostAllocationAuthoringSnapshot originalAllocation =
            recipe.OutputCostAllocation;
        try
        {
            recipe.ConfigureMassExplanation(
                "unknown-mass-policy",
                1,
                "registration-check");
            ExpectInvalidOperation(
                () => CreateCatalog(items, recipe),
                "Resource catalog accepted an unregistered mass policy.");

            RestoreMassExplanation(recipe, originalMass);
            _ = CreateCatalog(items, recipe);

            recipe.ConfigureOutputCostAllocation(
                "unknown-output-cost-policy",
                1,
                "registration-check");
            ExpectInvalidOperation(
                () => CreateCatalog(items, recipe),
                "Resource catalog accepted an unregistered output-cost policy.");

            RestoreOutputCostAllocation(recipe, originalAllocation);
            _ = CreateCatalog(items, recipe);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static ResourceEconomyContentCatalog CreateCatalog(
        ResourceItemDefinitionSO[] items,
        ProductionRecipeSO recipe) => new(
        items,
        new[] { recipe },
        Array.Empty<CropDefinitionSO>(),
        Array.Empty<CraftMaterialDefinitionSO>());

    private static void RestoreMassExplanation(
        ProductionRecipeSO recipe,
        ProductionMassExplanationAuthoringSnapshot snapshot)
    {
        recipe.ConfigureMassExplanation(
            snapshot.CapabilityId,
            snapshot.ContractVersion,
            snapshot.CanonicalPayload);
    }

    private static void RestoreOutputCostAllocation(
        ProductionRecipeSO recipe,
        ProductionOutputCostAllocationAuthoringSnapshot snapshot)
    {
        recipe.ConfigureOutputCostAllocation(
            snapshot.CapabilityId,
            snapshot.ContractVersion,
            snapshot.CanonicalPayload);
    }

    private static void ExpectInvalidOperation(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
