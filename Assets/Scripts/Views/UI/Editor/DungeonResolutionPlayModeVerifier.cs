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
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DungeonResolutionPlayModeVerifier
{
    public const string ReportPath = "Temp/resolution-matrix-report.txt";

    [MenuItem("DungeonStory/Debug/QA/Run Resolution Matrix Verification")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("Resolution matrix verification requires PlayMode.");
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<DungeonResolutionVerificationRunner>() != null)
        {
            Debug.LogWarning("Resolution matrix verification is already running.");
            return;
        }

        EditorApplication.ExecuteMenuItem("Window/General/Game");
        new GameObject("Resolution Matrix Verification Runner")
            .AddComponent<DungeonResolutionVerificationRunner>();
    }
}

public sealed class DungeonResolutionVerificationRunner : MonoBehaviour
{
    private static readonly Vector2Int[] Resolutions =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440),
        new Vector2Int(900, 1600)
    };

    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();

    private int originalGameViewSizeIndex = -1;
    private bool sizeRestored;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Temp");
        PlayModeVerificationPersistenceSnapshot.CaptureCurrent("resolution-matrix");
        Application.logMessageReceived += CaptureLog;
        originalGameViewSizeIndex = GameViewResolutionController.SelectedSizeIndex;
        yield return null;
        yield return null;
        yield return EnsureTitleScene();

        foreach (Vector2Int resolution in Resolutions)
        {
            yield return SelectResolution(resolution);
            VerifyTitle(resolution);
            yield return Capture($"Temp/resolution-{resolution.x}x{resolution.y}-title.png", resolution, "TITLE_CAPTURE");

            Button settingsButton = FindSceneButton("StartupSettingsButton");
            Check(settingsButton != null && settingsButton.interactable,
                $"SETTINGS_BUTTON_{Key(resolution)}",
                "startup settings button is available");
            if (settingsButton != null && settingsButton.interactable)
            {
                PressButton(settingsButton);
                yield return null;
                yield return null;
                VerifySettings(resolution);
                yield return Capture(
                    $"Temp/resolution-{resolution.x}x{resolution.y}-settings.png",
                    resolution,
                    "SETTINGS_CAPTURE");
                Button close = FindSceneButton("SettingsCloseButton");
                if (close != null && close.interactable)
                {
                    PressButton(close);
                    yield return null;
                }
            }
        }

        yield return EnsurePlayableRun();
        foreach (Vector2Int resolution in Resolutions)
        {
            yield return SelectResolution(resolution);
            VerifyGameplayHud(resolution);
            yield return Capture(
                $"Temp/resolution-{resolution.x}x{resolution.y}-game.png",
                resolution,
                "GAME_CAPTURE");
        }

        RestoreGameViewSize();
        yield return null;
        Finish();
        Destroy(gameObject);
        EditorApplication.ExitPlaymode();
    }

    private IEnumerator EnsureTitleScene()
    {
        if (SceneManager.GetActiveScene().name != DungeonSceneNavigator.TitleSceneName)
        {
            SceneManager.LoadScene(DungeonSceneNavigator.TitleSceneName, LoadSceneMode.Single);
        }

        float deadline = Time.realtimeSinceStartup + 8f;
        while (SceneManager.GetActiveScene().name != DungeonSceneNavigator.TitleSceneName
            && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        yield return null;
        yield return null;
    }

    private IEnumerator SelectResolution(Vector2Int resolution)
    {
        GameViewResolutionController.Select(resolution.x, resolution.y);
        float deadline = Time.realtimeSinceStartup + 3f;
        while ((Screen.width != resolution.x || Screen.height != resolution.y)
            && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        Check(Screen.width == resolution.x && Screen.height == resolution.y,
            $"RESOLUTION_{Key(resolution)}",
            $"actual={Screen.width}x{Screen.height}");
    }

    private void VerifyTitle(Vector2Int resolution)
    {
        GameObject title = FindSceneObject("DungeonTitleRuntimeUI");
        RectTransform brand = FindSceneObject("TitleBrand")?.GetComponent<RectTransform>();
        RectTransform saves = FindSceneObject("TitleSavePanel")?.GetComponent<RectTransform>();
        Check(SceneManager.GetActiveScene().name == DungeonSceneNavigator.TitleSceneName
                && title != null && title.activeInHierarchy,
            $"TITLE_VISIBLE_{Key(resolution)}",
            "dedicated title scene remains visible");
        Check(IsInsideScreen(brand) && IsInsideScreen(saves),
            $"TITLE_BOUNDS_{Key(resolution)}",
            $"brand={DescribeRect(brand)}; saves={DescribeRect(saves)}");
        VerifyNamedButtonsInside(
            resolution,
            "TITLE_BUTTONS",
            "ContinueLatestButton",
            "StartNewRunButton",
            "StartupSettingsButton",
            "StartupQuitButton");
        VerifyTextOverflow(title?.GetComponent<RectTransform>(), resolution, "TITLE_TEXT");
    }

    private void VerifySettings(Vector2Int resolution)
    {
        GameObject modal = FindSceneObject("SettingsModal");
        RectTransform panel = FindSceneObject("SettingsPanel")?.GetComponent<RectTransform>();
        Check(modal != null && modal.activeInHierarchy,
            $"SETTINGS_VISIBLE_{Key(resolution)}",
            "settings modal is visible");
        Check(IsInsideScreen(panel),
            $"SETTINGS_BOUNDS_{Key(resolution)}",
            DescribeRect(panel));
        VerifyNamedButtonsInside(
            resolution,
            "SETTINGS_BUTTONS",
            "DisplaySettingsTab",
            "AudioSettingsTab",
            "AccessibilitySettingsTab",
            "SettingsCloseButton",
            "ApplySettingsButton");
        VerifyTextOverflow(panel, resolution, "SETTINGS_TEXT");
    }

    private IEnumerator EnsurePlayableRun()
    {
        Button continueButton = FindSceneButton("ContinueLatestButton");
        if (continueButton != null && continueButton.gameObject.activeInHierarchy && continueButton.interactable)
        {
            PressButton(continueButton);
        }
        else
        {
            Button startNewButton = FindSceneButton("StartNewRunButton");
            if (startNewButton != null && startNewButton.interactable)
            {
                PressButton(startNewButton);
                yield return null;
                if (SceneManager.GetActiveScene().name == DungeonSceneNavigator.TitleSceneName
                    && startNewButton.gameObject.activeInHierarchy)
                {
                    PressButton(startNewButton);
                }
            }
        }

        float sceneDeadline = Time.realtimeSinceStartup + 10f;
        while (SceneManager.GetActiveScene().name != DungeonSceneNavigator.GameplaySceneName
            && Time.realtimeSinceStartup < sceneDeadline)
        {
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.5f);

        Button ownerButton = Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && button.interactable
                && button.name.StartsWith("OwnerOption_", StringComparison.Ordinal));
        if (ownerButton != null)
        {
            PressButton(ownerButton);
            yield return new WaitForSecondsRealtime(0.25f);
        }

        bool ownerSelectionVisible = Resources.FindObjectsOfTypeAll<OwnerSelectionPanel>()
            .Any(panel => panel != null
                && panel.gameObject.scene.IsValid()
                && panel.gameObject.activeInHierarchy);
        GameObject saveModal = FindSceneObject("SaveModal");
        Check(SceneManager.GetActiveScene().name == DungeonSceneNavigator.GameplaySceneName
                && (saveModal == null || !saveModal.activeInHierarchy)
                && !ownerSelectionVisible,
            "GAME_READY",
            "Gameplay loaded and owner selection is closed before HUD checks");
    }

    private void VerifyGameplayHud(Vector2Int resolution)
    {
        RectTransform upperRight = FindSceneObject("UpperRightPanel")?.GetComponent<RectTransform>();
        UITabManager tabManager = UnityEngine.Object.FindFirstObjectByType<UITabManager>();
        RectTransform bottomTabs = tabManager != null ? tabManager.GetComponent<RectTransform>() : null;
        Check(IsInsideScreen(upperRight),
            $"HUD_UPPER_RIGHT_{Key(resolution)}",
            DescribeRect(upperRight));
        Check(IsInsideScreen(bottomTabs),
            $"HUD_BOTTOM_TABS_{Key(resolution)}",
            DescribeRect(bottomTabs));
        VerifyNamedButtonsInside(
            resolution,
            "HUD_BUTTONS",
            "RoomInspectionToggle",
            "SettingsMenuButton",
            "SaveMenuButton");
        if (upperRight != null)
        {
            VerifyTextOverflow(upperRight, resolution, "HUD_TEXT");
        }
    }

    private IEnumerator Capture(string path, Vector2Int resolution, string label)
    {
        yield return new WaitForEndOfFrame();
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        Color32[] pixels = capture.GetPixels32();
        File.WriteAllBytes(path, capture.EncodeToPNG());
        int visiblePixels = pixels.Count(pixel => pixel.a > 0 && (pixel.r > 5 || pixel.g > 5 || pixel.b > 5));
        Check(capture.width == resolution.x && capture.height == resolution.y && visiblePixels > pixels.Length / 20,
            $"{label}_{Key(resolution)}",
            $"size={capture.width}x{capture.height}; visible={visiblePixels}");
        Destroy(capture);
    }

    private void VerifyNamedButtonsInside(Vector2Int resolution, string prefix, params string[] names)
    {
        foreach (string name in names)
        {
            Button button = FindSceneButton(name);
            RectTransform rect = button != null ? button.GetComponent<RectTransform>() : null;
            Check(button != null && button.gameObject.activeInHierarchy && IsInsideScreen(rect),
                $"{prefix}_{name}_{Key(resolution)}",
                DescribeRect(rect));
        }
    }

    private void VerifyTextOverflow(RectTransform root, Vector2Int resolution, string prefix)
    {
        TMP_Text[] texts = root != null
            ? root.GetComponentsInChildren<TMP_Text>(false)
            : Array.Empty<TMP_Text>();
        string[] overflowing = texts
            .Where(text => text != null && text.gameObject.activeInHierarchy && text.isTextOverflowing)
            .Select(text => text.name + ":" + text.text)
            .ToArray();
        Check(overflowing.Length == 0,
            $"{prefix}_{Key(resolution)}",
            overflowing.Length == 0 ? $"texts={texts.Length}" : string.Join(" | ", overflowing));
    }

    private static bool IsInsideScreen(RectTransform rect)
    {
        if (rect == null)
        {
            return false;
        }

        Rect screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
        Rect target = GetScreenRect(rect);
        const float tolerance = 1f;
        return target.xMin >= screenRect.xMin - tolerance
            && target.yMin >= screenRect.yMin - tolerance
            && target.xMax <= screenRect.xMax + tolerance
            && target.yMax <= screenRect.yMax + tolerance;
    }

    private static Rect GetScreenRect(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(
            corners.Min(corner => corner.x),
            corners.Min(corner => corner.y),
            corners.Max(corner => corner.x),
            corners.Max(corner => corner.y));
    }

    private static string DescribeRect(RectTransform rect)
    {
        return rect != null ? GetScreenRect(rect).ToString() : "missing";
    }

    private static Button FindSceneButton(string name)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(button => button != null
                && button.gameObject.scene.IsValid()
                && button.name == name);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<Transform>()
            .Where(transform => transform != null && transform.gameObject.scene.IsValid())
            .Select(transform => transform.gameObject)
            .FirstOrDefault(gameObject => gameObject.name == name);
    }

    private static void PressButton(Button button)
    {
        button.OnPointerClick(new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left
        });
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
        report.Add($"RESULT={(passed ? "PASS" : "FAIL")}; failures={failures.Count}; {string.Join(" || ", failures)}");
        File.WriteAllText(DungeonResolutionPlayModeVerifier.ReportPath, string.Join("\n", report));
        if (passed)
        {
            Debug.Log("Resolution matrix verification passed. " + DungeonResolutionPlayModeVerifier.ReportPath);
        }
        else
        {
            Debug.LogError("Resolution matrix verification failed. " + DungeonResolutionPlayModeVerifier.ReportPath);
        }
    }

    private void RestoreGameViewSize()
    {
        if (sizeRestored || originalGameViewSizeIndex < 0)
        {
            return;
        }

        sizeRestored = true;
        GameViewResolutionController.SelectedSizeIndex = originalGameViewSizeIndex;
    }

    private void OnDestroy()
    {
        RestoreGameViewSize();
        Application.logMessageReceived -= CaptureLog;
    }

    private static string Key(Vector2Int resolution)
    {
        return resolution.x + "x" + resolution.y;
    }
}

internal static class GameViewResolutionController
{
    private const string LabelPrefix = "DungeonStory QA ";
    private static readonly Type GameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
    private static readonly Type GameViewSizesType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");
    private static readonly Type GameViewSizeGroupType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizeGroup");
    private static readonly Type GameViewSizeType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSize");
    private static readonly Type GameViewSizeKindType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizeType");

    public static int SelectedSizeIndex
    {
        get => (int)GameViewType.GetProperty("selectedSizeIndex")?.GetValue(GetGameView());
        set
        {
            GameViewType.GetProperty("selectedSizeIndex")?.SetValue(GetGameView(), value);
            GetGameView().Repaint();
        }
    }

    public static void Select(int width, int height)
    {
        object group = GetCurrentGroup();
        string label = LabelPrefix + width + "x" + height;
        string[] labels = (string[])GameViewSizeGroupType.GetMethod("GetDisplayTexts")?.Invoke(group, null);
        int index = Array.FindIndex(labels ?? Array.Empty<string>(), text => text.Contains(label));
        if (index < 0)
        {
            index = (int)GameViewSizeGroupType.GetMethod("GetTotalCount")?.Invoke(group, null);
            object fixedResolution = Enum.Parse(GameViewSizeKindType, "FixedResolution");
            object size = Activator.CreateInstance(GameViewSizeType, fixedResolution, width, height, label);
            GameViewSizeGroupType.GetMethod("AddCustomSize")?.Invoke(group, new[] { size });
        }

        SelectedSizeIndex = index;
    }

    private static EditorWindow GetGameView()
    {
        return EditorWindow.GetWindow(GameViewType);
    }

    private static object GetCurrentGroup()
    {
        Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(GameViewSizesType);
        object sizes = singletonType.GetProperty("instance")?.GetValue(null);
        return GameViewSizesType.GetProperty("currentGroup")?.GetValue(sizes);
    }
}
