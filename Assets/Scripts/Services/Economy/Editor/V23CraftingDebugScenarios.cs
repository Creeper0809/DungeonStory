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

    [MenuItem("DungeonStory/Debug/Economy/Verify V23 Runtime Consumer Contracts")]
    public static void RunRuntimeConsumerContractsFromMenu()
    {
        ValidateTypedOperationalConsumers();
        Debug.Log("V23 typed runtime consumer contracts passed: links=61.");
    }

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
            + "typed runtime consumers=53, material quality=none.");
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
            minimumSkillId = BuiltInCharacterProficiencyIds.Crafting.Value,
            minimumSkillExperience = 800,
            requiredTraitIds = new List<string> { "trait:b", "trait:a", "trait:a" }
        };

        WorkerSelectionPolicySaveData normalized = source.CloneNormalized();
        Require(normalized.specificCharacterIds.SequenceEqual(
                new[] { "worker-a", "worker-b" }),
            "Specific worker IDs must be trimmed, unique and deterministic.");
        Require(normalized.excludedCharacterIds.SequenceEqual(new[] { "worker-z" }),
            "Excluded worker IDs must be unique.");
        Require(normalized.minimumSkillId == BuiltInCharacterProficiencyIds.Crafting.Value
                && normalized.minimumSkillExperience == 800,
            "Proficiency-only worker requirement was not preserved.");
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
        Require(links.Count == 61,
            $"Expected 61 typed runtime consumer links, found {links.Count}.");
        Require(links.Select(value => value.ItemId + "\n" + value.OwnerId)
                .Distinct(StringComparer.Ordinal).Count() == links.Count,
            "Typed operational consumer item/owner pairs must be unique.");
        Require(links.All(value =>
                !string.IsNullOrWhiteSpace(value.ItemId)
                && value.OwnerId.StartsWith("runtime:", StringComparison.Ordinal)
                && !value.OwnerId.StartsWith("sink:", StringComparison.Ordinal)),
            "Operational consumers must identify real runtime owners, not fake sinks.");
        string[] expectedEquipmentModuleConsumers =
        {
            "component:material-test-coupon\nruntime:equipment-module-testing",
            DurableToolItemRules.InspectionGauge
                + "\nruntime:equipment-module-inspection",
            DurableToolItemRules.RuneIdentificationLens
                + "\nruntime:rune-module-identification"
        };
        string[] actualEquipmentModuleConsumers = links
            .Where(value => string.Equals(
                    value.ItemId,
                    "component:material-test-coupon",
                    StringComparison.Ordinal)
                || string.Equals(
                    value.ItemId,
                    DurableToolItemRules.InspectionGauge,
                    StringComparison.Ordinal)
                || string.Equals(
                    value.ItemId,
                    DurableToolItemRules.RuneIdentificationLens,
                    StringComparison.Ordinal))
            .Select(value => value.ItemId + "\n" + value.OwnerId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(actualEquipmentModuleConsumers.SequenceEqual(
                expectedEquipmentModuleConsumers,
                StringComparer.Ordinal),
            "Equipment-module appraisal runtime consumer links are incomplete or stale.");
        string[] expectedDiseaseResponseItems =
        {
            "component:reclaimed-water-filter",
            "drug:dreamleaf-analgesic",
            "medical:isolation-care-kit",
            "medicine:antidote",
            "medicine:blood-pack",
            "resource:clean-water",
            "supply:fungicide",
            "supply:pest-lure"
        };
        string[] actualDiseaseResponseItems = links
            .Where(value => string.Equals(
                value.OwnerId,
                "runtime:disease-field-response",
                StringComparison.Ordinal))
            .Select(value => value.ItemId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(actualDiseaseResponseItems.SequenceEqual(
                expectedDiseaseResponseItems,
                StringComparer.Ordinal),
            "Disease field-response physical item links are incomplete or stale.");
        string[] expectedVaccinationItems =
        {
            "medicine:vaccine:blood-wasting",
            "medicine:vaccine:cave-flu",
            "medicine:vaccine:gut-rot",
            "medicine:vaccine:mana-pox",
            "medicine:vaccine:red-fever",
            "medicine:vaccine:slime-blight",
            "medicine:vaccine:spore-lung"
        };
        string[] actualVaccinationItems = links
            .Where(value => string.Equals(
                value.OwnerId,
                "runtime:physical-vaccination",
                StringComparison.Ordinal))
            .Select(value => value.ItemId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(actualVaccinationItems.SequenceEqual(
                expectedVaccinationItems,
                StringComparer.Ordinal),
            "Physical vaccination item links are incomplete or stale.");
        string[] expectedCharacterMedicalItems =
        {
            "captivity:extracted-blood",
            "medical:regenerative-medium",
            "medical:sterile-bandage",
            "medicine:advanced",
            "medicine:antiseptic",
            "medicine:herbal-poultice",
            "medicine:mycelial-culture-pack",
            "medicine:standard"
        };
        string[] actualCharacterMedicalItems = links
            .Where(value => string.Equals(
                value.OwnerId,
                "runtime:character-medical-treatment",
                StringComparison.Ordinal))
            .Select(value => value.ItemId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(actualCharacterMedicalItems.SequenceEqual(
                expectedCharacterMedicalItems,
                StringComparer.Ordinal),
            "Character-medical physical item links are incomplete or stale.");
        string[] expectedOffenseSupplyItems =
        {
            "food:preserved-ration",
            "medicine:blood-seal-kit",
            "medicine:field-emergency-kit",
            "medicine:mana-core-restraint",
            "medicine:mycelial-culture-pack",
            "medicine:rune-slime-patch",
            "medicine:standard",
            "medicine:temporary-power-bypass",
            "medicine:wing-splint-kit",
            "resource:mana-crystal",
            "tool:field-repair-kit"
        };
        string[] actualOffenseSupplyItems = links
            .Where(value => string.Equals(
                value.OwnerId,
                "runtime:offense-supply-package",
                StringComparison.Ordinal))
            .Select(value => value.ItemId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(actualOffenseSupplyItems.SequenceEqual(
                expectedOffenseSupplyItems,
                StringComparer.Ordinal),
            "Offense supply-package physical item links are incomplete or stale.");
        string[] expectedUrgentMitigationItems =
        {
            "material:low-fuel",
            "material:lumber",
            "medicine:standard",
            "resource:mana-crystal"
        };
        string[] actualUrgentMitigationItems = links
            .Where(value => string.Equals(
                value.OwnerId,
                "runtime:offense-urgent-mitigation",
                StringComparison.Ordinal))
            .Select(value => value.ItemId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(actualUrgentMitigationItems.SequenceEqual(
                expectedUrgentMitigationItems,
                StringComparer.Ordinal),
            "Offense urgent-mitigation physical item links are incomplete or stale.");
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
