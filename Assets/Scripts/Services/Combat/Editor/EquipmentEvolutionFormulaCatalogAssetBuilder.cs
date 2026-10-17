#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class EquipmentEvolutionFormulaCatalogAssetBuilder
{
    public const string AssetPath =
        "Assets/Resources/SO/EquipmentEvolutionFormulaCatalog.asset";
    public const string LegacyV3AssetPath =
        "Assets/Resources/SO/EquipmentEvolutionFormulaCatalogV3.asset";

    [MenuItem("Tools/Dungeon Story/Evolution/Author Equipment Formula Catalog")]
    public static void Author()
    {
        EquipmentEvolutionFormulaCatalogSO catalog =
            AssetDatabase.LoadAssetAtPath<EquipmentEvolutionFormulaCatalogSO>(AssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<EquipmentEvolutionFormulaCatalogSO>();
            AssetDatabase.CreateAsset(catalog, AssetPath);
        }

        catalog.formulaPolicy = new EquipmentEvolutionFormulaPolicyDefinition
        {
            formulaVersion = EquipmentEvolutionRules.CurrentFormulaVersion,
            drawbackCredit = new NarrativeFormulaDrawbackCreditPolicyDefinition
            {
                playerChoiceMaximumBudgetFraction = 0.5f,
                automaticMaximumBudgetFraction = 0.35f,
                absoluteMaximumCredit = 3,
                requireNegativeEvidenceForAutomatic = true
            }
        };
        catalog.milestoneThresholds = new List<int> { 1, 5, 10 };
        catalog.ordinaryImportance = 0.5d;
        catalog.historicalImportance = 2d;
        catalog.capabilities = new List<EquipmentEvolutionFormulaCapabilityDefinition>
        {
            Capability("equipment:force", "위력", "combat.damage", "equipment:combat-damage:increase"),
            Capability("equipment:precision", "정밀", "combat.accuracy", "equipment:combat-accuracy:increase"),
            Capability("equipment:cadence", "속도", "combat.reload", "equipment:combat-reload:reduction", EquipmentEvolutionFormulaModifierKind.MultiplierReduction),
            Capability("equipment:control", "제압", "combat.accuracy", "equipment:combat-accuracy:increase"),
            Capability("equipment:execution", "처형", "combat.damage", "equipment:combat-damage:increase"),
            Capability("equipment:durability", "내구", "combat.durability", "equipment:combat-durability:increase"),
            Capability("equipment:reinforced-durability", "보강 내구", "combat.durability", "equipment:combat-durability:increase")
        };
        catalog.catalogModuleIds = CatalogModuleIds(includePureDurability: true);
        catalog.formulaPolicy.catalogSha256 = catalog.ComputeCanonicalSha256();
        catalog.RequirePolicy();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("Equipment formula catalog authored and validated: "
            + catalog.formulaPolicy.catalogSha256);
    }

    [MenuItem("Tools/Dungeon Story/Evolution/Validate Equipment Formula Catalog")]
    public static void Validate()
    {
        ValidateCatalog(AssetPath, EquipmentEvolutionRules.CurrentFormulaVersion);
        ValidateCatalog(
            LegacyV3AssetPath,
            EquipmentEvolutionRules.DrawbackModuleSelectionFormulaVersion);
    }

    private static void ValidateCatalog(string assetPath, int expectedFormulaVersion)
    {
        EquipmentEvolutionFormulaCatalogSO catalog =
            AssetDatabase.LoadAssetAtPath<EquipmentEvolutionFormulaCatalogSO>(assetPath)
            ?? throw new InvalidOperationException(
                $"Equipment formula catalog asset is missing: {assetPath}");
        catalog.RequirePolicy();
        if (catalog.formulaPolicy.formulaVersion != expectedFormulaVersion)
            throw new InvalidOperationException(
                $"Equipment formula catalog '{assetPath}' has unexpected formula version.");
        string expected = catalog.ComputeCanonicalSha256();
        if (!string.Equals(expected, catalog.formulaPolicy.RequireCatalogSha256(), StringComparison.Ordinal))
            throw new InvalidOperationException("Equipment formula catalog SHA-256 does not match its authored content.");
        Debug.Log("Equipment formula catalog is valid: " + assetPath + " / " + expected);
    }

    private static List<string> CatalogModuleIds(bool includePureDurability)
    {
        List<string> result = new()
        {
            "equipment:cadence",
            "equipment:control",
            "equipment:drawback-blunted",
            "equipment:drawback-devalued",
            "equipment:drawback-fragile",
            "equipment:drawback-heavy",
            "equipment:drawback-inaccurate",
            "equipment:drawback-poor-penetration",
            "equipment:drawback-slow-reload",
            "equipment:durability",
            "equipment:execution",
            "equipment:force",
            "equipment:guard",
            "equipment:melee",
            "equipment:penetration",
            "equipment:precision",
            "equipment:ranged",
            "equipment:risky",
            "equipment:survivor"
        };
        if (includePureDurability)
            result.Add("equipment:reinforced-durability");
        return result;
    }

    private static EquipmentEvolutionFormulaCapabilityDefinition Capability(
        string capabilityId,
        string displayName,
        string statId,
        string applicatorId,
        EquipmentEvolutionFormulaModifierKind modifierKind =
            EquipmentEvolutionFormulaModifierKind.MultiplierIncrease) => new()
    {
        capabilityId = capabilityId,
        displayName = displayName,
        statId = statId,
        modifierKind = modifierKind,
        narrativeAffinity = 1f,
        affinityKeys = new List<string> { capabilityId },
        conflictGroups = new List<string> { "equipment-primary" },
        appliedParameterIds = new List<string> { NarrativeFormulaParameterIds.Magnitude },
        formatterId = capabilityId,
        applicatorId = applicatorId,
        parameterRanges = new List<EquipmentEvolutionFormulaRangeDefinition>
        {
            Range(NarrativeFormulaParameterIds.Magnitude, 400, 1600, 100, 4, 1),
            Range(NarrativeFormulaParameterIds.Duration, 0, 0, 1, 0, 1),
            Range(NarrativeFormulaParameterIds.Count, 1, 1, 1, 0, 1),
            Range(NarrativeFormulaParameterIds.TargetCount, 1, 1, 1, 0, 1)
        }
    };

    private static EquipmentEvolutionFormulaRangeDefinition Range(
        string id, long minimum, long maximum, long quantum, int decimals, int cost) => new()
    {
        parameterId = id,
        minimumUnits = minimum,
        maximumUnits = maximum,
        quantumUnits = quantum,
        decimalPlaces = decimals,
        costPerQuantum = cost
    };
}
#endif
