using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CombatEquipmentMaterialDebugScenarios
{
    private const string ReportPath =
        "docs/implementation-reports/combat-equipment-material-latest.txt";

    [MenuItem("DungeonStory/Debug/Combat/Run Material Equipment Contracts")]
    public static void RunAll()
    {
        List<string> report = new List<string>
        {
            "DungeonStory material equipment contracts"
        };
        List<string> failures = new List<string>();
        Run("catalog_and_allowed_families", VerifyCatalogAndFamilies, report, failures);
        Run("derived_stats", VerifyDerivedStats, report, failures);
        Run("material_policy", VerifyMaterialPolicy, report, failures);
        Run(
            "facility_mutation_fence",
            VerifyFacilityMutationFence,
            report,
            failures);
        Run(
            "craft_output_capability_binding",
            VerifyCraftOutputCapabilityBinding,
            report,
            failures);
        Run("save_round_trip", VerifySaveRoundTrip, report, failures);
        report.Add($"valid={failures.Count == 0}");
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "docs");
        File.WriteAllLines(ReportPath, report);

        if (failures.Count == 0)
        {
            Debug.Log($"Material equipment contracts passed. Report: {ReportPath}");
            return;
        }

        Debug.LogError(
            $"Material equipment contracts failed: {string.Join(" | ", failures)}");
    }

    private static string VerifyCatalogAndFamilies()
    {
        (ResourceCombatEquipmentCatalog equipmentCatalog,
            ResourceEconomyContentCatalog materialCatalog) = CreateCatalogs();
        CombatEquipmentRuntime runtime = CombatEquipmentEditorTestFactory.Create(
            equipmentCatalog,
            new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore()),
            new CharacterCarryInventoryRegistry(),
            materialCatalog,
            researchProvider: EditorAllResearchRuntimeProvider.Instance, evolutionModules: EmptyEvolutionModuleRegistry.Instance, moduleCatalog: EmptyEquipmentModuleCatalog.Instance, itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);

        Require(equipmentCatalog.All.Count >= 19, "initial equipment catalog is incomplete");
        Require(materialCatalog.Materials.Count == 12, "material catalog must contain 12 materials");
        Require(
            runtime.GetAllowedMaterials("weapon:longsword")
                .Any(material => material.MaterialId == "material:iron"),
            "longsword must allow iron");
        Require(
            runtime.GetAllowedMaterials("weapon:longsword")
                .Any(material => material.MaterialId == "material:bone"),
            "longsword must allow bone");
        Require(
            runtime.GetAllowedMaterials("weapon:longsword")
                .All(material => material.MaterialId != "material:wood"),
            "longsword must reject wood");
        Require(
            runtime.GetAllowedMaterials("weapon:shortbow")
                .Any(material => material.MaterialId == "material:wood"),
            "shortbow must allow wood");
        Require(
            runtime.GetAllowedMaterials("armor:breastplate")
                .All(material => material.Family == CombatMaterialFamily.Metal),
            "plate armor must only allow metal");
        return $"equipment={equipmentCatalog.All.Count}; materials={materialCatalog.Materials.Count}";
    }

    private static string VerifyDerivedStats()
    {
        (ResourceCombatEquipmentCatalog equipmentCatalog,
            ResourceEconomyContentCatalog materialCatalog) = CreateCatalogs();
        CombatEquipmentRuntime runtime = CombatEquipmentEditorTestFactory.Create(
            equipmentCatalog,
            new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore()),
            new CharacterCarryInventoryRegistry(),
            materialCatalog,
            researchProvider: EditorAllResearchRuntimeProvider.Instance, evolutionModules: EmptyEvolutionModuleRegistry.Instance, moduleCatalog: EmptyEquipmentModuleCatalog.Instance, itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
        CombatEquipmentInstance iron = runtime.CreateInstance(
            "weapon:longsword",
            CombatEquipmentQuality.Normal,
            CombatEquipmentWorldState.Stored,
            "material:iron");
        CombatEquipmentInstance blacksteel = runtime.CreateInstance(
            "weapon:longsword",
            CombatEquipmentQuality.Normal,
            CombatEquipmentWorldState.Stored,
            "material:blacksteel");
        CombatEquipmentInstance gold = runtime.CreateInstance(
            "weapon:longsword",
            CombatEquipmentQuality.Normal,
            CombatEquipmentWorldState.Stored,
            "material:gold");

        Require(runtime.TryGetDerivedStats(iron.instanceId, out CombatEquipmentDerivedStats ironStats),
            "iron stats missing");
        Require(runtime.TryGetDerivedStats(blacksteel.instanceId, out CombatEquipmentDerivedStats blacksteelStats),
            "blacksteel stats missing");
        Require(runtime.TryGetDerivedStats(gold.instanceId, out CombatEquipmentDerivedStats goldStats),
            "gold stats missing");
        Require(Approximately(blacksteelStats.DamageMultiplier, 1.2f * 0.88f),
            "blacksteel damage multiplier mismatch");
        Require(Approximately(
                blacksteelStats.PenetrationDefenseMultiplier,
                1.3f * 0.88f),
            "blacksteel penetration multiplier mismatch");
        Require(blacksteelStats.MaxDurability > ironStats.MaxDurability,
            "blacksteel durability must exceed iron");
        Require(goldStats.Weight > ironStats.Weight,
            "gold equipment must be heavier than iron");
        Require(goldStats.ValueMultiplier > blacksteelStats.ValueMultiplier,
            "gold value must exceed blacksteel");
        Require(blacksteelStats.DisplayName.Contains("흑강", StringComparison.Ordinal),
            "derived display name must contain material");
        return $"ironWeight={ironStats.Weight:0.##}; blacksteelDurability={blacksteelStats.MaxDurability:0.##}; goldValue={goldStats.ValueMultiplier:0.##}";
    }

    private static string VerifyMaterialPolicy()
    {
        (ResourceCombatEquipmentCatalog equipmentCatalog,
            ResourceEconomyContentCatalog materialCatalog) = CreateCatalogs();
        CombatEquipmentRuntime runtime = CombatEquipmentEditorTestFactory.Create(
            equipmentCatalog,
            new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore()),
            new CharacterCarryInventoryRegistry(),
            materialCatalog,
            researchProvider: EditorAllResearchRuntimeProvider.Instance, evolutionModules: EmptyEvolutionModuleRegistry.Instance, moduleCatalog: EmptyEquipmentModuleCatalog.Instance, itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
        GameObject facilityObject = new GameObject("MaterialPolicyFacility");
        BuildingSO facilityData = CreateFacilityData(88001);
        try
        {
            BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(facilityData, new Vector2Int(4, 7));
            CombatEquipmentCraftMaterialPolicySaveData policy =
                runtime.GetCraftMaterialPolicy("weapon:longsword", facility);
            Require(policy.allowedMaterialIds.Contains("material:iron"),
                "ordinary material must be allowed by default");
            Require(!policy.allowedMaterialIds.Contains("material:blacksteel"),
                "rare material must be excluded by default");
            Require(runtime.SetCraftMaterialAllowed(
                    "weapon:longsword",
                    "material:blacksteel",
                    facility,
                    true,
                    out string allowFailure),
                $"failed to allow blacksteel: {allowFailure}");
            Require(runtime.MoveCraftMaterialPriority(
                    "weapon:longsword",
                    "material:blacksteel",
                    facility,
                    -1,
                    out string priorityFailure),
                $"failed to move blacksteel priority: {priorityFailure}");

            CombatEquipmentCraftMaterialPolicySaveData changed =
                runtime.GetCraftMaterialPolicy("weapon:longsword", facility);
            Require(changed.allowedMaterialIds.Contains("material:blacksteel"),
                "allowed material change was not retained");
            int blacksteelIndex = changed.priorityMaterialIds.IndexOf("material:blacksteel");
            Require(blacksteelIndex >= 0
                    && blacksteelIndex < policy.priorityMaterialIds.IndexOf("material:blacksteel"),
                "priority move did not change order");
            return $"allowed={changed.allowedMaterialIds.Count}; blacksteelPriority={blacksteelIndex + 1}";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(facilityObject);
            UnityEngine.Object.DestroyImmediate(facilityData);
        }
    }

    private static string VerifyFacilityMutationFence()
    {
        (ResourceCombatEquipmentCatalog equipmentCatalog,
            ResourceEconomyContentCatalog materialCatalog) = CreateCatalogs();
        ProductionFacilityMutationEpochRuntime mutations = new();
        CombatEquipmentRuntime runtime = CombatEquipmentEditorTestFactory.Create(
            equipmentCatalog,
            new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore()),
            new CharacterCarryInventoryRegistry(),
            materialCatalog,
            researchProvider: EditorAllResearchRuntimeProvider.Instance,
            evolutionModules: EmptyEvolutionModuleRegistry.Instance,
            moduleCatalog: EmptyEquipmentModuleCatalog.Instance,
            itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance,
            facilityMutations: mutations);
        BuildingSO facilityData = Resources.LoadAll<BuildingSO>(string.Empty)
            .Where(value => value != null)
            .First(value => value.GetAbility<BuildingEquipmentCraftingAbility>()?
                .CraftableEquipmentIds.Contains(
                    "weapon:longsword",
                    StringComparer.Ordinal) == true);
        GameObject facilityObject = new("CombatMutationFenceFacility");
        long epoch = 0L;
        BuildingInstanceId facilityId = default;
        try
        {
            BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(facilityData, new Vector2Int(13, 9));
            facilityId = facility.RequirePersistentInstanceId();
            Require(mutations.TryBegin(
                    facilityId,
                    "qa:combat-craft-mutation",
                    out epoch,
                    out string beginFailure),
                "combat mutation fence could not open: " + beginFailure);
            Require(!runtime.TryQueueCraft(
                    "weapon:longsword",
                    "material:iron",
                    facility,
                    out string frozenFailure)
                && frozenFailure.Contains(
                    "production-facility-mutation-open",
                    StringComparison.Ordinal),
                "combat queue was not rejected by the exact mutation fence: "
                + frozenFailure);
            Require(mutations.TryEnd(
                    facilityId,
                    "qa:combat-craft-mutation",
                    epoch,
                    out string endFailure),
                "combat mutation fence could not close: " + endFailure);
            epoch = 0L;
            runtime.TryQueueCraft(
                "weapon:longsword",
                "material:iron",
                facility,
                out string reopenedFailure);
            Require(!reopenedFailure.Contains(
                    "production-facility-mutation-open",
                    StringComparison.Ordinal),
                "combat queue remained mutation-blocked after close");
            return "openRejected=1; closeReopened=1";
        }
        finally
        {
            if (epoch > 0L)
            {
                mutations.TryEnd(
                    facilityId,
                    "qa:combat-craft-mutation",
                    epoch,
                    out _);
            }
            UnityEngine.Object.DestroyImmediate(facilityObject);
        }
    }

    private static string VerifySaveRoundTrip()
    {
        (ResourceCombatEquipmentCatalog equipmentCatalog,
            ResourceEconomyContentCatalog materialCatalog) = CreateCatalogs();
        WorldItemRepository itemRepository =
            new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
        CombatEquipmentRuntime source = CombatEquipmentEditorTestFactory.Create(
            equipmentCatalog,
            itemRepository,
            new CharacterCarryInventoryRegistry(),
            materialCatalog,
            researchProvider: EditorAllResearchRuntimeProvider.Instance, evolutionModules: EmptyEvolutionModuleRegistry.Instance, moduleCatalog: EmptyEquipmentModuleCatalog.Instance, itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
        GameObject facilityObject = new GameObject("MaterialSaveFacility");
        BuildingSO facilityData = CreateFacilityData(88002);
        try
        {
            BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(facilityData, new Vector2Int(6, 9));
            CombatEquipmentInstance created = source.CreateInstance(
                "armor:breastplate",
                CombatEquipmentQuality.Masterwork,
                CombatEquipmentWorldState.Stored,
                "material:blacksteel");
            source.SetCraftMaterialAllowed(
                "armor:breastplate",
                "material:blacksteel",
                facility,
                true,
                out _);
            DungeonCombatEquipmentSaveData save = source.Capture();

            CombatEquipmentRuntime restored = CombatEquipmentEditorTestFactory.Create(
                equipmentCatalog,
                itemRepository,
            new CharacterCarryInventoryRegistry(),
                materialCatalog,
                researchProvider: EditorAllResearchRuntimeProvider.Instance, evolutionModules: EmptyEvolutionModuleRegistry.Instance, moduleCatalog: EmptyEquipmentModuleCatalog.Instance, itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
            restored.PublishRestoreCandidate(
                restored.BuildRestoreCandidate(save));
            Require(restored.TryGetInstance(created.instanceId, out CombatEquipmentInstance instance),
                "restored equipment instance missing");
            Require(instance.materialId == "material:blacksteel",
                "equipment material did not survive save");
            CombatEquipmentCraftMaterialPolicySaveData restoredPolicy =
                restored.GetCraftMaterialPolicy("armor:breastplate", facility);
            Require(restoredPolicy.allowedMaterialIds.Contains("material:blacksteel"),
                "material policy did not survive save");
            return $"instance={instance.instanceId}; material={instance.materialId}; policies={save.craftMaterialPolicies.Count}";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(facilityObject);
            UnityEngine.Object.DestroyImmediate(facilityData);
        }
    }

    private static string VerifyCraftOutputCapabilityBinding()
    {
        (ResourceCombatEquipmentCatalog equipmentCatalog,
            ResourceEconomyContentCatalog materialCatalog) = CreateCatalogs();
        IProductionOutputCapabilityRegistry registry =
            CombatEquipmentEditorTestFactory.CreateOutputCapabilities(
                equipmentCatalog,
                new ResourceItemDefinitionCatalog(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader())));

        ProductionOutputCapabilityDescriptor equipment =
            registry.CaptureDeclaredDescriptor(
                CombatEquipmentCraftOutputCapability.OutputLineId,
                PhysicalItemIds.ForEquipment("weapon:longsword"),
                ProductionOutputCapabilityIds.CombatEquipmentCraft);
        ProductionOutputCapabilityDescriptor ammunition =
            registry.CaptureDeclaredDescriptor(
                CombatAmmunitionCraftOutputCapability.OutputLineId,
                CombatItemDefinitions.ArrowItemId,
                ProductionOutputCapabilityIds.CombatAmmunitionCraft);
        Require(
            registry.TryValidateExact(equipment, out _, out _),
            "combat equipment output capability did not validate exactly");
        Require(
            registry.TryValidateExact(ammunition, out _, out _),
            "combat ammunition output capability did not validate exactly");

        ProductionOutputCapabilityDescriptor drifted = new(
            ammunition.OutputLineId,
            ammunition.ItemId,
            ammunition.CapabilityId,
            ammunition.CapabilityVersion,
            ammunition.ComponentCodecId,
            ammunition.ComponentCodecVersion + 1,
            ammunition.Fingerprint);
        Require(
            !registry.TryValidateExact(drifted, out _, out DomainFailure failure)
                && failure.IsFailure,
            "combat ammunition output capability codec drift was accepted");
        return $"equipment={equipment.CapabilityId}; ammunition={ammunition.CapabilityId}; driftRejected=true";
    }

    private static BuildingSO CreateFacilityData(int id)
    {
        BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
        data.id = id;
        data.objectName = "재질 장비 검증 시설";
        data.width = 1;
        data.height = 1;
        data.layer = GridLayer.Building;
        data.category = BuildingCategory.Crafting;
        return data;
    }

    private static (
        ResourceCombatEquipmentCatalog equipment,
        ResourceEconomyContentCatalog materials) CreateCatalogs()
    {
        ResourceCombatEquipmentCatalog equipment =
            new ResourceCombatEquipmentCatalog(new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        ResourceEconomyContentCatalog materials =
            new ResourceEconomyContentCatalog(
                Resources.LoadAll<ResourceItemDefinitionSO>(
                    ResourceItemDefinitionSO.ResourcePath),
                Resources.LoadAll<ProductionRecipeSO>(
                    ProductionRecipeSO.ResourcePath),
                Resources.LoadAll<CropDefinitionSO>(
                    CropDefinitionSO.ResourcePath),
                Resources.LoadAll<CraftMaterialDefinitionSO>(
                    CraftMaterialDefinitionSO.ResourcePath));
        return (equipment, materials);
    }

    private static bool Approximately(float left, float right)
    {
        return Mathf.Abs(left - right) <= 0.001f;
    }

    private static void Run(
        string name,
        Func<string> test,
        ICollection<string> report,
        ICollection<string> failures)
    {
        try
        {
            report.Add($"{name}=PASS; {test()}");
        }
        catch (Exception exception)
        {
            report.Add($"{name}=FAIL; {exception.Message}");
            failures.Add($"{name}: {exception.Message}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
