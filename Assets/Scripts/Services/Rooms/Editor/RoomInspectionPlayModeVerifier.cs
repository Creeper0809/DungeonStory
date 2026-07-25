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

public static class RoomInspectionPlayModeVerifier
{
    public const string ReportPath = "Temp/room-inspection-playmode-report.txt";
    public const string CameraBaselinePath = "Temp/room-inspection-camera-baseline.png";
    public const string CameraOverlayPath = "Temp/room-inspection-camera-overlay.png";
    public const string ScreenCapturePath = "Temp/room-inspection-hud.png";
    public static int LastMcpOverlayCellCount { get; private set; }
    public static int LastMcpRoomId { get; private set; } = -1;

    public static int PrepareMcpCapture()
    {
        if (!EditorApplication.isPlaying)
        {
            return -1;
        }

        DungeonRuntimeLifetimeScope scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        if (scope == null
            || !scope.Container.Resolve<IGridSystemProvider>().TryGetGrid(out Grid grid))
        {
            return -1;
        }

        IRoomLayoutCache cache = scope.Container.Resolve<IRoomLayoutCache>();
        RoomInstance room = cache.GetLayout(grid).Rooms
            .Where(candidate => candidate != null && !candidate.IsSelfContained)
            .OrderByDescending(candidate => candidate.IsUsable)
            .ThenByDescending(candidate => (candidate.Roles & FacilityRole.Research) != 0)
            .ThenByDescending(candidate => candidate.Doors.Count > 0 && candidate.Walls.Count > 0)
            .ThenByDescending(candidate => candidate.Furniture.Count)
            .ThenByDescending(candidate => candidate.Cells.Count)
            .FirstOrDefault();
        IRoomInspectionService inspection = scope.Container.Resolve<IRoomInspectionService>();
        if (room == null || !inspection.ShowRoom(grid, room))
        {
            return -1;
        }

        LastMcpRoomId = room.Id;
        LastMcpOverlayCellCount = inspection.OverlayCellCount;

        Camera camera = Camera.main != null
            ? Camera.main
            : UnityEngine.Object.FindFirstObjectByType<Camera>();
        return camera != null ? camera.gameObject.GetInstanceID() : -1;
    }

    [MenuItem("DungeonStory/Debug/QA/Run Room Inspection PlayMode Verification")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("Room inspection verification requires PlayMode.");
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<RoomInspectionPlayModeVerificationRunner>() != null)
        {
            Debug.LogWarning("Room inspection verification is already running.");
            return;
        }

        EditorApplication.ExecuteMenuItem("Window/General/Game");
        new GameObject("Room Inspection PlayMode Verification Runner")
            .AddComponent<RoomInspectionPlayModeVerificationRunner>();
    }
}

public sealed class RoomInspectionPlayModeVerificationRunner : MonoBehaviour
{
    private sealed class DoorRegistration
    {
        public BuildableObject Door;
        public GridBuildingPlacement Placement;
        public List<Vector2Int> Positions;
    }

    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> capturedErrors = new List<string>();
    private readonly List<string> capturedWarnings = new List<string>();
    private readonly List<string> addedMoodFactorIds = new List<string>();

    private IRoomInspectionService inspection;
    private IRoomLayoutCache roomCache;
    private IRoomEnvironmentEvaluator evaluator;
    private IRoomEnvironmentSettingsProvider settingsProvider;
    private IRoomEnvironmentExperienceService experienceService;
    private IGridSystemProvider gridProvider;
    private GridSystemManager gridManager;
    private Grid grid;
    private Camera mainCamera;
    private CharacterActor moodActor;
    private Mouse originalMouse;
    private Mouse verificationMouse;
    private int verificationMouseSerial;
    private float originalTimeScale;
    private InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;
    private bool inputBehaviorCaptured;
    private GameObject temporaryWallObject;
    private BuildingSO temporaryWallData;

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Temp");
        PlayModeVerificationPersistenceSnapshot.CaptureCurrent("room-inspection");
        Application.logMessageReceived += OnLogMessageReceived;
        originalEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        inputBehaviorCaptured = true;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        originalMouse = Mouse.current;
        if (originalMouse != null)
        {
            InputSystem.DisableDevice(originalMouse);
        }
        CreateVerificationMouse();
        originalTimeScale = Time.timeScale;

        yield return null;
        yield return null;
        yield return null;

        yield return EnsurePlayableRun();
        originalTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        ResolveRuntimeServices();
        Check(mainCamera != null, "MAIN_CAMERA", "main camera resolved");
        Check(gridManager != null && grid != null, "GRID", "runtime grid resolved");
        Check(inspection != null, "INSPECTION_SERVICE", "room inspection service resolved");
        Check(roomCache != null && evaluator != null, "ROOM_SERVICES", "room cache and evaluator resolved");
        Check(settingsProvider != null && experienceService != null, "EXPERIENCE_SERVICES", "settings and room experience services resolved");
        Check(Mouse.current != null, "INPUT_MOUSE", "Input System mouse resolved");
        if (mainCamera == null || grid == null || inspection == null || roomCache == null
            || evaluator == null || settingsProvider == null || experienceService == null
            || Mouse.current == null)
        {
            Finish();
            yield break;
        }

        gridManager.SetGridModeNone();
        inspection.SetEnabled(false);
        yield return null;
        Canvas.ForceUpdateCanvases();

        Button toggle = inspection.ToggleButton;
        Check(toggle != null, "TOGGLE_EXISTS", "room toggle exists under the HUD");
        Check(toggle != null && toggle.interactable, "TOGGLE_INTERACTABLE", "room toggle is interactable in normal mode");
        GridLayoutGroup legacyLayout = toggle != null
            ? toggle.transform.parent.GetComponent<GridLayoutGroup>()
            : null;
        Check(legacyLayout != null && Vector2.Distance(legacyLayout.cellSize, new Vector2(120f, 120f)) < 0.1f,
            "TOGGLE_LOGICAL_CELL", legacyLayout != null ? $"cell={legacyLayout.cellSize}" : "layout missing");
        RectTransform toggleRect = toggle != null ? toggle.GetComponent<RectTransform>() : null;
        RectTransform existingRect = toggle != null
            ? toggle.transform.parent.Cast<Transform>()
                .Select(item => item as RectTransform)
                .FirstOrDefault(item => item != null && item != toggleRect)
            : null;
        Check(toggleRect != null && existingRect != null
            && Mathf.Abs(toggleRect.rect.width - existingRect.rect.width) < 4f
            && Mathf.Abs(toggleRect.rect.height - existingRect.rect.height) < 1f,
            "TOGGLE_MATCHES_EXISTING", toggleRect != null && existingRect != null
                ? $"room={toggleRect.rect.size}; existing={existingRect.rect.size}"
                : "button rect missing");
        if (toggle == null)
        {
            Finish();
            yield break;
        }

        Color32[] baselinePixels = CaptureCamera(mainCamera, RoomInspectionPlayModeVerifier.CameraBaselinePath);
        Vector2 togglePoint = GetScreenCenter(toggle.GetComponent<RectTransform>());
        Check(IsInsideScreen(togglePoint), "TOGGLE_ON_SCREEN", $"screen={togglePoint}");
        AppendUiRaycastDiagnostics("TOGGLE_RAYCAST", togglePoint);
        yield return ClickMouse(togglePoint);
        Check(inspection.IsEnabled, "TOGGLE_ACTUAL_CLICK", "physical Input System click enabled the room view");
        Image toggleImage = toggle.targetGraphic as Image ?? toggle.GetComponent<Image>();
        Check(toggleImage != null && ColorsMatch(toggleImage.color, DungeonUiTheme.Accent),
            "TOGGLE_ACTIVE_COLOR", toggleImage != null ? $"color={toggleImage.color}" : "image missing");

        bool foundRoom = TryFindVisibleRoom(out RoomEnvironmentSnapshot target, out Vector2 roomPoint);
        Check(foundRoom, "VISIBLE_ROOM", foundRoom ? $"room={target.Room.Id}; screen={roomPoint}" : "no formal visible room found");
        if (!foundRoom)
        {
            Finish();
            yield break;
        }

        yield return MoveMouse(roomPoint, 0.4f);
        RoomEnvironmentSnapshot hovered = inspection.CurrentSnapshot;
        Check(hovered != null && hovered.Room.ContainsCell(target.Room.Cells[0]),
            "HOVER_ROOM", hovered != null ? $"room={hovered.Room.Id}" : "snapshot missing");
        Check(inspection.PanelObject != null && inspection.PanelObject.activeInHierarchy,
            "PANEL_VISIBLE", "room panel is visible after hover");
        Check(hovered != null && inspection.OverlayCellCount == hovered.Room.Cells.Count,
            "OVERLAY_CELL_COUNT", hovered != null
                ? $"overlay={inspection.OverlayCellCount}; room={hovered.Room.Cells.Count}"
                : "snapshot missing");
        VerifyPanelContent(hovered);
        VerifyOverlayPresentation();
        VerifyPanelPlacement(toggle);

        if (hovered != null)
        {
            RoomEnvironmentSnapshot frozen = hovered;
            Vector2 panelPoint = GetScreenCenter(inspection.PanelObject.GetComponent<RectTransform>());
            yield return MoveMouse(panelPoint, 0.4f);
            Check(ReferenceEquals(inspection.CurrentSnapshot, frozen),
                "UI_HOVER_FREEZES_ROOM", "moving over the room panel keeps the last room");
            yield return MoveMouse(roomPoint, 0.4f);
        }
        Check(toggleImage != null && ColorsMatch(toggleImage.color, DungeonUiTheme.Accent),
            "TOGGLE_ACTIVE_COLOR_PERSISTS", toggleImage != null ? $"color={toggleImage.color}" : "image missing");

        yield return VerifyDynamicRoomChanges(roomPoint);
        yield return VerifyMoodExperience();

        if (TryFindVisibleEmptyCell(out Vector2 emptyPoint))
        {
            yield return MoveMouse(emptyPoint, 0.4f);
            Check(inspection.CurrentSnapshot == null, "EMPTY_CELL_CLEARS_SNAPSHOT", $"screen={emptyPoint}");
            Check(inspection.OverlayCellCount == 0, "EMPTY_CELL_CLEARS_OVERLAY", "overlay cell count is zero");
            Check(inspection.PanelObject != null && !inspection.PanelObject.activeInHierarchy,
                "EMPTY_CELL_CLEARS_PANEL", "room panel is hidden");
        }
        else
        {
            report.Add("EMPTY_CELL_LOOKUP=SKIP; no empty visible world cell");
        }

        yield return MoveMouse(roomPoint, 0.4f);
        Check(inspection.CurrentSnapshot != null, "ROOM_REHOVER", "room restored after returning from empty world");

        gridManager.SetGridModeBuild();
        yield return null;
        yield return null;
        Check(!inspection.IsEnabled, "BUILD_MODE_AUTO_CLOSE", "build mode closes the room view");
        Check(!toggle.interactable, "BUILD_MODE_DISABLES_TOGGLE", "build mode disables the room toggle");
        Check(inspection.CurrentSnapshot == null && inspection.OverlayCellCount == 0,
            "BUILD_MODE_CLEARS_ROOM", "build mode clears room state and overlay");
        gridManager.SetGridModeNone();
        yield return null;
        Check(toggle.interactable, "NORMAL_MODE_REENABLES_TOGGLE", "normal mode re-enables the room toggle");

        yield return ClickMouse(togglePoint);
        yield return MoveMouse(roomPoint, 0.4f);
        Check(inspection.IsEnabled && inspection.CurrentSnapshot != null,
            "TOGGLE_REENABLE", "physical click re-enabled room hover after build mode");

        Color32[] overlayPixels = CaptureCamera(mainCamera, RoomInspectionPlayModeVerifier.CameraOverlayPath);
        int changedPixels = CountPixelDifferences(baselinePixels, overlayPixels);
        Check(changedPixels > 100, "CAMERA_OVERLAY_DIFFERENCE", $"changedPixels={changedPixels}");
        int visibleOutlinePixels = CountVisibleOutlinePixels(baselinePixels, overlayPixels);
        Check(visibleOutlinePixels > 200, "CAMERA_OUTLINE_VISIBLE_PIXELS",
            $"visibleOutlinePixels={visibleOutlinePixels}");
        yield return CaptureScreen();

        Finish();
    }

    private IEnumerator EnsurePlayableRun()
    {
        GameObject modal = FindSceneObject("SaveModal");
        if (modal != null && modal.activeInHierarchy)
        {
            Button continueButton = FindActiveSceneButton("ContinueLatestButton");
            if (continueButton != null && continueButton.interactable)
            {
                yield return ClickMouse(GetScreenCenter(continueButton.GetComponent<RectTransform>()));
                yield return new WaitForSecondsRealtime(0.25f);
            }

            if (modal.activeInHierarchy)
            {
                Button startNewButton = FindActiveSceneButton("StartNewRunButton");
                yield return ClickMouse(GetScreenCenter(startNewButton?.GetComponent<RectTransform>()));
                if (modal.activeInHierarchy)
                {
                    yield return ClickMouse(GetScreenCenter(startNewButton?.GetComponent<RectTransform>()));
                }

                yield return null;
            }
        }

        Button ownerButton = Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(candidate => candidate != null
                && candidate.gameObject.scene.IsValid()
                && candidate.gameObject.activeInHierarchy
                && candidate.name.StartsWith("OwnerOption_", StringComparison.Ordinal));
        if (ownerButton != null)
        {
            yield return ClickMouse(GetScreenCenter(ownerButton.GetComponent<RectTransform>()));
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible();
            yield return new WaitForSecondsRealtime(0.25f);
        }

        bool ownerSelectionVisible = Resources.FindObjectsOfTypeAll<OwnerSelectionPanel>()
            .Any(panel => panel != null
                && panel.gameObject.scene.IsValid()
                && panel.gameObject.activeInHierarchy);
        bool modalVisible = modal != null && modal.activeInHierarchy;
        Check(!modalVisible && !ownerSelectionVisible,
            "PLAYABLE_RUN",
            "title and any legacy owner-selection flow closed before room inspection");
    }

    private static Button FindActiveSceneButton(string name)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(candidate => candidate != null
                && candidate.gameObject.scene.IsValid()
                && candidate.gameObject.activeInHierarchy
                && candidate.name == name);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<Transform>()
            .Where(candidate => candidate != null && candidate.gameObject.scene.IsValid())
            .Select(candidate => candidate.gameObject)
            .FirstOrDefault(candidate => candidate.name == name);
    }

    private void ResolveRuntimeServices()
    {
        mainCamera = Camera.main;
        DungeonRuntimeLifetimeScope scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        if (scope == null || scope.Container == null)
        {
            return;
        }

        try
        {
            inspection = scope.Container.Resolve<IRoomInspectionService>();
            roomCache = scope.Container.Resolve<IRoomLayoutCache>();
            evaluator = scope.Container.Resolve<IRoomEnvironmentEvaluator>();
            settingsProvider = scope.Container.Resolve<IRoomEnvironmentSettingsProvider>();
            experienceService = scope.Container.Resolve<IRoomEnvironmentExperienceService>();
            gridProvider = scope.Container.Resolve<IGridSystemProvider>();
            gridManager = gridProvider.Manager;
            grid = gridProvider.Grid;
        }
        catch (Exception exception)
        {
            failures.Add("RESOLVE_RUNTIME: " + exception.Message);
        }
    }

    private bool TryFindVisibleRoom(out RoomEnvironmentSnapshot snapshot, out Vector2 screenPoint)
    {
        snapshot = null;
        screenPoint = default;
        IEnumerable<RoomInstance> candidates = roomCache.GetLayout(grid).Rooms
            .Where(room => room != null && !room.IsSelfContained)
            .OrderByDescending(room => room.IsUsable)
            .ThenByDescending(room => (room.Roles & FacilityRole.Research) != 0)
            .ThenByDescending(room => room.Doors.Count > 0 && room.Walls.Count > 0)
            .ThenByDescending(room => room.Furniture.Count)
            .ThenByDescending(room => room.Cells.Count);

        foreach (RoomInstance room in candidates)
        {
            foreach (Vector2Int cell in room.Cells)
            {
                Vector2 candidatePoint = mainCamera.WorldToScreenPoint(GetCellCenter(grid, cell));
                if (!IsInsideScreen(candidatePoint) || IsScreenPointOverUi(candidatePoint))
                {
                    continue;
                }

                snapshot = evaluator.Evaluate(grid, room);
                screenPoint = candidatePoint;
                return true;
            }
        }

        return false;
    }

    private bool TryFindVisibleEmptyCell(out Vector2 screenPoint)
    {
        screenPoint = default;
        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (roomCache.TryGetRoom(grid, cell, out RoomInstance room)
                    && room != null
                    && !room.IsSelfContained)
                {
                    continue;
                }

                Vector2 candidatePoint = mainCamera.WorldToScreenPoint(GetCellCenter(grid, cell));
                if (IsInsideScreen(candidatePoint) && !IsScreenPointOverUi(candidatePoint))
                {
                    screenPoint = candidatePoint;
                    return true;
                }
            }
        }

        return false;
    }

    private void VerifyPanelContent(RoomEnvironmentSnapshot snapshot)
    {
        string allText = inspection.PanelObject == null
            ? string.Empty
            : string.Join(" | ", inspection.PanelObject.GetComponentsInChildren<TMP_Text>(true).Select(text => text.text));
        Check(snapshot != null && allText.Contains(RoomEnvironmentPresentation.GetRoomName(snapshot.Roles)),
            "PANEL_ROOM_NAME", allText);
        Check(allText.Contains("넓이") && allText.Contains("미관")
            && allText.Contains("청결") && allText.Contains("인상도"),
            "PANEL_ENVIRONMENT_METRICS", allText);
        Check(allText.Contains("면적") && allText.Contains("문") && allText.Contains("벽") && allText.Contains("시설"),
            "PANEL_STRUCTURE", allText);
        Check(allText.Contains("성향을 만든 시설"), "PANEL_CONTRIBUTORS", allText);
    }

    private void VerifyOverlayPresentation()
    {
        GameObject root = GameObject.Find("RoomInspectionWorldOverlay");
        SpriteRenderer[] renderers = root != null
            ? root.GetComponentsInChildren<SpriteRenderer>(true)
            : Array.Empty<SpriteRenderer>();
        SpriteRenderer[] fills = renderers
            .Where(renderer => renderer.name.StartsWith("Fill_", StringComparison.Ordinal))
            .ToArray();
        SpriteRenderer[] borders = renderers
            .Where(renderer => renderer.name.StartsWith("Border_", StringComparison.Ordinal))
            .ToArray();
        SpriteRenderer[] verticalBorders = borders
            .Where(renderer => renderer.transform.localScale.y > renderer.transform.localScale.x)
            .ToArray();
        SpriteRenderer[] horizontalBorders = borders
            .Where(renderer => renderer.transform.localScale.x >= renderer.transform.localScale.y)
            .ToArray();
        Check(root != null && root.activeInHierarchy, "OVERLAY_ROOT", "overlay root is active");
        Check(renderers.Length > 0 && renderers.All(renderer => renderer.sharedMaterial != null
                && renderer.sharedMaterial.shader.name.Contains("Sprite-Unlit", StringComparison.Ordinal)),
            "OVERLAY_UNLIT_MATERIAL", $"renderers={renderers.Length}");
        Check(fills.Length > 0 && fills.All(renderer => renderer.sortingLayerName == "RoomOverlay"),
            "OVERLAY_FILL_SORTING_LAYER", $"fills={fills.Length}");
        Check(verticalBorders.Length > 0 && verticalBorders.All(renderer =>
                renderer.sortingLayerName == "RoomOutline" && renderer.sortingOrder == 1),
            "OVERLAY_VERTICAL_INSET_OUTLINE", $"vertical={verticalBorders.Length}");
        Check(horizontalBorders.Length > 0 && horizontalBorders.All(renderer =>
                renderer.sortingLayerName == "RoomOutline" && renderer.sortingOrder == 1),
            "OVERLAY_HORIZONTAL_BORDER_LAYER", $"horizontal={horizontalBorders.Length}");
        if (fills.Length > 0 && verticalBorders.Length >= 2 && horizontalBorders.Length > 0)
        {
            float fillMinX = fills.Min(renderer => renderer.bounds.min.x);
            float fillMaxX = fills.Max(renderer => renderer.bounds.max.x);
            float fillMinY = fills.Min(renderer => renderer.bounds.min.y);
            float fillMaxY = fills.Max(renderer => renderer.bounds.max.y);
            float fillCenterY = (fillMinY + fillMaxY) * 0.5f;
            SpriteRenderer[] floorBorders = horizontalBorders
                .Where(renderer => renderer.bounds.center.y < fillCenterY)
                .ToArray();
            SpriteRenderer[] topBorders = horizontalBorders
                .Where(renderer => renderer.bounds.center.y > fillCenterY)
                .ToArray();
            float outlineMinX = verticalBorders.Min(renderer => renderer.bounds.min.x);
            float outlineMaxX = verticalBorders.Max(renderer => renderer.bounds.max.x);
            float outlineMinY = floorBorders.Length > 0
                ? floorBorders.Min(renderer => renderer.bounds.min.y)
                : float.PositiveInfinity;
            float topOutlineMaxY = topBorders.Length > 0
                ? topBorders.Max(renderer => renderer.bounds.max.y)
                : float.NegativeInfinity;
            const float tolerance = 0.01f;
            Check(Mathf.Abs(outlineMinX - fillMinX) <= tolerance
                    && Mathf.Abs(outlineMaxX - fillMaxX) <= tolerance
                    && Mathf.Abs(outlineMinY - fillMinY) <= tolerance,
                "OVERLAY_SIDES_AND_FLOOR_FLUSH_WITH_ROOM_BOUNDS",
                $"fillX={fillMinX:F3}..{fillMaxX:F3}; outlineX={outlineMinX:F3}..{outlineMaxX:F3}; "
                + $"fillBottom={fillMinY:F3}; outlineBottom={outlineMinY:F3}");
            const float expectedCeilingClearance = 0.5f;
            Check(topBorders.Length > 0
                    && Mathf.Abs((fillMaxY - topOutlineMaxY) - expectedCeilingClearance) <= tolerance,
                "OVERLAY_TOP_OUTLINE_BELOW_CEILING",
                $"topBorders={topBorders.Length}; top={topOutlineMaxY:F3}; ceiling={fillMaxY:F3}; "
                + $"clearance={fillMaxY - topOutlineMaxY:F3}");
        }
        if (inspection.CurrentSnapshot?.Status == RoomEnvironmentStatus.Usable)
        {
            Color expectedOutline = Color.Lerp(DungeonUiTheme.Accent, Color.white, 0.25f);
            Check(borders.All(renderer => RgbMatches(renderer.color, expectedOutline)
                    && renderer.color.a >= 0.9f),
                "OVERLAY_SELECTION_ACCENT", $"borders={borders.Length}");
        }
        Check(root != null && root.GetComponentsInChildren<Collider2D>(true).Length == 0,
            "OVERLAY_NO_COLLIDERS", "overlay cannot consume world clicks");

        int backObject = SortingLayer.GetLayerValueFromName("DungeonBackObject");
        int overlay = SortingLayer.GetLayerValueFromName("RoomOverlay");
        int middleObject = SortingLayer.GetLayerValueFromName("DungeonMiddleObject");
        int frontObject = SortingLayer.GetLayerValueFromName("DungeonFrontObject");
        int outline = SortingLayer.GetLayerValueFromName("RoomOutline");
        int wall = SortingLayer.GetLayerValueFromName("Wall");
        int defaultLayer = SortingLayer.GetLayerValueFromName("Default");
        Check(backObject < overlay && overlay < middleObject,
            "OVERLAY_LAYER_ORDER", $"back={backObject}; overlay={overlay}; middle={middleObject}");
        Check(frontObject < wall && wall < outline && outline < defaultLayer,
            "OUTLINE_LAYER_ORDER",
            $"front={frontObject}; wall={wall}; outline={outline}; default={defaultLayer}");
    }

    private void VerifyPanelPlacement(Button toggle)
    {
        if (inspection.PanelObject == null || toggle == null)
        {
            Check(false, "PANEL_PLACEMENT", "panel or toggle missing");
            return;
        }

        Rect panelRect = GetScreenRect(inspection.PanelObject.GetComponent<RectTransform>());
        Rect toggleRect = GetScreenRect(toggle.GetComponent<RectTransform>());
        Rect screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
        Check(screenRect.Contains(panelRect.min) && screenRect.Contains(panelRect.max),
            "PANEL_ON_SCREEN", $"panel={panelRect}; screen={Screen.width}x{Screen.height}");
        Check(!panelRect.Overlaps(toggleRect), "PANEL_NO_TOGGLE_OVERLAP",
            $"panel={panelRect}; toggle={toggleRect}");
    }

    private IEnumerator VerifyDynamicRoomChanges(Vector2 roomPoint)
    {
        RoomEnvironmentSnapshot baseline = inspection.CurrentSnapshot;
        if (baseline == null)
        {
            Check(false, "DYNAMIC_BASELINE", "room snapshot missing");
            yield break;
        }

        List<BuildableObject> fixtures = baseline.Fixtures
            .Where(item => item != null && !item.isDestroy)
            .ToList();
        Dictionary<BuildableObject, bool> damagedBefore = fixtures.ToDictionary(item => item, item => item.IsDamaged);
        if (fixtures.Count > 0)
        {
            foreach (BuildableObject fixture in fixtures)
            {
                fixture.SetDamaged(true);
            }

            yield return MoveMouse(roomPoint, 0.4f);
            RoomEnvironmentSnapshot damaged = inspection.CurrentSnapshot;
            Check(damaged != null && damaged.DamagedFixtures >= fixtures.Count,
                "DAMAGE_REFRESH_COUNT", damaged != null ? $"damaged={damaged.DamagedFixtures}" : "snapshot missing");
            Check(damaged != null && damaged.Cleanliness < baseline.Cleanliness,
                "DAMAGE_LOWERS_CLEANLINESS", damaged != null
                    ? $"before={baseline.Cleanliness:0.0}; after={damaged.Cleanliness:0.0}"
                    : "snapshot missing");

            foreach (KeyValuePair<BuildableObject, bool> item in damagedBefore)
            {
                item.Key.SetDamaged(item.Value);
            }

            yield return MoveMouse(roomPoint, 0.4f);
        }
        else
        {
            report.Add("DAMAGE_REFRESH=SKIP; selected room has no fixtures");
        }

        RoomEnvironmentSnapshot restored = inspection.CurrentSnapshot;
        BuildableObject wall = restored?.Room.Walls.FirstOrDefault(IsRegisteredBoundary);
        if (wall != null)
        {
            List<Vector2Int> positions = new List<Vector2Int>(wall.buildPoses);
            GridBuildingPlacement placement = wall.BuildingData.Placement;
            bool removed = grid.RemoveOccupant(placement.Layer, positions, placement.IsMovement);
            Check(removed, "WALL_REMOVE_SETUP", $"wall={wall.name}; cells={positions.Count}");
            yield return MoveMouse(roomPoint, 0.4f);
            RoomEnvironmentSnapshot open = inspection.CurrentSnapshot;
            Check(open != null && open.Status == RoomEnvironmentStatus.OpenBoundary,
                "WALL_REMOVE_OPENS_ROOM", open != null ? $"status={open.Status}" : "snapshot missing");

            bool registered = grid.RegisterOccupant(wall, placement.Layer, positions, placement.IsMovement);
            Check(registered, "WALL_RESTORE", $"wall={wall.name}");
            yield return MoveMouse(roomPoint, 0.4f);
        }
        else
        {
            report.Add("WALL_REFRESH=SKIP; no registered wall boundary");
        }

        restored = inspection.CurrentSnapshot;
        List<DoorRegistration> doors = restored?.Room.Doors
            .Where(IsRegisteredBoundary)
            .Distinct()
            .Select(door => new DoorRegistration
            {
                Door = door,
                Placement = door.BuildingData.Placement,
                Positions = new List<Vector2Int>(door.buildPoses)
            })
            .ToList();
        if (doors != null && doors.Count > 0)
        {
            List<Vector2Int> doorPositions = doors
                .SelectMany(item => item.Positions)
                .Distinct()
                .ToList();
            BuildableObject substituteWall = CreateTemporaryWall(doorPositions[0]);
            GridBuildingPlacement wallPlacement = substituteWall.BuildingData.Placement;
            Dictionary<Vector2Int, IGridOccupant> hallwayByPosition = doorPositions
                .Select(position => new
                {
                    Position = position,
                    Occupant = grid.GetGridCell(position)?.GetOccupant(GridLayer.Hallway)
                })
                .Where(item => item.Occupant != null)
                .ToDictionary(item => item.Position, item => item.Occupant);
            bool removed = true;
            foreach (DoorRegistration item in doors)
            {
                removed &= grid.RemoveOccupant(
                    item.Placement.Layer,
                    item.Positions,
                    item.Placement.IsMovement);
            }
            foreach (Vector2Int position in hallwayByPosition.Keys)
            {
                grid.RemoveOccupant(GridLayer.Hallway, new[] { position }, false);
            }

            bool closedWithWall = removed && grid.RegisterOccupant(
                substituteWall,
                wallPlacement.Layer,
                doorPositions,
                false);
            Check(closedWithWall, "DOOR_REMOVE_SETUP", $"doors={doors.Count}; cells={doorPositions.Count}");
            if (closedWithWall)
            {
                yield return MoveMouse(roomPoint, 0.4f);
                RoomEnvironmentSnapshot missingDoor = inspection.CurrentSnapshot;
                Check(missingDoor != null && missingDoor.Status == RoomEnvironmentStatus.MissingDoor,
                    "DOOR_REMOVE_DISALLOWS_ROOM", missingDoor != null
                        ? $"status={missingDoor.Status}; active={missingDoor.IsEnvironmentActive}"
                        : "snapshot missing");
                Check(missingDoor != null && !missingDoor.IsEnvironmentActive,
                    "MISSING_DOOR_NO_EFFECT", "environment effects are disabled without a door");

                int versionBeforeWallRemoval = grid.version;
                RoomEnvironmentSnapshot beforeWallRemoval = inspection.CurrentSnapshot;
                grid.RemoveOccupant(wallPlacement.Layer, doorPositions, false);
                yield return MoveMouse(roomPoint, 0.4f);
                RoomEnvironmentSnapshot openAfterWallRemoval = inspection.CurrentSnapshot;
                Check(openAfterWallRemoval != null
                    && !ReferenceEquals(openAfterWallRemoval, beforeWallRemoval)
                    && grid.version > versionBeforeWallRemoval,
                    "WALL_REMOVE_REFRESHES_LAYOUT", openAfterWallRemoval != null
                        ? $"gridVersion={versionBeforeWallRemoval}->{grid.version}; status={openAfterWallRemoval.Status}"
                        : "snapshot missing");

                RoomInstance openBoundary = new RoomInstance(
                    -48191,
                    openAfterWallRemoval.Room.Cells,
                    openAfterWallRemoval.Room.Furniture,
                    Array.Empty<BuildableObject>(),
                    openAfterWallRemoval.Room.Walls,
                    openAfterWallRemoval.Room.SolidBoundaryCount,
                    1);
                bool openShown = inspection.ShowRoom(grid, openBoundary);
                Check(openShown
                    && inspection.CurrentSnapshot != null
                    && inspection.CurrentSnapshot.Status == RoomEnvironmentStatus.OpenBoundary
                    && !inspection.CurrentSnapshot.IsEnvironmentActive,
                    "OPEN_BOUNDARY_PRESENTATION", openShown && inspection.CurrentSnapshot != null
                        ? $"status={inspection.CurrentSnapshot.Status}; active={inspection.CurrentSnapshot.IsEnvironmentActive}"
                        : "open boundary snapshot was not shown");

                foreach (KeyValuePair<Vector2Int, IGridOccupant> hallway in hallwayByPosition)
                {
                    grid.RegisterOccupant(
                        hallway.Value,
                        GridLayer.Hallway,
                        new[] { hallway.Key },
                        false);
                }

                bool restoredDoor = true;
                foreach (DoorRegistration item in doors)
                {
                    restoredDoor &= grid.RegisterOccupant(
                        item.Door,
                        item.Placement.Layer,
                        item.Positions,
                        item.Placement.IsMovement);
                }
                Check(restoredDoor, "DOOR_RESTORE", $"doors={doors.Count}");
                yield return MoveMouse(roomPoint, 0.4f);
            }
            else if (removed)
            {
                foreach (KeyValuePair<Vector2Int, IGridOccupant> hallway in hallwayByPosition)
                {
                    grid.RegisterOccupant(
                        hallway.Value,
                        GridLayer.Hallway,
                        new[] { hallway.Key },
                        false);
                }
                foreach (DoorRegistration item in doors)
                {
                    grid.RegisterOccupant(
                        item.Door,
                        item.Placement.Layer,
                        item.Positions,
                        item.Placement.IsMovement);
                }
                yield return MoveMouse(roomPoint, 0.4f);
            }
        }
        else
        {
            report.Add("DOOR_REFRESH=SKIP; no registered door");
        }
    }

    private IEnumerator VerifyMoodExperience()
    {
        RoomEnvironmentSnapshot snapshot = inspection.CurrentSnapshot;
        BuildableObject facility = snapshot?.Fixtures.FirstOrDefault(item => item != null && !item.isDestroy);
        moodActor = UnityEngine.Object.FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(item => item != null && item.Stats != null);
        if (snapshot == null || !snapshot.IsEnvironmentActive || facility == null || moodActor == null)
        {
            report.Add("ROOM_MOOD=SKIP; usable room, fixture or actor missing");
            yield break;
        }

        List<BuildableObject> fixtures = snapshot.Fixtures.Where(item => item != null).ToList();
        Dictionary<BuildableObject, bool> damageBefore = fixtures.ToDictionary(item => item, item => item.IsDamaged);
        RoomEnvironmentSettingsSO settings = settingsProvider.Settings;
        float impression = settings.GetImpressivenessMood(snapshot.Impressiveness);
        float cleanliness = settings.GetCleanlinessMood(snapshot.Cleanliness);
        if (Mathf.Approximately(impression, 0f) && Mathf.Approximately(cleanliness, 0f))
        {
            foreach (BuildableObject item in fixtures)
            {
                item.SetDamaged(true);
            }

            snapshot = evaluator.Evaluate(grid, snapshot.Room);
            impression = settings.GetImpressivenessMood(snapshot.Impressiveness);
            cleanliness = settings.GetCleanlinessMood(snapshot.Cleanliness);
        }

        HashSet<string> factorsBefore = moodActor.Mood.Factors
            .Where(item => item.Kind == CharacterMoodFactorKind.Interaction)
            .Select(item => item.Id)
            .ToHashSet();
        RoomEnvironmentExperienceEvent eventType = new RoomEnvironmentExperienceEvent(
            moodActor,
            facility,
            RoomExperienceActivity.Work,
            BuiltInWorkTypeIds.Research);
        bool firstApplied = experienceService.Apply(eventType);
        CharacterMoodSnapshot firstMood = moodActor.Mood;
        bool secondApplied = experienceService.Apply(eventType);
        CharacterMoodSnapshot secondMood = moodActor.Mood;
        List<CharacterMoodFactorSnapshot> additions = secondMood.Factors
            .Where(item => item.Kind == CharacterMoodFactorKind.Interaction && !factorsBefore.Contains(item.Id))
            .ToList();
        addedMoodFactorIds.AddRange(additions.Select(item => item.Id));

        int expectedFactors = (Mathf.Approximately(impression, 0f) ? 0 : 1)
            + (Mathf.Approximately(cleanliness, 0f) ? 0 : 1);
        Check(expectedFactors > 0 && firstApplied && secondApplied,
            "ROOM_MOOD_APPLIED", $"expected={expectedFactors}; first={firstApplied}; second={secondApplied}");
        Check(additions.Count == expectedFactors,
            "ROOM_MOOD_NO_DUPLICATES", $"expected={expectedFactors}; actual={additions.Count}");
        Check(additions.All(item => !item.Label.Contains("x2") && item.RemainingSeconds > 175f),
            "ROOM_MOOD_REFRESHES_SINGLE_STACK",
            string.Join(" | ", additions.Select(item => $"{item.Label}; {item.RemainingSeconds:0.0}s")));
        Check(additions.All(item => item.Label.Length <= 24),
            "ROOM_MOOD_LABELS_SHORT", string.Join(" | ", additions.Select(item => item.Label)));
        Check(firstMood.Factors.Count(item => item.Kind == CharacterMoodFactorKind.Interaction
            && !factorsBefore.Contains(item.Id)) == expectedFactors,
            "ROOM_MOOD_FIRST_COMPLETION", "factors were created on the first completed action");

        foreach (KeyValuePair<BuildableObject, bool> item in damageBefore)
        {
            item.Key.SetDamaged(item.Value);
        }

        yield return null;
    }

    private bool IsRegisteredBoundary(BuildableObject boundary)
    {
        if (boundary == null || boundary.BuildingData == null || boundary.buildPoses == null
            || boundary.buildPoses.Count == 0)
        {
            return false;
        }

        GridBuildingPlacement placement = boundary.BuildingData.Placement;
        return boundary.buildPoses.All(position =>
            ReferenceEquals(grid.GetGridCell(position)?.GetOccupant(placement.Layer), boundary));
    }

    private BuildableObject CreateTemporaryWall(Vector2Int position)
    {
        temporaryWallData = ScriptableObject.CreateInstance<BuildingSO>();
        temporaryWallData.id = -48190;
        temporaryWallData.objectName = "Wall";
        temporaryWallData.width = 1;
        temporaryWallData.height = 1;
        temporaryWallData.layer = GridLayer.Building;
        temporaryWallData.category = BuildingCategory.Wall;
        temporaryWallData.type = typeof(BuildableObject);
        temporaryWallData.Facility = new FacilityData();
        temporaryWallData.Evolution = new FacilityEvolutionContributionData();

        temporaryWallObject = new GameObject("Room Inspection Temporary Wall");
        BuildableObject wall = temporaryWallObject.AddComponent<BuildableObject>();
        wall.SetGrid(grid);
        wall.Initialization(temporaryWallData, position);
        return wall;
    }

    private IEnumerator ClickMouse(Vector2 screenPoint)
    {
        EnsureVerificationMouseReady();
        MouseState pressed = new MouseState
        {
            position = screenPoint
        }.WithButton(MouseButton.Left, true);
        ApplyMouseState(pressed);
        yield return null;
        yield return null;
        EnsureVerificationMouseReady();
        MouseState released = new MouseState
        {
            position = screenPoint
        };
        ApplyMouseState(released);
        yield return null;
        yield return null;
        Check(Vector2.Distance(verificationMouse.position.ReadValue(), screenPoint) <= 0.1f,
            "POINTER_POSITION", $"expected={screenPoint}; actual={verificationMouse.position.ReadValue()}");
    }

    private void AppendUiRaycastDiagnostics(string label, Vector2 screenPoint)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            report.Add(label + "=missing EventSystem");
            return;
        }

        PointerEventData pointer = new PointerEventData(eventSystem)
        {
            position = screenPoint
        };
        List<RaycastResult> hits = new List<RaycastResult>();
        eventSystem.RaycastAll(pointer, hits);
        report.Add(label + "=" + string.Join(" > ", hits.Select(hit => GetTransformPath(hit.gameObject.transform))));
    }

    private static string GetTransformPath(Transform target)
    {
        string path = target != null ? target.name : "null";
        while (target != null && target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }

        return path;
    }

    private IEnumerator MoveMouse(Vector2 screenPoint, float waitSeconds)
    {
        ApplyMouseState(new MouseState
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
            CreateVerificationMouse();
            return;
        }

        if (!verificationMouse.enabled)
        {
            InputSystem.EnableDevice(verificationMouse);
        }
        verificationMouse.MakeCurrent();
    }

    private void CreateVerificationMouse()
    {
        if (verificationMouse != null && verificationMouse.added)
        {
            InputSystem.RemoveDevice(verificationMouse);
        }

        verificationMouse = InputSystem.AddDevice<Mouse>($"RoomInspectionVerificationMouse{++verificationMouseSerial}");
        InputSystem.EnableDevice(verificationMouse);
        verificationMouse.MakeCurrent();
    }

    private void ApplyMouseState(MouseState state)
    {
        EnsureVerificationMouseReady();
        if (verificationMouse == null || !verificationMouse.added)
        {
            return;
        }

        verificationMouse.MakeCurrent();
        InputState.Change(verificationMouse, state);
        InputSystem.QueueStateEvent(verificationMouse, state);
        InputSystem.Update();
        if (Vector2.Distance(verificationMouse.position.ReadValue(), state.position) <= 0.1f)
        {
            return;
        }

        CreateVerificationMouse();
        InputState.Change(verificationMouse, state);
        InputSystem.QueueStateEvent(verificationMouse, state);
        InputSystem.Update();
    }

    private IEnumerator CaptureScreen()
    {
        yield return new WaitForEndOfFrame();
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        Color32[] pixels = capture.GetPixels32();
        File.WriteAllBytes(RoomInspectionPlayModeVerifier.ScreenCapturePath, capture.EncodeToPNG());
        Check(pixels.Length > 0 && pixels.Any(pixel => pixel.a > 0 && (pixel.r > 8 || pixel.g > 8 || pixel.b > 8)),
            "SCREEN_CAPTURE_NONEMPTY", $"pixels={pixels.Length}");
        Destroy(capture);
        report.Add("screenCapture=" + RoomInspectionPlayModeVerifier.ScreenCapturePath);
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
        File.WriteAllBytes(path, capture.EncodeToPNG());
        camera.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        RenderTexture.ReleaseTemporary(texture);
        UnityEngine.Object.Destroy(capture);
        return pixels;
    }

    private static int CountPixelDifferences(IReadOnlyList<Color32> before, IReadOnlyList<Color32> after)
    {
        if (before == null || after == null || before.Count != after.Count)
        {
            return 0;
        }

        int changed = 0;
        for (int i = 0; i < before.Count; i++)
        {
            Color32 a = before[i];
            Color32 b = after[i];
            if (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g)
                + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a) > 12)
            {
                changed++;
            }
        }

        return changed;
    }

    private static int CountVisibleOutlinePixels(IReadOnlyList<Color32> before, IReadOnlyList<Color32> after)
    {
        if (before == null || after == null || before.Count != after.Count)
        {
            return 0;
        }

        int visible = 0;
        for (int i = 0; i < before.Count; i++)
        {
            Color32 source = before[i];
            Color32 result = after[i];
            int delta = Mathf.Abs(source.r - result.r)
                + Mathf.Abs(source.g - result.g)
                + Mathf.Abs(source.b - result.b);
            if (delta > 20
                && result.g > 100
                && result.g - result.r > 20
                && result.g - result.b > 5)
            {
                visible++;
            }
        }

        return visible;
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
        File.WriteAllText(RoomInspectionPlayModeVerifier.ReportPath, string.Join("\n", report));

        if (passed)
        {
            Debug.Log("Room inspection PlayMode verification passed. " + RoomInspectionPlayModeVerifier.ReportPath);
        }
        else
        {
            Debug.LogError("Room inspection PlayMode verification failed. " + RoomInspectionPlayModeVerifier.ReportPath);
        }

        Destroy(gameObject);
        EditorApplication.ExitPlaymode();
    }

    private void Cleanup()
    {
        foreach (string factorId in addedMoodFactorIds.Distinct())
        {
            moodActor?.Stats?.RemoveMoodFactor(factorId);
        }

        if (gridManager != null)
        {
            gridManager.SetGridModeNone();
        }
        if (inputBehaviorCaptured)
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInputBehavior;
            inputBehaviorCaptured = false;
        }
        if (verificationMouse != null && verificationMouse.added)
        {
            InputSystem.RemoveDevice(verificationMouse);
            verificationMouse = null;
        }
        if (originalMouse != null && originalMouse.added && !originalMouse.enabled)
        {
            InputSystem.EnableDevice(originalMouse);
        }
        originalMouse = null;

        if (temporaryWallObject != null)
        {
            Destroy(temporaryWallObject);
            temporaryWallObject = null;
        }
        if (temporaryWallData != null)
        {
            Destroy(temporaryWallData);
            temporaryWallData = null;
        }

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

    private static Vector3 GetCellCenter(Grid targetGrid, Vector2Int cell)
    {
        return targetGrid.GetWorldPos(new Vector2(cell.x + 0.5f, cell.y + 0.5f));
    }

    private static Vector2 GetScreenCenter(RectTransform rect)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
    }

    private static Rect GetScreenRect(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static bool IsInsideScreen(Vector2 screenPoint)
    {
        return screenPoint.x > 1f && screenPoint.x < Screen.width - 1f
            && screenPoint.y > 1f && screenPoint.y < Screen.height - 1f;
    }

    private static bool IsScreenPointOverUi(Vector2 screenPoint)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || !eventSystem.isActiveAndEnabled)
        {
            return false;
        }

        PointerEventData pointer = new PointerEventData(eventSystem)
        {
            position = screenPoint
        };
        List<RaycastResult> results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointer, results);
        return results.Any(result => result.module is GraphicRaycaster);
    }

    private static bool ColorsMatch(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.01f
            && Mathf.Abs(a.g - b.g) < 0.01f
            && Mathf.Abs(a.b - b.b) < 0.01f
            && Mathf.Abs(a.a - b.a) < 0.01f;
    }

    private static bool RgbMatches(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.01f
            && Mathf.Abs(a.g - b.g) < 0.01f
            && Mathf.Abs(a.b - b.b) < 0.01f;
    }

    private static string Compact(IReadOnlyList<string> values)
    {
        return values == null || values.Count == 0
            ? "<none>"
            : string.Join(" || ", values.Select(value => value.Replace('\n', ' ').Replace('\r', ' ')));
    }
}
