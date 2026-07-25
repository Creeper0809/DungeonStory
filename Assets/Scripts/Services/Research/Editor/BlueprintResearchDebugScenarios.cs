using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BlueprintResearchDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Research/Run P1 Blueprint Research Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 blueprint research scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        P1FacilityShopAssetBuilder.EnsureP1FacilityShopAssets();

        List<string> errors = new List<string>();
        RunScenario("설계도 연구 데이터", VerifyBlueprintResearchData, errors);
        RunScenario("설계도 해금 Inspector 목록", VerifyUnlockAuthoringList, errors);
        RunScenario("구매 이벤트 연구 대기열", VerifyBlueprintPurchaseQueuesResearch, errors);
        RunScenario("연구 중단/재개 진행도", VerifyResearchProgressPersistsUntilCompletion, errors);
        RunScenario("연구 완료 모듈 건축 조기 해금", VerifyCompletionUnlocksModularConstruction, errors);
        RunScenario("연구 완료 조합식 해금", VerifyCompletionUnlocksRecipes, errors);
        RunScenario("개방형 설계도 해금", VerifyOpenUnlockEntry, errors);
        RunScenario("연구 속도 보정", VerifyResearchSpeedUsesCharacterAndFacilityModifiers, errors);
        RunScenario("연구 작업 후보 조건", VerifyResearchWorkCandidateRequiresQueuedBlueprint, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("P1 blueprint research scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario()) return;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }

    private static bool VerifyBlueprintResearchData()
    {
        FacilityBlueprintSO commercial = LoadBlueprint("BP_CommercialBasics");
        FacilityBlueprintSO rare = LoadBlueprint("BP_BattleDining");

        return commercial != null
            && commercial.researchWorkRequired > 0f
            && commercial.Unlocks.OfType<BlueprintBuildingUnlock>().Count() >= 5
            && commercial.Unlocks
                .OfType<BlueprintBuildingUnlock>()
                .Select(unlock => new EditorFacilityShopCatalog().FindBuildingById(unlock.buildingId))
                .All(building => building != null
                    && building.IsModularFacility()
                    && building.GetUnlockPhase() > 1)
            && rare != null
            && rare.researchWorkRequired > commercial.researchWorkRequired
            && rare.Unlocks
                .OfType<BlueprintRecipeUnlock>()
                .Any(unlock => unlock.recipeId == "recipe_commerce_secure_display_2");
    }

    private static bool VerifyBlueprintPurchaseQueuesResearch()
    {
        FacilityBlueprintSO blueprint = LoadBlueprint("BP_CommercialBasics");
        GameData gameData = CreateGameData(500);
        FacilityShopUnlockState state = new FacilityShopUnlockState();
        FacilityShopOffer offer = new FacilityBlueprintOffer(blueprint, 100, blueprint.rarity, true);
        GameObject runtimeObject = new GameObject("BlueprintResearchRuntime_Purchase_Test");
        BlueprintResearchRuntime runtime = runtimeObject.AddComponent<BlueprintResearchRuntime>();
        DungeonStory.Foundation.IGameEventBus gameEvents =
            new DungeonStory.Foundation.GameEventBus();
        runtime.Construct(
            new FixedFacilityShopUnlockStateService(state),
            new EditorFacilityShopCatalog(),
            new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry),
            new DungeonWorkforceReplanService(CharacterAiEditorTestDependencies.WorldRegistry),
            gameEvents);

        bool purchased = FacilityShopService.TryPurchaseOffer(
            gameData,
            offer,
            state,
            out FacilityShopPurchaseResult result,
            purchase => gameEvents.Publish(new FacilityShopPurchasedEvent(purchase)));
        bool valid = purchased
            && result.success
            && result.TryGetBlueprint(out FacilityBlueprintSO purchasedBlueprint)
            && purchasedBlueprint == blueprint
            && state.IsBlueprintAcquired(blueprint)
            && runtime.State.Tasks.Count == 1
            && runtime.State.Tasks[0].Blueprint == blueprint;

        Object.DestroyImmediate(runtimeObject);
        Object.DestroyImmediate(gameData);
        return valid;
    }

    private static bool VerifyResearchProgressPersistsUntilCompletion()
    {
        using ResearchScenarioWorld world = new ResearchScenarioWorld();
        BlueprintResearchRuntime runtime = world.CreateResearchRuntime();
        FacilityBlueprintSO blueprint = LoadBlueprint("BP_DefenseBasics");
        BuildableObject lab = world.Place("P1_ResearchLab", new Vector2Int(2, 0));
        CharacterActor researcher = world.CreateCharacter("Species_Vampire", "Trait_Researcher");

        runtime.EnqueueBlueprint(blueprint);
        BlueprintResearchWorkResult first = runtime.ApplyResearchWork(CharacterActor.From(researcher), lab, 0.5f);
        BlueprintResearchWorkResult second = runtime.ApplyResearchWork(CharacterActor.From(researcher), lab, 999f);

        return first.Success
            && first.AddedProgress > 0f
            && !first.Completed
            && runtime.State.Tasks[0].Progress > 0f
            && second.Success
            && second.Completed
            && runtime.State.IsCompleted(blueprint);
    }

    private static bool VerifyCompletionUnlocksModularConstruction()
    {
        using ResearchScenarioWorld world = new ResearchScenarioWorld();
        BlueprintResearchRuntime runtime = world.CreateResearchRuntime();
        FacilityBlueprintSO blueprint = LoadBlueprint("BP_DefenseBasics");
        BuildableObject lab = world.Place("P1_ResearchLab", new Vector2Int(2, 0));
        CharacterActor researcher = world.CreateCharacter("Species_Vampire", "Trait_Researcher");
        BuildingSO tacticalMap = LoadModularBuilding("G04_전술지도탁자");
        GameData dayOne = CreateGameData(500);
        bool assetUnlockBefore = tacticalMap != null && tacticalMap.unlocked;
        bool blockedBeforeResearch = tacticalMap != null
            && !FacilityProgression.IsUnlocked(tacticalMap, dayOne);

        runtime.EnqueueBlueprint(blueprint);
        BlueprintResearchWorkResult result = runtime.ApplyResearchWork(CharacterActor.From(researcher), lab, 999f);
        bool valid = result.Completed
            && tacticalMap != null
            && runtime.State.IsBuildingUnlocked(tacticalMap.id)
            && FacilityProgression.IsUnlocked(tacticalMap, dayOne, runtime.State)
            && tacticalMap.unlocked == assetUnlockBefore
            && blockedBeforeResearch;
        Object.DestroyImmediate(dayOne);
        return valid;
    }

    private static bool VerifyCompletionUnlocksRecipes()
    {
        using ResearchScenarioWorld world = new ResearchScenarioWorld();
        BlueprintResearchRuntime runtime = world.CreateResearchRuntime();
        FacilityBlueprintSO blueprint = LoadBlueprint("BP_BattleDining");
        BuildableObject lab = world.Place("P1_ResearchLab", new Vector2Int(2, 0));
        CharacterActor researcher = world.CreateCharacter("Species_Vampire", "Trait_Researcher");

        runtime.EnqueueBlueprint(blueprint);
        BlueprintResearchWorkResult result = runtime.ApplyResearchWork(CharacterActor.From(researcher), lab, 999f);

        return result.Completed
            && runtime.State.UnlockedRecipeIds.Contains("recipe_commerce_secure_display_2");
    }

    private static bool VerifyOpenUnlockEntry()
    {
        FacilityBlueprintSO blueprint = ScriptableObject.CreateInstance<FacilityBlueprintSO>();
        BuildingSO building = LoadBuilding("P1_ResearchLab");
        DebugBlueprintUnlock debugUnlock = new DebugBlueprintUnlock();
        blueprint.blueprintName = "확장 해금 테스트";
        blueprint.unlocks.Add(debugUnlock);
        BlueprintResearchState state = new BlueprintResearchState();
        BlueprintResearchUnlockResult result = BlueprintResearchService.ApplyCompletion(
            blueprint,
            state,
            new FacilityShopUnlockState(),
            new FixedFacilityShopCatalog(building));

        bool valid = debugUnlock.ApplyCount == 1
            && result.Unlocks.Count == 1
            && result.Unlocks[0].UnlockTypeId == DebugBlueprintUnlock.TypeId
            && result.FormatSummaryLines().Single() == "확장 해금: 테스트 보상";
        Object.DestroyImmediate(blueprint);
        return valid;
    }

    private static bool VerifyUnlockAuthoringList()
    {
        FacilityBlueprintSO blueprint = ScriptableObject.CreateInstance<FacilityBlueprintSO>();
        bool addFirst = BlueprintUnlockListDrawer.AddUnlock(
            blueprint,
            "unlocks.items",
            typeof(BlueprintRecipeUnlock));
        bool addSecond = BlueprintUnlockListDrawer.AddUnlock(
            blueprint,
            "unlocks.items",
            typeof(BlueprintRecipeUnlock));

        SerializedObject serialized = new SerializedObject(blueprint);
        SerializedProperty items = serialized.FindProperty("unlocks.items");
        bool duplicateEntriesSupported = items != null
            && items.arraySize == 2
            && items.GetArrayElementAtIndex(0).managedReferenceValue is BlueprintRecipeUnlock
            && items.GetArrayElementAtIndex(1).managedReferenceValue is BlueprintRecipeUnlock;
        bool removed = BlueprintUnlockListDrawer.RemoveSelected(items, 0);
        serialized.Update();
        bool valid = addFirst
            && addSecond
            && duplicateEntriesSupported
            && removed
            && serialized.FindProperty("unlocks.items")?.arraySize == 1
            && BlueprintUnlockListDrawer.AddableTypes.Contains(typeof(BlueprintBuildingUnlock))
            && BlueprintUnlockListDrawer.AddableTypes.Contains(typeof(BlueprintBasicPurchaseUnlock))
            && BlueprintUnlockListDrawer.AddableTypes.Contains(typeof(BlueprintRecipeUnlock))
            && !BlueprintUnlockListDrawer.AddableTypes.Contains(typeof(DebugBlueprintUnlock));
        Object.DestroyImmediate(blueprint);
        return valid;
    }

    private static bool VerifyResearchSpeedUsesCharacterAndFacilityModifiers()
    {
        using ResearchScenarioWorld world = new ResearchScenarioWorld();
        BuildableObject lab = world.Place("P1_ResearchLab", new Vector2Int(2, 0));
        CharacterActor fighter = world.CreateCharacter("Species_Orc", "Trait_Fighter");
        CharacterActor researcher = world.CreateCharacter("Species_Vampire", "Trait_Researcher");

        float fighterWork = BlueprintResearchService.CalculateResearchWork(CharacterActor.From(fighter), lab, 1f);
        float researcherWork = BlueprintResearchService.CalculateResearchWork(CharacterActor.From(researcher), lab, 1f);
        float labMultiplier = BlueprintResearchService.GetFacilityResearchMultiplier(lab);

        return researcherWork > fighterWork
            && labMultiplier > 1f;
    }

    private static bool VerifyResearchWorkCandidateRequiresQueuedBlueprint()
    {
        using ResearchScenarioWorld world = new ResearchScenarioWorld();
        BlueprintResearchRuntime runtime = world.CreateResearchRuntime();
        BuildableObject lab = world.Place("P1_ResearchLab", new Vector2Int(2, 0));
        CharacterActor researcher = world.CreateCharacter("Species_Vampire", "Trait_Researcher");
        AbilityWork work = researcher.GetAbility<AbilityWork>();
        GridPathSearchResult search = world.Grid.SearchPath(Vector2Int.zero);

        bool rejectedWithoutBlueprint = !work.TrySetPriorityWorkTarget(
            lab,
            BuiltInWorkTypeIds.Research,
            search,
            out _);

        runtime.EnqueueBlueprint(LoadBlueprint("BP_SupportBasics"));

        bool acceptedWithBlueprint = work.TrySetPriorityWorkTarget(
            lab,
            BuiltInWorkTypeIds.Research,
            search,
            out _);

        return rejectedWithoutBlueprint
            && acceptedWithBlueprint
            && work.PriorityWorkTarget == lab
            && work.PriorityWorkTypeId == BuiltInWorkTypeIds.Research;
    }

    private static FacilityBlueprintSO LoadBlueprint(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<FacilityBlueprintSO>($"Assets/Resources/SO/Blueprint/P1/{assetName}.asset");
    }

    private static BuildingSO LoadBuilding(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<BuildingSO>($"Assets/Resources/SO/Building/P1/{assetName}.asset");
    }

    private static BuildingSO LoadModularBuilding(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<BuildingSO>(
            $"Assets/Resources/SO/Building/Modular/{assetName}.asset");
    }

    private static GameData CreateGameData(int holdingMoney)
    {
        GameData gameData = ScriptableObject.CreateInstance<GameData>();
        gameData.holdingMoney = new Data<int>();
        gameData.holdingMoney.Initialize(holdingMoney);
        return gameData;
    }

    private sealed class DebugBlueprintUnlock : BlueprintUnlock
    {
        public const string TypeId = "debug.blueprint-unlock";

        public int ApplyCount { get; private set; }
        public override string UnlockTypeId => TypeId;
        public override bool IsConfigured => true;

        public override BlueprintUnlockRecord Apply(BlueprintUnlockContext context)
        {
            ApplyCount++;
            return new BlueprintUnlockRecord(
                UnlockTypeId,
                "확장 해금",
                "debug-reward",
                "테스트 보상");
        }
    }

    private sealed class FixedFacilityShopCatalog : IFacilityShopCatalog
    {
        private readonly BuildingSO building;

        public FixedFacilityShopCatalog(BuildingSO building)
        {
            this.building = building;
        }

        public IReadOnlyCollection<BuildingSO> Buildings => building != null
            ? new[] { building }
            : Array.Empty<BuildingSO>();
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints => Array.Empty<FacilityBlueprintSO>();

        public BuildingSO FindBuildingById(int buildingId)
        {
            return building != null && building.id == buildingId ? building : null;
        }
    }

    private sealed class ResearchScenarioWorld : IDisposable
    {
        private static readonly FieldInfo GridSystemInstanceField =
            typeof(GridSystemManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo GridField =
            typeof(GridSystemManager).GetField("<grid>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CharacterAwakeMethod =
            typeof(CharacterActor).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly GridSystemManager previousGridSystem;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<ScriptableObject> scriptableObjects = new List<ScriptableObject>();

        public ResearchScenarioWorld()
        {
            previousGridSystem = GridSystemInstanceField?.GetValue(null) as GridSystemManager;
            Grid = new Grid(12, 1);
            for (int x = 0; x < Grid.width; x++)
            {
                Grid.RegisterOccupant(
                    new TestHallwayOccupant(),
                    GridLayer.Hallway,
                    new List<Vector2Int> { new Vector2Int(x, 0) },
                    false);
            }

            GameObject gridObject = new GameObject("Blueprint Research Scenario GridSystemManager");
            objects.Add(gridObject);
            GridSystemManager manager = gridObject.AddComponent<GridSystemManager>();
            GridField?.SetValue(manager, Grid);
            GridSystemInstanceField?.SetValue(null, manager);
        }

        public Grid Grid { get; }

        public BlueprintResearchRuntime CreateResearchRuntime()
        {
            GameObject obj = new GameObject("BlueprintResearchRuntime_Test");
            objects.Add(obj);
            DailyFacilityShopRuntime shopRuntime = obj.AddComponent<DailyFacilityShopRuntime>();
            BlueprintResearchRuntime runtime = obj.AddComponent<BlueprintResearchRuntime>();
            runtime.Construct(
                new FixedFacilityShopUnlockStateService(shopRuntime.UnlockState),
                new EditorFacilityShopCatalog(),
                new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry),
                new DungeonWorkforceReplanService(
                    CharacterAiEditorTestDependencies.WorldRegistry),
                new DungeonStory.Foundation.GameEventBus());
            return runtime;
        }

        public BuildableObject Place(string assetName, Vector2Int position)
        {
            BuildingSO buildingData = LoadBuilding(assetName);
            if (buildingData == null)
            {
                throw new InvalidOperationException($"{assetName} asset not found.");
            }

            GridBuildingFactory factory = new GridBuildingFactory();
            BuildableObject building = factory.Create(Grid, buildingData, position);
            if (building == null)
            {
                throw new InvalidOperationException($"{assetName} could not be created.");
            }

            objects.Add(building.gameObject);
            CharacterAiEditorTestDependencies.Inject(building);
            if (building is Shop shop)
            {
                CharacterAiEditorTestDependencies.InjectShop(shop);
            }
            building.SetGrid(Grid);
            building.Initialization(buildingData, position);
            bool registered = Grid.RegisterOccupant(
                building,
                buildingData.Placement.Layer,
                buildingData.GetGridPosList(position),
                buildingData.Placement.IsMovement);
            if (!registered)
            {
                throw new InvalidOperationException($"{assetName} could not be registered.");
            }

            return building;
        }

        public CharacterActor CreateCharacter(string speciesAssetName, string traitAssetName)
        {
            GameObject obj = new GameObject($"Blueprint Research Character {speciesAssetName} {traitAssetName}");
            objects.Add(obj);
            obj.AddComponent<SpriteRenderer>();
            obj.AddComponent<AbilityMove>();
            obj.AddComponent<AbilityWork>();
            AIBrain brain = obj.AddComponent<AIBrain>();
            brain.availableActions = AiDebugScenarioActionFactory.CreateStaffActions();
            CharacterActor character = obj.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(obj);
            CharacterAwakeMethod?.Invoke(character, null);

            CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
            scriptableObjects.Add(data);
            data.characterType = CharacterType.NPC;
            data.characterName = "Research Scenario";
            data.species = AssetDatabase.LoadAssetAtPath<CharacterSpeciesSO>(
                $"Assets/Resources/SO/Character/Species/{speciesAssetName}.asset");
            data.speciesTag = data.species != null ? data.species.speciesTag : string.Empty;
            data.traits = new[]
            {
                AssetDatabase.LoadAssetAtPath<CharacterTraitSO>(
                    $"Assets/Resources/SO/Character/Traits/{traitAssetName}.asset")
            }.Where((trait) => trait != null).ToArray();
            data.baseStats = CharacterStatBlock.CreateDefault();
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();
            character.Initialization(data);
            character.stats[CharacterCondition.SLEEP] = 100f;
            character.stats[CharacterCondition.MOOD] = 100f;
            return character;
        }

        public void Dispose()
        {
            foreach (GameObject obj in objects.Where((obj) => obj != null))
            {
                Object.DestroyImmediate(obj);
            }

            foreach (ScriptableObject scriptableObject in scriptableObjects.Where((obj) => obj != null))
            {
                Object.DestroyImmediate(scriptableObject);
            }

            GridSystemInstanceField?.SetValue(null, previousGridSystem);
        }
    }

    private sealed class FixedFacilityShopUnlockStateService : IFacilityShopUnlockStateService
    {
        private readonly FacilityShopUnlockState state;

        public FixedFacilityShopUnlockStateService(FacilityShopUnlockState state)
        {
            this.state = state;
        }

        public FacilityShopUnlockState GetUnlockState()
        {
            return state;
        }
    }

    private sealed class EditorFacilityShopCatalog : IFacilityShopCatalog
    {
        public IReadOnlyCollection<BuildingSO> Buildings => LoadAssets<BuildingSO>("Assets/Resources/SO/Building");
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints => LoadAssets<FacilityBlueprintSO>("Assets/Resources/SO/Blueprint");

        public BuildingSO FindBuildingById(int buildingId)
        {
            return Buildings.FirstOrDefault(building => building != null && building.id == buildingId);
        }

        private static IReadOnlyCollection<TAsset> LoadAssets<TAsset>(string folder)
            where TAsset : UnityEngine.Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(TAsset).Name}", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TAsset>)
                .Where(asset => asset != null)
                .ToArray();
        }
    }

    private sealed class TestHallwayOccupant : IGridOccupant
    {
        public int GridId => 0;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
    }
}
