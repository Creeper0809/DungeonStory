#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionWorkshopDebugScenarios
{
    [MenuItem("Tools/DungeonStory/QA/Production Workshop Contracts")]
    public static void Run()
    {
        List<string> failures = Validate();
        if (failures.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", failures));
        }

        Debug.Log(
            "Production workshop contracts PASS: exact workstation ownership, "
            + "physical intermediates, 28 supports, 180 research projects, "
            + "passive batch definitions and V3 save round-trip.");
    }

    public static List<string> Validate()
    {
        ProductionRecipeSO[] recipes = LoadAll<ProductionRecipeSO>(
            "Assets/Resources/SO/Economy/Recipes");
        ResourceItemDefinitionSO[] items = AssetDatabase
            .FindAssets("t:ResourceItemDefinitionSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>)
            .Where(item => item != null)
            .ToArray();
        BuildingSO[] buildings = LoadAll<BuildingSO>(
            "Assets/Resources/SO/Building");
        ResearchProjectSO[] research = LoadAll<ResearchProjectSO>(
            "Assets/Resources/SO/Research/Projects");
        List<string> failures = new List<string>();

        foreach (IGrouping<string, ProductionRecipeSO> duplicate in recipes
                     .Where(recipe => recipe != null)
                     .GroupBy(recipe => recipe.RecipeId, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            failures.Add($"Duplicate recipe ID: {duplicate.Key}");
        }

        foreach (IGrouping<int, ProductionRecipeSO> duplicate in recipes
                     .Where(recipe => recipe != null)
                     .GroupBy(recipe => recipe.id)
                     .Where(group => group.Count() > 1))
        {
            failures.Add(
                $"Duplicate recipe numeric ID: {duplicate.Key} "
                + $"({string.Join(", ", duplicate.Select(recipe => recipe.name))})");
        }

        HashSet<string> workstationTags = buildings
            .Select(building => building
                .GetProductionWorkstationAbility()?.WorkstationTag)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> itemIds = items
            .Where(item => item != null)
            .Select(item => item.ItemId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (ProductionRecipeSO recipe in recipes.Where(recipe =>
                     recipe != null
                     && recipe.RecipeId.StartsWith(
                         "recipe:",
                         StringComparison.Ordinal)))
        {
            bool workshopOwned = recipe.WorkstationTag.StartsWith(
                "workstation:",
                StringComparison.Ordinal);
            if (!workshopOwned)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(recipe.WorkstationTag))
            {
                failures.Add($"{recipe.RecipeId}: no exact workstation tag.");
            }
            else if (!workstationTags.Contains(recipe.WorkstationTag))
            {
                failures.Add(
                    $"{recipe.RecipeId}: workstation '{recipe.WorkstationTag}' "
                    + "has no building definition.");
            }

            foreach (ItemAmountDefinition input in recipe.Inputs)
            {
                if (input != null
                    && !itemIds.Contains(input.ItemId)
                    && !input.ItemId.StartsWith(
                        "stock-item:",
                        StringComparison.Ordinal))
                {
                    failures.Add(
                        $"{recipe.RecipeId}: missing input item {input.ItemId}.");
                }
            }
            foreach (ProductionOutputDefinition output in recipe.Outputs)
            {
                if (output != null && !itemIds.Contains(output.ItemId))
                {
                    failures.Add(
                        $"{recipe.RecipeId}: missing output item {output.ItemId}.");
                }
            }
        }

        BuildingSO[] supports = buildings
            .Where(building => building != null
                && building.id >= 1600
                && building.id <= 1631)
            .ToArray();
        if (supports.Length != 28)
        {
            failures.Add(
                $"Expected 28 support buildings, found {supports.Length}.");
        }
        foreach (BuildingSO support in supports)
        {
            BuildingProductionSupportAbility ability =
                support.GetProductionSupportAbility();
            if (ability == null)
            {
                failures.Add($"Building {support.id} has no valid support ability.");
            }
        }

        string[] stagedIds =
        {
            "recipe:twilight-beer",
            "recipe:young-wine",
            "recipe:night-wine",
            "recipe:night-spirit",
            "recipe:cheese",
            "recipe:fermented-pickle",
            "recipe:silage"
        };
        foreach (string stagedId in stagedIds)
        {
            ProductionRecipeSO recipe = recipes.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.RecipeId,
                    stagedId,
                    StringComparison.Ordinal));
            if (recipe == null
                || recipe.ProcessKind != ProductionProcessKind.PassiveBatch
                || recipe.ProcessingGameHours <= 0f
                || string.IsNullOrWhiteSpace(recipe.BatchSupportTag))
            {
                failures.Add($"{stagedId}: invalid passive batch definition.");
            }
        }

        if (research.Length != 180)
        {
            failures.Add(
                $"Expected 180 research projects, found {research.Length}.");
        }
        string[] newResearch =
        {
            "research:cuisine:baking",
            "research:cuisine:kitchen-hygiene",
            "research:cuisine:controlled-fermentation",
            "research:cuisine:distilling-aging"
        };
        foreach (string id in newResearch)
        {
            if (!research.Any(project =>
                    project.ProjectId.Value == id
                    && project.Unlocks
                        .OfType<BlueprintBuildingUnlock>()
                        .Any()))
            {
                failures.Add($"{id}: missing project or building unlock.");
            }
        }

        ProductionBillSaveData saved = new ProductionBillSaveData
        {
            billId = "production-bill:validation-batch",
            recipeId = "recipe:twilight-beer",
            buildingInstanceId = "building:validation-workshop",
            batchStage = ProductionBatchStage.Processing,
            remainingProcessingHours = 7.5f,
            batchIntegrity = 43f,
            utilityOutageHours = 6.25f,
            temperatureOutageHours = 1.5f,
            occupiedSupportNodeId = "building:test-support",
            blocked = new ProductionStatusSaveData
            {
                code = FailureCode.ProductionUtilitiesUnavailable,
                parameters = new List<string> { "validation" }
            },
            prefetchBatchCount = 3,
            estimatedDeliverySeconds = 18f,
            estimatedProductionCycleSeconds = 6f,
            logistics = new ProductionStatusSaveData
            {
                outcome = ProductionBillOutcomeCode.MaterialPrefetchAdjusted,
                parameters = new List<string> { "validation-prefetch" }
            }
        };
        DungeonProductionBillSaveData envelope =
            new DungeonProductionBillSaveData
            {
                bills = new List<ProductionBillSaveData> { saved }
            };
        DungeonProductionBillSaveData restored =
            JsonUtility.FromJson<DungeonProductionBillSaveData>(
                JsonUtility.ToJson(envelope));
        ProductionBillSaveData restoredBill = restored?.bills?.FirstOrDefault();
        if (DungeonProductionBillSaveData.CurrentVersion != 6
            || restoredBill == null
            || restoredBill.batchStage != ProductionBatchStage.Processing
            || !Mathf.Approximately(
                restoredBill.remainingProcessingHours,
                7.5f)
            || !Mathf.Approximately(restoredBill.batchIntegrity, 43f)
            || restoredBill.occupiedSupportNodeId != "building:test-support"
            || restoredBill.prefetchBatchCount != 3
            || !Mathf.Approximately(restoredBill.estimatedDeliverySeconds, 18f)
            || !Mathf.Approximately(
                restoredBill.estimatedProductionCycleSeconds,
                6f)
            || restoredBill.logistics == null
            || restoredBill.logistics.outcome
                != ProductionBillOutcomeCode.MaterialPrefetchAdjusted
            || restoredBill.logistics.parameters.Single()
                != "validation-prefetch")
        {
            failures.Add("Production bill V6 save round-trip failed.");
        }

        ValidateDeterministicRoomLinks(failures);
        return failures;
    }

    private static void ValidateDeterministicRoomLinks(
        ICollection<string> failures)
    {
        BuildingSO firstData = ScriptableObject.CreateInstance<BuildingSO>();
        BuildingSO secondData = ScriptableObject.CreateInstance<BuildingSO>();
        BuildingSO supportData = ScriptableObject.CreateInstance<BuildingSO>();
        GameObject firstObject = new GameObject("Workshop Link First");
        GameObject secondObject = new GameObject("Workshop Link Second");
        GameObject supportObject = new GameObject("Workshop Link Support");
        try
        {
            firstData.id = 99101;
            firstData.ReplaceAbilities(WorkstationAbilities(
                "workstation:test-room"));
            secondData.id = 99102;
            secondData.ReplaceAbilities(WorkstationAbilities(
                "workstation:test-room"));
            supportData.id = 99103;
            BuildingAbilityCollection supportAbilities =
                new BuildingAbilityCollection();
            supportAbilities.Add(new BuildingProductionSupportAbility
            {
                supportId = "support:test-room-instance",
                featureTags = new[] { "support:test-room" },
                compatibleWorkstationTags =
                    new[] { "workstation:test-room" },
                kind = ProductionSupportKind.Passive
            });
            supportData.ReplaceAbilities(supportAbilities);

            BuildableObject first =
                firstObject.AddComponent<BuildableObject>();
            BuildableObject second =
                secondObject.AddComponent<BuildableObject>();
            BuildableObject support =
                supportObject.AddComponent<BuildableObject>();
            first.RestorePersistentIdentity(
                (BuildingInstanceId)"building:workshop-link-first");
            second.RestorePersistentIdentity(
                (BuildingInstanceId)"building:workshop-link-second");
            support.RestorePersistentIdentity(
                (BuildingInstanceId)"building:workshop-link-support");
            CharacterAiEditorTestDependencies.Inject(first);
            CharacterAiEditorTestDependencies.Inject(second);
            CharacterAiEditorTestDependencies.Inject(support);
            first.Initialization(firstData, new Vector2Int(2, 0));
            second.Initialization(secondData, new Vector2Int(6, 0));
            support.Initialization(supportData, new Vector2Int(4, 0));
            Grid grid = new Grid(9, 1);
            first.SetGrid(grid);
            second.SetGrid(grid);
            support.SetGrid(grid);

            MutableBuildingWorldQuery world =
                new MutableBuildingWorldQuery(first, second, support);
            MutableRoomLayoutCache rooms = new MutableRoomLayoutCache();
            RoomInstance sharedRoom = ClosedRoom(
                1,
                first,
                second,
                support);
            rooms.Assign(sharedRoom, first, second, support);
            ProductionWorkshopRuntime runtime =
                new ProductionWorkshopRuntime(world, rooms);

            IReadOnlyList<ProductionSupportLinkSnapshot> firstLinks =
                runtime.GetLinks(first);
            IReadOnlyList<ProductionSupportLinkSnapshot> secondLinks =
                runtime.GetLinks(second);
            int cachedVersion = runtime.Version;
            runtime.GetLinks(first);
            if (firstLinks.Count != 1
                || secondLinks.Count != 0
                || !ReferenceEquals(firstLinks[0].Support, support)
                || runtime.Version != cachedVersion)
            {
                failures.Add(
                    "Same-room nearest/tie-break support linking was not "
                    + "deterministic or rebuilt without a world version change.");
            }

            RoomInstance splitRoom = ClosedRoom(2, support);
            rooms.Assign(sharedRoom, first, second);
            rooms.Assign(splitRoom, support);
            world.BuildingVersion++;
            if (runtime.GetLinks(first).Count != 0
                || runtime.GetLinks(second).Count != 0
                || runtime.TryGetLinkForSupport(support, out _))
            {
                failures.Add(
                    "Room split did not invalidate the support connection.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstObject);
            UnityEngine.Object.DestroyImmediate(secondObject);
            UnityEngine.Object.DestroyImmediate(supportObject);
            UnityEngine.Object.DestroyImmediate(firstData);
            UnityEngine.Object.DestroyImmediate(secondData);
            UnityEngine.Object.DestroyImmediate(supportData);
        }
    }

    private static BuildingAbilityCollection WorkstationAbilities(string tag)
    {
        BuildingAbilityCollection abilities = new BuildingAbilityCollection();
        abilities.Add(new BuildingProductionWorkstationAbility
        {
            workstationTag = tag
        });
        return abilities;
    }

    private static RoomInstance ClosedRoom(
        int id,
        params BuildableObject[] furniture)
    {
        return new RoomInstance(
            id,
            new[] { new Vector2Int(id, 0) },
            furniture,
            furniture.Take(1).ToArray(),
            Array.Empty<BuildableObject>(),
            2,
            0);
    }

    private sealed class MutableBuildingWorldQuery : IBuildingWorldQuery
    {
        public MutableBuildingWorldQuery(params BuildableObject[] buildings)
        {
            Buildings = buildings ?? Array.Empty<BuildableObject>();
        }

        public int BuildingVersion { get; set; } = 1;
        public IReadOnlyList<BuildableObject> Buildings { get; }
    }

    private sealed class MutableRoomLayoutCache : IRoomLayoutCache
    {
        private readonly Dictionary<BuildableObject, RoomInstance> rooms =
            new Dictionary<BuildableObject, RoomInstance>();

        public void Assign(
            RoomInstance room,
            params BuildableObject[] buildings)
        {
            foreach (BuildableObject building in buildings)
            {
                rooms[building] = room;
            }
        }

        public RoomLayout GetLayout(Grid grid) =>
            new RoomLayout(rooms.Values.Distinct().ToArray());

        public bool TryGetRoom(
            Grid grid,
            Vector2Int cell,
            out RoomInstance room)
        {
            room = null;
            return false;
        }

        public bool TryGetRoom(
            BuildableObject part,
            out RoomInstance room) =>
            rooms.TryGetValue(part, out room);

        public void Clear()
        {
            rooms.Clear();
        }
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
}
#endif
