using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CodexDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Codex/Run P1 Codex Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 codex scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        P1FacilityShopAssetBuilder.EnsureP1FacilityShopAssets();
        P1FacilitySynthesisAssetBuilder.EnsureP1SynthesisAssets();
        P1FacilityEvolutionAssetBuilder.EnsureP1EvolutionAssets();

        List<string> errors = new List<string>();
        RunScenario("도감 기준 데이터", VerifyReferenceCodexData, errors);
        RunScenario("특수 조합식 힌트와 연구 해금", VerifySpecialRecipeHintAndResearchReveal, errors);
        RunScenario("방어 관찰 침략 도감", VerifyDefenseObservationUpdatesInvasionCodex, errors);
        RunScenario("손님 방문 몬스터 도감", VerifyFacilityVisitUpdatesMonsterCodex, errors);
        RunScenario("시설 진화 도감 기록", VerifyFacilityEvolutionUpdatesFacilityCodex, errors);
        RunScenario("도감 UI 렌더", VerifyCodexPanelRendering, errors);

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
            Debug.Log("P1 codex scenarios passed.");
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

    private static bool VerifyReferenceCodexData()
    {
        using CodexScenarioWorld world = new CodexScenarioWorld();
        CodexRuntime runtime = world.CreateRuntime();

        CodexEntrySnapshot slime = runtime.State.GetSnapshot(CodexEntryCategory.Monster, "monster:Slime");
        CodexEntrySnapshot orc = runtime.State.GetSnapshot(CodexEntryCategory.Monster, "monster:Orc");
        CodexEntrySnapshot intruder = runtime.State.GetSnapshot(CodexEntryCategory.Invasion, CodexService.BreakthroughIntruderId);
        CodexEntrySnapshot spike = runtime.State.GetSnapshot(CodexEntryCategory.Facility, $"facility:{LoadBuilding("P1_SpikeTrap").id}");
        CodexEntrySnapshot guard = runtime.State.GetSnapshot(CodexEntryCategory.Facility, $"facility:{LoadBuilding("P1_GuardRoom").id}");

        return slime != null
            && orc != null
            && intruder != null
            && ContainsLine(intruder, "주의: 사장 캐릭터 처치")
            && spike != null
            && ContainsLinePart(spike, "공격 컨셉: 물리")
            && ContainsLinePart(spike, "효과: 피해")
            && guard != null
            && ContainsLinePart(guard, "시너지 대상: 경비 직원");
    }

    private static bool VerifySpecialRecipeHintAndResearchReveal()
    {
        using CodexScenarioWorld world = new CodexScenarioWorld();
        CodexRuntime runtime = world.CreateRuntime();
        CodexEntrySnapshot hint = runtime.GetEntries(CodexEntryCategory.Facility)
            .FirstOrDefault((entry) => entry.entryId == "special_recipe_hint:recipe_arcane_ritual_2");

        bool hiddenAsHint = hint != null
            && ContainsLinePart(hint, "특수 조합식 힌트")
            && !hint.lines.Any((line) => line.Text.Contains("룬안정기 + 촛대"));

        BlueprintResearchState researchState = new BlueprintResearchState();
        researchState.UnlockRecipe("recipe_arcane_ritual_2");
        CodexService.ImportSynthesisRecipes(runtime.State, researchState, CreateSynthesisRecipeQuery());
        BuildingSO ritualFocus = LoadBuilding("M04_의식초점석");
        CodexEntrySnapshot ritualFocusEntry = runtime.State.GetSnapshot(
            CodexEntryCategory.Facility,
            $"facility:{ritualFocus.id}");

        return hiddenAsHint
            && ritualFocusEntry != null
            && ContainsLinePart(ritualFocusEntry, "조합식: 룬안정기 + 촛대 -> 의식초점석");
    }

    private static bool VerifyDefenseObservationUpdatesInvasionCodex()
    {
        using CodexScenarioWorld world = new CodexScenarioWorld();
        CodexRuntime runtime = world.CreateRuntime();
        DefenseFacility iceVent = world.CreateDefenseFacility("P1_IceVent");
        CharacterActor intruder = world.CreateCharacter("Intruder_Breakthrough");
        DefenseActivationReport report = new DefenseActivationReport(iceVent, CharacterActor.From(intruder), DefenseTriggerTiming.OnEnter);
        report.AddMovementDelay(0.7f);
        report.AddEffectTag("감속");

        runtime.OnTriggerEvent(new DefenseFacilityTriggeredEvent(report));
        CodexEntrySnapshot invasion = runtime.State.GetSnapshot(CodexEntryCategory.Invasion, CodexService.BreakthroughIntruderId);
        CodexEntrySnapshot ice = runtime.State.GetSnapshot(CodexEntryCategory.Facility, $"facility:{LoadBuilding("P1_IceVent").id}");

        return invasion != null
            && ContainsLine(invasion, "약점: 감속")
            && ice != null
            && ContainsLinePart(ice, "공격 컨셉: 냉기");
    }

    private static bool VerifyFacilityVisitUpdatesMonsterCodex()
    {
        using CodexScenarioWorld world = new CodexScenarioWorld();
        CodexRuntime runtime = world.CreateRuntime();
        CharacterActor orc = world.CreateCharacter("Owner_Orc");
        BuildableObject meatRestaurant = world.CreateFacility("P1_MeatRestaurant");

        runtime.OnTriggerEvent(new FacilityVisitEvent(CharacterActor.From(orc), meatRestaurant));
        CodexEntrySnapshot orcEntry = runtime.State.GetSnapshot(CodexEntryCategory.Monster, "monster:Orc");
        CodexEntrySnapshot restaurantEntry = runtime.State.GetSnapshot(CodexEntryCategory.Facility, $"facility:{LoadBuilding("P1_MeatRestaurant").id}");

        return orcEntry != null
            && ContainsLinePart(orcEntry, "관찰:")
            && restaurantEntry != null
            && ContainsLinePart(restaurantEntry, "역할: 식사");
    }

    private static bool VerifyFacilityEvolutionUpdatesFacilityCodex()
    {
        using CodexScenarioWorld world = new CodexScenarioWorld();
        CodexRuntime runtime = world.CreateRuntime();
        FacilityEvolutionRecipeSO recipe = AssetDatabase.LoadAssetAtPath<FacilityEvolutionRecipeSO>(
            "Assets/Resources/SO/FacilityEvolution/P1/EV_AlchemyBench.asset");
        if (recipe == null)
        {
            return false;
        }

        BuildableObject alchemyBench = world.CreateFacility("Q02_연금술작업대");
        FacilityEvolutionProposal proposal = new FacilityEvolutionProposal(
            "마력 안정 장치와 연구 기록이 축적된 연구실",
            new[] { recipe.EffectiveId },
            new Dictionary<string, string>
            {
                { recipe.EffectiveId, "연구와 의식 정체성이 연금술 계보와 맞습니다." }
            },
            new[] { FacilityEvolutionTerms.Research, FacilityEvolutionTerms.Ritual },
            "연구 기록과 룬 안정 장치가 연금술 작업 흐름을 만들었습니다.",
            0.92f,
            FacilityEvolutionProposalSources.LocalLlm);
        FacilityEvolutionResult result = new FacilityEvolutionResult(
            true,
            recipe,
            alchemyBench,
            2,
            FacilityShopService.GetBuildingName(LoadBuilding("Q01_연구책상")),
            proposal,
            "비전 연구 진화 완료",
            new[] { FacilityEvolutionTerms.Research, FacilityEvolutionTerms.Ritual });

        runtime.OnTriggerEvent(new FacilityEvolutionCompletedEvent(result));

        BuildingSO alchemyBenchData = LoadBuilding("Q02_연금술작업대");
        CodexEntrySnapshot entry = runtime.State.GetSnapshot(
            CodexEntryCategory.Facility,
            $"facility:{alchemyBenchData.id}");

        return entry != null
            && ContainsLinePart(entry, "계보 진화: 연구책상 -> 연금술작업대 (2성)")
            && ContainsLinePart(entry, "진화식: 비전 연구 진화")
            && ContainsLinePart(entry, "정체성: 마력 안정 장치와 연구 기록이 축적된 연구실")
            && ContainsLinePart(entry, "진화 기록: 연구 기록과 룬 안정 장치가 연금술 작업 흐름을 만들었습니다.")
            && ContainsLinePart(entry, "해석 출처: LocalLLM")
            && ContainsLinePart(entry, "변이: Research, Ritual");
    }

    private static bool VerifyCodexPanelRendering()
    {
        using CodexScenarioWorld world = new CodexScenarioWorld();
        CodexRuntime runtime = world.CreateRuntime();
        runtime.State.AddInfo(
            CodexEntryCategory.Invasion,
            CodexService.BreakthroughIntruderId,
            "돌파형 침입자",
            "약점: 감속",
            CodexInfoSource.Observation);
        CodexPanel panel = new CodexPanelFactory(TMPKoreanFontEditorResolver.CreateService())
            .CreateDefaultPanel(runtime);
        world.TrackObject(panel.transform.root.gameObject);

        return panel.LastRenderedText.Contains("몬스터 도감")
            && panel.LastRenderedText.Contains("침략 도감")
            && panel.LastRenderedText.Contains("시설 도감")
            && panel.LastRenderedText.Contains("약점: 감속");
    }

    private static bool ContainsLine(CodexEntrySnapshot entry, string line)
    {
        return entry != null
            && entry.lines != null
            && entry.lines.Any((candidate) => candidate.Text == line);
    }

    private static bool ContainsLinePart(CodexEntrySnapshot entry, string text)
    {
        return entry != null
            && entry.lines != null
            && entry.lines.Any((candidate) => candidate.Text.Contains(text));
    }

    private static BuildingSO LoadBuilding(string assetName)
    {
        BuildingSO modular = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            $"Assets/Resources/SO/Building/Modular/{assetName}.asset");
        return modular != null
            ? modular
            : AssetDatabase.LoadAssetAtPath<BuildingSO>(
                $"Assets/Resources/SO/Building/P1/{assetName}.asset");
    }

    private static CharacterSO LoadCharacter(string assetName)
    {
        CharacterSO character = AssetDatabase.LoadAssetAtPath<CharacterSO>($"Assets/Resources/SO/Character/Owners/{assetName}.asset");
        if (character != null)
        {
            return character;
        }

        return AssetDatabase.LoadAssetAtPath<CharacterSO>($"Assets/Resources/SO/Character/Intruders/{assetName}.asset");
    }

    private static IFacilitySynthesisRecipeQuery CreateSynthesisRecipeQuery()
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

    private sealed class CodexScenarioWorld : IDisposable
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        public CodexRuntime CreateRuntime()
        {
            GameObject obj = new GameObject("CodexRuntime_Test");
            objects.Add(obj);
            CodexRuntime runtime = obj.AddComponent<CodexRuntime>();
            IFacilitySynthesisRecipeQuery recipeQuery = CreateSynthesisRecipeQuery();
            ScenarioCodexReferenceCatalog referenceCatalog = new ScenarioCodexReferenceCatalog();
            runtime.ConstructCodexRuntime(
                new ScenarioBlueprintResearchStateService(),
                new CodexReferenceImporter(referenceCatalog, recipeQuery),
                recipeQuery,
                new ScenarioFacilityShopCatalog(referenceCatalog.Facilities),
                CharacterAiEditorTestDependencies.GameEvents);
            runtime.ImportReferenceData();
            return runtime;
        }

        public BuildableObject CreateFacility(string assetName)
        {
            BuildingSO building = LoadBuilding(assetName);
            GameObject obj = new GameObject(assetName);
            objects.Add(obj);
            BuildableObject facility = obj.AddComponent(building != null && building.type != null ? building.type : typeof(BuildableObject)) as BuildableObject;
            if (facility == null)
            {
                throw new InvalidOperationException($"{assetName} is not a BuildableObject.");
            }

            facility.Initialization(building, Vector2Int.zero);
            return facility;
        }

        public DefenseFacility CreateDefenseFacility(string assetName)
        {
            return CreateFacility(assetName) as DefenseFacility;
        }

        public CharacterActor CreateCharacter(string assetName)
        {
            CharacterSO characterData = LoadCharacter(assetName);
            GameObject obj = new GameObject(assetName);
            objects.Add(obj);
            CharacterActor character = obj.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(obj);
            character.data = characterData;
            return character;
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
            foreach (GameObject obj in objects.Where((obj) => obj != null))
            {
                Object.DestroyImmediate(obj);
            }
        }

        private sealed class ScenarioBlueprintResearchStateService : IBlueprintResearchStateService
        {
            private readonly BlueprintResearchState state = new BlueprintResearchState();

            public BlueprintResearchState GetState()
            {
                return state;
            }
        }

        private sealed class ScenarioCodexReferenceCatalog : ICodexReferenceCatalog
        {
            public IReadOnlyCollection<CharacterSpeciesSO> Species { get; } =
                AssetDatabase.FindAssets("t:CharacterSpeciesSO")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<CharacterSpeciesSO>)
                    .Where(species => species != null)
                    .ToArray();

            public IReadOnlyCollection<BuildingSO> Facilities { get; } =
                AssetDatabase.FindAssets("t:BuildingSO")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
                    .Where(building => building != null)
                    .ToArray();
        }

        private sealed class ScenarioFacilityShopCatalog : IFacilityShopCatalog
        {
            public ScenarioFacilityShopCatalog(IReadOnlyCollection<BuildingSO> buildings)
            {
                Buildings = buildings ?? Array.Empty<BuildingSO>();
                Blueprints = AssetDatabase.FindAssets("t:FacilityBlueprintSO")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<FacilityBlueprintSO>)
                    .Where(blueprint => blueprint != null)
                    .ToArray();
            }

            public IReadOnlyCollection<BuildingSO> Buildings { get; }
            public IReadOnlyCollection<FacilityBlueprintSO> Blueprints { get; }

            public BuildingSO FindBuildingById(int buildingId)
            {
                return Buildings.FirstOrDefault(building => building != null && building.id == buildingId);
            }
        }
    }
}
