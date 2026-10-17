#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

public static class WimQualityEstimatePlayModeVerifier
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-056-live-apparel-estimate.txt";
    private const string Pending = "DungeonStory.WIM056.Live.Pending";
    private const string SharedQuality = "DungeonStory.WIM006.Shared.Live";
    private const string NaturalQuality = "DungeonStory.WIM006.Natural.Live";
    private static int phase;
    private static double deadline;
    private static BuildableObject facility;
    private static GameObject panel;
    private static float originalScale;
    private static ProductionRecipeSO naturalRecipe;
    private static ProductionBillId naturalBill;
    private static BuildableObject generator;
    private static int initialOutputCount;
    private static int naturalTarget;
    private static bool observedPowered;
    private static float observedWork;
    private static double nextDiagnostic;

    [MenuItem("DungeonStory/QA/WIM/056 Live Apparel Estimate")]
    public static void RequestRun()
    {
        SessionState.SetBool(NaturalQuality, false);
        SessionState.SetBool(SharedQuality, false);
        StartRun();
    }

    public static void RequestSharedQualityRun()
    {
        SessionState.SetBool(NaturalQuality, false);
        SessionState.SetBool(SharedQuality, true);
        StartRun();
    }

    public static void RequestNaturalQualityRun()
    {
        SessionState.SetBool(NaturalQuality, true);
        SessionState.SetBool(SharedQuality, true);
        StartRun();
    }

    private static void StartRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Start from Edit Mode; the fixture exits without saving the scene.");
        SessionState.SetBool(Pending, true);
        Write("RUNNING\n");
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged -= OnState;
        EditorApplication.playModeStateChanged += OnState;
    }

    private static void OnState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            phase = 0;
            originalScale = Time.timeScale;
            deadline = EditorApplication.timeSinceStartup + 45d;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Pending, false);
            Write("FAIL\nInterrupted before completion.\n");
        }
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Live quality fixture readiness timed out.");
            DungeonRuntimeLifetimeScope scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(value => value.Container != null);
            if (scope == null) return;
            IObjectResolver container = scope.Container;
            if (phase == 0)
            {
                phase = 1;
                if (container.Resolve<ICharacterWorldQuery>().Characters.Count == 0)
                    Debug.Log("WIM056 party fixture: " + StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug());
                return;
            }
            if (container.Resolve<ICharacterWorldQuery>().Characters.Count == 0) return;
            if (phase == 3)
            {
                TickNaturalQuality(container);
                return;
            }
            if (phase == 1)
            {
                Time.timeScale = 0f;
                BuildingSO definition = AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
                    .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal)
                    .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>).First(value =>
                        SessionState.GetBool(SharedQuality, false) ? SupportsSharedQuality(container, value)
                            : ApparelTailoringFacilityEligibility.IsEligible(value));
                facility = Place(container, definition);
                if (SessionState.GetBool(SharedQuality, false))
                {
                    var mode = container.Resolve<IAutomationInfrastructureCommand>().SetMode(facility, AutomationMode.Automatic);
                    Require(mode.Succeeded, "Automatic fixture mode failed: " + mode.Failure);
                }
                phase = 2;
                return;
            }
            if (SessionState.GetBool(SharedQuality, false))
            {
                RunSharedQuality(container);
                if (SessionState.GetBool(NaturalQuality, false))
                {
                    PrepareNaturalQuality(container);
                    phase = 3;
                    return;
                }
            }
            else
            {
                RunApparel(container);
                RunCombat(container);
                RunConstruction(container);
            }
            Finish();
        }
        catch (Exception exception)
        {
            Write("FAIL\n" + exception + "\n");
            Debug.LogException(exception);
            Finish();
        }
    }

    private static void RunApparel(IObjectResolver container)
    {
        IApparelDefinitionCatalog apparel = container.Resolve<IApparelDefinitionCatalog>();
        ITextileMaterialCatalog textile = container.Resolve<ITextileMaterialCatalog>();
        ApparelDefinitionSO definition = apparel.Definitions.OrderBy(value => value.ApparelId, StringComparer.Ordinal).First();
        TextileMaterialDefinitionSO material = textile.Definitions.Where(value =>
            (value.Tags & definition.AllowedMaterialTags) != 0).OrderBy(value => value.MaterialId, StringComparer.Ordinal).First();
        IItemTransferService transfer = container.Resolve<IItemTransferService>();
        int required = Mathf.Max(1, Mathf.CeilToInt(2f * definition.TailoringCoefficient));
        Require(transfer.TrySpawnItem(material.PhysicalItemId, required, facility.centerPos,
            WorldItemStackState.Loose, string.Empty, out int spawned) && spawned == required, "Physical source stock failed.");
        IApparelWorkOrderCommand command = container.Resolve<IApparelWorkOrderCommand>();
        Require(command.CreateCraft(new ApparelCraftOrderRequest(definition.ApparelId,
                ApparelSizeClass.Medium, ApparelModificationKind.None,
                ApparelMaterialSelectionPolicy.ExactMaterial, material.MaterialId,
                minimumCraftsmanshipQuality: CraftsmanshipQualityTier.Good, maximumAttempts: 3),
                out string orderId, out DomainFailure failure), "Real CreateCraft failed: " + failure);
        IApparelWorkOrderQuery query = container.Resolve<IApparelWorkOrderQuery>();
        IApparelWorkOrderPersistence persistence = container.Resolve<IApparelWorkOrderPersistence>();
        IWorldItemStackRuntime items = container.Resolve<IWorldItemStackRuntime>();
        string beforeOrders = string.Join("\n", persistence.CaptureOrders().Select(value => JsonUtility.ToJson(value)));
        string beforeItems = JsonUtility.ToJson(items.Capture());
        int version = query.Version;
        CraftQualityAttemptEstimate estimate = query.CaptureQualityEstimate(orderId);
        Require(estimate.IsAvailable && estimate.MaximumAttempts == 3, "Created order has no estimate: " + estimate.Condition);
        Require(estimate.WorkPerAttempt > 0 && estimate.MaximumGrossMaterial == required * 3L,
            "Live estimate did not use authored order work/materials.");
        for (int i = 0; i < 10; i++)
            Require(query.CaptureQualityEstimate(orderId).SuccessProbability == estimate.SuccessProbability, "Read-only estimate drifted.");
        panel = new GameObject("WIM056 Readonly Panel", typeof(RectTransform), typeof(Canvas));
        IApparelBuildingPanelPresenter presenter = container.Resolve<IApparelBuildingPanelPresenter>();
        TMP_FontAsset font = container.Resolve<ITmpKoreanFontProvider>().GetRequiredFont();
        Require(font != null, "Project Korean UI font missing.");
        presenter.Render(panel.transform, facility, font, _ => { }, () => { });
        Canvas.ForceUpdateCanvases();
        TMP_Text label = panel.GetComponentsInChildren<TMP_Text>(true).Single(value => value.name == "ApparelQualityEstimate");
        Require(label.text == GameplayUiPresentationText.QualityEstimate(estimate), "Actual panel did not render its authority query.");
        Require(query.Version == version
            && beforeOrders == string.Join("\n", persistence.CaptureOrders().Select(value => JsonUtility.ToJson(value)))
            && beforeItems == JsonUtility.ToJson(items.Capture()), "Preview/render changed order, hidden quality roll or physical stock.");
        var restoredOrders = persistence.CaptureOrders().Select(value =>
            JsonUtility.FromJson<ApparelWorkOrderSaveData>(JsonUtility.ToJson(value))).ToArray();
        var restoredTerminals = persistence.CaptureTerminalStates().Select(value =>
            JsonUtility.FromJson<ApparelWorkOrderTerminalStateSaveData>(JsonUtility.ToJson(value))).ToArray();
        persistence.PublishRestoreState(persistence.PrepareRestoreState(restoredOrders, restoredTerminals));
        // The existing restore contract resets transient scheduling, not quality or physical ownership.
        foreach (var value in restoredOrders)
        {
            value.state = value.repairCommitPhase == ApparelRepairCommitPhase.None
                ? ApparelWorkOrderState.NeedsRevalidation : ApparelWorkOrderState.WaitingForDispositionFinalization;
            value.nextRetryGameHour = 0f;
        }
        string expectedRestoredOrders = string.Join("\n", restoredOrders.Select(value => JsonUtility.ToJson(value)));
        string actualRestoredOrders = string.Join("\n", persistence.CaptureOrders().Select(value => JsonUtility.ToJson(value)));
        Require(expectedRestoredOrders == actualRestoredOrders,
            "Apparel domain restore changed order fields. expected=" + expectedRestoredOrders + " actual=" + actualRestoredOrders);
        var stockBeforeRestore = JsonUtility.FromJson<DungeonPhysicalItemSaveData>(beforeItems);
        var stockAfterRestore = items.Capture();
        Require(string.Join("\n", stockBeforeRestore.stacks.Select(value => JsonUtility.ToJson(value)))
                == string.Join("\n", stockAfterRestore.stacks.Select(value => JsonUtility.ToJson(value)))
            && string.Join("\n", stockBeforeRestore.uniqueItems.Select(value => JsonUtility.ToJson(value)))
                == string.Join("\n", stockAfterRestore.uniqueItems.Select(value => JsonUtility.ToJson(value))),
            "Apparel domain restore changed physical stacks or unique instances.");
        Require(query.CaptureQualityEstimate(orderId).SuccessProbability == estimate.SuccessProbability,
            "Apparel domain restore changed estimate: before=" + estimate.SuccessProbability
                + ";after=" + query.CaptureQualityEstimate(orderId).SuccessProbability
                + ";condition=" + query.CaptureQualityEstimate(orderId).Condition);
        Require(estimate.Condition.Contains("단독 새 시도"), "Preview must not promise current mixed-work results.");
        Require(transfer.TrySpawnItem(material.PhysicalItemId, required * 2, facility.centerPos,
            WorldItemStackState.Loose, string.Empty, out int extra) && extra == required * 2,
            "Special-condition fixture physical stock missing.");
        var noWorker = new WorkerSelectionPolicySaveData { mode = WorkerSelectionMode.SpecificCharacters };
        Require(command.CreateCraft(new ApparelCraftOrderRequest(definition.ApparelId,
            ApparelSizeClass.Medium, ApparelModificationKind.None, ApparelMaterialSelectionPolicy.ExactMaterial,
            material.MaterialId, workerPolicy: noWorker), out string noWorkerOrder, out failure),
            "No-worker order setup failed: " + failure);
        Require(!query.CaptureQualityEstimate(noWorkerOrder).IsAvailable
            && query.Orders.Single(value => value.orderId == noWorkerOrder).state == ApparelWorkOrderState.WaitingForEligibleWorker,
            "No-worker condition must be unavailable, not zero-probability failure.");
        CharacterActor ordinaryWorker = container.Resolve<ICharacterWorldQuery>().Characters.First(value =>
            value?.Progression != null && !value.Progression.ResolveSelectedTraits().Any(trait =>
                trait != null && trait.identityRules?.OfType<ExtremeCraftInspirationRule>().Any() == true));
        var ordinaryOnly = new WorkerSelectionPolicySaveData { mode = WorkerSelectionMode.SpecificCharacters };
        ordinaryOnly.specificCharacterIds.Add(ordinaryWorker.Identity.PersistentId);
        Require(command.CreateCraft(new ApparelCraftOrderRequest(definition.ApparelId,
            ApparelSizeClass.Medium, ApparelModificationKind.None, ApparelMaterialSelectionPolicy.ExactMaterial,
            material.MaterialId, minimumCraftsmanshipQuality: CraftsmanshipQualityTier.Mythic,
            workerPolicy: ordinaryOnly), out string impossibleOrder, out failure),
            "Impossible-target order setup failed: " + failure);
        CraftQualityAttemptEstimate impossible = query.CaptureQualityEstimate(impossibleOrder);
        Require(impossible.IsAvailable && impossible.SuccessProbability == 0d
            && query.Orders.Single(value => value.orderId == impossibleOrder).state == ApparelWorkOrderState.TargetCurrentlyUnreachable
            && GameplayUiPresentationText.QualityEstimate(impossible).Contains("도달 불가"),
            "Real target-unreachable state disagrees with estimated probability/text.");
        Write("PASS\nmain-gameplay-container=PASS\nreal-create-craft=" + definition.ApparelId
            + "\nphysical-source-material=" + material.PhysicalItemId + ";quantity=" + required
            + "\nprobability=" + estimate.SuccessProbability.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
            + "\nactual-panel-label=PASS\n10-preview-and-render-order-stock-roll-invariant=PASS"
            + "\napparel-domain-serialized-restore-estimate-roll-stock=PASS"
            + "\napparel-restore-transient-state-and-lease-release=EXISTING_CONTRACT"
            + "\nreal-no-worker-versus-mythic-unreachable=PASS\nmixed-work-preview-single-new-attempt-label=PASS"
            + "\nfull-world-save-restore=NOT_RUN\n");
        Debug.Log("WIM056 live apparel estimate PASS: " + ReportPath);
    }

    private static BuildableObject Place(IObjectResolver container, BuildingSO definition, Vector2Int? at = null)
    {
        Require(container.Resolve<IGridSystemProvider>().TryGetGrid(out Grid grid), "Live grid missing.");
        Vector2Int position = at ?? grid.GetCells().Where(cell => cell != null).Select(cell => cell.Position)
            .OrderBy(value => value.y).ThenBy(value => value.x).First(candidate => definition.GetGridPosList(candidate)
                .All(value => grid.GetGridCell(value) is GridCell cell
                    && cell.AreaType != GridCellAreaType.BlockedExterior && cell.CanOccupy(definition.layer)));
        BuildableObject building = container.Resolve<IGridBuildingObjectFactory>().Create(grid, definition, position);
        foreach (MonoBehaviour component in building.GetComponentsInChildren<MonoBehaviour>(true))
            if (component != null) container.Inject(component);
        building.SetGrid(grid);
        building.Initialization(definition, position);
        Require(grid.RegisterOccupant(building, definition.layer, definition.GetGridPosList(position),
            definition.Placement.IsMovement), "Fixture grid registration failed.");
        return building;
    }

    private static void RunCombat(IObjectResolver container)
    {
        ICombatEquipmentRuntime equipment = container.Resolve<ICombatEquipmentRuntime>();
        BuildingSO[] definitions = AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>).ToArray();
        var candidate = definitions.SelectMany(building => equipment.Definitions.Where(definition =>
                building.GetAbility<BuildingEquipmentCraftingAbility>()?.CraftableEquipmentIds.Contains(definition.EquipmentId) == true
                && equipment.IsDefinitionUnlocked(definition.EquipmentId, out _))
            .Select(definition => (building, definition))).First();
        BuildableObject craftFacility = Place(container, candidate.building);
        var materialCatalog = container.Resolve<IResourceEconomyContentCatalog>();
        Require(materialCatalog.TryGetMaterial(candidate.definition.DefaultMaterialId, out var material),
            "Equipment fixture default material missing.");
        Require(CombatCraftConcreteInputProjection.TryCapture(candidate.definition,
            candidate.definition.EquipmentId, material, out var concreteInputs, out string inputFailure),
            "Equipment fixture BOM projection failed: " + inputFailure);
        var transfer = container.Resolve<IItemTransferService>();
        foreach (var input in concreteInputs.Inputs)
            Require(transfer.TrySpawnItem(input.ItemId, input.Amount, craftFacility.centerPos,
                WorldItemStackState.Loose, string.Empty, out int spawned) && spawned == input.Amount,
                "Equipment fixture physical input failed: " + input.ItemId);
        Require(equipment.TryQueueCraft(candidate.definition.EquipmentId, material.MaterialId,
                craftFacility, out string failure),
            "Actual equipment queue failed: " + failure);
        CombatEquipmentCraftOrderSaveData order = equipment.CraftQueue.Last();
        Require(equipment.SetCraftQualityTarget(order.orderId, CraftsmanshipQualityTier.Good,
            RejectedOutputDisposition.KeepInStorage, QualityRepeatLimitMode.SafeLimits, 3, 0f, 1, out failure),
            "Actual equipment quality target failed: " + failure);
        string before = JsonUtility.ToJson(order);
        IWorldItemStackRuntime items = container.Resolve<IWorldItemStackRuntime>();
        string beforeItems = JsonUtility.ToJson(items.Capture());
        CraftQualityAttemptEstimate estimate = equipment.CaptureCraftQualityEstimate(order.orderId);
        Require(estimate.IsAvailable && estimate.MaximumAttempts == 3 && estimate.WorkPerAttempt > 0,
            "Equipment estimate unavailable: " + estimate.Condition);
        GameObject equipmentPanel = new("WIM056 Equipment Panel", typeof(RectTransform), typeof(Canvas));
        container.Resolve<IEquipmentCraftingPanelPresenter>().Render(equipmentPanel.transform, craftFacility,
            container.Resolve<ITmpKoreanFontProvider>().GetRequiredFont(), _ => { }, () => { });
        Canvas.ForceUpdateCanvases();
        Require(equipmentPanel.GetComponentsInChildren<TMP_Text>(true).Any(text =>
            text.text == GameplayUiPresentationText.QualityEstimate(estimate)), "Actual equipment label missing.");
        Require(before == JsonUtility.ToJson(order) && beforeItems == JsonUtility.ToJson(items.Capture()),
            "Equipment preview changed order/roll/stock.");
        File.AppendAllText(ReportPath, "real-equipment-queue=" + candidate.definition.EquipmentId
            + "\nequipment-target-query-panel-order-stock-roll-invariant=PASS\n",
            new System.Text.UTF8Encoding(false));
    }

    private static void RunConstruction(IObjectResolver container)
    {
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Modular/D03_조리손질대.asset");
        Require(definition != null, "Construction fixture definition missing.");
        Require(container.Resolve<IGridSystemProvider>().TryGetGrid(out Grid grid), "Construction grid missing.");
        Vector2Int position = grid.GetCells().Where(cell => cell != null).Select(cell => cell.Position)
            .OrderBy(value => value.y).ThenBy(value => value.x).First(anchor =>
                definition.GetGridPosList(anchor).All(cell => grid.GetGridCell(cell) is GridCell target
                    && target.CanBuildInArea(definition) && target.CanOccupy(definition.layer)
                    && target.CanOccupy(GridLayer.Construction)));
        var transfer = container.Resolve<IItemTransferService>();
        foreach (var material in definition.GetConstructionMaterials())
            Require(transfer.TrySpawnItem(material.ItemId, material.Amount, position,
                WorldItemStackState.Loose, string.Empty, out int spawned) && spawned == material.Amount,
                "Construction physical BOM missing: " + material.ItemId);
        Require(container.Resolve<IDungeonGridBuildingControllerProvider>().Controller.TryPlaceConstructionSite(
            definition, position, out string placementFailure), "Construction placement failed: " + placementFailure);
        ConstructionSite site = UnityEngine.Object.FindObjectsByType<ConstructionSite>(FindObjectsSortMode.None)
            .Single(value => value.centerPos == position && value.id == definition.id);
        var orders = container.Resolve<IWorkOrderRuntime>();
        Require(orders.TryGetOrderFor(site, BuiltInWorkTypeIds.Construct, out var order), "Construction order missing.");
        var commands = container.Resolve<IQualityTargetPipelineCommand>();
        Require(commands.CreateForWorkOrder(order.WorkOrderId, new QualityTargetPipelineSaveData
        {
            definitionId = definition.ContentDefinitionId,
            minimumQuality = CraftsmanshipQualityTier.Good,
            maximumAttempts = 3,
            rejectedDisposition = RejectedOutputDisposition.DismantleFacilityAndRetry,
            limitMode = QualityRepeatLimitMode.SafeLimits
        }, out string pipelineId, out DomainFailure failure), "Construction target failed: " + failure);
        var query = container.Resolve<IQualityTargetPipelineQuery>();
        var estimate = query.CaptureQualityEstimate(pipelineId);
        Require(estimate.IsAvailable && estimate.MaximumAttempts == 3
            && estimate.WorkPerAttempt == order.RequiredWork, "Construction estimate unavailable or wrong cost.");
        var items = container.Resolve<IWorldItemStackRuntime>();
        string beforeItems = JsonUtility.ToJson(items.Capture());
        string beforeOrders = JsonUtility.ToJson(orders.Capture());
        UIBuildingInfo view = UnityEngine.Object.FindObjectsByType<UIBuildingInfo>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).First();
        view.DisplayBuildingInfo(site);
        Canvas.ForceUpdateCanvases();
        TMP_Text label = view.GetComponentsInChildren<TMP_Text>(true)
            .Single(value => value.name == "BuildingConstructionQualityEstimate");
        Require(label.text == GameplayUiPresentationText.QualityEstimate(estimate), "Construction estimate label mismatch.");
        label.ForceMeshUpdate(true);
        Require(label.rectTransform.rect.height + 1f >= label.preferredHeight,
            "Construction estimate clipped: height=" + label.rectTransform.rect.height + ";required=" + label.preferredHeight);
        Require(beforeOrders == JsonUtility.ToJson(orders.Capture()) && beforeItems == JsonUtility.ToJson(items.Capture()),
            "Construction preview changed orders/hidden roll/stock.");
        File.AppendAllText(ReportPath, "construction-real-placement-query-panel=PASS\nconstruction-multiline-layout=PASS"
            + "\nconstruction-preview-order-roll-stock-invariant=PASS\n", new System.Text.UTF8Encoding(false));
    }

    private static void RunSharedQuality(IObjectResolver container)
    {
        var catalog = container.Resolve<IResourceEconomyContentCatalog>();
        var bridge = container.Resolve<IProductionAssemblyBridge>();
        var registry = container.Resolve<IProductionOutputCapabilityRegistry>();
        var definition = facility.BuildingData;
        var recipe = catalog.Recipes.OrderBy(value => value.RecipeId, StringComparer.Ordinal).First(value =>
            value.WorkstationTag == definition.GetProductionWorkstationAbility().WorkstationTag
            && value.CaptureCanonicalOutputs().Where(output => ProductionOutputRoleRules.IsPhysical(output.Role))
                .Any(output =>
                registry.TryValidateExact(bridge.CaptureOutputCapability(output.OutputLineId, output.ItemId),
                    out var capability, out _) && capability is IProductionDeterministicCraftQualityCapability));
        // Fixture-only checkpoint setup, not evidence of natural research progression.
        if (!string.IsNullOrWhiteSpace(recipe.RequiredResearchId))
            container.Resolve<ProgressionSceneRuntimeReferences>().BlueprintResearch.State.Projects.RestoreCompleted(
                new ResearchProjectId(recipe.RequiredResearchId));
        var commands = container.Resolve<IProductionBillOrderCommand>();
        var query = container.Resolve<IProductionBillQuery>();
        var added = commands.AddBill(facility, recipe.RecipeId, ProductionOrderMode.RepeatCount, 1);
        Require(added.Succeeded, "Live general recipe order failed: " + added.Failure);
        naturalRecipe = recipe;
        naturalBill = added.BillId;
        var presenter = container.Resolve<IProductionBuildingPanelPresenter>();
        panel = new GameObject("WIM006 Shared Quality Panel", typeof(RectTransform), typeof(Canvas));
        presenter.Render(panel.transform, facility, container.Resolve<ITmpKoreanFontProvider>().GetRequiredFont(),
            _ => { }, () => { });
        Canvas.ForceUpdateCanvases();
        var button = panel.GetComponentsInChildren<Button>(true).Single(value =>
            value.name.StartsWith("ProductionMinimumQuality_", StringComparison.Ordinal));
        Require(EventSystem.current != null, "Main EventSystem missing.");
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        Require(query.GetBills(facility).Single().MinimumCraftQuality == 0,
            "Actual quality button did not publish its shared order command.");
        Require(commands.SetMinimumCraftQuality(added.BillId, 7).Succeeded, "Could not set high quality target.");
        var bill = query.GetBills(facility).Single();
        Require(bill.Status == ProductionBillStatus.WaitingForQuality
            && bill.BlockedFailure.Code == FailureCode.QualityTargetUnreachable,
            "Automatic mode did not expose target-unreachable status.");
        var items = container.Resolve<IWorldItemStackRuntime>();
        string before = JsonUtility.ToJson(items.Capture());
        var started = container.Resolve<IProductionBillWorkExecution>().BeginWork(null, facility, recipe.WorkTypeId);
        Require(!started.Succeeded && started.Failure.Code == FailureCode.QualityTargetUnreachable
            && before == JsonUtility.ToJson(items.Capture()), "Automatic start bypassed target or changed physical stock.");
        Require(commands.SetMinimumCraftQuality(added.BillId, bill.CurrentAutomaticCraftQuality).Succeeded,
            "Could not select attainable automatic quality.");
        naturalTarget = bill.CurrentAutomaticCraftQuality;
        Require(query.GetBills(facility).Single().Status != ProductionBillStatus.WaitingForQuality,
            "Attainable target did not clear the quality gate.");
        Write("PASS\nshared-general-recipe=" + recipe.RecipeId
            + "\nactual-automatic-mode-command=PASS\nmain-EventSystem-quality-button=PASS"
            + "\nunreachable-start-physical-stock-invariant=PASS\nattainable-target-resumes=PASS"
            + "\nresearch-checkpoint=FIXTURE_ONLY\nnatural-powered-output=NOT_RUN\n");
    }

    private static bool SupportsSharedQuality(IObjectResolver container, BuildingSO definition)
    {
        if (definition.GetAbility<BuildingAutomationAbility>()?.maximumMode != AutomationMode.Automatic)
            return false;
        var registry = container.Resolve<IProductionOutputCapabilityRegistry>();
        var bridge = container.Resolve<IProductionAssemblyBridge>();
        return container.Resolve<IResourceEconomyContentCatalog>().Recipes.Any(recipe =>
            recipe.WorkstationTag == definition.GetProductionWorkstationAbility()?.WorkstationTag
            && recipe.CaptureCanonicalOutputs().Where(output => ProductionOutputRoleRules.IsPhysical(output.Role))
                .Any(output => registry.TryValidateExact(bridge.CaptureOutputCapability(output.OutputLineId, output.ItemId),
                    out var capability, out _) && capability is IProductionDeterministicCraftQualityCapability));
    }

    private static void PrepareNaturalQuality(IObjectResolver container)
    {
        BuildingSO producer = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Industrial/I03_마나_발전기.asset");
        BuildingSO duct = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Industrial/U04_통합_기반_덕트.asset");
        Require(producer != null && duct != null, "Authored generator/duct fixture missing.");
        generator = Place(container, producer);
        Require(container.Resolve<IGridSystemProvider>().TryGetGrid(out Grid grid), "Live grid missing.");
        // A minimal rectangular utility route joins the two actual authored footprints.
        int minX = Math.Min(generator.centerPos.x, facility.centerPos.x);
        int maxX = Math.Max(generator.centerPos.x, facility.centerPos.x);
        int minY = Math.Min(generator.centerPos.y, facility.centerPos.y);
        int maxY = Math.Max(generator.centerPos.y, facility.centerPos.y);
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
        {
            Vector2Int cell = new(x, y);
            Require(grid.GetGridCell(cell) != null, "Utility fixture route leaves the grid.");
            if (grid.GetGridCell(cell).CanOccupy(duct.layer)) Place(container, duct, cell);
        }
        Vector2Int source = grid.GetCells().Where(cell => cell != null && grid.IsWalkable(cell.Position))
            .Select(cell => cell.Position).OrderBy(cell => (cell - facility.centerPos).sqrMagnitude)
            .ThenBy(cell => cell.y).ThenBy(cell => cell.x).First();
        var transfer = container.Resolve<IItemTransferService>();
        foreach (var input in naturalRecipe.Inputs)
            Require(transfer.TrySpawnItem(input.ItemId, input.Amount, source, WorldItemStackState.Loose,
                string.Empty, out int count) && count == input.Amount, "Natural production input spawn failed.");
        string fuelId = producer.GetAbility<BuildingPowerProducerAbility>().fuelItemId;
        Require(transfer.TrySpawnItem(fuelId, 2, source, WorldItemStackState.Loose,
            string.Empty, out int fuel) && fuel == 2, "Natural generator physical fuel spawn failed.");
        initialOutputCount = container.Resolve<IWorldItemStackRuntime>().GetAllStacks()
            .Where(stack => stack.ItemId == naturalRecipe.Outputs[0].ItemId).Sum(stack => stack.Quantity);
        observedPowered = false;
        observedWork = 0f;
        deadline = EditorApplication.timeSinceStartup + 180d;
        nextDiagnostic = EditorApplication.timeSinceStartup;
        Time.timeScale = 1f;
        Write("RUNNING\nnatural-production-setup=real-loose-inputs+fuel+generator+duct+automatic-order\n");
    }

    private static void TickNaturalQuality(IObjectResolver container)
    {
        var items = container.Resolve<IWorldItemStackRuntime>();
        var bill = container.Resolve<IProductionBillQuery>().GetBills(facility)
            .FirstOrDefault(value => value.BillId == naturalBill);
        bool powered = container.Resolve<IPowerInfrastructureQuery>().IsPowered(facility);
        observedPowered |= powered;
        if (bill != null)
        {
            observedWork = Math.Max(observedWork, bill.CompletedWork);
            Require(string.IsNullOrEmpty(bill.ReservedWorkerId), "Natural automatic order acquired a manual worker.");
        }
        var output = items.GetAllStacks().Where(stack => stack.ItemId == naturalRecipe.Outputs[0].ItemId).ToArray();
        int produced = output.Sum(stack => stack.Quantity) - initialOutputCount;
        if (produced > 0)
        {
            Require(observedPowered && observedWork > 0f, "Output appeared without observed automatic power/work.");
            Require(produced == naturalRecipe.Outputs[0].Amount, "Natural one-cycle output count mismatch.");
            foreach (var stack in output)
            {
                Require(ApparelItemStateCodec.TryRead(stack.Components, out ApparelInstanceState apparel),
                    "Natural physical apparel grade missing.");
                Require((int)apparel.craftsmanshipQuality >= naturalTarget
                    && apparel.craftsmanshipQuality <= DeterministicCraftQualityResolver.FromScore(
                        facility.BuildingData.GetAbility<BuildingAutomationAbility>().automaticQualityCap * 100f),
                    "Natural output violated selected target/automatic cap.");
            }
            Write("PASS\nrecipe=" + naturalRecipe.RecipeId + "\nphysical-output-count=" + produced
                + "\nreal-loose-input-haul-and-fuelled-power=PASS\nnatural-AutomationRuntime-work=" + observedWork
                + "\nmanual-worker-contribution=0\nphysical-apparel-target-and-cap=PASS"
                + "\nresearch-and-buildings=FIXTURE_CHECKPOINT\noriginal-scene-save=NONE\n");
            Finish();
            return;
        }
        if (EditorApplication.timeSinceStartup >= nextDiagnostic)
        {
            nextDiagnostic = EditorApplication.timeSinceStartup + 15d;
            Write("RUNNING\npowered=" + powered + ";status=" + bill?.Status + ";work=" + bill?.CompletedWork
                + ";failure=" + bill?.BlockedFailure + ";clock=" + container.Resolve<IGameClock>().Time
                + "\nloose=" + string.Join(",", items.GetAllStacks().Where(stack => stack.State == WorldItemStackState.Loose)
                    .Select(stack => stack.ItemId + ":" + stack.Quantity)) + "\n");
        }
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Pending, false);
        // Exiting Play Mode discards all fixture stock, orders and buildings together.
        // Never destroy a stocked facility while live orders still own its buffers.
        Time.timeScale = originalScale;
        EditorApplication.ExitPlaymode();
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Write(string text)
    {
        string path = SessionState.GetBool(NaturalQuality, false)
            ? "Artifacts/QA/wim-implementation/wim-006-natural-quality.txt"
            : SessionState.GetBool(SharedQuality, false)
            ? "Artifacts/QA/wim-implementation/wim-006-shared-quality-live.txt" : ReportPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
    }
}
#endif
