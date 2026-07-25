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

[InitializeOnLoad]
public static class DungeonSaveUiPlayModeVerifier
{
    public const string RequestPath = "Temp/save-ui-verification.request";
    public const string ReportPath = "Temp/save-ui-verification-report.txt";
    public const string ScreenshotPath = "Temp/save-ui-verification.png";

    private static bool runnerCreated;

    static DungeonSaveUiPlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/QA/Request Save UI Verification")]
    public static void RequestRunFromMenu()
    {
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        Directory.CreateDirectory("Temp");
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
    }

    private static void OnEditorUpdate()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
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

        if (change != PlayModeStateChange.EnteredPlayMode || runnerCreated || !File.Exists(RequestPath))
        {
            return;
        }

        DungeonSaveUiVerificationRunner existingRunner =
            UnityEngine.Object.FindFirstObjectByType<DungeonSaveUiVerificationRunner>();
        if (existingRunner != null)
        {
            Debug.Log("Save UI verification runner already exists at PlayMode entry.");
            runnerCreated = true;
            return;
        }

        runnerCreated = true;
        Debug.Log("Save UI verification runner created at PlayMode entry.");
        new GameObject("Save UI Verification Runner").AddComponent<DungeonSaveUiVerificationRunner>();
    }
}

public sealed class DungeonSaveUiVerificationRunner : MonoBehaviour
{
    private readonly List<string> report = new List<string>();
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private InputSettings.EditorInputBehaviorInPlayMode originalInputBehavior;
    private Mouse originalMouse;
    private Mouse verificationMouse;
    private readonly List<SlotBackup> originalRunSlots = new List<SlotBackup>();
    private IDungeonGameSaveSlotService slotService;

    private IEnumerator Start()
    {
        yield return Run();
    }

    private IEnumerator Run()
    {
        Directory.CreateDirectory("Temp");
        PlayModeVerificationPersistenceSnapshot.CaptureCurrent("save-ui");
        Application.logMessageReceived += CaptureLog;
        originalInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        originalMouse = Mouse.current;
        if (originalMouse != null)
        {
            InputSystem.DisableDevice(originalMouse);
        }

        verificationMouse = InputSystem.AddDevice<Mouse>("SaveUiVerificationMouse");
        verificationMouse.MakeCurrent();

        try
        {
            yield return new WaitForSecondsRealtime(2f);
            DungeonRuntimeLifetimeScope scope = FindObjectsByType<DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null && candidate.Container != null);
            Check(scope != null, "DI_SCOPE", "runtime LifetimeScope resolved");
            if (scope == null)
            {
                yield break;
            }

            slotService = scope.Container.Resolve<IDungeonGameSaveSlotService>();
            IGameDataProvider gameDataProvider = scope.Container.Resolve<IGameDataProvider>();
            BackupRunSlots();

            Button saveMenuButton = FindSceneComponent<Button>("SaveMenuButton");
            Check(saveMenuButton != null, "MENU_BUTTON", "upper-right save button exists");
            if (saveMenuButton == null || !gameDataProvider.TryGetGameData(out GameData gameData))
            {
                yield break;
            }

            GameObject modal = FindSceneObject("SaveModal");
            Check(FindSceneComponent<Button>("StartNewRunButton") == null,
                "TITLE_SEPARATED", "Gameplay does not create title actions");
            Check(modal != null && !modal.activeInHierarchy,
                "MENU_INITIAL_STATE", "in-game save modal starts closed");
            OwnerRunManager ownerManager = FindFirstObjectByType<OwnerRunManager>(FindObjectsInactive.Include);
            OwnerSelectionPanel ownerSelection = FindFirstObjectByType<OwnerSelectionPanel>(FindObjectsInactive.Include);
            if (ownerManager != null && ownerManager.CurrentOwnerActor == null)
            {
                Button ownerButton = Resources.FindObjectsOfTypeAll<Button>()
                    .FirstOrDefault(candidate => candidate != null
                        && candidate.gameObject.scene.IsValid()
                        && candidate.gameObject.activeInHierarchy
                        && candidate.name.StartsWith("OwnerOption_", StringComparison.Ordinal));
                yield return Click(ownerButton);
                yield return StartPartyPlayModeTestDriver.CompleteIfVisible();
            }

            Check(ownerManager != null && ownerManager.CurrentOwnerActor != null,
                "OWNER_SELECTED", "owner candidate creates the run owner through pointer input");
            Check(ownerSelection != null && !ownerSelection.gameObject.activeInHierarchy,
                "OWNER_PANEL_CLOSE", "owner selection closes after a valid choice");
            Check(Time.timeScale > 0f, "OWNER_RESUME", $"game clock starts after owner selection; timeScale={Time.timeScale:0.##}");
            slotService.Delete(DungeonGameSaveSlotService.ManualSaveSlot);

            yield return Click(saveMenuButton);
            Check(modal != null && modal.activeInHierarchy, "MENU_OPEN", "save modal opens through pointer click");
            Check(Mathf.Approximately(Time.timeScale, 0f), "PAUSE", $"modal pauses simulation; timeScale={Time.timeScale:0.##}");
            Check(IsPanelInsideScreen(), "BOUNDS", $"panel fits {Screen.width}x{Screen.height} game view");
            Check(IsUiRaycastBlocking(modal), "INPUT_BLOCK", "modal graphic blocks world pointer input");

            int savedMoney = gameData.holdingMoney.Value + 777;
            gameData.holdingMoney.Value = savedMoney;
            Button saveButton = FindSceneComponent<Button>("SaveButton_" + DungeonGameSaveSlotService.ManualSaveSlot);
            yield return Click(saveButton);
            yield return null;
            Check(slotService.HasSave(DungeonGameSaveSlotService.ManualSaveSlot), "SAVE_CREATED", "manual slot created through public UI");
            TMP_Text metadata = FindSceneComponent<TMP_Text>("Metadata", "SaveSlot_" + DungeonGameSaveSlotService.ManualSaveSlot);
            Check(metadata != null && metadata.text.Contains(savedMoney.ToString("N0")),
                "METADATA", $"slot metadata reflects saved money; text={metadata?.text}");

            yield return CaptureScreen();

            gameData.holdingMoney.Value = savedMoney + 555;
            Button loadButton = FindSceneComponent<Button>("LoadButton_" + DungeonGameSaveSlotService.ManualSaveSlot);
            yield return Click(loadButton);
            yield return null;
            yield return null;
            Check(gameData.holdingMoney.Value == savedMoney,
                "LOAD_RESTORE", $"money restored through UI; expected={savedMoney}; actual={gameData.holdingMoney.Value}");
            Check(modal != null && !modal.activeInHierarchy, "LOAD_CLOSE", "successful load closes modal");
            Check(Time.timeScale > 0f, "RESUME", $"simulation resumes after load; timeScale={Time.timeScale:0.##}");

            yield return Click(saveMenuButton);
            Button deleteButton = FindSceneComponent<Button>("DeleteButton_" + DungeonGameSaveSlotService.ManualSaveSlot);
            yield return Click(deleteButton);
            Check(slotService.HasSave(DungeonGameSaveSlotService.ManualSaveSlot),
                "DELETE_CONFIRM", "first delete click requests confirmation");
            yield return Click(deleteButton);
            Check(!slotService.HasSave(DungeonGameSaveSlotService.ManualSaveSlot),
                "DELETE_EXECUTE", "second delete click removes slot");

            Button closeButton = FindSceneComponent<Button>("CloseButton", "SavePanel");
            yield return Click(closeButton);
            Check(modal != null && !modal.activeInHierarchy, "CLOSE", "close button dismisses modal");
        }
        finally
        {
            RestoreRunSlots();
            TeardownInput();
            Application.logMessageReceived -= CaptureLog;
            report.Add($"capturedErrors={errors.Count}; capturedWarnings={warnings.Count}");
            foreach (string error in errors)
            {
                report.Add("[CONSOLE ERROR] " + error.Replace('\n', ' '));
            }

            foreach (string warning in warnings)
            {
                report.Add("[CONSOLE WARNING] " + warning.Replace('\n', ' '));
            }

            bool passed = report.All(line => !line.StartsWith("[FAIL]", StringComparison.Ordinal))
                && errors.Count == 0
                && warnings.Count == 0;
            report.Insert(0, passed ? "SAVE_UI PASS" : "SAVE_UI FAIL");
            File.WriteAllLines(DungeonSaveUiPlayModeVerifier.ReportPath, report);
            File.Delete(DungeonSaveUiPlayModeVerifier.RequestPath);
            EditorApplication.ExitPlaymode();
        }
    }

    private IEnumerator Click(Button button)
    {
        Check(button != null && button.gameObject.activeInHierarchy && button.interactable,
            "CLICK_TARGET", button != null ? button.name : "<missing>");
        if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
        {
            yield break;
        }

        Vector2 point = RectTransformUtility.WorldToScreenPoint(null,
            button.GetComponent<RectTransform>().TransformPoint(button.GetComponent<RectTransform>().rect.center));
        if (Application.isBatchMode
            && PlayModeVerificationFrameWait.DispatchPointerClick(button.gameObject, point))
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield break;
        }

        QueueVerificationMouseState(
            new MouseState { position = point }.WithButton(MouseButton.Left, true));
        yield return null;
        yield return null;
        QueueVerificationMouseState(new MouseState { position = point });
        yield return null;
        yield return null;
    }

    private void QueueVerificationMouseState(MouseState state)
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
        InputSystem.QueueStateEvent(verificationMouse, state);
        InputSystem.Update();
        if (Vector2.Distance(verificationMouse.position.ReadValue(), state.position) > 0.1f)
        {
            InputState.Change(verificationMouse, state);
        }
    }

    private IEnumerator CaptureScreen()
    {
        yield return PlayModeVerificationFrameWait.CaptureReady();
        Texture2D capture = PlayModeVerificationFrameWait.CaptureScreenshotAsTexture();
        Color32[] pixels = capture != null ? capture.GetPixels32() : Array.Empty<Color32>();
        Check(pixels.Any(pixel => pixel.a > 0 && (pixel.r > 8 || pixel.g > 8 || pixel.b > 8)),
            "CAPTURE", $"nonblank pixels={pixels.Length}");
        if (capture != null)
        {
            File.WriteAllBytes(DungeonSaveUiPlayModeVerifier.ScreenshotPath, capture.EncodeToPNG());
            Destroy(capture);
        }
    }

    private static bool IsPanelInsideScreen()
    {
        GameObject panel = FindSceneObject("SavePanel");
        if (panel == null || !(panel.transform is RectTransform rect))
        {
            return false;
        }

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        return min.x >= 0f && min.y >= 0f && max.x <= Screen.width && max.y <= Screen.height;
    }

    private static bool IsUiRaycastBlocking(GameObject modal)
    {
        if (modal == null || EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);
        return results.Any(result => result.gameObject != null
            && result.gameObject.transform.IsChildOf(modal.transform));
    }

    private void BackupRunSlots()
    {
        originalRunSlots.Clear();
        IReadOnlyList<DungeonSaveSlotInfo> slots = slotService.GetSlots();
        foreach (string slotId in new[]
                 {
                     DungeonGameSaveSlotService.AutoSaveSlot,
                     DungeonGameSaveSlotService.QuickSaveSlot,
                     DungeonGameSaveSlotService.ManualSaveSlot
                 })
        {
            DungeonSaveSlotInfo original = slots.FirstOrDefault(info => info.SlotId == slotId);
            originalRunSlots.Add(new SlotBackup
            {
                SlotId = slotId,
                Path = original?.Path,
                Bytes = original != null
                    && !string.IsNullOrWhiteSpace(original.Path)
                    && File.Exists(original.Path)
                        ? File.ReadAllBytes(original.Path)
                        : null
            });
        }
    }

    private void RestoreRunSlots()
    {
        if (slotService == null)
        {
            return;
        }

        foreach (SlotBackup backup in originalRunSlots)
        {
            slotService.Delete(backup.SlotId);
        }

        foreach (SlotBackup backup in originalRunSlots.Where(backup => backup.Bytes != null))
        {
            string path = backup.Path;
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
            File.WriteAllBytes(path, backup.Bytes);
        }
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

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors.Add(condition + "\n" + stackTrace);
        }
        else if (type == LogType.Warning)
        {
            warnings.Add(condition);
        }
    }

    private void Check(bool condition, string id, string detail)
    {
        report.Add($"[{(condition ? "PASS" : "FAIL")}] {id} {detail}");
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<Transform>()
            .Where(candidate => candidate != null && candidate.gameObject.scene.IsValid())
            .Select(candidate => candidate.gameObject)
            .FirstOrDefault(candidate => candidate.name == name);
    }

    private static T FindSceneComponent<T>(string name, string parentName = null) where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .Where(candidate => candidate != null
                && candidate.gameObject.scene.IsValid()
                && candidate.gameObject.name == name
                && (string.IsNullOrEmpty(parentName) || candidate.transform.IsChildOf(FindSceneObject(parentName)?.transform)))
            .FirstOrDefault();
    }

    private sealed class SlotBackup
    {
        public string SlotId;
        public string Path;
        public byte[] Bytes;
    }
}
