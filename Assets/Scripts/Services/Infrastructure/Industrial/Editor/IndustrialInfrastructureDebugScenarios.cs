#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class IndustrialInfrastructureDebugScenarios
{
    private sealed class ProbeGridOccupant : IGridOccupant
    {
        public int GridId { get; }
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => false;

        public ProbeGridOccupant(int gridId)
        {
            GridId = gridId;
        }
    }

    private sealed class ProbeFeatureSurfaceView : IFeatureSurfaceView
    {
        public int SectionCount { get; private set; }
        public int CardCount { get; private set; }
        public int ControlCount { get; private set; }

        public void AddSection(string title, string summary)
        {
            SectionCount++;
        }

        public void AddLabel(string text, float fontSize, float height)
        {
        }

        public void AddDataCard(
            string actionName,
            string title,
            string detail,
            string buttonText,
            Action onClick,
            float height)
        {
            CardCount++;
        }

        public void AddControlCard(
            string actionName,
            string title,
            string detail,
            IReadOnlyList<FeatureSurfaceStepper> steppers,
            IReadOnlyList<FeatureSurfaceAction> actions,
            float height)
        {
            ControlCount++;
        }

        public void ShowFeedback(string message)
        {
        }

        public void RequestRefresh()
        {
        }
    }

    [MenuItem("DungeonStory/Debug/Infrastructure/Run Industrial Checks")]
    public static void RunAll()
    {
        VerifyResearchContent();
        VerifyIndustrialBuildings();
        VerifySanitationAndProcessFluids();
        VerifyWorkRegistration();
        VerifyUtilityLayerCoexistence();
        VerifyConveyorStateEvaluation();
        VerifyAutomationPowerDemand();
        VerifySaveRoundTrip();
        VerifyItemDefinitions();
        VerifyIndustryTab();
        Debug.Log(
            "IndustrialInfrastructureDebugScenarios passed: research, assets, utilities, conveyors, automation, save, and UI.");
    }

    [MenuItem(
        "DungeonStory/Debug/Infrastructure/Run Industrial PlayMode Checks")]
    public static void RunPlayModeChecks()
    {
        Require(Application.isPlaying,
            "Industrial PlayMode checks require Play Mode.");
        DungeonRuntimeLifetimeScope scope =
            UnityEngine.Object.FindFirstObjectByType<
                DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null,
            "DungeonRuntimeLifetimeScope is not ready.");

        IAutomationRuntime automation =
            scope.Container.Resolve<IAutomationRuntime>();
        IPlumbingCommandService plumbing =
            scope.Container.Resolve<IPlumbingCommandService>();
        IConveyorCommandService conveyor =
            scope.Container.Resolve<IConveyorCommandService>();
        IWorkExecutionHandlerRegistry work =
            scope.Container.Resolve<IWorkExecutionHandlerRegistry>();
        IFeatureSurfaceTabPresenterRegistry presenters =
            scope.Container.Resolve<IFeatureSurfaceTabPresenterRegistry>();
        Require(automation != null
                && plumbing != null
                && conveyor != null
                && work != null,
            "An industrial runtime service is not registered.");
        Require(presenters.TryGet(
                TabId.Industry,
                out IFeatureSurfaceTabPresenter presenter),
            "The industry feature presenter is not registered.");

        ProbeFeatureSurfaceView view = new ProbeFeatureSurfaceView();
        presenter.Present(view);
        Require(view.SectionCount >= 5,
            "The industry surface is missing a major section.");
        Debug.Log(
            $"Industrial PlayMode checks passed: "
            + $"sections={view.SectionCount}, cards={view.CardCount}, "
            + $"controls={view.ControlCount}, "
            + $"automation={automation.Facilities.Count}, "
            + $"waterTransfers={plumbing.WaterTransfers.Count}, "
            + $"conveyorNetworks={conveyor.Networks.Count}.");
    }

    [MenuItem(
        "DungeonStory/Debug/Infrastructure/Run 10K Infrastructure Stress Check")]
    public static void RunStressChecks()
    {
        IndustrialInfrastructureStressReport report =
            IndustrialInfrastructureStressProbe.Run();
        Debug.Log(
            "Industrial stress check passed: "
            + $"utilityCells={report.UtilityCellCount}, "
            + $"payloadRoutes={report.PayloadRouteCount}, "
            + $"topologyMs={report.TopologyMilliseconds:0.0}, "
            + $"topologyAllocKB={report.TopologyAllocatedBytes / 1024f:0.0}, "
            + $"routesMs={report.RouteMilliseconds:0.0}, "
            + $"routeAllocKB={report.RouteAllocatedBytes / 1024f:0.0}.");
    }

    private static void VerifyResearchContent()
    {
        ResearchProjectSO[] projects = AssetDatabase.FindAssets(
                "t:ResearchProjectSO",
                new[] { "Assets/Resources/SO/Research/Projects" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResearchProjectSO>)
            .Where(project => project != null)
            .ToArray();
        Require(projects.Length == 118,
            $"Expected 118 research projects, got {projects.Length}.");
        Require(projects.Select(project => project.ProjectId.Value)
                .Distinct(StringComparer.Ordinal)
                .Count()
            == projects.Length,
            "Research project IDs are not unique.");
        Require(projects.All(project =>
                project.ValidateDefinition().Count == 0),
            "A research project has an invalid reference or blueprint rule.");
        Require(projects.Count(project =>
                project.Field == ResearchField.IndustryAndAutomation)
            == 32,
            "Expected 32 industry and automation projects.");
        Require(projects.Count(project =>
                project.Field == ResearchField.WaterAndSanitation)
            == 8,
            "Expected 8 water and sanitation projects.");
    }

    private static void VerifyIndustrialBuildings()
    {
        BuildingSO[] buildings = LoadIndustrialBuildings();
        Require(buildings.Length == 32,
            $"Expected 32 industrial buildings, got {buildings.Length}.");
        Require(buildings.Select(building => building.id).Distinct().Count()
            == buildings.Length,
            "Industrial building IDs are not unique.");
        Require(buildings.All(building =>
                building.sprite != null && building.icon != null),
            "An industrial building is missing its sprite or icon.");
        Require(buildings.All(building =>
                building.Facility?.SupportsWork(
                    BuiltInWorkTypeIds.Repair)
                == true),
            "An industrial building cannot create repair work.");
        Require(buildings.Count(building =>
                building.layer == GridLayer.Utility)
            >= 4,
            "Utility-layer building content is missing.");
        Require(buildings.Count(building =>
                building.layer == GridLayer.Conveyor)
            >= 10,
            "Conveyor-layer building content is missing.");
        Require(buildings.Any(building =>
                building.GetAbility<BuildingConveyorOverflowAbility>() != null),
            "No conveyor overflow dump port was generated.");
        Require(buildings.Any(building =>
                building.GetAbility<BuildingWastewaterProcessorAbility>() != null),
            "No wastewater processor was generated.");
        BuildingSO bottling = FindByCode(buildings, "I10");
        Require(bottling != null
                && bottling.GetAbility<
                    BuildingWaterContainerTransferAbility>() != null,
            "The water bottling station has no transfer behavior.");
        BuildingUtilityConnectionAbility bottlingUtility =
            bottling.GetAbility<BuildingUtilityConnectionAbility>();
        Require(bottlingUtility != null
                && (bottlingUtility.channels & UtilityChannel.Power) != 0
                && (bottlingUtility.channels
                    & UtilityChannel.CleanWater) != 0,
            "The water bottling station is not connected to power and water.");
    }

    private static void VerifySanitationAndProcessFluids()
    {
        BuildingSO[] all = LoadAllBuildings();
        VerifyFixture(all, "H01", 0.25f, 0.25f, true);
        VerifyFixture(all, "H03", 0.15f, 0.15f, false);
        VerifyFixture(all, "H04", 1f, 1f, false);
        VerifyFixture(all, "I14", 0.45f, 0.45f, false);

        BuildingSO[] consumers = all.Where(building =>
                building.Facility?.SupportsWork(
                    BuiltInWorkTypeIds.Cook)
                == true
                || building.Facility?.SupportsWork(
                    BuiltInWorkTypeIds.Surgery)
                == true)
            .ToArray();
        Require(consumers.Length > 0,
            "No cooking or surgery fluid consumers were found.");
        foreach (BuildingSO building in consumers)
        {
            BuildingProcessFluidAbility process =
                building.GetAbility<BuildingProcessFluidAbility>();
            BuildingUtilityConnectionAbility utility =
                building.GetAbility<BuildingUtilityConnectionAbility>();
            Require(process != null,
                $"{building.objectName} is missing process fluid settings.");
            Require(utility != null
                    && (utility.channels & UtilityChannel.CleanWater) != 0
                    && (utility.channels & UtilityChannel.Wastewater) != 0,
                $"{building.objectName} is not connected to both water channels.");
            Require(building.Facility?.SupportsWork(
                    BuiltInWorkTypeIds.Plumbing)
                == true,
                $"{building.objectName} cannot create plumbing maintenance work.");
        }
    }

    private static void VerifyWorkRegistration()
    {
        Require(WorkTypeCatalog.TryGet(
                BuiltInWorkTypeIds.Plumbing,
                out WorkTypeDefinition plumbing),
            "Plumbing work is not registered.");
        Require(plumbing.DefaultPriority == WorkPriorityLevel.Priority2,
            "Plumbing work default priority changed.");
        Require(plumbing.DisplayName == "배관",
            "Plumbing work is not displayed in Korean.");
    }

    private static void VerifyConveyorStateEvaluation()
    {
        Require(ConveyorNetworkStateEvaluator.Evaluate(
                cyclic: true,
                payloadCount: 4,
                totalCapacity: 4,
                networkHasNoProgress: true,
                allUnpowered: false,
                allStopped: false,
                longestStallSeconds: 31f)
            == ConveyorNetworkState.Deadlocked,
            "A full stalled conveyor cycle was not classified as deadlocked.");
        Require(ConveyorNetworkStateEvaluator.Evaluate(
                cyclic: true,
                payloadCount: 4,
                totalCapacity: 4,
                networkHasNoProgress: true,
                allUnpowered: true,
                allStopped: false,
                longestStallSeconds: 120f)
            == ConveyorNetworkState.Unpowered,
            "Power loss was incorrectly classified as deadlock.");
        Require(ConveyorNetworkStateEvaluator.Evaluate(
                cyclic: false,
                payloadCount: 1,
                totalCapacity: 8,
                networkHasNoProgress: true,
                allUnpowered: false,
                allStopped: false,
                longestStallSeconds: 31f)
            == ConveyorNetworkState.Stalled,
            "A non-cyclic obstruction was not classified as stalled.");
        Require(ConveyorNetworkStateEvaluator.Evaluate(
                cyclic: true,
                payloadCount: 3,
                totalCapacity: 4,
                networkHasNoProgress: true,
                allUnpowered: false,
                allStopped: false,
                longestStallSeconds: 12f)
            == ConveyorNetworkState.Running,
            "A cycle with a free slot was incorrectly classified as deadlock.");
    }

    private static void VerifyUtilityLayerCoexistence()
    {
        GridCell cell = new GridCell(new Vector2Int(3, 4));
        ProbeGridOccupant power = new ProbeGridOccupant(1);
        ProbeGridOccupant cleanWater = new ProbeGridOccupant(2);
        ProbeGridOccupant wastewater = new ProbeGridOccupant(3);

        Require(cell.TrySetOccupant(GridLayer.Utility, power)
                && cell.TrySetOccupant(GridLayer.Utility, cleanWater)
                && cell.TrySetOccupant(GridLayer.Utility, wastewater),
            "Power, clean-water, and wastewater utilities cannot coexist.");

        List<IGridOccupant> utilities = new List<IGridOccupant>();
        cell.FillOccupantsInLayer(GridLayer.Utility, utilities);
        Require(utilities.Count == 3
                && utilities.Contains(power)
                && utilities.Contains(cleanWater)
                && utilities.Contains(wastewater),
            "A co-located utility disappeared from the grid cell.");

        Require(cell.RemoveOccupant(GridLayer.Utility, cleanWater)
                && cell.ContainsOccupant(GridLayer.Utility, power)
                && !cell.ContainsOccupant(GridLayer.Utility, cleanWater)
                && cell.ContainsOccupant(GridLayer.Utility, wastewater),
            "Removing one utility removed another co-located channel.");
    }

    private static void VerifyAutomationPowerDemand()
    {
        BuildingAutomationAbility ability =
            new BuildingAutomationAbility
            {
                assistedPowerDemand = 2.5f,
                automaticPowerDemand = 7f
            };
        Require(Mathf.Approximately(
                AutomationPowerDemandRules.Resolve(
                    AutomationMode.Manual,
                    ability),
                0f),
            "Manual automation mode consumed power.");
        Require(Mathf.Approximately(
                AutomationPowerDemandRules.Resolve(
                    AutomationMode.PoweredAssist,
                    ability),
                2.5f),
            "Powered-assist demand did not use its configured value.");
        Require(Mathf.Approximately(
                AutomationPowerDemandRules.Resolve(
                    AutomationMode.Automatic,
                    ability),
                7f),
            "Automatic demand did not use its configured value.");
    }

    private static void VerifySaveRoundTrip()
    {
        DungeonConveyorInfrastructureSaveData source =
            new DungeonConveyorInfrastructureSaveData
            {
                nextPayloadSequence = 42,
                nodes =
                {
                    new ConveyorNodeSaveData
                    {
                        nodeId = "node:overflow",
                        enabled = true,
                        destinationId = "warehouse:reserve",
                        overflowPolicy =
                            ConveyorOverflowPolicy.ReserveWarehouseThenLoose,
                        reserveWarehouseId = "warehouse:reserve",
                        filter = new ConveyorFilterSaveData
                        {
                            materialIds =
                            {
                                "material:steel"
                            },
                            filterQuality = true,
                            minimumQuality =
                                (int)CombatEquipmentQuality.Good,
                            maximumQuality =
                                (int)CombatEquipmentQuality.Legendary,
                            filterFreshness = true,
                            minimumFreshness01 = 0.65f,
                            maximumFreshness01 = 1f,
                            allowContaminated = false
                        }
                    }
                },
                payloads =
                {
                    new ConveyorPayloadSaveData
                    {
                        payloadId = "payload:unique",
                        segmentNodeId = "node:loop",
                        destinationId = "warehouse:reserve",
                        stalledSince = 37f,
                        stallReason = ConveyorStallReason.CyclicDeadlock,
                        stack = new WorldItemStackSaveData
                        {
                            stackId = "stack:corpse",
                            itemId = "dark:humanoid_corpse",
                            quantity = 1,
                            sourceCharacterId = "character:donor",
                            sourceDisplayName = "검증 대상",
                            sourceSpeciesTag = "human",
                            sourceDeathReason = "test",
                            emergencyButcheryAllowed = true,
                            contamination = 17f
                        }
                    }
                }
            };
        DungeonConveyorInfrastructureSaveData restored =
            JsonUtility.FromJson<DungeonConveyorInfrastructureSaveData>(
                JsonUtility.ToJson(source));
        Require(restored != null
                && restored.nextPayloadSequence == 42
                && restored.nodes.Count == 1
                && restored.payloads.Count == 1,
            "Conveyor save data did not round-trip.");
        ConveyorFilterSaveData restoredFilter =
            restored.nodes[0].filter;
        Require(restoredFilter.materialIds.SequenceEqual(
                    new[] { "material:steel" })
                && restoredFilter.filterQuality
                && restoredFilter.minimumQuality
                    == (int)CombatEquipmentQuality.Good
                && restoredFilter.filterFreshness
                && Mathf.Approximately(
                    restoredFilter.minimumFreshness01,
                    0.65f)
                && !restoredFilter.allowContaminated,
            "Advanced conveyor filters did not round-trip.");
        WorldItemStackSaveData stack = restored.payloads[0].stack;
        Require(stack.stackId == "stack:corpse"
                && stack.sourceCharacterId == "character:donor"
                && stack.emergencyButcheryAllowed
                && Mathf.Approximately(stack.contamination, 17f),
            "Unique item metadata was lost in conveyor save data.");

        ProductionBillSaveData bill = new ProductionBillSaveData
        {
            billId = "bill:water",
            materialsConsumed = true,
            processFluidConsumed = true
        };
        ProductionBillSaveData restoredBill =
            JsonUtility.FromJson<ProductionBillSaveData>(
                JsonUtility.ToJson(bill));
        Require(restoredBill.processFluidConsumed,
            "Production process-fluid state was not saved.");
        SurgeryOrder surgery = new SurgeryOrder
        {
            orderId = "surgery:water",
            processFluidConsumed = true
        };
        SurgeryOrder restoredSurgery = JsonUtility.FromJson<SurgeryOrder>(
            JsonUtility.ToJson(surgery));
        Require(restoredSurgery.processFluidConsumed,
            "Surgery process-fluid state was not saved.");

        DungeonFluidInfrastructureSaveData fluid =
            new DungeonFluidInfrastructureSaveData
            {
                nodes =
                {
                    new FluidNodeSaveData
                    {
                        nodeId = "node:bottling",
                        manualWaterReserve = 0.65f,
                        transferMode =
                            WaterContainerTransferMode.FeedNetwork,
                        transferWork = 2.5f
                    }
                }
            };
        DungeonFluidInfrastructureSaveData restoredFluid =
            JsonUtility.FromJson<DungeonFluidInfrastructureSaveData>(
                JsonUtility.ToJson(fluid));
        Require(restoredFluid.nodes.Count == 1
                && restoredFluid.nodes[0].transferMode
                    == WaterContainerTransferMode.FeedNetwork
                && Mathf.Approximately(
                    restoredFluid.nodes[0].transferWork,
                    2.5f)
                && Mathf.Approximately(
                    restoredFluid.nodes[0].manualWaterReserve,
                    0.65f),
            "Water transfer mode and progress did not round-trip.");
    }

    private static void VerifyItemDefinitions()
    {
        DungeonItemCatalogSO catalog =
            ScriptableObject.CreateInstance<DungeonItemCatalogSO>();
        try
        {
            Require(catalog.TryGetDefinition(
                    IndustrialItemDefinitions.SludgeId,
                    out DungeonItemDefinition sludge)
                && sludge.DisplayName == "오수 슬러지"
                && sludge.MaxStack > 1,
                "Industrial sludge is missing from the item catalog.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(catalog);
        }
    }

    private static void VerifyIndustryTab()
    {
        Require(UITabCatalog.TryGet(
                TabId.Industry,
                out UITabDefinition definition)
            && definition.SurfaceKind == UITabSurfaceKind.Feature
            && definition.ButtonLabel == "산업",
            "The industry feature tab is missing.");
    }

    private static void VerifyFixture(
        BuildingSO[] buildings,
        string code,
        float cleanWater,
        float wastewater,
        bool dryFallback)
    {
        BuildingSO building = FindByCode(buildings, code);
        Require(building != null, $"Fixture {code} was not found.");
        BuildingWaterFixtureAbility fixture =
            building.GetAbility<BuildingWaterFixtureAbility>();
        BuildingUtilityConnectionAbility utility =
            building.GetAbility<BuildingUtilityConnectionAbility>();
        Require(fixture != null
                && Mathf.Approximately(
                    fixture.cleanWaterPerUse,
                    cleanWater)
                && Mathf.Approximately(
                    fixture.wastewaterPerUse,
                    wastewater)
                && fixture.allowsDryFallback == dryFallback,
            $"Fixture {code} has incorrect water settings.");
        Require(utility != null
                && (utility.channels & UtilityChannel.CleanWater) != 0
                && (utility.channels & UtilityChannel.Wastewater) != 0,
            $"Fixture {code} is not connected to water and sewer.");
    }

    private static BuildingSO FindByCode(
        BuildingSO[] buildings,
        string code)
    {
        return buildings.FirstOrDefault(building =>
            string.Equals(
                building.GetAbility<BuildingFacilityPartAbility>()?.code,
                code,
                StringComparison.Ordinal));
    }

    private static BuildingSO[] LoadIndustrialBuildings()
    {
        return AssetDatabase.FindAssets(
                "t:BuildingSO",
                new[] { "Assets/Resources/SO/Building/Industrial" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(building => building != null)
            .ToArray();
    }

    private static BuildingSO[] LoadAllBuildings()
    {
        return AssetDatabase.FindAssets(
                "t:BuildingSO",
                new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(building => building != null)
            .ToArray();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
