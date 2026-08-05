#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ResearchEquipmentOverhaulDebugScenarios
{
    private const string LongswordResearchId =
        "research:equipment:weapon-patterns";

    private const string ProjectRoot = "Assets/Resources/SO/Research/Projects";
    private const string FacilityRoot = "Assets/Resources/SO/Building/ResearchOverhaul";
    private const string ItemRoot = "Assets/Resources/SO/Economy/Items/ResearchOverhaul";
    private const string RecipeRoot = "Assets/Resources/SO/Economy/Recipes/ResearchOverhaul";
    private const string ModuleRoot = "Assets/Resources/SO/Combat/EquipmentModules";
    private const string AppraisalFacilityPath =
        FacilityRoot + "/RF42_부품_감정대.asset";
    private const string RestorationFacilityPath =
        FacilityRoot + "/RF43_부품_복원_작업대.asset";
    private const string PrecisionFittingFacilityPath =
        FacilityRoot + "/RF44_정밀_장착대.asset";
    private const string WrongProgressionFacilityPath =
        "Assets/Resources/SO/Building/Modular/S08_대장작업대.asset";
    private const float EffectiveWorkPerDay = 180f * 0.55f;

    private static readonly string[] MedievalQueue =
    {
        "research:agriculture:indoor",
        "research:metallurgy:advanced",
        "research:textile:layered",
        "research:cuisine:livestock",
        "research:defense:tactical-command",
        "research:survival:field-rations",
        "research:medical:surgery"
    };

    private static readonly string[] EarlyIndustrialQueue =
    {
        "research:industry:steam-power",
        "research:industry:distribution",
        "research:industry:factory-layout",
        "research:equipment:black-powder",
        "research:equipment:engineering-drawing",
        "research:industry:powered-tools",
        "research:equipment:ignition-mechanisms",
        "research:equipment:ballistics",
        "research:equipment:standard-ammunition"
    };

    private static readonly string[] MatureIndustrialQueue =
    {
        "research:industry:high-speed-belts",
        "research:industry:precision",
        "research:equipment:precision-fitting",
        "research:industry:industrial-cooling"
    };

    private static readonly string[] LateIndustrialQueue =
    {
        "research:industry:rune-automation",
        "research:industry:dark-foundry",
        "research:plumbing:rune-purification",
        "research:equipment:rune-module-tuning",
        "research:equipment:lineage-binding",
        "research:equipment:powered-armor",
        "research:equipment:industrial-metrology"
    };

    [MenuItem("Tools/DungeonStory/Research/Validate 168 Research Equipment Overhaul")]
    public static void RunFromMenu()
    {
        IReadOnlyList<string> failures = ValidateAll(out string pacingReport);
        if (failures.Count > 0)
        {
            foreach (string failure in failures)
            {
                Debug.LogError($"[168 Research Overhaul] {failure}");
            }
            throw new InvalidOperationException(
                $"168 research/equipment overhaul validation failed ({failures.Count}).");
        }

        Debug.Log($"168 research/equipment overhaul validation passed. {pacingReport}");
    }

    public static IReadOnlyList<string> ValidateAll(out string pacingReport)
    {
        List<string> failures = new List<string>();
        ResearchProjectSO[] projects = LoadAssets<ResearchProjectSO>(ProjectRoot);
        CombatEquipmentDefinitionSO[] equipment = Resources
            .LoadAll<CombatEquipmentDefinitionSO>(ResourceCombatEquipmentCatalog.ResourcePath)
            .Where(item => item != null)
            .ToArray();
        EquipmentModuleDefinitionSO[] modules = LoadAssets<EquipmentModuleDefinitionSO>(ModuleRoot);

        ValidateResearchGraph(projects, failures);
        ValidateContentCounts(failures);
        ValidateRewards(projects, equipment, failures);
        ValidateEquipment(projects, equipment, modules, failures);
        ValidateRuntimeLocksModulesAndSave(failures);
        ValidateDeterministicDrops(failures);
        ValidatePacing(projects, failures, out pacingReport);
        return failures;
    }

    private static void ValidateResearchGraph(
        IReadOnlyList<ResearchProjectSO> projects,
        ICollection<string> failures)
    {
        Require(projects.Count == 168, $"research count {projects.Count}, expected 168", failures);
        Require(projects.Select(project => project.ProjectId.Value)
                .Distinct(StringComparer.Ordinal).Count() == projects.Count,
            "duplicate stable research ID", failures);
        Require(projects.Select(project => project.id).Distinct().Count() == projects.Count,
            "duplicate numeric research ID", failures);

        foreach (ResearchProjectSO project in projects)
        {
            foreach (string error in project.ValidateDefinition())
            {
                failures.Add(error);
            }
            Require(project.Prerequisites.Count <= 4,
                $"{project.ProjectId}: more than four direct prerequisites", failures);
            Require(project.PrerequisiteLinks.Count == project.Prerequisites.Count,
                $"{project.ProjectId}: causal link count mismatch", failures);
            foreach (ResearchPrerequisiteLink link in project.PrerequisiteLinks)
            {
                Require(link != null && link.IsValid,
                    $"{project.ProjectId}: invalid causal prerequisite link", failures);
            }
        }

        Dictionary<ResearchProjectSO, int> states = projects.ToDictionary(project => project, _ => 0);
        foreach (ResearchProjectSO project in projects)
        {
            if (HasCycle(project, states))
            {
                failures.Add($"research cycle reaches {project.ProjectId}");
                break;
            }
        }
    }

    private static bool HasCycle(
        ResearchProjectSO project,
        IDictionary<ResearchProjectSO, int> states)
    {
        if (!states.TryGetValue(project, out int state))
        {
            return false;
        }
        if (state == 1)
        {
            return true;
        }
        if (state == 2)
        {
            return false;
        }

        states[project] = 1;
        if (project.Prerequisites.Any(prerequisite => HasCycle(prerequisite, states)))
        {
            return true;
        }
        states[project] = 2;
        return false;
    }

    private static void ValidateContentCounts(ICollection<string> failures)
    {
        Require(LoadAssets<BuildingSO>(FacilityRoot).Length >= 40,
            "research-linked facility count is below 40", failures);
        Require(LoadAssets<ResourceItemDefinitionSO>(ItemRoot).Length >= 30,
            "branched production item set is incomplete", failures);
        Require(LoadAssets<ProductionRecipeSO>(RecipeRoot).Length >= 29,
            "branched production recipe set is incomplete", failures);
    }

    private static void ValidateRewards(
        ResearchProjectSO[] projects,
        CombatEquipmentDefinitionSO[] equipment,
        ICollection<string> failures)
    {
        ResourceResearchProjectCatalog research = new ResourceResearchProjectCatalog(projects);
        BuildingSO[] buildings = AssetDatabase.FindAssets(
                "t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(building => building != null)
            .ToArray();
        ResourceItemDefinitionSO[] items = Resources.LoadAll<ResourceItemDefinitionSO>(
            ResourceItemDefinitionSO.ResourcePath);
        ProductionRecipeSO[] recipes = Resources.LoadAll<ProductionRecipeSO>(
            ProductionRecipeSO.ResourcePath);
        ResourceEconomyContentCatalog economy = new ResourceEconomyContentCatalog(
            items,
            recipes,
            Resources.LoadAll<CropDefinitionSO>(CropDefinitionSO.ResourcePath),
            Resources.LoadAll<CraftMaterialDefinitionSO>(CraftMaterialDefinitionSO.ResourcePath));
        ResearchRewardCatalog rewards = new ResearchRewardCatalog(
            research,
            new FixedFacilityCatalog(buildings),
            economy,
            new FixedEquipmentCatalog(equipment),
            new ResourceSurgicalProcedureCatalog(
                Resources.LoadAll<SurgicalProcedureSO>(
                    SurgicalProcedureSO.ResourcePath)));
        foreach (string error in rewards.Validate())
        {
            failures.Add(error);
        }
    }

    private static void ValidateEquipment(
        IReadOnlyList<ResearchProjectSO> projects,
        IReadOnlyList<CombatEquipmentDefinitionSO> equipment,
        IReadOnlyList<EquipmentModuleDefinitionSO> modules,
        ICollection<string> failures)
    {
        HashSet<string> researchIds = projects
            .Select(project => project.ProjectId.Value)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> dayOneExpected = new HashSet<string>(StringComparer.Ordinal)
        {
            "weapon:dagger", "weapon:spear", "weapon:javelin",
            "armor:cloth-hood", "armor:leather-cap", "shield:wood"
        };
        HashSet<string> dayOneActual = equipment
            .Where(definition => string.IsNullOrWhiteSpace(definition.RequiredResearchId))
            .Select(definition => definition.EquipmentId)
            .ToHashSet(StringComparer.Ordinal);
        Require(dayOneActual.SetEquals(dayOneExpected),
            $"day-one equipment differs: {string.Join(", ", dayOneActual.OrderBy(id => id))}",
            failures);
        Require(equipment.Count == 43, $"equipment count {equipment.Count}, expected 43", failures);
        Require(modules.Count == 20, $"module count {modules.Count}, expected 20", failures);
        Require(modules.Select(module => module.ModuleId).Distinct(StringComparer.Ordinal).Count() == 20,
            "duplicate equipment module ID", failures);

        HashSet<string> growthExpected = new HashSet<string>(StringComparer.Ordinal)
        {
            "weapon:longsword", "armor:gambeson", "shield:iron",
            "weapon:halberd", "weapon:greatsword", "weapon:windlass-crossbow",
            "weapon:matchlock-pistol", "weapon:siege-arbalest", "weapon:rune-blade",
            "armor:scale-coat", "armor:articulated-plate", "armor:powered-harness",
            "armor:rune-ward-mail", "armor:blacksteel-carapace",
            "shield:buckler", "shield:rune"
        };
        HashSet<string> fourSlotExpected = new HashSet<string>(StringComparer.Ordinal)
        {
            "weapon:siege-arbalest", "weapon:rune-blade", "armor:powered-harness",
            "armor:blacksteel-carapace", "shield:rune"
        };
        HashSet<string> growthActual = equipment.Where(definition => definition.GrowthEquipment)
            .Select(definition => definition.EquipmentId)
            .ToHashSet(StringComparer.Ordinal);
        Require(growthActual.SetEquals(growthExpected), "growth equipment set differs", failures);

        foreach (CombatEquipmentDefinitionSO definition in equipment)
        {
            if (!string.IsNullOrWhiteSpace(definition.RequiredResearchId))
            {
                Require(researchIds.Contains(definition.RequiredResearchId),
                    $"{definition.EquipmentId}: missing research {definition.RequiredResearchId}", failures);
            }
            if (definition.GrowthEquipment)
            {
                int expectedSlots = fourSlotExpected.Contains(definition.EquipmentId) ? 4 : 3;
                Require(definition.ModuleSlotCount == expectedSlots,
                    $"{definition.EquipmentId}: growth slot count {definition.ModuleSlotCount}", failures);
                Require(Mathf.Approximately(definition.BaseStatMultiplier, 0.88f),
                    $"{definition.EquipmentId}: growth base multiplier is not 0.88", failures);
            }
            else
            {
                Require(definition.ModuleSlotCount <= 1,
                    $"{definition.EquipmentId}: normal equipment has more than one slot", failures);
            }
        }
    }

    private static void ValidateDeterministicDrops(ICollection<string> failures)
    {
        foreach (EquipmentExpeditionRewardKind kind in
                 Enum.GetValues(typeof(EquipmentExpeditionRewardKind)))
        {
            EquipmentExpeditionRewardRequest request = new EquipmentExpeditionRewardRequest(
                8675309, "fixed-event", kind, EquipmentEra.MatureIndustrial,
                "region:validation", Vector2Int.zero);
            int first = EquipmentExpeditionRewardService.PreviewModuleDropCount(request);
            int second = EquipmentExpeditionRewardService.PreviewModuleDropCount(request);
            Require(first == second, $"{kind}: runSeed result is not deterministic", failures);
            if (kind == EquipmentExpeditionRewardKind.RegionBoss)
            {
                Require(first is 1 or 2, "boss module reward is not guaranteed", failures);
            }
            else
            {
                Require(first is 0 or 1, $"{kind}: invalid optional drop count", failures);
            }
        }
    }

    private static void ValidateRuntimeLocksModulesAndSave(
        ICollection<string> failures)
    {
        ResourceCombatEquipmentCatalog catalog = new ResourceCombatEquipmentCatalog(new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        CombatEquipmentRuntime locked = CombatEquipmentEditorTestFactory.Create(
            catalog,
            new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore()),
            new CharacterCarryInventoryRegistry(), materialCatalog: EmptyResourceEconomyContentCatalog.Instance, evolutionModules: EmptyEvolutionModuleRegistry.Instance, researchProvider: EditorLockedResearchRuntimeReferences.Instance, moduleCatalog: EmptyEquipmentModuleCatalog.Instance, itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
        Require(locked.IsDefinitionUnlocked("weapon:dagger", out _),
            "day-one dagger is locked", failures);
        bool lockedWithExpectedCode =
            !locked.IsDefinitionUnlocked("weapon:longsword", out string lockReason)
            && string.Equals(
                lockReason,
                $"equipment.research.required:{LongswordResearchId}",
                StringComparison.Ordinal);
        /* Legacy localized-message assertion intentionally replaced by the stable failure code.
                && lockReason.Contains("연구 필요", StringComparison.Ordinal),
        */
        Require(lockedWithExpectedCode, "longsword does not expose its research lock", failures);
        bool directCreateRejected = false;
        try
        {
            locked.CreateInstance("weapon:longsword", CombatEquipmentQuality.Normal);
        }
        catch (InvalidOperationException exception)
        {
            directCreateRejected = string.Equals(
                exception.Message,
                $"equipment.research.required:{LongswordResearchId}",
                StringComparison.Ordinal);
            /* Legacy localized-message assertion intentionally replaced by the stable failure code.
                "연구 필요", StringComparison.Ordinal);
            */
        }
        Require(directCreateRejected, "direct runtime call bypasses research lock", failures);

        ResourceEquipmentModuleCatalog moduleCatalog = new ResourceEquipmentModuleCatalog(new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        WorldItemStackRuntime physicalItems =
            PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                out WorldItemRepository itemRepository,
                out CombatEquipmentRuntime runtime);
        physicalItems.Start();
        List<GameObject> progressionObjects = new List<GameObject>();
        BuildableObject appraisal = CreateProgressionFacility(
            AppraisalFacilityPath,
            "ResearchEquipment_Appraisal",
            new Vector2Int(40, 40),
            progressionObjects);
        BuildableObject restoration = CreateProgressionFacility(
            RestorationFacilityPath,
            "ResearchEquipment_Restoration",
            new Vector2Int(42, 40),
            progressionObjects);
        BuildableObject fitting = CreateProgressionFacility(
            PrecisionFittingFacilityPath,
            "ResearchEquipment_Fitting",
            new Vector2Int(44, 40),
            progressionObjects);
        BuildableObject wrongFacility = CreateProgressionFacility(
            WrongProgressionFacilityPath,
            "ResearchEquipment_Wrong",
            new Vector2Int(46, 40),
            progressionObjects);
        Require(appraisal != null && restoration != null && fitting != null
                && wrongFacility != null,
            "equipment progression facility fixture is incomplete", failures);
        Require(runtime.IsDefinitionUnlocked("weapon:longsword", out _),
            "completed research does not immediately unlock equipment", failures);
        CombatEquipmentInstance weapon = runtime.CreateInstance(
            "weapon:greatsword", CombatEquipmentQuality.Good);
        EquipmentModuleInstance module = runtime.CreateExpeditionModule(
            "module:weapon:balanced-core",
            3,
            appraisal.centerPos,
            WorldItemStackState.FacilityBuffer,
            appraisal.RequirePersistentInstanceId().Value);
        Require(!runtime.TryAppraiseModule(
                module.instanceId,
                wrongFacility,
                out DomainFailure wrongFacilityFailure)
                && wrongFacilityFailure.Code
                    == FailureCode.EquipmentProgressionFacilityUnavailable,
            "wrong facility bypassed module appraisal authorization", failures);
        Require(runtime.TryAppraiseModule(module.instanceId, appraisal, out _),
            "module appraisal failed", failures);
        Require(!runtime.TryRestoreModule(
                module.instanceId,
                restoration,
                out DomainFailure remoteRestoreFailure)
                && remoteRestoreFailure.Code == FailureCode.EquipmentModuleMissing,
            "module restoration ignored the facility-local buffer", failures);
        Require(physicalItems.TryRouteStackToDestination(
                module.sourceStackId,
                WorldItemStackState.FacilityBuffer,
                restoration.RequirePersistentInstanceId().Value,
                restoration.centerPos,
                out _),
            "module could not be routed to the restoration buffer", failures);
        Require(runtime.TryRestoreModule(module.instanceId, restoration, out _),
            "module restoration failed", failures);
        Require(physicalItems.TryRouteStackToDestination(
                module.sourceStackId,
                WorldItemStackState.FacilityBuffer,
                fitting.RequirePersistentInstanceId().Value,
                fitting.centerPos,
                out _),
            "module could not be routed to the fitting buffer", failures);
        Require(physicalItems.SpawnExistingUniqueItemAt(
                PhysicalItemIds.ForEquipment(weapon.definitionId),
                (ItemInstanceId)weapon.instanceId,
                fitting.centerPos,
                WorldItemStackState.FacilityBuffer,
                fitting.RequirePersistentInstanceId().Value,
                out string weaponStackId)
                && runtime.TryLinkToWorldStack(
                    weapon.instanceId,
                    weaponStackId,
                    CombatEquipmentWorldState.Stored),
            "equipment could not be materialized in the fitting buffer", failures);
        Require(!runtime.TryInstallModule(
                weapon.instanceId,
                module.instanceId,
                0,
                appraisal,
                out DomainFailure wrongInstallFailure)
                && wrongInstallFailure.Code
                    == FailureCode.EquipmentProgressionFacilityUnavailable,
            "wrong facility bypassed module installation authorization", failures);
        Require(runtime.TryInstallModule(
                weapon.instanceId, module.instanceId, 0, fitting, out _),
            "module installation failed", failures);
        Require(runtime.TryRemoveModule(
                weapon.instanceId,
                0,
                fitting,
                out EquipmentModuleInstance removed,
                out _)
                && removed.condition <= 0.7f
                && removed.state == EquipmentModuleProcessState.IdentifiedDamaged,
            "removed module was not returned as a <=70% damaged part", failures);
        Require(!runtime.TryInstallModule(
                weapon.instanceId,
                module.instanceId,
                0,
                fitting,
                out DomainFailure damagedFailure)
                && damagedFailure.Code == FailureCode.ModuleNeedsRestoration,
            "damaged module was reinstalled without restoration", failures);

        DungeonCombatEquipmentSaveData save = runtime.Capture();
        CombatEquipmentRuntime restored = CombatEquipmentEditorTestFactory.Create(
            catalog,
            itemRepository,
            new CharacterCarryInventoryRegistry(),
            researchProvider: EditorAllResearchRuntimeProvider.Instance,
            moduleCatalog: moduleCatalog, materialCatalog: EmptyResourceEconomyContentCatalog.Instance, evolutionModules: EmptyEvolutionModuleRegistry.Instance, itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
        restored.PublishRestoreCandidate(
            restored.BuildRestoreCandidate(save));
        Require(restored.ModuleInstances.Count == 1
                && restored.Instances.Any(instance => instance.instanceId == weapon.instanceId),
            "equipment V6 module save round trip failed", failures);
        physicalItems.Dispose();
        foreach (GameObject progressionObject in progressionObjects)
        {
            UnityEngine.Object.DestroyImmediate(progressionObject);
        }

        CombatEquipmentSaveSection saveSection = new CombatEquipmentSaveSection(restored);
        bool legacyRejected = false;
        try
        {
            saveSection.Restore(
                JsonUtility.ToJson(save), 4, new DungeonGameRestoreReport());
        }
        catch (InvalidOperationException exception)
        {
            legacyRejected = true;
            _ = exception.Message.Contains(
                "새 게임 필요",
                StringComparison.Ordinal);
        }
        Require(legacyRejected, "combat equipment V1-V4 save was not rejected", failures);

        string beforeInvalidRestore = JsonUtility.ToJson(restored.Capture());
        DungeonCombatEquipmentSaveData invalid = JsonUtility.FromJson<DungeonCombatEquipmentSaveData>(
            JsonUtility.ToJson(save));
        invalid.craftOrders.Add(new CombatEquipmentCraftOrderSaveData
        {
            orderId = " invalid-order ",
            definitionId = "weapon:dagger",
            requiredWork = 1f,
            completedWork = 0f,
            materialDestinationId = "equipment-craft:invalid-order"
        });
        bool invalidRejected = false;
        try
        {
            saveSection.StageRestore(
                JsonUtility.ToJson(invalid),
                6,
                new DungeonGameRestoreReport());
        }
        catch (InvalidOperationException)
        {
            invalidRejected = true;
        }

        Require(invalidRejected, "invalid combat equipment payload was accepted", failures);
        Require(string.Equals(
                beforeInvalidRestore,
                JsonUtility.ToJson(restored.Capture()),
                StringComparison.Ordinal),
            "invalid combat equipment payload mutated live state", failures);
        Require(saveSection is IDungeonRollbackFreeSaveSection
                && saveSection is IDungeonSaveSectionPreflight
                && saveSection is IDungeonStagedSaveSection,
            "combat equipment save section is missing strict restore contracts", failures);
    }

    private static void ValidatePacing(
        IReadOnlyList<ResearchProjectSO> projects,
        ICollection<string> failures,
        out string report)
    {
        Dictionary<string, ResearchProjectSO> byId = projects.ToDictionary(
            project => project.ProjectId.Value,
            StringComparer.Ordinal);
        HashSet<ResearchProjectSO> closure = new HashSet<ResearchProjectSO>();
        float medieval = AddQueueAndMeasure(MedievalQueue, byId, closure);
        float early = AddQueueAndMeasure(EarlyIndustrialQueue, byId, closure);
        float mature = AddQueueAndMeasure(MatureIndustrialQueue, byId, closure);
        float late = AddQueueAndMeasure(LateIndustrialQueue, byId, closure);
        Require(medieval >= 27f && medieval <= 34f,
            $"medieval pacing {medieval:0.0} days", failures);
        Require(early >= 80f && early <= 100f,
            $"early industrial pacing {early:0.0} days", failures);
        Require(mature >= 200f && mature <= 240f,
            $"mature industrial pacing {mature:0.0} days", failures);
        Require(late >= 320f && late <= 400f,
            $"late industrial pacing {late:0.0} days", failures);
        report = $"pacing days M/E/A/L={medieval:0.0}/{early:0.0}/{mature:0.0}/{late:0.0}";
    }

    private static float AddQueueAndMeasure(
        IEnumerable<string> ids,
        IReadOnlyDictionary<string, ResearchProjectSO> byId,
        ISet<ResearchProjectSO> closure)
    {
        foreach (string id in ids)
        {
            if (!byId.TryGetValue(id, out ResearchProjectSO project))
            {
                throw new InvalidOperationException($"Pacing queue research does not exist: {id}");
            }
            AddClosure(project, closure);
        }
        return closure.Sum(project => project.RequiredWork) / EffectiveWorkPerDay;
    }

    private static void AddClosure(ResearchProjectSO project, ISet<ResearchProjectSO> closure)
    {
        if (!closure.Add(project))
        {
            return;
        }
        foreach (ResearchProjectSO prerequisite in project.Prerequisites)
        {
            AddClosure(prerequisite, closure);
        }
    }

    private static T[] LoadAssets<T>(string root) where T : UnityEngine.Object =>
        AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .ToArray();

    private static BuildableObject CreateProgressionFacility(
        string assetPath,
        string objectName,
        Vector2Int position,
        ICollection<GameObject> created)
    {
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(assetPath);
        if (definition == null)
        {
            return null;
        }
        GameObject facilityObject = new GameObject(objectName);
        created.Add(facilityObject);
        BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
        facility.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
        CharacterAiEditorTestDependencies.Inject(facility);
        facility.Initialization(definition, position);
        return facility;
    }

    private static void Require(bool condition, string message, ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }

    private sealed class FixedFacilityCatalog : IFacilityShopCatalog
    {
        private readonly BuildingSO[] buildings;
        public FixedFacilityCatalog(BuildingSO[] buildings) => this.buildings = buildings;
        public IReadOnlyCollection<BuildingSO> Buildings => buildings;
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints =>
            Array.Empty<FacilityBlueprintSO>();
        public BuildingSO FindBuildingById(int buildingId) =>
            buildings.FirstOrDefault(building => building.id == buildingId);
    }

    private sealed class FixedEquipmentCatalog : ICombatEquipmentCatalog
    {
        private readonly CombatEquipmentDefinitionSO[] equipment;
        public FixedEquipmentCatalog(CombatEquipmentDefinitionSO[] equipment) =>
            this.equipment = equipment;
        public IReadOnlyList<CombatEquipmentDefinitionSO> All => equipment;
        public bool TryGet(string definitionId, out CombatEquipmentDefinitionSO definition)
        {
            definition = equipment.FirstOrDefault(item => string.Equals(
                item.EquipmentId, definitionId, StringComparison.Ordinal));
            return definition != null;
        }
    }
}
#endif
