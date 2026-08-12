#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ServiceRoomVisualValidationFacade
{
    public const string RequestPath =
        "Temp/service-room-pointer-matrix.request";
    public const string ReportPath =
        "Artifacts/QA/service-room-pointer-matrix-report.txt";
    public const string DesktopCapturePath =
        "Artifacts/QA/service-room-1600x900.png";
    public const string PortraitCapturePath =
        "Artifacts/QA/service-room-900x1600.png";

    private const string GameplayScenePath =
        "Assets/Scenes/GameplayScene.unity";
    private const string PersistenceSnapshotId =
        "service-room-pointer-matrix";

    private static bool runnerCreated;

    static ServiceRoomVisualValidationFacade()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/QA/Service Rooms/Request Pointer Matrix")]
    public static void RequestRunFromMenu()
    {
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ReportPath);
        File.Delete(DesktopCapturePath);
        File.Delete(PortraitCapturePath);
        if (!DungeonFinalPlayModeAcceptanceRequestFacade
                .IsPersistenceCoordinatorActive)
        {
            PlayModeVerificationPersistenceSnapshot.CaptureCurrent(
                PersistenceSnapshotId);
        }
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
    }

    [MenuItem("DungeonStory/QA/Service Rooms/Capture 1600x900")]
    public static void CaptureDesktop() => Capture(1600, 900);

    [MenuItem("DungeonStory/QA/Service Rooms/Capture 900x1600")]
    public static void CapturePortrait() => Capture(900, 1600);

    private static void OnEditorUpdate()
    {
        if (!File.Exists(RequestPath)
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(
                GameplayScenePath,
                OpenSceneMode.Single);
        }
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
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
        new GameObject("Service Room Pointer Matrix Runner")
            .AddComponent<ServiceRoomVisualCaptureRunner>()
            .InitializeMatrix();
    }

    private static void Capture(int width, int height)
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException(
                "Service-room visual capture requires PlayMode.");
        }
        if (UnityEngine.Object.FindFirstObjectByType<
                ServiceRoomVisualCaptureRunner>() != null)
        {
            throw new InvalidOperationException(
                "A service-room visual capture is already running.");
        }

        EditorApplication.ExecuteMenuItem("Window/General/Game");
        new GameObject("Service Room Visual Capture Runner")
            .AddComponent<ServiceRoomVisualCaptureRunner>()
            .Initialize(width, height);
    }
}

public sealed class ServiceRoomVisualCaptureRunner : MonoBehaviour
{
    private static readonly Vector2Int[] MatrixResolutions =
    {
        new(1600, 900),
        new(900, 1600)
    };

    private readonly List<string> report = new();
    private readonly List<string> failures = new();
    private readonly List<string> errors = new();
    private readonly List<string> warnings = new();

    private Vector2Int manualResolution;
    private int originalGameViewSizeIndex = -1;
    private bool initialized;
    private bool matrixMode;
    private bool resolutionRestored;

    public void Initialize(int targetWidth, int targetHeight)
    {
        manualResolution = new Vector2Int(
            Mathf.Max(1, targetWidth),
            Mathf.Max(1, targetHeight));
        initialized = true;
    }

    public void InitializeMatrix()
    {
        matrixMode = true;
        initialized = true;
    }

    private IEnumerator Start()
    {
        yield return null;
        if (!initialized)
        {
            Destroy(gameObject);
            yield break;
        }

        Application.logMessageReceived += CaptureLog;
        originalGameViewSizeIndex =
            GameViewResolutionController.SelectedSizeIndex;
        EnsureEventSystem();

        BuildableObject hub = UnityEngine.Object
            .FindObjectsByType<BuildableObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(building =>
                building?.GetServiceHubAbility()?.serviceCategory
                    == ServiceCategory.Dining);
        UIBuildingInfo panel = UnityEngine.Object
            .FindObjectsByType<UIBuildingInfo>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault();
        Check(hub != null,
            "SERVICE_HUB_READY",
            hub != null ? hub.name : "dining service hub missing");
        Check(panel != null,
            "BUILDING_PANEL_READY",
            panel != null ? panel.name : "building panel missing");

        HideBlockingOverlays();
        if (hub != null && panel != null)
        {
            Vector2Int[] resolutions = matrixMode
                ? MatrixResolutions
                : new[] { manualResolution };
            foreach (Vector2Int resolution in resolutions)
            {
                yield return VerifyResolution(hub, panel, resolution);
            }
        }

        Finish();
    }

    private IEnumerator VerifyResolution(
        BuildableObject hub,
        UIBuildingInfo panel,
        Vector2Int resolution)
    {
        GameViewResolutionController.Select(resolution.x, resolution.y);
        float deadline = Time.realtimeSinceStartup + 4f;
        while ((Screen.width != resolution.x
                || Screen.height != resolution.y)
               && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        panel.DisplayBuildingInfo(hub);
        Canvas.ForceUpdateCanvases();
        yield return PlayModeVerificationFrameWait.CaptureReady();

        string suffix = resolution.x + "x" + resolution.y;
        Check(Screen.width == resolution.x && Screen.height == resolution.y,
            "RESOLUTION_" + suffix,
            $"actual={Screen.width}x{Screen.height}");
        Check(panel.gameObject.activeInHierarchy,
            "SERVICE_PANEL_VISIBLE_" + suffix,
            panel.gameObject.name);
        Check(IsInsideScreen(panel.transform as RectTransform),
            "SERVICE_PANEL_BOUNDS_" + suffix,
            DescribeRect(panel.transform as RectTransform));

        if (matrixMode)
        {
            Button modeButton = panel.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button =>
                    button != null
                    && button.gameObject.activeInHierarchy
                    && button.interactable
                    && button.name.StartsWith(
                        "ServiceMode",
                        StringComparison.Ordinal));
            Check(modeButton != null,
                "SERVICE_MODE_TARGET_" + suffix,
                modeButton != null ? modeButton.name : "mode button missing");
            if (modeButton != null)
            {
                yield return BringIntoView(
                    modeButton.transform as RectTransform);
                yield return ClickThroughEventSystem(
                    modeButton,
                    "SERVICE_MODE_POINTER_" + suffix);
                yield return null;
                Canvas.ForceUpdateCanvases();
            }
        }

        string capturePath = matrixMode
            ? resolution.x > resolution.y
                ? ServiceRoomVisualValidationFacade.DesktopCapturePath
                : ServiceRoomVisualValidationFacade.PortraitCapturePath
            : $"Temp/ServiceRooms/service-room-"
                + $"{resolution.x}x{resolution.y}.png";
        yield return Capture(
            capturePath,
            resolution,
            "SERVICE_CAPTURE_" + suffix);
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
            Bounds bounds = RectTransformUtility
                .CalculateRelativeRectTransformBounds(viewport, target);
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

    private IEnumerator ClickThroughEventSystem(Button button, string key)
    {
        if (button == null || EventSystem.current == null)
        {
            Check(false, key, "button or EventSystem missing");
            yield break;
        }

        string buttonName = button.name;
        Vector2 point = Vector2.zero;
        GameObject topHandler = null;
        int hitCount = 0;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            button = FindObjectsByType<Button>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null
                    && candidate.interactable
                    && candidate.name == buttonName);
            if (button == null)
            {
                yield return null;
                continue;
            }

            Canvas.ForceUpdateCanvases();
            yield return null;
            RectTransform rect = button.transform as RectTransform;
            Canvas canvas = button.GetComponentInParent<Canvas>();
            Camera camera = canvas != null
                && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera ?? Camera.main
                    : null;
            point = RectTransformUtility.WorldToScreenPoint(
                camera,
                rect.TransformPoint(rect.rect.center));
            PointerEventData pointer = new(EventSystem.current)
            {
                position = point,
                button = PointerEventData.InputButton.Left
            };
            List<RaycastResult> hits = new();
            EventSystem.current.RaycastAll(pointer, hits);
            hitCount = hits.Count;
            topHandler = hits
                .Select(hit => ExecuteEvents.GetEventHandler<IPointerClickHandler>(
                    hit.gameObject))
                .FirstOrDefault(handler => handler != null);
            if (topHandler == button.gameObject)
            {
                Check(true,
                    key + "_HIT_TEST",
                    $"top={topHandler.name}; expected={button.name}; point={point}; hits={hitCount}");
                Check(
                    PlayModeVerificationFrameWait.DispatchPointerClick(
                        button.gameObject,
                        point),
                    key + "_DISPATCH",
                    "Unity EventSystem pointer dispatch");
                yield break;
            }
        }

        Check(false,
            key + "_HIT_TEST",
            $"top={topHandler?.name ?? "none"}; expected={buttonName}; point={point}; hits={hitCount}");
    }

    private IEnumerator Capture(
        string path,
        Vector2Int resolution,
        string key)
    {
        yield return PlayModeVerificationFrameWait.CaptureReady();
        Texture2D texture =
            PlayModeVerificationFrameWait.CaptureScreenshotAsTexture();
        if (texture == null)
        {
            Check(false, key, "capture returned null");
            yield break;
        }

        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(path) ?? "Artifacts/QA");
            byte[] bytes = texture.EncodeToPNG();
            Color32[] pixels = texture.GetPixels32();
            int visible = pixels.Count(pixel =>
                pixel.a > 0
                && (pixel.r > 5 || pixel.g > 5 || pixel.b > 5));
            File.WriteAllBytes(path, bytes);
            Check(texture.width == resolution.x
                && texture.height == resolution.y
                && bytes.Length > 1000
                && visible > pixels.Length / 20,
                key,
                $"size={texture.width}x{texture.height}; "
                + $"bytes={bytes.Length}; visible={visible}; path={path}");
        }
        finally
        {
            Destroy(texture);
        }
    }

    private bool Check(bool condition, string key, string detail)
    {
        report.Add($"[{(condition ? "PASS" : "FAIL")}] {key} {detail}");
        if (!condition)
        {
            failures.Add(key + ": " + detail);
        }
        return condition;
    }

    private void CaptureLog(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type is LogType.Error or LogType.Exception or LogType.Assert)
        {
            errors.Add(condition + "\n" + stackTrace);
        }
        else if (type == LogType.Warning)
        {
            warnings.Add(condition);
        }
    }

    private void Finish()
    {
        RestoreResolution();
        Application.logMessageReceived -= CaptureLog;
        bool passed = failures.Count == 0
            && errors.Count == 0
            && warnings.Count == 0;
        report.Add($"capturedErrors={errors.Count}; {Compact(errors)}");
        report.Add($"capturedWarnings={warnings.Count}; {Compact(warnings)}");
        report.Add($"RESULT={(passed ? "PASS" : "FAIL")}; "
            + $"failures={failures.Count}; {Compact(failures)}");

        if (matrixMode)
        {
            File.WriteAllText(
                ServiceRoomVisualValidationFacade.ReportPath,
                string.Join("\n", report));
            File.Delete(ServiceRoomVisualValidationFacade.RequestPath);
            if (passed)
            {
                Debug.Log(
                    "Service-room pointer matrix verification passed. "
                    + ServiceRoomVisualValidationFacade.ReportPath);
            }
            else
            {
                Debug.LogError(
                    "Service-room pointer matrix verification failed. "
                    + ServiceRoomVisualValidationFacade.ReportPath);
            }
            EditorApplication.ExitPlaymode();
        }
        else if (!passed)
        {
            Debug.LogError(
                "Service-room visual capture validation failed: "
                + Compact(failures.Concat(errors).Concat(warnings)));
        }

        Destroy(gameObject);
    }

    private void RestoreResolution()
    {
        if (resolutionRestored || originalGameViewSizeIndex < 0)
        {
            return;
        }
        resolutionRestored = true;
        GameViewResolutionController.SelectedSizeIndex =
            originalGameViewSizeIndex;
    }

    private void OnDestroy()
    {
        RestoreResolution();
        Application.logMessageReceived -= CaptureLog;
    }

    private static void HideBlockingOverlays()
    {
        foreach (GameObject overlay in Resources
                     .FindObjectsOfTypeAll<GameObject>()
                     .Where(candidate =>
                         candidate != null
                         && candidate.scene.IsValid()
                         && (candidate.name == "OwnerSelectionSurface"
                             || candidate.name == "OwnerSelectionPanel")))
        {
            overlay.SetActive(false);
        }
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            new GameObject("QA_ServiceRoom_EventSystem", typeof(EventSystem));
        }
    }

    private static bool IsInsideScreen(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy)
        {
            return false;
        }
        Rect target = GetScreenRect(rect);
        Rect screen = new(0f, 0f, Screen.width, Screen.height);
        const float tolerance = 1f;
        return target.xMin >= screen.xMin - tolerance
            && target.yMin >= screen.yMin - tolerance
            && target.xMax <= screen.xMax + tolerance
            && target.yMax <= screen.yMax + tolerance;
    }

    private static Rect GetScreenRect(RectTransform rect)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null
            && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera ?? Camera.main
                : null;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2[] screenCorners = corners
            .Select(corner => RectTransformUtility.WorldToScreenPoint(
                camera,
                corner))
            .ToArray();
        return Rect.MinMaxRect(
            screenCorners.Min(corner => corner.x),
            screenCorners.Min(corner => corner.y),
            screenCorners.Max(corner => corner.x),
            screenCorners.Max(corner => corner.y));
    }

    private static string DescribeRect(RectTransform rect) =>
        rect != null ? GetScreenRect(rect).ToString() : "missing";

    private static string Compact(IEnumerable<string> values)
    {
        string value = string.Join(" | ", values ?? Array.Empty<string>());
        return string.IsNullOrWhiteSpace(value)
            ? "<none>"
            : value.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
#endif
