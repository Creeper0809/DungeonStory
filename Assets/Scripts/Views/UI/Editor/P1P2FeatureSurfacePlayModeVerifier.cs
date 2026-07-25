using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

[InitializeOnLoad]
public static class P1P2FeatureSurfacePlayModeVerifier
{
    public const string RequestPath = "Temp/p1-p2-ui-surface-verification.request";
    public const string ReportPath = "Temp/p1-p2-ui-surface-verification-report.txt";
    public const string ScreenshotPath = "Temp/p1-p2-ui-surface-verification.png";

    private const string OriginalActiveSceneKey = "DungeonStory.P1P2UiVerifier.OriginalActiveScene";
    private const string OpenedGameplaySceneKey =
        "DungeonStory.P1P2UiVerifier.OpenedGameplayScene";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";

    private static bool runnerCreated;
    private static bool preparingScenes;

    static P1P2FeatureSurfacePlayModeVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
    }

    [MenuItem("DungeonStory/Debug/QA/Request P1 P2 UI Surface Verification")]
    public static void RequestRunFromMenu()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(RequestPath, DateTime.Now.ToString("O"));
    }

    private static void OnEditorUpdate()
    {
        if (preparingScenes
            || EditorApplication.isPlayingOrWillChangePlaymode
            || !File.Exists(RequestPath))
        {
            return;
        }

        preparingScenes = true;
        try
        {
            PrepareScenesForPlayMode();
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            preparingScenes = false;
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            RestoreSceneState();
            runnerCreated = false;
            preparingScenes = false;
            return;
        }

        if (change != PlayModeStateChange.EnteredPlayMode
            || !File.Exists(RequestPath)
            || runnerCreated)
        {
            return;
        }

        runnerCreated = true;
        GameObject runnerObject = new GameObject("P1 P2 UI Surface Verification Runner");
        UnityEngine.Object.DontDestroyOnLoad(runnerObject);
        runnerObject.AddComponent<P1P2FeatureSurfaceVerificationRunner>();
    }

    private static void PrepareScenesForPlayMode()
    {
        Scene originalActiveScene = SceneManager.GetActiveScene();
        SessionState.SetString(OriginalActiveSceneKey, originalActiveScene.path ?? string.Empty);
        bool openedGameplayScene =
            SceneManager.sceneCount != 1
            || !string.Equals(
                originalActiveScene.path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase);
        Scene gameplayScene = originalActiveScene;
        if (openedGameplayScene)
        {
            gameplayScene = EditorSceneManager.OpenScene(
                GameplayScenePath,
                OpenSceneMode.Single);
        }

        SessionState.SetBool(OpenedGameplaySceneKey, openedGameplayScene);
        if (!gameplayScene.IsValid() || !gameplayScene.isLoaded)
        {
            throw new InvalidOperationException(
                "P1/P2 UI verification could not load GameplayScene.");
        }

        SceneManager.SetActiveScene(gameplayScene);
    }

    private static void RestoreSceneState()
    {
        string originalScenePath = SessionState.GetString(OriginalActiveSceneKey, string.Empty);
        if (SessionState.GetBool(OpenedGameplaySceneKey, false)
            && !string.IsNullOrWhiteSpace(originalScenePath)
            && !string.Equals(
                originalScenePath,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
        }

        SessionState.EraseString(OriginalActiveSceneKey);
        SessionState.SetBool(OpenedGameplaySceneKey, false);
    }

    private sealed class P1P2FeatureSurfaceVerificationRunner : MonoBehaviour
    {
        private readonly List<string> capturedErrors = new List<string>();
        private readonly List<string> capturedWarnings = new List<string>();
        private readonly List<UnityEngine.Object> tempObjects = new List<UnityEngine.Object>();
        private HashSet<int> baselineBuildableIds = new HashSet<int>();
        private bool capturingLogs;
        private CharacterActor guard;
        private CharacterActor rebel;
        private DefenseFacility defenseFixture;
        private FacilitySynthesisRecipeSO synthesisRecipe;
        private List<BuildableObject> synthesisMaterials = new List<BuildableObject>();
        private int synthesisMaterialActionIndex = -1;
        private string synthesisMaterialActionName = string.Empty;
        private int synthesisRecipeActionIndex = -1;
        private string synthesisPreparationReason = string.Empty;
        private InputSettings.EditorInputBehaviorInPlayMode originalInputBehavior;
        private Mouse originalMouse;
        private Mouse verificationMouse;
        private bool inputConfigured;

        private IEnumerator Start()
        {
            yield return Run();
        }

        private IEnumerator Run()
        {
            List<string> lines = new List<string>();
            Directory.CreateDirectory("Temp");
            yield return EnsureGameplaySceneActive(lines);
            yield return null;
            yield return null;

            baselineBuildableIds = FindActiveSceneComponents<BuildableObject>()
                .Where((building) => building != null)
                .Select((building) => building.GetInstanceID())
                .ToHashSet();

            ClearConsole();
            StartLogCapture();
            ConfigureInput();
            float originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 1f;
                UITabManager manager = FindActiveSceneComponent<UITabManager>();
                lines.Add($"activeScene={SceneManager.GetActiveScene().path}; tabManager={manager != null}");
                if (manager == null)
                {
                    yield break;
                }

                PlayModeVerificationPersistenceSnapshot.CaptureCurrent("p1-p2-ui-surface");
                yield return DismissStartupAndSelectOwner(lines);
                RunStep("SETUP", lines, () => PrepareGameState(lines));
                yield return null;
                yield return null;

                RunStep("ROOM_IDENTITY", lines, () => VerifyRoomIdentity(manager, lines));
                yield return null;
                yield return Capture("Temp/p1-ui-room-identity.png", lines);
                yield return null;

                RunStep("FACILITIES", lines, () => VerifyFacilities(manager, lines));
                yield return null;
                yield return Capture("Temp/p1-ui-facilities.png", lines);
                yield return null;

                RunStep("OPERATION", lines, () => VerifyRunAndEconomy(manager, lines));
                yield return null;
                yield return Capture("Temp/p1-p2-ui-operation.png", lines);
                yield return null;

                yield return VerifyDefense(manager, lines);
                yield return null;
                yield return Capture("Temp/p1-ui-defense.png", lines);
                yield return null;

                RunStep("OFFENSE", lines, () => VerifyOffense(manager, lines));
                yield return null;
                yield return Capture("Temp/p1-ui-offense.png", lines);
                yield return null;

                RunStep("STAFF", lines, () => VerifyStaff(manager, lines));
                yield return null;
                yield return Capture("Temp/p1-ui-staff.png", lines);
                yield return null;

                RunStep("SHOP", lines, () => VerifyShopDetail(manager, lines));
                yield return null;
                yield return Capture("Temp/p2-ui-shop.png", lines);
                yield return null;

                RunStep("CODEX", lines, () => VerifyCodex(manager, lines));
                yield return null;
                yield return Capture("Temp/p2-ui-codex.png", lines);
                yield return null;

                RunStep("ARCHIVE", lines, () => VerifyArchiveAndEvents(manager, lines));
                yield return null;
                yield return Capture("Temp/p2-ui-events.png", lines);
                yield return null;

                RunStep("VISUAL", lines, () =>
                {
                    RunUiBounds(lines);
                    ScreenCapture.CaptureScreenshot(ScreenshotPath);
                    lines.Add($"screenCapture={ScreenshotPath}");
                });
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                TeardownInput();
                DestroyTempObjects();
                Finish(lines);
            }
        }

        private IEnumerator DismissStartupAndSelectOwner(List<string> lines)
        {
            yield return null;
            GameObject modal = FindSceneObject("SaveModal");
            Button startNewButton = FindActiveButtons("StartNewRunButton", exact: true).FirstOrDefault();
            bool startupWasVisible = modal != null && modal.activeInHierarchy;
            if (startupWasVisible)
            {
                yield return ClickWithInput(startNewButton);
                if (modal != null && modal.activeInHierarchy)
                {
                    yield return ClickWithInput(startNewButton);
                }
            }

            bool startupStillVisible = modal != null && modal.activeInHierarchy;
            lines.Add($"startupModal={startupWasVisible}->{startupStillVisible}; newGamePointer={startNewButton != null}");

            OwnerRunManager ownerManager = FindActiveSceneComponent<OwnerRunManager>();
            if (ownerManager != null && ownerManager.CurrentOwnerActor == null)
            {
                Button ownerButton = UnityEngine.Object.FindObjectsByType<Button>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .FirstOrDefault((candidate) => candidate != null
                        && candidate.gameObject.activeInHierarchy
                        && candidate.name.StartsWith("OwnerOption_", StringComparison.Ordinal));
                yield return ClickWithInput(ownerButton);
                yield return StartPartyPlayModeTestDriver.CompleteIfVisible();
            }

            lines.Add($"ownerSelected={ownerManager != null && ownerManager.CurrentOwnerActor != null}; timeScale={Time.timeScale:0.##}");
        }

        private IEnumerator ClickWithInput(Button button)
        {
            if (button == null || !button.gameObject.activeInHierarchy || !button.interactable || verificationMouse == null)
            {
                yield break;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            verificationMouse.MakeCurrent();
            InputSystem.QueueStateEvent(
                verificationMouse,
                new MouseState { position = point }.WithButton(MouseButton.Left, true));
            yield return null;
            yield return null;
            verificationMouse.MakeCurrent();
            InputSystem.QueueStateEvent(verificationMouse, new MouseState { position = point });
            yield return null;
            yield return null;
        }

        private void PrepareGameState(List<string> lines)
        {
            foreach (LocalLlmRequestQueue queue in FindActiveSceneComponents<LocalLlmRequestQueue>())
            {
                queue.ClearForDebug();
                queue.SetWarningLogsSuppressedForDebug(true);
            }

            foreach (SocialReputationRuntime social in FindActiveSceneComponents<SocialReputationRuntime>())
            {
                social.SetWarningLogsSuppressedForDebug(true);
            }

            foreach (AiDirectorRuntime director in FindActiveSceneComponents<AiDirectorRuntime>())
            {
                director.SetWarningLogsSuppressedForDebug(true);
            }

            foreach (CharacterAiScheduler scheduler in FindActiveSceneComponents<CharacterAiScheduler>())
            {
                scheduler.enabled = false;
            }

            guard = CreateQaCharacter("P1 UI Guard", CharacterType.NPC, addWork: true);
            rebel = CreateQaCharacter("P1 UI Rebel", CharacterType.NPC, addWork: true);
            guard.GetAbility<AbilityWork>()?.SetWorkPriority(BuiltInWorkTypeIds.Guard, WorkPriorityLevel.Priority1);
            Dictionary<CharacterCondition, float> rebelStats =
                new Dictionary<CharacterCondition, float>(rebel.stats);
            rebelStats[CharacterCondition.MOOD] = 0f;
            rebel.stats = rebelStats;
            rebel.Stats.ApplyMoodFactor(
                "qa:p1-local-rebellion",
                "검증용 극심한 불만",
                -100f,
                600f,
                1);

            StaffDiscontentRuntime discontent = FindActiveSceneComponent<StaffDiscontentRuntime>();
            StaffDiscontentOutcome outcome = StaffDiscontentOutcome.None;
            StaffDiscontentRecord record = discontent != null
                ? discontent.ProcessStaff(rebel, out outcome)
                : null;
            lines.Add($"preparedStaff={guard != null && rebel != null}; rebellion={record?.IsInLocalRebellion}; outcome={outcome}");

            BuildingSO defenseSource = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/P1/P1_PoisonPool.asset");
            if (defenseSource != null)
            {
                BuildingSO defenseData = Instantiate(defenseSource);
                defenseData.objectName = "P1 UI Defense Fixture";
                tempObjects.Add(defenseData);
                GameObject defenseObject = new GameObject("P1 UI Defense Fixture");
                tempObjects.Add(defenseObject);
                defenseFixture = defenseObject.AddComponent<DefenseFacility>();
                InjectGameObjectFromLifetimeScope(defenseObject);
                defenseFixture.Initialization(defenseData, new Vector2Int(-40, -40));
            }

            InvasionThreatRuntime threat = FindActiveSceneComponent<InvasionThreatRuntime>();
            threat?.AddThreat(1000f);

            OffenseWorldMapRuntime worldMap = FindActiveSceneComponent<OffenseWorldMapRuntime>();
            if (worldMap != null && worldMap.VisibleTargets.Count == 0)
            {
                worldMap.StartWorldMap(2);
            }

            CodexRuntime codex = FindActiveSceneComponent<CodexRuntime>();
            codex?.ImportReferenceData();
            BuildableObject facility = FindActiveSceneComponents<BuildableObject>()
                .FirstOrDefault((building) => building != null && !building.isDestroy);
            DungeonStory.Foundation.IGameEventBus gameEventBus =
                ResolveFromLifetimeScope<DungeonStory.Foundation.IGameEventBus>();
            gameEventBus?.Publish(new FacilityVisitEvent(guard, facility));
            gameEventBus?.Publish(new FacilityRevenueEvent(guard, facility, 123));
            gameEventBus?.Publish(new FacilityStockConsumedEvent(
                guard,
                facility,
                StockCategory.General,
                2));
            gameEventBus.RaiseAlert("P2 UI High Event", "상세 이벤트 기록", EventAlertImportance.High, "QA");
            gameEventBus.RaiseAlert("P2 UI Low Event", "필터 확인 기록", EventAlertImportance.Low, "QA");

            PrepareSynthesis(lines);
            lines.Add($"preparedDefense={defenseFixture != null}; visibleTargets={worldMap?.VisibleTargets.Count ?? -1}");
        }

        private void PrepareSynthesis(List<string> lines)
        {
            FacilitySynthesisRuntime runtime = FindActiveSceneComponent<FacilitySynthesisRuntime>();
            List<BuildableObject> facilities = FindActiveSceneComponents<BuildableObject>()
                .Where((building) => building != null && !building.isDestroy && building.BuildingData != null)
                .ToList();
            synthesisRecipe = null;
            synthesisMaterials = new List<BuildableObject>();
            synthesisPreparationReason = runtime == null ? "runtime missing" : "no valid placed combination";
            if (runtime != null)
            {
                bool fixtureReady = TryCreateSynthesisRoomFixture(
                    runtime,
                    out FacilitySynthesisRecipeSO fixtureRecipe,
                    out List<BuildableObject> fixtureMaterials,
                    out string fixtureReason);
                string fixtureValidationReason = "formal room fixture was not validated";
                if (fixtureReady && TryFindValidSynthesisMaterials(
                        runtime,
                        fixtureRecipe,
                        fixtureMaterials,
                        out List<BuildableObject> validatedFixtureMaterials,
                        out fixtureValidationReason))
                {
                    synthesisRecipe = fixtureRecipe;
                    synthesisMaterials = validatedFixtureMaterials;
                    synthesisPreparationReason = "validated formal room fixture";
                }
                else
                {
                    synthesisPreparationReason = fixtureReady ? fixtureValidationReason : fixtureReason;
                    foreach (FacilitySynthesisRecipeSO recipe in runtime.VisibleRecipes)
                    {
                        if (!TryFindValidSynthesisMaterials(
                                runtime,
                                recipe,
                                facilities,
                                out List<BuildableObject> materials,
                                out string reason))
                        {
                            synthesisPreparationReason = reason;
                            continue;
                        }

                        synthesisRecipe = recipe;
                        synthesisMaterials = materials;
                        synthesisPreparationReason = "validated placed combination";
                        break;
                    }
                }
            }

            runtime?.ClearSelection();
            if (runtime != null && synthesisRecipe != null)
            {
                List<BuildableObject> orderedFacilities = facilities
                    .OrderBy(GetBuildingName)
                    .ToList();
                BuildableObject clickedMaterial = synthesisMaterials[synthesisMaterials.Count - 1];
                synthesisMaterialActionIndex = orderedFacilities.IndexOf(clickedMaterial);
                synthesisMaterialActionName = $"P1Action_SynthesisMaterial_{clickedMaterial.GetInstanceID()}";
                synthesisRecipeActionIndex = runtime.VisibleRecipes.ToList().IndexOf(synthesisRecipe);
                foreach (BuildableObject material in synthesisMaterials.Take(synthesisMaterials.Count - 1))
                {
                    runtime.ToggleMaterialSelection(material);
                }
            }

            lines.Add($"preparedSynthesis={synthesisRecipe != null}; materials={synthesisMaterials.Count}; materialAction={synthesisMaterialActionIndex}; recipeAction={synthesisRecipeActionIndex}; reason={synthesisPreparationReason}");
        }

        private void PrepareEvolution(List<string> lines)
        {
            FacilityEvolutionRuntime runtime = FindActiveSceneComponent<FacilityEvolutionRuntime>();
            BuildableObject selectedFacility = null;
            FacilityEvolutionCandidate selectedCandidate = null;
            foreach (BuildableObject facility in FindActiveSceneComponents<BuildableObject>()
                .Where((candidate) => candidate != null && !synthesisMaterials.Contains(candidate)))
            {
                FacilityEvolutionCandidate candidate = runtime?
                    .GetCandidates(facility, includeRejected: true)
                    .FirstOrDefault();
                if (candidate == null)
                {
                    continue;
                }

                selectedFacility = facility;
                selectedCandidate = candidate;
                if (candidate.Approved)
                {
                    break;
                }
            }

            string fixtureReason = string.Empty;
            if ((selectedCandidate == null || !selectedCandidate.Approved)
                && TryCreateEvolutionRoomFixture(
                    runtime,
                    out BuildableObject evolutionFixture,
                    out fixtureReason))
            {
                selectedFacility = evolutionFixture;
                selectedCandidate = runtime
                    .GetCandidates(selectedFacility, includeRejected: true)
                    .FirstOrDefault();
            }

            bool prepared = selectedCandidate != null && selectedCandidate.Approved;
            if (!prepared && selectedFacility != null && selectedCandidate != null)
            {
                MethodInfo method = typeof(FullGameManualQaRuntimeProbe).GetMethod(
                    "PrepareFacilityForEvolutionQa",
                    BindingFlags.Static | BindingFlags.NonPublic);
                prepared = method != null
                    && method.Invoke(null, new object[] { selectedFacility, selectedCandidate }) is bool value
                    && value;
                foreach (GameObject fixture in UnityEngine.Object.FindObjectsByType<GameObject>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None).Where((obj) => obj != null && obj.name == "QA Evolution Fixture"))
                {
                    if (!tempObjects.Contains(fixture))
                    {
                        tempObjects.Add(fixture);
                    }
                }

                ResolveFromLifetimeScope<IRoomLayoutCache>()?.Clear();
                FacilityEvolutionCandidate refreshed = runtime
                    .GetCandidates(selectedFacility, includeRejected: true)
                    .FirstOrDefault((candidate) => candidate?.Recipe == selectedCandidate.Recipe);
                selectedCandidate = refreshed ?? selectedCandidate;
                prepared = prepared && selectedCandidate.Approved;
                if (!prepared && ApplyEvolutionQaContribution(selectedFacility, selectedCandidate.Recipe))
                {
                    ResolveFromLifetimeScope<IRoomLayoutCache>()?.Clear();
                    refreshed = runtime
                        .GetCandidates(selectedFacility, includeRejected: true)
                        .FirstOrDefault((candidate) => candidate?.Recipe == selectedCandidate.Recipe);
                    selectedCandidate = refreshed ?? selectedCandidate;
                    prepared = selectedCandidate.Approved;
                }
            }

            string failedChecks = selectedCandidate?.Validation?.Checks != null
                ? string.Join("|", selectedCandidate.Validation.Checks
                    .Where((check) => !check.Passed)
                    .Select((check) => $"{check.Category}/{check.Label}/{check.Detail}"))
                : string.Empty;
            RoomProfile profile = selectedFacility != null ? runtime?.BuildContext(selectedFacility)?.Profile : null;
            lines.Add($"preparedEvolution={prepared}; facility={selectedFacility?.name ?? "<none>"}; recipe={selectedCandidate?.Recipe?.name ?? "<none>"}; approved={selectedCandidate?.Approved}; failed={failedChecks}; reason={selectedCandidate?.Reason}; fixture={fixtureReason}; seats={profile?.GetMetric(FacilityEvolutionTerms.SeatCount) ?? 0f:0.##}; luxury={profile?.GetScore(FacilityEvolutionTerms.Luxury) ?? 0f:0.##}; luxuryPerSeat={profile?.GetMetric(FacilityEvolutionTerms.LuxuryPerSeat) ?? 0f:0.##}");
        }

        private void VerifyRoomIdentity(UITabManager manager, List<string> lines)
        {
            OpenTab(manager, 1);
            int roomCards = CountButtons("P1Action_RoomInspect_");
            const string formalBoundaryText = "문 1 / 벽 1";
            bool roomClicked = roomCards > 0
                && ClickByCardText("P1Action_RoomInspect_", formalBoundaryText);
            bool roomSelected = FindActiveButtons("P1Action_RoomInspect_")
                .Any((button) => CardTextContains(button, formalBoundaryText)
                    && GetButtonText(button).Contains("선택됨"));
            lines.Add($"roomIdentityVisible={roomCards > 0}; cards={roomCards}; formalBoundaryClicked={roomClicked}; selected={roomSelected}");
        }

        private void VerifyFacilities(UITabManager manager, List<string> lines)
        {
            FacilitySynthesisRuntime synthesis = FindActiveSceneComponent<FacilitySynthesisRuntime>();
            FacilityEvolutionRuntime evolution = FindActiveSceneComponent<FacilityEvolutionRuntime>();
            OpenTab(manager, 1);

            int selectedBefore = synthesis?.SelectedMaterials.Count ?? -1;
            bool materialClicked = !string.IsNullOrWhiteSpace(synthesisMaterialActionName)
                ? ClickExact(synthesisMaterialActionName)
                : ClickFirst("P1Action_SynthesisMaterial_");
            int selectedAfterMaterial = synthesis?.SelectedMaterials.Count ?? -1;
            bool executeClicked = synthesisRecipeActionIndex >= 0
                ? ClickExact($"P1Action_SynthesisExecute_{synthesisRecipeActionIndex}")
                : ClickFirst("P1Action_SynthesisExecute_");
            bool synthesized = synthesis != null && executeClicked && synthesis.SelectedMaterials.Count == 0;
            AddResult(lines, "P1-02 FACILITY_SYNTHESIS", materialClicked && selectedAfterMaterial != selectedBefore && synthesized,
                $"visible={IsTabActive(1)}; materialClicked={materialClicked}; selected={selectedBefore}->{selectedAfterMaterial}->{synthesis?.SelectedMaterials.Count}; executeClicked={executeClicked}; synthesized={synthesized}");

            PrepareEvolution(lines);
            OpenTab(manager, 1);
            int signatureBefore = FacilitySignature();
            Button evolveButton = FindButtonByLabel("P1Action_EvolutionExecute_", "진화");
            bool evolveClicked = Click(evolveButton);
            int signatureAfter = FacilitySignature();
            AddResult(lines, "P1-03 FACILITY_EVOLUTION", evolveClicked && signatureAfter != signatureBefore,
                $"button={evolveButton != null}; clicked={evolveClicked}; signature={signatureBefore}->{signatureAfter}; runtime={evolution != null}");
        }

        private void VerifyRunAndEconomy(UITabManager manager, List<string> lines)
        {
            RunVariableRuntime run = FindActiveSceneComponent<RunVariableRuntime>();
            OperatingDaySettlementRuntime settlement = FindActiveSceneComponent<OperatingDaySettlementRuntime>();
            DungeonStory.Foundation.IGameEventBus gameEventBus =
                ResolveFromLifetimeScope<DungeonStory.Foundation.IGameEventBus>();
            if (run != null && !run.State.HasStarted)
            {
                gameEventBus?.Publish(new OperatingDayStartedEvent(1));
            }

            if (run != null && run.State.ActiveOperationVariables.Count == 0)
            {
                gameEventBus?.Publish(new OperatingDayStartedEvent(2));
            }

            OpenTab(manager, 5);

            bool startStateVisible = HasActiveObject("P1State_RunStartVariables");
            bool operationStateVisible = HasActiveObject("P1State_RunOperationVariables")
                || HasActiveObjectStartingWith("P1State_RunOperationVariable_");
            bool invasionStateVisible = HasActiveObject("P1State_RunInvasionVariable");
            int publicVariableShortcuts = CountButtons("P1Action_Run");
            AddResult(lines, "P1-01 RUN_INVASION_VARIABLES",
                run?.State.HasStarted == true
                && startStateVisible
                && operationStateVisible
                && invasionStateVisible
                && publicVariableShortcuts == 0,
                $"started={run?.State.HasStarted}; operationCount={run?.State.ActiveOperationVariables.Count ?? -1}; invasion={run?.State.CurrentInvasionVariable?.id ?? "waiting"}; states={startStateVisible}/{operationStateVisible}/{invasionStateVisible}; publicShortcuts={publicVariableShortcuts}");

            bool settleShortcutPresent = FindActiveButtons("P0Action_OperationSettleDay", exact: true).Count > 0;
            bool currentClicked = ClickExact("P2Action_EconomyCurrent");
            bool historyClicked = ClickExact("P2Action_EconomyHistory");
            bool historySelected = ButtonTextContains("P2Action_EconomyHistory", "선택됨");
            AddResult(lines, "P2-03 ECONOMY_HUD_DETAIL",
                !settleShortcutPresent && currentClicked && historyClicked && historySelected && settlement != null,
                $"settleShortcutPresent={settleShortcutPresent}; currentClicked={currentClicked}; historyClicked={historyClicked}; historySelected={historySelected}; reports={settlement?.ReportHistory.Count ?? -1}; revenue={settlement?.LatestReport?.totalRevenue ?? -1}");
            ScrollActivePanel(0f);
        }

        private IEnumerator VerifyDefense(UITabManager manager, List<string> lines)
        {
            InvasionThreatRuntime threat = FindActiveSceneComponent<InvasionThreatRuntime>();
            InvasionDirectorRuntime director = FindActiveSceneComponent<InvasionDirectorRuntime>();
            InvasionCombatReportRuntime reports = FindActiveSceneComponent<InvasionCombatReportRuntime>();
            RunVariableRuntime run = FindActiveSceneComponent<RunVariableRuntime>();
            OpenTab(manager, 6);

            float threatBefore = threat?.CurrentThreat ?? -1f;
            threat?.Tick(1f);
            float threatAfter = threat?.CurrentThreat ?? -1f;
            bool threatStateVisible = HasActiveObject("P1State_Threat");
            bool threatShortcutPresent = FindActiveButtons("P1Action_ThreatIncrease", exact: true).Count > 0;
            AddResult(lines, "P1-04 INVASION_THREAT",
                threat != null && threatAfter >= threatBefore && threatStateVisible && !threatShortcutPresent,
                $"naturalTick={threatBefore:0.#}->{threatAfter:0.#}; stateVisible={threatStateVisible}; publicShortcut={threatShortcutPresent}; stage={threat?.CurrentStage}");

            int intrudersBefore = director?.ActiveIntruders.Count ?? -1;
            bool spawnShortcutPresent = FindActiveButtons("P1Action_IntruderSpawn", exact: true).Count > 0;
            bool patternPrepared = run?.SelectInvasionVariable(RunVariableIds.LootPriority, false) != null;
            bool spawnedForVerification = director != null
                && threat != null
                && director.TrySpawnIntruder(threat.LatestSnapshot, out _);
            OpenTab(manager, 6);
            int intrudersAfter = director?.ActiveIntruders.Count ?? -1;
            InvasionIntruderRuntime patternIntruder = director?.ActiveIntruders.FirstOrDefault();
            Button trackingButton = FindActiveButtons("P1Action_IntruderTrack_").FirstOrDefault();
            bool patternVisible = patternIntruder?.Pattern.id == InvasionIntruderPatternIds.Plunderer
                && CardTextContains(trackingButton, "약탈자");
            bool trackClicked = Click(trackingButton);
            AddResult(lines, "P1-05 INTRUDER_TRACKING",
                !spawnShortcutPresent
                && patternPrepared
                && spawnedForVerification
                && intrudersAfter > intrudersBefore
                && patternVisible
                && trackClicked,
                $"publicSpawnShortcut={spawnShortcutPresent}; patternPrepared={patternPrepared}; internalFixtureSpawn={spawnedForVerification}; active={intrudersBefore}->{intrudersAfter}; pattern={patternIntruder?.Pattern.id}; patternVisible={patternVisible}; trackClicked={trackClicked}");

            yield return null;
            OpenTab(manager, 6);
            yield return Capture("Temp/p1-ui-intruder-pattern.png", lines);

            CharacterActor target = director?.ActiveIntruders
                .Select((intruder) => intruder?.IntruderActor)
                .FirstOrDefault((actor) => actor != null);
            if (target != null && threat != null)
            {
                ResolveFromLifetimeScope<DungeonStory.Foundation.IGameEventBus>()
                    ?.Publish(new InvasionStartedEvent(threat.LatestSnapshot));
                ResolveFromLifetimeScope<DungeonStory.Foundation.IGameEventBus>()
                    ?.Publish(new InvasionSpawnedEvent(target, threat.LatestSnapshot));
            }

            OpenTab(manager, 6);
            DefenseStatusRuntime status = target != null ? target.GetComponent<DefenseStatusRuntime>() : null;
            int statusesBefore = status?.ActiveStatuses.Count ?? 0;
            float cooldownBefore = defenseFixture?.CooldownRemaining ?? 0f;
            bool defenseShortcutPresent = CountButtons("P1Action_DefenseTrigger_") > 0;
            IDefenseStatusRuntimeService statusRuntime = ResolveFromLifetimeScope<IDefenseStatusRuntimeService>();
            DefenseActivationReport automaticReport = target != null && defenseFixture != null && statusRuntime != null
                ? defenseFixture.Trigger(target, DefenseTriggerTiming.OnEnter, statusRuntime)
                : null;
            OpenTab(manager, 6);
            bool defenseStateVisible = HasActiveObjectStartingWith("P1State_DefenseFacility_");
            int statusesAfter = status?.ActiveStatuses.Count ?? 0;
            float cooldownAfter = defenseFixture?.CooldownRemaining ?? 0f;
            AddResult(lines, "P1-06 DEFENSE_EFFECT_COOLDOWN",
                !defenseShortcutPresent
                && defenseStateVisible
                && automaticReport != null
                && (cooldownAfter > cooldownBefore || statusesAfter > statusesBefore),
                $"publicTriggerShortcut={defenseShortcutPresent}; stateVisible={defenseStateVisible}; automaticReport={automaticReport != null}; cooldown={cooldownBefore:0.###}->{cooldownAfter:0.###}; statuses={statusesBefore}->{statusesAfter}");

            int reportsBefore = reports?.ReportHistory.Count ?? -1;
            ResolveFromLifetimeScope<DungeonStory.Foundation.IGameEventBus>()
                ?.Publish(new InvasionResolvedEvent(true, 0f));
            OpenTab(manager, 6);
            bool reportClicked = ClickFirst("P1Action_CombatReport_");
            int reportsAfter = reports?.ReportHistory.Count ?? -1;
            AddResult(lines, "P1-07 INVASION_COMBAT_REPORT", reportsAfter > reportsBefore && reportClicked,
                $"history={reportsBefore}->{reportsAfter}; reportClicked={reportClicked}; currentResolved={reports?.CurrentReport?.IsResolved}");
            ScrollActivePanel(0f);
        }

        private void VerifyOffense(UITabManager manager, List<string> lines)
        {
            OffenseWorldMapRuntime map = FindActiveSceneComponent<OffenseWorldMapRuntime>();
            OffenseExpeditionRuntime expedition = FindActiveSceneComponent<OffenseExpeditionRuntime>();
            OffenseRewardRuntime rewards = FindActiveSceneComponent<OffenseRewardRuntime>();
            OpenTab(manager, 7);

            bool mapClicked = ClickExact("P1Action_OffenseOpenMap");
            bool mapVisible = UnityEngine.Object.FindFirstObjectByType<OffenseWorldMapPanel>(FindObjectsInactive.Exclude) != null;
            CloseOffensePanels();
            OpenTab(manager, 7);
            bool expeditionClicked = ClickExact("P1Action_OffenseOpenExpedition");
            bool expeditionVisible = UnityEngine.Object.FindFirstObjectByType<OffenseExpeditionPanel>(FindObjectsInactive.Exclude) != null;
            CloseOffensePanels();
            OpenTab(manager, 7);
            AddResult(lines, "P1-10 OFFENSE_TAB_CONNECTION", mapClicked && mapVisible && expeditionClicked && expeditionVisible,
                $"mapClicked={mapClicked}; mapVisible={mapVisible}; expeditionClicked={expeditionClicked}; expeditionVisible={expeditionVisible}");

            bool targetMapClicked = ClickExact("P1Action_OffenseOpenMap");
            bool targetClicked = targetMapClicked && ClickExact("Button_외곽 식재료 농장");
            bool targetMapClosed = ClickExact("Button_닫기");
            bool compositionClicked = ClickExact("P1Action_OffenseOpenExpedition");
            bool memberClicked = compositionClicked && ClickFirstOffenseExpeditionMember();
            int activeBefore = expedition?.ActiveExpeditions.Count ?? -1;
            int historyBefore = expedition?.ResultHistory.Count ?? -1;
            int campaignBefore = map?.State.CompletedTargetCount ?? -1;
            bool startClicked = memberClicked && ClickExact("Button_원정 출발");
            int activeAfterStart = expedition?.ActiveExpeditions.Count ?? -1;
            IOffenseBattleRuntime battle = ResolveFromLifetimeScope<IOffenseBattleRuntime>();
            bool routeAdvanced = startClicked && AdvanceOffenseJourneyToFirstBattle(expedition, battle);
            OffenseBattleSession startedBattle = battle?.Session;
            OffenseBattleCombatant enemy = battle?.Session?.Combatants
                .FirstOrDefault(combatant => combatant.Team == OffenseBattleTeam.Enemies && !combatant.IsDead);
            float enemyHealthBefore = enemy?.CurrentHealth ?? -1f;
            long commandBefore = startedBattle?.LastProcessedCommandId ?? -1;
            bool attackClicked = ClickExact("Button_공격");
            bool targetClickedInBattle = ClickFirst("Combatant_enemy:");
            bool attackConfirmed = targetClickedInBattle
                && ClickExact("Button_공격 확정");
            float enemyHealthAfter = enemy?.CurrentHealth ?? -1f;
            long commandAfter = startedBattle?.LastProcessedCommandId ?? -1;
            bool returnedToRoute = battle != null
                && !battle.HasActiveBattle
                && expedition?.ActiveExpeditions.FirstOrDefault()?.Phase == OffenseExpeditionPhase.ChoosingRoute;
            bool dungeonClicked = returnedToRoute || ClickExact("Button_던전 보기");
            bool dungeonVisible = returnedToRoute || battle != null && !battle.IsBattleViewVisible;
            bool returnClicked = returnedToRoute || ClickExact("Button_전투 복귀");
            bool battleVisibleAgain = returnedToRoute || battle != null && battle.IsBattleViewVisible;
            int historyAfter = expedition?.ResultHistory.Count ?? -1;
            int campaignAfter = map?.State.CompletedTargetCount ?? -1;
            bool battleStillInProgress = historyAfter == historyBefore
                && campaignAfter == campaignBefore;
            AddResult(lines, "P1-11 DIRECT_TURN_BATTLE",
                targetClicked
                    && targetMapClosed
                    && compositionClicked
                    && memberClicked
                    && startClicked
                    && routeAdvanced
                    && activeAfterStart > activeBefore
                    && battle != null
                    && startedBattle != null
                    && attackClicked
                    && targetClickedInBattle
                    && attackConfirmed
                    && commandAfter > commandBefore
                    && enemyHealthAfter < enemyHealthBefore
                    && dungeonClicked
                    && dungeonVisible
                    && returnClicked
                    && battleVisibleAgain
                    && (battleStillInProgress || returnedToRoute)
                    && map != null
                    && !map.State.TruthRevealed,
                $"map={targetMapClicked}/{targetMapClosed}; targetClicked={targetClicked}; composition={compositionClicked}; memberClicked={memberClicked}; startClicked={startClicked}; routeAdvanced={routeAdvanced}; attack={attackClicked}/{targetClickedInBattle}/{attackConfirmed}; active={activeBefore}->{activeAfterStart}; command={commandBefore}->{commandAfter}; enemyHp={enemyHealthBefore:0.#}->{enemyHealthAfter:0.#}; outcome={startedBattle?.Outcome}; returnedToRoute={returnedToRoute}; dungeon={dungeonClicked}/{dungeonVisible}; return={returnClicked}/{battleVisibleAgain}; history={historyBefore}->{historyAfter}; campaign={campaignBefore}->{campaignAfter}; truth={map?.State.TruthRevealed}");
            ScrollActivePanel(0f);
        }

        private static bool AdvanceOffenseJourneyToFirstBattle(
            OffenseExpeditionRuntime runtime,
            IOffenseBattleRuntime battle)
        {
            int safety = 0;
            while (runtime?.ActiveExpeditions.Count > 0
                && battle != null
                && !battle.HasActiveBattle
                && safety++ < 12)
            {
                OffenseExpeditionRun active = runtime.ActiveExpeditions[0];
                string label = active.Phase switch
                {
                    OffenseExpeditionPhase.ChoosingRoute => active.GetAvailableRouteNodes()
                        .FirstOrDefault()?.Title,
                    OffenseExpeditionPhase.ResolvingNode when active.CurrentNode?.Kind == OffenseRouteNodeKind.Cache =>
                        "보급고 수색",
                    OffenseExpeditionPhase.ResolvingNode when active.CurrentNode?.Kind == OffenseRouteNodeKind.Camp =>
                        "쉬지 않고 전진",
                    OffenseExpeditionPhase.ResolvingNode => "위험 감수",
                    _ => string.Empty
                };
                if (!ClickButtonContaining(label)) return false;
            }

            return battle?.HasActiveBattle == true;
        }

        private void VerifyStaff(UITabManager manager, List<string> lines)
        {
            StaffDiscontentRuntime discontent = FindActiveSceneComponent<StaffDiscontentRuntime>();
            GridPathSearchResult guardSearch = guard?.Brain?.GetPathSearch(guard);
            List<BuildableObject> reachableBuildings = guardSearch?.GetAllReachableBuilding()
                .Where((building) => building != null && !building.isDestroy)
                .ToList() ?? new List<BuildableObject>();
            BuildableObject priorityFixture = null;
            foreach (BuildableObject building in reachableBuildings)
            {
                if (building.Facility == null
                    || !building.Facility.SupportsWork(BuiltInWorkTypeIds.Repair)
                    || building is not IWorkableFacility workable)
                {
                    continue;
                }

                bool wasDamaged = building.IsDamaged;
                building.SetDamaged(true);
                bool commandResolved = WorkCommandResolver.TryResolveFacilityCommand(
                    guard,
                    building,
                    out WorkTypeId workTypeId,
                    out _);
                bool workerAssignable = workable.GetWorkerAssignmentStatus(guard).IsAllowed;
                if (commandResolved
                    && workTypeId == BuiltInWorkTypeIds.Repair
                    && workerAssignable)
                {
                    priorityFixture = building;
                    break;
                }

                if (!wasDamaged)
                {
                    building.SetDamaged(false);
                }
            }
            OpenTab(manager, 2);
            bool modeClicked = ClickExact("P1Action_StaffModeManagement");
            bool guardSelected = ClickByCardText("P1Action_StaffSelect_", guard.name);
            AbilityWork guardWork = guard.GetAbility<AbilityWork>();
            bool dutyBefore = guardWork.IsOffDuty;
            bool dutyClicked = ClickExact("P1Action_StaffDutyToggle");
            bool dutyAfter = guardWork.IsOffDuty;
            AddResult(lines, "P1-12 STAFF_DUTY_REST", modeClicked && guardSelected && dutyClicked && dutyAfter != dutyBefore,
                $"modeClicked={modeClicked}; selected={guardSelected}; dutyClicked={dutyClicked}; offDuty={dutyBefore}->{dutyAfter}");
            bool resumedDuty = ClickExact("P1Action_StaffDutyToggle") && !guardWork.IsOffDuty;

            bool rebelSelected = ClickByCardText("P1Action_StaffSelect_", rebel.name);
            bool discontentStateVisible = HasActiveObject("P1State_StaffDiscontent");
            bool refreshShortcutPresent = FindActiveButtons("P1Action_StaffDiscontentRefresh", exact: true).Count > 0;
            StaffDiscontentRecord record = null;
            discontent?.State.TryGetRecord(rebel, out record);
            bool isolateClicked = ClickExact("P1Action_StaffRebellionIsolate");
            discontent?.State.TryGetRecord(rebel, out record);
            AddResult(lines, "P1-08 STAFF_DISCONTENT_REBELLION",
                rebelSelected
                && discontentStateVisible
                && !refreshShortcutPresent
                && record?.IsInLocalRebellion == true
                && isolateClicked
                && record.IsIsolated,
                $"selected={rebelSelected}; stateVisible={discontentStateVisible}; refreshShortcut={refreshShortcutPresent}; stage={record?.Stage}; rebellion={record?.IsInLocalRebellion}; isolateClicked={isolateClicked}; isolated={record?.IsIsolated}");

            guardSelected = ClickByCardText("P1Action_StaffSelect_", guard.name);
            bool priorityClicked = priorityFixture != null
                && ClickByCardText("P1Action_OwnerPriority_", GetBuildingName(priorityFixture));
            if (guardWork.PriorityWorkTarget == null)
            {
                priorityClicked = ClickSequentialUntil(
                    "P1Action_OwnerPriority_",
                    () => guardWork.PriorityWorkTarget != null,
                    CountButtons("P1Action_OwnerPriority_"));
            }
            BuildableObject priorityTargetAfterClick = guardWork.PriorityWorkTarget;
            bool suppressClicked = ClickExact("P1Action_OwnerSuppress");
            AddResult(lines, "P1-09 OWNER_PRIORITY_SUPPRESS",
                resumedDuty && guardSelected && priorityClicked && priorityTargetAfterClick != null && suppressClicked && guardWork.PrioritySuppressActor == rebel,
                $"resumedDuty={resumedDuty}; guardSelected={guardSelected}; reachable={reachableBuildings.Count}; fixture={GetBuildingName(priorityFixture)}; priorityClicked={priorityClicked}; workTarget={priorityTargetAfterClick?.name ?? "<none>"}; suppressClicked={suppressClicked}; suppressTarget={guardWork.PrioritySuppressActor?.name ?? "<none>"}");

            bool profileClicked = ClickExact("P1Action_StaffProfile");
            AddResult(lines, "P1-13 CHARACTER_PROFILE", profileClicked && guard.Identity?.Profile != null,
                $"clicked={profileClicked}; name={guard.Identity?.DisplayName}; species={guard.Identity?.SpeciesTag}; traits={guard.Identity?.Profile?.Traits.Count ?? -1}");

            bool personaStateVisible = HasActiveObject("P1State_StaffPersona");
            bool moodStateVisible = HasActiveObject("P1State_StaffMood");
            bool aiRequestShortcutPresent = FindActiveButtons("P1Action_StaffPersona", exact: true).Count > 0
                || FindActiveButtons("P1Action_StaffMood", exact: true).Count > 0;
            AddResult(lines, "P1-14 AI_LLM_PERSONA_MOOD",
                personaStateVisible && moodStateVisible && !aiRequestShortcutPresent,
                $"personaState={personaStateVisible}; moodState={moodStateVisible}; publicRequestShortcut={aiRequestShortcutPresent}; generatedPersona={guard.PersonaRuntime?.HasGeneratedPersona}");
            ScrollActiveStaffPanel(0f);
        }

        private void VerifyShopDetail(UITabManager manager, List<string> lines)
        {
            OpenTab(manager, 3);
            List<Shop> visibleShops = FindActiveSceneComponents<Shop>()
                .Where((shop) => shop != null && !shop.isDestroy && shop.BuildingData != null)
                .OrderBy(GetBuildingName)
                .Take(8)
                .ToList();
            int productShopIndex = visibleShops.FindIndex((shop) => shop.ProductSnapshots.Count > 0);
            bool selected = productShopIndex >= 0
                && ClickExact($"P2Action_ShopSelect_{productShopIndex}");
            int products = CountButtons("P2Action_ShopProduct_");
            bool productClicked = ClickFirst("P2Action_ShopProduct_");
            AddResult(lines, "P2-02 SHOP_PRODUCTS_CHECKOUT",
                visibleShops.Count > 0 && productShopIndex >= 0 && selected && products > 0 && productClicked,
                $"shops={visibleShops.Count}; productShopIndex={productShopIndex}; selected={selected}; products={products}; productClicked={productClicked}");
            ScrollActivePanel(0f);
        }

        private void VerifyCodex(UITabManager manager, List<string> lines)
        {
            CodexRuntime codex = FindActiveSceneComponent<CodexRuntime>();
            OpenTab(manager, 9);
            bool categoryClicked = ClickExact("P2Action_CodexCategory_2");
            bool entryClicked = ClickFirst("P2Action_CodexEntry_");
            bool selected = FindActiveButtons("P2Action_CodexEntry_")
                .Any((button) => GetButtonText(button).Contains("선택됨"));
            AddResult(lines, "P2-01 CODEX_RECORDS", categoryClicked && entryClicked && selected && codex?.State.Entries.Count > 0,
                $"categoryClicked={categoryClicked}; entryClicked={entryClicked}; selected={selected}; entries={codex?.State.Entries.Count ?? -1}");
        }

        private void VerifyArchiveAndEvents(UITabManager manager, List<string> lines)
        {
            EventAlertRuntime events = FindActiveSceneComponent<EventAlertRuntime>();
            bool reportsClicked = ClickExact("P2Action_ArchiveReports");
            bool reportDetailClicked = ClickFirst("P2Action_ArchiveInvasionReport")
                || ClickFirst("P2Action_ArchiveExpeditionReport")
                || ClickFirst("P2Action_ArchiveOperationReport");
            OpenTab(manager, 9);
            bool eventsClicked = ClickExact("P2Action_ArchiveEvents");
            bool filterClicked = ClickExact("P2Action_EventFilter_1");
            bool recordClicked = ClickFirst("P2Action_EventRecord_");
            AddResult(lines, "P2-04 ALERT_EVENT_HISTORY",
                reportsClicked && reportDetailClicked && eventsClicked && filterClicked && recordClicked && events?.SelectedRecord != null,
                $"reportsClicked={reportsClicked}; reportDetailClicked={reportDetailClicked}; eventsClicked={eventsClicked}; filterClicked={filterClicked}; recordClicked={recordClicked}; selected={events?.SelectedRecord?.Title ?? "<none>"}; log={events?.EventLog.Count ?? -1}");
            ScrollActivePanel(0f);
        }

        private CharacterActor CreateQaCharacter(string name, CharacterType type, bool addWork)
        {
            CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
            tempObjects.Add(data);
            data.id = 995000 + tempObjects.Count;
            data.characterName = name;
            data.characterType = type;
            data.role = CharacterRole.Regular;
            data.speciesTag = "QA";
            data.baseStats = CharacterStatBlock.CreateDefault(100);
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

            GameObject obj = new GameObject(name);
            tempObjects.Add(obj);
            obj.AddComponent<SpriteRenderer>();
            CharacterActor actor = obj.AddComponent<CharacterActor>();
            obj.AddComponent<AbilityMove>();
            obj.AddComponent<AbilityShopping>();
            if (addWork)
            {
                obj.AddComponent<AbilityWork>();
            }

            AIBrain brain = obj.AddComponent<AIBrain>();
            brain.availableActions = AiDebugScenarioActionFactory.CreateStaffActions();
            InjectGameObjectFromLifetimeScope(obj);
            actor.RefreshAbilityCache();
            actor.Initialization(data);
            actor.Identity.SetPersistentId($"p1p2-ui:{data.id}");
            actor.EnsureRuntimeState();
            InjectGameObjectFromLifetimeScope(obj);
            actor.RefreshAbilityCache();
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.stats[CharacterCondition.SLEEP] = 100f;
            actor.stats[CharacterCondition.MOOD] = 100f;
            return actor;
        }

        private static List<BuildableObject> MatchSynthesisMaterials(
            FacilitySynthesisRecipeSO recipe,
            IReadOnlyList<BuildableObject> facilities)
        {
            List<BuildableObject> result = new List<BuildableObject>();
            HashSet<BuildableObject> used = new HashSet<BuildableObject>();
            foreach (BuildingSO materialData in recipe.materialBuildings ?? Array.Empty<BuildingSO>())
            {
                BuildableObject match = facilities.FirstOrDefault((facility) =>
                    !used.Contains(facility)
                    && facility.BuildingData != null
                    && facility.BuildingData.id == materialData.id);
                if (match == null)
                {
                    return new List<BuildableObject>();
                }

                used.Add(match);
                result.Add(match);
            }

            return result;
        }

        private static bool TryFindValidSynthesisMaterials(
            FacilitySynthesisRuntime runtime,
            FacilitySynthesisRecipeSO recipe,
            IReadOnlyList<BuildableObject> facilities,
            out List<BuildableObject> materials,
            out string reason)
        {
            materials = new List<BuildableObject>();
            reason = "invalid recipe";
            if (runtime == null || recipe?.materialBuildings == null || recipe.materialBuildings.Length == 0)
            {
                return false;
            }

            List<List<BuildableObject>> candidates = recipe.materialBuildings
                .Select((required) => facilities
                    .Where((facility) => facility != null
                        && facility.BuildingData != null
                        && required != null
                        && facility.BuildingData.id == required.id)
                    .ToList())
                .ToList();
            if (candidates.Any((set) => set.Count == 0))
            {
                reason = "matching placed materials missing";
                return false;
            }

            MethodInfo validate = typeof(FacilitySynthesisRuntime).GetMethod(
                "Validate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (validate == null)
            {
                reason = "validation method missing";
                return false;
            }

            int attempts = 0;
            List<BuildableObject> current = new List<BuildableObject>();
            HashSet<BuildableObject> used = new HashSet<BuildableObject>();
            List<BuildableObject> foundMaterials = null;
            string lastReason = reason;
            bool Search(int requirementIndex)
            {
                if (attempts >= 4096)
                {
                    return false;
                }

                if (requirementIndex < candidates.Count)
                {
                    foreach (BuildableObject candidate in candidates[requirementIndex])
                    {
                        if (!used.Add(candidate))
                        {
                            continue;
                        }

                        current.Add(candidate);
                        if (Search(requirementIndex + 1))
                        {
                            return true;
                        }

                        current.RemoveAt(current.Count - 1);
                        used.Remove(candidate);
                    }

                    return false;
                }

                attempts++;
                for (int primaryIndex = 0; primaryIndex < current.Count; primaryIndex++)
                {
                    List<BuildableObject> ordered = new List<BuildableObject> { current[primaryIndex] };
                    ordered.AddRange(current.Where((_, index) => index != primaryIndex));
                    object[] args = { recipe, ordered, null };
                    bool valid = validate.Invoke(runtime, args) is bool value && value;
                    lastReason = args[2] as string ?? string.Empty;
                    if (!valid)
                    {
                        continue;
                    }

                    RoomLayoutCache roomCache = new RoomLayoutCache();
                    if (!roomCache.TryGetRoom(ordered[0], out RoomInstance room)
                        || !room.IsUsable
                        || room.IsSelfContained)
                    {
                        lastReason = "primary synthesis material is not in a formal usable room";
                        continue;
                    }

                    foundMaterials = ordered;
                    return true;
                }

                return false;
            }

            bool found = Search(0);
            materials = foundMaterials ?? new List<BuildableObject>();
            reason = lastReason;
            if (!found && string.IsNullOrWhiteSpace(reason))
            {
                reason = $"no valid combination after {attempts} attempts";
            }

            return found;
        }

        private bool TryCreateSynthesisRoomFixture(
            FacilitySynthesisRuntime runtime,
            out FacilitySynthesisRecipeSO recipe,
            out List<BuildableObject> materials,
            out string reason)
        {
            recipe = runtime?.VisibleRecipes.FirstOrDefault((candidate) => candidate?.materialBuildings != null
                && candidate.materialBuildings.Length >= 2);
            materials = new List<BuildableObject>();
            reason = string.Empty;
            if (recipe == null)
            {
                reason = "no visible synthesis recipe for room fixture";
                return false;
            }

            int materialCount = recipe.materialBuildings.Length;
            int gridWidth = Mathf.Max(28, 10 + (materialCount * 8));
            Grid grid = new Grid(gridWidth, 1, new Vector3(10000f, 10000f, 0f));
            GridBuildingFactory factory = new GridBuildingFactory((building) =>
            {
                if (building != null)
                {
                    InjectGameObjectFromLifetimeScope(building.gameObject);
                }
            });

            BuildingSO hallwayData = CreateQaBuildingData(
                "P1 UI Room Floor",
                gridWidth,
                GridLayer.Hallway,
                BuildingCategory.Movement,
                typeof(BuildableObject));
            BuildingSO doorData = CreateQaBuildingData(
                "문",
                1,
                GridLayer.Building,
                BuildingCategory.Movement,
                typeof(Door));
            BuildingSO wallData = CreateQaBuildingData(
                "P1 UI Room Wall",
                1,
                GridLayer.Building,
                BuildingCategory.Wall,
                typeof(BuildableObject));
            hallwayData.tiles = CreateQaTileVisual();
            doorData.tiles = CreateQaTileVisual();

            if (!TryPlaceQaBuilding(factory, grid, hallwayData, new Vector2Int(gridWidth / 2, 0), out _)
                || !TryPlaceQaBuilding(factory, grid, doorData, new Vector2Int(2, 0), out _)
                || !TryPlaceQaBuilding(factory, grid, wallData, new Vector2Int(gridWidth - 3, 0), out _))
            {
                reason = "failed to place formal room boundary fixture";
                return false;
            }

            for (int i = 0; i < materialCount; i++)
            {
                BuildingSO materialData = recipe.materialBuildings[i];
                Vector2Int position = new Vector2Int(6 + (i * 7), 0);
                if (!TryPlaceQaBuilding(factory, grid, materialData, position, out BuildableObject material))
                {
                    reason = $"failed to place synthesis material {i}";
                    return false;
                }

                materials.Add(material);
            }

            RoomLayoutCache roomCache = new RoomLayoutCache();
            if (!roomCache.TryGetRoom(materials[0], out RoomInstance room)
                || !room.IsUsable
                || room.IsSelfContained
                || room.Doors.Count == 0
                || room.Walls.Count == 0)
            {
                reason = "formal wall/door room was not detected";
                return false;
            }

            reason = "formal wall/door room fixture ready";
            return true;
        }

        private bool TryCreateEvolutionRoomFixture(
            FacilityEvolutionRuntime runtime,
            out BuildableObject facility,
            out string reason)
        {
            facility = null;
            reason = string.Empty;
            FacilityEvolutionRecipeSO recipe = runtime?.VisibleRecipes
                .FirstOrDefault((candidate) => candidate?.fromFacilities != null
                    && candidate.fromFacilities.Any((source) => source != null));
            BuildingSO sourceData = recipe?.fromFacilities?
                .FirstOrDefault((source) => source != null);
            if (sourceData == null)
            {
                reason = "no visible evolution source for room fixture";
                return false;
            }

            const int gridWidth = 24;
            Grid grid = new Grid(gridWidth, 1, new Vector3(10100f, 10100f, 0f));
            GridBuildingFactory factory = new GridBuildingFactory((building) =>
            {
                if (building != null)
                {
                    InjectGameObjectFromLifetimeScope(building.gameObject);
                }
            });

            BuildingSO hallwayData = CreateQaBuildingData(
                "P1 UI Evolution Room Floor",
                gridWidth,
                GridLayer.Hallway,
                BuildingCategory.Movement,
                typeof(BuildableObject));
            BuildingSO doorData = CreateQaBuildingData(
                "문",
                1,
                GridLayer.Building,
                BuildingCategory.Movement,
                typeof(Door));
            BuildingSO wallData = CreateQaBuildingData(
                "P1 UI Evolution Room Wall",
                1,
                GridLayer.Building,
                BuildingCategory.Wall,
                typeof(BuildableObject));
            hallwayData.tiles = CreateQaTileVisual();
            doorData.tiles = CreateQaTileVisual();

            if (!TryPlaceQaBuilding(factory, grid, hallwayData, new Vector2Int(gridWidth / 2, 0), out _)
                || !TryPlaceQaBuilding(factory, grid, doorData, new Vector2Int(2, 0), out _)
                || !TryPlaceQaBuilding(factory, grid, wallData, new Vector2Int(gridWidth - 3, 0), out _)
                || !TryPlaceQaBuilding(factory, grid, sourceData, new Vector2Int(gridWidth / 2, 0), out facility))
            {
                reason = "failed to place evolution room fixture";
                return false;
            }

            BuildingSO[] requiredFixtures = recipe.requiredUniqueFixtures?
                .Where(candidate => candidate != null)
                .ToArray()
                ?? Array.Empty<BuildingSO>();
            for (int i = 0; i < requiredFixtures.Length; i++)
            {
                Vector2Int fixturePosition = new Vector2Int(6 + (i * 4), 0);
                if (!TryPlaceQaBuilding(
                    factory,
                    grid,
                    requiredFixtures[i],
                    fixturePosition,
                    out _))
                {
                    reason = $"failed to place required evolution fixture {requiredFixtures[i].name}";
                    return false;
                }
            }

            RoomLayoutCache roomCache = new RoomLayoutCache();
            if (!roomCache.TryGetRoom(facility, out RoomInstance room)
                || !room.IsUsable
                || room.IsSelfContained)
            {
                reason = "evolution fixture is not a formal usable room";
                return false;
            }

            reason = $"formal source fixture for {recipe.name} with {requiredFixtures.Length} requirements";
            return true;
        }

        // Keep the off-screen QA room valid without introducing a visible sprite into captures.
        private static Dictionary<GridTexture.TilemapLayer, UnityEngine.Tilemaps.Tile> CreateQaTileVisual()
        {
            return new Dictionary<GridTexture.TilemapLayer, UnityEngine.Tilemaps.Tile>
            {
                { GridTexture.TilemapLayer.HALLWAY, null }
            };
        }

        private BuildingSO CreateQaBuildingData(
            string objectName,
            int width,
            GridLayer layer,
            BuildingCategory category,
            Type type)
        {
            BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
            data.id = 996000 + tempObjects.Count;
            data.objectName = objectName;
            data.width = Mathf.Max(1, width);
            data.height = 1;
            data.layer = layer;
            data.category = category;
            data.type = type;
            data.unlocked = true;
            tempObjects.Add(data);
            return data;
        }

        private bool TryPlaceQaBuilding(
            GridBuildingFactory factory,
            Grid grid,
            BuildingSO data,
            Vector2Int position,
            out BuildableObject building)
        {
            building = factory?.Create(grid, data, position);
            if (building == null)
            {
                return false;
            }

            tempObjects.Add(building.gameObject);
            building.SetGrid(grid);
            building.Initialization(data, position);
            InjectGameObjectFromLifetimeScope(building.gameObject);
            bool registered = grid.RegisterOccupant(
                building,
                data.Placement.Layer,
                data.GetGridPosList(position),
                data.Placement.IsMovement);
            if (!registered)
            {
                building.DestroySelf();
                building = null;
            }

            return registered;
        }

        private bool ApplyEvolutionQaContribution(
            BuildableObject facility,
            FacilityEvolutionRecipeSO recipe)
        {
            if (facility?.BuildingData == null || facility.Grid == null || recipe == null)
            {
                return false;
            }

            MethodInfo scoreMethod = typeof(FullGameManualQaRuntimeProbe).GetMethod(
                "BuildQaEvolutionScores",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo tagMethod = typeof(FullGameManualQaRuntimeProbe).GetMethod(
                "BuildQaEvolutionTags",
                BindingFlags.Static | BindingFlags.NonPublic);
            FacilityEvolutionValue[] qaScores = scoreMethod?.Invoke(null, new object[] { recipe })
                as FacilityEvolutionValue[];
            string[] qaTags = tagMethod?.Invoke(null, new object[] { recipe }) as string[];
            if (qaScores == null)
            {
                return false;
            }

            FacilityEvolutionValue[] qaMetrics = BuildQaEvolutionSourceMetrics(facility, recipe);
            qaScores = qaScores
                .Concat(BuildQaEvolutionDerivedScores(recipe, qaMetrics))
                .ToArray();

            RoomLayoutCache roomCache = new RoomLayoutCache();
            if (!roomCache.TryGetRoom(facility, out RoomInstance room) || room == null)
            {
                return false;
            }

            Vector2Int? position = room.Cells
                .OrderBy(cell => cell.x)
                .ThenBy(cell => cell.y)
                .Where(cell => facility.Grid.GetGridCell(cell)?.CanOccupy(GridLayer.FloorOverlay) == true)
                .Select(cell => (Vector2Int?)cell)
                .FirstOrDefault();
            if (!position.HasValue)
            {
                return false;
            }

            BuildingSO contributionData = CreateQaBuildingData(
                "P1 UI Evolution Contribution",
                1,
                GridLayer.FloorOverlay,
                BuildingCategory.None,
                typeof(BuildableObject));
            contributionData.Evolution = new FacilityEvolutionContributionData
            {
                contributesToRoomProfile = true,
                tags = (qaTags ?? Array.Empty<string>())
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Distinct()
                    .ToArray(),
                scores = qaScores,
                metrics = qaMetrics
            };

            GameObject contributionObject = new GameObject("QA Evolution Contribution");
            BuildableObject contribution = contributionObject.AddComponent<BuildableObject>();
            contribution.SetGrid(facility.Grid);
            contribution.Initialization(contributionData, position.Value);
            InjectGameObjectFromLifetimeScope(contributionObject);
            bool registered = facility.Grid.RegisterOccupant(
                contribution,
                contributionData.Placement.Layer,
                contributionData.GetGridPosList(position.Value),
                contributionData.Placement.IsMovement);
            if (!registered)
            {
                Destroy(contributionObject);
                tempObjects.Remove(contributionData);
                Destroy(contributionData);
                return false;
            }

            contributionObject.transform.position = facility.Grid.GetWorldPos(position.Value);
            tempObjects.Add(contributionObject);
            return true;
        }

        private static FacilityEvolutionValue[] BuildQaEvolutionSourceMetrics(
            BuildableObject facility,
            FacilityEvolutionRecipeSO recipe)
        {
            FacilityEvolutionMetricRequirement[] requirements =
                recipe?.requiredRoomMetrics ?? Array.Empty<FacilityEvolutionMetricRequirement>();
            float roomArea = 1f;
            RoomLayoutCache roomCache = new RoomLayoutCache();
            if (facility != null
                && roomCache.TryGetRoom(facility, out RoomInstance room)
                && room != null)
            {
                roomArea = Mathf.Max(1f, room.Cells.Count);
            }

            Dictionary<string, float> sourceMetrics = requirements
                .Where((requirement) => !string.IsNullOrWhiteSpace(requirement.key)
                    && !IsDerivedEvolutionMetric(requirement.key))
                .GroupBy((requirement) => requirement.key)
                .ToDictionary(
                    group => group.Key,
                    ResolveQaRequirementValue);

            ApplyQaDensitySource(
                requirements,
                FacilityEvolutionTerms.SeatDensity,
                FacilityEvolutionTerms.SeatCount,
                roomArea,
                sourceMetrics);
            ApplyQaDensitySource(
                requirements,
                FacilityEvolutionTerms.TableDensity,
                FacilityEvolutionTerms.TableCount,
                roomArea,
                sourceMetrics);

            if (requirements.Any(requirement =>
                    requirement.key == FacilityEvolutionTerms.LuxuryPerSeat
                    || requirement.key == FacilityEvolutionTerms.ServiceScorePerSeat
                    || requirement.key == FacilityEvolutionTerms.CookingScorePerSeat))
            {
                sourceMetrics[FacilityEvolutionTerms.SeatCount] = Mathf.Max(
                    1f,
                    sourceMetrics.TryGetValue(FacilityEvolutionTerms.SeatCount, out float seats)
                        ? seats
                        : 0f);
            }

            return sourceMetrics
                .Select(pair => new FacilityEvolutionValue(pair.Key, pair.Value))
                .ToArray();
        }

        private static IEnumerable<FacilityEvolutionValue> BuildQaEvolutionDerivedScores(
            FacilityEvolutionRecipeSO recipe,
            IReadOnlyList<FacilityEvolutionValue> sourceMetrics)
        {
            FacilityEvolutionMetricRequirement[] requirements =
                recipe?.requiredRoomMetrics ?? Array.Empty<FacilityEvolutionMetricRequirement>();
            float seatCount = Mathf.Max(
                1f,
                sourceMetrics?.FirstOrDefault(metric =>
                    metric.key == FacilityEvolutionTerms.SeatCount).value ?? 0f);

            foreach ((string metric, string score) in new[]
                     {
                         (FacilityEvolutionTerms.LuxuryPerSeat, FacilityEvolutionTerms.Luxury),
                         (FacilityEvolutionTerms.ServiceScorePerSeat, FacilityEvolutionTerms.Service),
                         (FacilityEvolutionTerms.CookingScorePerSeat, FacilityEvolutionTerms.Cooking)
                     })
            {
                FacilityEvolutionMetricRequirement[] matching = requirements
                    .Where(requirement => requirement.key == metric && requirement.requireMin)
                    .ToArray();
                if (matching.Length > 0)
                {
                    float minimum = matching.Max(requirement => requirement.minValue);
                    yield return new FacilityEvolutionValue(score, (minimum * seatCount) + 1f);
                }
            }
        }

        private static void ApplyQaDensitySource(
            IReadOnlyList<FacilityEvolutionMetricRequirement> requirements,
            string densityMetric,
            string sourceMetric,
            float area,
            IDictionary<string, float> sourceMetrics)
        {
            FacilityEvolutionMetricRequirement[] matching = requirements
                .Where(requirement => requirement.key == densityMetric)
                .ToArray();
            if (matching.Length == 0)
            {
                return;
            }

            float density = ResolveQaRequirementValue(matching);
            sourceMetrics[sourceMetric] = Mathf.Max(0f, density * Mathf.Max(1f, area));
        }

        private static float ResolveQaRequirementValue(
            IEnumerable<FacilityEvolutionMetricRequirement> requirements)
        {
            FacilityEvolutionMetricRequirement[] values = requirements.ToArray();
            bool hasMinimum = values.Any(requirement => requirement.requireMin);
            bool hasMaximum = values.Any(requirement => requirement.requireMax);
            float minimum = hasMinimum
                ? values.Where(requirement => requirement.requireMin)
                    .Max(requirement => requirement.minValue)
                : 0f;
            float maximum = hasMaximum
                ? values.Where(requirement => requirement.requireMax)
                    .Min(requirement => requirement.maxValue)
                : float.PositiveInfinity;
            float value = hasMinimum ? minimum : hasMaximum ? maximum : 0f;
            return hasMaximum ? Mathf.Min(value, maximum) : value;
        }

        private static bool IsDerivedEvolutionMetric(string key)
        {
            return FacilityEvolutionMetricOwnership.IsDerivedRoomMetric(key);
        }

        private void CloseOffensePanels()
        {
            foreach (OffenseWorldMapPanel panel in UnityEngine.Object.FindObjectsByType<OffenseWorldMapPanel>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                if (panel != null)
                {
                    panel.gameObject.SetActive(false);
                    Destroy(panel.gameObject);
                }
            }

            foreach (OffenseExpeditionPanel panel in UnityEngine.Object.FindObjectsByType<OffenseExpeditionPanel>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                if (panel != null)
                {
                    panel.gameObject.SetActive(false);
                    Destroy(panel.gameObject);
                }
            }
        }

        private static void AddResult(List<string> lines, string row, bool pass, string evidence)
        {
            lines.Add($"{row} PASS={pass}; {evidence}");
        }

        private static void RunStep(string name, List<string> lines, Action action)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                lines.Add($"{name}-EXCEPTION={ex.GetType().Name}: {ex.Message}");
                lines.Add(ex.StackTrace ?? string.Empty);
            }
        }

        private static IEnumerator Capture(string path, List<string> lines)
        {
            ForceLayout();
            yield return null;
            yield return new WaitForEndOfFrame();
            LogLargeUiVisuals(path, lines);
            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, screenshot.EncodeToPNG());
            UnityEngine.Object.Destroy(screenshot);
            lines.Add($"tabCapture={path}");
        }

        private static void LogLargeUiVisuals(string capturePath, List<string> lines)
        {
            IEnumerable<string> imageDetails = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Image>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where((image) => IsLargeVisibleGraphic(image.rectTransform, image.color.a))
                .Select((image) =>
                {
                    Rect rect = image.rectTransform.rect;
                    string spriteName = image.sprite != null ? image.sprite.name : "none";
                    return $"Image:{GetHierarchyPath(image.transform)} " +
                           $"{Mathf.Abs(rect.width):0}x{Mathf.Abs(rect.height):0} " +
                           $"rgba={ColorUtility.ToHtmlStringRGBA(image.color)} sprite={spriteName}";
                });

            IEnumerable<string> rawImageDetails = UnityEngine.Object.FindObjectsByType<RawImage>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where((image) => IsLargeVisibleGraphic(image.rectTransform, image.color.a))
                .Select((image) =>
                {
                    Rect rect = image.rectTransform.rect;
                    string textureName = image.texture != null ? image.texture.name : "none";
                    return $"RawImage:{GetHierarchyPath(image.transform)} " +
                           $"{Mathf.Abs(rect.width):0}x{Mathf.Abs(rect.height):0} " +
                           $"rgba={ColorUtility.ToHtmlStringRGBA(image.color)} texture={textureName}";
                });

            string details = string.Join(" | ", imageDetails.Concat(rawImageDetails));
            lines.Add($"captureVisuals[{capturePath}]={details}");
        }

        private static bool IsLargeVisibleGraphic(RectTransform rect, float alpha)
        {
            return rect != null &&
                   rect.gameObject.activeInHierarchy &&
                   alpha > 0.01f &&
                   Mathf.Abs(rect.rect.width) >= 500f &&
                   Mathf.Abs(rect.rect.height) >= 200f;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            List<string> parts = new List<string>();
            while (transform != null)
            {
                parts.Add(transform.name);
                transform = transform.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        private static void ForceLayout()
        {
            Canvas.ForceUpdateCanvases();
            foreach (RectTransform rect in UnityEngine.Object.FindObjectsByType<RectTransform>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None).Where((rect) => rect != null && rect.gameObject.activeInHierarchy))
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }

            Canvas.ForceUpdateCanvases();
        }

        private static void OpenTab(UITabManager manager, int id)
        {
            if (IsTabActive(id))
            {
                manager.ToggleSelectButton(id);
            }

            manager.ToggleSelectButton(id);
            ForceLayout();
        }

        private static bool ClickSequentialUntil(string prefix, Func<bool> condition, int max)
        {
            for (int i = 0; i < max; i++)
            {
                if (ClickExact(prefix + i) && condition())
                {
                    return true;
                }
            }

            return condition();
        }

        private static bool ClickFirst(string prefix)
        {
            return Click(FindActiveButtons(prefix).FirstOrDefault());
        }

        private static bool ClickFirstOffenseExpeditionMember()
        {
            GameObject memberRoot = FindSceneObject("OffenseExpeditionMembers");
            Button memberButton = memberRoot != null
                ? memberRoot.GetComponentsInChildren<Button>(false)
                    .FirstOrDefault(button => button != null
                        && button.gameObject.activeInHierarchy
                        && button.IsInteractable()
                        && button.name != "Button_원정 출발"
                        && button.name != "Button_닫기")
                : null;
            return Click(memberButton);
        }

        private static bool ClickExact(string name)
        {
            return Click(FindActiveButtons(name, exact: true).FirstOrDefault());
        }

        private static bool ClickByCardText(string prefix, string text)
        {
            Button button = FindActiveButtons(prefix)
                .FirstOrDefault((candidate) => CardTextContains(candidate, text));
            return Click(button);
        }

        private static bool ClickButtonContaining(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            Button button = UnityEngine.Object.FindObjectsByType<Button>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where(candidate => candidate != null
                    && candidate.gameObject.activeInHierarchy
                    && candidate.IsInteractable())
                .FirstOrDefault(candidate => candidate.GetComponentsInChildren<TMP_Text>(true)
                    .Any(label => label != null
                        && label.text.Contains(text, StringComparison.Ordinal)));
            return Click(button);
        }

        private static bool CardTextContains(Button button, string text)
        {
            return button != null
                && button.transform.parent != null
                && button.transform.parent.GetComponentsInChildren<TMP_Text>(true)
                    .Any((label) => label != null && label.text.Contains(text));
        }

        private static Button FindButtonByLabel(string prefix, string label)
        {
            return FindActiveButtons(prefix)
                .FirstOrDefault((button) => button.GetComponentsInChildren<TMP_Text>(true)
                    .Any((text) => text != null && text.text == label));
        }

        private static IReadOnlyList<Button> FindActiveButtons(string value, bool exact = false)
        {
            return UnityEngine.Object.FindObjectsByType<Button>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where((button) => button != null
                    && button.gameObject.activeInHierarchy
                    && (exact
                        ? button.name == value
                        : button.name.StartsWith(value, StringComparison.Ordinal)))
                .ToArray();
        }

        private static bool Click(Button button)
        {
            if (button == null || !button.IsInteractable())
            {
                return false;
            }

            button.onClick.Invoke();
            ForceLayout();
            return true;
        }

        private static int CountButtons(string prefix)
        {
            return FindActiveButtons(prefix).Count;
        }

        private static bool HasActiveObject(string name)
        {
            return UnityEngine.Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Any((item) => item != null && item.gameObject.activeInHierarchy && item.name == name);
        }

        private static bool HasActiveObjectStartingWith(string prefix)
        {
            return UnityEngine.Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Any((item) => item != null
                    && item.gameObject.activeInHierarchy
                    && item.name.StartsWith(prefix, StringComparison.Ordinal));
        }

        private static GameObject FindSceneObject(string name)
        {
            return Resources.FindObjectsOfTypeAll<Transform>()
                .Where((candidate) => candidate != null && candidate.gameObject.scene.IsValid())
                .Select((candidate) => candidate.gameObject)
                .FirstOrDefault((candidate) => candidate.name == name);
        }

        private static bool ButtonTextContains(string name, string value)
        {
            Button button = FindActiveButtons(name, exact: true).FirstOrDefault();
            return button != null && GetButtonText(button).Contains(value);
        }

        private static string GetButtonText(Button button)
        {
            return button == null
                ? string.Empty
                : string.Join(" ", button.GetComponentsInChildren<TMP_Text>(true).Select((text) => text.text));
        }

        private static bool IsTabActive(int id)
        {
            return UnityEngine.Object.FindObjectsByType<UITab>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Any((tab) => tab != null && tab.id == id && tab.gameObject.activeInHierarchy);
        }

        private static int FacilitySignature()
        {
            unchecked
            {
                int hash = 17;
                foreach (BuildableObject building in UnityEngine.Object.FindObjectsByType<BuildableObject>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Where((building) => building != null && !building.isDestroy)
                    .OrderBy((building) => building.GetInstanceID()))
                {
                    hash = (hash * 31) + building.GetInstanceID();
                    hash = (hash * 31) + (building.BuildingData != null ? building.BuildingData.id : 0);
                }

                return hash;
            }
        }

        private static string GetBuildingName(BuildableObject building)
        {
            return building?.BuildingData != null && !string.IsNullOrWhiteSpace(building.BuildingData.objectName)
                ? building.BuildingData.objectName
                : building?.name ?? "시설";
        }

        private static string FirstText(params string[] values)
        {
            return values?.FirstOrDefault((value) => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static void ScrollActivePanel(float normalizedPosition)
        {
            P0FeatureSurfacePanel panel = UnityEngine.Object.FindObjectsByType<P0FeatureSurfacePanel>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault((candidate) => candidate != null && candidate.gameObject.activeInHierarchy);
            ScrollRect scroll = panel?.GetComponentInChildren<ScrollRect>(includeInactive: false);
            if (scroll == null)
            {
                return;
            }

            scroll.verticalNormalizedPosition = Mathf.Clamp01(normalizedPosition);
            ForceLayout();
        }

        private static void ScrollActiveStaffPanel(float normalizedPosition)
        {
            StaffWorkPriorityPanel panel = UnityEngine.Object.FindObjectsByType<StaffWorkPriorityPanel>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault((candidate) => candidate != null && candidate.gameObject.activeInHierarchy);
            ScrollRect scroll = panel?.GetComponentInChildren<ScrollRect>(includeInactive: false);
            if (scroll == null)
            {
                return;
            }

            scroll.verticalNormalizedPosition = Mathf.Clamp01(normalizedPosition);
            ForceLayout();
        }

        private IEnumerator EnsureGameplaySceneActive(List<string> lines)
        {
            Scene gameplayScene = SceneManager.GetSceneByPath(GameplayScenePath);
            if (!gameplayScene.IsValid() || !gameplayScene.isLoaded)
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(
                    DungeonSceneNavigator.GameplaySceneName,
                    LoadSceneMode.Single);
                while (load != null && !load.isDone)
                {
                    yield return null;
                }

                gameplayScene = SceneManager.GetSceneByPath(GameplayScenePath);
            }

            if (gameplayScene.IsValid() && gameplayScene.isLoaded)
            {
                if (SceneManager.sceneCount != 1)
                {
                    AsyncOperation reload = SceneManager.LoadSceneAsync(
                        DungeonSceneNavigator.GameplaySceneName,
                        LoadSceneMode.Single);
                    while (reload != null && !reload.isDone)
                    {
                        yield return null;
                    }

                    gameplayScene = SceneManager.GetSceneByPath(GameplayScenePath);
                }

                SceneManager.SetActiveScene(gameplayScene);
                lines.Add($"gameplaySceneActive=True; sceneCount={SceneManager.sceneCount}");
            }
            else
            {
                lines.Add("gameplaySceneActive=False");
            }
        }

        private void RunUiBounds(List<string> lines)
        {
            RectTransform[] rects = UnityEngine.Object.FindObjectsByType<RectTransform>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where((rect) => rect != null && rect.gameObject.activeInHierarchy)
                .ToArray();
            int invalid = 0;
            int oversized = 0;
            foreach (RectTransform rect in rects)
            {
                Rect worldRect = GetWorldRect(rect);
                if (float.IsNaN(worldRect.width) || float.IsNaN(worldRect.height))
                {
                    invalid++;
                }

                if (worldRect.width > Screen.width * 1.2f || worldRect.height > Screen.height * 1.2f)
                {
                    oversized++;
                }
            }

            int buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Count((button) => button != null && button.gameObject.activeInHierarchy);
            lines.Add($"UI activeRects={rects.Length}; invalid={invalid}; oversized={oversized}; activeButtons={buttons}");
        }

        private static Rect GetWorldRect(RectTransform rectTransform)
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            return Rect.MinMaxRect(
                corners.Min((corner) => corner.x),
                corners.Min((corner) => corner.y),
                corners.Max((corner) => corner.x),
                corners.Max((corner) => corner.y));
        }

        private T FindActiveSceneComponent<T>() where T : Component
        {
            IReadOnlyList<T> components = FindActiveSceneComponents<T>();
            return components.FirstOrDefault((component) =>
                       component != null && component.transform.root.name == "DungeonRuntimeSystems")
                   ?? components.FirstOrDefault();
        }

        private IReadOnlyList<T> FindActiveSceneComponents<T>() where T : Component
        {
            Scene activeScene = SceneManager.GetActiveScene();
            return UnityEngine.Object.FindObjectsByType<T>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where((component) => component != null && component.gameObject.scene == activeScene)
                .ToArray();
        }

        private void InjectGameObjectFromLifetimeScope(GameObject target)
        {
            LifetimeScope scope = FindActiveSceneLifetimeScope();
            if (scope?.Container == null || target == null)
            {
                return;
            }

            foreach (MonoBehaviour component in target.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component != null)
                {
                    scope.Container.Inject(component);
                }
            }
        }

        private static LifetimeScope FindActiveSceneLifetimeScope()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            LifetimeScope[] scopes = UnityEngine.Object.FindObjectsByType<LifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            return scopes.FirstOrDefault((scope) => scope != null && scope.Container != null && scope.gameObject.scene == activeScene)
                ?? scopes.FirstOrDefault((scope) => scope != null && scope.Container != null);
        }

        private static T ResolveFromLifetimeScope<T>() where T : class
        {
            LifetimeScope scope = FindActiveSceneLifetimeScope();
            if (scope?.Container == null)
            {
                return null;
            }

            try
            {
                return scope.Container.Resolve<T>();
            }
            catch
            {
                return null;
            }
        }

        private void ConfigureInput()
        {
            originalInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            originalMouse = Mouse.current;
            if (originalMouse != null)
            {
                InputSystem.DisableDevice(originalMouse);
            }

            verificationMouse = InputSystem.AddDevice<Mouse>("P1P2FeatureSurfaceVerificationMouse");
            verificationMouse.MakeCurrent();
            inputConfigured = true;
        }

        private void TeardownInput()
        {
            if (!inputConfigured)
            {
                return;
            }

            if (verificationMouse != null && verificationMouse.added)
            {
                InputSystem.RemoveDevice(verificationMouse);
            }

            if (originalMouse != null && originalMouse.added)
            {
                InputSystem.EnableDevice(originalMouse);
                originalMouse.MakeCurrent();
            }

            InputSystem.settings.editorInputBehaviorInPlayMode = originalInputBehavior;
            inputConfigured = false;
        }

        private void StartLogCapture()
        {
            capturedErrors.Clear();
            capturedWarnings.Clear();
            Application.logMessageReceived += OnLogMessageReceived;
            capturingLogs = true;
        }

        private void StopLogCapture()
        {
            if (!capturingLogs)
            {
                return;
            }

            Application.logMessageReceived -= OnLogMessageReceived;
            capturingLogs = false;
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning)
            {
                capturedWarnings.Add(condition);
            }
            else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                capturedErrors.Add(string.IsNullOrWhiteSpace(stackTrace) ? condition : $"{condition}\n{stackTrace}");
            }
        }

        private void Finish(List<string> lines)
        {
            StopLogCapture();
            int passCount = lines.Count((line) => line.Contains(" PASS=True;"));
            int failCount = lines.Count((line) => line.Contains(" PASS=False;"));
            lines.Add($"RESULT rowsPassed={passCount}/18; rowsFailed={failCount}");
            lines.Add($"capturedErrors={capturedErrors.Count}; errors={CompactList(capturedErrors)}");
            lines.Add($"capturedWarnings={capturedWarnings.Count}; warnings={CompactList(capturedWarnings)}");
            File.WriteAllText(ReportPath, string.Join("\n", lines));
            if (File.Exists(RequestPath))
            {
                File.Delete(RequestPath);
            }

            EditorApplication.ExitPlaymode();
        }

        private void DestroyTempObjects()
        {
            BuildableObject[] generatedBuildables = FindActiveSceneComponents<BuildableObject>()
                .Where((building) => building != null
                    && !baselineBuildableIds.Contains(building.GetInstanceID()))
                .ToArray();
            HashSet<int> destroyedIds = new HashSet<int>();
            foreach (BuildableObject building in generatedBuildables.Reverse())
            {
                if (building == null || !destroyedIds.Add(building.gameObject.GetInstanceID()))
                {
                    continue;
                }

                BuildingSO data = building.BuildingData;
                Grid grid = building.Grid;
                if (grid != null
                    && data != null
                    && building.buildPoses != null
                    && building.buildPoses.Count > 0)
                {
                    grid.RemoveOccupant(
                        building,
                        data.Placement.Layer,
                        building.buildPoses,
                        data.Placement.IsMovement);
                }

                building.gameObject.SetActive(false);
                DestroyImmediate(building.gameObject);
            }

            foreach (UnityEngine.Object obj in tempObjects.Where((item) => item != null).Reverse())
            {
                if (obj == null)
                {
                    continue;
                }

                UnityEngine.Object target = obj is Component component ? component.gameObject : obj;
                if (target == null || !destroyedIds.Add(target.GetInstanceID()))
                {
                    continue;
                }

                if (target is GameObject gameObject)
                {
                    gameObject.SetActive(false);
                }

                DestroyImmediate(target);
            }

            tempObjects.Clear();
            baselineBuildableIds.Clear();
        }

        private static string CompactList(IReadOnlyList<string> values)
        {
            return values == null || values.Count == 0
                ? "<none>"
                : string.Join(" || ", values.Select((value) => CompactText(value, 180)));
        }

        private static string CompactText(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "<none>";
            }

            string singleLine = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return singleLine.Length <= maxLength ? singleLine : singleLine.Substring(0, maxLength) + "...";
        }

        private static void ClearConsole()
        {
            Type logEntries = Type.GetType("UnityEditor.LogEntries,UnityEditor.dll");
            MethodInfo clear = logEntries?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
            clear?.Invoke(null, null);
        }
    }
}
