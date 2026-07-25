using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using VContainer;

public static class BuildPlacementUxPlayModeVerifier
{
    public const string ReportPath = "Temp/build-placement-ux-report.txt";
    public const string CapturePath = "Temp/build-placement-ux.png";
    public const string OpenCatalogCapturePath = "Temp/phase66-compact-build-catalog.png";
    public const string ConstructionInfoCapturePath = "Temp/build-placement-construction-site-info.png";
    public const string ConstructionProgressCapturePath = "Temp/build-placement-construction-progress.png";

    [MenuItem("DungeonStory/Debug/QA/Run Build Placement UX Verification")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("Build placement UX verification requires PlayMode.");
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<BuildPlacementUxPlayModeVerificationRunner>() != null)
        {
            Debug.LogWarning("Build placement UX verification is already running.");
            return;
        }

        EditorApplication.ExecuteMenuItem("Window/General/Game");
        new GameObject("Build Placement UX Verification Runner")
            .AddComponent<BuildPlacementUxPlayModeVerificationRunner>();
    }
}

public sealed class BuildPlacementUxPlayModeVerificationRunner : MonoBehaviour
{
    private const string PartialConstructionSaveSlot =
        "qa-build-placement-partial";

    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> capturedErrors = new List<string>();
    private readonly List<string> capturedWarnings = new List<string>();
    private InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;
    private Mouse originalMouse;
    private Mouse verificationMouse;
    private int verificationMouseSerial;
    private Vector2 originalMousePosition;
    private bool inputConfigured;

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Temp");
        Application.logMessageReceived += OnLogMessageReceived;
        ConfigureInput();
        yield return null;
        yield return null;
        yield return null;
        yield return EnsureRunReady();

        UITabManager tabManager = UnityEngine.Object.FindFirstObjectByType<UITabManager>();
        GridConstructTab constructTab = UnityEngine.Object.FindFirstObjectByType<GridConstructTab>(FindObjectsInactive.Include);
        GridUIManager gridUi = UnityEngine.Object.FindFirstObjectByType<GridUIManager>(FindObjectsInactive.Include);
        DungeonStoryGridBuildingController controller = UnityEngine.Object.FindFirstObjectByType<DungeonStoryGridBuildingController>();
        DungeonStoryGridGhostPresenter ghostPresenter = UnityEngine.Object.FindFirstObjectByType<DungeonStoryGridGhostPresenter>();
        IWorkOrderRuntime workOrderRuntime = ResolveFromGameplayScope<IWorkOrderRuntime>();
        IWorldItemStackRuntime itemRuntime = ResolveFromGameplayScope<IWorldItemStackRuntime>();
        IDungeonGameSaveSlotService saveSlotService =
            ResolveFromGameplayScope<IDungeonGameSaveSlotService>();

        Check(tabManager != null, "TAB_MANAGER", "bottom tab manager resolved");
        Check(constructTab != null, "CONSTRUCT_TAB", "build catalog resolved");
        Check(gridUi != null, "GRID_UI", "placement grid UI resolved");
        Check(controller != null, "BUILD_CONTROLLER", "building controller resolved");
        Check(ghostPresenter != null, "GHOST_PRESENTER", "placement ghost presenter resolved");
        Check(workOrderRuntime != null, "WORK_ORDER_RUNTIME", "scoped work order runtime resolved");
        Check(itemRuntime != null, "ITEM_STACK_RUNTIME", "scoped world item runtime resolved");
        Check(saveSlotService != null, "SAVE_SLOT_SERVICE", "scoped save slot service resolved");
        if (tabManager == null
            || constructTab == null
            || gridUi == null
            || controller == null
            || ghostPresenter == null
            || workOrderRuntime == null
            || itemRuntime == null
            || saveSlotService == null)
        {
            Finish();
            yield break;
        }

        controller.SetGridModeNone();
        yield return null;

        Button buildTabButton = FindVisibleButtonByLabel("\uAC74\uCD95");
        Check(buildTabButton != null, "BUILD_TAB_BUTTON", "visible build tab button resolved");
        PressButton(buildTabButton);
        yield return null;
        yield return null;
        Check(constructTab.gameObject.activeInHierarchy, "CATALOG_OPENED_BY_POINTER", "build catalog opened from its UI button");
        CheckCompactCatalogRoot(constructTab);

        Button categoryButton = constructTab.GetComponentsInChildren<Button>(false)
            .FirstOrDefault(button => button != null
                && button.GetComponent<UIBuildingSelectButton>() == null
                && button.GetComponentsInChildren<TMP_Text>(true)
                    .Any(text => text != null && text.text == "\uBCBD/\uBB38"));
        Check(categoryButton != null, "CATEGORY_BUTTON", "visible build category button resolved");
        PressButton(categoryButton);
        yield return null;
        yield return null;
        CheckCompactCatalogContent(constructTab);
        yield return new WaitForEndOfFrame();
        CaptureScreen(BuildPlacementUxPlayModeVerifier.OpenCatalogCapturePath);

        UIBuildingSelectButton selection = constructTab
            .GetComponentsInChildren<UIBuildingSelectButton>(false)
            .Where(button => button != null && button.GetComponent<Button>()?.interactable == true)
            .OrderBy(button => button.id == 0 ? 1 : 0)
            .FirstOrDefault();
        Check(selection != null, "BUILD_ITEM_BUTTON", "visible build item button resolved");
        PressButton(selection != null ? selection.GetComponent<Button>() : null);
        yield return null;
        yield return new WaitForEndOfFrame();

        GridGhostObject ghost = ghostPresenter.GetComponent<GridGhostObject>();
        BuildingSO selectedBuilding = controller.SelectedBuilding;
        Check(controller.GridSystem.Mode == GridMode.Build, "PLACEMENT_MODE_PRESERVED", $"mode={controller.GridSystem.Mode}");
        Check(selectedBuilding != null, "BUILD_SELECTION_PRESERVED", selectedBuilding?.objectName ?? "<none>");
        Check(!constructTab.gameObject.activeSelf, "CATALOG_COLLAPSED", $"active={constructTab.gameObject.activeSelf}");
        Check(constructTab.selectButtonPanelList.All(panel => panel == null || !panel.gameObject.activeSelf),
            "CATEGORY_PANELS_COLLAPSED", "all category panels are hidden");
        Check(gridUi.IsGridVisible && gridUi.BuildableCellCount > 0,
            "PLACEMENT_GRID_VISIBLE", $"visible={gridUi.IsGridVisible}; buildable={gridUi.BuildableCellCount}");
        Check(ghost != null && !ghost.IsHidden, "PLACEMENT_GHOST_VISIBLE", $"ghostHidden={ghost?.IsHidden}");
        Check(!IsCatalogBlockingScreenCenter(constructTab), "WORLD_CENTER_NOT_BLOCKED", "collapsed catalog has no center-screen UI hit");

        CaptureScreen(BuildPlacementUxPlayModeVerifier.CapturePath);
        yield return VerifyConstructionPlacement(
            controller,
            gridUi,
            selectedBuilding,
            workOrderRuntime,
            itemRuntime,
            saveSlotService);

        Button buildingTabButton = FindVisibleButtonByLabel("\uAC74\uBB3C");
        Check(buildingTabButton != null, "OTHER_TAB_BUTTON", "visible building-management tab resolved");
        PressButton(buildingTabButton);
        yield return null;
        Check(controller.GridSystem.Mode == GridMode.None && controller.SelectedBuilding == null,
            "OTHER_TAB_CANCELS_PLACEMENT",
            $"mode={controller.GridSystem.Mode}; selection={controller.SelectedBuilding?.objectName ?? "<none>"}");

        controller.SetGridModeNone();
        Application.logMessageReceived -= OnLogMessageReceived;
        Finish();
        Destroy(gameObject);
    }

    private static Button FindVisibleButtonByLabel(string label)
    {
        return UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(button => button != null
                && button.interactable
                && button.GetComponentsInChildren<TMP_Text>(true).Any(text => text != null && text.text == label));
    }

    private IEnumerator EnsureRunReady()
    {
        OwnerRunManager ownerManager = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        if (ownerManager == null || ownerManager.CurrentOwnerActor == null)
        {
            string fastCommit = StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            report.Add("[INFO] FAST_PARTY_COMMIT " + fastCommit);
            for (int i = 0; i < 8; i++)
            {
                yield return null;
            }
        }

        ownerManager = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        Check(ownerManager != null && ownerManager.CurrentOwnerActor != null,
            "RUN_READY",
            ownerManager != null && ownerManager.CurrentOwnerActor != null
                ? $"owner={ownerManager.CurrentOwnerActor.name}"
                : "owner missing");
    }

    private static void PressButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left
        };
        button.OnPointerClick(eventData);
    }

    private IEnumerator VerifyConstructionPlacement(
        DungeonStoryGridBuildingController controller,
        GridUIManager gridUi,
        BuildingSO selectedBuilding,
        IWorkOrderRuntime workOrderRuntime,
        IWorldItemStackRuntime itemRuntime,
        IDungeonGameSaveSlotService saveSlotService)
    {
        if (controller == null || gridUi == null || selectedBuilding == null)
        {
            Check(false, "CONSTRUCTION_VERIFY_INPUTS", "controller, grid UI, or selected building missing");
            yield break;
        }

        Grid grid = controller.GridSystem?.grid;
        Camera camera = Camera.main;
        Check(grid != null, "CONSTRUCTION_GRID_RESOLVED", grid != null ? "grid ready" : "<null>");
        Check(camera != null, "CONSTRUCTION_CAMERA_RESOLVED", camera != null ? camera.name : "<null>");
        Check(itemRuntime != null, "ITEM_STACK_RUNTIME_SCOPED", itemRuntime != null ? "resolved" : "<null>");
        if (grid == null || camera == null || workOrderRuntime == null || itemRuntime == null)
        {
            yield break;
        }

        if (!TryFindBuildableScreenPoint(controller, grid, camera, selectedBuilding, out Vector2Int buildPos, out Vector2 screenPoint))
        {
            Check(false, "CONSTRUCTION_BUILDABLE_POINT", $"no visible buildable point for {selectedBuilding.objectName}");
            yield break;
        }

        BuildableObject finalBuildingBefore = FindFinalBuildingAt(grid, selectedBuilding, buildPos);
        Check(finalBuildingBefore == null,
            "TARGET_CELL_EMPTY_BEFORE_CONSTRUCTION",
            finalBuildingBefore == null ? $"target={buildPos}" : $"{finalBuildingBefore.name}@{finalBuildingBefore.centerPos}");
        Check(gridUi.IsGridVisible, "CONSTRUCTION_GRID_STILL_VISIBLE", $"visible={gridUi.IsGridVisible}; buildable={gridUi.BuildableCellCount}");
        yield return ClickWorldPoint(screenPoint);
        if (selectedBuilding.GetDraggable())
        {
            yield return ClickWorldPoint(screenPoint);
        }

        yield return null;
        yield return new WaitForEndOfFrame();

        ConstructionSite site = FindConstructionSiteAt(grid, selectedBuilding, buildPos);
        Check(site != null, "CONSTRUCTION_SITE_CREATED_BY_POINTER", site != null ? $"{site.name}@{buildPos}" : $"missing@{buildPos}");
        Check(site != null
                && selectedBuilding.GetGridPosList(buildPos)
                    .All(pos => ReferenceEquals(grid.GetGridCell(pos)?.GetOccupant(GridLayer.Construction), site)),
            "CONSTRUCTION_LAYER_OCCUPIED",
            site != null ? $"cells={selectedBuilding.GetGridPosList(buildPos).Count}" : "site missing");
        Check(FindFinalBuildingAt(grid, selectedBuilding, buildPos) == null,
            "FINAL_BUILDING_NOT_INSTANT",
            "target footprint still has only a construction site");
        if (site == null)
        {
            yield break;
        }

        Check(workOrderRuntime.TryGetOrderFor(site, BuiltInWorkTypeIds.Construct, out WorkOrderProgressState createdOrder),
            "CONSTRUCTION_WORK_ORDER_CREATED",
            workOrderRuntime.TryGetOrderFor(site, BuiltInWorkTypeIds.Construct, out createdOrder)
                ? $"{createdOrder.WorkOrderId}; status={createdOrder.Status}; work={createdOrder.CompletedWork:0.##}/{createdOrder.RequiredWork:0.##}"
                : "missing");

        yield return ClickWorldPoint(screenPoint);
        yield return null;
        yield return new WaitForEndOfFrame();
        CaptureScreen(BuildPlacementUxPlayModeVerifier.ConstructionInfoCapturePath);
        Check(File.Exists(BuildPlacementUxPlayModeVerifier.ConstructionInfoCapturePath),
            "CONSTRUCTION_INFO_CAPTURED",
            BuildPlacementUxPlayModeVerifier.ConstructionInfoCapturePath);

        if (!workOrderRuntime.TryGetOrderFor(site, BuiltInWorkTypeIds.Construct, out WorkOrderProgressState order))
        {
            yield break;
        }

        Check(order.Status == WorkOrderStatus.Ready
                || order.Status == WorkOrderStatus.InProgress
                || order.Status == WorkOrderStatus.WaitingForMaterials,
            "CONSTRUCTION_MATERIAL_STATUS_VALID",
            $"status={order.Status}; destination={order.MaterialDestinationId}");
        if (order.Status == WorkOrderStatus.WaitingForMaterials)
        {
            Check(!itemRuntime.GetAllStacks().Any(stack =>
                    stack.Position == order.Position
                    && string.Equals(
                        stack.DestinationId,
                        order.MaterialDestinationId,
                        StringComparison.Ordinal)),
                "CONSTRUCTION_DOES_NOT_DROP_WAREHOUSE_STOCK",
                $"destination={order.MaterialDestinationId}; target={order.Position}");
        }

        SetNaturalFlowTestSpeed();
        saveSlotService.Delete(PartialConstructionSaveSlot);
        float deadline = Time.realtimeSinceStartup + 45f;
        bool observedPhysicalDelivery = false;
        bool observedDeliveredMaterial = false;
        bool observedAiProgress = false;
        bool progressCaptured = false;
        bool partialSaveCreated = false;
        bool partialSaveRestored = false;
        float savedProgress = 0f;
        int savedDelivered = 0;
        float maxProgress = order.ProgressRatio;
        int maxDelivered = GetDeliveredMaterialCount(order);
        string materialDestinationId = order.MaterialDestinationId;
        string lastState = DescribeOrder(order);
        BuildableObject finalBuilding = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            StabilizeConstructionProbeWorkers();
            WorldItemStackSnapshot[] currentStacks = itemRuntime.GetAllStacks()
                .Where(stack => stack != null)
                .ToArray();
            observedPhysicalDelivery |= currentStacks.Any(stack =>
                string.Equals(
                    stack.DestinationId,
                    materialDestinationId,
                    StringComparison.Ordinal)
                && (stack.State == WorldItemStackState.Stored
                    || stack.State == WorldItemStackState.Loose
                    || stack.State == WorldItemStackState.Carried
                    || stack.State == WorldItemStackState.FacilityBuffer));

            if (workOrderRuntime.TryGetOrderFor(
                    site,
                    BuiltInWorkTypeIds.Construct,
                    out WorkOrderProgressState currentOrder))
            {
                int delivered = GetDeliveredMaterialCount(currentOrder);
                maxDelivered = Mathf.Max(maxDelivered, delivered);
                maxProgress = Mathf.Max(maxProgress, currentOrder.ProgressRatio);
                observedDeliveredMaterial |= delivered > 0
                    || currentOrder.Status == WorkOrderStatus.Ready
                    || currentOrder.Status == WorkOrderStatus.InProgress;
                observedAiProgress |= currentOrder.CompletedWork > 0.001f;
                lastState = DescribeOrder(currentOrder);

                if (observedAiProgress && !progressCaptured)
                {
                    yield return ClickWorldPoint(screenPoint);
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    CaptureScreen(BuildPlacementUxPlayModeVerifier.ConstructionProgressCapturePath);
                    progressCaptured = File.Exists(
                        BuildPlacementUxPlayModeVerifier.ConstructionProgressCapturePath);
                    continue;
                }

                if (!partialSaveCreated
                    && currentOrder.CompletedWork > 0.001f
                    && currentOrder.ProgressRatio < 0.999f)
                {
                    savedProgress = currentOrder.ProgressRatio;
                    savedDelivered = delivered;
                    Time.timeScale = 0f;
                    string savePath = saveSlotService.Save(
                        PartialConstructionSaveSlot,
                        prettyPrint: true);
                    partialSaveCreated = !string.IsNullOrWhiteSpace(savePath)
                        && File.Exists(savePath);
                    Check(
                        partialSaveCreated,
                        "CONSTRUCTION_PARTIAL_SAVE_CREATED",
                        partialSaveCreated
                            ? $"{savePath}; progress={savedProgress:P0}; delivered={savedDelivered}"
                            : "partial construction save was not written");

                    if (partialSaveCreated)
                    {
                        bool loaded = saveSlotService.TryLoad(
                            PartialConstructionSaveSlot,
                            out DungeonGameRestoreReport restoreReport);
                        yield return null;
                        yield return null;

                        grid = controller.GridSystem?.grid;
                        ConstructionSite restoredSite = grid != null
                            ? FindConstructionSiteAt(
                                grid,
                                selectedBuilding,
                                buildPos)
                            : null;
                        WorkOrderProgressState restoredOrder = null;
                        bool restoredOrderFound = restoredSite != null
                            && workOrderRuntime.TryGetOrderFor(
                                restoredSite,
                                BuiltInWorkTypeIds.Construct,
                                out restoredOrder);
                        float restoredProgress = restoredOrderFound
                            ? restoredOrder.ProgressRatio
                            : 0f;
                        int restoredDelivered = restoredOrderFound
                            ? GetDeliveredMaterialCount(restoredOrder)
                            : 0;
                        partialSaveRestored = loaded
                            && restoreReport != null
                            && restoreReport.Success
                            && restoreReport.Warnings.Count == 0
                            && restoredOrderFound
                            && Mathf.Abs(restoredProgress - savedProgress) <= 0.02f
                            && restoredDelivered == savedDelivered;
                        Check(
                            partialSaveRestored,
                            "CONSTRUCTION_PARTIAL_SAVE_RESTORED",
                            restoreReport == null
                                ? "restore report missing"
                                : $"loaded={loaded}; warnings={restoreReport.Warnings.Count};"
                                    + $" errors={restoreReport.Errors.Count};"
                                    + $" progress={savedProgress:P0}->{restoredProgress:P0};"
                                    + $" delivered={savedDelivered}->{restoredDelivered};"
                                    + $" site={restoredSite?.name ?? "<missing>"}");

                        if (restoredSite != null)
                        {
                            site = restoredSite;
                        }

                        SetNaturalFlowTestSpeed();
                        continue;
                    }

                    SetNaturalFlowTestSpeed();
                }
            }

            finalBuilding = FindFinalBuildingAt(grid, selectedBuilding, buildPos);
            if (finalBuilding != null)
            {
                break;
            }

            yield return null;
        }

        AppendNaturalConstructionDiagnostics(
            workOrderRuntime,
            itemRuntime,
            materialDestinationId,
            site);
        Check(observedPhysicalDelivery,
            "CONSTRUCTION_PHYSICAL_DELIVERY_OBSERVED",
            $"destination={materialDestinationId}; delivered={maxDelivered}");
        Check(observedDeliveredMaterial,
            "CONSTRUCTION_MATERIAL_DELIVERED_BY_AI",
            $"delivered={maxDelivered}; {lastState}");
        Check(observedAiProgress,
            "CONSTRUCTION_WORK_PROGRESS_BY_AI",
            $"maxProgress={maxProgress:P0}; {lastState}");
        Check(progressCaptured,
            "CONSTRUCTION_PROGRESS_CAPTURED",
            progressCaptured
                ? BuildPlacementUxPlayModeVerifier.ConstructionProgressCapturePath
                : "AI work never entered a capturable partial-progress state");
        Check(partialSaveCreated,
            "CONSTRUCTION_PARTIAL_SAVE_OBSERVED",
            $"created={partialSaveCreated}; progress={savedProgress:P0}; delivered={savedDelivered}");
        Check(partialSaveRestored && finalBuilding != null,
            "CONSTRUCTION_RESTORED_AI_RESUMED",
            $"restored={partialSaveRestored}; completed={finalBuilding != null}; {lastState}");
        Check(finalBuilding != null,
            "CONSTRUCTION_COMPLETED_BY_AI",
            finalBuilding != null
                ? $"{finalBuilding.name}@{finalBuilding.centerPos}; maxProgress={maxProgress:P0}"
                : $"timeout; {lastState}");
        Check(finalBuilding != null,
            "CONSTRUCTION_REPLACED_WITH_FINAL_BUILDING",
            finalBuilding != null ? $"{finalBuilding.name}@{finalBuilding.centerPos}" : $"missing@{buildPos}");
        Check(FindConstructionSiteAt(grid, selectedBuilding, buildPos) == null,
            "CONSTRUCTION_SITE_REMOVED_AFTER_COMPLETION",
            "construction layer cleared");
        saveSlotService.Delete(PartialConstructionSaveSlot);
    }

    private static void StabilizeConstructionProbeWorkers()
    {
        foreach (CharacterActor actor in UnityEngine.Object.FindObjectsByType<CharacterActor>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            if (actor == null
                || actor.IsDead
                || !CharacterWorkRoleUtility.TryGetWork(actor, out _))
            {
                continue;
            }

            actor.stats[CharacterCondition.HUNGER] = 100f;
            actor.stats[CharacterCondition.THIRST] = 100f;
            actor.stats[CharacterCondition.SLEEP] = 100f;
            actor.stats[CharacterCondition.FUN] = 100f;
            actor.stats[CharacterCondition.EXCRETION] = 100f;
            actor.stats[CharacterCondition.HYGIENE] = 100f;
            actor.stats[CharacterCondition.MOOD] = 80f;
        }
    }

    private static int GetDeliveredMaterialCount(WorkOrderProgressState order)
    {
        return order?.DeliveredMaterials?.Values.Sum() ?? 0;
    }

    private static string DescribeOrder(WorkOrderProgressState order)
    {
        return order == null
            ? "order=<null>"
            : $"status={order.Status}; progress={order.ProgressRatio:P0};"
                + $" delivered={GetDeliveredMaterialCount(order)};"
                + $" worker={order.ReservedWorkerPersistentId ?? string.Empty}";
    }

    private void SetNaturalFlowTestSpeed()
    {
        GameManager gameManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        if (gameManager == null)
        {
            report.Add("[INFO] GameManager missing; natural construction flow remains at current speed.");
            return;
        }

        if (gameManager.isPause)
        {
            gameManager.TogglePause();
        }

        int changes = 0;
        while (Time.timeScale < 4.9f && changes++ < 5)
        {
            gameManager.ChangeGameSpeed();
        }

        report.Add($"[INFO] Natural construction flow speed={Time.timeScale:0.##}x");
    }

    private void AppendNaturalConstructionDiagnostics(
        IWorkOrderRuntime workOrderRuntime,
        IWorldItemStackRuntime itemRuntime,
        string materialDestinationId,
        ConstructionSite site)
    {
        report.Add(
            $"[FLOW SITE] site={site?.name};"
            + $" reservation={site?.WorkerReservation?.name};"
            + $" worker={site?.ActiveWorker?.name};"
            + $" status={site?.GetConstructionWorkStatus().FailureKind}:"
            + $"{site?.GetConstructionWorkStatus().Reason}");

        foreach (WorldItemStackSnapshot stack in itemRuntime.GetAllStacks()
                     .Where(stack => stack != null)
                     .OrderBy(stack => stack.StackId, StringComparer.Ordinal))
        {
            report.Add(
                $"[FLOW STACK] id={stack.StackId}; item={stack.ItemId}; state={stack.State};"
                + $" pos={stack.Position}; qty={stack.Quantity}; dest={stack.DestinationId};"
                + $" reserved={stack.ReservedByPersistentId}; source={stack.SourceStorageDestinationId};"
                + $" matchesConstruction={string.Equals(stack.DestinationId, materialDestinationId, StringComparison.Ordinal)}");
        }

        foreach (CharacterActor actor in UnityEngine.Object.FindObjectsByType<CharacterActor>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None)
                     .Where(actor => actor != null && !actor.IsDead))
        {
            AbilityWork work = actor.GetComponent<AbilityWork>();
            string haulPriority = work != null
                ? work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul).ToString()
                : "none";
            string constructPriority = work != null
                ? work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Construct).ToString()
                : "none";
            FacilityAssignmentStatus constructionStatus =
                site != null
                    ? site.GetWorkerAssignmentStatus(actor)
                    : FacilityAssignmentStatus.Rejected(
                        FacilityAssignmentFailureKind.Unknown,
                        "site missing");
            report.Add(
                $"[FLOW ACTOR] name={actor.name}; id={actor.Identity?.PersistentId};"
                + $" pos={actor.GetNowXY()}; ai={actor.CanRunAi}; offDuty={work?.IsOffDuty};"
                + $" haul={haulPriority}; construct={constructPriority};"
                + $" haulCandidate={itemRuntime.HasAvailableHaulJob(actor)};"
                + $" constructAllowed={constructionStatus.IsAllowed};"
                + $" constructReason={constructionStatus.Reason};"
                + $" deferred={actor.Brain?.IsPathSearchDeferred};"
                + $" action={actor.Brain?.CurrentActionDebugLabel};"
                + $" phase={actor.Brain?.CurrentActionPhase};"
                + $" detail={actor.Brain?.CurrentActionPhaseDetail}");
            if (actor.Brain != null)
            {
                report.Add(
                    $"[FLOW AI] name={actor.name}; "
                    + actor.Brain.GetDebugSummary(8).Replace("\n", " | "));
            }
        }

        DungeonWorkOrderSaveData orderSnapshot = workOrderRuntime.Capture();
        foreach (WorkOrderSaveData order in orderSnapshot?.orders
                     ?? new List<WorkOrderSaveData>())
        {
            string materialSummary = string.Join(
                ",",
                (order.materials ?? new List<WorkOrderMaterialSaveData>())
                .Select(material =>
                    $"{material.category}:{material.delivered}/{material.required}"));
            report.Add(
                $"[FLOW ORDER] id={order.workOrderId}; status={order.status};"
                + $" work={order.completedWork:0.##}/{order.requiredWork:0.##};"
                + $" worker={order.reservedWorkerPersistentId};"
                + $" materials={materialSummary}");
        }

        IGameplayFlowDiagnosticsQuery diagnostics =
            ResolveFromGameplayScope<IGameplayFlowDiagnosticsQuery>();
        IWorldItemHaulPlanningService haulPlanning =
            ResolveFromGameplayScope<IWorldItemHaulPlanningService>();
        if (haulPlanning != null)
        {
            foreach (CharacterActor actor in UnityEngine.Object
                         .FindObjectsByType<CharacterActor>(
                             FindObjectsInactive.Exclude,
                             FindObjectsSortMode.None)
                         .Where(actor => actor != null
                             && !actor.IsDead
                             && actor.CanRunAi))
            {
                bool hasPreview = haulPlanning.TryPreviewBestPlan(
                    actor,
                    out WorldItemHaulPlan preview,
                    out string previewFailureReason);
                WorldItemReservedStackQuantity primary =
                    hasPreview && preview.ReservedStackQuantities.Count > 0
                        ? preview.ReservedStackQuantities[0]
                        : default;
                report.Add(
                    $"[FLOW HAUL PREVIEW] name={actor.name}; valid={hasPreview};"
                    + $" stack={primary.StackId}; dest={primary.DestinationId};"
                    + $" pickup={primary.Position};"
                    + $" failure={previewFailureReason}");
            }
        }

        GameplayFlowDiagnosticsSnapshot flow = diagnostics?.Capture();
        if (flow != null)
        {
            report.Add(
                $"[FLOW DIAGNOSTIC] {flow.Summary} :: "
                + string.Join(
                    " || ",
                    flow.Items.Select(item => $"{item.Title}={item.Detail}")));
        }
    }

    private IEnumerator ClickWorldPoint(Vector2 screenPoint)
    {
        MoveAutomationPointer(screenPoint);
        DungeonAutomationInputState.ClickPointer(0);
        yield return null;
        yield return null;
        yield return null;
    }

    private void ConfigureInput()
    {
        if (inputConfigured)
        {
            return;
        }

        inputConfigured = true;
        originalMouse = Mouse.current;
        originalEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        if (originalMouse != null)
        {
            originalMousePosition = originalMouse.position.ReadValue();
            InputSystem.DisableDevice(originalMouse);
        }

        verificationMouse = InputSystem.AddDevice<Mouse>($"BuildPlacementVerificationMouse{++verificationMouseSerial}");
        InputSystem.EnableDevice(verificationMouse);
        verificationMouse.MakeCurrent();
        DungeonAutomationInputState.Enable(
            new DungeonStory.Foundation.UnityGameClock(),
            new DungeonStory.Foundation.UnityUiClock());
    }

    private void RestoreInput()
    {
        if (!inputConfigured)
        {
            return;
        }

        DungeonAutomationInputState.Disable();
        if (verificationMouse != null && verificationMouse.added)
        {
            InputSystem.RemoveDevice(verificationMouse);
        }

        if (originalMouse != null && originalMouse.added)
        {
            InputSystem.EnableDevice(originalMouse);
            originalMouse.MakeCurrent();
            InputSystem.QueueStateEvent(originalMouse, new MouseState { position = originalMousePosition });
            InputSystem.Update();
        }

        InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInputBehavior;
        verificationMouse = null;
        originalMouse = null;
        inputConfigured = false;
    }

    private void MoveAutomationPointer(Vector2 screenPoint)
    {
        DungeonAutomationInputState.MovePointer(screenPoint);
        if (verificationMouse == null)
        {
            return;
        }

        verificationMouse.MakeCurrent();
        MouseState state = new MouseState { position = screenPoint };
        InputState.Change(verificationMouse, state);
        InputSystem.QueueStateEvent(verificationMouse, state);
        InputSystem.Update();
    }

    private bool TryFindBuildableScreenPoint(
        DungeonStoryGridBuildingController controller,
        Grid grid,
        Camera camera,
        BuildingSO selectedBuilding,
        out Vector2Int buildPos,
        out Vector2 screenPoint)
    {
        buildPos = default;
        screenPoint = default;
        if (controller == null || grid == null || camera == null || selectedBuilding == null)
        {
            return false;
        }

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.54f);
        IEnumerable<GridCell> candidates = grid.GetCells()
            .Where(cell => cell != null
                && cell.IsBuildableArea
                && controller.IsBuildableAt(cell.Position)
                && selectedBuilding.GetGridPosList(cell.Position).All(pos => grid.GetGridCell(pos) != null))
            .OrderBy(cell =>
            {
                Vector3 world = grid.GetWorldPos(cell.Position) + new Vector3(0f, 0.45f, 0f);
                Vector3 screen = camera.WorldToScreenPoint(world);
                return ((Vector2)screen - screenCenter).sqrMagnitude;
            });

        foreach (GridCell candidate in candidates)
        {
            Vector3 world = grid.GetWorldPos(candidate.Position) + new Vector3(0f, 0.45f, 0f);
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f
                || screen.x < 40f
                || screen.x > Screen.width - 40f
                || screen.y < 190f
                || screen.y > Screen.height - 40f)
            {
                continue;
            }

            Vector2 candidateScreenPoint = new Vector2(screen.x, screen.y);
            if (IsScreenPointOverBlockingUi(candidateScreenPoint))
            {
                continue;
            }

            buildPos = candidate.Position;
            screenPoint = candidateScreenPoint;
            return true;
        }

        return false;
    }

    private static bool IsScreenPointOverBlockingUi(Vector2 screenPoint)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = screenPoint
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);
        return results.Any(result => result.gameObject != null
            && result.gameObject.activeInHierarchy
            && IsBlockingUiObject(result.gameObject));
    }

    private static bool IsBlockingUiObject(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return false;
        }

        return gameObject.GetComponentInParent<Button>() != null
            || gameObject.GetComponentInParent<Scrollbar>() != null
            || gameObject.GetComponentInParent<Slider>() != null
            || gameObject.GetComponentInParent<TMP_InputField>() != null
            || gameObject.GetComponentInParent<UITabManager>() != null
            || gameObject.GetComponentInParent<GridConstructTab>() != null
            || gameObject.GetComponentInParent<BuildingSummaryInfo>() != null
            || gameObject.GetComponentInParent<UIBuildingInfo>() != null
            || gameObject.GetComponentInParent<ItemPileInfoPanel>() != null
            || gameObject.GetComponentInParent<CharacterSummeryInfo>() != null;
    }

    private static ConstructionSite FindConstructionSiteAt(Grid grid, BuildingSO building, Vector2Int position)
    {
        if (grid == null || building == null)
        {
            return null;
        }

        return building.GetGridPosList(position)
            .Select(pos => grid.GetGridCell(pos)?.GetOccupant(GridLayer.Construction))
            .OfType<ConstructionSite>()
            .FirstOrDefault(site => site != null && site.TargetBuilding == building && !site.isDestroy);
    }

    private static BuildableObject FindFinalBuildingAt(Grid grid, BuildingSO building, Vector2Int position)
    {
        if (grid == null || building == null)
        {
            return null;
        }

        return building.GetGridPosList(position)
            .Select(pos => grid.GetGridCell(pos)?.GetOccupant(building.Placement.Layer))
            .OfType<BuildableObject>()
            .FirstOrDefault(item => item != null
                && !(item is ConstructionSite)
                && item.BuildingData == building
                && !item.isDestroy);
    }

    private static CharacterActor FindWorkActor()
    {
        return UnityEngine.Object
            .FindObjectsByType<CharacterActor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(actor => actor != null && !actor.IsDead);
    }

    private static bool IsCatalogBlockingScreenCenter(GridConstructTab constructTab)
    {
        if (EventSystem.current == null)
        {
            return true;
        }

        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);
        return results.Any(result => result.gameObject != null
            && result.gameObject.transform.IsChildOf(constructTab.transform));
    }

    private void CheckCompactCatalogRoot(GridConstructTab constructTab)
    {
        RectTransform root = constructTab.transform as RectTransform;
        Check(root != null && root.rect.height <= 150f,
            "CATALOG_COMPACT_HEIGHT",
            $"height={root?.rect.height ?? -1f:0.##}");
        Check(root != null && root.rect.height >= 110f,
            "CATALOG_CONTROLS_HAVE_ROOM",
            $"height={root?.rect.height ?? -1f:0.##}");
    }

    private void CheckCompactCatalogContent(GridConstructTab constructTab)
    {
        RectTransform root = constructTab.transform as RectTransform;
        Transform categoryRootTransform = constructTab.transform.childCount > 0
            ? constructTab.transform.GetChild(0)
            : null;
        RectTransform categoryRoot = categoryRootTransform as RectTransform;
        GridLayoutGroup categoryLayout = categoryRootTransform != null
            ? categoryRootTransform.GetComponent<GridLayoutGroup>()
            : null;
        UITab visiblePanel = constructTab.selectButtonPanelList
            .FirstOrDefault(panel => panel != null && panel.gameObject.activeInHierarchy);
        RectTransform visiblePanelRect = visiblePanel != null ? visiblePanel.transform as RectTransform : null;

        Check(categoryLayout != null
                && categoryLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount
                && categoryLayout.constraintCount == 8,
            "CATEGORY_GRID_SINGLE_ROW",
            $"constraint={categoryLayout?.constraint}; columns={categoryLayout?.constraintCount ?? 0}");
        Check(root != null && categoryRoot != null && IsInside(root, categoryRoot),
            "CATEGORY_GRID_INSIDE_CATALOG",
            DescribeRect(categoryRoot));
        Check(root != null && visiblePanelRect != null && IsInside(root, visiblePanelRect),
            "ITEM_GRID_INSIDE_CATALOG",
            DescribeRect(visiblePanelRect));
        Check(categoryRoot != null && visiblePanelRect != null
                && visiblePanelRect.anchoredPosition.x >= categoryRoot.rect.width,
            "CATALOG_PANELS_SIDE_BY_SIDE",
            $"categoryWidth={categoryRoot?.rect.width ?? -1f:0.##}; itemX={visiblePanelRect?.anchoredPosition.x ?? -1f:0.##}");
    }

    private static bool IsInside(RectTransform parent, RectTransform child)
    {
        Vector3[] parentCorners = new Vector3[4];
        Vector3[] childCorners = new Vector3[4];
        parent.GetWorldCorners(parentCorners);
        child.GetWorldCorners(childCorners);
        const float tolerance = 0.5f;
        return childCorners[0].x >= parentCorners[0].x - tolerance
            && childCorners[0].y >= parentCorners[0].y - tolerance
            && childCorners[2].x <= parentCorners[2].x + tolerance
            && childCorners[2].y <= parentCorners[2].y + tolerance;
    }

    private static string DescribeRect(RectTransform rect)
    {
        return rect == null
            ? "<null>"
            : $"position={rect.anchoredPosition}; size={rect.rect.size}";
    }

    private static void CaptureScreen(string path)
    {
        Texture2D capture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        capture.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0);
        capture.Apply();
        File.WriteAllBytes(path, capture.EncodeToPNG());
        UnityEngine.Object.Destroy(capture);
    }

    private static T ResolveFromGameplayScope<T>() where T : class
    {
        DungeonRuntimeLifetimeScope scope = UnityEngine.Object
            .FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate != null && candidate.Container != null);
        if (scope == null)
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

    private void Check(bool passed, string key, string detail)
    {
        report.Add($"{key}={(passed ? "PASS" : "FAIL")}; {detail}");
        if (!passed)
        {
            failures.Add(key + ": " + detail);
        }
    }

    private void Finish()
    {
        report.Add($"capturedErrors={capturedErrors.Count}; {Compact(capturedErrors)}");
        report.Add($"capturedWarnings={capturedWarnings.Count}; {Compact(capturedWarnings)}");
        bool passed = failures.Count == 0 && capturedErrors.Count == 0 && capturedWarnings.Count == 0;
        report.Add($"RESULT={(passed ? "PASS" : "FAIL")}; failures={failures.Count}; {Compact(failures)}");
        File.WriteAllText(BuildPlacementUxPlayModeVerifier.ReportPath, string.Join("\n", report));
        if (passed)
        {
            Debug.Log("Build placement UX verification passed. " + BuildPlacementUxPlayModeVerifier.ReportPath);
        }
        else
        {
            Debug.LogError("Build placement UX verification failed. " + BuildPlacementUxPlayModeVerifier.ReportPath);
        }

        RestoreInput();
        EditorApplication.ExitPlaymode();
    }

    private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning)
        {
            capturedWarnings.Add(condition);
        }
        else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            capturedErrors.Add(string.IsNullOrWhiteSpace(stackTrace) ? condition : condition + "\n" + stackTrace);
        }
    }

    private static string Compact(IEnumerable<string> values)
    {
        string value = string.Join(" | ", values.Where(item => !string.IsNullOrWhiteSpace(item)));
        return string.IsNullOrWhiteSpace(value) ? "<none>" : value.Replace("\n", " ");
    }
}
