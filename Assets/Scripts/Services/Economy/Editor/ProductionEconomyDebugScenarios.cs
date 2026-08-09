#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class ProductionEconomyDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Economy/Run Production Economy Contracts")]
    public static void RunFromMenu()
    {
        RunAll();
    }

    public static void RunAll()
    {
        ValidateAuthoredContent();
        ValidateSubstanceSingleAuthority();
        ValidatePhysicalStockSensorInstallation();
        ValidatePhysicalProductionBill();
        ValidatePassiveBatchProduction();
        ValidateEconomyPlanning();
        ValidateEconomyPlanningLateFailureDiscard();
        Debug.Log("Production economy contracts passed.");
    }

    private static void ValidateSubstanceSingleAuthority()
    {
        const string legacyRoot = "Assets/Resources/SO/Economy/Substances";
        string[] legacyAssets = AssetDatabase.IsValidFolder(legacyRoot)
            ? AssetDatabase.FindAssets(string.Empty, new[] { legacyRoot })
            : Array.Empty<string>();
        Require(
            legacyAssets.Length == 0,
            $"legacy substance SO assets remain: {legacyAssets.Length}");

        ResourceEconomyContentCatalog catalog = LoadCatalog();
        foreach (SubstanceDefinitionView projection in catalog.Substances)
        {
            Require(
                catalog.TryGetItem(projection.ItemId, out ResourceItemDefinitionSO item),
                $"substance projection item is missing: {projection.ItemId}");
            Require(
                item.TryGetFeature(out SubstanceItemFeature feature),
                $"substance projection has no item feature: {projection.ItemId}");
            Require(
                string.Equals(
                    feature.substanceId?.Trim(),
                    projection.SubstanceId,
                    StringComparison.Ordinal),
                $"substance projection ID drift: {projection.ItemId}");
            Require(
                catalog.TryGetSubstance(projection.SubstanceId, out SubstanceDefinitionView indexed)
                    && ReferenceEquals(indexed, projection),
                $"substance projection index drift: {projection.SubstanceId}");
        }

        ResourceItemDefinitionSO first =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        ResourceItemDefinitionSO second =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        ItemDefinitionCatalogSO root =
            ScriptableObject.CreateInstance<ItemDefinitionCatalogSO>();
        try
        {
            first.name = "DuplicateSubstanceA";
            first.Configure(
                "debug:substance-a", "A", string.Empty, StockCategory.Medicine,
                ResourceItemKind.Substance, ResourceIngredientTag.None,
                1, 1f, 1, string.Empty);
            first.ConfigureSubstance(
                "substance:duplicate", SubstanceUseClass.NonAddictive,
                0f, 0f, 0f, 0f, 0f, 0f, 0f, 10f);
            second.name = "DuplicateSubstanceB";
            second.Configure(
                "debug:substance-b", "B", string.Empty, StockCategory.Medicine,
                ResourceItemKind.Substance, ResourceIngredientTag.None,
                1, 1f, 1, string.Empty);
            second.ConfigureSubstance(
                "substance:duplicate", SubstanceUseClass.NonAddictive,
                0f, 0f, 0f, 0f, 0f, 0f, 0f, 10f);
            root.SetDefinitions(new ItemDefinitionSO[] { first, second });
            Require(
                root.ValidateCatalog().Any(error =>
                    error.Contains("Duplicate substance ID", StringComparison.Ordinal)),
                "root item catalog accepted a duplicate substance ID");

            second.ClearSubstance();
            Require(
                root.ValidateCatalog().Any(error =>
                    error.Contains("has no substance feature", StringComparison.Ordinal)),
                "root item catalog accepted a substance item without a feature");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(second);
            UnityEngine.Object.DestroyImmediate(first);
        }
    }

    private static void ValidatePhysicalStockSensorInstallation()
    {
        BuildingSO building = AssetDatabase
            .FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .FirstOrDefault(candidate =>
                !string.IsNullOrWhiteSpace(candidate
                    ?.GetProductionWorkstationAbility()
                    ?.StockSensorInstallationItemId));
        Require(building != null, "stock sensor workstation definition is missing");

        GameObject gameObject = new("ProductionStockSensor_DebugFacility");
        try
        {
            BuildableObject facility = gameObject.AddComponent<BuildableObject>();
            const string facilityId = "building:debug-production-stock-sensor";
            facility.RestorePersistentIdentity(new BuildingInstanceId(facilityId));
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(building, Vector2Int.zero);

            string itemId = building
                .GetProductionWorkstationAbility()
                .StockSensorInstallationItemId;
            string destinationId = "production-sensor:" + facilityId;
            FakeProductionItemGateway items = new();
            NoOpWorkforceReplanService workforce =
                NoOpWorkforceReplanService.Instance;
            IProductionWorkshopRuntime workshops =
                EmptyProductionWorkshopRuntime.Instance;
            IProductionInputLogisticsService inputLogistics =
                new ProductionInputLogisticsService(
                    LoadCatalog(),
                    items,
                    EmptyResearchRuntimeReferences.Instance,
                    workforce,
                    workshops);
            IProductionAssemblyBridge bridge = new ProductionAssemblyBridgeAdapter(
                items,
                items,
                inputLogistics,
                new TestProductionCycleUtilityService(
                    workshops,
                    new MutablePowerRuntime()),
                workshops,
                new FixedBuildingWorldQuery(facility),
                EmptyWarehouseWorldQuery.Instance,
                workforce,
                Array.Empty<IProductionOutputHandler>());
            ProductionFacilityHandle facilityHandle =
                bridge.CaptureFacility(facility);
            ProductionStockSensorRuntime runtime = new(
                bridge,
                new ProductionAggregateStateStore(
                    new DungeonRuntimeAggregateRootStore()));

            runtime.RequestInstallation(facilityHandle);
            Require(
                items.GetRequested(itemId) == 1,
                $"stock sensor requested wrong physical item: {itemId}");
            items.Deliver(itemId, 1, destinationId);
            runtime.FinalizeDeliveredSensors();
            Require(runtime.Has(facilityHandle), "delivered stock sensor was not installed");
            Require(
                items.GetDelivered(itemId) == 0,
                "installed stock sensor was not physically consumed");

            runtime.Remove(facilityHandle);
            Require(!runtime.Has(facilityHandle), "removed stock sensor remains installed");
            Require(
                items.GetAvailable(itemId) == 1,
                "removed stock sensor was not returned as a physical item");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static void ValidateAuthoredContent()
    {
        BuildingSO[] buildings = AssetDatabase
            .FindAssets(
                "t:BuildingSO",
                new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(building => building != null)
            .ToArray();
        BuildingSO[] stations = buildings
            .Where(building =>
                (building.GetAbility<BuildingFacilityPartAbility>()?.code
                    ?? string.Empty)
                .StartsWith("P", StringComparison.Ordinal))
            .ToArray();
        int expectedStationCount = ModularFacilityAssetBuilder
            .GetCatalogCodes()
            .Count(code => code.StartsWith("P", StringComparison.Ordinal));
        Require(
            stations.Length == expectedStationCount,
            $"production stations={stations.Length}/{expectedStationCount}");
        Require(stations.All(station => !station.unlocked),
            "research production stations must start locked");
        Require(stations.All(station =>
                station.Facility != null
                && (station.Facility.SupportsWork(BuiltInWorkTypeIds.Craft)
                    || station.Facility.SupportsWork(BuiltInWorkTypeIds.Cook)
                    || station.Facility.SupportsWork(BuiltInWorkTypeIds.Quarry)
                    || station.Facility.SupportsWork(BuiltInWorkTypeIds.Sow)
                    || station.Facility.SupportsWork(BuiltInWorkTypeIds.Harvest))),
            "a production station has no production work type");
        Require(stations.All(station =>
                !string.IsNullOrWhiteSpace(station.GetPrimarySemanticTag())),
            "a production station has no recipe facility tag");

        ResearchProjectSO[] projects = AssetDatabase
            .FindAssets(
                "t:ResearchProjectSO",
                new[] { "Assets/Resources/SO/Research/Projects" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResearchProjectSO>)
            .Where(project => project != null)
            .ToArray();
        Require(projects.Length >= 78, $"research projects={projects.Length}");

        HashSet<int> stationIds = stations.Select(station => station.id).ToHashSet();
        int unlockedStationCount = projects
            .SelectMany(project => project.Unlocks)
            .OfType<BlueprintBuildingUnlock>()
            .Select(unlock => unlock.buildingId)
            .Where(stationIds.Contains)
            .Distinct()
            .Count();
        Require(unlockedStationCount == stations.Length,
            $"research station unlocks={unlockedStationCount}/{stations.Length}");

        ResourceEconomyContentCatalog catalog = LoadCatalog();
        Require(
            catalog.Items.Count
                >= ResourceEconomyAssetBuilder.ExpectedItemCount
                    + ProductionWorkshopContentAssetBuilder
                        .ExpectedWorkshopItemCount,
            $"resource items={catalog.Items.Count}");
        Require(
            catalog.Recipes.Count
                >= ResourceEconomyAssetBuilder.ExpectedRecipeCount
                    + ProductionWorkshopContentAssetBuilder
                        .ExpectedWorkshopRecipeCount,
            $"production recipes={catalog.Recipes.Count}");
        Require(
            catalog.Crops.Count == ResourceEconomyAssetBuilder.ExpectedCropCount + 4,
            $"crops={catalog.Crops.Count}");
        Require(
            catalog.Materials.Count == ResourceEconomyAssetBuilder.ExpectedMaterialCount,
            $"materials={catalog.Materials.Count}");
        Require(
            catalog.Substances.Count == ResourceEconomyAssetBuilder.ExpectedSubstanceCount,
            $"substances={catalog.Substances.Count}");

        HashSet<string> stationTags = stations
            .SelectMany(station => station.GetSemanticTags())
            .Concat(buildings.SelectMany(building => building.GetSemanticTags()))
            .ToHashSet(StringComparer.Ordinal);
        string[] missingTags = catalog.Recipes
            .Where(recipe => recipe.RecipeId.StartsWith(
                "recipe:",
                StringComparison.Ordinal))
            .Select(recipe => recipe.FacilityTag)
            .Where(tag => !string.IsNullOrWhiteSpace(tag)
                && !stationTags.Contains(tag))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();
        Require(missingTags.Length == 0,
            $"recipes without facilities: {string.Join(", ", missingTags)}");

        string[] wasteRecipeIds =
        {
            "recipe:compost-plant",
            "recipe:compost-animal",
            "recipe:compost-mixed",
            "recipe:low-fuel-plant",
            "recipe:low-fuel-animal",
            "recipe:low-fuel-rot",
            "recipe:rot-toxin",
            "recipe:incinerate-plant",
            "recipe:incinerate-animal",
            "recipe:incinerate-mixed",
            "recipe:incinerate-forbidden"
        };
        string[] missingWasteRecipes = wasteRecipeIds
            .Where(recipeId => !catalog.TryGetRecipe(recipeId, out _))
            .ToArray();
        Require(
            missingWasteRecipes.Length == 0,
            $"missing waste recipes: {string.Join(", ", missingWasteRecipes)}");
    }

    private static void ValidatePhysicalProductionBill()
    {
        ProductionRecipeSO recipe = ScriptableObject.CreateInstance<ProductionRecipeSO>();
        ResourceItemDefinitionSO grain =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        ResourceItemDefinitionSO flour =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        GameObject facilityObject = new GameObject("Production Bill Contract Facility");
        try
        {
            recipe.Configure(
                "test:recipe:flour",
                "시험 제분",
                "정확한 재료와 누적 작업량을 검증한다.",
                "mill",
                BuiltInWorkTypeIds.Craft.Value,
                string.Empty,
                10f,
                new[] { new ItemAmountDefinition("resource:test-grain", 3) },
                new[] { new ProductionOutputDefinition("material:test-flour", 2) });
            recipe.ConfigureWorkshop(
                "workstation:mill",
                Array.Empty<string>(),
                ProductionProcessKind.WorkOnly);
            grain.Configure(
                "resource:test-grain",
                "시험 곡물",
                "생산 주문 계약 검증용 원료",
                StockCategory.General,
                ResourceItemKind.Raw,
                ResourceIngredientTag.Plant,
                1,
                1f,
                100,
                string.Empty);
            flour.Configure(
                "material:test-flour",
                "시험 밀가루",
                "생산 주문 계약 검증용 중간재",
                StockCategory.General,
                ResourceItemKind.Intermediate,
                ResourceIngredientTag.Plant,
                1,
                1f,
                100,
                string.Empty);
            ResourceEconomyContentCatalog catalog =
                new ResourceEconomyContentCatalog(
                    new[] { grain, flour },
                    new[] { recipe },
                    Array.Empty<CropDefinitionSO>(),
                    Array.Empty<CraftMaterialDefinitionSO>());

            BuildingAbilityCollection abilities = new BuildingAbilityCollection();
            abilities.Add(new BuildingFacilityAbility
            {
                settings = CreateCraftFacilityData()
            });
            abilities.Add(new BuildingSemanticTagsAbility
            {
                tags = new[] { "mill" }
            });
            abilities.Add(new BuildingProductionWorkstationAbility
            {
                workstationTag = "workstation:mill"
            });
            building.id = 99101;
            building.objectName = "시험 제분소";
            building.ReplaceAbilities(abilities);

            BuildableObject facility =
                facilityObject.AddComponent<BuildableObject>();
            facility.ConstructPersistentIdentity(
                new GuidPersistentIdGenerator());
            CharacterAiEditorTestDependencies.Inject(facility);
            Require(
                facility.PersistentInstanceId.IsValid,
                "production fixture building identity was not assigned");
            facility.Initialization(building, new Vector2Int(7, 3));

            FakeProductionItemGateway items = new FakeProductionItemGateway();
            ProductionRuntimeFixture runtime = CreateRuntime(
                catalog,
                items,
                seed: 771);
            ProductionBillCommandResult added = runtime.AddBill(
                facility,
                recipe.RecipeId,
                ProductionOrderMode.RepeatCount,
                2);
            Require(added.Succeeded, added.Failure.Code.ToString());
            Require(items.GetRequested("resource:test-grain") == 3,
                "exact input delivery was not requested");
            Require(!runtime.CheckWorkAvailability(
                    facility,
                    BuiltInWorkTypeIds.Craft).Available,
                "production became runnable before delivery");

            items.Deliver(
                "resource:wrong-grain",
                3,
                ProductionBillRuntime.DestinationPrefix + added.BillId);
            Require(!runtime.CheckWorkAvailability(
                    facility,
                    BuiltInWorkTypeIds.Craft).Available,
                "a different item satisfied an exact recipe input");

            items.Deliver(
                "resource:test-grain",
                3,
                ProductionBillRuntime.DestinationPrefix + added.BillId);
            ProductionWorkAvailabilityResult availability =
                runtime.CheckWorkAvailability(
                    facility,
                    BuiltInWorkTypeIds.Craft);
            Require(availability.Available,
                $"delivered production did not become runnable: {availability.Failure.Code}");
            ProductionWorkBeginResult begin = runtime.BeginWork(
                null,
                facility,
                BuiltInWorkTypeIds.Craft);
            Require(begin.Succeeded,
                $"could not begin production: {begin.Failure.Code}");
            ProductionBillSnapshot started = begin.Bill;
            Require(items.GetDelivered("resource:test-grain") == 0,
                "delivered materials were not consumed at work start");

            ProductionWorkExecutionResult partialWork = runtime.ExecuteWork(
                null,
                facility,
                started.BillId,
                4f);
            Require(partialWork.Succeeded && !partialWork.CycleCompleted,
                "partial work incorrectly completed a cycle");
            ProductionBillsSaveSection saveSection =
                new ProductionBillsSaveSection(runtime.Core);
            string partialSave = saveSection.Capture();

            ProductionRuntimeFixture restored = CreateRuntime(
                catalog,
                items,
                seed: 771);
            ProductionBillsSaveSection restoredSection =
                new ProductionBillsSaveSection(restored.Core);
            DungeonGameRestoreReport restoreReport =
                new DungeonGameRestoreReport();
            restoredSection.Restore(
                partialSave,
                saveSection.SectionVersion,
                restoreReport);
            Require(
                restoreReport.Success,
                $"production save section restore failed: "
                + string.Join(" / ", restoreReport.Errors));
            ProductionBillSnapshot restoredBill =
                restored.GetBills(facility).Single();
            Require(Mathf.Approximately(restoredBill.CompletedWork, 4f),
                $"partial progress was not restored: {restoredBill.CompletedWork}");

            string beforeRejectedRestore = restoredSection.Capture();
            int versionBeforeRejectedRestore = restored.Version;
            DungeonProductionBillSaveData invalidRestore =
                JsonUtility.FromJson<DungeonProductionBillSaveData>(
                    beforeRejectedRestore);
            invalidRestore.bills[0].buildingInstanceId = string.Empty;
            bool invalidRestoreRejected = false;
            try
            {
                restoredSection.StageRestore(
                    JsonUtility.ToJson(invalidRestore),
                    restoredSection.SectionVersion,
                    new DungeonGameRestoreReport());
            }
            catch (InvalidOperationException)
            {
                invalidRestoreRejected = true;
            }
            Require(
                invalidRestoreRejected
                && restored.Version == versionBeforeRejectedRestore
                && string.Equals(
                    restoredSection.Capture(),
                    beforeRejectedRestore,
                    StringComparison.Ordinal),
                "invalid production restore mutated live aggregate state");

            ProductionWorkExecutionResult restoredWork = restored.ExecuteWork(
                null,
                facility,
                restoredBill.BillId,
                6f);
            Require(restoredWork.Succeeded && restoredWork.CycleCompleted,
                "restored production cycle did not complete");
            Require(items.GetAvailable("material:test-flour") == 2,
                "production output was not spawned as a physical stack");
            ProductionBillSnapshot repeated = restored.GetBills(facility).Single();
            Require(repeated.RemainingCycles == 1
                    && Mathf.Approximately(repeated.CompletedWork, 0f)
                    && !repeated.MaterialsConsumed,
                "repeat bill did not reset for its next cycle");
            Require(items.GetRequested("resource:test-grain") == 6,
                "repeat bill did not request the next exact material batch");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(facilityObject);
            UnityEngine.Object.DestroyImmediate(building);
            UnityEngine.Object.DestroyImmediate(grain);
            UnityEngine.Object.DestroyImmediate(flour);
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void ValidatePassiveBatchProduction()
    {
        ProductionRecipeSO recipe =
            ScriptableObject.CreateInstance<ProductionRecipeSO>();
        BuildingSO workstationData =
            ScriptableObject.CreateInstance<BuildingSO>();
        BuildingSO supportData =
            ScriptableObject.CreateInstance<BuildingSO>();
        ResourceItemDefinitionSO[] fixtureItems =
        {
            CreateFixtureItem(
                "test:wort",
                ResourceItemKind.Intermediate,
                StockCategory.General),
            CreateFixtureItem(
                "test:beer",
                ResourceItemKind.FinishedGood,
                StockCategory.Food),
            CreateFixtureItem(
                "test:fuel",
                ResourceItemKind.Raw,
                StockCategory.Fuel),
            CreateFixtureItem(
                "test:rot",
                ResourceItemKind.Waste,
                StockCategory.General)
        };
        GameObject workstationObject = new GameObject(
            "Passive Batch Contract Workstation");
        GameObject supportObject = new GameObject(
            "Passive Batch Contract Support");
        try
        {
            recipe.Configure(
                "test:recipe:fermentation",
                "시험 발효",
                "시간 공정과 저장 복원을 검증한다.",
                "brewery",
                BuiltInWorkTypeIds.Craft.Value,
                string.Empty,
                2f,
                new[] { new ItemAmountDefinition("test:wort", 2) },
                new[] { new ProductionOutputDefinition("test:beer", 2) });
            recipe.ConfigureWorkshop(
                "workstation:test-brewery",
                new[] { "support:test-fermenter" },
                ProductionProcessKind.PassiveBatch,
                "support:test-fermenter",
                prepareWork: 2f,
                finishWork: 1f,
                processGameHours: 12f,
                failedBatchItemId: "test:rot");
            ResourceEconomyContentCatalog catalog =
                new ResourceEconomyContentCatalog(
                    fixtureItems,
                    new[] { recipe },
                    Array.Empty<CropDefinitionSO>(),
                    Array.Empty<CraftMaterialDefinitionSO>());

            BuildingAbilityCollection workstationAbilities =
                new BuildingAbilityCollection();
            workstationAbilities.Add(new BuildingFacilityAbility
            {
                settings = CreateCraftFacilityData()
            });
            workstationAbilities.Add(
                new BuildingProductionWorkstationAbility
                {
                    workstationTag = "workstation:test-brewery"
                });
            workstationData.id = 99201;
            workstationData.objectName = "시험 양조장";
            workstationData.ReplaceAbilities(workstationAbilities);

            BuildingAbilityCollection supportAbilities =
                new BuildingAbilityCollection();
            supportAbilities.Add(new BuildingProductionSupportAbility
            {
                supportId = "support:test-fermenter-instance",
                featureTags = new[] { "support:test-fermenter" },
                compatibleWorkstationTags =
                    new[] { "workstation:test-brewery" },
                kind = ProductionSupportKind.BatchProcessor,
                batchCapacity = 1,
                requiresPower = true,
                requiresFuel = true,
                fuelItemId = "test:fuel",
                fuelPerCycle = 1
            });
            supportData.id = 99202;
            supportData.objectName = "시험 발효조";
            supportData.ReplaceAbilities(supportAbilities);

            BuildableObject workstation =
                workstationObject.AddComponent<BuildableObject>();
            workstation.ConstructPersistentIdentity(
                new GuidPersistentIdGenerator());
            CharacterAiEditorTestDependencies.Inject(workstation);
            workstation.Initialization(
                workstationData,
                new Vector2Int(4, 4));
            BuildableObject support =
                supportObject.AddComponent<BuildableObject>();
            support.ConstructPersistentIdentity(
                new GuidPersistentIdGenerator());
            CharacterAiEditorTestDependencies.Inject(support);
            support.Initialization(
                supportData,
                new Vector2Int(5, 4));

            FakeProductionItemGateway items =
                new FakeProductionItemGateway();
            MutableGameClock clock = new MutableGameClock();
            MutablePowerRuntime power = new MutablePowerRuntime();
            FakeProductionWorkshop workshop =
                new FakeProductionWorkshop(workstation, support);
            ProductionRuntimeFixture runtime = CreateRuntime(
                catalog,
                items,
                seed: 772,
                workshops: workshop,
                buildingWorld: new FixedBuildingWorldQuery(
                    workstation,
                    support),
                power: power,
                clock: clock);
            ProductionBillCommandResult added = runtime.AddBill(
                workstation,
                recipe.RecipeId,
                ProductionOrderMode.RepeatCount,
                1);
            Require(added.Succeeded, added.Failure.Code.ToString());
            string destination =
                ProductionBillRuntime.DestinationPrefix + added.BillId;
            items.Deliver("test:wort", 2, destination);
            items.Deliver("test:fuel", 1, destination);
            ProductionWorkBeginResult passiveBegin = runtime.BeginWork(
                null,
                workstation,
                BuiltInWorkTypeIds.Craft);
            Require(passiveBegin.Succeeded,
                $"passive batch did not begin: {passiveBegin.Failure.Code}");
            ProductionBillSnapshot prepared = passiveBegin.Bill;
            ProductionWorkExecutionResult preparation = runtime.ExecuteWork(
                null,
                workstation,
                prepared.BillId,
                2f);
            Require(preparation.Succeeded && !preparation.CycleCompleted,
                "preparation incorrectly emitted the final product");
            ProductionBillSnapshot processing =
                runtime.GetBills(workstation).Single();
            Require(
                processing.BatchStage == ProductionBatchStage.Processing
                && Mathf.Approximately(
                    processing.RemainingProcessingHours,
                    12f)
                && items.GetAvailable("test:beer") == 0,
                "batch did not occupy its passive processing stage");

            clock.DeltaTimeValue = 45f;
            runtime.Tick();
            string saveJson = new ProductionBillsSaveSection(runtime.Core).Capture();
            ProductionRuntimeFixture restored = CreateRuntime(
                catalog,
                items,
                seed: 772,
                workshops: workshop,
                buildingWorld: new FixedBuildingWorldQuery(
                    workstation,
                    support),
                power: power,
                clock: clock);
            DungeonGameRestoreReport report =
                new DungeonGameRestoreReport();
            new ProductionBillsSaveSection(restored.Core).Restore(
                saveJson,
                DungeonProductionBillSaveData.CurrentVersion,
                report);
            ProductionBillSnapshot halfProcessed =
                restored.GetBills(workstation).Single();
            Require(
                report.Success
                && Mathf.Approximately(
                    halfProcessed.RemainingProcessingHours,
                    6f),
                "passive processing time did not save and restore");

            power.Powered = false;
            restored.Tick();
            ProductionBillSnapshot gracePaused =
                restored.GetBills(workstation).Single();
            Require(
                Mathf.Approximately(
                    gracePaused.RemainingProcessingHours,
                    6f)
                && Mathf.Approximately(gracePaused.BatchIntegrity, 100f)
                && Mathf.Approximately(
                    gracePaused.UtilityOutageHours,
                    6f),
                "six-hour utility grace did not preserve progress and integrity");
            clock.DeltaTimeValue = 0.75f;
            restored.Tick();
            ProductionBillSnapshot decaying =
                restored.GetBills(workstation).Single();
            Require(
                decaying.BatchIntegrity < 100f
                && decaying.BatchIntegrity > 99f,
                "integrity did not start decaying after the utility grace");

            power.Powered = true;
            clock.DeltaTimeValue = 45f;
            restored.Tick();
            ProductionBillSnapshot finishing =
                restored.GetBills(workstation).Single();
            Require(
                finishing.BatchStage == ProductionBatchStage.Finishing
                && items.GetAvailable("test:beer") == 0,
                "processing completion did not wait for finishing work");
            ProductionWorkBeginResult finishBegin = restored.BeginWork(
                null,
                workstation,
                BuiltInWorkTypeIds.Craft);
            Require(finishBegin.Succeeded,
                $"finishing work did not become runnable: {finishBegin.Failure.Code}");
            ProductionBillSnapshot finishingWork = finishBegin.Bill;
            ProductionWorkExecutionResult finishWork = restored.ExecuteWork(
                null,
                workstation,
                finishingWork.BillId,
                1f);
            Require(finishWork.Succeeded
                && finishWork.CycleCompleted
                && items.GetAvailable("test:beer") == 2,
                "finishing work did not emit a physical final product");
            Require(
                items.GetRequested("test:wort") == 2,
                "passive production generated an automatic downstream order");

            ProductionBillCommandResult degraded = restored.AddBill(
                workstation,
                recipe.RecipeId,
                ProductionOrderMode.RepeatCount,
                1);
            Require(degraded.Succeeded, degraded.Failure.Code.ToString());
            string degradedDestination =
                ProductionBillRuntime.DestinationPrefix + degraded.BillId;
            items.Deliver("test:wort", 2, degradedDestination);
            items.Deliver("test:fuel", 1, degradedDestination);
            ProductionWorkBeginResult degradedBegin = restored.BeginWork(
                null,
                workstation,
                BuiltInWorkTypeIds.Craft);
            Require(degradedBegin.Succeeded,
                $"degraded batch did not begin: {degradedBegin.Failure.Code}");
            ProductionBillSnapshot degradedPreparation = degradedBegin.Bill;
            Require(restored.ExecuteWork(
                    null,
                    workstation,
                    degradedPreparation.BillId,
                    2f).Succeeded,
                "degraded batch preparation failed");
            power.Powered = false;
            clock.DeltaTimeValue = 7.5f * 16.2f;
            restored.Tick();
            ProductionBillSnapshot degradedProcessing = restored
                .GetBills(workstation)
                .Single(bill => bill.BillId == degraded.BillId);
            Require(
                degradedProcessing.BatchIntegrity < 50f
                && degradedProcessing.BatchIntegrity > 48f,
                "utility outage did not reach the half-yield integrity band");
            power.Powered = true;
            clock.DeltaTimeValue = 7.5f * 12f;
            restored.Tick();
            ProductionWorkBeginResult degradedFinish = restored.BeginWork(
                null,
                workstation,
                BuiltInWorkTypeIds.Craft);
            Require(degradedFinish.Succeeded,
                $"degraded finishing did not begin: {degradedFinish.Failure.Code}");
            ProductionBillSnapshot degradedFinishing = degradedFinish.Bill;
            ProductionWorkExecutionResult degradedFinishWork =
                restored.ExecuteWork(
                    null,
                    workstation,
                    degradedFinishing.BillId,
                    1f);
            Require(degradedFinishWork.Succeeded
                && degradedFinishWork.CycleCompleted
                && items.GetAvailable("test:beer") == 3,
                "integrity below 50 did not halve the physical output");

            ProductionBillCommandResult ruined = restored.AddBill(
                workstation,
                recipe.RecipeId,
                ProductionOrderMode.RepeatCount,
                1);
            Require(ruined.Succeeded, ruined.Failure.Code.ToString());
            string ruinedDestination =
                ProductionBillRuntime.DestinationPrefix + ruined.BillId;
            items.Deliver("test:wort", 2, ruinedDestination);
            items.Deliver("test:fuel", 1, ruinedDestination);
            ProductionWorkBeginResult ruinedBegin = restored.BeginWork(
                null,
                workstation,
                BuiltInWorkTypeIds.Craft);
            Require(ruinedBegin.Succeeded,
                $"ruined batch did not begin: {ruinedBegin.Failure.Code}");
            ProductionBillSnapshot ruinedPreparation = ruinedBegin.Bill;
            Require(restored.ExecuteWork(
                    null,
                    workstation,
                    ruinedPreparation.BillId,
                    2f).Succeeded,
                "ruined batch preparation failed");
            power.Powered = false;
            clock.DeltaTimeValue = 7.5f * 26f;
            restored.Tick();
            Require(
                restored.GetBills(workstation)
                    .All(bill => bill.BillId != ruined.BillId)
                && items.GetAvailable("test:rot") == 2
                && items.GetAvailable("test:beer") == 3
                && items.GetRequested("test:fuel") == 3,
                "zero-integrity batch did not become matching physical rot");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(workstationObject);
            UnityEngine.Object.DestroyImmediate(supportObject);
            UnityEngine.Object.DestroyImmediate(workstationData);
            UnityEngine.Object.DestroyImmediate(supportData);
            foreach (ResourceItemDefinitionSO fixtureItem in fixtureItems)
            {
                UnityEngine.Object.DestroyImmediate(fixtureItem);
            }
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void ValidateEconomyPlanning()
    {
        ResourceStockPolicyData normalized = new ResourceStockPolicyData
        {
            itemId = " material:iron-ingot ",
            enabled = true,
            minimumStock = 30,
            targetStock = 10,
            maximumStock = 5,
            surplusDisposition = StockSurplusDisposition.Process
        };
        normalized.Normalize();
        Require(
            normalized.itemId == "material:iron-ingot"
            && normalized.targetStock == 30
            && normalized.maximumStock == 30,
            "stock policy thresholds were not normalized");
        ValidateStockPolicySaveBoundary();

        int rawSmall = RegionalSupplyContractSizing.ResolveAmount(
            ResourceItemKind.Raw,
            population: 3,
            completedResearchCount: 12,
            offerIndex: 0);
        int rawLarge = RegionalSupplyContractSizing.ResolveAmount(
            ResourceItemKind.Raw,
            population: 30,
            completedResearchCount: 72,
            offerIndex: 2);
        int finished = RegionalSupplyContractSizing.ResolveAmount(
            ResourceItemKind.FinishedGood,
            population: 30,
            completedResearchCount: 72,
            offerIndex: 2);
        Require(
            rawSmall is >= 20 and <= 80
            && rawLarge is >= 20 and <= 80
            && finished is >= 2 and <= 12,
            "regional contract sizing escaped its content bounds");
        ValidateRegionalSupplyContractSaveBoundary();

        FakeProductionItemGateway items = new FakeProductionItemGateway();
        GrandProjectApplicationAdapter grandProjectAdapter =
            new GrandProjectApplicationAdapter(
                items,
                new EmptyBuildingWorldQuery(),
                new FixedDropZoneQuery(new Vector2Int(4, 1)),
                EmptyResearchRuntimeReferences.Instance,
                workforce: null,
                facilityCandidates: null);
        GrandProjectRuntime runtime = new GrandProjectRuntime(
            grandProjectAdapter,
            grandProjectAdapter,
            new FixedGameClock(),
            aggregateRootStore: new DungeonRuntimeAggregateRootStore());
        DungeonGrandProjectSaveData completedProjects =
            new DungeonGrandProjectSaveData
        {
            state = new GrandProjectRuntimeState
            {
                completedProjectIds = new List<string>
                {
                    GrandProjectRuntime.DeepMiningNetworkId,
                    GrandProjectRuntime.DefenseDistrictId,
                    GrandProjectRuntime.ExpeditionSupplyBaseId,
                    GrandProjectRuntime.RegionalTradePostId
                }
            }
        };
        runtime.PublishRestoreCandidate(
            runtime.BuildRestore(completedProjects));
        Require(
            Mathf.Approximately(
                runtime.GetProductionOutputMultiplier("quarry"),
                1.25f)
            && Mathf.Approximately(runtime.ContractRewardMultiplier, 1.25f)
            && Mathf.Approximately(
                runtime.DefensePreparationMultiplier,
                1.2f)
            && runtime.ExpeditionSupplyCapacityBonus == 12,
            "completed grand-project benefits were not restored");

        GrandProjectSaveSection section = new GrandProjectSaveSection(runtime);
        string json = section.Capture();
        GrandProjectApplicationAdapter restoredGrandProjectAdapter =
            new GrandProjectApplicationAdapter(
                items,
                new EmptyBuildingWorldQuery(),
                new FixedDropZoneQuery(new Vector2Int(4, 1)),
                EmptyResearchRuntimeReferences.Instance,
                workforce: null,
                facilityCandidates: null);
        GrandProjectRuntime restored = new GrandProjectRuntime(
            restoredGrandProjectAdapter,
            restoredGrandProjectAdapter,
            new FixedGameClock(),
            aggregateRootStore: new DungeonRuntimeAggregateRootStore());
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        GrandProjectSaveSection restoredSection =
            new GrandProjectSaveSection(restored);
        restoredSection.Restore(
            json,
            section.SectionVersion,
            report);
        Require(
            report.Success
            && restored.IsCompleted(
                GrandProjectRuntime.DeepMiningNetworkId)
            && restored.IsCompleted(
                GrandProjectRuntime.RegionalTradePostId),
            "grand-project save section did not round-trip");
        object grandProjectSectionContract = section;
        Require(
            grandProjectSectionContract is IDungeonSaveSectionPreflight
            && grandProjectSectionContract is IDungeonRollbackFreeSaveSection
            && grandProjectSectionContract is not IOptionalDungeonSaveSection
            && grandProjectSectionContract is not IDungeonStagedOptionalSaveSection,
            "grand-project save section is not strict and rollback-free");

        DungeonGrandProjectSaveData invalid =
            JsonUtility.FromJson<DungeonGrandProjectSaveData>(json);
        invalid.state.completedProjectIds.Add(
            GrandProjectRuntime.DeepMiningNetworkId);
        invalid.state.completedWork = 1f;
        string beforeInvalid = restoredSection.Capture();
        RequireStrictRejectsWithoutMutation(
            restoredSection,
            JsonUtility.ToJson(invalid),
            beforeInvalid,
            "invalid grand-project payload mutated live state");
        RequireStrictRejectsWithoutMutation(
            restoredSection,
            json,
            restoredSection.SectionVersion - 1,
            beforeInvalid,
            "legacy grand-project section version was accepted");
        RequireStrictRejectsWithoutMutation(
            restoredSection,
            string.Empty,
            restoredSection.SectionVersion,
            beforeInvalid,
            "empty grand-project payload was accepted");
        DungeonGrandProjectSaveData legacyPayload =
            JsonUtility.FromJson<DungeonGrandProjectSaveData>(json);
        legacyPayload.version--;
        RequireStrictRejectsWithoutMutation(
            restoredSection,
            JsonUtility.ToJson(legacyPayload),
            restoredSection.SectionVersion,
            beforeInvalid,
            "legacy grand-project payload version was accepted");
    }

    private static void ValidateStockPolicySaveBoundary()
    {
        ResourceEconomyContentCatalog catalog = LoadCatalog();
        FakeResourceStockPolicyRuntime source =
            new FakeResourceStockPolicyRuntime(catalog);
        ResourceStockPolicySaveSection sourceSection =
            new ResourceStockPolicySaveSection(source, catalog);
        string canonicalJson = sourceSection.Capture();

        FakeResourceStockPolicyRuntime target =
            new FakeResourceStockPolicyRuntime(catalog);
        ResourceStockPolicySaveSection targetSection =
            new ResourceStockPolicySaveSection(target, catalog);
        DungeonGameRestoreReport validReport = new DungeonGameRestoreReport();
        targetSection.Restore(
            canonicalJson,
            targetSection.SectionVersion,
            validReport);
        object sectionContract = targetSection;
        Require(
            validReport.Success
            && target.RestoreCount == 1
            && string.Equals(
                targetSection.Capture(),
                canonicalJson,
                StringComparison.Ordinal)
            && sectionContract is IDungeonSaveSectionPreflight
            && sectionContract is IDungeonRollbackFreeSaveSection
            && sectionContract is not IOptionalDungeonSaveSection
            && sectionContract is not IDungeonStagedOptionalSaveSection,
            "stock-policy save section did not preserve its strict canonical contract");

        DungeonResourceStockPolicySaveData invalid =
            JsonUtility.FromJson<DungeonResourceStockPolicySaveData>(
                canonicalJson);
        invalid.policies[0].minimumStock = -1;
        string beforeInvalid = targetSection.Capture();
        RequireStrictRejectsWithoutMutation(
            targetSection,
            JsonUtility.ToJson(invalid),
            beforeInvalid,
            "invalid stock-policy payload mutated live state");
        RequireStrictRejectsWithoutMutation(
            targetSection,
            canonicalJson,
            targetSection.SectionVersion - 1,
            beforeInvalid,
            "legacy stock-policy section version was accepted");
        RequireStrictRejectsWithoutMutation(
            targetSection,
            string.Empty,
            targetSection.SectionVersion,
            beforeInvalid,
            "empty stock-policy payload was accepted");
        DungeonResourceStockPolicySaveData legacyPayload =
            JsonUtility.FromJson<DungeonResourceStockPolicySaveData>(
                canonicalJson);
        legacyPayload.version--;
        RequireStrictRejectsWithoutMutation(
            targetSection,
            JsonUtility.ToJson(legacyPayload),
            targetSection.SectionVersion,
            beforeInvalid,
            "legacy stock-policy payload version was accepted");
        Require(target.RestoreCount == 1,
            "invalid stock-policy payload reached publish");
    }

    private static void ValidateRegionalSupplyContractSaveBoundary()
    {
        ResourceEconomyContentCatalog catalog = LoadCatalog();
        string itemId = catalog.Items
            .Where(item => item != null)
            .OrderBy(item => item.ItemId, StringComparer.Ordinal)
            .First().ItemId;
        DungeonRegionalSupplyContractSaveData canonical =
            new DungeonRegionalSupplyContractSaveData
            {
                currentDay = 1,
                nextOfferDay = 4,
                nextSequence = 2,
                contracts = new List<RegionalSupplyContractState>
                {
                    new RegionalSupplyContractState
                    {
                        contractId = "contract:1:1",
                        title = "Fixture supply contract",
                        regionName = "Fixture region",
                        offeredDay = 1,
                        deadlineDay = 4,
                        rewardGold = 25,
                        status = RegionalSupplyContractStatus.Offered,
                        destinationId = string.Empty,
                        lastStatus = string.Empty,
                        requirements = new List<RegionalSupplyContractRequirement>
                        {
                            new RegionalSupplyContractRequirement
                            {
                                itemId = itemId,
                                amount = 2
                            }
                        }
                    }
                }
            };
        FakeRegionalSupplyContractRuntime source =
            new FakeRegionalSupplyContractRuntime(canonical);
        RegionalSupplyContractSaveSection sourceSection =
            new RegionalSupplyContractSaveSection(source, catalog);
        string canonicalJson = sourceSection.Capture();

        FakeRegionalSupplyContractRuntime target =
            new FakeRegionalSupplyContractRuntime(canonical);
        RegionalSupplyContractSaveSection targetSection =
            new RegionalSupplyContractSaveSection(target, catalog);
        DungeonGameRestoreReport validReport = new DungeonGameRestoreReport();
        targetSection.Restore(
            canonicalJson,
            targetSection.SectionVersion,
            validReport);
        object sectionContract = targetSection;
        Require(
            validReport.Success
            && target.RestoreCount == 1
            && string.Equals(
                targetSection.Capture(),
                canonicalJson,
                StringComparison.Ordinal)
            && sectionContract is IDungeonSaveSectionPreflight
            && sectionContract is IDungeonRollbackFreeSaveSection
            && sectionContract is not IOptionalDungeonSaveSection
            && sectionContract is not IDungeonStagedOptionalSaveSection,
            "regional-contract save section did not preserve its strict canonical contract");

        DungeonRegionalSupplyContractSaveData invalid =
            JsonUtility.FromJson<DungeonRegionalSupplyContractSaveData>(
                canonicalJson);
        invalid.contracts[0].destinationId = "regional-contract:wrong";
        string beforeInvalid = targetSection.Capture();
        RequireStrictRejectsWithoutMutation(
            targetSection,
            JsonUtility.ToJson(invalid),
            beforeInvalid,
            "invalid regional-contract payload mutated live state");
        RequireStrictRejectsWithoutMutation(
            targetSection,
            canonicalJson,
            targetSection.SectionVersion - 1,
            beforeInvalid,
            "legacy regional-contract section version was accepted");
        RequireStrictRejectsWithoutMutation(
            targetSection,
            string.Empty,
            targetSection.SectionVersion,
            beforeInvalid,
            "empty regional-contract payload was accepted");
        DungeonRegionalSupplyContractSaveData legacyPayload =
            JsonUtility.FromJson<DungeonRegionalSupplyContractSaveData>(
                canonicalJson);
        legacyPayload.version--;
        RequireStrictRejectsWithoutMutation(
            targetSection,
            JsonUtility.ToJson(legacyPayload),
            targetSection.SectionVersion,
            beforeInvalid,
            "legacy regional-contract payload version was accepted");
        Require(target.RestoreCount == 1,
            "invalid regional-contract payload reached publish");
    }

    private static void ValidateEconomyPlanningLateFailureDiscard()
    {
        ResourceEconomyContentCatalog catalog = LoadCatalog();
        DungeonRuntimeAggregateRootStore aggregateRootStore = new();
        FakeProductionItemGateway items = new();
        GrandProjectApplicationAdapter grandProjectAdapter =
            new GrandProjectApplicationAdapter(
                items,
                new EmptyBuildingWorldQuery(),
                new FixedDropZoneQuery(new Vector2Int(4, 1)),
                EmptyResearchRuntimeReferences.Instance,
                workforce: null,
                facilityCandidates: null);
        GrandProjectRuntime grandProjects = new GrandProjectRuntime(
            grandProjectAdapter,
            grandProjectAdapter,
            new FixedGameClock(),
            aggregateRootStore: aggregateRootStore);
        FakeResourceStockPolicyRuntime stockPolicies =
            new FakeResourceStockPolicyRuntime(catalog, aggregateRootStore);
        DungeonRegionalSupplyContractSaveData emptyContracts =
            new DungeonRegionalSupplyContractSaveData
            {
                currentDay = 1,
                nextOfferDay = 1,
                nextSequence = 1,
                contracts = new List<RegionalSupplyContractState>()
            };
        FakeRegionalSupplyContractRuntime regionalContracts =
            new FakeRegionalSupplyContractRuntime(
                emptyContracts,
                aggregateRootStore);

        GrandProjectSaveSection grandProjectSection =
            new GrandProjectSaveSection(grandProjects);
        ResourceStockPolicySaveSection stockPolicySection =
            new ResourceStockPolicySaveSection(stockPolicies, catalog);
        RegionalSupplyContractSaveSection regionalSection =
            new RegionalSupplyContractSaveSection(regionalContracts, catalog);

        RequiredDependencyStubSection physicalItems = new(
            PhysicalItemsSaveSection.Id,
            DungeonSaveRestorePhase.Items);
        RequiredDependencyStubSection modularFacilities = new(
            ModularFacilityWorldSaveSection.Id,
            DungeonSaveRestorePhase.World);
        RequiredDependencyStubSection productionBills = new(
            ProductionBillsSaveSection.Id,
            DungeonSaveRestorePhase.RuntimeState);
        FinalFailingSection finalFailure = new(new[]
        {
            GrandProjectSaveSection.Id,
            ResourceStockPolicySaveSection.Id,
            RegionalSupplyContractSaveSection.Id
        });
        IDungeonSaveSection[] sections =
        {
            modularFacilities,
            physicalItems,
            productionBills,
            grandProjectSection,
            stockPolicySection,
            regionalSection,
            finalFailure
        };
        DungeonSaveSectionRegistry registry = new(
            sections,
            aggregateRootStore);

        string grandProjectBefore = grandProjectSection.Capture();
        string stockPolicyBefore = stockPolicySection.Capture();
        string regionalBefore = regionalSection.Capture();
        int revisionBefore = aggregateRootStore.PublishedRestoreRevision;

        DungeonGrandProjectSaveData incomingGrandProject =
            JsonUtility.FromJson<DungeonGrandProjectSaveData>(
                grandProjectBefore);
        incomingGrandProject.state.lastStatus = "incoming";
        DungeonResourceStockPolicySaveData incomingStockPolicy =
            JsonUtility.FromJson<DungeonResourceStockPolicySaveData>(
                stockPolicyBefore);
        incomingStockPolicy.policies[0].lastStatus = "incoming";
        DungeonRegionalSupplyContractSaveData incomingRegional =
            CreateRegionalSupplyContractFixture(catalog);

        Dictionary<string, string> payloadById = new(StringComparer.Ordinal)
        {
            [grandProjectSection.SectionId] =
                JsonUtility.ToJson(incomingGrandProject),
            [stockPolicySection.SectionId] =
                JsonUtility.ToJson(incomingStockPolicy),
            [regionalSection.SectionId] = JsonUtility.ToJson(incomingRegional)
        };
        List<DungeonSaveSectionEnvelope> envelopes = sections
            .Select(section => new DungeonSaveSectionEnvelope
            {
                sectionId = section.SectionId,
                sectionVersion = section.SectionVersion,
                restorePhase = section.RestorePhase,
                optional = false,
                payloadJson = payloadById.TryGetValue(
                    section.SectionId,
                    out string payload)
                    ? payload
                    : section.Capture()
            })
            .ToList();
        DungeonGameRestoreReport report = new();
        bool restored = registry.RestoreAll(envelopes, report);

        Require(
            !restored
            && !report.Success
            && finalFailure.WasCommitted
            && aggregateRootStore.PublishedRestoreRevision == revisionBefore
            && string.Equals(
                grandProjectSection.Capture(),
                grandProjectBefore,
                StringComparison.Ordinal)
            && string.Equals(
                stockPolicySection.Capture(),
                stockPolicyBefore,
                StringComparison.Ordinal)
            && string.Equals(
                regionalSection.Capture(),
                regionalBefore,
                StringComparison.Ordinal),
            "economy planning late failure leaked a staged Aggregate state");
    }

    private static DungeonRegionalSupplyContractSaveData
        CreateRegionalSupplyContractFixture(
            IResourceEconomyContentCatalog catalog)
    {
        string itemId = catalog.Items
            .Where(item => item != null)
            .OrderBy(item => item.ItemId, StringComparer.Ordinal)
            .First().ItemId;
        return new DungeonRegionalSupplyContractSaveData
        {
            currentDay = 1,
            nextOfferDay = 4,
            nextSequence = 2,
            contracts = new List<RegionalSupplyContractState>
            {
                new RegionalSupplyContractState
                {
                    contractId = "contract:1:1",
                    title = "Fixture supply contract",
                    regionName = "Fixture region",
                    offeredDay = 1,
                    deadlineDay = 4,
                    rewardGold = 25,
                    status = RegionalSupplyContractStatus.Offered,
                    destinationId = string.Empty,
                    lastStatus = string.Empty,
                    requirements = new List<RegionalSupplyContractRequirement>
                    {
                        new RegionalSupplyContractRequirement
                        {
                            itemId = itemId,
                            amount = 2
                        }
                    }
                }
            }
        };
    }

    private static FacilityData CreateCraftFacilityData()
    {
        FacilityData data = new FacilityData
        {
            requiredWorkers = 1
        };
        data.SetSupportedWorkTypeIds(new[] { BuiltInWorkTypeIds.Craft });
        return data;
    }

    private static ResourceItemDefinitionSO CreateFixtureItem(
        string itemId,
        ResourceItemKind kind,
        StockCategory category)
    {
        ResourceItemDefinitionSO item =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        item.Configure(
            itemId,
            itemId,
            "생산 계약 검증용 아이템",
            category,
            kind,
            ResourceIngredientTag.None,
            1,
            1f,
            100,
            string.Empty);
        return item;
    }

    private static ResourceEconomyContentCatalog LoadCatalog()
    {
        return new ResourceEconomyContentCatalog(
            LoadAll<ResourceItemDefinitionSO>("Assets/Resources/SO/Economy/Items"),
            LoadAll<ProductionRecipeSO>("Assets/Resources/SO/Economy/Recipes"),
            LoadAll<CropDefinitionSO>("Assets/Resources/SO/Economy/Crops"),
            LoadAll<CraftMaterialDefinitionSO>("Assets/Resources/SO/Economy/Materials"));
    }

    private static T[] LoadAll<T>(string root)
        where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .ToArray();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void RequireStrictRejectsWithoutMutation(
        IDungeonSaveSection section,
        string payloadJson,
        string before,
        string message)
    {
        RequireStrictRejectsWithoutMutation(
            section,
            payloadJson,
            section.SectionVersion,
            before,
            message);
    }

    private static void RequireStrictRejectsWithoutMutation(
        IDungeonSaveSection section,
        string payloadJson,
        int sectionVersion,
        string before,
        string message)
    {
        bool rejected = false;
        try
        {
            ((IDungeonStagedSaveSection)section).StageRestore(
                payloadJson,
                sectionVersion,
                new DungeonGameRestoreReport());
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(
            rejected
            && string.Equals(section.Capture(), before, StringComparison.Ordinal),
            message);
    }

    private static ProductionRuntimeFixture CreateRuntime(
        IResourceEconomyContentCatalog catalog,
        IProductionItemGateway items,
        int seed,
        IProductionWorkshopRuntime workshops = null,
        IBuildingWorldQuery buildingWorld = null,
        IPowerInfrastructureQuery power = null,
        IGameClock clock = null)
    {
        workshops ??= EmptyProductionWorkshopRuntime.Instance;
        buildingWorld ??= new FixedBuildingWorldQuery();
        power ??= new MutablePowerRuntime();
        clock ??= new MutableGameClock();
        NoOpWorkforceReplanService workforce =
            NoOpWorkforceReplanService.Instance;
        IProductionInputLogisticsService inputLogistics =
            new ProductionInputLogisticsService(
                catalog,
                items,
                EmptyResearchRuntimeReferences.Instance,
                workforce,
                workshops);
        IProductionCycleUtilityService cycleUtilities =
            new TestProductionCycleUtilityService(workshops, power);
        IProductionAssemblyBridge bridge = new ProductionAssemblyBridgeAdapter(
            items,
            items as IProductionOutputBufferGateway
                ?? throw new InvalidOperationException(
                    "Production test item gateway must expose an output buffer."),
            inputLogistics,
            cycleUtilities,
            workshops,
            buildingWorld,
            EmptyWarehouseWorldQuery.Instance,
            workforce,
            Array.Empty<IProductionOutputHandler>());
        IProductionOutputPlanningService outputPlanning =
            new ProductionOutputPlanningService(catalog, bridge);
        IProductionOutputExecutionService outputExecution =
            new ProductionOutputExecutionService(
                bridge,
                EmptyGrandProjectBenefitQuery.Instance,
                outputPlanning,
                new RandomStreamProvider(seed));
        ProductionAggregateStateStore stateStore =
            new ProductionAggregateStateStore(
                new DungeonRuntimeAggregateRootStore());
        IProductionStockSensorRuntime stockSensors =
            new ProductionStockSensorRuntime(
                bridge,
                stateStore);
        IProductionBillSnapshotProjector snapshots =
            new ProductionBillSnapshotProjector(
                catalog,
                bridge,
                outputPlanning,
                stockSensors,
                EmptyProductionDistributionQuery.Instance);
        ProductionBillOrderDependencies order = new(
            catalog,
            bridge,
            stockSensors,
            stateStore);
        ProductionBillExecutionDependencies execution = new(
            outputPlanning,
            outputExecution,
            snapshots,
            bridge,
            clock);
        ProductionBillRuntime core = new(order, execution);
        ProductionBillSceneFacade scene = new(core, core, core, bridge);
        return new ProductionRuntimeFixture(core, scene);
    }

    private sealed class ProductionRuntimeFixture
    {
        private readonly ProductionBillSceneFacade scene;

        public ProductionRuntimeFixture(
            ProductionBillRuntime core,
            ProductionBillSceneFacade scene)
        {
            Core = core ?? throw new ArgumentNullException(nameof(core));
            this.scene = scene ?? throw new ArgumentNullException(nameof(scene));
        }

        public ProductionBillRuntime Core { get; }
        public int Version => scene.Version;

        public ProductionBillCommandResult AddBill(
            BuildableObject facility,
            string recipeId,
            ProductionOrderMode mode,
            int amount) => scene.AddBill(facility, recipeId, mode, amount);

        public IReadOnlyList<ProductionBillSnapshot> GetBills(
            BuildableObject facility) => scene.GetBills(facility);

        public ProductionWorkAvailabilityResult CheckWorkAvailability(
            BuildableObject facility,
            WorkTypeId workTypeId) =>
            scene.CheckWorkAvailability(facility, workTypeId);

        public ProductionWorkBeginResult BeginWork(
            CharacterActor worker,
            BuildableObject facility,
            WorkTypeId workTypeId) =>
            scene.BeginWork(worker, facility, workTypeId);

        public ProductionWorkExecutionResult ExecuteWork(
            CharacterActor worker,
            BuildableObject facility,
            ProductionBillId billId,
            float amount) =>
            scene.ExecuteWork(worker, facility, billId, amount);

        public void Tick() => Core.Tick();
    }

    private sealed class EmptyGrandProjectBenefitQuery :
        IGrandProjectBenefitQuery
    {
        public static readonly EmptyGrandProjectBenefitQuery Instance = new();

        public float ContractRewardMultiplier => 1f;
        public float DefensePreparationMultiplier => 1f;
        public int ExpeditionSupplyCapacityBonus => 0;

        public bool IsCompleted(string projectId) => false;
        public float GetProductionOutputMultiplier(string facilityTag) => 1f;
    }

    private sealed class EmptyProductionDistributionQuery :
        IProductionDistributionQuery
    {
        public static readonly EmptyProductionDistributionQuery Instance = new();

        public IReadOnlyList<ProductionConsumerRouteState> GetRouteStates(
            ProductionBillId billId) =>
            Array.Empty<ProductionConsumerRouteState>();
    }

    private static class EmptyResearchRuntimeReferences
    {
        private static BlueprintResearchRuntime runtime;

        public static ProgressionSceneRuntimeReferences Instance
        {
            get
            {
                if (runtime == null)
                {
                    GameObject host = new GameObject("EmptyResearchRuntime")
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    runtime = host.AddComponent<BlueprintResearchRuntime>();
                    runtime.enabled = false;
                }

                return new ProgressionSceneRuntimeReferences(null, runtime, null);
            }
        }
    }

    private sealed class NoOpWorkforceReplanService :
        IWorkforceReplanService
    {
        public static readonly NoOpWorkforceReplanService Instance = new();

        public void RequestIdleWorkersToReplan(bool clearFailures = true)
        {
        }

        public void RequestOneWorkerToReplanFor(
            WorkTypeId workTypeId,
            bool clearFailures = true,
            bool forceInterrupt = false)
        {
        }

        public void RequestOneHaulerToReplan(
            bool clearFailures = true,
            bool forceInterrupt = false)
        {
        }
    }

    private sealed class EmptyProductionWorkshopRuntime :
        IProductionWorkshopRuntime
    {
        public static readonly EmptyProductionWorkshopRuntime Instance = new();

        public int Version => 0;

        public IReadOnlyList<ProductionSupportLinkSnapshot> GetLinks(
            BuildableObject workstation) =>
            Array.Empty<ProductionSupportLinkSnapshot>();

        public bool TryGetLinkForSupport(
            BuildableObject support,
            out ProductionSupportLinkSnapshot link)
        {
            link = null;
            return false;
        }

        public bool HasRequiredSupports(
            BuildableObject workstation,
            IReadOnlyList<string> requiredFeatureTags,
            out string failureReason)
        {
            bool valid = requiredFeatureTags == null
                || requiredFeatureTags.All(string.IsNullOrWhiteSpace);
            failureReason = valid ? string.Empty : "test-support-missing";
            return valid;
        }

        public bool TryResolveSupport(
            BuildableObject workstation,
            string featureTag,
            ProductionSupportKind? requiredKind,
            out BuildableObject support,
            out BuildingProductionSupportAbility ability)
        {
            support = null;
            ability = null;
            return false;
        }
    }

    private sealed class TestProductionCycleUtilityService :
        IProductionCycleUtilityService
    {
        private readonly IProductionWorkshopRuntime workshops;
        private readonly IPowerInfrastructureQuery power;

        public TestProductionCycleUtilityService(
            IProductionWorkshopRuntime workshops,
            IPowerInfrastructureQuery power)
        {
            this.workshops = workshops;
            this.power = power;
        }

        public bool ValidateCycleRequirements(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            BuildableObject facility,
            IReadOnlyList<ProductionBillRecord> allBills,
            out string failureReason)
        {
            if (!workshops.HasRequiredSupports(
                    facility,
                    recipe.RequiredSupportTags,
                    out failureReason))
            {
                return false;
            }

            if (recipe.ProcessKind != ProductionProcessKind.PassiveBatch)
            {
                return true;
            }

            return TryResolveBatchSupport(
                recipe,
                facility,
                out _,
                out failureReason);
        }

        public bool ValidateProcessingUtilities(
            string occupiedSupportNodeId,
            ProductionRecipeSO recipe,
            BuildableObject facility,
            out string failureReason)
        {
            BuildableObject support = ResolveOccupiedBatchSupport(
                occupiedSupportNodeId,
                facility);
            failureReason = support == null || power.IsPowered(support)
                ? string.Empty
                : "test-support-power-off";
            return string.IsNullOrEmpty(failureReason);
        }

        public bool TryConsumeCycleUtilities(
            ProductionRecipeSO recipe,
            BuildableObject facility,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryResolveBatchSupport(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            BuildableObject facility,
            IReadOnlyList<ProductionBillRecord> allBills,
            out string supportNodeId,
            out string failureReason)
        {
            supportNodeId = string.Empty;
            if (!TryResolveBatchSupport(
                    recipe,
                    facility,
                    out BuildableObject support,
                    out failureReason))
            {
                return false;
            }

            supportNodeId = support.RequirePersistentInstanceId().Value;
            return true;
        }

        public float ResolveTemperatureSpeed(
            ProductionRecipeSO recipe,
            BuildableObject facility,
            out bool dangerous)
        {
            dangerous = false;
            return 1f;
        }

        public BuildableObject ResolveOccupiedBatchSupport(
            string occupiedSupportNodeId,
            BuildableObject facility)
        {
            return workshops.GetLinks(facility)
                .Select(link => link.Support)
                .FirstOrDefault(support => string.Equals(
                    support.RequirePersistentInstanceId().Value,
                    occupiedSupportNodeId,
                    StringComparison.Ordinal));
        }

        private bool TryResolveBatchSupport(
            ProductionRecipeSO recipe,
            BuildableObject facility,
            out BuildableObject support,
            out string failureReason)
        {
            support = workshops.GetLinks(facility)
                .Select(link => link.Support)
                .FirstOrDefault(candidate =>
                {
                    BuildingProductionSupportAbility ability = candidate?
                        .BuildingData.GetProductionSupportAbility();
                    return ability != null
                        && ability.kind == ProductionSupportKind.BatchProcessor
                        && ability.Provides(recipe.BatchSupportTag);
                });
            failureReason = support == null
                ? "test-batch-support-missing"
                : string.Empty;
            return support != null;
        }
    }

    private sealed class FakeProductionItemGateway :
        IProductionItemGateway,
        IProductionOutputBufferGateway
    {
        private readonly Dictionary<string, int> requested =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> cumulativeRequested =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> delivered =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> available =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> buffered =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public int CountDelivered(string itemId, string destinationId) =>
            Get(delivered, Key(itemId, destinationId));

        public int CountPending(string itemId, string destinationId) =>
            Get(requested, Key(itemId, destinationId))
            + Get(delivered, Key(itemId, destinationId));

        public int CountAvailableStock(
            string itemId,
            string excludedDestinationId) =>
            Get(available, itemId);

        public bool RequestDelivery(
            string itemId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requestedAmount,
            out string failureReason)
        {
            requestedAmount = Mathf.Max(0, amount);
            failureReason = string.Empty;
            string key = Key(itemId, destinationId);
            Add(requested, key, requestedAmount);
            Add(cumulativeRequested, key, requestedAmount);
            return requestedAmount > 0;
        }

        public bool ConsumeDelivered(
            string destinationId,
            IReadOnlyDictionary<string, int> costs,
            out string failureReason)
        {
            failureReason = string.Empty;
            foreach (KeyValuePair<string, int> cost in costs)
            {
                if (CountDelivered(cost.Key, destinationId) < cost.Value)
                {
                    failureReason = $"missing {cost.Key}";
                    return false;
                }
            }

            foreach (KeyValuePair<string, int> cost in costs)
            {
                Add(delivered, Key(cost.Key, destinationId), -cost.Value);
            }
            return true;
        }

        public bool SpawnOutput(
            string itemId,
            int amount,
            Vector2Int position)
        {
            Add(available, itemId, amount);
            return true;
        }

        public int CountBufferedOutput(string itemId)
        {
            return buffered
                .Where(pair => pair.Key.StartsWith(
                    itemId + "|",
                    StringComparison.Ordinal))
                .Sum(pair => pair.Value);
        }

        public int CountBufferedOutput(string itemId, string destinationId) =>
            Get(buffered, Key(itemId, destinationId));

        public bool SpawnBufferedOutput(
            string itemId,
            int amount,
            Vector2Int position,
            string destinationId)
        {
            Add(buffered, Key(itemId, destinationId), amount);
            Add(available, itemId, amount);
            return amount > 0;
        }

        public int ReleaseBufferedOutput(
            string destinationId,
            Vector2Int releasePosition)
        {
            int released = 0;
            foreach (string key in buffered.Keys
                         .Where(key => key.EndsWith(
                             "|" + destinationId,
                             StringComparison.Ordinal))
                         .ToArray())
            {
                released += buffered[key];
                buffered.Remove(key);
            }
            return released;
        }

        public bool TryRouteBufferedOutput(
            string sourceDestinationId,
            string itemId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int routed,
            out DomainFailure failure)
        {
            string sourceKey = Key(itemId, sourceDestinationId);
            routed = Mathf.Min(Mathf.Max(0, amount), Get(buffered, sourceKey));
            failure = DomainFailure.None;
            Add(buffered, sourceKey, -routed);
            Add(buffered, Key(itemId, destinationId), routed);
            return routed == amount;
        }

        public void PrioritizeDestination(string destinationId)
        {
        }

        public int ReleaseDestination(
            string destinationId,
            Vector2Int releasePosition) => 0;

        public int RemoveDestination(string destinationId) => 0;

        public void Deliver(string itemId, int amount, string destinationId)
        {
            string key = Key(itemId, destinationId);
            int moved = Mathf.Min(amount, Get(requested, key));
            Add(requested, key, -moved);
            Add(delivered, key, moved);
        }

        public int GetRequested(string itemId)
        {
            return cumulativeRequested
                .Where(pair => pair.Key.StartsWith(
                    itemId + "|",
                    StringComparison.Ordinal))
                .Sum(pair => pair.Value);
        }

        public int GetDelivered(string itemId)
        {
            return delivered
                .Where(pair => pair.Key.StartsWith(
                    itemId + "|",
                    StringComparison.Ordinal))
                .Sum(pair => pair.Value);
        }

        public int GetAvailable(string itemId) => Get(available, itemId);

        private static string Key(string itemId, string destinationId) =>
            $"{itemId}|{destinationId}";

        private static int Get(
            IReadOnlyDictionary<string, int> values,
            string key) =>
            values.TryGetValue(key, out int value) ? value : 0;

        private static void Add(
            IDictionary<string, int> values,
            string key,
            int amount)
        {
            values[key] = Mathf.Max(
                0,
                (values.TryGetValue(key, out int current) ? current : 0)
                + amount);
        }
    }

    private sealed class FakeProductionWorkshop :
        IProductionWorkshopRuntime
    {
        private readonly BuildableObject workstation;
        private readonly BuildableObject support;
        private readonly BuildingProductionSupportAbility ability;

        public FakeProductionWorkshop(
            BuildableObject workstation,
            BuildableObject support)
        {
            this.workstation = workstation;
            this.support = support;
            ability = support.BuildingData.GetProductionSupportAbility();
        }

        public int Version => 1;

        public IReadOnlyList<ProductionSupportLinkSnapshot> GetLinks(
            BuildableObject candidate)
        {
            return candidate == workstation
                ? new[]
                {
                    new ProductionSupportLinkSnapshot
                    {
                        Workstation = workstation,
                        Support = support,
                        WorkstationTag =
                            workstation.GetProductionWorkstationTag(),
                        SupportId = ability.SupportId,
                        FeatureTags = ability.featureTags
                    }
                }
                : Array.Empty<ProductionSupportLinkSnapshot>();
        }

        public bool TryGetLinkForSupport(
            BuildableObject candidate,
            out ProductionSupportLinkSnapshot link)
        {
            link = candidate == support
                ? GetLinks(workstation).Single()
                : null;
            return link != null;
        }

        public bool HasRequiredSupports(
            BuildableObject candidate,
            IReadOnlyList<string> requiredFeatureTags,
            out string failureReason)
        {
            failureReason = string.Empty;
            bool valid = candidate == workstation
                && (requiredFeatureTags ?? Array.Empty<string>())
                    .All(ability.Provides);
            if (!valid)
            {
                failureReason = "missing fake support";
            }
            return valid;
        }

        public bool TryResolveSupport(
            BuildableObject candidate,
            string featureTag,
            ProductionSupportKind? requiredKind,
            out BuildableObject resolvedSupport,
            out BuildingProductionSupportAbility resolvedAbility)
        {
            bool valid = candidate == workstation
                && ability.Provides(featureTag)
                && (!requiredKind.HasValue
                    || ability.kind == requiredKind.Value);
            resolvedSupport = valid ? support : null;
            resolvedAbility = valid ? ability : null;
            return valid;
        }
    }

    private sealed class FixedBuildingWorldQuery : IBuildingWorldQuery
    {
        public FixedBuildingWorldQuery(params BuildableObject[] buildings)
        {
            Buildings = buildings ?? Array.Empty<BuildableObject>();
        }

        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings { get; }
    }

    private sealed class EmptyWarehouseWorldQuery : IWarehouseWorldQuery
    {
        public static readonly EmptyWarehouseWorldQuery Instance = new();

        public int WarehouseVersion => 0;
        public IReadOnlyList<IWarehouseFacility> Warehouses { get; } =
            Array.Empty<IWarehouseFacility>();
    }

    private sealed class MutableGameClock : IGameClock
    {
        public float DeltaTimeValue { get; set; }
        public bool Paused { get; set; }

        public float DeltaTime => DeltaTimeValue;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => Paused;
    }

    private sealed class MutablePowerRuntime : IPowerInfrastructureQuery
    {
        public bool Powered { get; set; } = true;

        public int Version => 1;
        public IReadOnlyList<PowerNetworkSnapshot> Networks { get; } =
            Array.Empty<PowerNetworkSnapshot>();

        public bool IsPowered(BuildableObject building) => Powered;

        public bool TryGetNode(
            BuildableObject building,
            out PowerNodeSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }

        public DungeonPowerInfrastructureSaveData Capture() =>
            new DungeonPowerInfrastructureSaveData();

        public void Restore(DungeonPowerInfrastructureSaveData snapshot)
        {
        }
    }

    private sealed class EmptyBuildingWorldQuery : IBuildingWorldQuery
    {
        public int BuildingVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings { get; } =
            Array.Empty<BuildableObject>();
    }

    private sealed class FixedDropZoneQuery : IWorldDropZoneQuery
    {
        private readonly Vector2Int position;

        public FixedDropZoneQuery(Vector2Int position)
        {
            this.position = position;
        }

        public bool TryGetDeliveryDropoff(out Vector2Int result)
        {
            result = position;
            return true;
        }

        public bool TryGetExpeditionLootDropoff(out Vector2Int result)
        {
            result = position;
            return true;
        }

        public bool TryGetVisitorEntryPoint(
            out WorldGridEntryPoint entryPoint)
        {
            entryPoint = default;
            return false;
        }
    }

    private sealed class FixedGameClock : IGameClock
    {
        public float DeltaTime => 0.02f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class RequiredDependencyStubSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        public RequiredDependencyStubSection(
            string sectionId,
            DungeonSaveRestorePhase restorePhase)
        {
            SectionId = sectionId
                ?? throw new ArgumentNullException(nameof(sectionId));
            RestorePhase = restorePhase;
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase { get; }
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public string Capture() => "{}";

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || string.IsNullOrWhiteSpace(payloadJson))
            {
                report.AddError(
                    $"Invalid prerequisite payload for '{SectionId}'.");
            }
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            ValidatePayload(payloadJson, sectionVersion, report);
            return new DungeonDelegateSaveRestoreStage(SectionId, _ => { });
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }
    }

    private sealed class FinalFailingSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly IReadOnlyList<string> dependencies;

        public FinalFailingSection(IReadOnlyList<string> dependencies)
        {
            this.dependencies = dependencies
                ?? throw new ArgumentNullException(nameof(dependencies));
        }

        public bool WasCommitted { get; private set; }
        public string SectionId => "economy.debug.late-failure";
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.Presentation;
        public IReadOnlyList<string> DependsOn => dependencies;
        public string Capture() => "{}";

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || string.IsNullOrWhiteSpace(payloadJson))
            {
                report.AddError("Injected economy final payload is invalid.");
            }
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            ValidatePayload(payloadJson, sectionVersion, report);
            return new DungeonDelegateSaveRestoreStage(
                SectionId,
                commitReport =>
                {
                    WasCommitted = true;
                    commitReport.AddError(
                        "Injected economy final-section failure.");
                });
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }
    }

    private sealed class FakeResourceStockPolicyRuntime :
        IResourceStockPolicyRuntime
    {
        private readonly DungeonRuntimeAggregateRootStore store;
        private DungeonResourceStockPolicySaveData localState;

        public FakeResourceStockPolicyRuntime(
            IResourceEconomyContentCatalog catalog,
            DungeonRuntimeAggregateRootStore store = null)
        {
            this.store = store;
            State = new DungeonResourceStockPolicySaveData
            {
                policies = catalog.Items
                    .Where(item => item != null)
                    .OrderBy(item => item.ItemId, StringComparer.Ordinal)
                    .Select(item => new ResourceStockPolicyData
                    {
                        itemId = item.ItemId,
                        minimumStock = 10,
                        targetStock = 20,
                        maximumStock = 40,
                        surplusDisposition = StockSurplusDisposition.Hold,
                        lastStatus = string.Empty
                    })
                    .ToList()
            };
        }

        private DungeonResourceStockPolicySaveData State
        {
            get => store != null
                ? store.GetOrCreate(
                    () => new DungeonResourceStockPolicySaveData())
                : localState;
            set
            {
                if (store != null)
                {
                    store.Replace(value);
                }
                else
                {
                    localState = value;
                }
            }
        }

        public int RestoreCount { get; private set; }
        public int Version => RestoreCount;
        public IReadOnlyList<ResourceStockPolicyData> Policies =>
            State.policies;

        public ResourceStockPolicyData GetOrCreate(string itemId) =>
            State.policies.Single(policy => string.Equals(
                policy.itemId,
                itemId,
                StringComparison.Ordinal)).Clone();

        public bool SetPolicy(
            ResourceStockPolicyData policy,
            out string failureReason)
        {
            failureReason = "Fixture runtime is read-only.";
            return false;
        }

        public int CountOwned(string itemId) => 0;

        public DungeonResourceStockPolicySaveData Capture() =>
            Clone(State);

        public ResourceStockPolicyRestoreCandidate PrepareRestoreCandidate(
            DungeonResourceStockPolicySaveData saveData)
        {
            DungeonResourceStockPolicySaveData payload = Clone(saveData);
            ResourceStockPolicyAggregateState candidateState = new();
            foreach (ResourceStockPolicyData policy in payload.policies)
            {
                candidateState.ByItemId.Add(policy.itemId, policy.Clone());
            }
            candidateState.PolicyView = candidateState.ByItemId.Values
                .OrderBy(policy => policy.itemId, StringComparer.Ordinal)
                .ToArray();
            return new ResourceStockPolicyRestoreCandidate(
                candidateState,
                payload);
        }

        public void PublishRestoreCandidate(
            ResourceStockPolicyRestoreCandidate candidate)
        {
            State = candidate.Payload;
            if (store == null)
            {
                RestoreCount++;
            }
        }

        private static DungeonResourceStockPolicySaveData Clone(
            DungeonResourceStockPolicySaveData source) =>
            JsonUtility.FromJson<DungeonResourceStockPolicySaveData>(
                JsonUtility.ToJson(source));
    }

    private sealed class FakeRegionalSupplyContractRuntime :
        IRegionalSupplyContractRuntime
    {
        private readonly DungeonRuntimeAggregateRootStore store;
        private DungeonRegionalSupplyContractSaveData localState;

        public FakeRegionalSupplyContractRuntime(
            DungeonRegionalSupplyContractSaveData initialState,
            DungeonRuntimeAggregateRootStore store = null)
        {
            this.store = store;
            State = Clone(initialState);
        }

        private DungeonRegionalSupplyContractSaveData State
        {
            get => store != null
                ? store.GetOrCreate(
                    () => new DungeonRegionalSupplyContractSaveData())
                : localState;
            set
            {
                if (store != null)
                {
                    store.Replace(value);
                }
                else
                {
                    localState = value;
                }
            }
        }

        public int RestoreCount { get; private set; }
        public int Version => RestoreCount;
        public bool IsUnlocked => true;
        public IReadOnlyList<RegionalSupplyContractState> Contracts =>
            State.contracts;

        public bool Accept(string contractId, out string message)
        {
            message = "Fixture runtime is read-only.";
            return false;
        }

        public bool Decline(string contractId, out string message)
        {
            message = "Fixture runtime is read-only.";
            return false;
        }

        public DungeonRegionalSupplyContractSaveData Capture() =>
            Clone(State);

        public RegionalSupplyContractRestoreCandidate PrepareRestoreCandidate(
            DungeonRegionalSupplyContractSaveData saveData) =>
            new RegionalSupplyContractRestoreCandidate(Clone(saveData));

        public void PublishRestoreCandidate(
            RegionalSupplyContractRestoreCandidate candidate)
        {
            State = candidate.Payload;
            if (store == null)
            {
                RestoreCount++;
            }
        }

        private static DungeonRegionalSupplyContractSaveData Clone(
            DungeonRegionalSupplyContractSaveData source) =>
            JsonUtility.FromJson<DungeonRegionalSupplyContractSaveData>(
                JsonUtility.ToJson(source));
    }
}
#endif
