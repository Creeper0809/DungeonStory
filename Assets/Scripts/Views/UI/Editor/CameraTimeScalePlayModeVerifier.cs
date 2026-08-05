using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CameraTimeScalePlayModeVerifier
{
    public const string RequestPath = "Temp/camera-time-scale-verification.request";
    public const string ReportPath = "Temp/camera-time-scale-verification-report.txt";

    private static bool runnerCreated;

    static CameraTimeScalePlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/QA/Request Camera Time-Scale Verification")]
    public static void RequestRunFromMenu()
    {
        Directory.CreateDirectory("Temp");
        File.Delete(ReportPath);
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
    }

    private static void OnEditorUpdate()
    {
        if (File.Exists(RequestPath) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.EnterPlaymode();
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            runnerCreated = false;
            return;
        }

        if (change != PlayModeStateChange.EnteredPlayMode || runnerCreated || !File.Exists(RequestPath))
        {
            return;
        }

        runnerCreated = true;
        new GameObject("Camera Time-Scale Verification Runner")
            .AddComponent<CameraTimeScaleVerificationRunner>();
    }
}

public sealed class CameraTimeScaleVerificationRunner : MonoBehaviour
{
    private const float HoldSeconds = 0.6f;

    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();

    private CameraManager cameraManager;
    private DungeonAutomationInputTestCapability automationInput;
    private Vector3 originalPosition;
    private float originalTimeScale;
    private bool originalEdgeScroll;
    private bool originalZoomEnabled;
    private bool originalBlockWheelZoomOverUi;
    private float originalOrthographicSize;
    private bool stateCaptured;

    private IEnumerator Start()
    {
        Application.logMessageReceived += CaptureLog;

        float resolveDeadline = Time.realtimeSinceStartup + 10f;
        while (cameraManager == null && Time.realtimeSinceStartup < resolveDeadline)
        {
            cameraManager = UnityEngine.Object.FindFirstObjectByType<CameraManager>(FindObjectsInactive.Include);
            if (cameraManager == null)
            {
                yield return null;
            }
        }

        Check(cameraManager != null, "CAMERA_MANAGER", "runtime camera manager resolved");
        if (cameraManager != null)
        {
            CaptureState();
            cameraManager.enableEdgeScroll = false;
            automationInput = new DungeonAutomationInputTestCapability();
            automationInput.Enable();

            float pausedDistance = 0f;
            float oneXDistance = 0f;
            float fiveXDistance = 0f;
            yield return MeasureDistance(0f, value => pausedDistance = value);
            yield return MeasureDistance(1f, value => oneXDistance = value);
            yield return MeasureDistance(5f, value => fiveXDistance = value);

            float minimumDistance = Mathf.Min(pausedDistance, oneXDistance, fiveXDistance);
            float maximumDistance = Mathf.Max(pausedDistance, oneXDistance, fiveXDistance);
            float difference = maximumDistance - minimumDistance;
            float tolerance = Mathf.Max(0.15f, maximumDistance * 0.08f);
            Check(pausedDistance > 0.25f, "CAMERA_MOVES_PAUSED", $"distance={pausedDistance:0.0000}");
            Check(oneXDistance > 0.25f, "CAMERA_MOVES_1X", $"distance={oneXDistance:0.0000}");
            Check(fiveXDistance > 0.25f, "CAMERA_MOVES_5X", $"distance={fiveXDistance:0.0000}");
            Check(difference <= tolerance,
                "CAMERA_TIME_SCALE_INDEPENDENT",
                $"paused={pausedDistance:0.0000}; oneX={oneXDistance:0.0000}; fiveX={fiveXDistance:0.0000}; difference={difference:0.0000}; tolerance={tolerance:0.0000}");
            report.Add("movementClock=Time.unscaledDeltaTime");

            yield return VerifyZoomInput();
        }

        RestoreState();
        Finish();
        Destroy(gameObject);
        EditorApplication.ExitPlaymode();
    }

    private IEnumerator MeasureDistance(float timeScale, Action<float> complete)
    {
        Time.timeScale = timeScale;
        cameraManager.transform.position = new Vector3(-9999f, originalPosition.y, originalPosition.z);
        cameraManager.ClampToCurrentBounds();
        yield return null;

        float startX = cameraManager.transform.position.x;
        automationInput.HoldKey(KeyCode.D, HoldSeconds);
        double startedAt = Time.realtimeSinceStartupAsDouble;
        while (Time.realtimeSinceStartupAsDouble - startedAt < HoldSeconds)
        {
            yield return null;
        }

        automationInput.ReleaseKey(KeyCode.D);
        complete(Mathf.Max(0f, cameraManager.transform.position.x - startX));
        yield return null;
    }

    private void CaptureState()
    {
        originalPosition = cameraManager.transform.position;
        originalTimeScale = Time.timeScale;
        originalEdgeScroll = cameraManager.enableEdgeScroll;
        originalZoomEnabled = cameraManager.enableZoom;
        originalBlockWheelZoomOverUi = cameraManager.blockWheelZoomOverUi;
        Camera camera = cameraManager.GetComponent<Camera>();
        originalOrthographicSize = camera != null ? camera.orthographicSize : 0f;
        stateCaptured = true;
    }

    private IEnumerator VerifyZoomInput()
    {
        Camera camera = cameraManager.GetComponent<Camera>();
        Check(camera != null && camera.orthographic, "CAMERA_ZOOM_CAMERA", "orthographic gameplay camera resolved");
        if (camera == null || !camera.orthographic)
        {
            yield break;
        }

        Time.timeScale = 0f;
        cameraManager.enableZoom = true;
        cameraManager.blockWheelZoomOverUi = true;
        float minimum = Mathf.Min(cameraManager.zoomBounds.x, cameraManager.zoomBounds.y);
        float maximum = Mathf.Max(cameraManager.zoomBounds.x, cameraManager.zoomBounds.y);
        camera.orthographicSize = Mathf.Clamp((minimum + maximum) * 0.5f, minimum, maximum);
        automationInput.MovePointer(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        yield return null;

        float before = camera.orthographicSize;
        automationInput.Scroll(120f);
        yield return null;
        float zoomedIn = camera.orthographicSize;
        automationInput.Scroll(-120f);
        yield return null;
        float zoomedOut = camera.orthographicSize;

        Check(zoomedIn < before - 0.01f,
            "CAMERA_WHEEL_ZOOM_IN",
            $"before={before:0.###}; after={zoomedIn:0.###}");
        Check(zoomedOut > zoomedIn + 0.01f,
            "CAMERA_WHEEL_ZOOM_OUT",
            $"in={zoomedIn:0.###}; out={zoomedOut:0.###}");
        Check(Mathf.Abs(zoomedOut - before) <= cameraManager.wheelZoomStep + 0.05f,
            "CAMERA_WHEEL_ZOOM_REVERSIBLE",
            $"before={before:0.###}; restored={zoomedOut:0.###}");
        report.Add("zoomClock=Time.unscaledDeltaTime; wheelUiRule=scrollable-only");
    }

    private void RestoreState()
    {
        automationInput?.ReleaseKey(KeyCode.D);
        automationInput?.Dispose();
        automationInput = null;
        if (!stateCaptured || cameraManager == null)
        {
            return;
        }

        Time.timeScale = originalTimeScale;
        cameraManager.enableEdgeScroll = originalEdgeScroll;
        cameraManager.enableZoom = originalZoomEnabled;
        cameraManager.blockWheelZoomOverUi = originalBlockWheelZoomOverUi;
        Camera camera = cameraManager.GetComponent<Camera>();
        if (camera != null && originalOrthographicSize > 0f)
        {
            camera.orthographicSize = originalOrthographicSize;
        }
        cameraManager.transform.position = originalPosition;
        cameraManager.ClampToCurrentBounds();
        stateCaptured = false;
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
        if (type == LogType.Warning)
        {
            warnings.Add(condition);
        }
        else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors.Add(condition);
        }
    }

    private void Finish()
    {
        Application.logMessageReceived -= CaptureLog;
        report.Add($"capturedErrors={errors.Count}; capturedWarnings={warnings.Count}");
        bool passed = failures.Count == 0 && errors.Count == 0 && warnings.Count == 0;
        report.Insert(0, passed ? "CAMERA_TIME_SCALE PASS" : "CAMERA_TIME_SCALE FAIL");
        if (errors.Count > 0)
        {
            report.Add("errors=" + string.Join(" || ", errors));
        }

        if (warnings.Count > 0)
        {
            report.Add("warnings=" + string.Join(" || ", warnings));
        }

        Directory.CreateDirectory("Temp");
        File.WriteAllLines(CameraTimeScalePlayModeVerifier.ReportPath, report);
        File.Delete(CameraTimeScalePlayModeVerifier.RequestPath);
        if (passed)
        {
            Debug.Log("Camera time-scale verification passed. " + CameraTimeScalePlayModeVerifier.ReportPath);
        }
        else
        {
            Debug.LogError("Camera time-scale verification failed. " + CameraTimeScalePlayModeVerifier.ReportPath);
        }
    }

    private void OnDestroy()
    {
        RestoreState();
        Application.logMessageReceived -= CaptureLog;
    }
}
