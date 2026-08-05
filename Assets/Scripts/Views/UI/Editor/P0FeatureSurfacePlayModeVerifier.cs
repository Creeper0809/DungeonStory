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
using UnityEngine.EventSystems;
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

                yield return VerifyWarehouse(manager, lines);
                RunStep("WAREHOUSE-CAPTURE", lines, () =>
                    CaptureTab("Temp/p0-ui-warehouse.png", lines));
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

                RunStep("RESEARCH-TREE-VISUAL", lines, Canvas.ForceUpdateCanvases);
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
            GameSessionState gameData = gameManager != null ? gameManager.gameData : null;
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
            obj.SetActive(false);
            obj.AddComponent<SpriteRenderer>();
            AIBrain brain = obj.AddComponent<AIBrain>();
            obj.AddComponent<AbilityMove>();
            obj.AddComponent<AbilityShopping>();
            CharacterActor actor = obj.AddComponent<CharacterActor>();
            brain.availableActions = AiDebugScenarioActionFactory.CreateCustomerActions();

            InjectGameObjectFromLifetimeScope(obj);
            actor.RefreshAbilityCache();
            actor.Initialization(data);
            actor.EnsureRuntimeState();
            InjectGameObjectFromLifetimeScope(obj);
            actor.RefreshAbilityCache();
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.stats[CharacterCondition.MOOD] = 100f;
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
                foreach (StockCategoryDefinition definition in
                         ((IStockCategoryDefinitionCatalog)CharacterAiEditorTestDependencies.AuthoredGameplay).All)
                {
                    StockCategory category = definition.Category;
                    removed += warehouse.Inventory.ConsumePhysicalStockForTest(category, 10);
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
            LifetimeScope scope = FindActiveSceneLifetimeScope();
            IWorldItemStackRuntime itemRuntime =
                scope?.Container?.Resolve(typeof(IWorldItemStackRuntime)) as IWorldItemStackRuntime;
            int beforeMoney = GetHoldingMoney();
            int beforeQueue = research != null ? research.State.Projects.Queue.Count : -1;
            int beforeBlueprintItems = itemRuntime?.GetAllStacks()
                .Count(stack => stack != null
                    && stack.Quantity > 0
                    && stack.StockCategory == StockCategory.Blueprint) ?? -1;

            manager.ToggleSelectButton(3);
            Canvas.ForceUpdateCanvases();
            int dailyButtonsBefore = CountActiveButtons("P0Action_ShopDaily_");
            int basicButtonsBefore = CountActiveButtons("P0Action_ShopBasic_");
            int dailyClicked = ClickSequential("P0Action_ShopDaily_", 8);
            Canvas.ForceUpdateCanvases();
            int basicClicked = ClickSequential("P0Action_ShopBasic_", 8);
            int afterMoney = GetHoldingMoney();
            int afterQueue = research != null ? research.State.Projects.Queue.Count : -1;
            int afterBlueprintItems = itemRuntime?.GetAllStacks()
                .Count(stack => stack != null
                    && stack.Quantity > 0
                    && stack.StockCategory == StockCategory.Blueprint) ?? -1;

            lines.Add(
                $"SHOP visible={IsTabActive(3)}; runtime={shopRuntime != null}; dailyButtons={dailyButtonsBefore}; dailyClicked={dailyClicked}; basicButtons={basicButtonsBefore}; basicClicked={basicClicked}; money={beforeMoney}->{afterMoney}; blueprintItems={beforeBlueprintItems}->{afterBlueprintItems}; researchQueue={beforeQueue}->{afterQueue}; stateChanged={afterMoney != beforeMoney || afterBlueprintItems != beforeBlueprintItems}");
        }

        private IEnumerator VerifyWarehouse(UITabManager manager, List<string> lines)
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

            LifetimeScope scope = FindActiveSceneLifetimeScope();
            IResourceStockPolicyRuntime stockPolicies =
                scope?.Container?.Resolve(typeof(IResourceStockPolicyRuntime))
                as IResourceStockPolicyRuntime;
            IResourceEconomyContentCatalog economyCatalog =
                scope?.Container?.Resolve(typeof(IResourceEconomyContentCatalog))
                as IResourceEconomyContentCatalog;
            IItemDefinitionCatalog itemDefinitions =
                scope?.Container?.Resolve(typeof(IItemDefinitionCatalog))
                as IItemDefinitionCatalog;
            IRegionalSupplyContractRuntime contracts =
                scope?.Container?.Resolve(typeof(IRegionalSupplyContractRuntime))
                as IRegionalSupplyContractRuntime;
            IWorldItemStackRuntime itemRuntime =
                scope?.Container?.Resolve(typeof(IWorldItemStackRuntime))
                as IWorldItemStackRuntime;
            IWorldDropZoneQuery dropZones =
                scope?.Container?.Resolve(typeof(IWorldDropZoneQuery))
                as IWorldDropZoneQuery;
            IWarehouseFeatureQueryService warehouseQuery =
                scope?.Container?.Resolve(typeof(IWarehouseFeatureQueryService))
                as IWarehouseFeatureQueryService;
            IResourceEconomyForecastService forecastService =
                scope?.Container?.Resolve(typeof(IResourceEconomyForecastService))
                as IResourceEconomyForecastService;
            BlueprintResearchRuntime research =
                FindActiveSceneComponent<BlueprintResearchRuntime>();

            ResourceEconomyForecast forecast = forecastService?.Capture(3);
            string controlItemId = forecast?.Shortages
                .Concat(forecast.Surpluses)
                .Select(row => row?.ItemId)
                .FirstOrDefault(itemId =>
                    economyCatalog?.TryGetItem(itemId, out _) == true
                    || TryGetAuthoredStockCategory(
                        itemDefinitions,
                        itemId,
                        out _));
            HashSet<string> physicalItemIds = new HashSet<string>(
                itemRuntime?.GetAllStacks()
                    .Where(stack => stack != null && stack.Quantity > 0)
                    .Select(stack => stack.ItemId)
                    ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            ResourceItemDefinitionSO fallbackItem = economyCatalog?.Items
                .FirstOrDefault(item => item != null
                    && physicalItemIds.Contains(item.ItemId));
            controlItemId ??= fallbackItem?.ItemId;
            if (string.IsNullOrWhiteSpace(controlItemId)
                && economyCatalog != null
                && itemRuntime != null
                && dropZones != null
                && dropZones.TryGetDeliveryDropoff(out Vector2Int dropoff))
            {
                foreach (ResourceItemDefinitionSO candidate in
                         economyCatalog.Items.Where(item => item != null))
                {
                    if (!itemRuntime.SpawnItemAt(
                            candidate.ItemId,
                            1,
                            dropoff,
                            WorldItemStackState.Loose,
                            string.Empty,
                            out int spawned)
                        || spawned <= 0)
                    {
                        continue;
                    }

                    controlItemId = candidate.ItemId;
                    break;
                }
            }
            ResourceStockPolicyData policyBefore = null;
            if (stockPolicies != null
                && !string.IsNullOrWhiteSpace(controlItemId))
            {
                int owned = stockPolicies.CountOwned(controlItemId);
                policyBefore = stockPolicies.GetOrCreate(controlItemId);
                policyBefore.enabled = true;
                policyBefore.minimumStock = owned + 50;
                policyBefore.targetStock = owned + 60;
                policyBefore.maximumStock = owned + 70;
                policyBefore.surplusDisposition = StockSurplusDisposition.Hold;
                stockPolicies.SetPolicy(policyBefore, out _);
                policyBefore = stockPolicies.GetOrCreate(controlItemId);
            }

            research?.State.Projects.Complete(
                new ResearchProjectId("research:commerce:integration"));
            PrepareWarehouseContracts(contracts);

            Button warehouseTabButton = FindActiveTabButton(TabId.Warehouse);
            yield return ClickWithInput(warehouseTabButton);
            if (!IsTabActive(4))
            {
                manager.ToggleSelectButton(4);
            }

            Canvas.ForceUpdateCanvases();
            yield return null;
            WarehouseFeatureSurfaceModel warehouseModel =
                warehouseQuery?.Capture();
            lines.Add(
                "WAREHOUSE_CONTROLS modelForecast="
                + string.Join(
                    ",",
                    warehouseModel?.ForecastRows.Select(row => row.ItemId)
                    ?? Array.Empty<string>())
                + "; modelContracts="
                + string.Join(
                    ",",
                    warehouseModel?.Contracts.Select(row =>
                        $"{row.ContractId}:{row.Status}")
                    ?? Array.Empty<string>())
                + "; buttons="
                + string.Join(
                    ",",
                    FindActiveButtonNames(
                        "EconomyForecast_",
                        "RegionalContract_")));

            Button restockButton =
                FindActiveButtonByPrefix("P0Action_WarehouseRestock_");
            ScrollContainingButton(restockButton);
            yield return null;
            bool restockClicked = restockButton != null && restockButton.interactable;
            yield return ClickWithInput(restockButton);

            Canvas.ForceUpdateCanvases();
            Button deliveryButton =
                FindActiveButtonByPrefix("P0Action_WarehouseDelivery_");
            ScrollContainingButton(deliveryButton);
            yield return null;
            bool deliveryClicked = deliveryButton != null && deliveryButton.interactable;
            yield return ClickWithInput(deliveryButton);

            int policyPointerClicks = 0;
            List<string> policyPointerTargets = new List<string>();
            if (!string.IsNullOrWhiteSpace(controlItemId))
            {
                string controlPrefix = "EconomyForecast_" + controlItemId;
                string[] controlNames =
                {
                    controlPrefix + "_Minimum_Increase",
                    controlPrefix + "_Target_Increase",
                    controlPrefix + "_Maximum_Increase",
                    controlPrefix + "_Toggle",
                    controlPrefix + "_Disposition"
                };

                foreach (string controlName in controlNames)
                {
                    Button controlButton = FindActiveButton(controlName);
                    ScrollContainingButton(controlButton);
                    yield return null;
                    policyPointerTargets.Add(
                        controlName + "->" + DescribePointerTarget(controlButton));
                    if (controlButton != null && controlButton.interactable)
                    {
                        yield return ClickWithInput(controlButton);
                        policyPointerClicks++;
                        Canvas.ForceUpdateCanvases();
                        yield return null;
                    }
                }
            }

            RegionalSupplyContractState acceptTarget = contracts?.Contracts
                .FirstOrDefault(contract => contract != null
                    && contract.status == RegionalSupplyContractStatus.Offered);
            bool contractAcceptClicked = false;
            if (acceptTarget != null)
            {
                Button acceptButton = FindActiveButton(
                    "RegionalContract_" + acceptTarget.contractId + "_Accept");
                ScrollContainingButton(acceptButton);
                yield return null;
                contractAcceptClicked =
                    acceptButton != null && acceptButton.interactable;
                yield return ClickWithInput(acceptButton);
                Canvas.ForceUpdateCanvases();
                yield return null;
            }

            RegionalSupplyContractState declineTarget = contracts?.Contracts
                .FirstOrDefault(contract => contract != null
                    && contract.status == RegionalSupplyContractStatus.Offered);
            bool contractDeclineClicked = false;
            string contractDeclinePointerTarget = "none";
            if (declineTarget != null)
            {
                Button declineButton = FindActiveButton(
                    "RegionalContract_" + declineTarget.contractId + "_Decline");
                ScrollContainingButton(declineButton);
                yield return null;
                contractDeclinePointerTarget =
                    DescribePointerTarget(declineButton);
                contractDeclineClicked =
                    declineButton != null && declineButton.interactable;
                yield return ClickWithInput(declineButton);
                Canvas.ForceUpdateCanvases();
                yield return null;
                RegionalSupplyContractStatus declineStatusAfterFirstClick =
                    contracts.Contracts
                        .FirstOrDefault(contract => contract != null
                            && contract.contractId == declineTarget.contractId)
                        ?.status
                    ?? RegionalSupplyContractStatus.Offered;
                if (declineStatusAfterFirstClick
                    == RegionalSupplyContractStatus.Offered)
                {
                    declineButton = FindActiveButton(
                        "RegionalContract_"
                        + declineTarget.contractId
                        + "_Decline");
                    ScrollContainingButton(declineButton);
                    yield return null;
                    yield return null;
                    yield return ClickWithInput(declineButton);
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                }
            }

            int afterWarehouse = warehouses.Sum((warehouse) => warehouse.Inventory.TotalStock);
            int afterShopStock = shops.Sum((shop) => shop.CurrentStock);
            int afterMoney = GetHoldingMoney();
            ResourceStockPolicyData policyAfter =
                !string.IsNullOrWhiteSpace(controlItemId)
                ? stockPolicies?.GetOrCreate(controlItemId)
                : null;
            RegionalSupplyContractStatus? acceptStatus = acceptTarget != null
                ? contracts?.Contracts
                    .FirstOrDefault(contract => contract != null
                        && contract.contractId == acceptTarget.contractId)
                    ?.status
                : null;
            RegionalSupplyContractStatus? declineStatus = declineTarget != null
                ? contracts?.Contracts
                    .FirstOrDefault(contract => contract != null
                        && contract.contractId == declineTarget.contractId)
                    ?.status
                : null;
            bool policyChanged = policyBefore != null
                && policyAfter != null
                && policyAfter.minimumStock == policyBefore.minimumStock + 5
                && policyAfter.targetStock == policyBefore.targetStock + 5
                && policyAfter.maximumStock == policyBefore.maximumStock + 5
                && policyAfter.enabled != policyBefore.enabled
                && policyAfter.surplusDisposition !=
                    policyBefore.surplusDisposition;
            bool contractsChanged =
                (acceptTarget == null
                    || acceptStatus is RegionalSupplyContractStatus.Accepted
                        or RegionalSupplyContractStatus.Delivering
                        or RegionalSupplyContractStatus.Completed)
                && (declineTarget == null
                    || declineStatus == RegionalSupplyContractStatus.Declined);
            lines.Add(
                $"WAREHOUSE visible={IsTabActive(4)}; tabPointerClicked={warehouseTabButton != null}; warehouses={warehouses.Count}; shops={shops.Count}; restockPointerClicked={restockClicked}; deliveryPointerClicked={deliveryClicked}; warehouseStock={beforeWarehouse}->{afterWarehouse}; shopStock={beforeShopStock}->{afterShopStock}; money={beforeMoney}->{afterMoney}; policyItem={controlItemId ?? "none"}; policyPointerClicks={policyPointerClicks}/5; policyPointerTargets={string.Join(",", policyPointerTargets)}; policy={FormatPolicy(policyBefore)}->{FormatPolicy(policyAfter)}; policyChanged={policyChanged}; contractAcceptPointerClicked={contractAcceptClicked}; contractAcceptStatus={acceptStatus?.ToString() ?? "none"}; contractDeclinePointerClicked={contractDeclineClicked}; contractDeclinePointerTarget={contractDeclinePointerTarget}; contractDeclineStatus={declineStatus?.ToString() ?? "none"}; contractsChanged={contractsChanged}; stateChanged={beforeWarehouse != afterWarehouse || beforeShopStock != afterShopStock || beforeMoney != afterMoney || policyChanged || contractsChanged}");
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
            GameObject flowSection = FindSceneObject("Section_작업·물류") as GameObject;
            bool flowSectionVisible = flowSection != null && flowSection.activeInHierarchy;
            bool flowStateVisible = Resources.FindObjectsOfTypeAll<Transform>()
                .Any(candidate => candidate != null
                    && candidate.gameObject.scene.IsValid()
                    && candidate.gameObject.activeInHierarchy
                    && candidate.name.StartsWith("P0State_Flow_", StringComparison.Ordinal));
            string flowText = flowSectionVisible
                ? string.Join(
                    " / ",
                    flowSection.GetComponentsInChildren<TMP_Text>(includeInactive: false)
                        .Select(text => text.text?.Replace("\r", " ").Replace("\n", " ").Trim())
                        .Where(text => !string.IsNullOrWhiteSpace(text)))
                : string.Empty;
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
                $"OPERATION visible={IsTabActive(5)}; flowSection={flowSectionVisible}; flowState={flowStateVisible}; flowText={flowText}; fundingClicked={fundingClicked}; fundingUsed={beforeFundingUsed}->{afterFundingUsed}; money={beforeMoney}->{afterMoney}; debt={beforeDebt}->{afterDebt}; report={(report != null)}; recruitClicked={recruitClicked}; recruited={beforeRecruited}->{afterRecruited}; strategyCards={strategyCardsPresent}; commerceMetaClicked={metaClicked}; commerceLevel={beforeStrategyLevel}->{afterStrategyLevel}; metaCurrency={beforeCurrency}->{afterCurrency}; metaLevels={beforeLevels}->{afterLevels}; stateChanged={flowSectionVisible && flowStateVisible && (beforeFundingUsed != afterFundingUsed || beforeMoney != afterMoney || beforeDebt != afterDebt || beforeRecruited != afterRecruited || beforeLevels != afterLevels)}");
        }

        private static string FormatPolicy(ResourceStockPolicyData policy)
        {
            return policy == null
                ? "none"
                : $"{policy.minimumStock}/{policy.targetStock}/{policy.maximumStock}"
                    + $":enabled={policy.enabled}"
                    + $":surplus={policy.surplusDisposition}";
        }

        private static void PrepareWarehouseContracts(
            IRegionalSupplyContractRuntime contracts)
        {
            if (contracts == null
                || contracts.Contracts.Count(contract => contract != null
                    && contract.status == RegionalSupplyContractStatus.Offered) >= 2)
            {
                return;
            }

            DungeonRegionalSupplyContractSaveData saveData =
                contracts.Capture();
            saveData.nextOfferDay = Mathf.Max(saveData.currentDay + 3, 4);
            saveData.contracts.RemoveAll(contract => contract != null
                && contract.contractId.StartsWith(
                    "qa:p0:",
                    StringComparison.Ordinal));
            for (int index = 0; index < 2; index++)
            {
                saveData.contracts.Add(new RegionalSupplyContractState
                {
                    contractId = $"qa:p0:{index}",
                    title = index == 0
                        ? "검증용 식수 공급"
                        : "검증용 연료 공급",
                    regionName = "검증 교역권",
                    offeredDay = saveData.currentDay,
                    deadlineDay = saveData.currentDay + 3,
                    rewardGold = 75 + index * 25,
                    status = RegionalSupplyContractStatus.Offered,
                    lastStatus = "계약 결정을 기다리고 있습니다.",
                    requirements = new List<RegionalSupplyContractRequirement>
                    {
                        new RegionalSupplyContractRequirement
                        {
                            itemId = index == 0
                                ? "resource:clean-water"
                                : "material:low-fuel",
                            amount = 1
                        }
                    }
                });
            }

            contracts.PublishRestoreCandidate(
                contracts.PrepareRestoreCandidate(saveData));
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

        private static string DescribePointerTarget(Button button)
        {
            if (button == null || EventSystem.current == null)
            {
                return "missing";
            }

            RectTransform rect = button.transform as RectTransform;
            if (rect == null)
            {
                return "no-rect";
            }

            Vector2 point = RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center));
            PointerEventData eventData = new PointerEventData(
                EventSystem.current)
            {
                position = point
            };
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            string top = results.Count > 0 && results[0].gameObject != null
                ? results[0].gameObject.name
                : "none";
            return $"{point.x:0},{point.y:0}:{top}";
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
                scroll.content,
                target);
            if (overflow > 0.1f)
            {
                float distanceFromTop =
                    scroll.content.rect.yMax - targetBounds.center.y;
                float desiredOffset = Mathf.Clamp(
                    distanceFromTop - scroll.viewport.rect.height * 0.5f,
                    0f,
                    overflow);
                scroll.verticalNormalizedPosition =
                    1f - desiredOffset / overflow;
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

        private static Button FindActiveButtonByPrefix(string prefix)
        {
            return UnityEngine.Object.FindObjectsByType<Button>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null
                    && candidate.gameObject.activeInHierarchy
                    && candidate.name.StartsWith(
                        prefix,
                        StringComparison.Ordinal));
        }

        private static IEnumerable<string> FindActiveButtonNames(
            params string[] prefixes)
        {
            string[] requested = prefixes ?? Array.Empty<string>();
            return UnityEngine.Object.FindObjectsByType<Button>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where(candidate => candidate != null
                    && candidate.gameObject.activeInHierarchy
                    && requested.Any(prefix => candidate.name.StartsWith(
                        prefix,
                        StringComparison.Ordinal)))
                .Select(candidate => candidate.name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
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
            string categoryName =
                ((IBuildingCategoryDefinitionCatalog)CharacterAiEditorTestDependencies.AuthoredGameplay)
                .GetDisplayName(category, string.Empty);
            return constructTab.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(candidate => candidate != null
                    && candidate.gameObject.activeInHierarchy
                    && candidate.GetComponent<UIBuildingSelectButton>() == null
                    && candidate.GetComponentInChildren<TMP_Text>(true)?.text == categoryName);
        }

        private void VerifyResearch(UITabManager manager, List<string> lines)
        {
            BlueprintResearchRuntime research = FindActiveSceneComponent<BlueprintResearchRuntime>();
            int beforeQueue = research != null ? research.State.Projects.Queue.Count : -1;
            int beforeCompleted = research != null
                ? research.State.Projects.CompletedProjectIds.Count
                : -1;
            float beforeProgress = GetActiveResearchProgress(research);

            manager.ToggleSelectButton(8);
            Canvas.ForceUpdateCanvases();
            ResearchTreeWindow window = FindVisibleResearchTreeWindow();
            int nodeCount = window != null
                ? window.GetComponentsInChildren<Button>(false)
                    .Count(button => button != null
                        && button.name.StartsWith("Node_", StringComparison.Ordinal))
                : 0;
            ResearchProjectSO availableProject = research?.ProjectCatalog?.Projects
                .FirstOrDefault(project => project != null
                    && research.GetNodeState(project, out _) == ResearchNodeState.Available);
            bool nodeClicked = availableProject != null
                && ClickFirstExact($"Node_{availableProject.ProjectId.Value}");
            bool actionClicked = nodeClicked && ClickFirstExact("ProjectAction");
            Canvas.ForceUpdateCanvases();
            int afterQueue = research != null ? research.State.Projects.Queue.Count : -1;
            int afterCompleted = research != null
                ? research.State.Projects.CompletedProjectIds.Count
                : -1;
            float afterProgress = GetActiveResearchProgress(research);

            lines.Add(
                $"RESEARCH visible={window != null && window.gameObject.activeInHierarchy}; runtime={research != null}; nodes={nodeCount}; selected={availableProject?.ProjectId.Value ?? "none"}; nodeClicked={nodeClicked}; actionClicked={actionClicked}; queue={beforeQueue}->{afterQueue}; completed={beforeCompleted}->{afterCompleted}; progress={beforeProgress:0.##}->{afterProgress:0.##}; queueChanged={beforeQueue != afterQueue}");
        }

        private IEnumerator VerifyNaturalResearchProgress(List<string> lines)
        {
            BlueprintResearchRuntime research = FindActiveSceneComponent<BlueprintResearchRuntime>();
            float progressBefore = GetActiveResearchProgress(research);
            int completedBefore = research?.State.Projects.CompletedProjectIds.Count ?? -1;
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
                    int currentCompleted = research?.State.Projects.CompletedProjectIds.Count ?? -1;
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
            int completedAfter = research?.State.Projects.CompletedProjectIds.Count ?? -1;
            int assignedResearchers = FindActiveWorkers()
                .Count((work) => work != null && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Research);
            AppendResearchWorkerDiagnostics(lines, "after");
            lines.Add(
                $"NATURAL_RESEARCH changed={changed}; progress={progressBefore:0.##}->{progressAfter:0.##}; completed={completedBefore}->{completedAfter}; assignedResearchers={assignedResearchers}; treeOpen={FindActiveSceneComponent<ResearchTreeWindow>() != null}");
        }

        private IEnumerator VerifyResearchConstructionUnlockThroughUi(List<string> lines)
        {
            BlueprintResearchRuntime research = FindActiveSceneComponent<BlueprintResearchRuntime>();
            LifetimeScope scope = FindActiveSceneLifetimeScope();
            IResearchProjectCatalog projectCatalog =
                scope?.Container?.Resolve(typeof(IResearchProjectCatalog)) as IResearchProjectCatalog;
            IDataCatalog dataCatalog = scope?.Container?.Resolve(typeof(IDataCatalog)) as IDataCatalog;
            GameManager gameManager = FindActiveSceneComponent<GameManager>();
            GameSessionState gameData = gameManager != null ? gameManager.gameData : null;

            if (research == null
                || projectCatalog == null
                || dataCatalog == null
                || gameData == null)
            {
                Debug.LogError("Research construction unlock UI verification setup is incomplete.");
                yield break;
            }

            IReadOnlyDictionary<int, BuildingSO> buildings = dataCatalog.GetData<BuildingSO>();
            int currentPhase = FacilityProgression.GetCurrentPhase(gameData);
            ResearchProjectSO project = projectCatalog.Projects
                .Where(candidate => candidate != null
                    && !research.State.Projects.IsCompleted(candidate.ProjectId)
                    && candidate.Unlocks
                    .OfType<IBlueprintBuildingUnlock>()
                    .Any(unlock => buildings.TryGetValue(unlock.BuildingId, out BuildingSO building)
                        && building != null
                        && building.IsModularFacility()
                        && building.GetUnlockPhase() > currentPhase))
                .OrderBy(candidate => candidate.ProjectId.Value, StringComparer.Ordinal)
                .FirstOrDefault();
            IBlueprintBuildingUnlock buildingUnlock = project?.Unlocks
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

            if (project == null || targetBuilding == null)
            {
                Debug.LogError("No locked modular research reward is available for public UI verification.");
                yield break;
            }

            bool lockedBefore = !FacilityProgression.IsUnlocked(
                targetBuilding,
                gameData,
                research.State,
                DisabledDungeonDebugRuleQuery.Instance);
            ResearchNodeState state = research.GetNodeState(project, out string blocker);
            ResearchTreeWindow window = FindVisibleResearchTreeWindow();
            if (window == null)
            {
                Button researchTabButton = FindActiveTabButton(TabId.Research);
                yield return ClickWithInput(researchTabButton);
                yield return null;
                Canvas.ForceUpdateCanvases();
                window = FindVisibleResearchTreeWindow();
            }

            bool nodeCentered = window != null && window.CenterProject(project);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Button nodeButton = FindButtonInResearchTree(
                window,
                $"Node_{project.ProjectId.Value}");
            bool nodeFound = nodeButton != null;
            yield return ClickWithInput(nodeButton);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Button actionButton = FindButtonInResearchTree(window, "ProjectAction");
            bool actionBlocked = actionButton != null && !actionButton.interactable;
            yield return CaptureTabAtEndOfFrame(
                "Temp/p0-ui-research-construction-unlock.png",
                lines);
            bool passed = lockedBefore
                && nodeCentered
                && nodeFound
                && actionBlocked
                && state is ResearchNodeState.Locked or ResearchNodeState.BlueprintInTransit
                && !string.IsNullOrWhiteSpace(blocker);

            lines.Add(
                $"RESEARCH_CONSTRUCTION_GATE passed={passed}; project={project.DisplayName}; target={targetBuilding.objectName}#{targetBuilding.id}; phase={currentPhase}; locked={lockedBefore}; centered={nodeCentered}; node={nodeFound}; actionBlocked={actionBlocked}; state={state}; blocker={blocker}");
            if (!passed)
            {
                Debug.LogError("Locked research reward was not represented by the research tree's public gating UI.");
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
                BuildableObject researchTarget =
                    WorkTargetCandidateRuntimeAdapter.ResolveBuilding(candidate);
                BuildableObject cleanTarget =
                    WorkTargetCandidateRuntimeAdapter.ResolveBuilding(
                        cleanCandidate);
                BuildableObject anyTarget =
                    WorkTargetCandidateRuntimeAdapter.ResolveBuilding(anyCandidate);
                lines.Add(
                    $"RESEARCH_WORKER {phase}; actor={actor?.name ?? work.name}; active={work.gameObject.activeInHierarchy}; canRunAi={actor != null && actor.CanRunAi}; pos={(actor != null ? actor.GetNowXY().ToString() : "none")}; offDuty={work.IsOffDuty}; working={work.isWorking}; assigned={work.AssignedWorkTypeId}:{assignedTarget?.name ?? "none"}@{(assignedTarget != null ? assignedTarget.centerPos.ToString() : "none")}; priority={work.WorkPriorities?.GetPriority(BuiltInWorkTypeIds.Research)}; research={found}:{researchTarget?.name ?? "none"}:{candidate.Score:0.##}; clean={foundClean}:{cleanTarget?.name ?? "none"}:{cleanCandidate.Score:0.##}; best={foundAny}:{anyCandidate.WorkTypeId}:{anyTarget?.name ?? "none"}:{anyCandidate.Score:0.##}; action={brain?.CurrentActionDebugLabel ?? "none"}/{brain?.CurrentActionPhase ?? "none"}; running={(action != null ? action.RunningSeconds : -1f):0.##}; plan={(action != null ? action.planKind.ToString() : "none")}:{(action != null ? action.pathSteps.Count : -1)}; moveBlocked={work.WorkerMove != null && work.WorkerMove.LastGridMoveWasBlocked}; rejected={rejected}");
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
            GameSessionState gameData = gameManager != null ? gameManager.gameData : null;
            return gameData != null && gameData.holdingMoney != null ? gameData.holdingMoney.Value : -1;
        }

        private int GetCurrentDay()
        {
            GameManager gameManager = FindActiveSceneComponent<GameManager>();
            GameSessionState gameData = gameManager != null ? gameManager.gameData : null;
            return gameData != null && gameData.day != null ? gameData.day.Value : -1;
        }

        private static float GetActiveResearchProgress(BlueprintResearchRuntime research)
        {
            if (research == null)
            {
                return -1f;
            }

            ResearchProjectId activeProjectId = research.State.Projects.ActiveProjectId;
            if (!activeProjectId.IsValid
                || !research.ProjectCatalog.TryGet(activeProjectId, out ResearchProjectSO project))
            {
                return -1f;
            }

            return research.State.Projects.GetProgress(activeProjectId).Progress;
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

        private ResearchTreeWindow FindVisibleResearchTreeWindow()
        {
            return FindActiveSceneComponents<ResearchTreeWindow>()
                .FirstOrDefault(candidate =>
                    candidate != null
                    && candidate.gameObject.activeInHierarchy);
        }

        private static Button FindButtonInResearchTree(
            ResearchTreeWindow window,
            string buttonName)
        {
            return window != null
                ? window.GetComponentsInChildren<Button>(includeInactive: false)
                    .FirstOrDefault(candidate =>
                        candidate != null
                        && candidate.gameObject.activeInHierarchy
                        && string.Equals(
                            candidate.name,
                            buttonName,
                            StringComparison.Ordinal))
                : null;
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

        private static bool TryGetAuthoredStockCategory(
            IItemDefinitionCatalog itemDefinitions,
            string itemId,
            out StockCategory category)
        {
            if (itemDefinitions != null
                && itemDefinitions.TryGet(
                    (ItemDefinitionId)itemId,
                    out ItemDefinitionSO definition))
            {
                category = definition.StockCategory;
                return true;
            }

            category = default;
            return false;
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
