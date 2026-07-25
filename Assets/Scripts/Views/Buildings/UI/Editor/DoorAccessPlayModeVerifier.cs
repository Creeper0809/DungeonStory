using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using VContainer;

public static class DoorAccessPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/CaptivityCircusDoorAccess/door-access-playmode.txt";
    public const string DesktopCapturePath =
        "Artifacts/QA/CaptivityCircusDoorAccess/door-access-1600x900.png";
    public const string MobileCapturePath =
        "Artifacts/QA/CaptivityCircusDoorAccess/door-access-900x1600.png";

    [MenuItem("DungeonStory/Debug/QA/Run Door Access PlayMode Verification")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("Door access verification requires PlayMode.");
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<DoorAccessPlayModeVerificationRunner>() != null)
        {
            Debug.LogWarning("Door access verification is already running.");
            return;
        }

        EditorApplication.ExecuteMenuItem("Window/General/Game");
        new GameObject("Door Access PlayMode Verification Runner")
            .AddComponent<DoorAccessPlayModeVerificationRunner>();
    }
}

public sealed class DoorAccessPlayModeVerificationRunner : MonoBehaviour
{
    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> consoleErrors = new List<string>();
    private readonly List<string> consoleWarnings = new List<string>();

    private InputSettings.EditorInputBehaviorInPlayMode originalInputBehavior;
    private Mouse originalMouse;
    private Mouse verificationMouse;
    private int originalResolutionIndex = -1;
    private Door door;
    private DoorAccessPolicyState originalPolicy;
    private bool cleanedUp;

    private IEnumerator Start()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DoorAccessPlayModeVerifier.ReportPath));
        Application.logMessageReceived += CaptureLog;
        originalResolutionIndex = GameViewResolutionController.SelectedSizeIndex;
        originalInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        originalMouse = Mouse.current;
        if (originalMouse != null)
        {
            InputSystem.DisableDevice(originalMouse);
        }

        verificationMouse = InputSystem.AddDevice<Mouse>(
            "DoorAccessPlayModeVerificationMouse");
        InputSystem.EnableDevice(verificationMouse);
        verificationMouse.MakeCurrent();

        yield return EnsurePlayableRun();
        yield return VerifyAtResolution(
            new Vector2Int(1600, 900),
            DoorAccessPlayModeVerifier.DesktopCapturePath,
            verifyPointerInteraction: true);
        yield return VerifyAtResolution(
            new Vector2Int(900, 1600),
            DoorAccessPlayModeVerifier.MobileCapturePath,
            verifyPointerInteraction: false);

        Finish();
    }

    private IEnumerator EnsurePlayableRun()
    {
        OwnerRunManager ownerManager =
            FindFirstObjectByType<OwnerRunManager>(FindObjectsInactive.Include);
        if (ownerManager != null && ownerManager.CurrentOwnerActor == null)
        {
            Button ownerOption = Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(candidate =>
                    candidate != null
                    && candidate.gameObject.scene.IsValid()
                    && candidate.gameObject.activeInHierarchy
                    && candidate.name.StartsWith("OwnerOption_", StringComparison.Ordinal));
            yield return Click(ownerOption);
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible();
            yield return new WaitForSecondsRealtime(0.25f);
        }

        Check(
            ownerManager != null && ownerManager.CurrentOwnerActor != null,
            "PLAYABLE_RUN",
            ownerManager?.CurrentOwnerActor != null
                ? "사장 선택이 끝난 실제 게임 상태"
                : "사장 선택을 완료하지 못함");

        VerifyDomainComposition();
    }

    private void VerifyDomainComposition()
    {
        DungeonRuntimeLifetimeScope scope =
            FindFirstObjectByType<DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include);
        Check(
            scope != null && scope.Container != null,
            "DOMAIN_SCOPE",
            scope != null ? "게임플레이 컨테이너 준비" : "게임플레이 컨테이너 없음");
        if (scope == null || scope.Container == null)
        {
            return;
        }

        try
        {
            ICaptivityRuntime captivity = scope.Container.Resolve<ICaptivityRuntime>();
            ICaptivityCommandService captivityCommands =
                scope.Container.Resolve<ICaptivityCommandService>();
            ICircusRuntime circus = scope.Container.Resolve<ICircusRuntime>();
            IWildlifeCaptureRuntime wildlife =
                scope.Container.Resolve<IWildlifeCaptureRuntime>();
            IDoorAccessQuery doorAccess = scope.Container.Resolve<IDoorAccessQuery>();
            IDoorAccessCommandService doorCommands =
                scope.Container.Resolve<IDoorAccessCommandService>();
            BuildingSO[] assets =
                Resources.LoadAll<BuildingSO>("SO/Building/Captivity");

            Check(
                captivity != null
                && captivityCommands != null
                && circus != null
                && wildlife != null
                && doorAccess != null
                && doorCommands != null,
                "DOMAIN_SERVICES",
                "포로·공연·야생 생포·문 권한 서비스 해석");
            Check(
                assets.Length == 10
                && assets.All(item =>
                    item != null
                    && item.Abilities != null
                    && item.Abilities.Count > 0),
                "DOMAIN_ASSETS",
                $"설비 자산 {assets.Length}/10 · 능력 모듈 연결");
        }
        catch (Exception exception)
        {
            Check(false, "DOMAIN_SERVICES", exception.GetBaseException().Message);
        }
    }

    private IEnumerator VerifyAtResolution(
        Vector2Int resolution,
        string capturePath,
        bool verifyPointerInteraction)
    {
        GameViewResolutionController.Select(resolution.x, resolution.y);
        yield return WaitForResolution(resolution);
        Canvas.ForceUpdateCanvases();
        yield return null;

        if (verifyPointerInteraction)
        {
            yield return VerifyDoorPointerFlow();
        }

        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(capturePath, capture.EncodeToPNG());
        report.Add(
            $"capture={capturePath}; requested={resolution.x}x{resolution.y}; "
            + $"actual={capture.width}x{capture.height}");
        Destroy(capture);

        RectTransform accessHeader = Resources.FindObjectsOfTypeAll<RectTransform>()
            .FirstOrDefault(item =>
                item != null
                && item.gameObject.scene.IsValid()
                && item.gameObject.activeInHierarchy
                && item.name == "DoorAccessHeader");
        Check(
            accessHeader != null && IsRectInsideScreen(accessHeader),
            $"PANEL_INSIDE_{resolution.x}x{resolution.y}",
            accessHeader != null
                ? DescribeRect(accessHeader)
                : "권한 패널 헤더 없음");
    }

    private IEnumerator VerifyDoorPointerFlow()
    {
        Camera camera = Camera.main;
        UIBuildingInfo info = Resources.FindObjectsOfTypeAll<UIBuildingInfo>()
            .FirstOrDefault(candidate =>
                candidate != null && candidate.gameObject.scene.IsValid());
        Check(camera != null, "CAMERA", "메인 카메라 조회");
        Check(info != null, "BUILDING_INFO", "건물 상세 패널 조회");
        if (camera == null || info == null)
        {
            yield break;
        }

        door = FindClickableDoor(camera);
        Check(door != null, "VISIBLE_DOOR", door != null ? door.name : "화면 안 문 없음");
        if (door == null)
        {
            yield break;
        }

        originalPolicy = door.AccessPolicy.Clone();
        Collider2D collider = door.GetComponent<Collider2D>();
        Vector2 doorScreenPoint = camera.WorldToScreenPoint(collider.bounds.center);
        yield return ClickScreenPoint(doorScreenPoint);
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;

        BuildingSummaryInfo summary = FindFirstObjectByType<BuildingSummaryInfo>(
            FindObjectsInactive.Include);
        Check(
            summary != null
            && summary.UI != null
            && summary.UI.activeInHierarchy
            && summary.objectName != null
            && summary.objectName.text == door.BuildingData.objectName,
            "EXACT_DOOR_CLICK",
            $"summary={summary?.objectName?.text}; expected={door.BuildingData.objectName}");
        Button detailButton = FindActive<Button>("CleanPriorityButton");
        yield return Click(detailButton);
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;

        Toggle captiveToggle = FindActive<Toggle>("Toggle_포로");
        Check(
            info.gameObject.activeInHierarchy && captiveToggle != null,
            "DOOR_PANEL_OPEN",
            $"info={info.gameObject.activeInHierarchy}; captiveToggle={captiveToggle != null}");
        if (captiveToggle == null)
        {
            yield break;
        }

        bool captiveBefore = door.AccessPolicy.IsGroupAllowed(DoorAccessGroup.Captive);
        yield return Click(captiveToggle);
        yield return null;
        bool captiveAfter = door.AccessPolicy.IsGroupAllowed(DoorAccessGroup.Captive);
        Check(
            captiveBefore && !captiveAfter,
            "CAPTIVE_TOGGLE",
            $"before={captiveBefore}; after={captiveAfter}");
        Check(
            info.gameObject.activeInHierarchy
            && info.nameText != null
            && info.nameText.text == door.BuildingData.objectName,
            "UI_BLOCKS_WORLD_CLICK",
            $"detail={info.nameText?.text}; expected={door.BuildingData.objectName}");
        Check(
            door.GetComponent<DoorAccessLockIndicator>() != null,
            "LOCK_INDICATOR",
            "제한된 문에 자물쇠 표시 컴포넌트 존재");

        Button staffOnly = FindActive<Button>("Button_직원_전용");
        yield return Click(staffOnly);
        yield return null;
        Check(
            door.AccessPolicy.IsGroupAllowed(DoorAccessGroup.Owner)
            && door.AccessPolicy.IsGroupAllowed(DoorAccessGroup.Staff)
            && !door.AccessPolicy.IsGroupAllowed(DoorAccessGroup.Customer)
            && !door.AccessPolicy.IsGroupAllowed(DoorAccessGroup.Captive),
            "STAFF_PRESET",
            $"groups={door.AccessPolicy.AllowedGroups}");

        Button allowAll = FindActive<Button>("Button_모두_허용");
        yield return Click(allowAll);
        yield return null;
        Check(
            door.AccessPolicy.AllowedGroups == DoorAccessGroup.All,
            "ALLOW_ALL_PRESET",
            $"groups={door.AccessPolicy.AllowedGroups}");
    }

    private Door FindClickableDoor(Camera camera)
    {
        foreach (Door candidate in Resources.FindObjectsOfTypeAll<Door>())
        {
            if (candidate == null
                || !candidate.gameObject.scene.IsValid()
                || !candidate.gameObject.activeInHierarchy
                || candidate.AccessPolicy == null)
            {
                continue;
            }

            Collider2D collider = candidate.GetComponent<Collider2D>();
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            Vector3 viewport = camera.WorldToViewportPoint(collider.bounds.center);
            if (viewport.z <= 0f
                || viewport.x < 0.08f
                || viewport.x > 0.92f
                || viewport.y < 0.14f
                || viewport.y > 0.88f)
            {
                continue;
            }

            Vector2 screenPoint = camera.WorldToScreenPoint(collider.bounds.center);
            if (!IsScreenPointOverUi(screenPoint))
            {
                return candidate;
            }
        }

        return null;
    }

    private IEnumerator Click(Component target)
    {
        Selectable selectable = target as Selectable;
        bool available = target != null
            && target.gameObject.activeInHierarchy
            && (selectable == null || selectable.interactable);
        Check(
            available,
            "POINTER_TARGET",
            target != null ? target.name : "<missing>");
        if (!available)
        {
            yield break;
        }

        Canvas.ForceUpdateCanvases();
        RectTransform rect = target.GetComponent<RectTransform>();
        Camera canvasCamera = GetCanvasCamera(rect);
        Vector2 point = RectTransformUtility.WorldToScreenPoint(
            canvasCamera,
            rect.TransformPoint(rect.rect.center));
        yield return ClickScreenPoint(point);
    }

    private IEnumerator ClickScreenPoint(Vector2 point)
    {
        QueueMouse(new MouseState { position = point });
        yield return null;
        QueueMouse(new MouseState { position = point }.WithButton(MouseButton.Left, true));
        yield return null;
        yield return null;
        QueueMouse(new MouseState { position = point });
        yield return null;
        yield return null;
    }

    private void QueueMouse(MouseState state)
    {
        if (verificationMouse == null || !verificationMouse.added)
        {
            return;
        }

        verificationMouse.MakeCurrent();
        InputState.Change(verificationMouse, state);
        InputSystem.QueueStateEvent(verificationMouse, state);
        InputSystem.Update();
    }

    private static Camera GetCanvasCamera(RectTransform rect)
    {
        Canvas canvas = rect != null ? rect.GetComponentInParent<Canvas>() : null;
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
    }

    private static T FindActive<T>(string name) where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(candidate =>
                candidate != null
                && candidate.gameObject.scene.IsValid()
                && candidate.gameObject.activeInHierarchy
                && candidate.name == name);
    }

    private static bool IsScreenPointOverUi(Vector2 screenPoint)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        List<RaycastResult> hits = new List<RaycastResult>();
        eventSystem.RaycastAll(
            new PointerEventData(eventSystem) { position = screenPoint },
            hits);
        return hits.Any(hit =>
            hit.gameObject != null
            && hit.gameObject.GetComponentInParent<Canvas>() != null);
    }

    private static bool IsRectInsideScreen(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Camera canvasCamera = GetCanvasCamera(rect);
        return corners.All(corner =>
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(canvasCamera, corner);
            return screen.x >= -1f
                && screen.y >= -1f
                && screen.x <= Screen.width + 1f
                && screen.y <= Screen.height + 1f;
        });
    }

    private static string DescribeRect(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Camera canvasCamera = GetCanvasCamera(rect);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(canvasCamera, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(canvasCamera, corners[2]);
        return $"screen={Screen.width}x{Screen.height}; min={min}; max={max}";
    }

    private static IEnumerator WaitForResolution(Vector2Int expected)
    {
        float deadline = Time.realtimeSinceStartup + 4f;
        while ((Screen.width != expected.x || Screen.height != expected.y)
            && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        yield return null;
        yield return null;
    }

    private void Check(bool passed, string key, string detail)
    {
        report.Add($"{key}={(passed ? "PASS" : "FAIL")}; {detail}");
        if (!passed)
        {
            failures.Add(key + ": " + detail);
        }
    }

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            consoleErrors.Add(condition);
        }
        else if (type == LogType.Warning)
        {
            consoleWarnings.Add(condition);
        }
    }

    private void Finish()
    {
        Cleanup();
        report.Add($"consoleErrors={consoleErrors.Count}; {string.Join(" | ", consoleErrors)}");
        report.Add($"consoleWarnings={consoleWarnings.Count}; {string.Join(" | ", consoleWarnings)}");
        bool passed = failures.Count == 0
            && consoleErrors.Count == 0
            && consoleWarnings.Count == 0;
        report.Add($"RESULT={(passed ? "PASS" : "FAIL")}; failures={string.Join(" | ", failures)}");
        File.WriteAllLines(DoorAccessPlayModeVerifier.ReportPath, report);
        if (passed)
        {
            Debug.Log("Door access PlayMode verification passed. "
                + DoorAccessPlayModeVerifier.ReportPath);
        }
        else
        {
            Debug.LogError("Door access PlayMode verification failed. "
                + DoorAccessPlayModeVerifier.ReportPath);
        }

        Destroy(gameObject);
        EditorApplication.ExitPlaymode();
    }

    private void Cleanup()
    {
        if (cleanedUp)
        {
            return;
        }

        cleanedUp = true;
        if (door?.AccessStateModule != null && originalPolicy != null)
        {
            door.AccessStateModule.CopyFrom(originalPolicy);
        }
        if (originalResolutionIndex >= 0)
        {
            GameViewResolutionController.SelectedSizeIndex = originalResolutionIndex;
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
        Application.logMessageReceived -= CaptureLog;
    }

    private void OnDestroy()
    {
        Cleanup();
    }
}
