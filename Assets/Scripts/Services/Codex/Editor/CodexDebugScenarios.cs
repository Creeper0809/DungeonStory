using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CodexDebugScenarios
{
    public static bool RunSingleForVerification(string scenarioId)
    {
        return scenarioId switch
        {
            "reference" => VerifyReferenceCodexData(),
            "recipe" => VerifySpecialRecipeHintAndResearchReveal(),
            "defense" => VerifyDefenseObservationUpdatesInvasionCodex(),
            "visit" => VerifyFacilityVisitUpdatesMonsterCodex(),
            "evolution" => VerifyFacilityEvolutionUpdatesFacilityCodex(),
            "panel" => VerifyCodexPanelRendering(),
            "restore" => VerifyDiscardedRestoreLeavesLiveCodexUntouched(),
            _ => throw new ArgumentOutOfRangeException(nameof(scenarioId), scenarioId, null)
        };
    }

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
        RunScenario(
            "실패한 복원 후보가 라이브 도감을 보존",
            VerifyDiscardedRestoreLeavesLiveCodexUntouched,
            errors);

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
        CodexService.ImportSynthesisRecipes(
            runtime.State,
            CodexDomainSnapshotFactory.CreateRecipeObservation(
                researchState,
                CreateSynthesisRecipeQuery(),
                CodexInfoSource.System));
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

        CharacterAiEditorTestDependencies.GameEvents.Publish(
            new DefenseFacilityTriggeredEvent(report));
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

        CharacterAiEditorTestDependencies.GameEvents.Publish(
            new FacilityVisitEvent(CharacterActor.From(orc), meatRestaurant));
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

        CharacterAiEditorTestDependencies.GameEvents.Publish(
            new FacilityEvolutionCompletedEvent(result));

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

    private static bool VerifyDiscardedRestoreLeavesLiveCodexUntouched()
    {
        const string markerEntryId = "debug:discarded-candidate";
        const string markerLine = "candidate-only-line";
        using CodexScenarioWorld source = new CodexScenarioWorld();
        CodexRuntime sourceRuntime = source.CreateRuntime();
        sourceRuntime.State.AddInfo(
            CodexEntryCategory.Invasion,
            markerEntryId,
            "Discard candidate",
            markerLine,
            CodexInfoSource.System);
        CodexSaveApplicationAdapter sourceAdapter =
            new CodexSaveApplicationAdapter(
                new FacilityFeatureSceneRuntimeReferences(
                    null,
                    null,
                    sourceRuntime));
        string candidatePayload = new CodexSaveSection(
            sourceAdapter,
            sourceAdapter,
            sourceAdapter).Capture();

        using CodexScenarioWorld target = new CodexScenarioWorld();
        CodexRuntime targetRuntime = target.CreateRuntime();
        CodexSaveApplicationAdapter targetAdapter =
            new CodexSaveApplicationAdapter(
                new FacilityFeatureSceneRuntimeReferences(
                    null,
                    null,
                    targetRuntime));
        CodexSaveSection targetSection = new CodexSaveSection(
            targetAdapter,
            targetAdapter,
            targetAdapter);
        CodexFailureSection lateFailure = new CodexFailureSection
        {
            RemainingCommitFailures = 1
        };
        CodexDiscardObserver observer = new CodexDiscardObserver(
            targetRuntime,
            markerEntryId,
            markerLine);
        DungeonSaveSectionRegistry registry = new DungeonSaveSectionRegistry(
            new IDungeonSaveSection[] { targetSection, lateFailure },
            target.RootStore,
            new IDungeonRestoreTransactionParticipant[] { observer });
        List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
        envelopes.First(envelope => string.Equals(
                envelope.sectionId,
                CodexSaveSection.Id,
                StringComparison.Ordinal))
            .payloadJson = candidatePayload;

        bool restored = registry.RestoreAll(
            envelopes,
            new DungeonGameRestoreReport());
        return !restored
            && observer.DiscardCount == 1
            && !observer.ObservedMarker
            && !targetRuntime.State.HasInfo(
                CodexEntryCategory.Invasion,
                markerEntryId,
                markerLine)
            && target.RootStore.PublishedRestoreRevision == 1;
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

    private sealed class CodexFailureSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection
    {
        public string SectionId => "codex.debug.late-failure";
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.Presentation;
        public IReadOnlyList<string> DependsOn => new[] { CodexSaveSection.Id };
        public int RemainingCommitFailures { get; set; }

        public string Capture() => "{}";

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion)
            {
                throw new InvalidOperationException("Codex scenario version mismatch.");
            }
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            return new DungeonDelegateSaveRestoreStage(SectionId, _ =>
            {
                if (RemainingCommitFailures <= 0)
                {
                    return;
                }

                RemainingCommitFailures--;
                throw new InvalidOperationException(
                    "Injected late Codex restore failure.");
            });
        }
    }

    private sealed class CodexDiscardObserver :
        IDungeonRestoreTransactionParticipant
    {
        private readonly CodexRuntime runtime;
        private readonly string entryId;
        private readonly string line;
        private bool hasCandidate;

        public CodexDiscardObserver(
            CodexRuntime runtime,
            string entryId,
            string line)
        {
            this.runtime = runtime;
            this.entryId = entryId;
            this.line = line;
        }

        public string ParticipantId => "codex.debug.discard-observer";
        public int DiscardCount { get; private set; }
        public bool ObservedMarker { get; private set; }

        public void BeginRestoreCandidate()
        {
            hasCandidate = true;
        }

        public void PublishRestoreCandidate()
        {
            hasCandidate = false;
        }

        public void DiscardRestoreCandidate()
        {
            if (!hasCandidate)
            {
                return;
            }

            hasCandidate = false;
            DiscardCount++;
            ObservedMarker = runtime.State.HasInfo(
                CodexEntryCategory.Invasion,
                entryId,
                line);
        }
    }

    private sealed class CodexScenarioWorld : IDisposable
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        public DungeonRuntimeAggregateRootStore RootStore { get; } =
            new DungeonRuntimeAggregateRootStore();

        public CodexRuntime CreateRuntime()
        {
            GameObject obj = new GameObject("CodexRuntime_Test");
            objects.Add(obj);
            CodexRuntime runtime = obj.AddComponent<CodexRuntime>();
            IFacilitySynthesisRecipeQuery recipeQuery = CreateSynthesisRecipeQuery();
            ScenarioCodexReferenceCatalog referenceCatalog = new ScenarioCodexReferenceCatalog();
            ScenarioBlueprintResearchStateService researchStateService =
                new ScenarioBlueprintResearchStateService();
            CodexRuntimeApplicationAdapter applicationAdapter =
                new CodexRuntimeApplicationAdapter(
                    researchStateService,
                    referenceCatalog,
                    recipeQuery,
                    CharacterAiEditorTestDependencies.GameEvents);
            runtime.ConstructCodexRuntime(
                applicationAdapter,
                new CodexReferenceImporter(applicationAdapter),
                RootStore);
            runtime.ImportReferenceData();
            return runtime;
        }

        public BuildableObject CreateFacility(string assetName)
        {
            BuildingSO building = LoadBuilding(assetName);
            GameObject obj = new GameObject(assetName);
            objects.Add(obj);
            BuildableObject facility = (building != null
                    ? building.runtimeArchetype
                    : BuildingRuntimeArchetypeKind.Generic)
                .AddComponent(obj);
            if (facility == null)
            {
                throw new InvalidOperationException($"{assetName} is not a BuildableObject.");
            }

            facility.ConstructBuildableObject(
                ScenarioBuildingDependencies.Instance,
                ScenarioBuildingDependencies.Instance,
                ScenarioBuildingDependencies.Instance,
                combatEquipmentRuntime: null,
                worldRegistry: null,
                worldItemStackRuntime: null,
                abilityRuntimeDispatcher: null,
                gameClock: null,
                paidFacilityContracts: null,
                evolutionState: new FacilityEvolutionStateComponentFactory());
            facility.RestorePersistentIdentity(
                (BuildingInstanceId)$"building:codex-fixture:{building.id}:{objects.Count}");
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
            CharacterAiEditorTestDependencies.EnsureCharacterProgression(obj);
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

        private sealed class ScenarioBuildingDependencies :
            IBuildingResearchWorkPort,
            IBuildingFacilityStateChangePort,
            IBuildingRoomPolicyPort
        {
            public static readonly ScenarioBuildingDependencies Instance =
                new ScenarioBuildingDependencies();

            private ScenarioBuildingDependencies()
            {
            }

            public bool HasResearchWorkFor(IBuildingWorldEntryPort facility)
            {
                return false;
            }

            public void MarkDynamicStateDirty()
            {
            }

            public bool IsFacilityRoleAvailable(
                IBuildingWorldEntryPort building,
                FacilityRole requestedRole,
                out string rejectReason)
            {
                rejectReason = string.Empty;
                return true;
            }

            public float GetRoomUtilityScore(
                IBuildingWorldEntryPort building,
                FacilityRole role)
            {
                return 0f;
            }

            public int GetEffectiveCapacity(IBuildingWorldEntryPort building)
            {
                return 0;
            }

            public BuildingRoomOperationalSnapshot GetOperationalProfile(
                IBuildingWorldEntryPort building)
            {
                return null;
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
