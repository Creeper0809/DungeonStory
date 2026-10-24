#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class EvolutionFormulaForbiddenSynergyReferenceDebugScenarios
{
    private const string EquipmentCatalogPath =
        "Assets/Resources/SO/EquipmentEvolutionFormulaCatalog.asset";
    private const string FacilityRecipePath =
        "Assets/Resources/SO/FacilityEvolution/P1/EV_AlchemyBench.asset";
    private const string ForeignFacilityRecipePath =
        "Assets/Resources/SO/FacilityEvolution/P1/EV_ArcheryTarget.asset";

    [MenuItem("DungeonStory/V27/Content/Validate Formula Forbidden-Synergy References")]
    public static void Validate()
    {
        VerifyEquipmentReferencesAndSelectionConflict();
        VerifyFacilityReferencesAndRetry();
        Debug.Log("[EvolutionFormulaForbiddenSynergyReference] focused scenarios passed.");
    }

    private static void VerifyEquipmentReferencesAndSelectionConflict()
    {
        EquipmentEvolutionFormulaCatalogSO source = AssetDatabase.LoadAssetAtPath<
            EquipmentEvolutionFormulaCatalogSO>(EquipmentCatalogPath);
        Require(source != null, "The authored equipment formula catalog is missing.");
        EquipmentEvolutionFormulaCatalogSO catalog = UnityEngine.Object.Instantiate(source);
        try
        {
            string[] positiveIds = catalog.capabilities
                .Where(value => value != null)
                .Select(value => value.capabilityId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            Require(positiveIds.Length >= 2,
                "The equipment fixture requires two authored positive capabilities.");
            string[] referenceIds = catalog.ReferenceCapabilityIds().ToArray();
            Require(positiveIds.All(value => referenceIds.Contains(value,
                        StringComparer.Ordinal)),
                "The equipment reference list omitted an authored positive capability.");
            string drawbackId = referenceIds.FirstOrDefault(value =>
                !positiveIds.Contains(value, StringComparer.Ordinal));
            Require(!string.IsNullOrEmpty(drawbackId),
                "The current equipment formula generation exposes no optional drawback reference.");

            EquipmentEvolutionFormulaCapabilityDefinition definition =
                catalog.RequireCapability(positiveIds[0]);
            definition.forbiddenSynergies = new List<string> { positiveIds[1] };
            catalog.RequireCatalog();

            definition.forbiddenSynergies = new List<string> { drawbackId };
            catalog.RequireCatalog();
            NarrativeFormulaCapabilityDescriptor positiveDescriptor = definition.ToRuntime();

            const string unknown = "facility:drawback-research-drag";
            definition.forbiddenSynergies = new List<string> { unknown };
            string[] rejectedValue = definition.forbiddenSynergies.ToArray();
            ExpectInvalidOperation(
                () => catalog.RequireCatalog(),
                "A foreign equipment forbidden-synergy target was accepted.");
            Require(definition.forbiddenSynergies.SequenceEqual(
                    rejectedValue, StringComparer.Ordinal),
                "Rejected equipment validation mutated the authored value.");

            definition.forbiddenSynergies = new List<string> { drawbackId };
            catalog.RequireCatalog();
            VerifySelectionConflict(positiveDescriptor, definition.capabilityId, drawbackId);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(catalog);
        }
    }

    private static void VerifySelectionConflict(
        NarrativeFormulaCapabilityDescriptor positiveDescriptor,
        string positiveModuleId,
        string drawbackModuleId)
    {
        EvolutionModuleRegistry registry = new();
        Require(registry.TryGet(drawbackModuleId,
                out EvolutionModuleDefinition drawback),
            "The referenced equipment drawback is not registered.");
        NarrativeFormulaCapabilityDescriptor drawbackDescriptor =
            EvolutionModuleFormulaDescriptor.ForOptionalDrawback(
                drawback, "fixture-equipment-drawback", Array.Empty<string>());
        NarrativeFormulaModuleSelectionRequest request = new(
            "module-selection:forbidden-synergy-fixture",
            new[]
            {
                new NarrativeFormulaModuleOffer(
                    positiveModuleId,
                    NarrativeFormulaModulePolarity.Positive,
                    "fixture positive",
                    new NarrativeFormulaCapabilityInput(positiveDescriptor, 1d, 1d),
                    0),
                new NarrativeFormulaModuleOffer(
                    drawbackModuleId,
                    NarrativeFormulaModulePolarity.Drawback,
                    "fixture drawback",
                    new NarrativeFormulaCapabilityInput(drawbackDescriptor, 1d, 1d),
                    1)
            },
            new[] { "fact:forbidden-synergy-fixture" },
            maximumPositiveModules: 1,
            maximumDrawbackModules: 1);
        bool accepted = NarrativeFormulaModuleSelectionValidator.TryValidate(
            request,
            new NarrativeFormulaModuleSelectionChoice(
                request.SelectionId,
                new[] { positiveModuleId },
                new[] { drawbackModuleId },
                request.EvidenceFactIds),
            out _,
            out string error);
        Require(!accepted && error.Contains("conflict", StringComparison.OrdinalIgnoreCase),
            "The runtime module-selection validator accepted a forbidden synergy.");
    }

    private static void VerifyFacilityReferencesAndRetry()
    {
        FacilityEvolutionRecipeSO source = AssetDatabase.LoadAssetAtPath<
            FacilityEvolutionRecipeSO>(FacilityRecipePath);
        FacilityEvolutionRecipeSO foreignSource = AssetDatabase.LoadAssetAtPath<
            FacilityEvolutionRecipeSO>(ForeignFacilityRecipePath);
        Require(source != null && foreignSource != null,
            "The authored facility formula fixture recipes are missing.");
        FacilityEvolutionRecipeSO recipe = UnityEngine.Object.Instantiate(source);
        try
        {
            FacilityEvolutionFormulaCapabilityDefinition definition =
                recipe.formulaCapabilities.Single(value => value != null);
            string[] referenceIds = recipe.ReferenceCapabilityIds().ToArray();
            Require(referenceIds.Contains(definition.capabilityId,
                    StringComparer.Ordinal),
                "The facility reference list omitted its authored positive capability.");
            string drawbackId = referenceIds.FirstOrDefault(value =>
                value.StartsWith("facility:drawback-", StringComparison.Ordinal));
            Require(!string.IsNullOrEmpty(drawbackId),
                "The facility target exposes no applicable optional drawback reference.");

            definition.forbiddenSynergies = new List<string> { drawbackId };
            recipe.RequireFormulaCapabilities();
            recipe.RequireFormulaCapability();

            string foreignCapabilityId = foreignSource.formulaCapabilities
                .Single(value => value != null).capabilityId;
            Require(!referenceIds.Contains(foreignCapabilityId,
                    StringComparer.Ordinal),
                "The facility reference list leaked a capability from another recipe.");
            definition.forbiddenSynergies = new List<string> { foreignCapabilityId };
            string[] rejectedValue = definition.forbiddenSynergies.ToArray();
            ExpectInvalidOperation(
                () => recipe.RequireFormulaCapabilities(),
                "A foreign facility forbidden-synergy target was accepted by the plural getter.");
            ExpectInvalidOperation(
                () => recipe.RequireFormulaCapability(),
                "A foreign facility forbidden-synergy target was accepted by the singular getter.");
            Require(definition.forbiddenSynergies.SequenceEqual(
                    rejectedValue, StringComparer.Ordinal),
                "Rejected facility validation mutated the authored value.");

            definition.forbiddenSynergies = new List<string> { drawbackId };
            recipe.RequireFormulaCapabilities();
            recipe.RequireFormulaCapability();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
        }
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
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
