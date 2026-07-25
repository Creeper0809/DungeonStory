using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public static class ModularFacilityPlayModeVerifier
{
    public const string ReportPath = "Temp/modular-facility-playmode-report.txt";
    public const string CatalogCapturePath = "Temp/modular-facility-catalog.png";
    public const string PlacementCapturePath = "Temp/modular-facility-placement.png";
    public const string CameraCapturePath = "Temp/modular-facility-camera.png";
    public const string RecipeCapturePath = "Temp/modular-facility-recipes.png";
    public const string RecipeCameraCapturePath = "Temp/modular-facility-recipes-camera.png";
    public const string EconomyCapturePath = "Temp/modular-facility-economy.png";

    [MenuItem("DungeonStory/Debug/Facilities/Run Modular Facility PlayMode Verification")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("Modular facility verification requires PlayMode.");
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<ModularFacilityPlayModeVerificationRunner>() != null)
        {
            Debug.LogWarning("Modular facility verification is already running.");
            return;
        }

        EditorApplication.ExecuteMenuItem("Window/General/Game");
        new GameObject("Modular Facility PlayMode Verification Runner")
            .AddComponent<ModularFacilityPlayModeVerificationRunner>();
    }
}

public sealed class ModularFacilityPlayModeVerificationRunner : MonoBehaviour
{
    private const int FirstModularId = ModularFacilityAssetBuilder.FirstBuildingId;
    private const int LastModularId = FirstModularId + 72;

    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> capturedErrors = new List<string>();
    private readonly List<string> capturedWarnings = new List<string>();
    private readonly List<BuildableObject> placedParts = new List<BuildableObject>();

    private DungeonStoryGridBuildingController controller;
    private GridConstructTab constructTab;
    private DungeonStoryGridGhostPresenter ghostPresenter;
    private GridUIManager gridUi;
    private BuildingSummaryInfo buildingSummary;
    private Grid grid;
    private GridBuildingPlacementService placementService;
    private Camera mainCamera;
    private Behaviour cameraMovementController;
    private Vector3 lockedCameraPosition;
    private Quaternion lockedCameraRotation;
    private bool cameraMovementWasEnabled;
    private bool cameraLockCaptured;
    private Mouse originalMouse;
    private Mouse verificationMouse;
    private GameData gameData;
    private int originalDay;
    private int originalMoney;
    private bool gameDataCaptured;
    private float originalTimeScale;
    private InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;
    private bool inputBehaviorCaptured;
    private bool cleanupComplete;

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Temp");
        Application.logMessageReceived += OnLogMessageReceived;
        ConfigureInput();

        yield return null;
        yield return null;
        yield return null;

        controller = UnityEngine.Object.FindFirstObjectByType<DungeonStoryGridBuildingController>();
        RestartBuildingControllerInput();
        constructTab = UnityEngine.Object.FindFirstObjectByType<GridConstructTab>(FindObjectsInactive.Include);
        ghostPresenter = UnityEngine.Object.FindFirstObjectByType<DungeonStoryGridGhostPresenter>();
        gridUi = UnityEngine.Object.FindFirstObjectByType<GridUIManager>(FindObjectsInactive.Include);
        buildingSummary = UnityEngine.Object.FindFirstObjectByType<BuildingSummaryInfo>(FindObjectsInactive.Include);
        mainCamera = Camera.main;
        LockCameraMovement();
        grid = controller != null ? controller.GridSystem?.grid : null;
        placementService = controller != null
            ? typeof(DungeonStoryGridBuildingController)
                .GetField("placementService", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(controller) as GridBuildingPlacementService
            : null;
        GameManager gameManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        gameData = gameManager != null ? gameManager.gameData : null;

        Check(controller != null && grid != null, "RUNTIME_GRID", "building controller and grid resolved");
        Check(placementService != null, "PLACEMENT_SERVICE", "runtime placement service resolved");
        Check(constructTab != null, "CONSTRUCT_TAB", "build catalog resolved");
        Check(ghostPresenter != null && gridUi != null, "PLACEMENT_UI", "ghost and placement grid resolved");
        Check(mainCamera != null, "MAIN_CAMERA", "main camera resolved");
        Check(mainCamera != null && cameraMovementController != null,
            "CAMERA_MOVEMENT_LOCK",
            cameraMovementController != null
                ? $"controller={cameraMovementController.GetType().Name}; position={lockedCameraPosition}"
                : "camera movement controller missing");
        Check(EventSystem.current != null, "EVENT_SYSTEM", "UI event system resolved");
        Check(gameData != null && gameData.day != null && gameData.holdingMoney != null,
            "GAME_DATA", "progression and economy data resolved");
        Check(Mouse.current == verificationMouse, "INPUT_MOUSE", "verification mouse is the active Input System mouse");
        if (controller == null || grid == null || placementService == null
            || constructTab == null || ghostPresenter == null
            || gridUi == null || mainCamera == null || EventSystem.current == null
            || verificationMouse == null || gameData == null || gameData.day == null
            || gameData.holdingMoney == null)
        {
            Finish();
            yield break;
        }

        originalDay = gameData.day.Value;
        originalMoney = gameData.holdingMoney.Value;
        gameDataCaptured = true;
        controller.SetGridModeNone();
        yield return VerifyEconomyProgressionThroughPointer();
        gameData.day.Value = 7;
        gameData.holdingMoney.Value = Mathf.Max(500000, originalMoney);
        yield return null;

        controller.SetGridModeNone();
        yield return null;
        VerifyInitialWorldMigration();
        yield return OpenCatalog();
        VerifyCatalogContract();

        BuildingSO hearth = LoadPart(1000, "D01");
        BuildingSO wallRack = LoadPart(1010, "D11");
        BuildingSO chandelier = LoadPart(1066, "E03");
        BuildingSO rug = LoadPart(1067, "E04");
        BuildingSO[] showcaseParts = { hearth, wallRack, chandelier, rug };
        Check(showcaseParts.All(part => part != null), "SHOWCASE_DATA", "four representative modular parts resolved");
        if (showcaseParts.Any(part => part == null))
        {
            Finish();
            yield break;
        }

        bool foundPosition = TryFindShowcasePosition(showcaseParts, out Vector2Int showcasePosition);
        Check(foundPosition, "SHOWCASE_POSITION", foundPosition ? $"cell={showcasePosition}" : "no visible shared placement cell");
        if (!foundPosition)
        {
            Finish();
            yield break;
        }

        Color32[] cameraBefore = CaptureCamera(mainCamera, null);

        yield return SelectPartThroughUi(hearth, BuildingCategory.Shop, captureCatalog: true);
        yield return PlaceSelectedPart(hearth, showcasePosition);
        yield return SelectPartThroughUi(wallRack, BuildingCategory.Shop, captureCatalog: false);
        yield return PlaceSelectedPart(wallRack, showcasePosition);
        yield return SelectPartThroughUi(chandelier, BuildingCategory.Resource, captureCatalog: false);
        yield return PlaceSelectedPart(chandelier, showcasePosition);
        yield return SelectPartThroughUi(rug, BuildingCategory.Resource, captureCatalog: false);
        yield return PlaceSelectedPart(rug, showcasePosition);

        VerifySharedGridSlots(showcasePosition, showcaseParts);
        VerifyPlacedRenderers(showcaseParts);

        controller.SetGridModeNone();
        yield return null;
        yield return null;
        Color32[] cameraAfter = CaptureCamera(mainCamera, ModularFacilityPlayModeVerifier.CameraCapturePath);
        int changedPixels = CountPixelDifferences(cameraBefore, cameraAfter);
        Check(changedPixels > 100, "CAMERA_VISUAL_CHANGE", $"changedPixels={changedPixels}");
        yield return CaptureScreen(ModularFacilityPlayModeVerifier.PlacementCapturePath, "PLACEMENT_SCREEN_CAPTURE");

        yield return VerifyAllCatalogPartsThroughPointer();
        VerifyCameraLock("AFTER_CATALOG_POINTERS");
        yield return VerifyAllLegacyRecipesThroughPointer();
        VerifyCameraLock("AFTER_RECIPE_POINTERS");

        Finish();
    }

    private IEnumerator VerifyEconomyProgressionThroughPointer()
    {
        BuildingSO phase1 = LoadPart(1000, "D01");
        BuildingSO phase2 = LoadPart(1005, "D06");
        BuildingSO phase3 = LoadPart(1049, "G06");
        Check(phase1 != null && phase2 != null && phase3 != null,
            "ECONOMY_PHASE_DATA",
            $"p1={phase1.GetFacilityCode()}; p2={phase2.GetFacilityCode()}; p3={phase3.GetFacilityCode()}");
        if (phase1 == null || phase2 == null || phase3 == null)
        {
            yield break;
        }

        gameData.day.Value = 1;
        gameData.holdingMoney.Value = 500000;
        yield return null;
        yield return OpenPartPanelThroughUi(phase2, "ECONOMY_DAY1_PHASE2");
        UIBuildingSelectButton phase1Button = GetCatalogButton(phase1);
        UIBuildingSelectButton phase2Button = GetCatalogButton(phase2);
        Check(phase1Button?.GetComponent<Button>()?.interactable == true,
            "ECONOMY_DAY1_PHASE1_ENABLED",
            $"day={gameData.day.Value}; part={phase1.objectName}");
        Check(phase2Button?.GetComponent<Button>()?.interactable == false,
            "ECONOMY_DAY1_PHASE2_DISABLED",
            $"day={gameData.day.Value}; part={phase2.objectName}");
        VerifyEconomyLabel(phase1, phase1Button, "PHASE1");
        VerifyEconomyLabel(phase2, phase2Button, "PHASE2");

        if (phase2Button != null)
        {
            yield return ClickMouse(GetScreenCenter(phase2Button.transform as RectTransform));
        }
        Check(controller.SelectedBuilding == null
                && controller.GridSystem.Mode == GridMode.None
                && constructTab.gameObject.activeInHierarchy,
            "ECONOMY_LOCKED_POINTER_REJECTED",
            $"selected={controller.SelectedBuilding?.id}; mode={controller.GridSystem.Mode}; catalog={constructTab.gameObject.activeInHierarchy}");

        yield return OpenPartPanelThroughUi(phase3, "ECONOMY_DAY1_PHASE3");
        UIBuildingSelectButton phase3Button = GetCatalogButton(phase3);
        Check(phase3Button?.GetComponent<Button>()?.interactable == false,
            "ECONOMY_DAY1_PHASE3_DISABLED",
            $"day={gameData.day.Value}; part={phase3.objectName}");
        VerifyEconomyLabel(phase3, phase3Button, "PHASE3");

        gameData.day.Value = 4;
        yield return null;
        yield return null;
        yield return OpenPartPanelThroughUi(phase2, "ECONOMY_DAY4_PHASE2");
        phase2Button = GetCatalogButton(phase2);
        Check(phase2Button?.GetComponent<Button>()?.interactable == true,
            "ECONOMY_DAY4_PHASE2_ENABLED",
            $"day={gameData.day.Value}; phase={FacilityProgression.GetCurrentPhase(gameData)}");

        yield return OpenPartPanelThroughUi(phase3, "ECONOMY_DAY4_PHASE3");
        phase3Button = GetCatalogButton(phase3);
        Check(phase3Button?.GetComponent<Button>()?.interactable == false,
            "ECONOMY_DAY4_PHASE3_DISABLED",
            $"day={gameData.day.Value}; phase={FacilityProgression.GetCurrentPhase(gameData)}");

        gameData.day.Value = 7;
        yield return null;
        yield return null;
        phase3Button = GetCatalogButton(phase3);
        Check(phase3Button?.GetComponent<Button>()?.interactable == true,
            "ECONOMY_DAY7_PHASE3_ENABLED",
            $"day={gameData.day.Value}; phase={FacilityProgression.GetCurrentPhase(gameData)}");
        constructTab.CloseTab();
        yield return null;

        gameData.holdingMoney.Value = 500000;
        yield return SelectPartThroughUi(phase2, phase2.category, captureCatalog: false);
        bool foundPosition = TryFindSelectedPlacementPosition(phase2, out Vector2Int position);
        Check(foundPosition,
            "ECONOMY_PLACEMENT_POSITION",
            foundPosition ? $"cell={position}" : "no clear pointer-visible cell");
        if (!foundPosition)
        {
            controller.SetGridModeNone();
            yield break;
        }

        int phase2Cost = phase2.GetConstructionCost();
        int poorMoney = Mathf.Max(0, phase2Cost - 1);
        gameData.holdingMoney.Value = poorMoney;
        yield return null;
        yield return null;
        Vector2 worldPoint = mainCamera.WorldToScreenPoint(GetCellCenter(grid, position));
        yield return MoveMouse(worldPoint, 0.15f);
        GridGhostObject ghost = ghostPresenter.GetComponent<GridGhostObject>();
        SpriteRenderer ghostRenderer = ghost != null
            ? ghost.GetComponentsInChildren<SpriteRenderer>(true)
                .FirstOrDefault(renderer => renderer != null && renderer.gameObject.activeInHierarchy && renderer.sprite != null)
            : null;
        bool blockedTint = ghostRenderer != null
            && ghostRenderer.color.r > ghostRenderer.color.g + 0.1f
            && ghostRenderer.color.r > ghostRenderer.color.b + 0.1f;
        Check(!controller.IsBuildableAt(position) && blockedTint,
            "ECONOMY_POOR_GHOST_BLOCKED",
            ghostRenderer != null
                ? $"buildable={controller.IsBuildableAt(position)}; color={ghostRenderer.color}"
                : "ghost renderer missing");

        int beforePoorClick = gameData.holdingMoney.Value;
        yield return ClickMouse(worldPoint);
        yield return null;
        yield return null;
        BuildableObject poorOccupant = grid.GetGridCell(position)?.GetOccupant(phase2.layer) as BuildableObject;
        string noticeText = GetActiveNoticeText();
        Check(poorOccupant == null
                && gameData.holdingMoney.Value == beforePoorClick
                && controller.GridSystem.Mode == GridMode.Build
                && controller.SelectedBuilding == phase2,
            "ECONOMY_POOR_PLACEMENT_REJECTED",
            $"occupant={poorOccupant?.id}; money={beforePoorClick}->{gameData.holdingMoney.Value}; "
            + $"mode={controller.GridSystem.Mode}; selected={controller.SelectedBuilding?.id}");
        Check(noticeText.Contains(phase2Cost.ToString(), StringComparison.Ordinal)
                && noticeText.Contains(poorMoney.ToString(), StringComparison.Ordinal),
            "ECONOMY_POOR_NOTICE",
            noticeText);
        yield return CaptureScreen(ModularFacilityPlayModeVerifier.EconomyCapturePath, "ECONOMY_SCREEN_CAPTURE");

        controller.SetGridModeNone();
        gameData.holdingMoney.Value = phase2Cost + 10;
        yield return SelectPartThroughUi(phase2, phase2.category, captureCatalog: false);
        int beforeSuccessfulBuild = gameData.holdingMoney.Value;
        yield return PlaceSelectedPart(phase2, position);
        BuildableObject placed = grid.GetGridCell(position)?.GetOccupant(phase2.layer) as BuildableObject;
        Check(placed != null && placed.id == phase2.id && gameData.holdingMoney.Value == 10,
            "ECONOMY_EXACT_DEDUCTION",
            $"money={beforeSuccessfulBuild}->{gameData.holdingMoney.Value}; cost={phase2Cost}");

        int beforeRefund = gameData.holdingMoney.Value;
        yield return DemolishPlacedPartThroughPointer(phase2, position);
        int expectedRefund = FacilityProgression.GetRefund(phase2);
        Check(grid.GetGridCell(position)?.GetOccupant(phase2.layer) == null
                && gameData.holdingMoney.Value == beforeRefund + expectedRefund,
            "ECONOMY_EXACT_POINTER_REFUND",
            $"money={beforeRefund}->{gameData.holdingMoney.Value}; refund={expectedRefund}");

        controller.SetGridModeNone();
        gameData.day.Value = 7;
        gameData.holdingMoney.Value = 500000;
        yield return null;
    }

    private IEnumerator OpenPartPanelThroughUi(BuildingSO part, string suffix)
    {
        if (!constructTab.gameObject.activeInHierarchy)
        {
            yield return OpenCatalog(suffix);
        }

        UITab panel = constructTab.selectButtonPanelList
            .FirstOrDefault(item => item != null && item.id == (int)part.category);
        Check(panel != null, "ECONOMY_PANEL_" + suffix, $"category={part.category}");
        if (panel == null)
        {
            yield break;
        }

        if (!panel.gameObject.activeInHierarchy)
        {
            Button categoryButton = FindCategoryButton(GetCategoryLabel(part.category));
            Check(categoryButton != null, "ECONOMY_CATEGORY_" + suffix, $"category={part.category}");
            if (categoryButton == null)
            {
                yield break;
            }
            yield return ClickMouse(GetScreenCenter(categoryButton.transform as RectTransform));
        }

        UIBuildingSelectButton item = GetCatalogButton(part);
        ScrollRect scroll = panel.GetComponent<ScrollRect>();
        BringIntoView(scroll, item != null ? item.transform as RectTransform : null);
        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return null;
        Check(item != null && IsInsideViewport(item.transform as RectTransform, panel.transform as RectTransform),
            "ECONOMY_ITEM_VISIBLE_" + suffix,
            item != null ? $"part={part.objectName}" : "item missing");
    }

    private UIBuildingSelectButton GetCatalogButton(BuildingSO part)
    {
        return part == null
            ? null
            : constructTab.GetComponentsInChildren<UIBuildingSelectButton>(true)
                .FirstOrDefault(button => button != null && button.id == part.id);
    }

    private void VerifyEconomyLabel(BuildingSO part, UIBuildingSelectButton item, string suffix)
    {
        TMP_Text label = item != null
            ? item.transform.Find("Label")?.GetComponent<TMP_Text>()
            : null;
        bool valid = label != null
            && label.text.Contains(part.GetConstructionCost().ToString(), StringComparison.Ordinal)
            && label.text.Contains($"P{part.GetUnlockPhase()}", StringComparison.Ordinal);
        Check(valid,
            "ECONOMY_LABEL_" + suffix,
            label != null ? label.text.Replace('\n', ' ') : "label missing");
    }

    private static string GetActiveNoticeText()
    {
        NoticeFeed feed = UnityEngine.Object.FindFirstObjectByType<NoticeFeed>(FindObjectsInactive.Exclude);
        return feed == null
            ? string.Empty
            : string.Join(" | ", feed.GetComponentsInChildren<TMP_Text>(false)
                .Where(text => text != null && text.gameObject.activeInHierarchy)
                .Select(text => text.text));
    }

    private IEnumerator VerifyAllLegacyRecipesThroughPointer()
    {
        BuildingSO[] legacyRecipes = AssetDatabase.FindAssets("t:BuildingSO", new[]
            {
                "Assets/Resources/SO/Building"
            })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(ModularFacilityInitialPlacementMigrator.IsLegacyMonolith)
            .Distinct()
            .OrderBy(part => part.name, StringComparer.Ordinal)
            .ToArray();
        Dictionary<int, BuildingSO> modularById = AssetDatabase.FindAssets("t:BuildingSO", new[]
            {
                "Assets/Resources/SO/Building/Modular"
            })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(part => part != null)
            .ToDictionary(part => part.id);
        Check(legacyRecipes.Length == 21, "RECIPE_DATA_COUNT", $"recipes={legacyRecipes.Length}");

        bool foundSandboxRoom = TryFindRecipeSandboxRoom(
            legacyRecipes,
            modularById,
            out RoomInstance sandboxRoom,
            out Vector2Int plannedAnchor);
        Check(foundSandboxRoom, "RECIPE_SANDBOX_ROOM",
            foundSandboxRoom
                ? $"bounds={sandboxRoom.Bounds}; cells={sandboxRoom.Cells.Count}; furniture={sandboxRoom.Furniture.Count}; plannedAnchor={plannedAnchor}"
                : "no usable formal room can fit every legacy recipe");
        if (!foundSandboxRoom)
        {
            yield break;
        }

        BuildableObject[] originalFurniture = GetRecipeSandboxOccupants(sandboxRoom);
        int clearedOriginalParts = DestroyThroughPlacementService(originalFurniture, "RECIPE_SANDBOX_CLEAR");
        controller.GridSystem.NotifyGridObjectChanged();
        yield return null;
        yield return null;
        Check(clearedOriginalParts == originalFurniture.Length,
            "RECIPE_SANDBOX_CLEARED",
            $"cleared={clearedOriginalParts}/{originalFurniture.Length}");

        bool foundAnchor = TryFindRecipeAnchor(legacyRecipes, modularById, sandboxRoom, out Vector2Int anchor);
        Check(foundAnchor, "RECIPE_SANDBOX_ANCHOR",
            foundAnchor ? $"anchor={anchor}; room={sandboxRoom.Bounds}" : "no pointer-visible common anchor");
        if (!foundAnchor)
        {
            yield break;
        }

        int passedRecipes = 0;
        for (int recipeIndex = 0; recipeIndex < legacyRecipes.Length; recipeIndex++)
        {
            BuildingSO legacy = legacyRecipes[recipeIndex];
            bool expanded = ModularFacilityInitialPlacementMigrator.TryExpand(
                new InitialBuildInfo { Position = anchor, Building = legacy },
                id => modularById.TryGetValue(id, out BuildingSO data) ? data : null,
                out IReadOnlyList<InitialBuildInfo> recipe);
            Check(expanded && recipe.Count >= 3,
                "RECIPE_EXPANDED_" + legacy.name,
                expanded ? $"parts={recipe.Count}" : "expansion failed");
            if (!expanded)
            {
                continue;
            }

            List<BuildableObject> instances = new List<BuildableObject>();
            int pointerPlaced = 0;
            foreach (InitialBuildInfo placement in recipe)
            {
                yield return SelectPartThroughUi(placement.Building, placement.Building.category, captureCatalog: false);
                if (controller.SelectedBuilding == null || controller.SelectedBuilding.id != placement.Building.id)
                {
                    controller.SetGridModeNone();
                    break;
                }

                yield return PlaceSelectedPart(placement.Building, placement.Position);
                BuildableObject instance = grid.GetGridCell(placement.Position)
                    ?.GetOccupant(placement.Building.layer) as BuildableObject;
                if (instance == null || instance.id != placement.Building.id)
                {
                    break;
                }

                pointerPlaced++;
                instances.Add(instance);
            }

            RoomInstance room = RoomRegistry.GetLayout(grid).Rooms.FirstOrDefault(candidate =>
                candidate != null && instances.Any(candidate.ContainsPart));
            FacilityRole facilityRoles = instances.Aggregate(
                FacilityRole.None,
                (roles, part) => roles | (part.Facility?.roles ?? FacilityRole.None));
            FacilityRole expectedRoles = (facilityRoles);
            List<IGridOccupant> reachable = room != null && room.Doors.Count > 0
                ? grid.GetAllReachableOccupants(room.Doors[0].centerPos)
                : new List<IGridOccupant>();
            BuildableObject[] visitorCores = instances
                .Where(part => part.Facility != null && part.Facility.IsVisitorFacility)
                .ToArray();
            BuildableObject[] roleParts = instances
                .Where(part => part.Facility != null && part.Facility.roles != FacilityRole.None)
                .ToArray();
            BuildableObject[] supportParts = instances
                .Where(part => part.Facility == null || part.Facility.roles == FacilityRole.None)
                .ToArray();
            bool allVisitable = visitorCores.All(part => part.CanVisit(null, out _));
            bool roleMembership = room != null && roleParts.All(room.ContainsPart);
            bool supportMembership = room != null && supportParts.All(part =>
                part.buildPoses.Any(room.ContainsCell));
            bool mountedValid = instances
                .Where(part => part.BuildingData.UsesIndependentRenderer)
                .All(part => part.GetComponentInChildren<SpriteRenderer>() is SpriteRenderer renderer
                    && renderer.enabled
                    && renderer.sprite == part.BuildingData.sprite
                    && part.buildPoses.All(cell => ReferenceEquals(
                        grid.GetGridCell(cell).GetOccupant(part.BuildingData.layer),
                        part)));
            bool roomPassed = pointerPlaced == recipe.Count
                && room != null
                && room.IsUsable
                && !room.IsSelfContained
                && room.HasDoor
                && expectedRoles != FacilityRole.None
                && room.Roles == expectedRoles
                && roleMembership
                && supportMembership
                && instances.All(reachable.Contains)
                && visitorCores.Length > 0
                && allVisitable
                && mountedValid;

            BuildableObject selectedCore = visitorCores.FirstOrDefault();
            bool worldSelected = false;
            if (selectedCore != null)
            {
                yield return SelectPlacedPartThroughPointer(
                    selectedCore,
                    selectedCore.centerPos,
                    value => worldSelected = value);
            }
            roomPassed &= worldSelected;

            string roomName = room != null
                ? RoomEnvironmentPresentation.GetRoomName(room.Roles)
                : string.Empty;
            Check(roomPassed,
                "RECIPE_POINTER_" + legacy.name,
                $"placed={pointerPlaced}/{recipe.Count}; room={roomName}; roles={room?.Roles}; "
                + $"reachable={instances.Count(reachable.Contains)}/{instances.Count}; "
                + $"visitable={visitorCores.Length}; mounted={instances.Count(part => part.BuildingData.UsesIndependentRenderer)}; "
                + $"roleMembership={roleMembership}; supportMembership={supportMembership}; "
                + $"allVisitable={allVisitable}; mountedValid={mountedValid}; selected={worldSelected}");
            if (roomPassed)
            {
                passedRecipes++;
            }

            if (recipeIndex == legacyRecipes.Length - 1)
            {
                Color32[] cameraPixels = CaptureCamera(mainCamera, ModularFacilityPlayModeVerifier.RecipeCameraCapturePath);
                Check(cameraPixels.Length > 0 && cameraPixels.Any(pixel =>
                        pixel.a > 0 && (pixel.r > 8 || pixel.g > 8 || pixel.b > 8)),
                    "RECIPE_CAMERA_CAPTURE",
                    $"path={ModularFacilityPlayModeVerifier.RecipeCameraCapturePath}; pixels={cameraPixels.Length}");
                yield return CaptureScreen(
                    ModularFacilityPlayModeVerifier.RecipeCapturePath,
                    "RECIPE_SCREEN_CAPTURE");
            }

            int removed = DestroyThroughPlacementService(instances, "RECIPE_CLEANUP_" + legacy.name);
            controller.GridSystem.NotifyGridObjectChanged();
            yield return null;
            yield return null;
            Check(removed == instances.Count,
                "RECIPE_CLEARED_" + legacy.name,
                $"removed={removed}/{instances.Count}");
        }

        Check(passedRecipes == 21, "RECIPE_POINTER_TOTAL", $"passed={passedRecipes}/21");
    }

    private bool TryFindRecipeAnchor(
        IReadOnlyList<BuildingSO> legacyRecipes,
        IReadOnlyDictionary<int, BuildingSO> modularById,
        RoomInstance room,
        out Vector2Int anchor)
    {
        return TryFindRecipeAnchor(
            legacyRecipes,
            modularById,
            room,
            ignoredRoomFurniture: null,
            requireRuntimeBuildable: true,
            out anchor);
    }

    private bool TryFindRecipeSandboxRoom(
        IReadOnlyList<BuildingSO> legacyRecipes,
        IReadOnlyDictionary<int, BuildingSO> modularById,
        out RoomInstance sandboxRoom,
        out Vector2Int anchor)
    {
        RoomLayout layout = RoomRegistry.GetLayout(grid);
        IEnumerable<RoomInstance> rooms = layout != null
            ? layout.Rooms
            : Enumerable.Empty<RoomInstance>();
        foreach (RoomInstance candidate in rooms
            .Where(room => room != null && room.IsUsable && !room.IsSelfContained)
            .OrderBy(room => room.Furniture.Count == 0 ? 0 : 1)
            .ThenByDescending(room => room.Bounds.yMin)
            .ThenByDescending(room => room.Cells.Count))
        {
            BuildableObject[] ignoredFurniture = GetRecipeSandboxOccupants(candidate);
            if (TryFindRecipeAnchor(
                    legacyRecipes,
                    modularById,
                    candidate,
                    ignoredFurniture,
                    requireRuntimeBuildable: false,
                    out anchor))
            {
                sandboxRoom = candidate;
                return true;
            }
        }

        sandboxRoom = null;
        anchor = default;
        return false;
    }

    private bool TryFindRecipeAnchor(
        IReadOnlyList<BuildingSO> legacyRecipes,
        IReadOnlyDictionary<int, BuildingSO> modularById,
        RoomInstance room,
        IReadOnlyCollection<BuildableObject> ignoredRoomFurniture,
        bool requireRuntimeBuildable,
        out Vector2Int anchor)
    {
        HashSet<Vector2Int> roomCells = new HashSet<Vector2Int>(room.Cells);
        HashSet<BuildableObject> ignoredFurniture = ignoredRoomFurniture != null
            ? new HashSet<BuildableObject>(ignoredRoomFurniture)
            : null;
        BuildingPlacementValidator validator = new BuildingPlacementValidator(
            new GridPlacementValidator(),
            () => new BuildingConditionContext(gameData));
        IEnumerable<int> candidateXs = Enumerable.Range(room.Bounds.xMin, room.Bounds.width)
            .OrderBy(x => Mathf.Abs((x + 0.5f) - room.Bounds.center.x));
        foreach (int x in candidateXs)
        {
            Vector2Int candidate = new Vector2Int(x, room.Bounds.yMin);
            bool valid = true;
            foreach (BuildingSO legacy in legacyRecipes)
            {
                if (!ModularFacilityInitialPlacementMigrator.TryExpand(
                        new InitialBuildInfo { Position = candidate, Building = legacy },
                        id => modularById.TryGetValue(id, out BuildingSO data) ? data : null,
                        out IReadOnlyList<InitialBuildInfo> recipe))
                {
                    valid = false;
                    break;
                }

                foreach (InitialBuildInfo placement in recipe)
                {
                    IReadOnlyList<Vector2Int> footprint = placement.Building.GetGridPosList(placement.Position);
                    Vector2 screenPoint = mainCamera.WorldToScreenPoint(GetCellCenter(grid, placement.Position));
                    if (footprint.Any(cell => !roomCells.Contains(cell)
                            || IsRecipeCellBlocked(cell, placement.Building.layer, ignoredFurniture))
                        || (requireRuntimeBuildable && !validator.CanBuild(grid, placement.Building, placement.Position, out _))
                        || !IsInsideScreen(screenPoint)
                        || screenPoint.x < 32f
                        || screenPoint.x > Screen.width - 32f
                        || IsScreenPointOverUi(screenPoint))
                    {
                        valid = false;
                        break;
                    }
                }

                if (!valid)
                {
                    break;
                }
            }

            if (valid)
            {
                anchor = candidate;
                return true;
            }
        }

        anchor = default;
        return false;
    }

    private BuildableObject[] GetRecipeSandboxOccupants(RoomInstance room)
    {
        if (room == null)
        {
            return Array.Empty<BuildableObject>();
        }

        return room.Cells
            .Select(cell => grid.GetGridCell(cell))
            .Where(cell => cell != null)
            .SelectMany(cell => cell.GetAllOccupants())
            .OfType<BuildableObject>()
            .Where(part => part != null
                && !part.isDestroy
                && part.BuildingData != null
                && part.id >= FirstModularId
                && part.id <= LastModularId)
            .Distinct()
            .ToArray();
    }

    private bool IsRecipeCellBlocked(
        Vector2Int cell,
        GridLayer layer,
        IReadOnlyCollection<BuildableObject> ignoredFurniture)
    {
        GridCell target = grid.GetGridCell(cell);
        if (target == null || target.GetOccupant(GridLayer.Character) != null)
        {
            return true;
        }

        IGridOccupant occupant = target.GetOccupant(layer);
        if (occupant == null)
        {
            return false;
        }

        BuildableObject buildable = occupant as BuildableObject;
        return buildable == null || ignoredFurniture == null || !ignoredFurniture.Contains(buildable);
    }

    private int DestroyThroughPlacementService(IEnumerable<BuildableObject> buildings, string keyPrefix)
    {
        int removed = 0;
        foreach (BuildableObject building in buildings
            .Where(part => part != null && !part.isDestroy)
            .Distinct()
            .ToArray())
        {
            if (placementService.TryDestroyBuilding(building, out BuildingSO data, out string error))
            {
                removed++;
            }
            else
            {
                Check(false, keyPrefix + "_" + building.id,
                    $"{data?.objectName ?? building.name}: {error}");
            }
        }

        return removed;
    }

    private void LockCameraMovement()
    {
        if (mainCamera == null)
        {
            return;
        }

        lockedCameraPosition = mainCamera.transform.position;
        lockedCameraRotation = mainCamera.transform.rotation;
        cameraMovementController = mainCamera.GetComponent<CameraManager>();
        if (cameraMovementController != null)
        {
            cameraMovementWasEnabled = cameraMovementController.enabled;
            cameraMovementController.enabled = false;
        }

        cameraLockCaptured = true;
    }

    private void VerifyCameraLock(string key)
    {
        if (!cameraLockCaptured || mainCamera == null)
        {
            Check(false, "CAMERA_STABLE_" + key, "camera lock was not captured");
            return;
        }

        float positionDelta = Vector3.Distance(mainCamera.transform.position, lockedCameraPosition);
        float rotationDelta = Quaternion.Angle(mainCamera.transform.rotation, lockedCameraRotation);
        Check(positionDelta <= 0.001f && rotationDelta <= 0.001f,
            "CAMERA_STABLE_" + key,
            $"positionDelta={positionDelta:F4}; rotationDelta={rotationDelta:F4}");
        if (positionDelta > 0.001f || rotationDelta > 0.001f)
        {
            mainCamera.transform.SetPositionAndRotation(lockedCameraPosition, lockedCameraRotation);
        }
    }

    private IEnumerator VerifyAllCatalogPartsThroughPointer()
    {
        BuildingSO[] allParts = AssetDatabase.FindAssets("t:BuildingSO", new[]
            {
                "Assets/Resources/SO/Building/Modular"
            })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(part => part != null && part.id >= FirstModularId && part.id <= LastModularId)
            .OrderBy(part => part.id)
            .ToArray();
        Check(allParts.Length == 73, "EXHAUSTIVE_DATA_COUNT", $"parts={allParts.Length}");

        int placed = 0;
        int selected = 0;
        int demolished = 0;
        foreach (BuildingSO part in allParts)
        {
            yield return SelectPartThroughUi(part, part.category, captureCatalog: false);
            if (controller.SelectedBuilding == null || controller.SelectedBuilding.id != part.id)
            {
                controller.SetGridModeNone();
                continue;
            }

            bool foundPosition = TryFindSelectedPlacementPosition(part, out Vector2Int position);
            Check(foundPosition, "EXHAUSTIVE_POSITION_" + part.id,
                foundPosition ? $"cell={position}; layer={part.layer}" : "no clear visible placement cell");
            if (!foundPosition)
            {
                controller.SetGridModeNone();
                continue;
            }

            int beforePlacementMoney = gameData.holdingMoney.Value;
            yield return PlaceSelectedPart(part, position);
            BuildableObject instance = grid.GetGridCell(position)?.GetOccupant(part.layer) as BuildableObject;
            if (instance == null || instance.id != part.id)
            {
                controller.SetGridModeNone();
                continue;
            }

            placed++;
            int expectedAfterPlacement = beforePlacementMoney - part.GetConstructionCost();
            Check(gameData.holdingMoney.Value == expectedAfterPlacement,
                "EXHAUSTIVE_COST_" + part.id,
                $"money={beforePlacementMoney}->{gameData.holdingMoney.Value}; cost={part.GetConstructionCost()}");
            Check(instance.BuildingData != null
                    && instance.BuildingData.GetFacilityCode() == part.GetFacilityCode()
                    && instance.buildPoses.SequenceEqual(part.GetGridPosList(position)),
                "EXHAUSTIVE_RUNTIME_" + part.id,
                $"code={instance.BuildingData?.GetFacilityCode()}; footprint={instance.buildPoses.Count}; layer={part.layer}");

            bool selectionPassed = false;
            yield return SelectPlacedPartThroughPointer(instance, position, value => selectionPassed = value);
            if (selectionPassed)
            {
                selected++;
            }

            int beforeDemolitionMoney = gameData.holdingMoney.Value;
            yield return DemolishPlacedPartThroughPointer(part, position);
            BuildableObject remaining = grid.GetGridCell(position)?.GetOccupant(part.layer) as BuildableObject;
            bool removed = remaining == null || remaining.id != part.id;
            Check(removed, "EXHAUSTIVE_REMOVED_" + part.id,
                removed ? $"layer {part.layer} cleared" : $"remaining={remaining.id}");
            if (removed)
            {
                demolished++;
            }

            int refund = FacilityProgression.GetRefund(part);
            Check(gameData.holdingMoney.Value == beforeDemolitionMoney + refund,
                "EXHAUSTIVE_REFUND_" + part.id,
                $"money={beforeDemolitionMoney}->{gameData.holdingMoney.Value}; refund={refund}");
        }

        Check(placed == 73, "EXHAUSTIVE_PLACED_TOTAL", $"placed={placed}/73");
        Check(selected == 73, "EXHAUSTIVE_SELECTED_TOTAL", $"selected={selected}/73");
        Check(demolished == 73, "EXHAUSTIVE_DEMOLISHED_TOTAL", $"demolished={demolished}/73");
    }

    private IEnumerator SelectPlacedPartThroughPointer(
        BuildableObject instance,
        Vector2Int position,
        Action<bool> completed)
    {
        bool clicked = false;
        void OnClicked(BuildableObject clickedBuilding)
        {
            if (clickedBuilding == instance)
            {
                clicked = true;
            }
        }

        instance.OnBuildingClicked += OnClicked;
        Vector2 screenPoint = mainCamera.WorldToScreenPoint(GetCellCenter(grid, position));
        yield return MoveMouse(screenPoint, 0.05f);
        yield return ClickMouse(screenPoint);
        yield return null;
        yield return null;
        instance.OnBuildingClicked -= OnClicked;

        bool panelMatches = IsBuildingInfoOpenFor(instance, out string panelState);
        Check(clicked, "EXHAUSTIVE_WORLD_CLICK_" + instance.id,
            clicked ? instance.BuildingData.objectName : "OnBuildingClicked was not raised");
        Check(panelMatches, "EXHAUSTIVE_INFO_" + instance.id, panelState);
        completed?.Invoke(clicked && panelMatches);

        CloseBuildingInfoPanels();
        yield return new WaitForSecondsRealtime(0.12f);
        yield return null;
    }

    private bool IsBuildingInfoOpenFor(BuildableObject instance, out string state)
    {
        string expectedName = instance != null && instance.BuildingData != null
            ? instance.BuildingData.objectName
            : string.Empty;
        UIBuildingInfo detailInfo = gridUi != null ? gridUi.buildingInfoUI : null;
        bool detailMatches = detailInfo != null
            && detailInfo.gameObject.activeInHierarchy
            && detailInfo.nameText != null
            && detailInfo.nameText.text == expectedName;
        bool summaryMatches = buildingSummary != null
            && buildingSummary.UI != null
            && buildingSummary.UI.activeInHierarchy
            && buildingSummary.objectName != null
            && buildingSummary.objectName.text == expectedName;
        state = $"detailActive={detailInfo != null && detailInfo.gameObject.activeInHierarchy}; "
            + $"detailName={detailInfo?.nameText?.text ?? "<null>"}; "
            + $"summaryActive={buildingSummary != null && buildingSummary.UI != null && buildingSummary.UI.activeInHierarchy}; "
            + $"summaryName={buildingSummary?.objectName?.text ?? "<null>"}";
        return detailMatches || summaryMatches;
    }

    private void CloseBuildingInfoPanels()
    {
        if (gridUi != null && gridUi.buildingInfoUI != null)
        {
            gridUi.buildingInfoUI.CloseDispaly();
        }

        buildingSummary?.OnClose();
    }

    private IEnumerator DemolishPlacedPartThroughPointer(BuildingSO part, Vector2Int position)
    {
        controller.SetDestroyMode();
        Check(controller.GridSystem.Mode == GridMode.Destory,
            "EXHAUSTIVE_DESTROY_MODE_" + part.id,
            $"mode={controller.GridSystem.Mode}");
        Vector2 screenPoint = mainCamera.WorldToScreenPoint(GetCellCenter(grid, position));
        yield return MoveMouse(screenPoint, 0.05f);
        yield return ClickMouse(screenPoint);
        yield return null;
        yield return null;
        controller.SetGridModeNone();
    }

    private bool TryFindSelectedPlacementPosition(BuildingSO part, out Vector2Int position)
    {
        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 2; x < grid.width - 2; x++)
            {
                Vector2Int candidate = new Vector2Int(x, y);
                IReadOnlyList<Vector2Int> footprint = part.GetGridPosList(candidate);
                if (footprint.Any(cell =>
                    {
                        GridCell target = grid.GetGridCell(cell);
                        return target == null
                            || target.GetOccupant(GridLayer.Character) != null
                            || target.GetOccupant(GridLayer.Building) != null
                            || target.GetOccupant(GridLayer.WallFixture) != null
                            || target.GetOccupant(GridLayer.CeilingFixture) != null
                            || target.GetOccupant(GridLayer.FloorOverlay) != null;
                    }))
                {
                    continue;
                }

                Vector2 screenPoint = mainCamera.WorldToScreenPoint(GetCellCenter(grid, candidate));
                if (!IsInsideScreen(screenPoint)
                    || IsScreenPointOverUi(screenPoint)
                    || !controller.IsBuildableAt(candidate))
                {
                    continue;
                }

                position = candidate;
                return true;
            }
        }

        position = default;
        return false;
    }

    private void ConfigureInput()
    {
        originalTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        originalEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        inputBehaviorCaptured = true;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        originalMouse = Mouse.current;
        if (originalMouse != null)
        {
            InputSystem.DisableDevice(originalMouse);
        }

        verificationMouse = InputSystem.AddDevice<Mouse>("ModularFacilityVerificationMouse");
        EnsureVerificationMouseReady();
        RestartBuildingControllerInput();
    }

    private void RestartBuildingControllerInput()
    {
        if (controller == null || !controller.enabled)
        {
            return;
        }

        controller.enabled = false;
        controller.enabled = true;
    }

    private void VerifyCatalogContract()
    {
        UIBuildingSelectButton[] modularButtons = constructTab
            .GetComponentsInChildren<UIBuildingSelectButton>(true)
            .Where(button => button != null && button.id >= FirstModularId && button.id <= LastModularId)
            .OrderBy(button => button.id)
            .ToArray();
        Check(modularButtons.Length == 73, "CATALOG_ALL_PARTS", $"modularButtons={modularButtons.Length}");
        Check(modularButtons.Select(button => button.id).Distinct().Count() == 73,
            "CATALOG_UNIQUE_IDS", "all modular button ids are unique");
        Check(modularButtons.All(button => button.GetComponent<Button>()?.interactable == true),
            "CATALOG_BUTTONS_INTERACTABLE", "all modular buttons are interactable");
        Check(modularButtons.All(button => button.transform.Find("Label")?.GetComponent<TMP_Text>() is TMP_Text text
                && !string.IsNullOrWhiteSpace(text.text)),
            "CATALOG_NAMES_VISIBLE", "all modular buttons have a visible name label");

        string[] legacyPaths =
        {
            "Assets/Resources/SO/Building/P1/P1_GeneralStore.asset",
            "Assets/Resources/SO/Building/P1/P1_ResearchLab.asset",
            "Assets/Resources/SO/Building/P1/P1_Warehouse.asset",
            "Assets/Resources/SO/Building/P1/P1_Washroom.asset"
        };
        bool legacyHidden = legacyPaths
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(asset => asset != null)
            .All(asset => !asset.unlocked
                && constructTab.GetComponentsInChildren<UIBuildingSelectButton>(true)
                    .All(button => button == null || button.id != asset.id));
        Check(legacyHidden, "LEGACY_MONOLITHS_HIDDEN", "representative room-sized legacy assets are absent from the catalog");

        foreach (UITab panel in constructTab.selectButtonPanelList.Where(panel => panel != null))
        {
            ScrollRect scroll = panel.GetComponent<ScrollRect>();
            Check(scroll != null && scroll.horizontal && !scroll.vertical && scroll.content != null,
                "CATEGORY_HORIZONTAL_SCROLL_" + panel.id,
                scroll != null && scroll.content != null
                    ? $"viewport={((RectTransform)panel.transform).rect.width:0.#}; content={scroll.content.rect.width:0.#}"
                    : "scroll missing");
        }
    }

    private void VerifyInitialWorldMigration()
    {
        BuildableObject[] worldBuildings = grid.FindAllOccupants(null)
            .OfType<BuildableObject>()
            .Where(building => building != null && !building.isDestroy && building.BuildingData != null)
            .Distinct()
            .ToArray();
        BuildableObject[] legacy = worldBuildings
            .Where(building => ModularFacilityInitialPlacementMigrator.IsLegacyMonolith(building.BuildingData))
            .ToArray();
        BuildableObject[] modular = worldBuildings
            .Where(building => building.id >= FirstModularId && building.id <= LastModularId)
            .ToArray();
        Check(legacy.Length == 0,
            "INITIAL_WORLD_NO_MONOLITHS",
            legacy.Length == 0
                ? "legacy room-sized initial placements were expanded"
                : string.Join(", ", legacy.Select(building => building.BuildingData.name)));
        Check(modular.Length >= 20,
            "INITIAL_WORLD_MODULAR_PARTS",
            $"modularInitialParts={modular.Length}");

        RoomInstance[] rooms = RoomRegistry.GetLayout(grid)?.Rooms
            ?.Where(room => room != null && !room.IsSelfContained)
            .ToArray() ?? Array.Empty<RoomInstance>();
        int usableRooms = rooms.Count(room => room.IsUsable);
        int furnishedRooms = rooms.Count(room => room.IsUsable && room.Furniture.Count > 0);
        int interiorDoorCount = rooms
            .SelectMany(room => room.Doors)
            .Where(door => door != null && door.BuildingData != null && door.BuildingData.IsInteriorDoor)
            .Distinct()
            .Count();
        Check(rooms.Length >= 10 && usableRooms >= 8 && furnishedRooms >= 8 && interiorDoorCount >= 5,
            "INITIAL_WORLD_ROOM_SHELLS",
            $"rooms={rooms.Length}; usable={usableRooms}; furnished={furnishedRooms}; interiorDoors={interiorDoorCount}");
    }

    private IEnumerator SelectPartThroughUi(
        BuildingSO part,
        BuildingCategory category,
        bool captureCatalog)
    {
        if (!constructTab.gameObject.activeInHierarchy)
        {
            yield return OpenCatalog(part.id.ToString());
        }

        Check(constructTab.gameObject.activeInHierarchy,
            "CATALOG_OPEN_" + part.id,
            $"catalog active for {part.objectName}");

        UITab panel = constructTab.selectButtonPanelList
            .FirstOrDefault(item => item != null && item.id == (int)category);
        if (panel == null)
        {
            Check(false, "CATEGORY_PANEL_" + part.id, $"category={category} missing");
            yield break;
        }

        if (!panel.gameObject.activeInHierarchy)
        {
            Button categoryButton = FindCategoryButton(GetCategoryLabel(category));
            Check(categoryButton != null, "CATEGORY_BUTTON_" + part.id, $"category={category}");
            if (categoryButton == null)
            {
                yield break;
            }

            yield return ClickMouse(GetScreenCenter(categoryButton.transform as RectTransform));
        }

        Check(panel.gameObject.activeInHierarchy,
            "CATEGORY_OPEN_" + part.id,
            $"category={category}; items={panel.GetComponentsInChildren<UIBuildingSelectButton>(false).Length}");
        UIBuildingSelectButton item = panel.GetComponentsInChildren<UIBuildingSelectButton>(true)
            .FirstOrDefault(button => button != null && button.id == part.id);
        Check(item != null, "PART_BUTTON_" + part.id, part.objectName);
        if (item == null)
        {
            yield break;
        }

        ScrollRect scroll = panel.GetComponent<ScrollRect>();
        BringIntoView(scroll, item.transform as RectTransform);
        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return null;

        Vector2 itemPoint = GetScreenCenter(item.transform as RectTransform);
        Check(IsInsideScreen(itemPoint) && IsInsideViewport(item.transform as RectTransform, panel.transform as RectTransform),
            "PART_BUTTON_VISIBLE_" + part.id,
            $"screen={itemPoint}");
        if (captureCatalog)
        {
            yield return CaptureScreen(ModularFacilityPlayModeVerifier.CatalogCapturePath, "CATALOG_SCREEN_CAPTURE");
        }

        yield return ClickMouse(itemPoint);
        Check(controller.GridSystem.Mode == GridMode.Build,
            "BUILD_MODE_" + part.id,
            $"mode={controller.GridSystem.Mode}");
        Check(controller.SelectedBuilding != null && controller.SelectedBuilding.id == part.id,
            "PART_SELECTED_" + part.id,
            controller.SelectedBuilding != null ? $"selected={controller.SelectedBuilding.id}" : "selection missing");
        Check(!constructTab.gameObject.activeInHierarchy,
            "CATALOG_COLLAPSED_" + part.id,
            "catalog collapses after selecting a part");
        GridGhostObject ghost = ghostPresenter.GetComponent<GridGhostObject>();
        Check(ghost != null && !ghost.IsHidden,
            "GHOST_VISIBLE_" + part.id,
            ghost != null ? $"hidden={ghost.IsHidden}" : "ghost missing");
        Check(gridUi.IsGridVisible && gridUi.BuildableCellCount > 0,
            "PLACEMENT_GRID_" + part.id,
            $"visible={gridUi.IsGridVisible}; buildable={gridUi.BuildableCellCount}");
    }

    private IEnumerator PlaceSelectedPart(BuildingSO part, Vector2Int position)
    {
        Check(controller.SelectedBuilding != null && controller.SelectedBuilding.id == part.id,
            "SELECTION_READY_" + part.id,
            part.objectName);
        Check(controller.IsBuildableAt(position),
            "TARGET_BUILDABLE_" + part.id,
            $"part={part.objectName}; cell={position}");

        Vector2 worldPoint = mainCamera.WorldToScreenPoint(GetCellCenter(grid, position));
        Check(IsInsideScreen(worldPoint) && !IsScreenPointOverUi(worldPoint),
            "WORLD_POINTER_" + part.id,
            $"screen={worldPoint}");
        yield return MoveMouse(worldPoint, 0.15f);

        GridGhostObject ghost = ghostPresenter.GetComponent<GridGhostObject>();
        Check(ghost != null && !ghost.IsHidden,
            "GHOST_AT_TARGET_" + part.id,
            $"part={part.objectName}");
        yield return ClickMouse(worldPoint);
        yield return null;
        yield return null;

        BuildableObject placed = grid.GetGridCell(position)?.GetOccupant(part.layer) as BuildableObject;
        Check(placed != null && placed.id == part.id,
            "PLACED_" + part.id,
            placed != null ? $"id={placed.id}; layer={part.layer}" : $"missing in {part.layer}");
        if (placed != null && placed.id == part.id && !placedParts.Contains(placed))
        {
            placedParts.Add(placed);
        }

        Check(controller.SelectedBuilding == null && controller.GridSystem.Mode == GridMode.None,
            "PLACEMENT_COMPLETES_" + part.id,
            $"mode={controller.GridSystem.Mode}; selection={(controller.SelectedBuilding == null ? "clear" : "set")}");
    }

    private void VerifySharedGridSlots(Vector2Int position, IReadOnlyList<BuildingSO> expectedParts)
    {
        GridCell cell = grid.GetGridCell(position);
        Check(cell != null, "SHARED_CELL", $"cell={position}");
        if (cell == null)
        {
            return;
        }

        foreach (BuildingSO part in expectedParts)
        {
            BuildableObject occupant = cell.GetOccupant(part.layer) as BuildableObject;
            Check(occupant != null && occupant.id == part.id,
                "SHARED_SLOT_" + part.layer,
                occupant != null ? $"id={occupant.id}" : "slot empty");
        }

        Check(cell.GetTopOccupant() is BuildableObject top && top.id == expectedParts[0].id,
            "SHARED_SELECTION_PRIORITY",
            "floor facility remains above decorative fixtures for grid selection");
    }

    private void VerifyPlacedRenderers(IReadOnlyList<BuildingSO> expectedParts)
    {
        foreach (BuildingSO part in expectedParts.Where(item => item.UsesIndependentRenderer))
        {
            BuildableObject placed = placedParts.FirstOrDefault(item => item != null && item.id == part.id);
            SpriteRenderer renderer = placed != null ? placed.GetComponentInChildren<SpriteRenderer>() : null;
            Check(renderer != null && renderer.enabled && renderer.sprite == part.sprite,
                "RENDERER_" + part.id,
                renderer != null
                    ? $"layer={renderer.sortingLayerName}; order={renderer.sortingOrder}; bounds={renderer.bounds.size}"
                    : "renderer missing");
            Check(renderer != null && renderer.bounds.size.x > 0.5f && renderer.bounds.size.y > 1f,
                "RENDERER_BOUNDS_" + part.id,
                renderer != null ? $"bounds={renderer.bounds.size}" : "renderer missing");
            if (part.layer == GridLayer.WallFixture || part.layer == GridLayer.CeilingFixture)
            {
                Check(renderer != null
                    && renderer.sortingLayerName == "DungeonBackObject"
                    && renderer.sortingOrder < 100,
                    "RENDERER_MOUNTED_BEHIND_" + part.id,
                    renderer != null
                        ? $"layer={renderer.sortingLayerName}; order={renderer.sortingOrder}"
                        : "renderer missing");
                Check(renderer != null && renderer.transform.localPosition.y > 1.5f,
                    "RENDERER_MOUNTED_UPPER_ANCHOR_" + part.id,
                    renderer != null
                        ? $"local={renderer.transform.localPosition}; bounds={renderer.bounds}"
                        : "renderer missing");
            }

            BuildingLightingAbility lightingAbility = part.GetAbility<BuildingLightingAbility>();
            if (lightingAbility != null && lightingAbility.IsValid)
            {
                Light2D light = placed != null ? placed.GetComponentInChildren<Light2D>() : null;
                RoomClippedLight2D clippedLight = placed != null
                    ? placed.GetComponentInChildren<RoomClippedLight2D>()
                    : null;
                clippedLight?.ForceRefresh();
                Check(light != null
                    && lightingAbility != null
                    && clippedLight != null
                    && clippedLight.HasCurrentWorldRect
                    && light.lightType == Light2D.LightType.Freeform
                    && clippedLight.CurrentFalloffSize > 0.25f
                    && Mathf.Approximately(light.intensity, lightingAbility.intensity)
                    && Mathf.Approximately(light.pointLightOuterRadius, lightingAbility.radius),
                    "LIGHT_ROOM_CLIPPED_" + part.id,
                    light != null && clippedLight != null && lightingAbility != null
                        ? $"type={light.lightType}; ability={lightingAbility.AbilityId}; rect={clippedLight.CurrentWorldRect}; falloff={clippedLight.CurrentFalloffSize}"
                        : "light or clipper missing");
                Check(light != null
                    && light.targetSortingLayers != null
                    && !light.targetSortingLayers.Contains(SortingLayer.NameToID("Wall")),
                    "LIGHT_IGNORES_CEILING_" + part.id,
                    light != null && light.targetSortingLayers != null
                        ? string.Join(",", light.targetSortingLayers.Select(SortingLayer.IDToName))
                        : "light missing");
                Check(clippedLight != null
                    && clippedLight.CurrentCoreRect.width < clippedLight.CurrentWorldRect.width
                    && clippedLight.CurrentCoreRect.height < clippedLight.CurrentWorldRect.height,
                    "LIGHT_SOFT_EDGE_" + part.id,
                    clippedLight != null
                        ? $"core={clippedLight.CurrentCoreRect}; rect={clippedLight.CurrentWorldRect}"
                        : "clipper missing");
                Check(clippedLight != null
                    && clippedLight.CurrentShapePointCount >= 16,
                    "LIGHT_ROUNDED_SHAPE_" + part.id,
                    clippedLight != null
                        ? $"points={clippedLight.CurrentShapePointCount}"
                        : "clipper missing");
            }
        }
    }

    private bool TryFindShowcasePosition(IReadOnlyList<BuildingSO> parts, out Vector2Int position)
    {
        BuildingPlacementValidator validator = new BuildingPlacementValidator(
            new GridPlacementValidator(),
            () => new BuildingConditionContext(gameData));
        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 1; x < grid.width - 1; x++)
            {
                Vector2Int candidate = new Vector2Int(x, y);
                IEnumerable<Vector2Int> footprint = parts.SelectMany(part => part.GetGridPosList(candidate)).Distinct();
                if (footprint.Any(cell => grid.GetGridCell(cell)?.GetOccupant(GridLayer.Character) != null)
                    || parts.Any(part => !validator.CanBuild(grid, part, candidate, out _)))
                {
                    continue;
                }

                Vector2 screenPoint = mainCamera.WorldToScreenPoint(GetCellCenter(grid, candidate));
                if (!IsInsideScreen(screenPoint) || IsScreenPointOverUi(screenPoint))
                {
                    continue;
                }

                position = candidate;
                return true;
            }
        }

        position = default;
        return false;
    }

    private static BuildingSO LoadPart(int expectedId, string code)
    {
        string guid = AssetDatabase.FindAssets($"{code}_ t:BuildingSO", new[]
            {
                "Assets/Resources/SO/Building/Modular"
            })
            .FirstOrDefault();
        BuildingSO part = !string.IsNullOrWhiteSpace(guid)
            ? AssetDatabase.LoadAssetAtPath<BuildingSO>(AssetDatabase.GUIDToAssetPath(guid))
            : null;
        return part != null && part.id == expectedId ? part : null;
    }

    private static Button FindVisibleButtonByLabel(string label)
    {
        return UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(button => button != null
                && button.interactable
                && button.GetComponentsInChildren<TMP_Text>(true)
                    .Any(text => text != null && text.text == label));
    }

    private Button FindCategoryButton(string label)
    {
        return constructTab.GetComponentsInChildren<Button>(false)
            .FirstOrDefault(button => button != null
                && button.interactable
                && button.GetComponent<UIBuildingSelectButton>() == null
                && button.GetComponentsInChildren<TMP_Text>(true)
                    .Any(text => text != null && text.text == label));
    }

    private IEnumerator OpenCatalog(string suffix = "INITIAL")
    {
        if (constructTab.gameObject.activeInHierarchy)
        {
            yield break;
        }

        Button buildTabButton = FindVisibleButtonByLabel("건축");
        Check(buildTabButton != null, "BUILD_TAB_BUTTON_" + suffix, "visible build tab button resolved");
        if (buildTabButton == null)
        {
            yield break;
        }

        yield return ClickMouse(GetScreenCenter(buildTabButton.transform as RectTransform));
        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return null;
        Check(constructTab.gameObject.activeInHierarchy,
            "CATALOG_OPENED_" + suffix,
            "physical Input System click opened the build catalog");
    }

    private static string GetCategoryLabel(BuildingCategory category)
    {
        return category switch
        {
            BuildingCategory.Shop => "상점",
            BuildingCategory.Special => "특수",
            BuildingCategory.Resource => "자원",
            BuildingCategory.Crafting => "제작",
            BuildingCategory.Production => "생산",
            BuildingCategory.Wall => "벽/문",
            BuildingCategory.Movement => "이동",
            _ => "행동"
        };
    }

    private static void BringIntoView(ScrollRect scroll, RectTransform target)
    {
        if (scroll == null || scroll.content == null || scroll.viewport == null || target == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        float overflow = Mathf.Max(0f, scroll.content.rect.width - scroll.viewport.rect.width);
        if (overflow <= 0.1f)
        {
            scroll.horizontalNormalizedPosition = 0f;
            return;
        }

        float targetCenter = -target.anchoredPosition.x;
        if (targetCenter < 0f)
        {
            targetCenter = target.anchoredPosition.x;
        }
        float desiredOffset = Mathf.Clamp(targetCenter - scroll.viewport.rect.width * 0.5f, 0f, overflow);
        scroll.horizontalNormalizedPosition = desiredOffset / overflow;
    }

    private IEnumerator ClickMouse(Vector2 screenPoint)
    {
        EnsureVerificationMouseReady();
        QueueVerificationMouseState(new MouseState
        {
            position = screenPoint
        }.WithButton(MouseButton.Left, true));
        yield return null;
        yield return null;
        EnsureVerificationMouseReady();
        QueueVerificationMouseState(new MouseState
        {
            position = screenPoint
        });
        yield return null;
        yield return null;
        Check(Vector2.Distance(verificationMouse.position.ReadValue(), screenPoint) <= 0.1f,
            "POINTER_AT_TARGET",
            $"expected={screenPoint}; actual={verificationMouse.position.ReadValue()}");
    }

    private IEnumerator MoveMouse(Vector2 screenPoint, float waitSeconds)
    {
        EnsureVerificationMouseReady();
        QueueVerificationMouseState(new MouseState
        {
            position = screenPoint
        });
        yield return null;
        yield return null;
        if (waitSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(waitSeconds);
        }
        yield return null;
    }

    private void EnsureVerificationMouseReady()
    {
        if (verificationMouse == null || !verificationMouse.added)
        {
            return;
        }

        if (!verificationMouse.enabled)
        {
            InputSystem.EnableDevice(verificationMouse);
        }
        verificationMouse.MakeCurrent();
    }

    private void QueueVerificationMouseState(MouseState state)
    {
        if (verificationMouse == null || !verificationMouse.added)
        {
            return;
        }

        InputSystem.QueueStateEvent(verificationMouse, state);
        InputSystem.Update();
        if (Vector2.Distance(verificationMouse.position.ReadValue(), state.position) > 0.1f)
        {
            InputState.Change(verificationMouse, state);
        }
    }

    private IEnumerator CaptureScreen(string path, string key)
    {
        yield return new WaitForEndOfFrame();
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        Color32[] pixels = capture != null ? capture.GetPixels32() : Array.Empty<Color32>();
        if (capture != null)
        {
            File.WriteAllBytes(path, capture.EncodeToPNG());
            Destroy(capture);
        }

        Check(pixels.Length > 0 && pixels.Any(pixel => pixel.a > 0 && (pixel.r > 8 || pixel.g > 8 || pixel.b > 8)),
            key,
            $"path={path}; pixels={pixels.Length}");
    }

    private static Color32[] CaptureCamera(Camera camera, string path)
    {
        int width = Mathf.Max(1, Screen.width);
        int height = Mathf.Max(1, Screen.height);
        RenderTexture texture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        camera.targetTexture = texture;
        camera.Render();
        RenderTexture.active = texture;
        Texture2D capture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        capture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        capture.Apply();
        Color32[] pixels = capture.GetPixels32();
        if (!string.IsNullOrWhiteSpace(path))
        {
            File.WriteAllBytes(path, capture.EncodeToPNG());
        }

        camera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        RenderTexture.ReleaseTemporary(texture);
        Destroy(capture);
        return pixels;
    }

    private static int CountPixelDifferences(IReadOnlyList<Color32> before, IReadOnlyList<Color32> after)
    {
        if (before == null || after == null || before.Count != after.Count)
        {
            return 0;
        }

        int changed = 0;
        for (int index = 0; index < before.Count; index++)
        {
            Color32 a = before[index];
            Color32 b = after[index];
            if (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g)
                + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a) > 12)
            {
                changed++;
            }
        }

        return changed;
    }

    private static Vector3 GetCellCenter(Grid targetGrid, Vector2Int cell)
    {
        return targetGrid.GetWorldPos(cell);
    }

    private static Vector2 GetScreenCenter(RectTransform rect)
    {
        if (rect == null)
        {
            return default;
        }

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
    }

    private static bool IsInsideViewport(RectTransform target, RectTransform viewport)
    {
        if (target == null || viewport == null)
        {
            return false;
        }

        Vector3[] targetCorners = new Vector3[4];
        Vector3[] viewportCorners = new Vector3[4];
        target.GetWorldCorners(targetCorners);
        viewport.GetWorldCorners(viewportCorners);
        const float tolerance = 1f;
        return targetCorners[0].x >= viewportCorners[0].x - tolerance
            && targetCorners[2].x <= viewportCorners[2].x + tolerance
            && targetCorners[0].y >= viewportCorners[0].y - tolerance
            && targetCorners[2].y <= viewportCorners[2].y + tolerance;
    }

    private static bool IsInsideScreen(Vector2 point)
    {
        return point.x > 1f && point.x < Screen.width - 1f
            && point.y > 1f && point.y < Screen.height - 1f;
    }

    private static bool IsScreenPointOverUi(Vector2 point)
    {
        if (EventSystem.current == null || !EventSystem.current.isActiveAndEnabled)
        {
            return false;
        }

        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = point
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);
        return results.Any(result => result.module is GraphicRaycaster);
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
        Cleanup();
        Application.logMessageReceived -= OnLogMessageReceived;
        report.Add($"capturedErrors={capturedErrors.Count}; {Compact(capturedErrors)}");
        report.Add($"capturedWarnings={capturedWarnings.Count}; {Compact(capturedWarnings)}");
        bool passed = failures.Count == 0 && capturedErrors.Count == 0 && capturedWarnings.Count == 0;
        report.Add($"RESULT={(passed ? "PASS" : "FAIL")}; failures={failures.Count}; {Compact(failures)}");
        File.WriteAllText(ModularFacilityPlayModeVerifier.ReportPath, string.Join("\n", report));
        if (passed)
        {
            Debug.Log("Modular facility PlayMode verification passed. " + ModularFacilityPlayModeVerifier.ReportPath);
        }
        else
        {
            Debug.LogError("Modular facility PlayMode verification failed. " + ModularFacilityPlayModeVerifier.ReportPath);
        }

        Destroy(gameObject);
    }

    private void Cleanup()
    {
        if (cleanupComplete)
        {
            return;
        }

        cleanupComplete = true;
        controller?.SetGridModeNone();
        if (gameDataCaptured && gameData != null)
        {
            gameData.day.Value = originalDay;
            gameData.holdingMoney.Value = originalMoney;
            gameDataCaptured = false;
        }
        if (inputBehaviorCaptured)
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInputBehavior;
            inputBehaviorCaptured = false;
        }
        if (verificationMouse != null && verificationMouse.added)
        {
            InputSystem.RemoveDevice(verificationMouse);
        }
        verificationMouse = null;
        if (originalMouse != null && originalMouse.added && !originalMouse.enabled)
        {
            InputSystem.EnableDevice(originalMouse);
        }
        originalMouse = null;
        if (cameraLockCaptured && mainCamera != null)
        {
            mainCamera.transform.SetPositionAndRotation(lockedCameraPosition, lockedCameraRotation);
        }
        if (cameraMovementController != null)
        {
            cameraMovementController.enabled = cameraMovementWasEnabled;
        }
        cameraMovementController = null;
        cameraLockCaptured = false;
        Time.timeScale = originalTimeScale;
    }

    private void OnDestroy()
    {
        Cleanup();
        Application.logMessageReceived -= OnLogMessageReceived;
    }

    private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning)
        {
            capturedWarnings.Add(condition);
        }
        else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            capturedErrors.Add(string.IsNullOrWhiteSpace(stackTrace)
                ? condition
                : condition + "\n" + stackTrace);
        }
    }

    private static string Compact(IEnumerable<string> values)
    {
        string compact = string.Join(" | ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(compact) ? "<none>" : compact;
    }
}
