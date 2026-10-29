#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionMassExplanationCapabilityDebugScenarios
{
    [MenuItem("DungeonStory/V27/Physical Mass/Verify Process Loss Capability")]
    public static void RunFromMenu()
    {
        Verify();
        Debug.Log("V27_PROCESS_MASS_EXPLANATION_CAPABILITIES=PASS");
    }

    public static void Verify()
    {
        string payload = ProcessLossProductionMassExplanationCapability
            .BuildPayload(
                PhysicalMassLossKind.CuttingWaste,
                "cutting-dust-or-offcut");
        ProductionMassExplanationAuthoringSnapshot authoring = new(
            ProcessLossProductionMassExplanationCapability.Id,
            ProcessLossProductionMassExplanationCapability.Version,
            payload);
        ProductionMassExplanationCapabilityRegistry registry =
            ProductionMassExplanationCapabilityRegistry.CreateDefault();
        ProductionMassExplanationDisposition first = registry.Resolve(
            authoring,
            new ProductionMassExplanationEquationSubject(
                "recipe:canary-a",
                3_600L,
                0L,
                0L,
                3_300L,
                0L,
                0L));
        ProductionMassExplanationDisposition repeated = registry.Resolve(
            authoring,
            new ProductionMassExplanationEquationSubject(
                "recipe:canary-a",
                3_600L,
                0L,
                0L,
                3_300L,
                0L,
                0L));
        Require(first.DeclaredLossGrams == 300L
            && first.LossKind == PhysicalMassLossKind.CuttingWaste
            && first.ReasonCode == "cutting-dust-or-offcut"
            && first.CanonicalReceiptPayload.Contains(
                "equation=",
                StringComparison.Ordinal)
            && string.Equals(
                first.Fingerprint,
                repeated.Fingerprint,
                StringComparison.Ordinal),
            "Process-loss canary did not resolve deterministically.");

        ProductionMassExplanationDisposition differentRecipe = registry.Resolve(
            authoring,
            new ProductionMassExplanationEquationSubject(
                "recipe:canary-b",
                3_600L,
                0L,
                0L,
                3_300L,
                0L,
                0L));
        Require(differentRecipe.DeclaredLossGrams == 300L,
            "Process-loss capability depends on a recipe-specific code branch.");

        ExpectFailure(() => registry.Resolve(
            authoring,
            new ProductionMassExplanationEquationSubject(
                "recipe:canary-a",
                3_600L,
                0L,
                0L,
                3_601L,
                0L,
                0L)), "equation drift");
        ExpectFailure(() => registry.Resolve(
            new ProductionMassExplanationAuthoringSnapshot(
                "unknown-capability",
                1,
                payload),
            new ProductionMassExplanationEquationSubject(
                "recipe:canary-a", 3_600L, 0L, 0L, 3_300L, 0L, 0L)),
            "unknown capability");
        ExpectFailure(() => new ProductionMassExplanationCapabilityRegistry(
            new IProductionMassExplanationCapability[]
            {
                new ProcessLossProductionMassExplanationCapability(),
                new ProcessLossProductionMassExplanationCapability()
            }), "duplicate capability");
        ExpectFailure(() => registry.Resolve(
            new ProductionMassExplanationAuthoringSnapshot(
                ProcessLossProductionMassExplanationCapability.Id,
                ProcessLossProductionMassExplanationCapability.Version,
                payload.Replace("mode=residual", "mode=fixed")),
            new ProductionMassExplanationEquationSubject(
                "recipe:canary-a", 3_600L, 0L, 0L, 3_300L, 0L, 0L)),
            "noncanonical payload");

        string additionPayload =
            ProcessAdditionProductionMassExplanationCapability.BuildPayload(
                PhysicalMassExternalInputKind.AbstractProcessAddition,
                "game-unit-abstraction-addition");
        ProductionMassExplanationAuthoringSnapshot additionAuthoring = new(
            ProcessAdditionProductionMassExplanationCapability.Id,
            ProcessAdditionProductionMassExplanationCapability.Version,
            additionPayload);
        ProductionMassExplanationDisposition addition = registry.Resolve(
            additionAuthoring,
            new ProductionMassExplanationEquationSubject(
                "recipe:canary-addition",
                800L,
                0L,
                0L,
                1_000L,
                0L,
                0L));
        Require(addition.DeclaredExternalInputGrams == 200L
            && addition.ExternalInputKind
                == PhysicalMassExternalInputKind.AbstractProcessAddition
            && addition.DeclaredLossGrams == 0L
            && addition.LossKind == PhysicalMassLossKind.None
            && addition.ReasonCode == "game-unit-abstraction-addition",
            "Process-addition canary did not resolve the exact residual.");
        ExpectFailure(() => registry.Resolve(
            additionAuthoring,
            new ProductionMassExplanationEquationSubject(
                "recipe:canary-addition",
                1_000L,
                0L,
                0L,
                800L,
                0L,
                0L)),
            "negative process addition");
    }

    public static void RunR30RegistryConsistencyFocused()
    {
        ProductionMassExplanationCapabilityRegistry sharedRegistry =
            new(new IProductionMassExplanationCapability[]
            {
                new ProcessAdditionProductionMassExplanationCapability(),
                new ProcessLossProductionMassExplanationCapability(),
                new R30CustomProcessLossCapability()
            });
        ProductionMassExplanationCapabilityRegistry defaultRegistry =
            ProductionMassExplanationCapabilityRegistry.CreateDefault();
        ProductionRecipeSO source = Resources
            .LoadAll<ProductionRecipeSO>(ProductionRecipeSO.ResourcePath)
            .OrderBy(value => value?.RecipeId, StringComparer.Ordinal)
            .FirstOrDefault(value => value != null)
            ?? throw new InvalidOperationException(
                "R30 registry fixture requires one production recipe.");
        ProductionMassExplanationAuthoringSnapshot sourceBefore =
            source.MassExplanation;
        ProductionRecipeSO recipe = UnityEngine.Object.Instantiate(source);
        try
        {
            string payload = ProcessLossProductionMassExplanationCapability
                .BuildPayload(
                    PhysicalMassLossKind.CuttingWaste,
                    "r30-custom-registry-loss");
            recipe.ConfigureMassExplanation(
                R30CustomProcessLossCapability.Id,
                R30CustomProcessLossCapability.Version,
                payload);
            ProductionMassExplanationAuthoringSnapshot authoring =
                recipe.MassExplanation;
            sharedRegistry.ValidateRegistered(authoring);

            ScriptableObject[] roots = { recipe };
            ContentReferenceCatalog references = new(roots, sharedRegistry);
            string customKey = R30CustomProcessLossCapability.Id + "@"
                + R30CustomProcessLossCapability.Version;
            Require(references.Entries(new ContentReferenceAttribute(
                    ContentReferenceKind.ProductionMassPolicy))
                .Any(entry => string.Equals(
                    entry.Id,
                    customKey,
                    StringComparison.Ordinal)),
                "Content-reference candidates omitted the shared custom mass policy.");
            IReadOnlyList<string> acceptedReferenceErrors = references.Validate(roots);
            Require(!acceptedReferenceErrors.Any(error => error.Contains(
                    "Unknown production mass explanation capability: " + customKey,
                    StringComparison.Ordinal)),
                "Content-reference validation rejected the shared custom mass policy.");

            IReadOnlyList<string> defaultReferenceErrors =
                new ContentReferenceCatalog(roots, defaultRegistry).Validate(roots);
            Require(defaultReferenceErrors.Any(error => error.Contains(
                    "Unknown production mass explanation capability: " + customKey,
                    StringComparison.Ordinal)),
                "Default content-reference validation accepted the custom mass policy.");

            ResourceItemDefinitionSO[] items = Resources
                .LoadAll<ResourceItemDefinitionSO>(
                    ItemDefinitionSO.UnifiedResourcePath);
            ResourceEconomyContentCatalog economy = new(
                items,
                new[] { recipe },
                Array.Empty<CropDefinitionSO>(),
                Array.Empty<CraftMaterialDefinitionSO>(),
                sharedRegistry);
            Require(economy != null,
                "Shared registry did not construct the resource-economy catalog.");
            ExpectFailure(() => new ResourceEconomyContentCatalog(
                    items,
                    new[] { recipe },
                    Array.Empty<CropDefinitionSO>(),
                    Array.Empty<CraftMaterialDefinitionSO>(),
                    defaultRegistry),
                "Default resource-economy catalog accepted the custom mass policy.");

            ProductionMassExplanationDisposition disposition = sharedRegistry.Resolve(
                authoring,
                new ProductionMassExplanationEquationSubject(
                    recipe.RecipeId,
                    3_600L,
                    0L,
                    0L,
                    3_300L,
                    0L,
                    0L));
            Require(disposition.DeclaredLossGrams == 300L
                && disposition.LossKind == PhysicalMassLossKind.CuttingWaste,
                "Shared registry did not resolve the custom policy through the production-loss contract.");
            ExpectFailure(() => defaultRegistry.ValidateRegistered(authoring),
                "Default registry accepted the custom mass policy.");
            ExpectFailure(() => new ProductionMassExplanationCapabilityRegistry(
                    new IProductionMassExplanationCapability[]
                    {
                        new ProcessAdditionProductionMassExplanationCapability(),
                        new ProcessLossProductionMassExplanationCapability(),
                        new R30CustomProcessLossCapability(),
                        new R30CustomProcessLossCapability()
                    }),
                "Registry accepted a duplicate custom mass policy.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
        }
        Require(SameAuthoring(sourceBefore, source.MassExplanation),
            "R30 registry fixture changed the source recipe authoring.");
    }

    private static bool SameAuthoring(
        ProductionMassExplanationAuthoringSnapshot left,
        ProductionMassExplanationAuthoringSnapshot right) =>
        string.Equals(left.CapabilityId, right.CapabilityId,
            StringComparison.Ordinal)
        && left.ContractVersion == right.ContractVersion
        && string.Equals(left.CanonicalPayload, right.CanonicalPayload,
            StringComparison.Ordinal);

    private sealed class R30CustomProcessLossCapability :
        IProductionMassExplanationCapability
    {
        public const string Id = "r30-custom-process-loss";
        public const int Version = 1;

        public string CapabilityId => Id;
        public int ContractVersion => Version;

        public ProductionMassExplanationDisposition Resolve(
            ProductionMassExplanationAuthoringSnapshot authoring,
            ProductionMassExplanationEquationSubject subject)
        {
            if (!string.Equals(authoring.CapabilityId, Id, StringComparison.Ordinal)
                || authoring.ContractVersion != Version)
            {
                throw new InvalidOperationException(
                    "R30 custom mass policy metadata drifted.");
            }
            return new ProcessLossProductionMassExplanationCapability().Resolve(
                new ProductionMassExplanationAuthoringSnapshot(
                    ProcessLossProductionMassExplanationCapability.Id,
                    ProcessLossProductionMassExplanationCapability.Version,
                    authoring.CanonicalPayload),
                subject);
        }
    }

    private static void ExpectFailure(Action action, string label)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            return;
        }
        throw new InvalidOperationException(
            "Process-loss scenario accepted " + label + ".");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
