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
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

[InitializeOnLoad]
public static class P0FeatureSurfacePlayModeVerifier
{
    public const string RequestPath = "Temp/p0-ui-surface-verification.request";
    public const string ReportPath = "Temp/p0-ui-surface-verification-report.txt";
    public const string ScreenshotPath = "Temp/p0-ui-surface-verification.png";

    private const string OriginalActiveSceneKey = "DungeonStory.P0UiVerifier.OriginalActiveScene";
    private const string SceneRootStatesKey = "DungeonStory.P0UiVerifier.SceneRootStates";
    private const string OpenedSampleSceneKey = "DungeonStory.P0UiVerifier.OpenedSampleScene";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    private static bool runnerCreated;
    private static bool preparingScenes;

    [Serializable]
    private sealed class SceneRootState
    {
        public string scenePath;
        public string rootName;
        public int rootIndex;
        public bool activeSelf;
    }

    [Serializable]
    private sealed class SceneRootStateCollection
    {
        public List<SceneRootState> entries = new List<SceneRootState>();
    }

    static P0FeatureSurfacePlayModeVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
    }

    [MenuItem("DungeonStory/Debug/QA/Request P0 UI Surface Verification")]
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
        GameObject runnerObject = new GameObject("P0 UI Surface Verification Runner");
        UnityEngine.Object.DontDestroyOnLoad(runnerObject);
        runnerObject.AddComponent<P0FeatureSurfaceVerificationRunner>();
    }

    private static void PrepareScenesForPlayMode()
    {
        Scene originalActiveScene = SceneManager.GetActiveScene();
        SessionState.SetString(OriginalActiveSceneKey, originalActiveScene.path ?? string.Empty);

        SceneRootStateCollection stateCollection = new SceneRootStateCollection();
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded || scene.path == SampleScenePath)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject root = roots[rootIndex];
                stateCollection.entries.Add(new SceneRootState
                {
                    scenePath = scene.path,
                    rootName = root.name,
                    rootIndex = rootIndex,
                    activeSelf = root.activeSelf
                });

                if (root.activeSelf)
                {
                    root.SetActive(false);
                }
            }
        }

        SessionState.SetString(SceneRootStatesKey, JsonUtility.ToJson(stateCollection));

        Scene sampleScene = SceneManager.GetSceneByPath(SampleScenePath);
        bool openedSampleScene = !sampleScene.IsValid() || !sampleScene.isLoaded;
        if (openedSampleScene)
        {
            sampleScene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Additive);
        }

        SessionState.SetBool(OpenedSampleSceneKey, openedSampleScene);
        if (!sampleScene.IsValid() || !sampleScene.isLoaded)
        {
            throw new InvalidOperationException("P0 UI verification could not load SampleScene.");
        }

        SceneManager.SetActiveScene(sampleScene);
    }

    private static void RestoreSceneState()
    {
        string originalScenePath = SessionState.GetString(OriginalActiveSceneKey, string.Empty);
        Scene originalScene = SceneManager.GetSceneByPath(originalScenePath);
        if (originalScene.IsValid() && originalScene.isLoaded)
        {
            SceneManager.SetActiveScene(originalScene);
        }

        if (SessionState.GetBool(OpenedSampleSceneKey, false))
        {
            Scene sampleScene = SceneManager.GetSceneByPath(SampleScenePath);
            if (sampleScene.IsValid() && sampleScene.isLoaded && sampleScene != originalScene)
            {
                EditorSceneManager.CloseScene(sampleScene, true);
            }
        }

        string json = SessionState.GetString(SceneRootStatesKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(json))
        {
            SceneRootStateCollection stateCollection = JsonUtility.FromJson<SceneRootStateCollection>(json);
            foreach (SceneRootState entry in stateCollection?.entries ?? new List<SceneRootState>())
            {
                Scene scene = SceneManager.GetSceneByPath(entry.scenePath);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                GameObject root = entry.rootIndex >= 0 && entry.rootIndex < roots.Length
                    ? roots[entry.rootIndex]
                    : roots.FirstOrDefault((candidate) => candidate != null && candidate.name == entry.rootName);
                if (root != null && root.activeSelf != entry.activeSelf)
                {
                    root.SetActive(entry.activeSelf);
                }
            }
        }

        SessionState.EraseString(OriginalActiveSceneKey);
        SessionState.EraseString(SceneRootStatesKey);
        SessionState.SetBool(OpenedSampleSceneKey, false);
    }

    private sealed class P0FeatureSurfaceVerificationRunner : MonoBehaviour
    {
        private readonly List<string> capturedErrors = new List<string>();
        private readonly List<string> capturedWarnings = new List<string>();
        private readonly List<UnityEngine.Object> tempObjects = new List<UnityEngine.Object>();
        private bool capturingLogs;
        private InputSettings.EditorInputBehaviorInPlayMode originalInputBehavior;
        private Mouse originalMouse;
        private Mouse verificationMouse;
        private bool inputConfigured;
        private string metaProfilePath;
        private byte[] metaProfileBackup;
        private bool metaProfileExisted;

        private IEnumerator Start()
        {
            yield return Run();
        }

        private IEnumerator Run()
        {
            List<string> lines = new List<string>();
            Directory.CreateDirectory("Temp");

            yield return EnsureSampleSceneActive(lines);
            yield return null;
            yield return null;

            ClearConsole();
            StartLogCapture();
            ConfigureInput();

            float originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 1f;
                UITabManager manager = null;
                RunStep("SETUP", lines, () =>
                {
                    Scene activeScene = SceneManager.GetActiveScene();
                    lines.Add($"activeScene={activeScene.path}");
                    manager = FindActiveSceneComponent<UITabManager>();
                    lines.Add($"tabManager={manager != null}");
                });
                if (manager == null)
                {
                    yield break;
                }

                PlayModeVerificationPersistenceSnapshot.CaptureCurrent("p0-ui-surface");
                BackupMetaProfile();
                yield return DismissStartupAndSelectOwner(lines);
                RunStep("PREPARE", lines, () => PrepareGameState(lines));
                yield return null;

                RunStep("SHOP", lines, () =>
                {
                    VerifyShop(manager, lines);
                    CaptureTab("Temp/p0-ui-shop.png", lines);
                });
                yield return null;

                RunStep("SHOP-BASIC-VISUAL", lines, () =>
                {
                    ScrollActiveP0Panel(0f);
                });
                yield return null;
                RunStep("SHOP-BASIC-CAPTURE", lines, () =>
                    CaptureTab("Temp/p0-ui-shop-basic.png", lines));
                yield return null;

                RunStep("WAREHOUSE", lines, () =>
                {
                    VerifyWarehouse(manager, lines);
                    CaptureTab("Temp/p0-ui-warehouse.png", lines);
                });
                yield return null;

                RunStep("WAREHOUSE-ACTIONS-VISUAL", lines, () =>
                {
                    ScrollActiveP0Panel(0.52f);
                });
                yield return null;
                RunStep("WAREHOUSE-ACTIONS-CAPTURE", lines, () =>
                    CaptureTab("Temp/p0-ui-warehouse-actions.png", lines));
                yield return null;

                yield return VerifyOperationRecruitmentMeta(manager, lines);
                yield return CaptureTabAtEndOfFrame("Temp/p0-ui-operation.png", lines);
                yield return null;

                RunStep("RECRUITMENT-VISUAL", lines, () =>
                {
                    ScrollActiveP0Panel(0.55f);
                });
                yield return null;
                RunStep("RECRUITMENT-CAPTURE", lines, () =>
                    CaptureTab("Temp/p0-ui-recruitment.png", lines));
                yield return null;

                RunStep("META-VISUAL", lines, () =>
                {
                    ScrollContainingButton(
                        FindActiveButton($"P0Action_MetaUpgrade_{MetaUpgradeIds.CommerceSupplyNetwork}"));
                });
                yield return null;
                RunStep("META-CAPTURE", lines, () =>
                    CaptureTab("Temp/p0-ui-meta.png", lines));
                yield return null;

                yield return ConfigureResearchPriorityThroughUi(manager, lines);

                RunStep("RESEARCH", lines, () =>
                {
                    VerifyResearch(manager, lines);
                });
                yield return VerifyNaturalResearchProgress(lines);
                yield return null;
                Canvas.ForceUpdateCanvases();
                yield return CaptureTabAtEndOfFrame("Temp/p0-ui-research.png", lines);
                yield return null;

                RunStep("RESEARCH-REWARDS-VISUAL", lines, () =>
                {
                    ScrollActiveP0Panel(0.55f);
                });
                yield return null;
                yield return CaptureTabAtEndOfFrame("Temp/p0-ui-research-rewards.png", lines);
                yield return null;

                yield return VerifyResearchConstructionUnlockThroughUi(lines);
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
                RestoreMetaProfile();
                TeardownInput();
                DestroyTempObjects();
                Finish(lines);
            }
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
                if (!string.IsNullOrWhiteSpace(ex.StackTrace))
                {
                    lines.Add(ex.StackTrace);
                }
            }
        }

        private static void CaptureTab(string path, List<string> lines)
        {
            ScreenCapture.CaptureScreenshot(path);
            lines.Add($"tabCapture={path}");
        }

        private static IEnumerator CaptureTabAtEndOfFrame(string path, List<string> lines)
        {
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return new WaitForEndOfFrame();

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            ScreenCapture.CaptureScreenshot(path);
            const int maxWaitFrames = 120;
            int waitedFrames = 0;
            while ((!File.Exists(path) || new FileInfo(path).Length == 0)
                && waitedFrames < maxWaitFrames)
            {
                waitedFrames++;
                yield return null;
            }

            yield return null;

            int pixelCount = 0;
            if (File.Exists(path))
            {
                Texture2D capture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (capture.LoadImage(File.ReadAllBytes(path), markNonReadable: false))
                {
                    pixelCount = capture.width * capture.height;
                }

                UnityEngine.Object.Destroy(capture);
            }

            lines.Add($"tabCapture={path}; pixels={pixelCount}; waitFrames={waitedFrames}");
        }

        private static void ScrollActiveP0Panel(float normalizedPosition)
        {
            P0FeatureSurfacePanel panel = UnityEngine.Object.FindObjectsByType<P0FeatureSurfacePanel>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault((candidate) => candidate != null && candidate.gameObject.activeInHierarchy);
            ScrollRect scroll = panel != null
                ? panel.GetComponentInChildren<ScrollRect>(includeInactive: false)
                : null;
            if (scroll == null)
            {
                throw new InvalidOperationException("Active P0 scroll view was not found.");
            }

            Canvas.ForceUpdateCanvases();
            if (scroll.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            }

            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = Mathf.Clamp01(normalizedPosition);
            Canvas.ForceUpdateCanvases();
        }

        private IEnumerator EnsureSampleSceneActive(List<string> lines)
        {
            const string sampleScenePath = "Assets/Scenes/SampleScene.unity";
            Scene sampleScene = SceneManager.GetSceneByPath(sampleScenePath);
            if (!sampleScene.IsValid() || !sampleScene.isLoaded)
            {
                AsyncOperation load = SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Additive);
                while (load != null && !load.isDone)
                {
                    yield return null;
                }

                sampleScene = SceneManager.GetSceneByPath(sampleScenePath);
                lines.Add($"loadedSampleScene={sampleScene.IsValid() && sampleScene.isLoaded}");
            }

            if (sampleScene.IsValid() && sampleScene.isLoaded)
            {
                SceneManager.SetActiveScene(sampleScene);
                lines.Add("sampleSceneActive=True");
            }
            else
            {
                lines.Add("sampleSceneActive=False");
            }
        }

        private void PrepareGameState(List<string> lines)
        {
            GameManager gameManager = FindActiveSceneComponent<GameManager>();
            GameData gameData = gameManager != null ? gameManager.gameData : null;
            if (gameData != null && gameData.holdingMoney != null)
            {
                int beforeMoney = gameData.holdingMoney.Value;
                gameData.holdingMoney.Value = Mathf.Max(beforeMoney, 50000);
                lines.Add($"preparedMoney={beforeMoney}->{gameData.holdingMoney.Value}");
            }

            MetaProgressionRuntime meta = FindActiveSceneComponent<MetaProgressionRuntime>();
            if (meta != null)
            {
                int beforeCurrency = meta.State.AvailableCurrency;
                meta.State.AddCurrency(600);
                lines.Add($"preparedMetaCurrency={beforeCurrency}->{meta.State.AvailableCurrency}");
            }

            PrepareBasicPurchaseOffer(lines);
            PrepareRecruitCandidate(lines);
            PrepareWarehouseRestockTarget(lines);
        }

        private void PrepareBasicPurchaseOffer(List<string> lines)
        {
            DailyFacilityShopRuntime runtime = FindActiveSceneComponent<DailyFacilityShopRuntime>();
            LifetimeScope scope = FindActiveSceneLifetimeScope();
            IFacilityShopCatalog catalog = scope != null && scope.Container != null
                ? scope.Container.Resolve(typeof(IFacilityShopCatalog)) as IFacilityShopCatalog
                : null;
            BuildingSO building = catalog?.Buildings
                .FirstOrDefault((candidate) => candidate != null && FacilityShopService.CanEnterBasicPurchase(candidate));
            bool unlocked = runtime != null && building != null && runtime.UnlockState.UnlockBasicPurchase(building);
            int offerCount = runtime != null ? runtime.CurrentBasicPurchaseOffers.Count : -1;
            lines.Add($"preparedBasicPurchase={unlocked || offerCount > 0}; building={(building != null ? building.name : "<none>")}; offers={offerCount}");
        }

        private void PrepareRecruitCandidate(List<string> lines)
        {
            RegularCustomerRuntime runtime = FindActiveSceneComponent<RegularCustomerRuntime>();
            if (runtime == null)
            {
                lines.Add("preparedRecruitCandidate=False; reason=no runtime");
                return;
            }

            CharacterActor customer = CreateQaCustomer("P0 UI Recruit Candidate");
            tempObjects.Add(customer.data);
            tempObjects.Add(customer.gameObject);
            BuildableObject facility = FindActiveSceneComponents<BuildableObject>()
                .FirstOrDefault((building) => building != null && !building.isDestroy);
            DungeonStory.Foundation.IGameEventBus gameEventBus =
                FindActiveSceneLifetimeScope()?.Container?.Resolve(
                    typeof(DungeonStory.Foundation.IGameEventBus))
                as DungeonStory.Foundation.IGameEventBus;

            for (int i = 0; i < 4; i++)
            {
                gameEventBus?.Publish(new FacilityVisitEvent(customer, facility));
            }

            bool hasRecord = runtime.State.TryGetRecord(
                RegularCustomerService.GetCustomerId(customer),
                out RegularCustomerRecord record);
            lines.Add($"preparedRecruitCandidate={hasRecord}; status={(record != null ? record.Status.ToString() : "<none>")}; visits={(record != null ? record.VisitCount : -1)}");
        }

        private CharacterActor CreateQaCustomer(string name)
        {
            CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
            data.id = 990000 + tempObjects.Count;
            data.characterName = name;
            data.characterType = CharacterType.Customer;
            data.role = CharacterRole.Regular;
            data.speciesTag = "QA";
            data.baseStats = CharacterStatBlock.CreateDefault(90);
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

            GameObject obj = new GameObject(name);
            obj.AddComponent<SpriteRenderer>();
            CharacterActor actor = obj.AddComponent<CharacterActor>();
            obj.AddComponent<AbilityMove>();
            obj.AddComponent<AbilityShopping>();
            AIBrain brain = obj.AddComponent<AIBrain>();
            brain.availableActions = AiDebugScenarioActionFactory.CreateCustomerActions();

            InjectGameObjectFromLifetimeScope(obj);
            actor.RefreshAbilityCache();
            actor.Initialization(data);
            actor.EnsureRuntimeState();
            InjectGameObjectFromLifetimeScope(obj);
            actor.RefreshAbilityCache();
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.stats[CharacterCondition.MOOD] = 100f;
            obj.SetActive(false);
            return actor;
        }

        private void PrepareWarehouseRestockTarget(List<string> lines)
        {
            IWarehouseFacility warehouse = FindActiveSceneComponents<MonoBehaviour>()
                .OfType<IWarehouseFacility>()
                .FirstOrDefault((candidate) => candidate != null && candidate.HasWarehouseInventory && candidate.Inventory != null);
            if (warehouse != null)
            {
                int beforeWarehouse = warehouse.Inventory.TotalStock;
                int removed = 0;
                foreach (StockCategoryDefinition definition in StockCategoryCatalog.All)
                {
                    StockCategory category = definition.Category;
                    removed += warehouse.Inventory.Withdraw(category, 10);
                    if (removed >= 30)
                    {
                        break;
                    }
                }

                lines.Add($"preparedDeliveryCapacity=True; warehouseStock={beforeWarehouse}->{warehouse.Inventory.TotalStock}; removed={removed}");
            }
            else
            {
                lines.Add("preparedDeliveryCapacity=False; reason=no warehouse");
            }

            Shop shop = FindActiveSceneComponents<Shop>()
                .FirstOrDefault((candidate) => candidate != null && !candidate.isDestroy && candidate.CurrentStock > 0);
            if (shop == null)
            {
                lines.Add("preparedRestockTarget=False; reason=no stocked shop");
                return;
            }

            int before = shop.CurrentStock;
            shop.DebugClearStock();
            lines.Add($"preparedRestockTarget=True; shop={shop.name}; stock={before}->{shop.CurrentStock}");
        }

        private void VerifyShop(UITabManager manager, List<string> lines)
        {
            DailyFacilityShopRuntime shopRuntime = FindActiveSceneComponent<DailyFacilityShopRuntime>();
            BlueprintResearchRuntime research = FindActiveSceneComponent<BlueprintResearchRuntime>();
            int beforeMoney = GetHoldingMoney();
            int beforeTasks = research != null ? research.State.Tasks.Count : -1;

            manager.ToggleSelectButton(3);
            Canvas.ForceUpdateCanvases();
            int dailyButtonsBefore = CountActiveButtons("P0Action_ShopDaily_");
            int basicButtonsBefore = CountActiveButtons("P0Action_ShopBasic_");
            int dailyClicked = ClickSequential("P0Action_ShopDaily_", 8);
            Canvas.ForceUpdateCanvases();
            int basicClicked = ClickSequential("P0Action_ShopBasic_", 8);
            int afterMoney = GetHoldingMoney();
            int afterTasks = research != null ? research.State.Tasks.Count : -1;

            lines.Add(
                $"SHOP visible={IsTabActive(3)}; runtime={shopRuntime != null}; dailyButtons={dailyButtonsBefore}; dailyClicked={dailyClicked}; basicButtons={basicButtonsBefore}; basicClicked={basicClicked}; money={beforeMoney}->{afterMoney}; researchTasks={beforeTasks}->{afterTasks}; stateChanged={afterMoney != beforeMoney || afterTasks != beforeTasks}");
        }

        private void VerifyWarehouse(UITabManager manager, List<string> lines)
        {
            List<IWarehouseFacility> warehouses = FindActiveSceneComponents<MonoBehaviour>()
                .OfType<IWarehouseFacility>()
                .Where((warehouse) => warehouse != null && warehouse.HasWarehouseInventory && warehouse.Inventory != null)
                .ToList();
            List<Shop> shops = FindActiveSceneComponents<Shop>()
                .Where((shop) => shop != null && !shop.isDestroy)
                .ToList();
            int beforeWarehouse = warehouses.Sum((warehouse) => warehouse.Inventory.TotalStock);
            int beforeShopStock = shops.Sum((shop) => shop.CurrentStock);
            int beforeMoney = GetHoldingMoney();

            manager.ToggleSelectButton(4);
            Canvas.ForceUpdateCanvases();
            bool restockClicked = ClickFirst("P0Action_WarehouseRestock_");
            Canvas.ForceUpdateCanvases();
            bool deliveryClicked = ClickFirst("P0Action_WarehouseDelivery_");

            int afterWarehouse = warehouses.Sum((warehouse) => warehouse.Inventory.TotalStock);
            int afterShopStock = shops.Sum((shop) => shop.CurrentStock);
            int afterMoney = GetHoldingMoney();
            lines.Add(
                $"WAREHOUSE visible={IsTabActive(4)}; warehouses={warehouses.Count}; shops={shops.Count}; restockClicked={restockClicked}; deliveryClicked={deliveryClicked}; warehouseStock={beforeWarehouse}->{afterWarehouse}; shopStock={beforeShopStock}->{afterShopStock}; money={beforeMoney}->{afterMoney}; stateChanged={beforeWarehouse != afterWarehouse || beforeShopStock != afterShopStock || beforeMoney != afterMoney}");
        }

        private IEnumerator VerifyOperationRecruitmentMeta(UITabManager manager, List<string> lines)
        {
            OperatingDaySettlementRuntime settlement = FindActiveSceneComponent<OperatingDaySettlementRuntime>();
            RegularCustomerRuntime regularCustomer = FindActiveSceneComponent<RegularCustomerRuntime>();
            MetaProgressionRuntime meta = FindActiveSceneComponent<MetaProgressionRuntime>();

            int beforeMoney = GetHoldingMoney();
            int beforeDebt = settlement != null ? settlement.OutstandingDebt : -1;
            bool beforeFundingUsed = settlement != null && settlement.EmergencyFundingUsed;
            int beforeRecruited = regularCustomer != null ? regularCustomer.State.RecruitedCharacters.Count : -1;
            int beforeCurrency = meta != null ? meta.State.AvailableCurrency : -1;
            int beforeLevels = meta != null ? meta.State.UpgradeLevels.Values.Sum() : -1;

            manager.ToggleSelectButton(5);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Button fundingButton = FindActiveButton("P0Action_OperationEmergencyFunding");
            bool fundingClickable = fundingButton != null && fundingButton.interactable;
            yield return ClickWithInput(fundingButton);
            bool fundingClicked = fundingClickable
                && settlement != null
                && !beforeFundingUsed
                && settlement.EmergencyFundingUsed;
            bool recruitClicked = ClickFirst("P0Action_Recruit_");
            string[] strategyUpgradeIds =
            {
                MetaUpgradeIds.CommerceSupplyNetwork,
                MetaUpgradeIds.FortressEngineering,
                MetaUpgradeIds.ArcaneResearchMethod
            };
            bool strategyCardsPresent = strategyUpgradeIds.All(id =>
                FindActiveButton($"P0Action_MetaUpgrade_{id}") != null);
            Button strategyMetaButton = FindActiveButton(
                $"P0Action_MetaUpgrade_{MetaUpgradeIds.CommerceSupplyNetwork}");
            if (strategyMetaButton != null)
            {
                ScrollContainingButton(strategyMetaButton);
                yield return null;
                strategyMetaButton = FindActiveButton(
                    $"P0Action_MetaUpgrade_{MetaUpgradeIds.CommerceSupplyNetwork}");
            }

            int beforeStrategyLevel = meta != null
                ? meta.State.GetUpgradeLevel(MetaUpgradeIds.CommerceSupplyNetwork)
                : -1;
            yield return ClickWithInput(strategyMetaButton);
            int afterStrategyLevel = meta != null
                ? meta.State.GetUpgradeLevel(MetaUpgradeIds.CommerceSupplyNetwork)
                : -1;
            bool metaClicked = afterStrategyLevel > beforeStrategyLevel;

            int afterMoney = GetHoldingMoney();
            int afterDebt = settlement != null ? settlement.OutstandingDebt : -1;
            bool afterFundingUsed = settlement != null && settlement.EmergencyFundingUsed;
            int afterRecruited = regularCustomer != null ? regularCustomer.State.RecruitedCharacters.Count : -1;
            int afterCurrency = meta != null ? meta.State.AvailableCurrency : -1;
            int afterLevels = meta != null ? meta.State.UpgradeLevels.Values.Sum() : -1;
            OperatingDayReport report = settlement != null ? settlement.LatestReport : null;

            lines.Add(
                $"OPERATION visible={IsTabActive(5)}; fundingClicked={fundingClicked}; fundingUsed={beforeFundingUsed}->{afterFundingUsed}; money={beforeMoney}->{afterMoney}; debt={beforeDebt}->{afterDebt}; report={(report != null)}; recruitClicked={recruitClicked}; recruited={beforeRecruited}->{afterRecruited}; strategyCards={strategyCardsPresent}; commerceMetaClicked={metaClicked}; commerceLevel={beforeStrategyLevel}->{afterStrategyLevel}; metaCurrency={beforeCurrency}->{afterCurrency}; metaLevels={beforeLevels}->{afterLevels}; stateChanged={beforeFundingUsed != afterFundingUsed || beforeMoney != afterMoney || beforeDebt != afterDebt || beforeRecruited != afterRecruited || beforeLevels != afterLevels}");
        }

        private IEnumerator DismissStartupAndSelectOwner(List<string> lines)
        {
            yield return null;
            GameObject modal = FindSceneObject("SaveModal");
            Button startNewButton = FindActiveButton("StartNewRunButton");
            bool startupWasVisible = modal != null && modal.activeInHierarchy;
            if (startupWasVisible)
            {
                yield return ClickWithInput(startNewButton);
                if (modal != null && modal.activeInHierarchy)
                {
                    yield return ClickWithInput(startNewButton);
                }
            }

            lines.Add($"startupModal={startupWasVisible}->{(modal != null && modal.activeInHierarchy)}; newGamePointer={startNewButton != null}");

            OwnerRunManager ownerManager = FindActiveSceneComponent<OwnerRunManager>();
            if (ownerManager != null && ownerManager.CurrentOwnerActor == null)
            {
                Button ownerButton = UnityEngine.Object.FindObjectsByType<Button>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate != null
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
            Vector2 point = RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center));
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

        private static void ScrollContainingButton(Button button)
        {
            ScrollRect scroll = button != null ? button.GetComponentInParent<ScrollRect>() : null;
            RectTransform target = button != null ? button.transform as RectTransform : null;
            if (scroll == null || scroll.content == null || scroll.viewport == null || target == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);

            Canvas.ForceUpdateCanvases();
            float overflow = Mathf.Max(0f, scroll.content.rect.height - scroll.viewport.rect.height);
            Bounds targetBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                scroll.viewport,
                target);
            if (overflow > 0.1f)
            {
                scroll.verticalNormalizedPosition = Mathf.Clamp01(
                    scroll.verticalNormalizedPosition + targetBounds.center.y / overflow);
            }

            Canvas.ForceUpdateCanvases();
        }

        private static Button FindActiveButton(string name)
        {
            return UnityEngine.Object.FindObjectsByType<Button>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null
                    && candidate.gameObject.activeInHierarchy
                    && candidate.name == name);
        }

        private static Button FindActiveTabButton(TabId tabId)
        {
            return UnityEngine.Object.FindObjectsByType<Button>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null
                    && candidate.gameObject.activeInHierarchy
                    && candidate.TryGetComponent(out UITabButtonBinding binding)
                    && binding.Id == tabId);
        }

        private static UIBuildingSelectButton FindBuildingSelectButton(
            GridConstructTab constructTab,
            int buildingId)
        {
            return constructTab != null
                ? constructTab.GetComponentsInChildren<UIBuildingSelectButton>(true)
                    .FirstOrDefault(candidate => candidate != null && candidate.id == buildingId)
                : null;
        }

        private static Button FindCategoryButton(GridConstructTab constructTab, BuildingSO building)
        {
            if (constructTab == null || building == null)
            {
                return null;
            }

            BuildingCategory category = building.IsInteriorDoor
                ? BuildingCategory.Wall
                : building.category;
            string categoryName = BuildingCategoryCatalog.GetDisplayName(category, string.Empty);
            return constructTab.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(candidate => candidate != null
                    && candidate.gameObject.activeInHierarchy
                    && candidate.GetComponent<UIBuildingSelectButton>() == null
                    && candidate.GetComponentInChildren<TMP_Text>(true)?.text == categoryName);
        }

        private void VerifyResearch(UITabManager manager, List<string> lines)
        {
            BlueprintResearchRuntime research = FindActiveSceneComponent<BlueprintResearchRuntime>();
            if (research != null && !research.State.HasActiveTask)
            {
                FacilityBlueprintSO blueprint = research.ShopUnlockState.AcquiredBlueprintIds.Count > 0
                    ? null
                    : FindFirstBlueprint();
                if (blueprint != null)
                {
                    research.ShopUnlockState.MarkBlueprintAcquired(blueprint);
                }
            }

            int beforeTasks = research != null ? research.State.Tasks.Count : -1;
            int beforeCompleted = research != null ? research.State.CompletedBlueprintIds.Count : -1;
            float beforeProgress = GetActiveResearchProgress(research);

            manager.ToggleSelectButton(8);
            Canvas.ForceUpdateCanvases();
            bool cancelClicked = ClickFirst("P0Action_ResearchCancel_");
            Canvas.ForceUpdateCanvases();
            bool startClicked = ClickFirst("P0Action_ResearchStart_");
            Canvas.ForceUpdateCanvases();
            bool progressShortcutPresent = FindActiveButton("P0Action_ResearchProgress") != null;
            GameObject researchState = FindSceneObject("P0State_ResearchWorkSource");
            bool researchStateVisible = researchState != null && researchState.activeInHierarchy;
            int afterTasks = research != null ? research.State.Tasks.Count : -1;
            int afterCompleted = research != null ? research.State.CompletedBlueprintIds.Count : -1;
            float afterProgress = GetActiveResearchProgress(research);

            lines.Add(
                $"RESEARCH visible={IsTabActive(8)}; runtime={research != null}; naturalStateVisible={researchStateVisible}; progressShortcutPresent={progressShortcutPresent}; cancelClicked={cancelClicked}; startClicked={startClicked}; tasks={beforeTasks}->{afterTasks}; completed={beforeCompleted}->{afterCompleted}; progress={beforeProgress:0.##}->{afterProgress:0.##}; queueChanged={beforeTasks != afterTasks}");
        }

        private IEnumerator VerifyNaturalResearchProgress(List<string> lines)
        {
            BlueprintResearchRuntime research = FindActiveSceneComponent<BlueprintResearchRuntime>();
            float progressBefore = GetActiveResearchProgress(research);
            int completedBefore = research?.State.CompletedBlueprintIds.Count ?? -1;
            AppendResearchWorkerDiagnostics(lines, "before");
            float originalScale = Time.timeScale;
            bool changed = false;
            const float naturalProgressionTimeoutSeconds = 15f;
            float deadline = Time.realtimeSinceStartup + naturalProgressionTimeoutSeconds;

            Time.timeScale = 5f;
            try
            {
                while (Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    float currentProgress = GetActiveResearchProgress(research);
                    int currentCompleted = research?.State.CompletedBlueprintIds.Count ?? -1;
                    if (currentProgress > progressBefore + 0.001f || currentCompleted > completedBefore)
                    {
                        changed = true;
                        break;
                    }
                }
            }
            finally
            {
                Time.timeScale = originalScale;
            }

            float progressAfter = GetActiveResearchProgress(research);
            int completedAfter = research?.State.CompletedBlueprintIds.Count ?? -1;
            int assignedResearchers = FindActiveWorkers()
                .Count((work) => work != null && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Research);
            AppendResearchWorkerDiagnostics(lines, "after");
            lines.Add(
                $"NATURAL_RESEARCH changed={changed}; progress={progressBefore:0.##}->{progressAfter:0.##}; completed={completedBefore}->{completedAfter}; assignedResearchers={assignedResearchers}; publicProgressShortcut={FindActiveButton("P0Action_ResearchProgress") != null}");
        }

        private IEnumerator VerifyResearchConstructionUnlockThroughUi(List<string> lines)
        {
            BlueprintResearchRuntime research = FindActiveSceneComponent<BlueprintResearchRuntime>();
            LifetimeScope scope = FindActiveSceneLifetimeScope();
            IFacilityShopCatalog shopCatalog = scope?.Container?.Resolve(typeof(IFacilityShopCatalog))
                as IFacilityShopCatalog;
            IDataCatalog dataCatalog = scope?.Container?.Resolve(typeof(IDataCatalog)) as IDataCatalog;
            IDungeonGridBuildingControllerProvider controllerProvider = scope?.Container?.Resolve(
                    typeof(IDungeonGridBuildingControllerProvider))
                as IDungeonGridBuildingControllerProvider;
            GameManager gameManager = FindActiveSceneComponent<GameManager>();
            GameData gameData = gameManager != null ? gameManager.gameData : null;

            if (research == null
                || shopCatalog == null
                || dataCatalog == null
                || controllerProvider?.Controller == null
                || gameData == null)
            {
                Debug.LogError("Research construction unlock UI verification setup is incomplete.");
                yield break;
            }

            IReadOnlyDictionary<int, BuildingSO> buildings = dataCatalog.GetData<BuildingSO>();
            int currentPhase = FacilityProgression.GetCurrentPhase(gameData);
            int unlockCountBefore = research.State.UnlockedBuildingIds.Count;
            FacilityBlueprintSO blueprint = shopCatalog.Blueprints
                .Where(candidate => candidate != null && !research.State.IsCompleted(candidate))
                .FirstOrDefault(candidate => candidate.Unlocks
                    .OfType<IBlueprintBuildingUnlock>()
                    .Any(unlock => buildings.TryGetValue(unlock.BuildingId, out BuildingSO building)
                        && building != null
                        && building.IsModularFacility()
                        && building.GetUnlockPhase() > currentPhase));
            IBlueprintBuildingUnlock buildingUnlock = blueprint?.Unlocks
                .OfType<IBlueprintBuildingUnlock>()
                .FirstOrDefault(unlock => buildings.TryGetValue(
                    unlock.BuildingId,
                    out BuildingSO building)
                    && building != null
                    && building.IsModularFacility()
                    && building.GetUnlockPhase() > currentPhase);
            BuildingSO targetBuilding = buildingUnlock != null
                && buildings.TryGetValue(buildingUnlock.BuildingId, out BuildingSO resolved)
                    ? resolved
                    : null;
            Button constructionTabButton = FindActiveTabButton(TabId.Construction);

            if (blueprint == null || targetBuilding == null || constructionTabButton == null)
            {
                string mappingSummary = string.Join(" || ", shopCatalog.Blueprints
                    .OrderBy(candidate => candidate != null ? candidate.id : int.MaxValue)
                    .Select(candidate =>
                    {
                        if (candidate == null)
                        {
                            return "null";
                        }

                        string rewards = string.Join(",", candidate.Unlocks
                            .OfType<IBlueprintBuildingUnlock>()
                            .Select(unlock => buildings.TryGetValue(
                                    unlock.BuildingId,
                                    out BuildingSO building)
                                ? $"{unlock.BuildingId}:{building.objectName}:mod={building.IsModularFacility()}:p={building.GetUnlockPhase()}"
                                : $"{unlock.BuildingId}:missing"));
                        return $"{candidate.id}:{candidate.DisplayName}[{rewards}]";
                    }));
                lines.Add(
                    $"RESEARCH_CONSTRUCTION_UI setupFailed=true; day={gameData.day.Value}; phase={currentPhase}; stateUnlocks={unlockCountBefore}; blueprints={shopCatalog.Blueprints.Count}; buildings={buildings.Count}");
                lines.Add($"RESEARCH_CONSTRUCTION_MAPPINGS {mappingSummary}");
                Debug.LogError("No locked modular research reward is available for public UI verification.");
                yield break;
            }

            yield return ClickWithInput(constructionTabButton);
            yield return null;

            GridConstructTab constructTab = FindActiveSceneComponent<GridConstructTab>();
            Button lockedCategoryButton = FindCategoryButton(constructTab, targetBuilding);
            yield return ClickWithInput(lockedCategoryButton);
            yield return null;

            constructTab = FindActiveSceneComponent<GridConstructTab>();
            bool lockedBefore = !FacilityProgression.IsUnlocked(targetBuilding, gameData, research.State);
            UIBuildingSelectButton lockedSelectButton = FindBuildingSelectButton(
                constructTab,
                targetBuilding.id);
            Button lockedUnityButton = lockedSelectButton != null
                ? lockedSelectButton.GetComponent<Button>()
                : null;
            bool buttonDisabledBefore = lockedUnityButton != null && !lockedUnityButton.interactable;

            constructionTabButton = FindActiveTabButton(TabId.Construction);
            yield return ClickWithInput(constructionTabButton);
            yield return null;

            foreach (BlueprintResearchTask queued in research.State.Tasks.ToArray())
            {
                if (queued?.Blueprint != null && !queued.IsCompleted)
                {
                    research.TryCancelBlueprint(queued.Blueprint, out _);
                }
            }

            research.ShopUnlockState.MarkBlueprintAcquired(blueprint);
            bool queuedTarget = research.EnqueueBlueprint(blueprint);
            BuildableObject researchFacility = FindActiveSceneComponents<BuildableObject>()
                .FirstOrDefault(candidate => candidate != null
                    && candidate.gameObject.activeInHierarchy
                    && candidate.SupportsWork(BuiltInWorkTypeIds.Research));
            CharacterActor researcher = FindActiveWorkers()
                .Select(worker => worker?.WorkerActor)
                .FirstOrDefault(actor => actor != null && !actor.IsDead);
            BlueprintResearchWorkResult completion = researchFacility != null
                ? research.ApplyResearchWork(researcher, researchFacility, 999f)
                : default;
            yield return null;
            yield return null;

            bool unlockedAfter = research.State.IsBuildingUnlocked(targetBuilding.id)
                && FacilityProgression.IsUnlocked(targetBuilding, gameData, research.State);
            constructionTabButton = FindActiveTabButton(TabId.Construction);
            yield return ClickWithInput(constructionTabButton);
            yield return null;

            constructTab = FindActiveSceneComponent<GridConstructTab>();
            Button categoryButton = FindCategoryButton(constructTab, targetBuilding);
            yield return ClickWithInput(categoryButton);
            yield return null;

            constructTab = FindActiveSceneComponent<GridConstructTab>();
            UIBuildingSelectButton selectButton = FindBuildingSelectButton(constructTab, targetBuilding.id);
            Button unitySelectButton = selectButton != null ? selectButton.GetComponent<Button>() : null;
            bool buttonVisibleAfter = unitySelectButton != null
                && unitySelectButton.gameObject.activeInHierarchy
                && unitySelectButton.interactable;
            yield return ClickWithInput(unitySelectButton);
            yield return null;

            DungeonStoryGridBuildingController controller = controllerProvider.Controller;
            bool pointerSelected = controller.GridSystem.Mode == GridMode.Build
                && controller.SelectedBuilding != null
                && controller.SelectedBuilding.id == targetBuilding.id;
            yield return CaptureTabAtEndOfFrame(
                "Temp/p0-ui-research-construction-unlock.png",
                lines);
            bool passed = lockedBefore
                && buttonDisabledBefore
                && queuedTarget
                && completion.Completed
                && unlockedAfter
                && categoryButton != null
                && buttonVisibleAfter
                && pointerSelected;

            lines.Add(
                $"RESEARCH_CONSTRUCTION_UI passed={passed}; blueprint={blueprint.DisplayName}; target={targetBuilding.objectName}#{targetBuilding.id}; day={gameData.day.Value}; phase={currentPhase}; stateUnlocks={unlockCountBefore}; lockedBefore={lockedBefore}; buttonDisabledBefore={buttonDisabledBefore}; queued={queuedTarget}; completed={completion.Completed}; unlockedAfter={unlockedAfter}; categoryPointer={categoryButton != null}; buttonVisibleAfter={buttonVisibleAfter}; selected={controller.SelectedBuilding?.id}; mode={controller.GridSystem.Mode}; pointerSelected={pointerSelected}");
            if (!passed)
            {
                Debug.LogError("Research completion did not unlock and select its modular construction reward through public UI.");
            }
        }

        private IEnumerator ConfigureResearchPriorityThroughUi(UITabManager manager, List<string> lines)
        {
            manager.ToggleSelectButton(2);
            yield return null;

            StaffWorkPriorityPanel panel = UnityEngine.Object.FindObjectsByType<StaffWorkPriorityPanel>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault((candidate) => candidate != null && candidate.gameObject.activeInHierarchy);
            panel?.Refresh();
            yield return null;

            AbilityWork target = FindActiveWorkers()
                .Where((work) => work != null
                    && work.WorkerActor != null
                    && !work.WorkerActor.IsDead
                    && work.WorkPriorities.IsEnabled(BuiltInWorkTypeIds.Research))
                .OrderByDescending((work) => work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Research))
                .FirstOrDefault();
            WorkPriorityLevel before = target != null
                ? target.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Research)
                : WorkPriorityLevel.Off;
            int pointerClicks = 0;
            while (target != null
                && target.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Research) != WorkPriorityLevel.Priority1
                && pointerClicks < 4)
            {
                Button cell = FindActiveButton($"Cell_{target.WorkerActor.GetInstanceID()}_{FacilityWorkType.Research}");
                if (cell == null)
                {
                    break;
                }

                yield return ClickWithInput(cell);
                pointerClicks++;
                panel?.Refresh();
                yield return null;
            }

            WorkPriorityLevel after = target != null
                ? target.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Research)
                : WorkPriorityLevel.Off;
            lines.Add(
                $"RESEARCH_PRIORITY_UI panel={panel != null}; actor={target?.WorkerActor?.name ?? "none"}; priority={before}->{after}; pointerClicks={pointerClicks}; configured={after == WorkPriorityLevel.Priority1}");
        }

        private static void AppendResearchWorkerDiagnostics(List<string> lines, string phase)
        {
            foreach (AbilityWork work in FindActiveWorkers())
            {
                if (work == null)
                {
                    continue;
                }

                CharacterActor actor = work.WorkerActor;
                GridPathSearchResult search = actor != null && actor.Brain != null
                    ? actor.Brain.GetPathSearch(actor)
                    : null;
                bool found = work.TryGetBestWorkCandidate(
                    BuiltInWorkTypeIds.Research,
                    search,
                    out WorkTargetCandidate candidate);
                bool foundClean = work.TryGetBestWorkCandidate(
                    BuiltInWorkTypeIds.Clean,
                    search,
                    out WorkTargetCandidate cleanCandidate);
                bool foundAny = work.TryGetBestAnyWorkCandidate(
                    search,
                    out WorkTargetCandidate anyCandidate);
                string rejected = work.TryGetLastRejectedWorkCandidate(out WorkTargetCandidate failure)
                    ? $"{failure.FailureKind}:{failure.FailureReason}"
                    : "none";
                AIBrain brain = actor != null ? actor.Brain : null;
                AIAction action = brain != null ? brain.bestAction : null;
                BuildableObject assignedTarget = work.assignedShop;
                lines.Add(
                    $"RESEARCH_WORKER {phase}; actor={actor?.name ?? work.name}; active={work.gameObject.activeInHierarchy}; canRunAi={actor != null && actor.CanRunAi}; pos={(actor != null ? actor.GetNowXY().ToString() : "none")}; offDuty={work.IsOffDuty}; working={work.isWorking}; assigned={work.AssignedWorkTypeId}:{assignedTarget?.name ?? "none"}@{(assignedTarget != null ? assignedTarget.centerPos.ToString() : "none")}; priority={work.WorkPriorities?.GetPriority(BuiltInWorkTypeIds.Research)}; research={found}:{candidate.Building?.name ?? "none"}:{candidate.Score:0.##}; clean={foundClean}:{cleanCandidate.Building?.name ?? "none"}:{cleanCandidate.Score:0.##}; best={foundAny}:{anyCandidate.WorkTypeId}:{anyCandidate.Building?.name ?? "none"}:{anyCandidate.Score:0.##}; action={brain?.CurrentActionDebugLabel ?? "none"}/{brain?.CurrentActionPhase ?? "none"}; running={(action != null ? action.RunningSeconds : -1f):0.##}; plan={(action != null ? action.planKind.ToString() : "none")}:{(action != null ? action.pathSteps.Count : -1)}; moveBlocked={work.WorkerMove != null && work.WorkerMove.LastGridMoveWasBlocked}; rejected={rejected}");
            }
        }

        private static IReadOnlyList<AbilityWork> FindActiveWorkers()
        {
            return Resources.FindObjectsOfTypeAll<AbilityWork>()
                .Where((work) => work != null
                    && work.gameObject.scene.IsValid()
                    && work.gameObject.activeInHierarchy)
                .ToArray();
        }

        private FacilityBlueprintSO FindFirstBlueprint()
        {
            LifetimeScope scope = FindActiveSceneLifetimeScope();
            if (scope == null || scope.Container == null)
            {
                return null;
            }

            IFacilityShopCatalog catalog = scope.Container.Resolve(typeof(IFacilityShopCatalog)) as IFacilityShopCatalog;
            return catalog != null
                ? catalog.Blueprints.FirstOrDefault((blueprint) => blueprint != null)
                : null;
        }

        private int ClickSequential(string prefix, int max)
        {
            int clicked = 0;
            for (int i = 0; i < max; i++)
            {
                if (ClickFirstExact(prefix + i))
                {
                    clicked++;
                }
            }

            return clicked;
        }

        private bool ClickFirst(string prefix)
        {
            Button button = UnityEngine.Object.FindObjectsByType<Button>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault((candidate) => candidate != null
                    && candidate.gameObject.activeInHierarchy
                    && candidate.name.StartsWith(prefix, StringComparison.Ordinal));
            return Click(button);
        }

        private bool ClickFirstExact(string name)
        {
            Button button = UnityEngine.Object.FindObjectsByType<Button>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault((candidate) => candidate != null
                    && candidate.gameObject.activeInHierarchy
                    && candidate.name == name);
            return Click(button);
        }

        private static bool Click(Button button)
        {
            if (button == null || !button.IsInteractable())
            {
                return false;
            }

            button.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            return true;
        }

        private int CountActiveButtons(string prefix)
        {
            return UnityEngine.Object.FindObjectsByType<Button>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Count((button) => button != null
                    && button.gameObject.activeInHierarchy
                    && button.name.StartsWith(prefix, StringComparison.Ordinal));
        }

        private bool IsTabActive(int id)
        {
            return UnityEngine.Object.FindObjectsByType<UITab>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Any((tab) => tab != null && tab.id == id && tab.gameObject.activeInHierarchy);
        }

        private int GetHoldingMoney()
        {
            GameManager gameManager = FindActiveSceneComponent<GameManager>();
            GameData gameData = gameManager != null ? gameManager.gameData : null;
            return gameData != null && gameData.holdingMoney != null ? gameData.holdingMoney.Value : -1;
        }

        private int GetCurrentDay()
        {
            GameManager gameManager = FindActiveSceneComponent<GameManager>();
            GameData gameData = gameManager != null ? gameManager.gameData : null;
            return gameData != null && gameData.day != null ? gameData.day.Value : -1;
        }

        private static float GetActiveResearchProgress(BlueprintResearchRuntime research)
        {
            if (research == null || !research.State.TryGetActiveTask(out BlueprintResearchTask task))
            {
                return -1f;
            }

            return task.Progress;
        }

        private void RunUiBounds(List<string> lines)
        {
            RectTransform[] rects = UnityEngine.Object.FindObjectsByType<RectTransform>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where((rect) => rect != null && rect.gameObject.activeInHierarchy)
                .ToArray();
            TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where((text) => text != null && text.gameObject.activeInHierarchy)
                .ToArray();
            Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where((button) => button != null && button.gameObject.activeInHierarchy)
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

            lines.Add($"UI activeRects={rects.Length}; invalid={invalid}; oversized={oversized}; activeTexts={texts.Length}; activeButtons={buttons.Length}");
        }

        private static Rect GetWorldRect(RectTransform rectTransform)
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            float minX = corners.Min((corner) => corner.x);
            float maxX = corners.Max((corner) => corner.x);
            float minY = corners.Min((corner) => corner.y);
            float maxY = corners.Max((corner) => corner.y);
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private T FindActiveSceneComponent<T>() where T : Component
        {
            return FindActiveSceneComponents<T>().FirstOrDefault();
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
            if (scope == null || scope.Container == null || target == null)
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

            return scopes.FirstOrDefault((scope) =>
                    scope != null
                    && scope.Container != null
                    && scope.gameObject.scene == activeScene)
                ?? scopes.FirstOrDefault((scope) => scope != null && scope.Container != null);
        }

        private static GameObject FindSceneObject(string name)
        {
            return Resources.FindObjectsOfTypeAll<Transform>()
                .Where(candidate => candidate != null && candidate.gameObject.scene.IsValid())
                .Select(candidate => candidate.gameObject)
                .FirstOrDefault(candidate => candidate.name == name);
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

            verificationMouse = InputSystem.AddDevice<Mouse>("P0FeatureSurfaceVerificationMouse");
            verificationMouse.MakeCurrent();
            inputConfigured = true;
        }

        private void BackupMetaProfile()
        {
            LifetimeScope scope = FindActiveSceneLifetimeScope();
            IMetaProfileStore store = scope?.Container?.Resolve(typeof(IMetaProfileStore)) as IMetaProfileStore;
            metaProfilePath = store?.ProfilePath;
            metaProfileExisted = !string.IsNullOrWhiteSpace(metaProfilePath) && File.Exists(metaProfilePath);
            metaProfileBackup = metaProfileExisted ? File.ReadAllBytes(metaProfilePath) : null;
        }

        private void RestoreMetaProfile()
        {
            if (string.IsNullOrWhiteSpace(metaProfilePath))
            {
                return;
            }

            if (!metaProfileExisted)
            {
                if (File.Exists(metaProfilePath))
                {
                    File.Delete(metaProfilePath);
                }

                return;
            }

            string directory = Path.GetDirectoryName(metaProfilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(metaProfilePath, metaProfileBackup ?? Array.Empty<byte>());
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
            if (capturingLogs)
            {
                return;
            }

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
                return;
            }

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                capturedErrors.Add(string.IsNullOrWhiteSpace(stackTrace)
                    ? condition
                    : $"{condition}\n{stackTrace}");
            }
        }

        private void Finish(List<string> lines)
        {
            StopLogCapture();
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
            foreach (UnityEngine.Object obj in tempObjects.Where((item) => item != null).Reverse())
            {
                if (obj == null)
                {
                    continue;
                }

                UnityEngine.Object target = obj is Component component ? component.gameObject : obj;
                if (target is GameObject gameObject)
                {
                    gameObject.SetActive(false);
                }

                if (target != null)
                {
                    DestroyImmediate(target);
                }
            }

            tempObjects.Clear();
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
            return singleLine.Length <= maxLength
                ? singleLine
                : singleLine.Substring(0, maxLength) + "...";
        }

        private static void ClearConsole()
        {
            Type logEntries = Type.GetType("UnityEditor.LogEntries,UnityEditor.dll");
            MethodInfo clear = logEntries?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
            clear?.Invoke(null, null);
        }
    }
}
