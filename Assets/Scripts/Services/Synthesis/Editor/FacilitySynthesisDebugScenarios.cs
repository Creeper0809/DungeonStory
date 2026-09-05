using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Diagnostics;
using Object = UnityEngine.Object;

public static class FacilitySynthesisDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Synthesis/Run Modular Facility Synthesis Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("Modular facility synthesis scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        FacilitySynthesisRulesDebugScenarios.Validate();
        List<string> errors = new List<string>();
        RunScenario("9개 조합식이 세 모듈 전략만 참조", VerifySynthesisAssets, errors);
        RunScenario("공개 6개와 희귀 3개 가시성", VerifyRecipeVisibility, errors);
        RunScenario("세 전략의 배치 시설 합성", VerifyStrategySynthesis, errors);
        RunScenario("역순 선택에서도 선언 재료가 결과 앵커", VerifyDeclaredAnchorWinsSelectionOrder, errors);
        RunScenario("생산 권위 반영 실패 시 모든 재료와 epoch 복구", VerifyRetargetCommitFailureRestoresAllMaterials, errors);
        RunScenario("모듈 시설 레벨 계승", VerifyLevelInheritance, errors);
        RunScenario("파손 모듈 재료 거부", VerifyDamagedMaterialRejected, errors);
        RunScenario("세 희귀 조합식 연구 해금", VerifyRareRecipesRequireResearch, errors);
        RunScenario("모듈 합성 UI 렌더", VerifySynthesisPanelRendering, errors);

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
            Debug.Log("Modular facility synthesis scenarios passed.");
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

    private static bool VerifySynthesisAssets()
    {
        IReadOnlyList<FacilitySynthesisRecipeSO> recipes = CreateRecipeQuery().GetAllRecipes();
        FacilitySynthesisRecipeSO[] modularRecipes = recipes
            .Where(recipe => AssetDatabase.GetAssetPath(recipe).StartsWith(
                "Assets/Resources/SO/Synthesis/P1/",
                StringComparison.Ordinal))
            .ToArray();
        bool onlyModularBuildings = modularRecipes.All(recipe =>
            IsModularBuilding(recipe.resultBuilding)
            && recipe.materialBuildings.All(IsModularBuilding));

        return modularRecipes.Length == 9
            && modularRecipes.Count(recipe => recipe.recipeId.StartsWith("recipe_commercial_", StringComparison.Ordinal)
                || recipe.recipeId.StartsWith("recipe_logistics_", StringComparison.Ordinal)
                || recipe.recipeId.StartsWith("recipe_commerce_", StringComparison.Ordinal)) == 3
            && modularRecipes.Count(recipe => recipe.recipeId.StartsWith("recipe_fortress_", StringComparison.Ordinal)) == 3
            && modularRecipes.Count(recipe => recipe.recipeId.StartsWith("recipe_arcane_", StringComparison.Ordinal)) == 3
            && modularRecipes.Count(recipe => recipe.IsSpecial) == 3
            && onlyModularBuildings;
    }

    private static bool VerifyRecipeVisibility()
    {
        IFacilitySynthesisRecipeQuery recipeQuery = CreateRecipeQuery();
        IReadOnlyList<FacilitySynthesisRecipeSO> publicRecipes = recipeQuery.GetVisibleRecipes(null);
        FacilitySynthesisRecipeSO[] rareRecipes =
        {
            LoadRecipe("RS_SecureDisplay"),
            LoadRecipe("RS_BattleBanner"),
            LoadRecipe("RS_RitualFocus")
        };
        BlueprintResearchState researchState = new BlueprintResearchState();

        bool hiddenBeforeResearch = rareRecipes.All(recipe => !recipeQuery.IsVisible(recipe, researchState));
        foreach (FacilitySynthesisRecipeSO recipe in rareRecipes)
        {
            researchState.UnlockRecipe(recipe.requiredResearchRecipeId);
        }

        return publicRecipes.Count == 6
            && hiddenBeforeResearch
            && rareRecipes.All(recipe => recipeQuery.IsVisible(recipe, researchState));
    }

    private static bool VerifyStrategySynthesis()
    {
        return VerifyRecipeExecution(
                "RS_CommercialGrill",
                "D01_간이화덕",
                "D03_조리손질대",
                "D02_고기그릴")
            && VerifyRecipeExecution(
                "RS_PatrolBoard",
                "G02_경보종",
                "E05_세력깃발",
                "G03_순찰상황판")
            && VerifyRecipeExecution(
                "RS_AlchemyBench",
                "Q01_연구책상",
                "Q03_연구용책장",
                "Q02_연금술작업대");
    }

    private static bool VerifyRecipeExecution(
        string recipeAssetName,
        string firstMaterialAssetName,
        string secondMaterialAssetName,
        string resultAssetName)
    {
        using SynthesisScenarioWorld world = new SynthesisScenarioWorld();
        FacilitySynthesisRuntime runtime = world.CreateRuntime();
        BuildableObject first = world.Place(firstMaterialAssetName, new Vector2Int(4, 0));
        BuildableObject second = world.Place(secondMaterialAssetName, new Vector2Int(11, 0));
        BuildingInstanceId anchorId = first.RequirePersistentInstanceId();
        BuildingInstanceId consumedId = second.RequirePersistentInstanceId();

        bool success = runtime.TrySynthesize(
            LoadRecipe(recipeAssetName),
            new[] { first, second },
            out FacilitySynthesisResult result);

        return success
            && result.Success
            && result.ResultBuilding != null
            && result.ResultBuilding.id == LoadBuilding(resultAssetName).id
            && result.ResultBuilding.centerPos == new Vector2Int(4, 0)
            && result.ResultBuilding.RequirePersistentInstanceId().Equals(anchorId)
            && CharacterAiEditorTestDependencies.WorldRegistry.Buildings.Count(building =>
                building.BuildingInstanceId.Equals(anchorId)) == 1
            && CharacterAiEditorTestDependencies.WorldRegistry.Buildings.All(building =>
                !building.BuildingInstanceId.Equals(consumedId))
            && first.isDestroy
            && second.isDestroy;
    }

    private static bool VerifyDeclaredAnchorWinsSelectionOrder()
    {
        using SynthesisScenarioWorld world = new SynthesisScenarioWorld();
        FacilitySynthesisRuntime runtime = world.CreateRuntime();
        BuildableObject declaredFirst = world.Place("D01_간이화덕", new Vector2Int(5, 0));
        BuildableObject selectedFirst = world.Place("D03_조리손질대", new Vector2Int(13, 0));
        Vector2Int declaredAnchorPosition = declaredFirst.centerPos;
        BuildingInstanceId declaredAnchorId = declaredFirst.RequirePersistentInstanceId();

        bool success = runtime.TrySynthesize(
            LoadRecipe("RS_CommercialGrill"),
            new[] { selectedFirst, declaredFirst },
            out FacilitySynthesisResult result);

        return success
            && result.ResultBuilding != null
            && result.ResultBuilding.centerPos == declaredAnchorPosition
            && result.ResultBuilding.RequirePersistentInstanceId().Equals(declaredAnchorId);
    }

    private static bool VerifyLevelInheritance()
    {
        using SynthesisScenarioWorld world = new SynthesisScenarioWorld();
        FacilitySynthesisRuntime runtime = world.CreateRuntime();
        BuildableObject shelf = world.Place("D10_식재료선반", new Vector2Int(3, 0));
        BuildableObject crates = world.Place("L02_상자더미", new Vector2Int(9, 0));
        shelf.SetFacilityLevel(4);
        crates.SetFacilityLevel(2);

        bool success = runtime.TrySynthesize(
            LoadRecipe("RS_LogisticsShelf"),
            new[] { shelf, crates },
            out FacilitySynthesisResult result);

        return success
            && result.InheritedLevel == 2
            && result.ResultBuilding != null
            && result.ResultBuilding.FacilityLevel == 2;
    }

    private static bool VerifyDamagedMaterialRejected()
    {
        using SynthesisScenarioWorld world = new SynthesisScenarioWorld();
        FacilitySynthesisRuntime runtime = world.CreateRuntime();
        BuildableObject desk = world.Place("Q01_연구책상", new Vector2Int(3, 0));
        BuildableObject bookcase = world.Place("Q03_연구용책장", new Vector2Int(9, 0));
        bookcase.SetDamaged(true);

        bool rejected = !runtime.TrySynthesize(
            LoadRecipe("RS_AlchemyBench"),
            new[] { desk, bookcase },
            out FacilitySynthesisResult result);

        return rejected
            && result.Message.Contains("파손")
            && !desk.isDestroy
            && !bookcase.isDestroy;
    }

    private static bool VerifyRetargetCommitFailureRestoresAllMaterials()
    {
        using SynthesisScenarioWorld world = new SynthesisScenarioWorld(
            failRetargetCommit: true);
        FacilitySynthesisRuntime runtime = world.CreateRuntime();
        BuildableObject hearth = world.Place("D01_간이화덕", new Vector2Int(5, 0));
        BuildableObject prep = world.Place("D03_조리손질대", new Vector2Int(13, 0));
        BuildingInstanceId hearthId = hearth.RequirePersistentInstanceId();
        BuildingInstanceId prepId = prep.RequirePersistentInstanceId();

        bool rejected = !runtime.TrySynthesize(
            LoadRecipe("RS_CommercialGrill"),
            new[] { prep, hearth },
            out FacilitySynthesisResult result);

        return rejected
            && result.Message.Contains("생산 권위")
            && !hearth.isDestroy
            && !prep.isDestroy
            && ReferenceEquals(hearth.Grid, world.Grid)
            && ReferenceEquals(prep.Grid, world.Grid)
            && ReferenceEquals(
                world.Grid.GetGridCell(hearth.centerPos)?.GetOccupant(
                    hearth.BuildingData.Placement.Layer),
                hearth)
            && ReferenceEquals(
                world.Grid.GetGridCell(prep.centerPos)?.GetOccupant(
                    prep.BuildingData.Placement.Layer),
                prep)
            && CharacterAiEditorTestDependencies.WorldRegistry.Buildings.Count(building =>
                building.BuildingInstanceId.Equals(hearthId)) == 1
            && CharacterAiEditorTestDependencies.WorldRegistry.Buildings.Count(building =>
                building.BuildingInstanceId.Equals(prepId)) == 1
            && !world.IsMutationFrozen(hearthId)
            && !world.IsMutationFrozen(prepId);
    }

    private static bool VerifyRareRecipesRequireResearch()
    {
        return VerifyRareRecipeGate(
                "RS_SecureDisplay",
                "S02_잡화진열선반",
                "S04_잡화상자",
                "S03_잠금진열장")
            && VerifyRareRecipeGate(
                "RS_BattleBanner",
                "G03_순찰상황판",
                "E05_세력깃발",
                "G05_전투깃발")
            && VerifyRareRecipeGate(
                "RS_RitualFocus",
                "M03_룬안정기",
                "E07_촛대",
                "M04_의식초점석");
    }

    private static bool VerifyRareRecipeGate(
        string recipeAssetName,
        string firstMaterialAssetName,
        string secondMaterialAssetName,
        string resultAssetName)
    {
        using SynthesisScenarioWorld world = new SynthesisScenarioWorld();
        FacilitySynthesisRuntime runtime = world.CreateRuntime();
        BlueprintResearchRuntime researchRuntime = world.CreateResearchRuntime();
        BuildableObject first = world.Place(firstMaterialAssetName, new Vector2Int(4, 0));
        BuildableObject second = world.Place(secondMaterialAssetName, new Vector2Int(11, 0));
        FacilitySynthesisRecipeSO recipe = LoadRecipe(recipeAssetName);

        bool rejectedBeforeResearch = !runtime.TrySynthesize(
            recipe,
            new[] { first, second },
            out FacilitySynthesisResult beforeResult);
        researchRuntime.State.UnlockRecipe(recipe.requiredResearchRecipeId);
        bool acceptedAfterResearch = runtime.TrySynthesize(
            recipe,
            new[] { first, second },
            out FacilitySynthesisResult afterResult);

        return rejectedBeforeResearch
            && beforeResult.Message.Contains("해금")
            && acceptedAfterResearch
            && afterResult.ResultBuilding != null
            && afterResult.ResultBuilding.id == LoadBuilding(resultAssetName).id;
    }

    private static bool VerifySynthesisPanelRendering()
    {
        using SynthesisScenarioWorld world = new SynthesisScenarioWorld();
        FacilitySynthesisRuntime runtime = world.CreateRuntime();
        BuildableObject hearth = world.Place("D01_간이화덕", new Vector2Int(3, 0));
        BuildableObject prep = world.Place("D03_조리손질대", new Vector2Int(9, 0));
        runtime.ToggleMaterialSelection(hearth);
        runtime.ToggleMaterialSelection(prep);

        FacilitySynthesisPanel panel = new FacilitySynthesisPanelFactory(TMPKoreanFontEditorResolver.CreateService())
            .CreateDefaultPanel(runtime);
        world.TrackObject(panel.transform.root.gameObject);

        return panel.LastRenderedText.Contains("시설 합성")
            && panel.LastRenderedText.Contains("간이화덕")
            && panel.LastRenderedText.Contains("고기그릴");
    }

    private static BuildingSO LoadBuilding(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<BuildingSO>(
            $"Assets/Resources/SO/Building/Modular/{assetName}.asset");
    }

    private static FacilitySynthesisRecipeSO LoadRecipe(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<FacilitySynthesisRecipeSO>($"Assets/Resources/SO/Synthesis/P1/{assetName}.asset");
    }

    private static bool IsModularBuilding(BuildingSO building)
    {
        return building != null
            && AssetDatabase.GetAssetPath(building).StartsWith(
                "Assets/Resources/SO/Building/Modular/",
                StringComparison.Ordinal);
    }

    private static IFacilitySynthesisRecipeQuery CreateRecipeQuery()
    {
        return new EditorFacilitySynthesisRecipeQuery();
    }

    private sealed class EditorFacilitySynthesisRecipeQuery : IFacilitySynthesisRecipeQuery
    {
        public IReadOnlyList<FacilitySynthesisRecipeSO> GetAllRecipes()
        {
            return AssetDatabase.FindAssets("t:FacilitySynthesisRecipeSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<FacilitySynthesisRecipeSO>)
                .Where((recipe) => recipe != null && recipe.HasValidData)
                .OrderBy((recipe) => recipe.id)
                .ToArray();
        }

        public bool IsVisible(FacilitySynthesisRecipeSO recipe, BlueprintResearchState researchState)
        {
            return FacilitySynthesisService.IsRecipeVisible(recipe, researchState, null);
        }

        public IReadOnlyList<FacilitySynthesisRecipeSO> GetVisibleRecipes(BlueprintResearchState researchState)
        {
            return GetAllRecipes()
                .Where((recipe) => IsVisible(recipe, researchState))
                .ToArray();
        }

        public FacilitySynthesisRecipeSnapshot ToSnapshot(
            FacilitySynthesisRecipeSO recipe,
            BlueprintResearchState researchState)
        {
            return FacilitySynthesisService.ToSnapshot(recipe, researchState, null);
        }
    }

    private sealed class SynthesisScenarioWorld : IDisposable
    {
        private static readonly FieldInfo GridSystemInstanceField =
            typeof(GridSystemManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo GridField =
            typeof(GridSystemManager).GetField("<grid>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly GridSystemManager previousGridSystem;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly BlueprintResearchState fallbackResearchState = new BlueprintResearchState();
        private readonly ScenarioObjectResolver objectResolver;
        private BlueprintResearchRuntime researchRuntime;

        public SynthesisScenarioWorld(bool failRetargetCommit = false)
        {
            objectResolver = new ScenarioObjectResolver(failRetargetCommit);
            previousGridSystem = GridSystemInstanceField?.GetValue(null) as GridSystemManager;
            Grid = new Grid(24, 1);
            for (int x = 0; x < Grid.width; x++)
            {
                Grid.RegisterOccupant(
                    new TestHallwayOccupant(),
                    GridLayer.Hallway,
                    new List<Vector2Int> { new Vector2Int(x, 0) },
                    false);
            }

            GameObject gridObject = new GameObject("Synthesis Scenario GridSystemManager");
            objects.Add(gridObject);
            GridSystemInstanceField?.SetValue(null, null);
            GridSystemManager manager = gridObject.AddComponent<GridSystemManager>();
            GridField?.SetValue(manager, Grid);
            GridSystemInstanceField?.SetValue(null, manager);
        }

        public Grid Grid { get; }

        public bool IsMutationFrozen(BuildingInstanceId facilityId) =>
            objectResolver.IsMutationFrozen(facilityId);

        public FacilitySynthesisRuntime CreateRuntime()
        {
            GameObject obj = new GameObject("FacilitySynthesisRuntime_Test");
            objects.Add(obj);
            FacilitySynthesisRuntime runtime = obj.AddComponent<FacilitySynthesisRuntime>();
            runtime.ConstructFacilitySynthesisRuntime(
                new ScenarioBlueprintResearchStateService(this),
                new NullGridTextureProvider(),
                objectResolver,
                CreateRecipeQuery(),
                new GridBuildingObjectFactory(),
                new DungeonStory.Foundation.GameEventBus());
            return runtime;
        }

        public BlueprintResearchRuntime CreateResearchRuntime()
        {
            GameObject obj = new GameObject("BlueprintResearchRuntime_Synthesis_Test");
            objects.Add(obj);
            researchRuntime = obj.AddComponent<BlueprintResearchRuntime>();
            return researchRuntime;
        }

        public BuildableObject Place(string assetName, Vector2Int position)
        {
            BuildingSO buildingData = LoadBuilding(assetName);
            GridBuildingFactory factory = new GridBuildingFactory();
            BuildableObject building = factory.Create(Grid, buildingData, position);
            if (building == null)
            {
                throw new InvalidOperationException($"{assetName} could not be created.");
            }

            objects.Add(building.gameObject);
            InjectBuilding(building);
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

        public void TrackObject(GameObject obj)
        {
            if (obj != null && !objects.Contains(obj))
            {
                objects.Add(obj);
            }
        }

        public void Dispose()
        {
            foreach (BuildableObject building in Resources.FindObjectsOfTypeAll<BuildableObject>()
                .Where(building => building != null && ReferenceEquals(building.Grid, Grid)))
            {
                TrackObject(building.gameObject);
            }

            foreach (GameObject obj in objects.Where((obj) => obj != null))
            {
                Object.DestroyImmediate(obj);
            }

            GridSystemInstanceField?.SetValue(null, previousGridSystem);
            objectResolver.Dispose();
        }

        private BlueprintResearchState GetResearchState()
        {
            return researchRuntime != null ? researchRuntime.State : fallbackResearchState;
        }

        private static void InjectBuilding(BuildableObject building)
        {
            CharacterAiEditorTestDependencies.Inject(building);
            if (building is Shop shop)
            {
                CharacterAiEditorTestDependencies.InjectShop(shop);
            }
        }

        private sealed class ScenarioBlueprintResearchStateService : IBlueprintResearchStateService
        {
            private readonly SynthesisScenarioWorld world;

            public ScenarioBlueprintResearchStateService(SynthesisScenarioWorld world)
            {
                this.world = world;
            }

            public BlueprintResearchState GetState()
            {
                return world.GetResearchState();
            }
        }

        private sealed class NullGridTextureProvider : IGridTextureProvider
        {
            public GridTexture Texture => null;
        }

        private sealed class ScenarioObjectResolver : IObjectResolver
        {
            private readonly ProductionFacilityMutationEpochRuntime mutationEpochs = new();
            private readonly IProductionFacilityRetargetTransaction retargetTransaction;
            private readonly IProductionFacilityMutationFence productionFence =
                new EmptyProductionFacilityMutationFence();

            public ScenarioObjectResolver(bool failRetargetCommit)
            {
                retargetTransaction = new ProductionFacilityRetargetTransaction(
                    new ProductionFacilityRetargetParticipantRegistry(
                        new IProductionFacilityRetargetParticipant[]
                        {
                            new ScenarioRetargetParticipant(failRetargetCommit)
                        }),
                    mutationEpochs);
            }

            public bool IsMutationFrozen(BuildingInstanceId facilityId) =>
                mutationEpochs.IsFrozen(facilityId);

            public object ApplicationOrigin => null;
            public DiagnosticsCollector Diagnostics { get; set; }

            public object Resolve(Type type, object key = null)
            {
                throw new InvalidOperationException($"Scenario resolver cannot resolve {type?.Name ?? "null"}.");
            }

            public bool TryResolve(Type type, out object resolved, object key = null)
            {
                if (type == typeof(IProductionFacilityRetargetTransaction))
                {
                    resolved = retargetTransaction;
                    return true;
                }
                if (type == typeof(IProductionFacilityMutationFence))
                {
                    resolved = productionFence;
                    return true;
                }
                resolved = null;
                return false;
            }

            public object Resolve(Registration registration)
            {
                throw new InvalidOperationException("Scenario resolver does not resolve registrations.");
            }

            public IScopedObjectResolver CreateScope(Action<IContainerBuilder> installation = null)
            {
                throw new InvalidOperationException("Scenario resolver does not create scopes.");
            }

            public void Inject(object instance)
            {
                if (instance is BuildableObject building)
                {
                    InjectBuilding(building);
                }
            }

            public bool TryGetRegistration(Type type, out Registration registration, object key = null)
            {
                registration = null;
                return false;
            }

            public void Dispose()
            {
            }
        }

        private sealed class EmptyProductionFacilityMutationFence :
            IProductionFacilityMutationFence
        {
            public bool TryRequireNoAuthority(
                BuildableObject facility,
                ProductionFacilityMutationKind kind,
                out string failureReason)
            {
                failureReason = string.Empty;
                return true;
            }

            public bool TryPrepareEmpty(
                BuildableObject facility,
                ProductionFacilityMutationKind kind,
                string operationId,
                out ProductionFacilityEmptyMutationCandidate candidate,
                out string failureReason)
            {
                candidate = null;
                failureReason = "unused";
                return false;
            }

            public bool TryCommitAuthorityRevoke(
                ProductionFacilityEmptyMutationCandidate candidate,
                out string failureReason)
            {
                failureReason = "unused";
                return false;
            }

            public bool TryAbort(
                ProductionFacilityEmptyMutationCandidate candidate,
                out string failureReason)
            {
                failureReason = "unused";
                return false;
            }

            public bool TryComplete(
                ProductionFacilityEmptyMutationCandidate candidate,
                out string failureReason)
            {
                failureReason = "unused";
                return false;
            }
        }

        private sealed class ScenarioRetargetParticipantState
        {
            public bool Committed { get; set; }
        }

        private sealed class ScenarioRetargetParticipant :
            IProductionFacilityRetargetParticipant
        {
            private const string PreparedFingerprint =
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            private const string CommittedFingerprint =
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            private readonly bool failCommit;

            public ScenarioRetargetParticipant(bool failCommit)
            {
                this.failCommit = failCommit;
            }

            public string ParticipantId => "synthesis-fixture-authority";

            public bool TryPrepare(
                IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
                string operationId,
                out ProductionFacilityRetargetParticipantPlan plan,
                out string failureReason)
            {
                plan = ProductionFacilityRetargetParticipantPlan.Create(
                    ParticipantId,
                    PreparedFingerprint,
                    new ScenarioRetargetParticipantState());
                failureReason = string.Empty;
                return true;
            }

            public bool TryCommit(
                ProductionFacilityRetargetParticipantPlan plan,
                IReadOnlyList<ProductionFacilityRetargetBinding> orderedBindings,
                out string committedFingerprint,
                out string failureReason)
            {
                ScenarioRetargetParticipantState state =
                    (ScenarioRetargetParticipantState)plan.ParticipantState;
                if (failCommit)
                {
                    committedFingerprint = string.Empty;
                    failureReason = "fixture-forced-commit-failure";
                    return false;
                }
                state.Committed = true;
                committedFingerprint = CommittedFingerprint;
                failureReason = string.Empty;
                return true;
            }

            public bool TryRollback(
                ProductionFacilityRetargetParticipantPlan plan,
                out string rolledBackFingerprint,
                out string failureReason)
            {
                ((ScenarioRetargetParticipantState)plan.ParticipantState).Committed = false;
                rolledBackFingerprint = PreparedFingerprint;
                failureReason = string.Empty;
                return true;
            }

            public bool TryCaptureCurrentFingerprint(
                ProductionFacilityRetargetParticipantPlan plan,
                out string currentFingerprint,
                out string failureReason)
            {
                bool committed = ((ScenarioRetargetParticipantState)plan.ParticipantState)
                    .Committed;
                currentFingerprint = committed
                    ? CommittedFingerprint
                    : PreparedFingerprint;
                failureReason = string.Empty;
                return true;
            }
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
