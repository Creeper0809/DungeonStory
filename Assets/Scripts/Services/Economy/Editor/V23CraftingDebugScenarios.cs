#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Focused deterministic gate for the V23 worker, quality, material-state and
/// operational-consumer contracts. The full balance audit is invoked last so
/// a passing run also refreshes the generated design appendix.
/// </summary>
public static class V23CraftingDebugScenarios
{
    private const int QualitySampleCount = 100_000;

    [MenuItem("DungeonStory/Debug/Economy/Run V23 Crafting Contracts")]
    public static void RunFromMenu() => RunAll();

    public static void RunAll()
    {
        ValidateWorkerPolicyNormalization();
        ValidateDeterministicQualityAndDistribution();
        ValidateMaterialStateHasNoQuality();
        ValidateTypedOperationalConsumers();
        ValidateEquipmentWorkMigration();

        IReadOnlyList<string> productionFailures =
            BranchedProductionNetworkDebugScenarios.Validate();
        Require(productionFailures.Count == 0,
            "Branched production validation failed:\n"
            + string.Join("\n", productionFailures));

        V23BalanceAudit.Generate();
        Debug.Log(
            "V23 crafting contracts passed: deterministic quality=100000, "
            + "typed runtime consumers=24, material quality=none.");
    }

    private static void ValidateWorkerPolicyNormalization()
    {
        WorkerSelectionPolicySaveData source = new()
        {
            mode = WorkerSelectionMode.SpecificOrRuleSet,
            matchMode = WorkerRequirementMatchMode.All,
            sortMode = WorkerCandidateSortMode.SpecificThenBestExpectedQuality,
            specificCharacterIds = new List<string> { " worker-b ", "worker-a", "worker-a" },
            excludedCharacterIds = new List<string> { "worker-z", "worker-z" },
            statRequirements = new List<WorkerStatRequirementSaveData>
            {
                new() { statType = 2, minimumValue = 4 },
                new() { statType = 2, minimumValue = 8 },
                new() { statType = 1, minimumValue = 6 }
            },
            requiredTraitIds = new List<string> { "trait:b", "trait:a", "trait:a" }
        };

        WorkerSelectionPolicySaveData normalized = source.CloneNormalized();
        Require(normalized.specificCharacterIds.SequenceEqual(
                new[] { "worker-a", "worker-b" }),
            "Specific worker IDs must be trimmed, unique and deterministic.");
        Require(normalized.excludedCharacterIds.SequenceEqual(new[] { "worker-z" }),
            "Excluded worker IDs must be unique.");
        Require(normalized.statRequirements.Count == 2
                && normalized.statRequirements[1].statType == 2
                && normalized.statRequirements[1].minimumValue == 8,
            "Duplicate stat requirements must keep the strictest threshold.");
        Require(normalized.requiredTraitIds.SequenceEqual(
                new[] { "trait:a", "trait:b" }),
            "Trait requirements must be deterministic.");
    }

    private static void ValidateDeterministicQualityAndDistribution()
    {
        DeterministicCraftQualityResolver resolver = new();
        CraftQualityRollSaveData first = resolver.Roll(
            0xC0FFEEUL, "pipeline:save", "equipment:test", 17);
        CraftQualityRollSaveData restored = new QualityTargetPipelineSaveData
        {
            pipelineId = "pipeline:save",
            definitionId = "equipment:test",
            currentRoll = first
        }.CloneNormalized().currentRoll;
        CraftQualityRollSaveData rerun = resolver.Roll(
            0xC0FFEEUL, "pipeline:save", "equipment:test", 17);
        Require(SameRoll(first, restored) && SameRoll(first, rerun),
            "A saved quality attempt must never reroll.");

        double lowScore = 0d;
        double highScore = 0d;
        int[] lowTiers = new int[7];
        int[] highTiers = new int[7];
        for (int attempt = 0; attempt < QualitySampleCount; attempt++)
        {
            CraftQualityRollSaveData roll = resolver.Roll(
                0x5EED23UL, "pipeline:distribution", "apparel:test", attempt);
            CraftQualityResolution low = resolver.Resolve(
                roll, 30f, 0f, 0f, 8f);
            CraftQualityResolution high = resolver.Resolve(
                roll, 70f, 0f, 0f, 8f);
            lowScore += low.Score;
            highScore += high.Score;
            lowTiers[(int)low.Tier]++;
            highTiers[(int)high.Tier]++;
        }

        Require(highScore > lowScore,
            "Higher weighted skill must improve the quality distribution.");
        Require(lowTiers.Count(value => value > 0) >= 3
                && highTiers.Count(value => value > 0) >= 3,
            "Skill may shift quality odds but must not make output deterministic.");
        Require(highTiers[(int)CraftsmanshipQualityTier.Excellent]
                + highTiers[(int)CraftsmanshipQualityTier.Masterwork]
                + highTiers[(int)CraftsmanshipQualityTier.Legendary]
                > lowTiers[(int)CraftsmanshipQualityTier.Excellent]
                + lowTiers[(int)CraftsmanshipQualityTier.Masterwork]
                + lowTiers[(int)CraftsmanshipQualityTier.Legendary],
            "Higher skill must increase upper-tier outcomes.");
    }

    private static void ValidateMaterialStateHasNoQuality()
    {
        ItemInstanceComponentSaveData textile = TextileBatchItemState.Create(
            TextileConditionBand.Wet);
        Require(textile.values.Count == 1
                && textile.values[0].key == "condition-band"
                && textile.values.All(value =>
                    !value.key.Contains("quality", StringComparison.OrdinalIgnoreCase)),
            "Stackable textile state may contain condition, never material quality.");

        ItemInstanceComponentSaveData seed = SeedLotItemStateCodec.Encode(
            new SeedLotState
            {
                cropId = "crop:test",
                cultivarGenomeId = "genome:test",
                generation = 2,
                pathogenLoad = 7f
            });
        Require(seed.values.All(value =>
                !value.key.Contains("quality", StringComparison.OrdinalIgnoreCase)),
            "Seed lots may contain genome and pathogen state, never quality tiers.");
    }

    private static void ValidateTypedOperationalConsumers()
    {
        IReadOnlyList<PhysicalItemRuntimeConsumerCatalog.Link> links =
            PhysicalItemRuntimeConsumerCatalog.All;
        Require(links.Count == 24,
            $"Expected 24 formerly omitted typed consumers, found {links.Count}.");
        Require(links.Select(value => value.ItemId)
                .Distinct(StringComparer.Ordinal).Count() == links.Count,
            "Typed operational consumer item IDs must be unique.");
        Require(links.All(value =>
                !string.IsNullOrWhiteSpace(value.ItemId)
                && value.OwnerId.StartsWith("runtime:", StringComparison.Ordinal)
                && !value.OwnerId.StartsWith("sink:", StringComparison.Ordinal)),
            "Operational consumers must identify real runtime owners, not fake sinks.");
    }

    private static void ValidateEquipmentWorkMigration()
    {
        CombatEquipmentDefinitionSO[] equipment = AssetDatabase.FindAssets(
                "t:CombatEquipmentDefinitionSO",
                new[] { "Assets/Resources/SO/Combat/Equipment" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CombatEquipmentDefinitionSO>)
            .Where(value => value != null)
            .ToArray();
        Require(equipment.Length == 61,
            $"Expected 61 combat equipment definitions, found {equipment.Length}.");
        Require(equipment.All(value => value.RequiredCraftWork > 6.001f),
            "Combat equipment must not retain the old requiredCraftWork=6 placeholder.");
        Require(equipment.Select(value => value.RequiredCraftWork).Distinct().Count() >= 20,
            "Equipment work must vary by form and complexity.");
    }

    private static bool SameRoll(
        CraftQualityRollSaveData left,
        CraftQualityRollSaveData right) => left != null
        && right != null
        && left.attemptIndex == right.attemptIndex
        && left.randomA == right.randomA
        && left.randomB == right.randomB
        && left.randomC == right.randomC;

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
