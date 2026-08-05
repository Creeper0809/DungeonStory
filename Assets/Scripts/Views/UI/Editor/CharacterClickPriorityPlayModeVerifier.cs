using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using VContainer;

public static class CharacterClickPriorityPlayModeVerifier
{
    public const string ReportPath = "Temp/phase47-exclusive-world-info-report.txt";
    public const string CharacterCapturePath = "Temp/phase47-character-only.png";
    public const string BuildingCapturePath = "Temp/phase47-building-only.png";

    [MenuItem("DungeonStory/Debug/QA/Run Character Click Priority Verification")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("Character click priority verification requires PlayMode.");
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<CharacterClickPriorityVerificationRunner>() != null)
        {
            Debug.LogWarning("Character click priority verification is already running.");
            return;
        }

        EditorApplication.ExecuteMenuItem("Window/General/Game");
        new GameObject("Character Click Priority Verification Runner")
            .AddComponent<CharacterClickPriorityVerificationRunner>();
    }
}

public sealed class CharacterClickPriorityVerificationRunner : MonoBehaviour
{
    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> capturedErrors = new List<string>();
    private readonly List<string> capturedWarnings = new List<string>();

    private CharacterActor actor;
    private BuildableObject building;
    private CharacterSummaryInfo characterSummary;
    private BuildingSummaryInfo buildingSummary;
    private UIBuildingInfo buildingDetail;
    private WildlifeInfoPanel wildlifeInfo;
    private Vector3 originalActorPosition;
    private Vector2 originalMousePosition;
    private float originalTimeScale;
    private int buildingClickCount;
    private int physicalClickCount;
    private int activeClickNumber;
    private bool subscribed;
    private bool stateCaptured;
    private bool preparedRun;
    private InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;
    private bool inputBehaviorCaptured;
    private Mouse originalMouse;
    private Mouse verificationMouse;
    private int verificationMouseSerial;

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Temp");
        PlayModeVerificationPersistenceSnapshot.CaptureCurrent("character-click-priority");
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
        Time.timeScale = 0f;

        yield return null;
        yield return null;

        Check(verificationMouse != null, "MOUSE", "Input System mouse resolved");
        if (verificationMouse == null)
        {
            CleanupAndFinish();
            yield break;
        }

        originalMousePosition = verificationMouse.position.ReadValue();
        yield return EnsurePlayableRun();
        if (preparedRun)
        {
            originalTimeScale = Time.timeScale;
        }
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(0.2f);

        Camera camera = Camera.main;
        characterSummary = UnityEngine.Object.FindFirstObjectByType<CharacterSummaryInfo>();
        buildingSummary = UnityEngine.Object.FindFirstObjectByType<BuildingSummaryInfo>();
        buildingDetail = UnityEngine.Object.FindFirstObjectByType<UIBuildingInfo>(FindObjectsInactive.Include);
        wildlifeInfo = UnityEngine.Object.FindFirstObjectByType<WildlifeInfoPanel>();
        actor = FindActor();

        Check(camera != null, "CAMERA", "main camera resolved");
        Check(characterSummary != null, "CHARACTER_SUMMARY", "character summary resolved");
        Check(buildingSummary != null, "BUILDING_SUMMARY", "building summary resolved");
        Check(buildingDetail != null, "BUILDING_DETAIL", "full building detail panel resolved");
        Check(wildlifeInfo != null, "WILDLIFE_INFO", "wildlife info panel resolved");
        Check(actor != null, "CHARACTER", "active character resolved");
        if (camera == null || characterSummary == null || buildingSummary == null
            || buildingDetail == null || wildlifeInfo == null)
        {
            CleanupAndFinish();
            yield break;
        }

        CloseSummaries();
        yield return new WaitForSecondsRealtime(0.2f);
        Canvas.ForceUpdateCanvases();

        building = FindBuilding(camera, out Collider2D buildingCollider, out Vector2 screenPoint);
        Check(building != null && buildingCollider != null, "BUILDING", "grid-registered visible building collider resolved");
        Check(building != null && building.BuildingData != null, "BUILDING_DATA", "selected building has runtime definition");
        Check(!IsScreenPointOverUi(screenPoint), "WORLD_POINT_CLEAR", $"real building point starts outside UI; screen={screenPoint}");
        if (building == null || buildingCollider == null || building.BuildingData == null)
        {
            CleanupAndFinish();
            yield break;
        }

        building.OnBuildingClicked += OnBuildingClicked;
        subscribed = true;
        buildingClickCount = 0;
        Vector2 clickWorldPosition = buildingCollider.bounds.center;
        Check(buildingCollider.OverlapPoint(clickWorldPosition), "EXACT_BUILDING_COLLIDER", $"world={clickWorldPosition}");
        yield return PressMouse(screenPoint);
        yield return new WaitForSecondsRealtime(0.2f);
        Check(IsBuildingOnlyOpen() && IsDisplayedBuilding(building),
            "EXACT_BUILDING_CLICK",
            $"{DescribeBuildingInfoPanels()}; displayed={GetDisplayedBuildingName()}; expected={building.BuildingData.objectName}; buildingClicks={buildingClickCount}");
        yield return ReleaseMouse(screenPoint);

        CloseSummaries();
        yield return new WaitForSecondsRealtime(0.2f);
        bool foundBareHallway = TryFindBareHallwayScreenPoint(camera, out Vector2 hallwayScreenPoint, out Vector2Int hallwayCell);
        Check(foundBareHallway, "BARE_HALLWAY_POINT", $"cell={hallwayCell}; screen={hallwayScreenPoint}");
        if (foundBareHallway)
        {
            yield return PressMouse(hallwayScreenPoint);
            yield return ReleaseMouse(hallwayScreenPoint);
            Check(IsNoWorldInfoOpen(), "BARE_HALLWAY_NO_INFO", DescribeBuildingInfoPanels());

            WildlifeActor wildlife = UnityEngine.Object.FindObjectsByType<WildlifeActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null
                    && candidate.IsAlive
                    && candidate.GetComponentsInChildren<Collider2D>(true)
                        .Any(collider => collider != null && collider.enabled));
            Collider2D wildlifeCollider = wildlife != null
                ? wildlife.GetComponentsInChildren<Collider2D>(true)
                    .FirstOrDefault(collider => collider != null && collider.enabled)
                : null;
            Check(wildlife != null && wildlifeCollider != null,
                "WILDLIFE_CLICK_TARGET",
                "living wildlife with collider resolved");
            if (wildlife != null && wildlifeCollider != null)
            {
                Vector3 originalWildlifePosition = wildlife.transform.position;
                Vector3 wildlifeClickWorld = camera.ScreenToWorldPoint(new Vector3(
                    hallwayScreenPoint.x,
                    hallwayScreenPoint.y,
                    -camera.transform.position.z));
                wildlife.transform.position += wildlifeClickWorld - (Vector3)wildlifeCollider.bounds.center;
                Physics2D.SyncTransforms();

                Check(wildlifeCollider.OverlapPoint(wildlifeClickWorld),
                    "WILDLIFE_CLICK_COLLIDER",
                    $"world={wildlifeClickWorld}");
                yield return PressMouse(hallwayScreenPoint);
                yield return ReleaseMouse(hallwayScreenPoint);
                Check(wildlifeInfo.IsShowingWildlife && wildlifeInfo.CurrentWildlife == wildlife,
                    "WILDLIFE_FIRST_CLICK",
                    "first click opens the selected wildlife");

                yield return PressMouse(hallwayScreenPoint);
                yield return ReleaseMouse(hallwayScreenPoint);
                Check(wildlifeInfo.IsShowingWildlife && wildlifeInfo.CurrentWildlife == wildlife,
                    "WILDLIFE_CONSECUTIVE_CLICK",
                    "second consecutive click keeps the same wildlife detail open");

                wildlife.transform.position = originalWildlifePosition;
                Physics2D.SyncTransforms();
                wildlifeInfo.OnClose();
            }
        }

        if (actor == null)
        {
            CleanupAndFinish();
            yield break;
        }

        CloseSummaries();
        yield return new WaitForSecondsRealtime(0.2f);

        Collider2D actorCollider = actor.GetComponentsInChildren<Collider2D>(true)
            .FirstOrDefault(item => item != null && item.enabled);
        Check(actorCollider != null, "CHARACTER_COLLIDER", "character collider resolved");
        if (actorCollider == null)
        {
            CleanupAndFinish();
            yield break;
        }

        originalActorPosition = actor.transform.position;
        stateCaptured = true;
        actor.transform.position += (Vector3)(clickWorldPosition - (Vector2)actorCollider.bounds.center);
        Physics2D.SyncTransforms();

        Vector2 overlapPoint = actorCollider.bounds.center;
        screenPoint = camera.WorldToScreenPoint(overlapPoint);
        bool overlapReady = actorCollider.OverlapPoint(overlapPoint) && buildingCollider.OverlapPoint(overlapPoint);
        bool pointOnScreen = screenPoint.x > 0f && screenPoint.x < Screen.width
            && screenPoint.y > 0f && screenPoint.y < Screen.height;
        Check(overlapReady, "COLLIDER_OVERLAP", $"world={overlapPoint}");
        Check(pointOnScreen, "SCREEN_POINT", $"screen={screenPoint}");
        Check(!IsScreenPointOverUi(screenPoint), "OVERLAP_WORLD_POINT_CLEAR", "overlap click point starts outside UI");

        yield return null;
        buildingClickCount = 0;

        yield return PressMouse((Vector2)screenPoint);

        Check(buildingClickCount == 0, "OVERLAP_PRESS_BLOCKS_BUILDING", $"buildingClicks={buildingClickCount}");
        Check(IsCharacterOnlyOpen(), "OVERLAP_PRESS_UI", "character popup open while pressed; building popup closed");
        yield return ReleaseMouse((Vector2)screenPoint);
        Check(buildingClickCount == 0, "OVERLAP_RELEASE_BLOCKS_BUILDING", $"buildingClicks={buildingClickCount}");
        Check(IsCharacterOnlyOpen(), "OVERLAP_RELEASE_UI", "character popup remains open after release; building popup closed");
        yield return CaptureScreenshot(CharacterClickPriorityPlayModeVerifier.CharacterCapturePath);

        actor.transform.position += Vector3.right * 100f;
        Physics2D.SyncTransforms();
        yield return WaitForWorldPointClear(screenPoint, "BUILDING_WORLD_POINT_CLEAR");
        Check(IsCharacterOnlyOpen(), "CHARACTER_STILL_OPEN_BEFORE_BUILDING_CLICK", "character panel remains open before reverse physical click");
        buildingClickCount = 0;

        Check(buildingCollider.OverlapPoint(clickWorldPosition), "BUILDING_TEST_OVERLAP", $"world={clickWorldPosition}");
        yield return PressMouse(screenPoint);
        yield return new WaitForSecondsRealtime(0.2f);

        Check(IsBuildingOnlyOpen() && IsDisplayedBuilding(building),
            "BUILDING_PRESS_CLICK",
            $"{DescribeBuildingInfoPanels()}; displayed={GetDisplayedBuildingName()}; expected={building.BuildingData.objectName}; buildingClicks={buildingClickCount}");
        Check(IsBuildingOnlyOpen(), "BUILDING_PRESS_UI", DescribeBuildingInfoPanels());
        yield return CaptureScreenshot(CharacterClickPriorityPlayModeVerifier.BuildingCapturePath);
        yield return ReleaseMouse(screenPoint);
        Check(IsBuildingOnlyOpen() && IsDisplayedBuilding(building),
            "BUILDING_RELEASE_CLICK",
            $"{DescribeBuildingInfoPanels()}; displayed={GetDisplayedBuildingName()}; expected={building.BuildingData.objectName}; buildingClicks={buildingClickCount}");
        Check(IsBuildingOnlyOpen(), "BUILDING_RELEASE_UI", DescribeBuildingInfoPanels());

        ResolveGameEventBus()?.ShowInfo(actor);
        yield return null;
        Check(IsCharacterOnlyOpen(), "BUILDING_TO_CHARACTER_EVENT_UI", "character event closes the active full building detail immediately");

        RestoreWorldState();
        yield return null;
        CleanupAndFinish();
    }

    private IEnumerator EnsurePlayableRun()
    {
        Button startNew = FindSceneButton("StartNewRunButton");
        if (startNew != null && startNew.gameObject.activeInHierarchy)
        {
            preparedRun = true;
            yield return ClickUiButton(startNew, "new game");
            if (startNew.gameObject.activeInHierarchy)
            {
                yield return ClickUiButton(startNew, "confirm new game");
            }
            yield return new WaitForSecondsRealtime(0.2f);
        }

        OwnerRunManager ownerManager = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        if (ownerManager != null && ownerManager.CurrentOwnerActor == null)
        {
            preparedRun = true;
            Button ownerOption = Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(button => button != null
                    && button.gameObject.scene.IsValid()
                    && button.gameObject.activeInHierarchy
                    && button.name.StartsWith("OwnerOption_", StringComparison.Ordinal));
            yield return ClickUiButton(ownerOption, "owner option");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible();
            yield return new WaitForSecondsRealtime(0.2f);
        }

        Check(ownerManager != null && ownerManager.CurrentOwnerActor != null,
            "RUN_READY",
            "new game and owner selection completed with pointer input");
    }

    private IEnumerator ClickUiButton(Button button, string label)
    {
        bool available = button != null && button.gameObject.activeInHierarchy && button.interactable;
        Check(available, "POINTER_TARGET", available ? label : label + " missing");
        if (!available)
        {
            yield break;
        }

        Canvas.ForceUpdateCanvases();
        RectTransform rect = button.GetComponent<RectTransform>();
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            GetCanvasCamera(rect),
            rect.TransformPoint(rect.rect.center));
        yield return PressMouse(screenPoint);
        yield return ReleaseMouse(screenPoint);
    }

    private static Button FindSceneButton(string name)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(button => button != null
                && button.gameObject.scene.IsValid()
                && button.name == name);
    }

    private CharacterActor FindActor()
    {
        return UnityEngine.Object.FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(item => item != null
                && item.GetComponentsInChildren<Collider2D>(true).Any(collider => collider != null && collider.enabled));
    }

    private BuildableObject FindBuilding(
        Camera camera,
        out Collider2D selectedCollider,
        out Vector2 selectedScreenPoint)
    {
        selectedCollider = null;
        selectedScreenPoint = default;

        foreach (BuildableObject candidate in UnityEngine.Object.FindObjectsByType<BuildableObject>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            if (candidate == null || candidate.BuildingData == null || candidate.isDestroy)
            {
                continue;
            }

            foreach (Collider2D candidateCollider in candidate.GetComponentsInChildren<Collider2D>(true))
            {
                if (candidateCollider == null || !candidateCollider.enabled)
                {
                    continue;
                }

                Vector3 screenPoint = camera.WorldToScreenPoint(candidateCollider.bounds.center);
                bool onScreen = screenPoint.z > 0f
                    && screenPoint.x > 1f
                    && screenPoint.x < Screen.width - 1f
                    && screenPoint.y > 1f
                    && screenPoint.y < Screen.height - 1f;
                if (!onScreen || IsScreenPointOverUi(screenPoint))
                {
                    continue;
                }

                RectTransform characterPanelRect = characterSummary?.UI != null
                    ? characterSummary.UI.GetComponent<RectTransform>()
                    : null;
                if (characterPanelRect != null
                    && RectTransformUtility.RectangleContainsScreenPoint(
                        characterPanelRect,
                        screenPoint,
                        GetCanvasCamera(characterPanelRect)))
                {
                    continue;
                }

                BuildableObject[] overlappingBuildings = Physics2D
                    .OverlapPointAll(candidateCollider.bounds.center)
                    .Select(hit => hit != null ? hit.GetComponentInParent<BuildableObject>() : null)
                    .Where(item => item != null && !item.isDestroy)
                    .Distinct()
                    .ToArray();
                if (overlappingBuildings.Length != 1 || overlappingBuildings[0] != candidate)
                {
                    continue;
                }

                selectedCollider = candidateCollider;
                selectedScreenPoint = screenPoint;
                return candidate;
            }
        }

        return null;
    }

    private static Camera GetCanvasCamera(RectTransform rect)
    {
        Canvas canvas = rect != null ? rect.GetComponentInParent<Canvas>() : null;
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
    }

    private bool IsCharacterOnlyOpen()
    {
        bool characterOpen = characterSummary != null
            && characterSummary.UI != null
            && characterSummary.UI.activeInHierarchy;
        bool buildingOpen = buildingSummary != null
            && buildingSummary.UI != null
            && buildingSummary.UI.activeInHierarchy;
        return characterOpen && !buildingOpen && !IsDetailedBuildingOpen();
    }

    private bool IsBuildingOnlyOpen()
    {
        bool characterOpen = characterSummary != null
            && characterSummary.UI != null
            && characterSummary.UI.activeInHierarchy;
        bool buildingOpen = buildingSummary != null
            && buildingSummary.UI != null
            && buildingSummary.UI.activeInHierarchy;
        bool detailOpen = IsDetailedBuildingOpen();
        return !characterOpen && (detailOpen ^ buildingOpen);
    }

    private bool IsNoWorldInfoOpen()
    {
        bool characterOpen = characterSummary != null
            && characterSummary.UI != null
            && characterSummary.UI.activeInHierarchy;
        bool buildingOpen = buildingSummary != null
            && buildingSummary.UI != null
            && buildingSummary.UI.activeInHierarchy;
        bool wildlifeOpen = wildlifeInfo != null && wildlifeInfo.IsShowingWildlife;
        return !characterOpen && !buildingOpen && !wildlifeOpen && !IsDetailedBuildingOpen();
    }

    private bool TryFindBareHallwayScreenPoint(
        Camera camera,
        out Vector2 screenPoint,
        out Vector2Int gridPosition)
    {
        screenPoint = default;
        gridPosition = default;
        GridSystemManager manager = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>();
        Grid grid = manager != null ? manager.grid : null;
        if (grid == null || camera == null)
        {
            return false;
        }

        foreach (GridCell cell in grid.GetCells())
        {
            if (cell == null
                || cell.AreaType != GridCellAreaType.DungeonInterior
                || cell.GetOccupant(GridLayer.Hallway) is not Hallway
                || cell.GetAllOccupants().Count != 1)
            {
                continue;
            }

            Vector3 worldPoint = grid.GetWorldPos(cell.Position) + Vector3.up * 0.5f;
            Vector3 candidateScreenPoint = camera.WorldToScreenPoint(worldPoint);
            bool onScreen = candidateScreenPoint.z > 0f
                && candidateScreenPoint.x > 1f
                && candidateScreenPoint.x < Screen.width - 1f
                && candidateScreenPoint.y > 1f
                && candidateScreenPoint.y < Screen.height - 1f;
            if (!onScreen
                || IsScreenPointOverUi(candidateScreenPoint)
                || Physics2D.OverlapPointAll(worldPoint).Length > 0)
            {
                continue;
            }

            screenPoint = candidateScreenPoint;
            gridPosition = cell.Position;
            return true;
        }

        return false;
    }

    private string DescribeBuildingInfoPanels()
    {
        bool characterOpen = characterSummary != null
            && characterSummary.UI != null
            && characterSummary.UI.activeInHierarchy;
        bool summaryOpen = buildingSummary != null
            && buildingSummary.UI != null
            && buildingSummary.UI.activeInHierarchy;
        bool detailOpen = IsDetailedBuildingOpen();
        return $"character={characterOpen}; summary={summaryOpen}; detail={detailOpen}";
    }

    private bool IsDisplayedBuilding(BuildableObject expected)
    {
        if (expected == null || expected.BuildingData == null)
        {
            return false;
        }

        return string.Equals(GetDisplayedBuildingName(), expected.BuildingData.objectName, StringComparison.Ordinal);
    }

    private string GetDisplayedBuildingName()
    {
        if (buildingSummary != null
            && buildingSummary.UI != null
            && buildingSummary.UI.activeInHierarchy
            && buildingSummary.objectName != null)
        {
            return buildingSummary.objectName.text;
        }

        if (IsDetailedBuildingOpen() && buildingDetail != null && buildingDetail.nameText != null)
        {
            return buildingDetail.nameText.text;
        }

        return string.Empty;
    }

    private bool IsDetailedBuildingOpen()
    {
        if (buildingDetail == null || !buildingDetail.gameObject.activeInHierarchy)
        {
            return false;
        }

        CanvasGroup group = buildingDetail.GetComponent<CanvasGroup>();
        return group == null || group.alpha > 0.01f;
    }

    private void CloseSummaries()
    {
        characterSummary?.OnClose();
        buildingSummary?.OnClose();
        if (buildingDetail != null && buildingDetail.gameObject.activeInHierarchy)
        {
            buildingDetail.CloseDispaly();
        }
        wildlifeInfo?.OnClose();
    }

    private IEnumerator PressMouse(Vector2 screenPoint)
    {
        activeClickNumber = ++physicalClickCount;
        MouseState pressedState = new MouseState
        {
            position = screenPoint
        }.WithButton(MouseButton.Left, true);
        ApplyMouseState(pressedState);
        yield return null;
        yield return null;

        Vector2 actualPosition = verificationMouse.position.ReadValue();
        Check(
            Vector2.Distance(actualPosition, screenPoint) <= 0.01f,
            $"PLAYMODE_CLICK_{activeClickNumber}_POSITION",
            $"expected={screenPoint}; actual={actualPosition}");
        report.Add(
            $"PLAYMODE_CLICK_{activeClickNumber}_PRESS_STATE; frame={Time.frameCount}; "
            + $"pressed={verificationMouse.leftButton.isPressed}; down={verificationMouse.leftButton.wasPressedThisFrame}");
    }

    private IEnumerator ReleaseMouse(Vector2 screenPoint)
    {
        MouseState releasedState = new MouseState
        {
            position = screenPoint
        };
        ApplyMouseState(releasedState);
        yield return null;
        yield return null;
        report.Add(
            $"PLAYMODE_CLICK_{activeClickNumber}_RELEASE_STATE; frame={Time.frameCount}; "
            + $"pressed={verificationMouse.leftButton.isPressed}; up={verificationMouse.leftButton.wasReleasedThisFrame}");
    }

    private void ApplyMouseState(MouseState state)
    {
        EnsureVerificationMouse();
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
        verificationMouse.MakeCurrent();
        InputState.Change(verificationMouse, state);
        InputSystem.QueueStateEvent(verificationMouse, state);
        InputSystem.Update();
    }

    private void EnsureVerificationMouse()
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

        verificationMouse = InputSystem.AddDevice<Mouse>($"CharacterClickVerificationMouse{++verificationMouseSerial}");
        InputSystem.EnableDevice(verificationMouse);
        verificationMouse.MakeCurrent();
    }

    private IEnumerator WaitForWorldPointClear(Vector2 screenPoint, string key)
    {
        bool clear = false;
        int settledFrames = 0;
        for (; settledFrames < 10; settledFrames++)
        {
            Canvas.ForceUpdateCanvases();
            if (!IsScreenPointOverUi(screenPoint))
            {
                clear = true;
                break;
            }

            yield return null;
        }

        Check(
            clear,
            key,
            clear
                ? $"world point clear after {settledFrames} settled frames"
                : $"blocked after {settledFrames} frames; {DescribeUiHits(screenPoint)}");
    }

    private IEnumerator CaptureScreenshot(string path)
    {
        yield return new WaitForEndOfFrame();
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(path, capture.EncodeToPNG());
        Destroy(capture);
        report.Add("capture=" + path);
    }

    private void OnBuildingClicked(BuildableObject clickedBuilding)
    {
        buildingClickCount++;
    }

    private void Check(bool passed, string key, string detail)
    {
        report.Add($"{key}={(passed ? "PASS" : "FAIL")}; {detail}");
        if (!passed)
        {
            failures.Add(key + ": " + detail);
        }
    }

    private void CleanupAndFinish()
    {
        Cleanup();
        Application.logMessageReceived -= OnLogMessageReceived;

        report.Add($"capturedErrors={capturedErrors.Count}; {Compact(capturedErrors)}");
        report.Add($"capturedWarnings={capturedWarnings.Count}; {Compact(capturedWarnings)}");
        bool passed = failures.Count == 0 && capturedErrors.Count == 0 && capturedWarnings.Count == 0;
        report.Add($"RESULT={(passed ? "PASS" : "FAIL")}; failures={failures.Count}; {Compact(failures)}");
        File.WriteAllText(CharacterClickPriorityPlayModeVerifier.ReportPath, string.Join("\n", report));

        if (passed)
        {
            Debug.Log("Character click priority verification passed. " + CharacterClickPriorityPlayModeVerifier.ReportPath);
        }
        else
        {
            Debug.LogError("Character click priority verification failed. " + CharacterClickPriorityPlayModeVerifier.ReportPath);
        }

        Destroy(gameObject);
        EditorApplication.ExitPlaymode();
    }

    private void Cleanup()
    {
        if (subscribed && building != null)
        {
            building.OnBuildingClicked -= OnBuildingClicked;
            subscribed = false;
        }

        RestoreWorldState();
        if (inputBehaviorCaptured)
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInputBehavior;
            inputBehaviorCaptured = false;
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
        Time.timeScale = originalTimeScale;
    }

    private void RestoreWorldState()
    {
        if (!stateCaptured)
        {
            return;
        }

        if (actor != null)
        {
            actor.transform.position = originalActorPosition;
        }

        Physics2D.SyncTransforms();

        if (Mouse.current != null)
        {
            MouseState restoredState = new MouseState
            {
                position = originalMousePosition
            };
            InputSystem.QueueStateEvent(Mouse.current, restoredState);
            InputSystem.Update();
        }

        stateCaptured = false;
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

    private static string DescribeUiHits(Vector2 screenPoint)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return "EventSystem=<none>";
        }

        PointerEventData pointer = new PointerEventData(eventSystem)
        {
            position = screenPoint
        };
        List<RaycastResult> results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointer, results);
        IEnumerable<string> graphicHits = results
            .Where(result => result.module is GraphicRaycaster)
            .Select(result =>
            {
                GraphicRaycaster raycaster = (GraphicRaycaster)result.module;
                Canvas canvas = raycaster.GetComponent<Canvas>();
                return $"{GetPath(result.gameObject)} canvas={(canvas != null ? canvas.renderMode.ToString() : "<none>")}";
            });
        string joined = string.Join(" || ", graphicHits);
        return string.IsNullOrEmpty(joined) ? "GraphicHits=<none>" : joined;
    }

    private static string GetPath(GameObject target)
    {
        string path = target != null ? target.name : "<null>";
        Transform current = target != null ? target.transform.parent : null;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static string Compact(IReadOnlyList<string> values)
    {
        return values == null || values.Count == 0
            ? "<none>"
            : string.Join(" || ", values.Select(value => value.Replace('\n', ' ').Replace('\r', ' ')));
    }

    private static IGameEventBus ResolveGameEventBus()
    {
        DungeonRuntimeLifetimeScope scope =
            UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include);
        return scope != null ? scope.Container.Resolve<IGameEventBus>() : null;
    }
}
