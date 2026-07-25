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

[InitializeOnLoad]
public static class FirstRunObjectivePlayModeVerifier
{
    public const string RequestPath = "Temp/first-run-objective.request";
    public const string ReportPath = "Temp/first-run-objective-report.txt";
    public const string ScreenshotPath = "Temp/first-run-objective.png";

    private static bool runnerCreated;

    static FirstRunObjectivePlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/QA/Request First Run Objective Verification")]
    public static void RequestRunFromMenu()
    {
        Directory.CreateDirectory("Temp");
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

        if (change == PlayModeStateChange.EnteredPlayMode
            && !runnerCreated
            && File.Exists(RequestPath))
        {
            runnerCreated = true;
            GameObject runnerObject = new GameObject("First Run Objective Verification Runner");
            UnityEngine.Object.DontDestroyOnLoad(runnerObject);
            runnerObject.AddComponent<FirstRunObjectiveVerificationRunner>();
        }
    }
}

public sealed class FirstRunObjectiveVerificationRunner : MonoBehaviour
{
    private sealed class FileBackup
    {
        public string Path;
        public byte[] Bytes;
    }

    private readonly List<string> report = new List<string>();
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<FileBackup> backups = new List<FileBackup>();

    private InputSettings.EditorInputBehaviorInPlayMode originalInputBehavior;
    private Mouse originalMouse;
    private Mouse verificationMouse;

    private IEnumerator Start()
    {
        yield return Run();
    }

    private IEnumerator Run()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(FirstRunObjectivePlayModeVerifier.ReportPath, "FIRST_RUN_OBJECTIVE IN_PROGRESS\n");
        Application.logMessageReceived += CaptureLog;
        ConfigureInput();

        try
        {
            yield return new WaitForSecondsRealtime(2f);
            DungeonRuntimeLifetimeScope scope = FindScope();
            Check(scope != null, "DI_SCOPE", "active game container resolved");
            if (scope == null)
            {
                yield break;
            }

            BackupPersistentFiles(scope);
            IFirstRunObjectiveRuntime objective = scope.Container.Resolve<IFirstRunObjectiveRuntime>();
            Check(objective != null, "OBJECTIVE_RUNTIME", "runtime resolved");
            objective?.RefreshNow();
            Check(
                objective != null && objective.CurrentObjective == FirstRunObjectiveId.ChooseOwner,
                "INITIAL_OBJECTIVE",
                objective != null ? objective.CurrentObjective.ToString() : "missing");
            CheckNonBlocking(objective);

            yield return StartFreshRun();
            objective.RefreshNow();
            Check(
                objective.CurrentObjective == FirstRunObjectiveId.AcquireBlueprint,
                "POST_OWNER_OBJECTIVE",
                objective.CurrentObjective.ToString());
            CheckPanelBounds(objective);

            IBlueprintResearchRuntimeProvider researchProvider =
                scope.Container.Resolve<IBlueprintResearchRuntimeProvider>();
            IDailyFacilityShopRuntimeProvider shopProvider =
                scope.Container.Resolve<IDailyFacilityShopRuntimeProvider>();
            researchProvider.TryGetRuntime(out BlueprintResearchRuntime research);
            int taskCountBefore = research?.State.Tasks.Count ?? -1;

            Button shopTab = FindTopTabButton(TabId.Shop);
            yield return Click(shopTab, "shop tab");
            yield return new WaitForSecondsRealtime(0.25f);

            int blueprintOfferIndex = FindBlueprintOfferIndex(shopProvider);
            Button blueprintButton = FindButton($"P0Action_ShopDaily_{blueprintOfferIndex}");
            yield return Click(blueprintButton, "daily blueprint");
            yield return new WaitForSecondsRealtime(0.25f);

            int taskCountAfter = research?.State.Tasks.Count ?? -1;
            objective.RefreshNow();
            Check(
                blueprintOfferIndex >= 0 && taskCountAfter > taskCountBefore,
                "PUBLIC_BLUEPRINT_PURCHASE",
                $"offer={blueprintOfferIndex}; tasks={taskCountBefore}->{taskCountAfter}");
            Check(
                objective.CurrentObjective == FirstRunObjectiveId.CompleteResearch,
                "POST_PURCHASE_OBJECTIVE",
                objective.CurrentObjective.ToString());

            yield return Click(shopTab, "shop tab close");
            yield return new WaitForSecondsRealtime(0.2f);
            CheckNonBlocking(objective);
            CheckPanelBounds(objective);
            yield return CaptureScreen();
        }
        finally
        {
            Application.logMessageReceived -= CaptureLog;
            TeardownInput();
            RestoreFiles();

            report.Add($"capturedErrors={errors.Count}; capturedWarnings={warnings.Count}");
            foreach (string error in errors) report.Add("[CONSOLE ERROR] " + error.Replace('\n', ' '));
            foreach (string warning in warnings) report.Add("[CONSOLE WARNING] " + warning.Replace('\n', ' '));
            bool passed = report.All(line => !line.StartsWith("[FAIL]", StringComparison.Ordinal))
                && errors.Count == 0
                && warnings.Count == 0;
            report.Insert(0, passed ? "FIRST_RUN_OBJECTIVE PASS" : "FIRST_RUN_OBJECTIVE FAIL");
            File.WriteAllLines(FirstRunObjectivePlayModeVerifier.ReportPath, report);
            File.Delete(FirstRunObjectivePlayModeVerifier.RequestPath);
            EditorApplication.ExitPlaymode();
        }
    }

    private IEnumerator StartFreshRun()
    {
        Button startNew = FindButton("StartNewRunButton");
        if (startNew != null && startNew.gameObject.activeInHierarchy)
        {
            yield return Click(startNew, "new game");
            if (startNew.gameObject.activeInHierarchy)
            {
                yield return Click(startNew, "confirm new game");
            }
        }

        OwnerRunManager ownerManager = FindFirstObjectByType<OwnerRunManager>();
        if (ownerManager != null && ownerManager.CurrentOwnerActor == null)
        {
            Button ownerButton = Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(button => button != null
                    && button.gameObject.scene.IsValid()
                    && button.gameObject.activeInHierarchy
                    && button.name.StartsWith("OwnerOption_", StringComparison.Ordinal));
            yield return Click(ownerButton, "owner option");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible();
        }

        Check(
            ownerManager != null && ownerManager.CurrentOwnerActor != null,
            "PUBLIC_NEW_RUN",
            "new game and owner selected with pointer input");
        yield return new WaitForSecondsRealtime(0.25f);
    }

    private IEnumerator Click(Button button, string label)
    {
        bool available = button != null && button.gameObject.activeInHierarchy && button.interactable;
        Check(available, "POINTER_TARGET", available ? label : label + " missing");
        if (!available)
        {
            yield break;
        }

        yield return ScrollIntoView(button);
        RectTransform rect = button.GetComponent<RectTransform>();
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        verificationMouse.MakeCurrent();
        InputSystem.QueueStateEvent(
            verificationMouse,
            new MouseState { position = point }.WithButton(MouseButton.Left, true));
        yield return null;
        yield return null;
        verificationMouse.MakeCurrent();
        InputSystem.QueueStateEvent(verificationMouse, new MouseState { position = point });
        yield return null;
        yield return null;
    }

    private IEnumerator ScrollIntoView(Button button)
    {
        ScrollRect scroll = button != null ? button.GetComponentInParent<ScrollRect>() : null;
        RectTransform viewport = scroll != null ? scroll.viewport : null;
        if (scroll == null || viewport == null || !scroll.vertical)
        {
            yield break;
        }

        for (int attempt = 0; attempt < 10; attempt++)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            Vector2 buttonPoint = RectTransformUtility.WorldToScreenPoint(
                null,
                buttonRect.TransformPoint(buttonRect.rect.center));
            if (RectTransformUtility.RectangleContainsScreenPoint(viewport, buttonPoint, null))
            {
                yield break;
            }

            Vector2 viewportPoint = RectTransformUtility.WorldToScreenPoint(
                null,
                viewport.TransformPoint(viewport.rect.center));
            float scrollDelta = buttonPoint.y < viewportPoint.y ? -120f : 120f;
            verificationMouse.MakeCurrent();
            InputSystem.QueueStateEvent(
                verificationMouse,
                new MouseState { position = viewportPoint, scroll = new Vector2(0f, scrollDelta) });
            yield return null;
            InputSystem.QueueStateEvent(verificationMouse, new MouseState { position = viewportPoint });
            yield return null;
        }
    }

    private void CheckNonBlocking(IFirstRunObjectiveRuntime objective)
    {
        RectTransform panel = objective?.PanelRect;
        CanvasGroup group = panel != null ? panel.GetComponent<CanvasGroup>() : null;
        bool graphicsIgnoreRaycasts = panel != null
            && panel.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget);
        Check(
            group != null && !group.blocksRaycasts && !group.interactable && graphicsIgnoreRaycasts,
            "NON_BLOCKING",
            $"group={group != null}; blocks={group?.blocksRaycasts}; graphics={graphicsIgnoreRaycasts}");

        if (panel == null || EventSystem.current == null)
        {
            Check(false, "RAYCAST_PASS_THROUGH", "panel or EventSystem missing");
            return;
        }

        Vector2 center = RectTransformUtility.WorldToScreenPoint(null, panel.TransformPoint(panel.rect.center));
        PointerEventData pointer = new PointerEventData(EventSystem.current) { position = center };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);
        bool intercepted = results.Any(result => result.gameObject != null
            && result.gameObject.transform.IsChildOf(panel));
        Check(!intercepted, "RAYCAST_PASS_THROUGH", $"panelHits={results.Count(result => result.gameObject != null && result.gameObject.transform.IsChildOf(panel))}");
    }

    private void CheckPanelBounds(IFirstRunObjectiveRuntime objective)
    {
        RectTransform panel = objective?.PanelRect;
        if (panel == null)
        {
            Check(false, "PANEL_BOUNDS", "panel missing");
            return;
        }

        Vector3[] corners = new Vector3[4];
        panel.GetWorldCorners(corners);
        bool inside = corners.All(corner => corner.x >= 0f
            && corner.y >= 0f
            && corner.x <= Screen.width
            && corner.y <= Screen.height);
        Check(inside, "PANEL_BOUNDS", $"screen={Screen.width}x{Screen.height}; rect={panel.rect.size}");
    }

    private IEnumerator CaptureScreen()
    {
        yield return new WaitForEndOfFrame();
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        Color32[] pixels = capture != null ? capture.GetPixels32() : Array.Empty<Color32>();
        bool nonblank = pixels.Any(pixel => pixel.a > 0 && (pixel.r > 8 || pixel.g > 8 || pixel.b > 8));
        Check(nonblank, "SCREEN_CAPTURE", $"nonblank={nonblank}; pixels={pixels.Length}");
        if (capture != null)
        {
            File.WriteAllBytes(FirstRunObjectivePlayModeVerifier.ScreenshotPath, capture.EncodeToPNG());
            Destroy(capture);
        }
    }

    private static int FindBlueprintOfferIndex(IDailyFacilityShopRuntimeProvider provider)
    {
        if (!provider.TryGetRuntime(out DailyFacilityShopRuntime shop) || shop == null)
        {
            return -1;
        }

        for (int index = 0; index < shop.CurrentDailyOffers.Count; index++)
        {
            FacilityShopOffer offer = shop.CurrentDailyOffers[index];
            if (offer != null
                && string.Equals(offer.OfferTypeId, FacilityShopOfferTypeIds.Blueprint, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static Button FindTopTabButton(TabId tabId)
    {
        return Resources.FindObjectsOfTypeAll<UITabButtonBinding>()
            .Where(binding => binding != null
                && binding.Id == tabId
                && binding.gameObject.scene.IsValid()
                && binding.gameObject.activeInHierarchy)
            .Select(binding => binding.GetComponent<Button>())
            .FirstOrDefault(button => button != null);
    }

    private static Button FindButton(string name)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(button => button != null
                && button.gameObject.scene.IsValid()
                && button.name == name);
    }

    private static DungeonRuntimeLifetimeScope FindScope()
    {
        return FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(scope => scope != null && scope.Container != null);
    }

    private void ConfigureInput()
    {
        originalInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        originalMouse = Mouse.current;
        if (originalMouse != null)
        {
            InputSystem.DisableDevice(originalMouse);
        }

        verificationMouse = InputSystem.AddDevice<Mouse>("FirstRunObjectiveVerificationMouse");
        verificationMouse.MakeCurrent();
    }

    private void TeardownInput()
    {
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
    }

    private void BackupPersistentFiles(DungeonRuntimeLifetimeScope scope)
    {
        IMetaProfileStore profile = scope.Container.Resolve<IMetaProfileStore>();
        BackupFile(profile.ProfilePath);
        IDungeonGameSaveSlotService slots = scope.Container.Resolve<IDungeonGameSaveSlotService>();
        foreach (DungeonSaveSlotInfo slot in slots.GetSlots())
        {
            BackupFile(slot.Path);
        }
    }

    private void BackupFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || backups.Any(backup => string.Equals(backup.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        backups.Add(new FileBackup
        {
            Path = path,
            Bytes = File.Exists(path) ? File.ReadAllBytes(path) : null
        });
    }

    private void RestoreFiles()
    {
        foreach (FileBackup backup in backups)
        {
            if (backup.Bytes == null)
            {
                if (File.Exists(backup.Path)) File.Delete(backup.Path);
                continue;
            }

            string directory = Path.GetDirectoryName(backup.Path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllBytes(backup.Path, backup.Bytes);
        }
    }

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors.Add(condition);
        }
        else if (type == LogType.Warning)
        {
            warnings.Add(condition);
        }
    }

    private void Check(bool condition, string id, string detail)
    {
        report.Add($"[{(condition ? "PASS" : "FAIL")}] {id} {detail}");
        File.WriteAllLines(
            FirstRunObjectivePlayModeVerifier.ReportPath,
            new[] { "FIRST_RUN_OBJECTIVE IN_PROGRESS" }.Concat(report));
    }
}
