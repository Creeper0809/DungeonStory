using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

[InitializeOnLoad]
public static class OwnerDoctrinePlayModeVerifier
{
    public const string RequestPath = "Temp/owner-doctrine-verification.request";
    public const string ReportPath = "Temp/owner-doctrine-verification-report.txt";
    public const string SelectionCapturePath = "Temp/owner-doctrine-selection.png";

    private static bool runnerCreated;

    static OwnerDoctrinePlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/QA/Request Owner Doctrine Verification")]
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
            GameObject runnerObject = new GameObject("Owner Doctrine Verification Runner");
            UnityEngine.Object.DontDestroyOnLoad(runnerObject);
            runnerObject.AddComponent<OwnerDoctrineVerificationRunner>();
        }
    }
}

public sealed class OwnerDoctrineVerificationRunner : MonoBehaviour
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
    private IDungeonGameSaveSlotService slotService;

    private IEnumerator Start()
    {
        yield return Run();
    }

    private IEnumerator Run()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(OwnerDoctrinePlayModeVerifier.ReportPath, "OWNER_DOCTRINE IN_PROGRESS\n");
        Application.logMessageReceived += CaptureLog;
        ConfigureInput();

        try
        {
            yield return WaitForScope();
            DungeonRuntimeLifetimeScope firstScope = FindScope();
            Check(firstScope != null, "DI_SCOPE", "initial game container resolved");
            if (firstScope == null)
            {
                yield break;
            }

            BackupPersistentFiles(firstScope);
            OwnerDoctrineDefinition[] doctrines =
            {
                OwnerDoctrineCatalog.Get(OwnerDoctrineIds.SlimeStewardship),
                OwnerDoctrineCatalog.Get(OwnerDoctrineIds.OrcWarCamp),
                OwnerDoctrineCatalog.Get(OwnerDoctrineIds.VampireForbiddenStudy)
            };

            for (int index = 0; index < doctrines.Length; index++)
            {
                OwnerDoctrineDefinition doctrine = doctrines[index];
                Check(doctrine != null, "DOCTRINE_DEFINITION", doctrine?.id ?? $"missing index={index}");
                if (doctrine == null)
                {
                    continue;
                }

                yield return PrepareOwnerSelection(index == 0);
                if (index == 0)
                {
                    VerifySelectionCards();
                    yield return CaptureScreen(OwnerDoctrinePlayModeVerifier.SelectionCapturePath);
                }

                yield return SelectOwner(doctrine);
                VerifyActiveDoctrine(doctrine);

                if (index < doctrines.Length - 1)
                {
                    Scene activeScene = SceneManager.GetActiveScene();
                    SceneManager.LoadScene(activeScene.path);
                    yield return WaitForScope();
                }
            }
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
            report.Insert(0, passed ? "OWNER_DOCTRINE PASS" : "OWNER_DOCTRINE FAIL");
            File.WriteAllLines(OwnerDoctrinePlayModeVerifier.ReportPath, report);
            File.Delete(OwnerDoctrinePlayModeVerifier.RequestPath);
            EditorApplication.ExitPlaymode();
        }
    }

    private IEnumerator PrepareOwnerSelection(bool requireCardAudit)
    {
        Button startNew = FindButton("StartNewRunButton");
        if (startNew != null && startNew.gameObject.activeInHierarchy && startNew.interactable)
        {
            yield return Click(startNew, "new game");
            if (startNew.gameObject.activeInHierarchy)
            {
                yield return Click(startNew, "confirm new game");
            }
        }
        else
        {
            Check(
                SceneManager.GetActiveScene().name == DungeonSceneNavigator.GameplaySceneName,
                "GAMEPLAY_ENTRY",
                $"dedicated Gameplay scene starts directly for doctrine QA; cardAudit={requireCardAudit}");
        }

        yield return new WaitForSecondsRealtime(0.25f);
        Button[] ownerButtons = FindOwnerButtons();
        Check(ownerButtons.Length == 3, "OWNER_CARD_COUNT", $"cards={ownerButtons.Length}");
    }

    private void VerifySelectionCards()
    {
        Canvas.ForceUpdateCanvases();
        Button[] ownerButtons = FindOwnerButtons();
        foreach (OwnerDoctrineDefinition doctrine in OwnerDoctrineCatalog.All)
        {
            Button button = ownerButtons.FirstOrDefault(candidate =>
                GetButtonText(candidate).Contains(doctrine.title, StringComparison.Ordinal));
            string text = GetButtonText(button);
            TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            bool exactEffectsVisible = text.Contains(doctrine.benefit, StringComparison.Ordinal)
                && text.Contains(doctrine.tradeoff, StringComparison.Ordinal);
            bool textFits = label != null && !label.isTextOverflowing && label.fontSize >= 13f;
            bool insideScreen = button != null && IsInsideScreen(button.GetComponent<RectTransform>());
            Check(
                button != null && exactEffectsVisible && textFits && insideScreen,
                "OWNER_CARD_" + doctrine.speciesTag.ToUpperInvariant(),
                $"effects={exactEffectsVisible}; fits={textFits}; inside={insideScreen}; font={label?.fontSize:0.##}");
        }
    }

    private IEnumerator SelectOwner(OwnerDoctrineDefinition doctrine)
    {
        Button button = FindOwnerButtons().FirstOrDefault(candidate =>
            GetButtonText(candidate).Contains(doctrine.title, StringComparison.Ordinal));
        yield return Click(button, doctrine.speciesTag + " owner");
        yield return StartPartyPlayModeTestDriver.CompleteIfVisible();

        float deadline = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < deadline)
        {
            DungeonRuntimeLifetimeScope scope = FindScope();
            IRunVariableRuntimeProvider provider = scope?.Container.Resolve<IRunVariableRuntimeProvider>();
            if (provider != null
                && provider.TryGetRuntime(out RunVariableRuntime runtime)
                && runtime?.State.StartVariables?.ownerDoctrineId == doctrine.id)
            {
                yield break;
            }

            yield return null;
        }
    }

    private void VerifyActiveDoctrine(OwnerDoctrineDefinition doctrine)
    {
        DungeonRuntimeLifetimeScope scope = FindScope();
        IOwnerRunManagerProvider ownerProvider = scope?.Container.Resolve<IOwnerRunManagerProvider>();
        IRunVariableRuntimeProvider runProvider = scope?.Container.Resolve<IRunVariableRuntimeProvider>();
        IFacilityShopCatalog catalog = scope?.Container.Resolve<IFacilityShopCatalog>();

        bool ownerSelected = ownerProvider != null
            && ownerProvider.TryGetManager(out OwnerRunManager manager)
            && manager.CurrentOwnerActor != null
            && string.Equals(
                manager.CurrentOwnerActor.SpeciesTag,
                doctrine.speciesTag,
                StringComparison.OrdinalIgnoreCase);
        RunVariableRuntime runtime = null;
        bool runtimeReady = runProvider != null
            && runProvider.TryGetRuntime(out runtime)
            && runtime?.State.StartVariables != null;
        Check(ownerSelected, "OWNER_SELECTED_" + doctrine.speciesTag.ToUpperInvariant(), doctrine.id);
        Check(
            runtimeReady && runtime.State.StartVariables.ownerDoctrineId == doctrine.id,
            "DOCTRINE_ACTIVE_" + doctrine.speciesTag.ToUpperInvariant(),
            runtimeReady ? runtime.State.StartVariables.ownerDoctrineId : "runtime missing");
        if (!runtimeReady)
        {
            return;
        }

        BuildingSO general = catalog?.Buildings.FirstOrDefault(building => building != null
            && (building.Defense == null || !building.Defense.IsDefenseFacility));
        BuildingSO defense = catalog?.Buildings.FirstOrDefault(building => building?.Defense?.IsDefenseFacility == true);
        FacilityBlueprintSO blueprint = catalog?.Blueprints.FirstOrDefault(candidate => candidate != null);

        bool effectsCorrect = doctrine.id switch
        {
            OwnerDoctrineIds.SlimeStewardship =>
                Mathf.Approximately(runtime.GetStockCostMultiplier(StockCategory.Food), 0.85f)
                && Mathf.Approximately(runtime.GetStockCostMultiplier(StockCategory.General), 0.85f)
                && general != null
                && Mathf.Approximately(runtime.GetFacilityShopCostMultiplier(general), 1.1f),
            OwnerDoctrineIds.OrcWarCamp =>
                defense != null
                && Mathf.Approximately(runtime.GetFacilityShopCostMultiplier(defense), 0.75f)
                && Mathf.Approximately(runtime.GetThreatRiseMultiplier(), 1.15f),
            OwnerDoctrineIds.VampireForbiddenStudy =>
                blueprint != null
                && Mathf.Approximately(runtime.GetBlueprintCostMultiplier(blueprint), 0.75f)
                && general != null
                && Mathf.Approximately(runtime.GetFacilityShopCostMultiplier(general), 1.1f),
            _ => false
        };
        Check(
            effectsCorrect,
            "DOCTRINE_EFFECTS_" + doctrine.speciesTag.ToUpperInvariant(),
            $"stockFood={runtime.GetStockCostMultiplier(StockCategory.Food):0.##}; "
            + $"shopGeneral={(general != null ? runtime.GetFacilityShopCostMultiplier(general) : -1f):0.##}; "
            + $"shopDefense={(defense != null ? runtime.GetFacilityShopCostMultiplier(defense) : -1f):0.##}; "
            + $"blueprint={(blueprint != null ? runtime.GetBlueprintCostMultiplier(blueprint) : -1f):0.##}; "
            + $"threat={runtime.GetThreatRiseMultiplier():0.##}");
    }

    private IEnumerator Click(Button button, string label)
    {
        bool available = button != null && button.gameObject.activeInHierarchy && button.interactable;
        Check(available, "POINTER_TARGET", available ? label : label + " missing");
        if (!available)
        {
            yield break;
        }

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

    private IEnumerator CaptureScreen(string path)
    {
        yield return new WaitForEndOfFrame();
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        Color32[] pixels = capture != null ? capture.GetPixels32() : Array.Empty<Color32>();
        bool nonblank = pixels.Any(pixel => pixel.a > 0 && (pixel.r > 8 || pixel.g > 8 || pixel.b > 8));
        Check(nonblank, "SCREEN_CAPTURE", $"nonblank={nonblank}; pixels={pixels.Length}");
        if (capture != null)
        {
            File.WriteAllBytes(path, capture.EncodeToPNG());
            Destroy(capture);
        }
    }

    private static bool IsInsideScreen(RectTransform rect)
    {
        if (rect == null)
        {
            return false;
        }

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return corners.All(corner => corner.x >= 0f
            && corner.y >= 0f
            && corner.x <= Screen.width
            && corner.y <= Screen.height);
    }

    private static string GetButtonText(Button button)
    {
        return button?.GetComponentInChildren<TMP_Text>(true)?.text ?? string.Empty;
    }

    private static Button[] FindOwnerButtons()
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .Where(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && button.name.StartsWith("OwnerOption_", StringComparison.Ordinal))
            .OrderBy(button => button.name, StringComparer.Ordinal)
            .ToArray();
    }

    private static Button FindButton(string name)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && button.name == name);
    }

    private static DungeonRuntimeLifetimeScope FindScope()
    {
        return FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(scope => scope != null && scope.Container != null);
    }

    private IEnumerator WaitForScope()
    {
        float deadline = Time.realtimeSinceStartup + 6f;
        while (Time.realtimeSinceStartup < deadline && FindScope() == null)
        {
            yield return null;
        }

        yield return new WaitForSecondsRealtime(1f);
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

        verificationMouse = InputSystem.AddDevice<Mouse>("OwnerDoctrineVerificationMouse");
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
        slotService = scope.Container.Resolve<IDungeonGameSaveSlotService>();
        foreach (DungeonSaveSlotInfo slot in slotService.GetSlots())
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
        if (slotService != null)
        {
            HashSet<string> originalPaths = backups
                .Where(backup => backup.Bytes != null)
                .Select(backup => backup.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (DungeonSaveSlotInfo slot in slotService.GetSlots())
            {
                if (!originalPaths.Contains(slot.Path) && File.Exists(slot.Path))
                {
                    File.Delete(slot.Path);
                }
            }
        }

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
            OwnerDoctrinePlayModeVerifier.ReportPath,
            new[] { "OWNER_DOCTRINE IN_PROGRESS" }.Concat(report));
    }
}
