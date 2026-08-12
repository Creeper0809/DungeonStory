#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

[InitializeOnLoad]
public static class ProductionBuildingPlayModeVerifier
{
    public const string RequestPath =
        "Temp/production-ui-pointer-matrix.request";
    public const string ReportPath =
        "Artifacts/QA/production-ui-pointer-matrix-report.txt";
    public const string DesktopCapturePath =
        "Artifacts/QA/production-branches-1600x900.png";
    public const string PortraitCapturePath =
        "Artifacts/QA/production-branches-900x1600.png";

    private static bool runnerCreated;

    static ProductionBuildingPlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem(
        "DungeonStory/Debug/Production/Request Production UI Pointer Matrix")]
    public static void RequestRunFromMenu()
    {
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ReportPath);
        File.Delete(DesktopCapturePath);
        File.Delete(PortraitCapturePath);
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
    }

    private static void OnEditorUpdate()
    {
        if (File.Exists(RequestPath)
            && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.EnterPlaymode();
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            runnerCreated = false;
            return;
        }

        if (change != PlayModeStateChange.EnteredPlayMode
            || runnerCreated
            || !File.Exists(RequestPath))
        {
            return;
        }

        runnerCreated = true;
        new GameObject("Production UI Pointer Matrix Runner")
            .AddComponent<ProductionBuildingPlayModeVerificationRunner>();
    }
}

public sealed class ProductionBuildingPlayModeVerificationRunner : MonoBehaviour
{
    private const float ResolveTimeoutSeconds = 20f;

    private readonly List<string> report = new();
    private readonly List<string> failures = new();
    private readonly List<string> errors = new();
    private readonly List<string> warnings = new();

    private DungeonRuntimeLifetimeScope scope;
    private Grid grid;
    private IGridBuildingObjectFactory buildingFactory;
    private IGameContentCatalog content;
    private IResourceEconomyContentCatalog catalog;
    private IProductionDependencyCatalog dependencies;
    private IProductionBillQuery billQuery;
    private IProductionBillOrderCommand billCommands;
    private IProductionBillPersistence billPersistence;
    private IProductionStockSensorRuntime stockSensors;
    private IProductionOutputPlanningService outputPlanning;
    private IProductionAssemblyBridge productionBridge;
    private IProductionItemGateway productionItems;
    private IItemTransferService itemTransfers;
    private IStockQuery stock;
    private IWorldItemStackRuntime worldItems;
    private BlueprintResearchRuntime research;
    private IDungeonSaveSection researchSaveSection;
    private UIBuildingInfo buildingInfo;
    private GameManager gameManager;

    private DungeonProductionBillSaveData originalBills;
    private DungeonPhysicalItemSaveData originalWorldItems;
    private string originalResearchJson = string.Empty;
    private BuildableObject activeFacility;
    private int originalResolutionIndex = -1;
    private float originalTimeScale;
    private bool originalPause;
    private Exception verificationException;

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        Application.logMessageReceived += CaptureLog;
        originalResolutionIndex = GameViewResolutionController.SelectedSizeIndex;

        yield return ExecuteGuarded(RunVerification());
        CleanupScenario();
        Finish();
    }

    private IEnumerator RunVerification()
    {
        yield return CompleteOwnerSelectionIfVisible();
        yield return StartPartyPlayModeTestDriver.CompleteIfVisible(45f);
        yield return RefreshFrames();
        yield return ResolveRuntime();
        CaptureOriginalState();
        yield return RunResolutionScenario(
            1600,
            900,
            "DESKTOP",
            ProductionBuildingPlayModeVerifier.DesktopCapturePath);
        CleanupScenario();
        yield return RunResolutionScenario(
            900,
            1600,
            "PORTRAIT",
            ProductionBuildingPlayModeVerifier.PortraitCapturePath);
    }

    private IEnumerator CompleteOwnerSelectionIfVisible()
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        Button owner = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            owner = Resources.FindObjectsOfTypeAll<Button>()
                .Where(button => button != null
                    && button.gameObject.scene.IsValid()
                    && button.gameObject.activeInHierarchy
                    && button.interactable
                    && button.name.StartsWith(
                        "OwnerOption_",
                        StringComparison.Ordinal))
                .OrderBy(button => button.name, StringComparer.Ordinal)
                .FirstOrDefault();
            if (owner != null
                || FindButton("StartPartyConfirm") != null
                || FindButton("PreparationStartRunButton") != null)
            {
                break;
            }
            yield return null;
        }

        if (owner != null)
        {
            yield return Click(owner, "OWNER_SELECTION_POINTER");
        }
    }

    private IEnumerator ResolveRuntime()
    {
        float deadline = Time.realtimeSinceStartup + ResolveTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = FindObjectsByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate =>
                    candidate != null && candidate.Container != null);
            if (scope?.Container != null)
            {
                break;
            }
            yield return null;
        }

        Require(scope?.Container != null,
            "DungeonRuntimeLifetimeScope was not available.");
        IObjectResolver container = scope.Container;
        IGridSystemProvider gridProvider =
            container.Resolve<IGridSystemProvider>();
        Require(gridProvider.TryGetGrid(out grid) && grid != null,
            "The live gameplay grid was not available.");

        buildingFactory = container.Resolve<IGridBuildingObjectFactory>();
        content = container.Resolve<IGameContentCatalog>();
        catalog = container.Resolve<IResourceEconomyContentCatalog>();
        dependencies = container.Resolve<IProductionDependencyCatalog>();
        billQuery = container.Resolve<IProductionBillQuery>();
        billCommands = container.Resolve<IProductionBillOrderCommand>();
        billPersistence = container.Resolve<IProductionBillPersistence>();
        stockSensors = container.Resolve<IProductionStockSensorRuntime>();
        outputPlanning = container.Resolve<IProductionOutputPlanningService>();
        productionBridge = container.Resolve<IProductionAssemblyBridge>();
        productionItems = container.Resolve<IProductionItemGateway>();
        itemTransfers = container.Resolve<IItemTransferService>();
        stock = container.Resolve<IStockQuery>();
        worldItems = container.Resolve<IWorldItemStackRuntime>();
        research = container.Resolve<ProgressionSceneRuntimeReferences>()
            .BlueprintResearch;
        researchSaveSection = container.Resolve<IDungeonSaveSectionRegistry>()
            .OrderedSections
            .Single(section => string.Equals(
                section.SectionId,
                BlueprintResearchSaveSection.Id,
                StringComparison.Ordinal));
        buildingInfo = Resources.FindObjectsOfTypeAll<UIBuildingInfo>()
            .FirstOrDefault(candidate =>
                candidate != null && candidate.gameObject.scene.IsValid());
        gameManager = FindFirstObjectByType<GameManager>();

        Require(research != null, "Blueprint research runtime was not loaded.");
        Require(buildingInfo != null, "UIBuildingInfo was not found in the live scene.");
        Require(EventSystem.current != null, "The live EventSystem was not available.");
        Require(productionItems is IProductionOutputBufferGateway,
            "Production output buffer gateway was not registered.");
    }

    private void CaptureOriginalState()
    {
        originalBills = billPersistence.Capture();
        originalWorldItems = worldItems.Capture();
        originalResearchJson = researchSaveSection.Capture();
        originalTimeScale = Time.timeScale;
        originalPause = gameManager != null && gameManager.isPause;
        Time.timeScale = 0f;
        if (gameManager != null)
        {
            gameManager.isPause = true;
        }
    }

    private IEnumerator RunResolutionScenario(
        int width,
        int height,
        string suffix,
        string capturePath)
    {
        yield return SelectResolution(width, height, suffix);
        (ProductionRecipeSO recipe, BuildingSO building) =
            SelectScenarioContent();
        if (!string.IsNullOrWhiteSpace(recipe.RequiredResearchId)
            && !research.State.Projects.IsCompleted(
                new ResearchProjectId(recipe.RequiredResearchId)))
        {
            research.State.Projects.RestoreCompleted(
                new ResearchProjectId(recipe.RequiredResearchId));
        }
        activeFacility = PlaceFacility(building);

        ProductionBillCommandResult add = billCommands.AddBill(
            activeFacility,
            recipe.RecipeId,
            ProductionOrderMode.RepeatForever,
            0);
        Check(add.Succeeded,
            suffix + "_ORDER_CREATED",
            $"recipe={recipe.RecipeId}; outcome={add.Outcome}; failure={add.Failure.Code}");
        Require(add.Succeeded, "Could not create the production verification order.");
        ProductionBillId billId = add.BillId;

        IReadOnlyList<ProductionConsumerLink> expectedConsumers = recipe.Outputs
            .Where(output => output != null)
            .SelectMany(output => dependencies.GetConsumers(output.ItemId))
            .Where(link => link != null && link.IsRealConsumer)
            .GroupBy(link => link.consumerId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        buildingInfo.DisplayBuildingInfo(activeFacility);
        yield return RefreshFrames();
        VerifyBranchSurface(expectedConsumers, suffix, beforeSensor: true);

        ProductionBillSnapshot before = RequireBill(activeFacility, billId);
        Button targetStock = FindButton("ProductionTargetStockTab_0");
        Check(targetStock != null
                && ButtonLabel(targetStock).Contains("감지반 설치", StringComparison.Ordinal),
            suffix + "_TARGET_STOCK_LOCKED",
            targetStock == null ? "button missing" : ButtonLabel(targetStock));
        Check(before.Mode == ProductionOrderMode.RepeatForever
                && !before.HasStockSensor,
            suffix + "_PRE_INSTALL_ORDER",
            $"id={before.BillId.Value}; mode={before.Mode}; sensor={before.HasStockSensor}");

        string sensorItemId = activeFacility.BuildingData
            .GetProductionWorkstationAbility()
            .StockSensorInstallationItemId;
        Check(itemTransfers.TrySpawnItem(
                sensorItemId,
                1,
                activeFacility.centerPos,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawnedSensor)
            && spawnedSensor == 1,
            suffix + "_SENSOR_PHYSICAL_ITEM",
            $"item={sensorItemId}; spawned={spawnedSensor}");

        yield return Click(targetStock, suffix + "_SENSOR_INSTALL_POINTER");
        ProductionBillSnapshot requested = RequireBill(activeFacility, billId);
        Check(requested.BillId == before.BillId
                && requested.Mode == ProductionOrderMode.RepeatForever,
            suffix + "_INSTALL_REQUEST_PRESERVES_ORDER",
            $"id={requested.BillId.Value}; mode={requested.Mode}");

        string sensorDestination = "production-sensor:"
            + activeFacility.RequirePersistentInstanceId().Value;
        WorldItemStackSnapshot reservedSensor = stock.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && string.Equals(stack.ItemId, sensorItemId, StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    sensorDestination,
                    StringComparison.Ordinal));
        Check(reservedSensor != null,
            suffix + "_SENSOR_DELIVERY_RESERVED",
            reservedSensor == null ? "reservation missing" : reservedSensor.StackId);
        Require(reservedSensor != null,
            "The stock sensor delivery reservation was not created.");

        string transitOwner = "qa-production-ui-" + suffix.ToLowerInvariant();
        bool beganTransit = itemTransfers.TryBeginTransit(
            (ItemStackId)reservedSensor.StackId,
            reservedSensor.Position,
            transitOwner,
            out _,
            out DomainFailure beginFailure);
        DomainFailure completeFailure = DomainFailure.None;
        bool completedTransit = beganTransit
            && itemTransfers.TryCompleteTransit(
                (ItemStackId)reservedSensor.StackId,
                transitOwner,
                WorldItemStackState.FacilityBuffer,
                activeFacility.centerPos,
                sensorDestination,
                out completeFailure);
        Check(completedTransit,
            suffix + "_SENSOR_PHYSICAL_DELIVERY",
            beganTransit
                ? completeFailure.Code.ToString()
                : beginFailure.Code.ToString());
        Require(completedTransit, "The stock sensor could not be physically delivered.");
        stockSensors.FinalizeDeliveredSensors();
        buildingInfo.DisplayBuildingInfo(activeFacility);
        yield return RefreshFrames();

        ProductionBillSnapshot installed = RequireBill(activeFacility, billId);
        Check(installed.HasStockSensor
                && installed.BillId == before.BillId
                && installed.Mode == ProductionOrderMode.RepeatForever,
            suffix + "_INSTALL_PRESERVES_EXISTING_ORDER",
            $"id={installed.BillId.Value}; mode={installed.Mode}; sensor={installed.HasStockSensor}");
        targetStock = FindButton("ProductionTargetStockTab_0");
        Check(targetStock != null
                && ButtonLabel(targetStock).Contains("목표 재고", StringComparison.Ordinal),
            suffix + "_TARGET_STOCK_UNLOCKED",
            targetStock == null ? "button missing" : ButtonLabel(targetStock));

        yield return Click(targetStock, suffix + "_TARGET_STOCK_POINTER");
        ProductionBillSnapshot targetMode = RequireBill(activeFacility, billId);
        Check(targetMode.Mode == ProductionOrderMode.MaintainStock
                && targetMode.TargetStock == 10,
            suffix + "_TARGET_STOCK_ENABLED",
            $"mode={targetMode.Mode}; target={targetMode.TargetStock}");

        yield return EditFirstRouteThroughPointers(billId, suffix);
        VerifyPhysicalOutputRoute(recipe, suffix);
        FillOutputBuffer(recipe, targetMode, suffix);
        buildingInfo.DisplayBuildingInfo(activeFacility);
        yield return RefreshFrames();

        ProductionBillSnapshot full = RequireBill(activeFacility, billId);
        TMP_Text progressLabel = FindSceneObject("ProductionBill_1")
            ?.transform.Find("Label")?.GetComponent<TMP_Text>();
        Check(full.Status == ProductionBillStatus.WaitingForOutputSpace
                && progressLabel != null
                && progressLabel.text.Contains(
                    "출력 공간 대기",
                    StringComparison.Ordinal),
            suffix + "_OUTPUT_FULL_STATUS",
            $"status={full.Status}; label={progressLabel?.text ?? "<missing>"}");
        VerifyBranchSurface(expectedConsumers, suffix, beforeSensor: false);

        Button lastRouteEditor = FindButton(
            $"ProductionRoutePriorityPlus_0_{expectedConsumers.Count - 1}");
        yield return BringIntoView(
            lastRouteEditor != null
                ? lastRouteEditor.transform as RectTransform
                : null);
        Canvas.ForceUpdateCanvases();
        VerifyCaptureLayout(expectedConsumers.Count, suffix);
        yield return Capture(capturePath, width, height, suffix + "_CAPTURE");
        report.Add($"{suffix}_ARTIFACT={capturePath}");
    }

    private IEnumerator EditFirstRouteThroughPointers(
        ProductionBillId billId,
        string suffix)
    {
        ProductionBillSnapshot initial = RequireBill(activeFacility, billId);
        ProductionConsumerRoutePolicy[] initialPolicies =
            ResolvePolicies(initial);
        Check(initialPolicies.Length >= 2,
            suffix + "_ROUTE_POLICY_COUNT",
            $"routes={initialPolicies.Length}");
        Require(initialPolicies.Length >= 2,
            "The selected intermediate item did not expose two route policies.");

        string consumerId = initialPolicies[0].consumerId;
        int initialPriority = initialPolicies[0].priority;
        int initialWeight = initialPolicies[0].weight;
        int initialReserve = initialPolicies[0].minimumReserve;

        yield return Click(
            FindButton("ProductionRoutePriorityPlus_0_0"),
            suffix + "_ROUTE_PRIORITY_POINTER");
        yield return Click(
            FindButton("ProductionRouteWeightPlus_0_0"),
            suffix + "_ROUTE_WEIGHT_POINTER");
        yield return Click(
            FindButton("ProductionRouteReservePlus_0_0"),
            suffix + "_ROUTE_RESERVE_POINTER");

        ProductionBillSnapshot edited = RequireBill(activeFacility, billId);
        ProductionConsumerRoutePolicy changed = edited.RoutePolicies
            .FirstOrDefault(policy => policy != null
                && string.Equals(
                    policy.consumerId,
                    consumerId,
                    StringComparison.Ordinal));
        Check(changed != null
                && changed.priority == initialPriority + 1
                && changed.weight == initialWeight + 1
                && changed.minimumReserve == initialReserve + 1,
            suffix + "_ROUTE_VALUES_EDITED",
            changed == null
                ? "route missing"
                : $"priority={changed.priority}; weight={changed.weight}; minimum={changed.minimumReserve}");
    }

    private void FillOutputBuffer(
        ProductionRecipeSO recipe,
        ProductionBillSnapshot bill,
        string suffix)
    {
        ProductionOutputDefinition output = recipe.Outputs
            .First(value => value != null && value.Amount > 0);
        int capacity = outputPlanning.ResolveCapacity(
            productionBridge.CaptureFacility(activeFacility),
            output.ItemId,
            output.Amount);
        IProductionOutputBufferGateway buffer =
            (IProductionOutputBufferGateway)productionItems;
        bool filled = buffer.SpawnBufferedOutput(
            output.ItemId,
            capacity,
            activeFacility.centerPos,
            bill.OutputDestinationId);
        Check(filled,
            suffix + "_OUTPUT_BUFFER_FILLED",
            $"item={output.ItemId}; quantity={capacity}; destination={bill.OutputDestinationId}");
    }

    private void VerifyPhysicalOutputRoute(
        ProductionRecipeSO recipe,
        string suffix)
    {
        ProductionOutputDefinition output = recipe.Outputs
            .First(value => value != null && value.Amount > 0);
        string sourceDestination =
            $"production-output-route-qa:{suffix.ToLowerInvariant()}";
        string targetDestination =
            $"production-route-qa:{suffix.ToLowerInvariant()}";
        IProductionOutputBufferGateway buffer =
            (IProductionOutputBufferGateway)productionItems;
        int amount = Mathf.Max(2, output.Amount);
        bool spawned = buffer.SpawnBufferedOutput(
            output.ItemId,
            amount,
            activeFacility.centerPos,
            sourceDestination);
        int moved = 0;
        DomainFailure failure = DomainFailure.None;
        bool routed = spawned && buffer.TryRouteBufferedOutput(
            sourceDestination,
            output.ItemId,
            amount - 1,
            activeFacility.centerPos,
            targetDestination,
            out moved,
            out failure);
        int sourceRemaining = buffer.CountBufferedOutput(
            output.ItemId,
            sourceDestination);
        int targetReserved = productionItems.CountPending(
            output.ItemId,
            targetDestination);
        Check(routed
                && moved == amount - 1
                && sourceRemaining == 1
                && targetReserved == amount - 1,
            suffix + "_PHYSICAL_OUTPUT_ROUTE",
            $"spawned={spawned}; routed={routed}; moved={moved}; "
                + $"source={sourceRemaining}; target={targetReserved}; "
                + $"failure={failure.Code}");
        buffer.ReleaseBufferedOutput(
            sourceDestination,
            activeFacility.centerPos);
        productionItems.ReleaseDestination(
            targetDestination,
            activeFacility.centerPos);
    }

    private void VerifyBranchSurface(
        IReadOnlyList<ProductionConsumerLink> expected,
        string suffix,
        bool beforeSensor)
    {
        GameObject[] routeRoots = FindSceneObjects("ProductionRoute_0_");
        string[] labels = routeRoots
            .Select(root => root.transform
                .Find("ProductionRouteLabel")
                ?.GetComponent<TMP_Text>()?.text ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        bool allConsumersVisible = expected.All(link => labels.Any(label =>
            label.Contains(
                string.IsNullOrWhiteSpace(link.displayName)
                    ? link.consumerId
                    : link.displayName,
                StringComparison.Ordinal)));
        bool allConsumerIdsVisible = expected.All(link => labels.Any(label =>
            label.Contains(
                $"[{link.consumerId}]",
                StringComparison.Ordinal)));
        bool allLiveStatesVisible = expected.All(link => labels.Any(label =>
        {
            string consumerMarker = $"[{link.consumerId}]";
            int markerIndex = label.IndexOf(
                consumerMarker,
                StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return false;
            }

            int statusSeparator = label.IndexOf(
                " | ",
                markerIndex + consumerMarker.Length,
                StringComparison.Ordinal);
            return statusSeparator >= 0
                && statusSeparator + 3 < label.Length;
        }));
        string phase = beforeSensor ? "BEFORE_SENSOR" : "AFTER_SENSOR";
        Check(routeRoots.Length >= 2
                && allConsumersVisible
                && allConsumerIdsVisible
                && allLiveStatesVisible,
            suffix + "_" + phase + "_BRANCH_CONSUMERS",
            $"expected={expected.Count}; rows={routeRoots.Length}; labels={string.Join(" | ", labels)}");

        bool recipeBranchText = Resources.FindObjectsOfTypeAll<TMP_Text>()
            .Where(text => text != null
                && text.gameObject.scene.IsValid()
                && text.gameObject.activeInHierarchy
                && text.name == "ProductionProcessLabel")
            .Any(text => text.text.Contains("분기:", StringComparison.Ordinal));
        Check(recipeBranchText,
            suffix + "_" + phase + "_RECIPE_BRANCH_SUMMARY",
            "production recipe branch summary is visible");
    }

    private ProductionConsumerRoutePolicy[] ResolvePolicies(
        ProductionBillSnapshot bill)
    {
        if (bill.RoutePolicies.Count > 0)
        {
            return bill.RoutePolicies.Select(policy => policy.Clone()).ToArray();
        }
        Require(catalog.TryGetRecipe(bill.RecipeId, out ProductionRecipeSO recipe),
            "The active recipe disappeared from the catalog.");
        return recipe.Outputs
            .Where(output => output != null)
            .SelectMany(output => dependencies.GetConsumers(output.ItemId))
            .Where(link => link != null && link.IsRealConsumer)
            .GroupBy(link => link.consumerId, StringComparer.Ordinal)
            .Select(group => new ProductionConsumerRoutePolicy
            {
                consumerId = group.Key,
                enabled = true,
                priority = 50,
                weight = 1
            })
            .ToArray();
    }

    private (ProductionRecipeSO Recipe, BuildingSO Building)
        SelectScenarioContent()
    {
        BuildingSO[] buildings = content.GetAll<BuildingSO>()
            .Where(building => building != null
                && building.GetProductionWorkstationAbility() != null)
            .OrderBy(building => building.id)
            .ToArray();
        foreach (ProductionRecipeSO recipe in catalog.Recipes
                     .Where(recipe => recipe != null
                         && recipe.RequiredSupportTags.Count == 0)
                     .OrderBy(recipe => recipe.RecipeId, StringComparer.Ordinal))
        {
            int branches = recipe.Outputs
                .Where(output => output != null)
                .SelectMany(output => dependencies.GetConsumers(output.ItemId))
                .Where(link => link != null && link.IsRealConsumer)
                .Select(link => link.consumerId)
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (branches < 2)
            {
                continue;
            }

            BuildingSO building = buildings.FirstOrDefault(candidate =>
                candidate.Facility != null
                && candidate.Facility.SupportsWork(recipe.WorkTypeId)
                && string.Equals(
                    candidate.GetProductionWorkstationAbility().WorkstationTag,
                    recipe.WorkstationTag,
                    StringComparison.Ordinal)
                && TryFindPosition(candidate, out _));
            if (building != null)
            {
                report.Add($"SCENARIO_CONTENT=recipe:{recipe.RecipeId}; building:{building.id}:{building.objectName}; branches:{branches}");
                return (recipe, building);
            }
        }

        throw new InvalidOperationException(
            "No unlocked, support-free branched production recipe and workstation could be placed.");
    }

    private BuildableObject PlaceFacility(BuildingSO definition)
    {
        Require(TryFindPosition(definition, out Vector2Int position),
            $"No free grid position was found for '{definition.objectName}'.");
        BuildableObject building = buildingFactory.Create(
            grid,
            definition,
            position);
        Require(building != null,
            $"Could not create '{definition.objectName}'.");
        foreach (MonoBehaviour component in
                 building.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component != null)
            {
                scope.Container.Inject(component);
            }
        }
        building.SetGrid(grid);
        building.Initialization(definition, position);
        bool registered = grid.RegisterOccupant(
            building,
            definition.layer,
            definition.GetGridPosList(position),
            definition.Placement.IsMovement);
        Require(registered,
            $"Could not register '{definition.objectName}' on the live grid.");
        report.Add($"FACILITY={definition.objectName}; position={position.x},{position.y}; instance={building.RequirePersistentInstanceId().Value}");
        return building;
    }

    private bool TryFindPosition(
        BuildingSO definition,
        out Vector2Int position)
    {
        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                Vector2Int candidate = new(x, y);
                bool available = definition.GetGridPosList(candidate)
                    .All(cellPosition =>
                    {
                        GridCell cell = grid.GetGridCell(cellPosition);
                        return cell != null
                            && cell.AreaType != GridCellAreaType.BlockedExterior
                            && cell.CanOccupy(definition.layer);
                    });
                if (available)
                {
                    position = candidate;
                    return true;
                }
            }
        }
        position = default;
        return false;
    }

    private ProductionBillSnapshot RequireBill(
        BuildableObject facility,
        ProductionBillId billId)
    {
        ProductionBillSnapshot bill = billQuery.GetBills(facility)
            .SingleOrDefault(candidate => candidate.BillId == billId);
        Require(bill != null,
            $"Production bill '{billId.Value}' was not present.");
        return bill;
    }

    private IEnumerator Click(Button button, string key)
    {
        Check(button != null && button.gameObject.activeInHierarchy,
            key + "_TARGET",
            button == null ? "button missing" : button.name);
        if (button == null)
        {
            yield break;
        }

        Canvas.ForceUpdateCanvases();
        RectTransform rect = button.transform as RectTransform;
        yield return BringIntoView(rect);
        Vector2 point = RectTransformUtility.WorldToScreenPoint(
            null,
            rect.TransformPoint(rect.rect.center));
        PointerEventData pointer = new(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            position = point,
            pressPosition = point
        };
        List<RaycastResult> hits = new();
        EventSystem.current.RaycastAll(pointer, hits);
        GameObject handler = hits
            .Select(hit => ExecuteEvents.GetEventHandler<IPointerClickHandler>(
                hit.gameObject))
            .FirstOrDefault(candidate => candidate == button.gameObject);
        bool dispatched = handler != null;
        if (dispatched)
        {
            ExecuteEvents.Execute(
                handler,
                pointer,
                ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(
                handler,
                pointer,
                ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(
                handler,
                pointer,
                ExecuteEvents.pointerClickHandler);
        }
        Check(dispatched,
            key,
            $"target={button.name}; point={point}; hits={hits.Count}");
        yield return RefreshFrames();
    }

    private static IEnumerator BringIntoView(RectTransform target)
    {
        ScrollRect scroll = target != null
            ? target.GetComponentInParent<ScrollRect>()
            : null;
        RectTransform viewport = scroll != null
            ? scroll.viewport ?? scroll.transform as RectTransform
            : null;
        if (scroll == null
            || scroll.content == null
            || viewport == null
            || !target.IsChildOf(scroll.content))
        {
            yield break;
        }

        scroll.StopMovement();
        Canvas.ForceUpdateCanvases();
        for (int pass = 0; pass < 2; pass++)
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                viewport,
                target);
            float lower = viewport.rect.yMin + 8f;
            float upper = viewport.rect.yMax - 8f;
            float adjustment = 0f;
            if (bounds.min.y < lower)
            {
                adjustment = lower - bounds.min.y;
            }
            else if (bounds.max.y > upper)
            {
                adjustment = upper - bounds.max.y;
            }

            if (Mathf.Abs(adjustment) < 0.5f)
            {
                break;
            }
            Vector2 position = scroll.content.anchoredPosition;
            position.y += adjustment;
            scroll.content.anchoredPosition = position;
            scroll.velocity = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            yield return null;
        }
        yield return null;
    }

    private IEnumerator SelectResolution(
        int width,
        int height,
        string suffix)
    {
        GameViewResolutionController.Select(width, height);
        float deadline = Time.realtimeSinceStartup + 5f;
        while ((Screen.width != width || Screen.height != height)
            && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
        yield return RefreshFrames();
        Check(Screen.width == width && Screen.height == height,
            suffix + "_RESOLUTION",
            $"actual={Screen.width}x{Screen.height}");
    }

    private IEnumerator Capture(
        string path,
        int expectedWidth,
        int expectedHeight,
        string key)
    {
        yield return PlayModeVerificationFrameWait.CaptureReady();
        Texture2D capture =
            PlayModeVerificationFrameWait.CaptureScreenshotAsTexture();
        if (capture == null)
        {
            Check(false, key, "capture returned null");
            yield break;
        }

        File.WriteAllBytes(path, capture.EncodeToPNG());
        int visible = capture.GetPixels32().Count(pixel =>
            pixel.a > 0 && (pixel.r > 5 || pixel.g > 5 || pixel.b > 5));
        Check(capture.width == expectedWidth
                && capture.height == expectedHeight
                && visible > capture.width * capture.height / 20,
            key,
            $"size={capture.width}x{capture.height}; visible={visible}");
        Destroy(capture);
    }

    private static IEnumerator RefreshFrames()
    {
        Canvas.ForceUpdateCanvases();
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;
    }

    private static Button FindButton(string name)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && string.Equals(button.name, name, StringComparison.Ordinal));
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(candidate => candidate != null
                && candidate.scene.IsValid()
                && candidate.activeInHierarchy
                && string.Equals(candidate.name, name, StringComparison.Ordinal));
    }

    private static GameObject[] FindSceneObjects(string prefix)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(candidate => candidate != null
                && candidate.scene.IsValid()
                && candidate.activeInHierarchy
                && candidate.name.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(candidate => candidate.name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ButtonLabel(Button button)
    {
        return button == null
            ? string.Empty
            : button.GetComponentInChildren<TMP_Text>(true)?.text
                ?? string.Empty;
    }

    private void VerifyCaptureLayout(int expectedRouteCount, string suffix)
    {
        RectTransform buildingPanelRect = buildingInfo != null
            ? buildingInfo.transform as RectTransform
            : null;
        Check(IsInsideScreen(buildingPanelRect, 1f),
            suffix + "_BUILDING_PANEL_SCREEN_BOUNDS",
            DescribeScreenRect(buildingPanelRect));
        Button[] activeBuildingButtons = buildingInfo != null
            ? buildingInfo.GetComponentsInChildren<Button>(false)
                .Where(button => button != null
                    && button.gameObject.activeInHierarchy
                    && IsInsideActiveViewport(button.transform as RectTransform))
                .ToArray()
            : Array.Empty<Button>();
        Button[] outOfBoundsButtons = activeBuildingButtons
            .Where(button => !IsInsideScreen(
                button.transform as RectTransform,
                1f))
            .ToArray();
        Check(activeBuildingButtons.Length > 0
                && outOfBoundsButtons.Length == 0,
            suffix + "_BUILDING_ACTIONS_SCREEN_BOUNDS",
            $"buttons={activeBuildingButtons.Length}; outOfBounds="
            + string.Join(",", outOfBoundsButtons.Select(button =>
                button.name + ":" + DescribeScreenRect(
                    button.transform as RectTransform))));

        GameObject panel = FindSceneObject("BuildingContextActions");
        RectTransform panelRect = panel != null
            ? panel.transform as RectTransform
            : null;
        bool panelInScreen = IsInsideScreen(panelRect, 1f);
        Check(panelInScreen,
            suffix + "_CONTEXT_PANEL_SCREEN_BOUNDS",
            DescribeScreenRect(panelRect));

        GameObject[] routes = FindSceneObjects("ProductionRoute_0_");
        ScrollRect scroll = routes.FirstOrDefault()
            ?.GetComponentInParent<ScrollRect>();
        RectTransform viewport = scroll != null
            ? scroll.viewport ?? scroll.transform as RectTransform
            : null;
        bool everyRouteReachable = routes.Length == expectedRouteCount
            && scroll?.content != null
            && routes.All(route => route != null
                && route.activeInHierarchy
                && route.transform.IsChildOf(scroll.content));
        Check(everyRouteReachable,
            suffix + "_ALL_ROUTE_ROWS_REACHABLE",
            $"expected={expectedRouteCount}; actual={routes.Length}; "
            + string.Join(" | ", routes.Select(route =>
                route.name + ":" + DescribeViewportBounds(
                    route.transform as RectTransform,
                    viewport))));

        RectTransform lastRoute = FindSceneObject(
                $"ProductionRoute_0_{expectedRouteCount - 1}")
            ?.transform as RectTransform;
        Check(lastRoute != null
                && IsFullyInsideViewport(lastRoute, viewport, 1f),
            suffix + "_LAST_ROUTE_VISIBLE_AFTER_SCROLL",
            DescribeViewportBounds(lastRoute, viewport));
    }

    private static bool IsInsideScreen(RectTransform rect, float tolerance)
    {
        if (rect == null)
        {
            return false;
        }
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Camera camera = rect.GetComponentInParent<Canvas>()?.renderMode
            == RenderMode.ScreenSpaceOverlay
                ? null
                : rect.GetComponentInParent<Canvas>()?.worldCamera;
        return corners.Select(corner => RectTransformUtility.WorldToScreenPoint(
                camera,
                corner))
            .All(point => point.x >= -tolerance
                && point.y >= -tolerance
                && point.x <= Screen.width + tolerance
                && point.y <= Screen.height + tolerance);
    }

    private static bool IsFullyInsideViewport(
        RectTransform target,
        RectTransform viewport,
        float tolerance)
    {
        if (target == null || viewport == null)
        {
            return false;
        }
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            viewport,
            target);
        return bounds.min.x >= viewport.rect.xMin - tolerance
            && bounds.max.x <= viewport.rect.xMax + tolerance
            && bounds.min.y >= viewport.rect.yMin - tolerance
            && bounds.max.y <= viewport.rect.yMax + tolerance;
    }

    private static bool IsInsideActiveViewport(RectTransform target)
    {
        ScrollRect scroll = target != null
            ? target.GetComponentInParent<ScrollRect>()
            : null;
        if (scroll == null)
        {
            return true;
        }
        RectTransform viewport = scroll.viewport
            ?? scroll.transform as RectTransform;
        return IsFullyInsideViewport(target, viewport, 1f);
    }

    private static string DescribeScreenRect(RectTransform rect)
    {
        if (rect == null)
        {
            return "missing";
        }
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Camera camera = rect.GetComponentInParent<Canvas>()?.renderMode
            == RenderMode.ScreenSpaceOverlay
                ? null
                : rect.GetComponentInParent<Canvas>()?.worldCamera;
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        return $"screen={Screen.width}x{Screen.height}; rect={min.x:F1},{min.y:F1}..{max.x:F1},{max.y:F1}";
    }

    private static string DescribeViewportBounds(
        RectTransform target,
        RectTransform viewport)
    {
        if (target == null || viewport == null)
        {
            return "missing";
        }
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            viewport,
            target);
        return $"row={bounds.min.x:F1},{bounds.min.y:F1}..{bounds.max.x:F1},{bounds.max.y:F1}; "
            + $"view={viewport.rect.xMin:F1},{viewport.rect.yMin:F1}..{viewport.rect.xMax:F1},{viewport.rect.yMax:F1}";
    }

    private IEnumerator ExecuteGuarded(IEnumerator root)
    {
        Stack<IEnumerator> routines = new();
        routines.Push(root);
        while (routines.Count > 0 && verificationException == null)
        {
            IEnumerator routine = routines.Peek();
            bool moved = false;
            object current = null;
            try
            {
                moved = routine.MoveNext();
                if (moved)
                {
                    current = routine.Current;
                }
            }
            catch (Exception exception)
            {
                verificationException = exception;
            }

            if (verificationException != null)
            {
                break;
            }
            if (!moved)
            {
                (routine as IDisposable)?.Dispose();
                routines.Pop();
                continue;
            }
            if (current is IEnumerator nested)
            {
                routines.Push(nested);
                continue;
            }
            yield return current;
        }

        while (routines.Count > 0)
        {
            (routines.Pop() as IDisposable)?.Dispose();
        }
    }

    private void CleanupScenario()
    {
        if (buildingInfo != null && buildingInfo.gameObject.activeInHierarchy)
        {
            buildingInfo.CloseDispaly();
        }
        if (billPersistence != null && originalBills != null)
        {
            billPersistence.Restore(
                billPersistence.BuildRestore(originalBills));
        }
        if (worldItems != null && originalWorldItems != null)
        {
            worldItems.Restore(originalWorldItems);
        }
        if (researchSaveSection != null
            && !string.IsNullOrWhiteSpace(originalResearchJson))
        {
            DungeonGameRestoreReport restoreReport = new();
            researchSaveSection.Restore(
                originalResearchJson,
                researchSaveSection.SectionVersion,
                restoreReport);
            if (!restoreReport.Success)
            {
                failures.Add(
                    "RESEARCH_RESTORE: "
                    + string.Join(" | ", restoreReport.Errors));
            }
        }
        if (activeFacility != null)
        {
            BuildingSO definition = activeFacility.BuildingData;
            if (definition != null && grid != null)
            {
                grid.RemoveOccupant(
                    activeFacility,
                    definition.layer,
                    definition.GetGridPosList(activeFacility.centerPos),
                    definition.Placement.IsMovement);
            }
            Destroy(activeFacility.gameObject);
            activeFacility = null;
        }
    }

    private void Finish()
    {
        Time.timeScale = originalTimeScale;
        if (gameManager != null)
        {
            gameManager.isPause = originalPause;
        }
        if (originalResolutionIndex >= 0)
        {
            GameViewResolutionController.SelectedSizeIndex =
                originalResolutionIndex;
        }
        Application.logMessageReceived -= CaptureLog;
        File.Delete(ProductionBuildingPlayModeVerifier.RequestPath);

        if (verificationException != null)
        {
            failures.Add("EXCEPTION: " + verificationException);
        }
        report.Add($"CONSOLE errors={errors.Count}; warnings={warnings.Count}");
        if (errors.Count > 0 || warnings.Count > 0)
        {
            failures.Add(
                $"Console errors={errors.Count}, warnings={warnings.Count}");
        }
        UTF8Encoding reportEncoding = new(encoderShouldEmitUTF8Identifier: true);
        File.WriteAllLines(
            ProductionBuildingPlayModeVerifier.ReportPath,
            report,
            reportEncoding);
        byte[] encodedReport = File.ReadAllBytes(
            ProductionBuildingPlayModeVerifier.ReportPath);
        byte[] preamble = reportEncoding.GetPreamble();
        bool hasUtf8Bom = encodedReport.Length >= preamble.Length
            && preamble.SequenceEqual(encodedReport.Take(preamble.Length));
        string decodedReport = File.ReadAllText(
            ProductionBuildingPlayModeVerifier.ReportPath,
            reportEncoding);
        bool koreanRoundTrip = decodedReport.Contains(
            "출력 공간 대기",
            StringComparison.Ordinal)
            && decodedReport.Contains("목표 재고", StringComparison.Ordinal);
        Check(hasUtf8Bom,
            "REPORT_UTF8_BOM",
            $"preambleBytes={preamble.Length}");
        Check(koreanRoundTrip,
            "REPORT_KOREAN_ROUND_TRIP",
            "Korean verification labels decoded without replacement.");
        report.Add($"RESULT={(failures.Count == 0 ? "PASS" : "FAIL")}");
        foreach (string failure in failures)
        {
            report.Add("FAILURE=" + failure);
        }
        File.WriteAllLines(
            ProductionBuildingPlayModeVerifier.ReportPath,
            report,
            reportEncoding);

        if (failures.Count == 0)
        {
            Debug.Log(
                "Production UI pointer matrix verification passed. "
                + ProductionBuildingPlayModeVerifier.ReportPath);
        }
        else
        {
            Debug.LogError(
                "Production UI pointer matrix verification failed. "
                + ProductionBuildingPlayModeVerifier.ReportPath);
        }
        EditorApplication.ExitPlaymode();
    }

    private void CaptureLog(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type is LogType.Error or LogType.Exception or LogType.Assert)
        {
            errors.Add(condition);
        }
        else if (type == LogType.Warning)
        {
            warnings.Add(condition);
        }
    }

    private void Check(bool passed, string key, string detail)
    {
        report.Add($"{key}={(passed ? "PASS" : "FAIL")}; {detail}");
        if (!passed)
        {
            failures.Add(key + ": " + detail);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
