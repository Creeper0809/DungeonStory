#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

[InitializeOnLoad]
public static class DungeonDebugModePlayModeVerifier
{
    public const string RequestPath = "Temp/debug-mode-playmode.request";
    public const string ReportPath = "Artifacts/QA/debug-mode-playmode-report.txt";
    public const string DesktopCapturePath = "Artifacts/QA/debug-palette-1600x900.png";
    public const string PortraitCapturePath = "Artifacts/QA/debug-palette-900x1600.png";

    private static bool runnerCreated;

    static DungeonDebugModePlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("DungeonStory/Debug/Developer Mode/Request PlayMode Verification")]
    public static void RequestRunFromMenu()
    {
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
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

    private static void OnPlayModeChanged(PlayModeStateChange change)
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
        new GameObject("Dungeon Debug Mode Verification Runner")
            .AddComponent<DungeonDebugModeVerificationRunner>();
    }
}

public sealed class DungeonDebugModeVerificationRunner : MonoBehaviour
{
    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();

    private Mouse originalMouse;
    private Mouse verificationMouse;
    private Keyboard originalKeyboard;
    private Keyboard verificationKeyboard;
    private DungeonAutomationInputTestCapability automationInput;
    private InputSettings.EditorInputBehaviorInPlayMode originalInputBehavior;
    private int originalGameViewSizeIndex = -1;
    private IDungeonUserSettingsService settings;

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        Application.logMessageReceived += CaptureLog;
        originalGameViewSizeIndex = GameViewResolutionController.SelectedSizeIndex;
        ConfigureInput();

        yield return SelectResolution(1600, 900);
        DungeonRuntimeLifetimeScope scope = null;
        float deadline = Time.realtimeSinceStartup + 12f;
        while ((scope == null || scope.Container == null) && Time.realtimeSinceStartup < deadline)
        {
            scope = FindObjectsByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null && candidate.Container != null);
            yield return null;
        }

        Check(scope != null && scope.Container != null, "SCOPE", "gameplay lifetime scope resolved");
        if (scope == null || scope.Container == null)
        {
            Finish();
            yield break;
        }

        IObjectResolver container = scope.Container;
        settings = container.Resolve<IDungeonUserSettingsService>();
        IDungeonDebugModeService mode = container.Resolve<IDungeonDebugModeService>();
        IDungeonDebugCommandRegistry registry = container.Resolve<IDungeonDebugCommandRegistry>();
        DungeonDebugPaletteUiController paletteController =
            container.Resolve<DungeonDebugPaletteUiController>();
        IGridSystemProvider gridProvider = container.Resolve<IGridSystemProvider>();
        IWorldDropZoneQuery dropZoneQuery = container.Resolve<IWorldDropZoneQuery>();
        IWorldItemStackRuntime itemRuntime = container.Resolve<IWorldItemStackRuntime>();
        IMainCameraProvider cameraProvider = container.Resolve<IMainCameraProvider>();
        IWildlifeRuntime wildlifeRuntime = container.Resolve<IWildlifeRuntime>();
        InvasionDirectorRuntime director = container
            .Resolve<InvasionSceneRuntimeReferences>()
            .Director;
        IDungeonGameSaveService saveService = container.Resolve<IDungeonGameSaveService>();

        settings.Update(data => data.developerMode = false);
        yield return null;
        Check(!mode.IsDeveloperModeEnabled, "DEFAULT_OFF", "developer mode disabled before pointer flow");

        Button settingsButton = FindButton("SettingsMenuButton");
        yield return Click(settingsButton);
        Button developerTab = FindButton("DeveloperSettingsTab");
        yield return Click(developerTab);
        Toggle developerToggle = FindToggle("DeveloperModeToggle");
        yield return Click(developerToggle);
        Check(mode.IsDeveloperModeEnabled, "OPTIONS_TOGGLE", "actual pointer enabled developer mode");

        yield return Click(FindButton("SettingsCloseButton"));
        Button debugButton = FindButton("DungeonDebugOpenButton");
        Check(debugButton != null && debugButton.gameObject.activeInHierarchy,
            "HUD_BUTTON",
            "center-top debug button visible");
        float timeScaleBeforePalette = Time.timeScale;
        yield return Click(debugButton);
        Check(paletteController.IsPaletteVisible, "PALETTE_OPEN", "non-modal palette opened");
        Check(Mathf.Approximately(Time.timeScale, timeScaleBeforePalette),
            "NON_MODAL",
            $"timeScale={Time.timeScale:0.##}");

        Check(registry.Commands.Count >= 45
                && registry.Commands.Select(command => command.Id).Distinct(StringComparer.Ordinal).Count()
                == registry.Commands.Count,
            "COMMAND_REGISTRY",
            $"commands={registry.Commands.Count}; ids unique");

        yield return Click(FindButton("DebugTab_Spawn"));
        Button spawnButton = FindVisibleExecuteButton(
            new[] { "DebugCommand_spawn:stock:", "DebugCommand_spawn:item:" },
            out string spawnRowName);
        Check(spawnButton != null, "SPAWN_COMMAND_VISIBLE", spawnRowName);
        yield return Click(spawnButton);
        Check(paletteController.IsTargeting, "TARGETING_STARTED", "grid-cell targeting active");

        Grid grid = gridProvider.Grid;
        Vector2Int targetCell = FindVisibleTargetCell(
            grid,
            cameraProvider.Camera,
            dropZoneQuery,
            FindObject("DungeonDebugPalette")?.GetComponent<RectTransform>());
        int beforeQuantity = itemRuntime.GetStacksAt(targetCell, includeStored: true)
            .Sum(stack => stack.Quantity);
        yield return ClickScreen(cameraProvider.Camera.WorldToScreenPoint(grid.GetWorldPos(targetCell)), 0);
        int afterQuantity = itemRuntime.GetStacksAt(targetCell, includeStored: true)
            .Sum(stack => stack.Quantity);
        Check(afterQuantity > beforeQuantity,
            "EXACT_GRID_SPAWN",
            $"cell={targetCell}; quantity={beforeQuantity}->{afterQuantity}");
        Check(!paletteController.IsTargeting, "TARGETING_FINISHED", "single click ended targeting");

        spawnButton = FindExecuteButton(spawnRowName);
        yield return Click(spawnButton);
        QueueKeyboard(new KeyboardState(Key.LeftShift));
        yield return ClickScreen(cameraProvider.Camera.WorldToScreenPoint(grid.GetWorldPos(targetCell)), 0);
        Check(paletteController.IsTargeting, "SHIFT_REPEAT", "Shift click kept targeting active");
        QueueKeyboard(new KeyboardState());
        yield return ClickScreen(cameraProvider.Camera.WorldToScreenPoint(grid.GetWorldPos(targetCell)), 1);
        Check(!paletteController.IsTargeting, "RIGHT_CANCEL", "right click cancelled targeting");

        spawnButton = FindExecuteButton(spawnRowName);
        yield return Click(spawnButton);
        QueueKeyboardForNextFrame(new KeyboardState(Key.Escape));
        yield return null;
        yield return null;
        Check(!paletteController.IsTargeting, "ESC_CANCEL", "Escape cancelled targeting");
        QueueKeyboard(new KeyboardState());

        int historyBeforeOverlay = mode.RecentCommands.Count;
        yield return Click(FindButton("DebugTab_Overlay"));
        Button gridOverlayButton = FindExecuteButton("DebugCommand_overlay:Grid");
        yield return Click(gridOverlayButton);
        Check(mode.IsOverlayEnabled(DungeonDebugOverlayKind.Grid),
            "OVERLAY_ACTIVE",
            "grid overlay enabled");
        Check(mode.RecentCommands.Count == historyBeforeOverlay,
            "OVERLAY_NOT_MUTATION",
            $"history={mode.RecentCommands.Count}");

        yield return Capture(DungeonDebugModePlayModeVerifier.DesktopCapturePath, 1600, 900);
        yield return SelectResolution(900, 1600);
        RectTransform portraitPalette = FindObject("DungeonDebugPalette")?.GetComponent<RectTransform>();
        Check(IsInsideScreen(portraitPalette),
            "PORTRAIT_SHEET_BOUNDS",
            DescribeRect(portraitPalette));
        yield return Capture(DungeonDebugModePlayModeVerifier.PortraitCapturePath, 900, 1600);
        yield return SelectResolution(1600, 900);

        int wildlifeBefore = wildlifeRuntime.Wildlife.Count;
        IDungeonDebugCommand wildlifeSpawn = registry.Commands.FirstOrDefault(command =>
            command.Id.StartsWith("wildlife:spawn:", StringComparison.Ordinal));
        if (wildlifeSpawn != null
            && TryFindVisibleWildlifeSpawnCell(grid, cameraProvider.Camera, out Vector2Int wildlifeCell))
        {
            DungeonDebugCommandResult spawned = registry.Execute(
                wildlifeSpawn,
                ContextForCell(wildlifeCell, 1f));
            Check(spawned.Success && wildlifeRuntime.Wildlife.Count > wildlifeBefore,
                "WILDLIFE_COMMAND",
                $"{spawned.Message}; cell={wildlifeCell}");
        }
        else
        {
            Check(false, "WILDLIFE_COMMAND", "wildlife spawn command or visible exterior cell missing");
        }

        DungeonDebugCommandResult invasionResult = Execute(registry, "defense:invasion");
        yield return new WaitForSecondsRealtime(0.5f);
        bool hasDirector = director != null;
        Check(invasionResult.Success && hasDirector && director.ActiveIntruders.Count > 0,
            "NORMAL_INVASION",
            invasionResult.Message);

        CharacterActor commandTarget = hasDirector
            ? director.ActiveIntruders
                .Select(runtime => runtime?.IntruderActor)
                .FirstOrDefault(actor => actor != null && !actor.IsDead)
            : null;
        if (commandTarget != null
            && registry.TryGet("character:heal", out IDungeonDebugCommand heal))
        {
            commandTarget.ApplyDamage(5f, "debug verifier");
            DungeonDebugCommandResult healed = registry.Execute(
                heal,
                ContextForCharacter(commandTarget));
            Check(healed.Success
                    && Mathf.Approximately(commandTarget.CurrentHealth, commandTarget.MaxHealth),
                "CHARACTER_COMMAND",
                healed.Message);
        }
        else
        {
            Check(false, "CHARACTER_COMMAND", "spawned humanoid or heal command missing");
        }

        DungeonDebugCommandResult victory = Execute(registry, "defense:resolve-victory");
        yield return new WaitForSecondsRealtime(0.25f);
        Check(victory.Success, "INVASION_VICTORY", victory.Message);

        DungeonDebugCommandResult bossResult = Execute(registry, "defense:boss-invasion");
        yield return new WaitForSecondsRealtime(0.5f);
        Check(bossResult.Success && director.ActiveIntruders.Count > 0,
            "BOSS_INVASION",
            bossResult.Message);
        Execute(registry, "defense:resolve-victory");
        yield return new WaitForSecondsRealtime(0.25f);

        Check(mode.IsDebugModified && mode.RecentCommands.Count > 0,
            "DEBUG_METADATA",
            $"modified={mode.IsDebugModified}; history={mode.RecentCommands.Count}");
        DungeonGameSaveData save = saveService.Capture();
        DungeonDebugRunSaveData savedDebug =
            DungeonSaveSectionPayload.ReadOrNew<DungeonDebugRunSaveData>(
                save,
                DungeonDebugSaveSection.Id);
        Check(savedDebug.debugModified,
            "SAVE_METADATA",
            $"savedDebug={savedDebug.debugModified}");

        mode.SetCheat(DungeonDebugCheat.FreezeNeeds, true);
        DungeonDebugRunSaveData debugSnapshot = mode.Capture();
        mode.PublishRestoreCandidate(
            mode.PrepareRestoreCandidate(debugSnapshot));
        Check(mode.IsDebugModified && !mode.IsCheatEnabled(DungeonDebugCheat.FreezeNeeds),
            "LOAD_RESETS_CHEATS",
            "world marker persisted and transient cheat cleared");

        settings.Update(data => data.developerMode = false);
        yield return null;
        Check(!mode.IsDeveloperModeEnabled
                && !paletteController.IsPaletteVisible
                && !mode.IsOverlayEnabled(DungeonDebugOverlayKind.Grid),
            "DISABLE_CLEARS_TRANSIENT",
            "palette, targeting, cheats, and overlays disabled");

        Finish();
    }

    private static DungeonDebugExecutionContext ContextForCharacter(CharacterActor actor)
    {
        return new DungeonDebugExecutionContext
        {
            Target = new DungeonDebugTargetSelection
            {
                Kind = DungeonDebugTargetKind.Character,
                Character = actor,
                SourceObject = actor
            }
        };
    }

    private static DungeonDebugExecutionContext ContextForCell(Vector2Int position, float value)
    {
        return new DungeonDebugExecutionContext
        {
            NumericValue = value,
            Target = new DungeonDebugTargetSelection
            {
                Kind = DungeonDebugTargetKind.GridCell,
                HasGridPosition = true,
                GridPosition = position
            }
        };
    }

    private static DungeonDebugCommandResult Execute(
        IDungeonDebugCommandRegistry registry,
        string id)
    {
        return registry.TryGet(id, out IDungeonDebugCommand command)
            ? registry.Execute(command, new DungeonDebugExecutionContext())
            : DungeonDebugCommandResult.Failed($"명령 없음: {id}");
    }

    private static Vector2Int FindVisibleTargetCell(
        Grid grid,
        Camera camera,
        IWorldDropZoneQuery dropZoneQuery,
        RectTransform palette)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();
        if (dropZoneQuery.TryGetDeliveryDropoff(out Vector2Int dropoff))
        {
            candidates.Add(dropoff);
        }

        candidates.AddRange(grid.GetCells()
            .Where(cell => cell != null && cell.AllowsItemDrop)
            .Select(cell => cell.Position));
        foreach (Vector2Int candidate in candidates.Distinct())
        {
            Vector3 screen = camera.WorldToScreenPoint(grid.GetWorldPos(candidate));
            if (screen.z <= 0f
                || screen.x < 16f
                || screen.x > Screen.width - 16f
                || screen.y < 16f
                || screen.y > Screen.height - 16f)
            {
                continue;
            }

            if (palette == null
                || !RectTransformUtility.RectangleContainsScreenPoint(palette, screen, null))
            {
                return candidate;
            }
        }

        return dropZoneQuery.TryGetDeliveryDropoff(out dropoff) ? dropoff : Vector2Int.zero;
    }

    private static bool TryFindVisibleWildlifeSpawnCell(
        Grid grid,
        Camera camera,
        out Vector2Int position)
    {
        position = default;
        if (grid == null || camera == null)
        {
            return false;
        }

        IEnumerable<GridCell> candidates = grid.GetCells()
            .Where(cell => WildlifeRuntime.IsInitialWildlifeSpawnCell(grid, cell))
            .OrderBy(cell => Mathf.Abs(cell.Position.x - grid.width / 2f));
        GridCell fallback = null;
        foreach (GridCell cell in candidates)
        {
            fallback ??= cell;
            Vector3 screen = camera.WorldToScreenPoint(grid.GetWorldPos(cell.Position));
            if (screen.z <= 0f
                || screen.x < 16f
                || screen.x > Screen.width - 16f
                || screen.y < 16f
                || screen.y > Screen.height - 16f)
            {
                continue;
            }

            position = cell.Position;
            return true;
        }

        if (fallback == null)
        {
            return false;
        }

        position = fallback.Position;
        return true;
    }

    private IEnumerator Click(Selectable selectable)
    {
        Check(selectable != null && selectable.gameObject.activeInHierarchy,
            "POINTER_TARGET_" + (selectable != null ? selectable.name : "missing"),
            selectable != null ? selectable.name : "missing");
        if (selectable == null)
        {
            yield break;
        }

        RectTransform rect = selectable.GetComponent<RectTransform>();
        Vector2 point = RectTransformUtility.WorldToScreenPoint(
            null,
            rect.TransformPoint(rect.rect.center));
        yield return ClickScreen(point, 0);
    }

    private IEnumerator ClickScreen(Vector2 screenPoint, int button)
    {
        QueueMouse(new MouseState { position = screenPoint });
        yield return null;
        bool dispatched = DispatchPointerClick(screenPoint, button);
        Check(dispatched, "POINTER_DISPATCH", $"position={screenPoint}; button={button}");
        yield return null;
        yield return null;
    }

    private static bool DispatchPointerClick(Vector2 screenPoint, int button)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            button = button == 1
                ? PointerEventData.InputButton.Right
                : PointerEventData.InputButton.Left,
            position = screenPoint,
            pressPosition = screenPoint
        };
        List<RaycastResult> hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        foreach (RaycastResult hit in hits)
        {
            GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            if (handler == null)
            {
                continue;
            }

            pointer.pointerCurrentRaycast = hit;
            pointer.pointerPressRaycast = hit;
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerExitHandler);
            return true;
        }

        return false;
    }

    private IEnumerator SelectResolution(int width, int height)
    {
        GameViewResolutionController.Select(width, height);
        float deadline = Time.realtimeSinceStartup + 4f;
        while ((Screen.width != width || Screen.height != height)
            && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        Check(Screen.width == width && Screen.height == height,
            $"RESOLUTION_{width}x{height}",
            $"actual={Screen.width}x{Screen.height}");
    }

    private IEnumerator Capture(string path, int width, int height)
    {
        yield return new WaitForEndOfFrame();
        Texture2D texture = PlayModeVerificationFrameWait.CaptureScreenshotAsTexture();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        int visible = texture.GetPixels32()
            .Count(pixel => pixel.a > 0 && (pixel.r > 5 || pixel.g > 5 || pixel.b > 5));
        Check(texture.width == width && texture.height == height && visible > texture.width * texture.height / 20,
            "CAPTURE_" + width + "x" + height,
            $"size={texture.width}x{texture.height}; visible={visible}");
        Destroy(texture);
    }

    private void ConfigureInput()
    {
        originalInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        originalMouse = Mouse.current;
        originalKeyboard = Keyboard.current;
        if (originalMouse != null)
        {
            InputSystem.DisableDevice(originalMouse);
        }
        if (originalKeyboard != null)
        {
            InputSystem.DisableDevice(originalKeyboard);
        }

        verificationMouse = InputSystem.AddDevice<Mouse>("DungeonDebugVerificationMouse");
        verificationKeyboard = InputSystem.AddDevice<Keyboard>("DungeonDebugVerificationKeyboard");
        verificationMouse.MakeCurrent();
        verificationKeyboard.MakeCurrent();
        automationInput = new DungeonAutomationInputTestCapability();
        automationInput.Enable();
    }

    private void QueueMouse(MouseState state)
    {
        automationInput.MovePointer(state.position);
        verificationMouse.MakeCurrent();
        InputSystem.QueueStateEvent(verificationMouse, state);
        InputSystem.Update();
    }

    private void QueueKeyboard(KeyboardState state)
    {
        verificationKeyboard.MakeCurrent();
        InputSystem.QueueStateEvent(verificationKeyboard, state);
        InputSystem.Update();
    }

    private void QueueKeyboardForNextFrame(KeyboardState state)
    {
        verificationKeyboard.MakeCurrent();
        InputSystem.QueueStateEvent(verificationKeyboard, state);
    }

    private void TeardownInput()
    {
        automationInput?.Dispose();
        automationInput = null;
        if (verificationMouse != null && verificationMouse.added)
        {
            InputSystem.RemoveDevice(verificationMouse);
        }
        if (verificationKeyboard != null && verificationKeyboard.added)
        {
            InputSystem.RemoveDevice(verificationKeyboard);
        }
        if (originalMouse != null && originalMouse.added)
        {
            InputSystem.EnableDevice(originalMouse);
            originalMouse.MakeCurrent();
        }
        if (originalKeyboard != null && originalKeyboard.added)
        {
            InputSystem.EnableDevice(originalKeyboard);
            originalKeyboard.MakeCurrent();
        }
        InputSystem.settings.editorInputBehaviorInPlayMode = originalInputBehavior;
    }

    private void Finish()
    {
        settings?.Update(data => data.developerMode = false);
        if (originalGameViewSizeIndex >= 0)
        {
            GameViewResolutionController.SelectedSizeIndex = originalGameViewSizeIndex;
        }
        TeardownInput();
        Application.logMessageReceived -= CaptureLog;
        File.Delete(DungeonDebugModePlayModeVerifier.RequestPath);
        report.Add($"CONSOLE errors={errors.Count}; warnings={warnings.Count}");
        if (errors.Count > 0 || warnings.Count > 0)
        {
            failures.Add($"Console errors={errors.Count}, warnings={warnings.Count}");
        }
        report.Add($"RESULT={(failures.Count == 0 ? "PASS" : "FAIL")}");
        foreach (string failure in failures)
        {
            report.Add("FAILURE=" + failure);
        }
        File.WriteAllLines(DungeonDebugModePlayModeVerifier.ReportPath, report);
        if (failures.Count > 0)
        {
            Debug.LogError("Developer mode PlayMode verification failed: " + string.Join(" | ", failures));
        }
        else
        {
            Debug.Log("Developer mode PlayMode verification passed.");
        }
        EditorApplication.ExitPlaymode();
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

    private static Button FindButton(string name)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && button.name == name);
    }

    private static Toggle FindToggle(string name)
    {
        return Resources.FindObjectsOfTypeAll<Toggle>()
            .FirstOrDefault(toggle => toggle != null
                && toggle.gameObject.scene.IsValid()
                && toggle.gameObject.activeInHierarchy
                && toggle.name == name);
    }

    private static Button FindExecuteButton(string rowName)
    {
        GameObject row = FindObject(rowName);
        return row != null
            ? row.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == "Execute")
            : null;
    }

    private static Button FindVisibleExecuteButton(
        IReadOnlyList<string> rowPrefixes,
        out string rowName)
    {
        rowName = string.Empty;
        RectTransform viewport = FindObject("DebugCommandViewport")?.GetComponent<RectTransform>();
        if (viewport == null)
        {
            return null;
        }

        foreach (GameObject row in Resources.FindObjectsOfTypeAll<GameObject>()
                     .Where(candidate => candidate != null
                         && candidate.scene.IsValid()
                         && candidate.activeInHierarchy
                         && rowPrefixes.Any(prefix =>
                             candidate.name.StartsWith(prefix, StringComparison.Ordinal))))
        {
            Button execute = row.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == "Execute");
            RectTransform executeRect = execute != null
                ? execute.GetComponent<RectTransform>()
                : null;
            if (executeRect == null)
            {
                continue;
            }

            Vector2 center = RectTransformUtility.WorldToScreenPoint(
                null,
                executeRect.TransformPoint(executeRect.rect.center));
            if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, center, null))
            {
                continue;
            }

            rowName = row.name;
            return execute;
        }

        return null;
    }

    private static GameObject FindObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(candidate => candidate != null
                && candidate.scene.IsValid()
                && candidate.name == name);
    }

    private static bool IsInsideScreen(RectTransform rect)
    {
        if (rect == null)
        {
            return false;
        }

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return corners.All(corner =>
            corner.x >= -1f
            && corner.x <= Screen.width + 1f
            && corner.y >= -1f
            && corner.y <= Screen.height + 1f);
    }

    private static string DescribeRect(RectTransform rect)
    {
        if (rect == null)
        {
            return "missing";
        }

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return $"min=({corners[0].x:0},{corners[0].y:0}); max=({corners[2].x:0},{corners[2].y:0}); screen={Screen.width}x{Screen.height}";
    }
}
#endif
