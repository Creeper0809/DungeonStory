#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

[InitializeOnLoad]
public static class CharacterSummaryMedicalUiMatrixPlayModeVerifier
{
    public const string RequestPath =
        "Temp/character-summary-medical-ui-matrix.request";
    public const string ReportPath =
        "Artifacts/QA/CharacterSummaryMedical/ui-matrix-report.txt";
    public const string CaptureDirectory =
        "Artifacts/QA/CharacterSummaryMedical";

    private const string GameplayScenePath =
        "Assets/Scenes/GameplayScene.unity";
    private const string PersistenceSnapshotId =
        "character-summary-medical-ui-matrix";

    private static bool runnerCreated;

    static CharacterSummaryMedicalUiMatrixPlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/QA/Request Character Summary Medical UI Matrix")]
    public static void RequestRunFromMenu()
    {
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory(CaptureDirectory);
        File.Delete(ReportPath);
        foreach (Vector2Int resolution in
                 CharacterSummaryMedicalUiMatrixRunner.Resolutions)
        {
            File.Delete(GetCapturePath(resolution, "summary-health"));
            File.Delete(GetCapturePath(resolution, "surgery-modal"));
        }

        if (!DungeonFinalPlayModeAcceptanceRequestFacade
                .IsPersistenceCoordinatorActive)
        {
            PlayModeVerificationPersistenceSnapshot.CaptureCurrent(
                PersistenceSnapshotId);
        }
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
    }

    public static string GetCapturePath(
        Vector2Int resolution,
        string surface)
    {
        return Path.Combine(
            CaptureDirectory,
            $"{resolution.x}x{resolution.y}-{surface}.png");
    }

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
        new GameObject("Character Summary Medical UI Matrix Runner")
            .AddComponent<CharacterSummaryMedicalUiMatrixRunner>();
    }
}

public sealed class CharacterSummaryMedicalUiMatrixRunner : MonoBehaviour
{
    public static readonly Vector2Int[] Resolutions =
    {
        new(1600, 900),
        new(900, 1600)
    };

    private readonly List<string> report = new();
    private readonly List<string> failures = new();
    private readonly List<string> capturedErrors = new();
    private readonly List<string> capturedWarnings = new();

    private int originalGameViewSizeIndex = -1;
    private bool resolutionRestored;
    private CharacterSummaryInfo summary;

    private IEnumerator Start()
    {
        Directory.CreateDirectory(
            CharacterSummaryMedicalUiMatrixPlayModeVerifier.CaptureDirectory);
        Application.logMessageReceived += OnLogMessageReceived;
        originalGameViewSizeIndex = GameViewResolutionController.SelectedSizeIndex;
        EnsureEventSystem();
        yield return null;
        yield return null;
        yield return EnsurePlayableRun();

        DungeonRuntimeLifetimeScope scope = FindScope();
        IGameEventBus eventBus = Resolve<IGameEventBus>(scope);
        ICharacterSurgeryWindowService surgeryWindow =
            Resolve<ICharacterSurgeryWindowService>(scope);
        summary = UnityEngine.Object.FindFirstObjectByType<CharacterSummaryInfo>(
            FindObjectsInactive.Include);
        CharacterActor actor = FindVerificationActor();

        Check(scope?.Container != null,
            "SCOPE_READY",
            "gameplay LifetimeScope resolved");
        Check(eventBus != null,
            "EVENT_BUS_READY",
            "character info event bus resolved");
        Check(surgeryWindow != null,
            "MEDICAL_UI_SERVICE_READY",
            "character surgery window service resolved");
        Check(summary != null,
            "CHARACTER_SUMMARY_READY",
            "CharacterSummaryInfo resolved");
        Check(actor != null,
            "CHARACTER_ACTOR_READY",
            actor != null ? actor.name : "active actor missing");

        if (failures.Count == 0)
        {
            foreach (Vector2Int resolution in Resolutions)
            {
                yield return SelectResolution(resolution);
                yield return VerifyPointerFlow(
                    resolution,
                    actor,
                    eventBus,
                    surgeryWindow);
            }
        }

        Finish();
    }

    private IEnumerator EnsurePlayableRun()
    {
        OwnerRunManager ownerManager =
            UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        if (ownerManager == null || ownerManager.CurrentOwnerActor == null)
        {
            string result =
                StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            report.Add("[INFO] FAST_PARTY_COMMIT " + Compact(result));
            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
            }
        }

        ownerManager =
            UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        Check(ownerManager != null && ownerManager.CurrentOwnerActor != null,
            "PLAYABLE_RUN_READY",
            ownerManager?.CurrentOwnerActor != null
                ? "owner=" + ownerManager.CurrentOwnerActor.name
                : "fast party commit did not establish an owner");
    }

    private IEnumerator SelectResolution(Vector2Int resolution)
    {
        GameViewResolutionController.Select(resolution.x, resolution.y);
        float deadline = Time.realtimeSinceStartup + 4f;
        while ((Screen.width != resolution.x
                || Screen.height != resolution.y)
               && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Canvas.ForceUpdateCanvases();
        yield return PlayModeVerificationFrameWait.CaptureReady();
        Check(Screen.width == resolution.x
            && Screen.height == resolution.y,
            "RESOLUTION_" + Key(resolution),
            $"actual={Screen.width}x{Screen.height}");
    }

    private IEnumerator VerifyPointerFlow(
        Vector2Int resolution,
        CharacterActor actor,
        IGameEventBus eventBus,
        ICharacterSurgeryWindowService surgeryWindow)
    {
        string suffix = Key(resolution);
        CloseSurgeryWindowWithoutInteraction();
        if (summary.UI != null && summary.UI.activeSelf)
        {
            summary.OnClose();
        }

        eventBus.ShowInfo(actor);
        yield return null;
        yield return PlayModeVerificationFrameWait.CaptureReady();

        Transform generated = summary.UI != null
            ? summary.UI.transform.Find("CharacterSummaryGeneratedView")
            : null;
        GameObject statusContent = generated != null
            ? generated.Find("Content/StatusContent")?.gameObject
            : null;
        GameObject healthContent = generated != null
            ? generated.Find("Content/HealthContent")?.gameObject
            : null;
        Button healthTab = FindButton(generated, "TabBar/HealthTab");
        Button surgeryCommand = FindButton(
            generated,
            "Content/HealthContent/HealthCommandRow/SurgeryCommand");
        Button automaticSurgery = FindButton(
            generated,
            "Content/HealthContent/HealthCommandRow/AutomaticSurgery");
        Button summaryClose = FindButton(generated, "Header/CloseButton");

        Check(summary.UI != null && summary.UI.activeInHierarchy,
            "SUMMARY_OPEN_" + suffix,
            "info event opened CharacterSummaryInfo");
        Check(generated != null && generated.gameObject.activeInHierarchy,
            "SUMMARY_GENERATED_" + suffix,
            "generated summary view is active");
        Check(IsInsideScreen(summary.UI?.transform as RectTransform),
            "SUMMARY_BOUNDS_" + suffix,
            DescribeRect(summary.UI?.transform as RectTransform));
        Check(healthTab != null
            && surgeryCommand != null
            && automaticSurgery != null
            && summaryClose != null,
            "SUMMARY_MEDICAL_POINTER_TARGETS_" + suffix,
            $"health={healthTab != null}; surgery={surgeryCommand != null}; "
            + $"automatic={automaticSurgery != null}; close={summaryClose != null}");

        bool healthClicked = ClickThroughEventSystem(
            healthTab,
            "HEALTH_TAB_POINTER_" + suffix);
        yield return null;
        Canvas.ForceUpdateCanvases();
        Check(healthClicked
            && healthContent != null
            && healthContent.activeInHierarchy
            && statusContent != null
            && !statusContent.activeSelf,
            "HEALTH_TAB_SELECTED_" + suffix,
            $"health={healthContent?.activeInHierarchy}; status={statusContent?.activeSelf}");
        Check(AreInsideScreen(surgeryCommand, automaticSurgery),
            "HEALTH_COMMAND_BOUNDS_" + suffix,
            $"surgery={DescribeRect(surgeryCommand?.transform as RectTransform)}; "
            + $"automatic={DescribeRect(automaticSurgery?.transform as RectTransform)}");
        Check(ButtonLabelsFit(healthTab, surgeryCommand, automaticSurgery),
            "HEALTH_COMMAND_LABELS_FIT_" + suffix,
            "health and surgery command labels do not overflow");

        yield return null;
        Canvas.ForceUpdateCanvases();

        bool automaticBefore =
            surgeryWindow.IsAutomaticEmergencyEnabled(actor);
        bool automaticClicked = ClickThroughEventSystem(
            automaticSurgery,
            "AUTOMATIC_SURGERY_ENABLE_POINTER_" + suffix);
        yield return null;
        bool automaticAfter =
            surgeryWindow.IsAutomaticEmergencyEnabled(actor);
        Check(automaticClicked && automaticAfter != automaticBefore,
            "AUTOMATIC_SURGERY_TOGGLED_" + suffix,
            $"{automaticBefore}->{automaticAfter}");

        bool automaticRestoreClicked = ClickThroughEventSystem(
            automaticSurgery,
            "AUTOMATIC_SURGERY_RESTORE_POINTER_" + suffix);
        yield return null;
        bool automaticRestored =
            surgeryWindow.IsAutomaticEmergencyEnabled(actor);
        Check(automaticRestoreClicked && automaticRestored == automaticBefore,
            "AUTOMATIC_SURGERY_RESTORED_" + suffix,
            $"expected={automaticBefore}; actual={automaticRestored}");

        yield return Capture(
            CharacterSummaryMedicalUiMatrixPlayModeVerifier.GetCapturePath(
                resolution,
                "summary-health"),
            resolution,
            "SUMMARY_HEALTH_CAPTURE_" + suffix);

        bool surgeryClicked = ClickThroughEventSystem(
            surgeryCommand,
            "SURGERY_OPEN_POINTER_" + suffix);
        yield return null;
        yield return PlayModeVerificationFrameWait.CaptureReady();

        GameObject surgeryRoot = FindActiveSceneObject("CharacterSurgeryWindow");
        RectTransform surgeryPanel = surgeryRoot != null
            ? surgeryRoot.transform.Find("SurgeryPanel") as RectTransform
            : null;
        Button schedule = FindDescendantButton(surgeryRoot, "Schedule");
        Button cancel = FindDescendantButton(surgeryRoot, "CancelOrder");
        Button close = FindDescendantButton(surgeryRoot, "Close");

        Check(surgeryClicked
            && surgeryRoot != null
            && surgeryRoot.activeInHierarchy,
            "SURGERY_MODAL_OPEN_" + suffix,
            surgeryRoot != null ? surgeryRoot.name : "modal missing");
        Check(IsInsideScreen(surgeryPanel),
            "SURGERY_PANEL_BOUNDS_" + suffix,
            DescribeRect(surgeryPanel));
        Check(schedule != null && cancel != null && close != null,
            "SURGERY_FOOTER_TARGETS_" + suffix,
            $"schedule={schedule != null}; cancel={cancel != null}; close={close != null}");
        Check(AreInsideScreen(schedule, cancel, close),
            "SURGERY_FOOTER_BOUNDS_" + suffix,
            $"schedule={DescribeRect(schedule?.transform as RectTransform)}; "
            + $"cancel={DescribeRect(cancel?.transform as RectTransform)}; "
            + $"close={DescribeRect(close?.transform as RectTransform)}");
        Check(ButtonLabelsFit(schedule, cancel, close),
            "SURGERY_FOOTER_LABELS_FIT_" + suffix,
            "surgery footer labels do not overflow");

        yield return Capture(
            CharacterSummaryMedicalUiMatrixPlayModeVerifier.GetCapturePath(
                resolution,
                "surgery-modal"),
            resolution,
            "SURGERY_MODAL_CAPTURE_" + suffix);

        bool closeClicked = ClickThroughEventSystem(
            close,
            "SURGERY_CLOSE_POINTER_" + suffix);
        yield return null;
        yield return null;
        Check(closeClicked
            && FindActiveSceneObject("CharacterSurgeryWindow") == null,
            "SURGERY_MODAL_CLOSED_" + suffix,
            "close pointer removed the surgery modal");

        bool summaryCloseClicked = ClickThroughEventSystem(
            summaryClose,
            "SUMMARY_CLOSE_POINTER_" + suffix);
        yield return null;
        Check(summaryCloseClicked
            && summary.UI != null
            && !summary.UI.activeSelf,
            "SUMMARY_CLOSED_" + suffix,
            "close pointer hid CharacterSummaryInfo");
    }

    private bool ClickThroughEventSystem(Button button, string key)
    {
        if (button == null
            || !button.gameObject.activeInHierarchy
            || !button.interactable
            || EventSystem.current == null)
        {
            return Check(false,
                key,
                $"target unavailable; button={button != null}; "
                + $"active={button?.gameObject.activeInHierarchy}; "
                + $"interactable={button?.interactable}; "
                + $"eventSystem={EventSystem.current != null}");
        }

        Canvas.ForceUpdateCanvases();
        RectTransform rect = button.transform as RectTransform;
        Canvas canvas = button.GetComponentInParent<Canvas>();
        Camera camera = canvas != null
            && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera ?? Camera.main
                : null;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(
            camera,
            rect.TransformPoint(rect.rect.center));
        PointerEventData pointer = new(EventSystem.current)
        {
            position = point,
            button = PointerEventData.InputButton.Left
        };
        List<RaycastResult> hits = new();
        EventSystem.current.RaycastAll(pointer, hits);
        RaycastResult hit = hits.FirstOrDefault(result =>
            ExecuteEvents.GetEventHandler<IPointerClickHandler>(
                result.gameObject) == button.gameObject);
        GameObject topHandler = hits
            .Select(result => ExecuteEvents.GetEventHandler<IPointerClickHandler>(
                result.gameObject))
            .FirstOrDefault(handler => handler != null);
        bool targetIsTopHandler = hit.gameObject != null
            && topHandler == button.gameObject
            && RectTransformUtility.RectangleContainsScreenPoint(
                rect,
                point,
                camera);
        if (!Check(targetIsTopHandler,
                key + "_HIT_TEST",
                $"point={point}; hits={hits.Count}; "
                + $"top={topHandler?.name ?? "none"}; expected={button.name}"))
        {
            return false;
        }

        bool dispatched = PlayModeVerificationFrameWait.DispatchPointerClick(
            hit.gameObject,
            point);
        return Check(dispatched,
            key + "_DISPATCH",
            dispatched ? "Unity pointer event dispatched" : "dispatch failed");
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

    private static Button FindButton(Transform root, string path)
    {
        return root?.Find(path)?.GetComponent<Button>();
    }

    private static Button FindDescendantButton(GameObject root, string name)
    {
        return root != null
            ? root.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button != null
                    && string.Equals(button.name, name, StringComparison.Ordinal))
            : null;
    }

    private static GameObject FindActiveSceneObject(string name)
    {
        return UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where(candidate => candidate != null
                && candidate.gameObject.scene.IsValid())
            .Select(candidate => candidate.gameObject)
            .FirstOrDefault(candidate => string.Equals(
                candidate.name,
                name,
                StringComparison.Ordinal));
    }

    private static CharacterActor FindVerificationActor()
    {
        OwnerRunManager owner = UnityEngine.Object.FindFirstObjectByType<
            OwnerRunManager>();
        if (owner?.CurrentOwnerActor != null
            && !owner.CurrentOwnerActor.IsDead)
        {
            return owner.CurrentOwnerActor;
        }

        return UnityEngine.Object.FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(actor => actor != null
                && !actor.IsDead
                && actor.Stats != null);
    }

    private static bool AreInsideScreen(params Button[] buttons)
    {
        return buttons != null
            && buttons.Length > 0
            && buttons.All(button => button != null
                && button.gameObject.activeInHierarchy
                && IsInsideScreen(button.transform as RectTransform));
    }

    private static bool ButtonLabelsFit(params Button[] buttons)
    {
        if (buttons == null || buttons.Any(button => button == null))
        {
            return false;
        }

        foreach (TMP_Text label in buttons.SelectMany(button =>
                     button.GetComponentsInChildren<TMP_Text>(true)))
        {
            if (label == null || !label.gameObject.activeInHierarchy)
            {
                continue;
            }

            label.ForceMeshUpdate();
            if (label.isTextOverflowing)
            {
                return false;
            }
        }

        return true;
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

    private static string DescribeRect(RectTransform rect)
    {
        return rect != null ? GetScreenRect(rect).ToString() : "missing";
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            new GameObject(
                "QA_CharacterSummaryMedical_EventSystem",
                typeof(EventSystem));
        }
    }

    private static DungeonRuntimeLifetimeScope FindScope()
    {
        return UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate?.Container != null);
    }

    private static T Resolve<T>(DungeonRuntimeLifetimeScope scope)
        where T : class
    {
        try
        {
            return scope?.Container?.Resolve<T>();
        }
        catch
        {
            return null;
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

    private void OnLogMessageReceived(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type is LogType.Error or LogType.Exception or LogType.Assert)
        {
            capturedErrors.Add(condition + "\n" + stackTrace);
        }
        else if (type == LogType.Warning)
        {
            capturedWarnings.Add(condition);
        }
    }

    private void Finish()
    {
        CloseSurgeryWindowWithoutInteraction();
        if (summary?.UI != null && summary.UI.activeSelf)
        {
            summary.OnClose();
        }

        RestoreResolution();
        Application.logMessageReceived -= OnLogMessageReceived;
        report.Add($"capturedErrors={capturedErrors.Count}; "
            + Compact(capturedErrors));
        report.Add($"capturedWarnings={capturedWarnings.Count}; "
            + Compact(capturedWarnings));
        bool passed = failures.Count == 0
            && capturedErrors.Count == 0
            && capturedWarnings.Count == 0;
        report.Add($"RESULT={(passed ? "PASS" : "FAIL")}; "
            + $"failures={failures.Count}; {Compact(failures)}");
        File.WriteAllText(
            CharacterSummaryMedicalUiMatrixPlayModeVerifier.ReportPath,
            string.Join("\n", report));
        File.Delete(
            CharacterSummaryMedicalUiMatrixPlayModeVerifier.RequestPath);

        if (passed)
        {
            Debug.Log(
                "Character summary/medical UI matrix verification passed. "
                + CharacterSummaryMedicalUiMatrixPlayModeVerifier.ReportPath);
        }
        else
        {
            Debug.LogError(
                "Character summary/medical UI matrix verification failed. "
                + CharacterSummaryMedicalUiMatrixPlayModeVerifier.ReportPath);
        }

        EditorApplication.ExitPlaymode();
        Destroy(gameObject);
    }

    private static void CloseSurgeryWindowWithoutInteraction()
    {
        GameObject window = FindActiveSceneObject("CharacterSurgeryWindow");
        if (window != null)
        {
            UnityEngine.Object.Destroy(window);
        }
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
        Application.logMessageReceived -= OnLogMessageReceived;
    }

    private static string Key(Vector2Int resolution)
    {
        return resolution.x + "x" + resolution.y;
    }

    private static string Compact(IEnumerable<string> values)
    {
        return Compact(string.Join(" | ", values ?? Array.Empty<string>()));
    }

    private static string Compact(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "<none>"
            : value.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
#endif
