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
        ValidatePhysicalProductionBill();
        ValidateEconomyPlanning();
        Debug.Log("Production economy contracts passed.");
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
        Require(projects.Length == 78, $"research projects={projects.Length}");

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
            catalog.Items.Count == ResourceEconomyAssetBuilder.ExpectedItemCount,
            $"resource items={catalog.Items.Count}");
        Require(
            catalog.Recipes.Count == ResourceEconomyAssetBuilder.ExpectedRecipeCount,
            $"production recipes={catalog.Recipes.Count}");
        Require(
            catalog.Crops.Count == ResourceEconomyAssetBuilder.ExpectedCropCount,
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
            ResourceEconomyContentCatalog catalog =
                new ResourceEconomyContentCatalog(
                    Array.Empty<ResourceItemDefinitionSO>(),
                    new[] { recipe },
                    Array.Empty<CropDefinitionSO>(),
                    Array.Empty<CraftMaterialDefinitionSO>(),
                    Array.Empty<SubstanceDefinitionSO>());

            BuildingAbilityCollection abilities = new BuildingAbilityCollection();
            abilities.Add(new BuildingFacilityAbility
            {
                settings = CreateCraftFacilityData()
            });
            abilities.Add(new BuildingSemanticTagsAbility
            {
                tags = new[] { "mill" }
            });
            building.id = 99101;
            building.objectName = "시험 제분소";
            building.ReplaceAbilities(abilities);

            BuildableObject facility =
                facilityObject.AddComponent<BuildableObject>();
            facility.Initialization(building, new Vector2Int(7, 3));

            FakeProductionItemGateway items = new FakeProductionItemGateway();
            ProductionBillRuntime runtime = new ProductionBillRuntime(
                catalog,
                items,
                new RandomStreamProvider(771));
            ProductionBillCommandResult added = runtime.AddBill(
                facility,
                recipe.RecipeId,
                ProductionOrderMode.RepeatCount,
                2);
            Require(added.Succeeded, added.Message);
            Require(items.GetRequested("resource:test-grain") == 3,
                "exact input delivery was not requested");
            Require(!runtime.HasWorkAvailable(
                    facility,
                    BuiltInWorkTypeIds.Craft,
                    out _),
                "production became runnable before delivery");

            items.Deliver(
                "resource:wrong-grain",
                3,
                ProductionBillRuntime.DestinationPrefix + added.BillId);
            Require(!runtime.HasWorkAvailable(
                    facility,
                    BuiltInWorkTypeIds.Craft,
                    out _),
                "a different item satisfied an exact recipe input");

            items.Deliver(
                "resource:test-grain",
                3,
                ProductionBillRuntime.DestinationPrefix + added.BillId);
            Require(runtime.HasWorkAvailable(
                    facility,
                    BuiltInWorkTypeIds.Craft,
                    out string readyReason),
                $"delivered production did not become runnable: {readyReason}");
            Require(runtime.TryBeginWork(
                    null,
                    facility,
                    BuiltInWorkTypeIds.Craft,
                    out ProductionBillSnapshot started,
                    out string beginReason),
                $"could not begin production: {beginReason}");
            Require(items.GetDelivered("resource:test-grain") == 0,
                "delivered materials were not consumed at work start");

            Require(runtime.ApplyWork(
                    null,
                    facility,
                    started.BillId,
                    4f,
                    out bool earlyComplete,
                    out _)
                && !earlyComplete,
                "partial work incorrectly completed a cycle");
            ProductionBillsSaveSection saveSection =
                new ProductionBillsSaveSection(runtime);
            string partialSave = saveSection.Capture();

            ProductionBillRuntime restored = new ProductionBillRuntime(
                catalog,
                items,
                new RandomStreamProvider(771));
            ProductionBillsSaveSection restoredSection =
                new ProductionBillsSaveSection(restored);
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

            Require(restored.ApplyWork(
                    null,
                    facility,
                    restoredBill.BillId,
                    6f,
                    out bool completed,
                    out _)
                && completed,
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

        FakeProductionItemGateway items = new FakeProductionItemGateway();
        GrandProjectRuntime runtime = new GrandProjectRuntime(
            items,
            new EmptyBuildingWorldQuery(),
            new FixedDropZoneQuery(new Vector2Int(4, 1)),
            new FixedGameClock());
        runtime.Restore(new DungeonGrandProjectSaveData
        {
            state = new GrandProjectRuntimeState
            {
                completedProjectIds = new List<string>
                {
                    GrandProjectRuntime.DeepMiningNetworkId,
                    GrandProjectRuntime.RegionalTradePostId,
                    GrandProjectRuntime.DefenseDistrictId,
                    GrandProjectRuntime.ExpeditionSupplyBaseId
                }
            }
        });
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
        GrandProjectRuntime restored = new GrandProjectRuntime(
            items,
            new EmptyBuildingWorldQuery(),
            new FixedDropZoneQuery(new Vector2Int(4, 1)),
            new FixedGameClock());
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        new GrandProjectSaveSection(restored).Restore(
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

    private static ResourceEconomyContentCatalog LoadCatalog()
    {
        return new ResourceEconomyContentCatalog(
            LoadAll<ResourceItemDefinitionSO>("Assets/Resources/SO/Economy/Items"),
            LoadAll<ProductionRecipeSO>("Assets/Resources/SO/Economy/Recipes"),
            LoadAll<CropDefinitionSO>("Assets/Resources/SO/Economy/Crops"),
            LoadAll<CraftMaterialDefinitionSO>("Assets/Resources/SO/Economy/Materials"),
            LoadAll<SubstanceDefinitionSO>("Assets/Resources/SO/Economy/Substances"));
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

    private sealed class FakeProductionItemGateway : IProductionItemGateway
    {
        private readonly Dictionary<string, int> requested =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> cumulativeRequested =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> delivered =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> available =
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
}
#endif
