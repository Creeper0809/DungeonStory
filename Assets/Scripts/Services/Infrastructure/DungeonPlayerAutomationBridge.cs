using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer.Unity;

public static class DungeonAutomationInputState
{
    private static readonly Dictionary<KeyCode, double> HeldKeys = new Dictionary<KeyCode, double>();
    private static readonly Dictionary<KeyCode, int> KeyDownFrames = new Dictionary<KeyCode, int>();
    private static readonly int[] MouseDownFrames = { -1, -1, -1 };
    private static readonly int[] MouseHeldUntilFrames = { -1, -1, -1 };
    private static readonly AutomationInputClockState Clocks = new AutomationInputClockState();

    private static bool enabled;
    private static bool pointerOverridden;
    private static Vector3 pointerPosition;
    private static float scrollDeltaY;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        Disable();
    }

    public static void Enable(IGameClock frameClock, IUiClock realtimeClock)
    {
        Clocks.Configure(frameClock, realtimeClock);
        enabled = true;
    }

    public static void Disable()
    {
        enabled = false;
        Clocks.Clear();
        pointerOverridden = false;
        pointerPosition = Vector3.zero;
        scrollDeltaY = 0f;
        HeldKeys.Clear();
        KeyDownFrames.Clear();
        for (int index = 0; index < MouseDownFrames.Length; index++)
        {
            MouseDownFrames[index] = -1;
            MouseHeldUntilFrames[index] = -1;
        }
    }

    public static bool TryGetPointerPosition(out Vector3 position)
    {
        position = pointerPosition;
        return enabled && pointerOverridden;
    }

    public static void MovePointer(Vector2 position)
    {
        if (!enabled)
        {
            return;
        }

        pointerOverridden = true;
        pointerPosition = new Vector3(
            Mathf.Clamp(position.x, 0f, Mathf.Max(0f, Screen.width - 1f)),
            Mathf.Clamp(position.y, 0f, Mathf.Max(0f, Screen.height - 1f)),
            0f);
    }

    public static int ClickPointer(int button)
    {
        if (!enabled || button < 0 || button >= MouseDownFrames.Length)
        {
            return -1;
        }

        int frame = CurrentFrame;
        if (frame < 0)
        {
            return -1;
        }

        int downFrame = frame + 1;
        MouseDownFrames[button] = downFrame;
        MouseHeldUntilFrames[button] = downFrame + 1;
        return downFrame;
    }

    public static void Scroll(float deltaY)
    {
        if (!enabled || Mathf.Approximately(deltaY, 0f))
        {
            return;
        }

        scrollDeltaY += deltaY;
    }

    public static bool TryConsumeScrollDeltaY(out float deltaY)
    {
        deltaY = 0f;
        if (!enabled || Mathf.Approximately(scrollDeltaY, 0f))
        {
            return false;
        }

        deltaY = scrollDeltaY;
        scrollDeltaY = 0f;
        return true;
    }

    public static bool GetMouseButtonDown(int button)
    {
        int frame = CurrentFrame;
        return enabled
            && frame >= 0
            && button >= 0
            && button < MouseDownFrames.Length
            && MouseDownFrames[button] == frame;
    }

    public static bool GetMouseButton(int button)
    {
        int frame = CurrentFrame;
        return enabled
            && frame >= 0
            && button >= 0
            && button < MouseHeldUntilFrames.Length
            && frame <= MouseHeldUntilFrames[button];
    }

    public static bool HoldKey(KeyCode key, float durationSeconds)
    {
        int frame = CurrentFrame;
        if (!enabled || key == KeyCode.None || frame < 0)
        {
            return false;
        }

        HeldKeys[key] = CurrentRealtime + Mathf.Clamp(durationSeconds, 0.05f, 30f);
        KeyDownFrames[key] = frame + 1;
        return true;
    }

    public static void ReleaseKey(KeyCode key)
    {
        HeldKeys.Remove(key);
        KeyDownFrames.Remove(key);
    }

    public static bool GetKey(KeyCode key)
    {
        if (!enabled || !HeldKeys.TryGetValue(key, out double expiresAt))
        {
            return false;
        }

        if (CurrentRealtime <= expiresAt)
        {
            return true;
        }

        ReleaseKey(key);
        return false;
    }

    public static bool GetKeyDown(KeyCode key)
    {
        int currentFrame = CurrentFrame;
        return enabled
            && currentFrame >= 0
            && KeyDownFrames.TryGetValue(key, out int frame)
            && frame == currentFrame;
    }

    private static int CurrentFrame => Clocks.FrameCount;
    private static double CurrentRealtime => Clocks.Realtime;

    private sealed class AutomationInputClockState
    {
        private IGameClock gameClock;
        private IUiClock uiClock;

        public int FrameCount => gameClock != null ? gameClock.FrameCount : -1;
        public double Realtime => uiClock != null ? uiClock.Time : 0d;

        public void Configure(IGameClock frameClock, IUiClock realtimeClock)
        {
            gameClock = frameClock ?? throw new ArgumentNullException(nameof(frameClock));
            uiClock = realtimeClock ?? throw new ArgumentNullException(nameof(realtimeClock));
        }

        public void Clear()
        {
            gameClock = null;
            uiClock = null;
        }
    }
}

public sealed class DungeonPlayerAutomationBridge : IStartable, IDisposable
{
    private readonly IDungeonRunFlowRuntime runFlow;
    private readonly IFirstRunObjectiveRuntime firstRunObjective;
    private readonly IGameDataProvider gameDataProvider;
    private readonly DungeonUserSettingsRuntimeTargets userSettingsTargets;
    private readonly IDungeonUiCanvasProvider canvasProvider;
    private readonly IMainCameraProvider mainCameraProvider;
    private readonly IGameClock gameClock;
    private readonly IUiClock uiClock;
    private readonly IGameTimeScaleController timeScaleController;

    private DungeonPlayerAutomationHost host;

    public DungeonPlayerAutomationBridge(
        IDungeonRunFlowRuntime runFlow,
        IFirstRunObjectiveRuntime firstRunObjective,
        IGameDataProvider gameDataProvider,
        DungeonUserSettingsRuntimeTargets userSettingsTargets,
        IDungeonUiCanvasProvider canvasProvider,
        IMainCameraProvider mainCameraProvider,
        IGameClock gameClock,
        IUiClock uiClock,
        IGameTimeScaleController timeScaleController)
    {
        this.runFlow = runFlow ?? throw new ArgumentNullException(nameof(runFlow));
        this.firstRunObjective = firstRunObjective ?? throw new ArgumentNullException(nameof(firstRunObjective));
        this.gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.userSettingsTargets = userSettingsTargets
            ?? throw new ArgumentNullException(nameof(userSettingsTargets));
        this.canvasProvider = canvasProvider
            ?? throw new ArgumentNullException(nameof(canvasProvider));
        this.mainCameraProvider = mainCameraProvider
            ?? throw new ArgumentNullException(nameof(mainCameraProvider));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        this.timeScaleController = timeScaleController
            ?? throw new ArgumentNullException(nameof(timeScaleController));
    }

    public void Start()
    {
        DungeonPlayerAutomationConfig config = DungeonPlayerAutomationConfig.FromCommandLine(
            Environment.GetCommandLineArgs());
        if (!config.Requested)
        {
            return;
        }

        bool playtestBuild = Application.identifier.EndsWith(".playtest", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Application.productName, "DungeonStoryPlaytest", StringComparison.Ordinal);
        bool allowedBuild = Debug.isDebugBuild || playtestBuild;
        if (!allowedBuild)
        {
            Debug.LogWarning(
                $"Player automation was requested but this build does not allow it "
                + $"(product={Application.productName}, identifier={Application.identifier}, debug={Debug.isDebugBuild}).");
            return;
        }

        GameObject hostObject = new GameObject("DungeonPlayerAutomationHost");
        UnityEngine.Object.DontDestroyOnLoad(hostObject);
        host = hostObject.AddComponent<DungeonPlayerAutomationHost>();
        host.Configure(
            config,
            runFlow,
            firstRunObjective,
            gameDataProvider,
            userSettingsTargets,
            canvasProvider,
            mainCameraProvider,
            gameClock,
            uiClock,
            timeScaleController);
    }

    public void Dispose()
    {
        if (host == null)
        {
            return;
        }

        host.Shutdown();
        UnityEngine.Object.Destroy(host.gameObject);
        host = null;
    }
}

[Serializable]
internal sealed class DungeonPlayerAutomationConfig
{
    public bool Requested;
    public int Port = 48761;
    public string Token = string.Empty;

    public static DungeonPlayerAutomationConfig FromCommandLine(IReadOnlyList<string> args)
    {
        DungeonPlayerAutomationConfig config = new DungeonPlayerAutomationConfig();
        if (args == null)
        {
            return config;
        }

        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index] ?? string.Empty;
            if (string.Equals(argument, "-automation", StringComparison.OrdinalIgnoreCase))
            {
                config.Requested = true;
                continue;
            }

            if (string.Equals(argument, "-automation-port", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Count
                && int.TryParse(args[++index], NumberStyles.Integer, CultureInfo.InvariantCulture, out int port))
            {
                config.Port = Mathf.Clamp(port, 0, 65535);
                continue;
            }

            if (string.Equals(argument, "-automation-token", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Count)
            {
                config.Token = args[++index] ?? string.Empty;
            }
        }

        if (config.Requested && string.IsNullOrWhiteSpace(config.Token))
        {
            config.Token = Guid.NewGuid().ToString("N");
        }

        return config;
    }
}

internal sealed class DungeonPlayerAutomationHost : MonoBehaviour
{
    private const int RequestTimeoutMilliseconds = 10000;
    private const int MaxRequestsPerFrame = 32;

    private readonly ConcurrentQueue<PendingAutomationRequest> requests =
        new ConcurrentQueue<PendingAutomationRequest>();

    private IDungeonRunFlowRuntime runFlow;
    private IFirstRunObjectiveRuntime firstRunObjective;
    private IGameDataProvider gameDataProvider;
    private DungeonUserSettingsRuntimeTargets userSettingsTargets;
    private IDungeonUiCanvasProvider canvasProvider;
    private IMainCameraProvider mainCameraProvider;
    private IGameClock gameClock;
    private IUiClock uiClock;
    private IGameTimeScaleController timeScaleController;
    private DungeonPlayerAutomationConfig config;
    private TcpListener listener;
    private Thread listenerThread;
    private volatile bool running;
    private string automationDirectory;
    private string connectionPath;

    public void Configure(
        DungeonPlayerAutomationConfig automationConfig,
        IDungeonRunFlowRuntime flow,
        IFirstRunObjectiveRuntime objective,
        IGameDataProvider dataProvider,
        DungeonUserSettingsRuntimeTargets settingsTargets,
        IDungeonUiCanvasProvider uiCanvasProvider,
        IMainCameraProvider cameraProvider,
        IGameClock frameClock,
        IUiClock realtimeClock,
        IGameTimeScaleController scaleController)
    {
        config = automationConfig ?? throw new ArgumentNullException(nameof(automationConfig));
        runFlow = flow ?? throw new ArgumentNullException(nameof(flow));
        firstRunObjective = objective ?? throw new ArgumentNullException(nameof(objective));
        gameDataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        userSettingsTargets = settingsTargets
            ?? throw new ArgumentNullException(nameof(settingsTargets));
        canvasProvider = uiCanvasProvider
            ?? throw new ArgumentNullException(nameof(uiCanvasProvider));
        mainCameraProvider = cameraProvider ?? throw new ArgumentNullException(nameof(cameraProvider));
        gameClock = frameClock ?? throw new ArgumentNullException(nameof(frameClock));
        uiClock = realtimeClock ?? throw new ArgumentNullException(nameof(realtimeClock));
        timeScaleController = scaleController
            ?? throw new ArgumentNullException(nameof(scaleController));

        automationDirectory = Path.Combine(Application.persistentDataPath, "Automation");
        connectionPath = Path.Combine(automationDirectory, "bridge.json");
        Directory.CreateDirectory(automationDirectory);
        DungeonAutomationInputState.Enable(gameClock, uiClock);
        StartServer();
    }

    private void Update()
    {
        for (int index = 0; index < MaxRequestsPerFrame && requests.TryDequeue(out PendingAutomationRequest pending); index++)
        {
            try
            {
                pending.Response = Execute(pending.Request);
            }
            catch (Exception exception)
            {
                pending.Response = AutomationResponse.Fail(
                    pending.Request != null ? pending.Request.id : string.Empty,
                    exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                pending.Completed.Set();
            }
        }
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    public void Shutdown()
    {
        if (!running && listener == null)
        {
            DungeonAutomationInputState.Disable();
            return;
        }

        running = false;
        try
        {
            listener?.Stop();
        }
        catch (SocketException)
        {
        }

        listener = null;
        if (listenerThread != null && listenerThread.IsAlive)
        {
            listenerThread.Join(500);
        }

        listenerThread = null;
        DungeonAutomationInputState.Disable();
        TryDeleteConnectionFile();
    }

    private void StartServer()
    {
        try
        {
            listener = new TcpListener(IPAddress.Loopback, config.Port);
            listener.Start();
            int actualPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            running = true;
            WriteConnectionFile(actualPort);
            listenerThread = new Thread(AcceptLoop)
            {
                IsBackground = true,
                Name = "DungeonPlayerAutomationListener"
            };
            listenerThread.Start();
            Debug.Log($"Player automation bridge listening on 127.0.0.1:{actualPort}.");
        }
        catch (Exception exception)
        {
            running = false;
            listener = null;
            DungeonAutomationInputState.Disable();
            Debug.LogError("Player automation bridge failed to start: " + exception.Message);
        }
    }

    private void AcceptLoop()
    {
        while (running)
        {
            try
            {
                TcpClient client = listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
            }
            catch (SocketException)
            {
                if (running)
                {
                    Thread.Sleep(50);
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    private void HandleClient(TcpClient client)
    {
        using (client)
        using (NetworkStream stream = client.GetStream())
        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true))
        using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true)
               {
                   AutoFlush = true
               })
        {
            client.NoDelay = true;
            string line = reader.ReadLine();
            AutomationRequest request;
            try
            {
                request = JsonUtility.FromJson<AutomationRequest>(line ?? string.Empty);
            }
            catch (Exception exception)
            {
                writer.WriteLine(JsonUtility.ToJson(AutomationResponse.Fail(string.Empty, "Invalid JSON: " + exception.Message)));
                return;
            }

            if (request == null || !FixedTimeEquals(request.token, config.Token))
            {
                writer.WriteLine(JsonUtility.ToJson(AutomationResponse.Fail(
                    request != null ? request.id : string.Empty,
                    "Unauthorized")));
                return;
            }

            PendingAutomationRequest pending = new PendingAutomationRequest(request);
            requests.Enqueue(pending);
            if (!pending.Completed.Wait(RequestTimeoutMilliseconds))
            {
                writer.WriteLine(JsonUtility.ToJson(AutomationResponse.Fail(request.id, "Main-thread request timed out")));
                return;
            }

            writer.WriteLine(JsonUtility.ToJson(pending.Response ?? AutomationResponse.Fail(request.id, "No response")));
        }
    }

    private AutomationResponse Execute(AutomationRequest request)
    {
        string command = request.command?.Trim().ToLowerInvariant() ?? string.Empty;
        return command switch
        {
            "ping" => AutomationResponse.Ok(request.id, "pong"),
            "game.status" => GetStatus(request.id),
            "ui.list" => ListUi(request.id),
            "ui.click" => ClickUi(request),
            "input.pointer_move" => MovePointer(request),
            "input.pointer_click" => ClickPointer(request),
            "input.key_down" => HoldKey(request),
            "input.key_up" => ReleaseKey(request),
            "capture.screen" => CaptureScreen(request),
            _ => AutomationResponse.Fail(request.id, "Unknown command: " + command)
        };
    }

    private AutomationResponse GetStatus(string requestId)
    {
        GameData gameData = null;
        gameDataProvider.TryGetGameData(out gameData);
        CameraManager cameraManager = userSettingsTargets.CameraManager;
        Vector3 cameraPosition = cameraManager != null
            ? cameraManager.transform.position
            : mainCameraProvider.Camera.transform.position;

        AutomationGameStatus status = new AutomationGameStatus
        {
            product = Application.productName,
            identifier = Application.identifier,
            version = Application.version,
            scene = SceneManager.GetActiveScene().name,
            focused = Application.isFocused,
            screenWidth = Screen.width,
            screenHeight = Screen.height,
            fullScreenMode = Screen.fullScreenMode.ToString(),
            timeScale = timeScaleController.Scale,
            frame = gameClock.FrameCount,
            day = runFlow.CurrentDay,
            phase = runFlow.Phase.ToString(),
            outcome = runFlow.Outcome.ToString(),
            objective = firstRunObjective.CurrentObjective.ToString(),
            money = ValueOrDefault(gameData?.holdingMoney),
            gameSpeed = ValueOrDefault(gameData?.gameSpeed),
            hour = ValueOrDefault(gameData?.hour),
            cameraX = cameraPosition.x,
            cameraY = cameraPosition.y,
            cameraZ = cameraPosition.z
        };
        return AutomationResponse.Ok(requestId, "status", JsonUtility.ToJson(status));
    }

    private AutomationResponse ListUi(string requestId)
    {
        Canvas canvas = canvasProvider.GetOrCreateCanvas();
        IReadOnlyList<Selectable> selectables = canvas != null
            ? canvas.GetComponentsInChildren<Selectable>(includeInactive: false)
            : Array.Empty<Selectable>();
        List<AutomationUiControl> controls = new List<AutomationUiControl>();
        foreach (Selectable selectable in selectables.OrderBy(item => item.gameObject.name, StringComparer.Ordinal))
        {
            if (selectable == null || !selectable.gameObject.activeInHierarchy)
            {
                continue;
            }

            controls.Add(CreateUiControl(selectable));
        }

        AutomationUiControlList result = new AutomationUiControlList
        {
            controls = controls.ToArray()
        };
        return AutomationResponse.Ok(requestId, $"{controls.Count} controls", JsonUtility.ToJson(result));
    }

    private AutomationResponse ClickUi(AutomationRequest request)
    {
        string target = request.target?.Trim() ?? string.Empty;
        Canvas canvas = canvasProvider.GetOrCreateCanvas();
        Button button = (canvas != null
                ? canvas.GetComponentsInChildren<Button>(includeInactive: false)
                : Array.Empty<Button>())
            .FirstOrDefault(candidate => candidate != null
                && candidate.gameObject.activeInHierarchy
                && string.Equals(candidate.gameObject.name, target, StringComparison.Ordinal));
        if (button == null)
        {
            return AutomationResponse.Fail(request.id, "Active button not found: " + target);
        }

        if (!button.IsInteractable())
        {
            return AutomationResponse.Fail(request.id, "Button is not interactable: " + target);
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return AutomationResponse.Fail(request.id, "No active EventSystem");
        }

        Vector2 center = RectTransformUtility.WorldToScreenPoint(
            ResolveCanvasCamera(button.transform),
            ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center));
        DungeonAutomationInputState.MovePointer(center);
        PointerEventData eventData = new PointerEventData(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            position = center,
            pointerPress = button.gameObject,
            rawPointerPress = button.gameObject
        };
        ExecuteEvents.Execute(button.gameObject, eventData, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject, eventData, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject, eventData, ExecuteEvents.pointerClickHandler);
        return AutomationResponse.Ok(request.id, "Clicked " + target);
    }

    private AutomationResponse MovePointer(AutomationRequest request)
    {
        DungeonAutomationInputState.MovePointer(new Vector2(request.x, request.y));
        return AutomationResponse.Ok(request.id, $"Pointer moved to {request.x:0.##},{request.y:0.##}");
    }

    private AutomationResponse ClickPointer(AutomationRequest request)
    {
        DungeonAutomationInputState.MovePointer(new Vector2(request.x, request.y));
        int frame = DungeonAutomationInputState.ClickPointer(Mathf.Clamp(request.button, 0, 2));
        return frame >= 0
            ? AutomationResponse.Ok(request.id, "Pointer click scheduled", $"{{\"frame\":{frame}}}")
            : AutomationResponse.Fail(request.id, "Pointer click could not be scheduled");
    }

    private AutomationResponse HoldKey(AutomationRequest request)
    {
        if (!Enum.TryParse(request.key, true, out KeyCode key) || key == KeyCode.None)
        {
            return AutomationResponse.Fail(request.id, "Unknown KeyCode: " + request.key);
        }

        float duration = request.duration > 0f ? request.duration : 0.25f;
        return DungeonAutomationInputState.HoldKey(key, duration)
            ? AutomationResponse.Ok(request.id, $"Holding {key} for {duration:0.##}s")
            : AutomationResponse.Fail(request.id, "Key could not be held: " + key);
    }

    private AutomationResponse ReleaseKey(AutomationRequest request)
    {
        if (!Enum.TryParse(request.key, true, out KeyCode key) || key == KeyCode.None)
        {
            return AutomationResponse.Fail(request.id, "Unknown KeyCode: " + request.key);
        }

        DungeonAutomationInputState.ReleaseKey(key);
        return AutomationResponse.Ok(request.id, "Released " + key);
    }

    private AutomationResponse CaptureScreen(AutomationRequest request)
    {
        string requestedName = string.IsNullOrWhiteSpace(request.path)
            ? $"capture-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.png"
            : Path.GetFileName(request.path);
        if (!requestedName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            requestedName += ".png";
        }

        string captureDirectory = Path.Combine(automationDirectory, "Captures");
        Directory.CreateDirectory(captureDirectory);
        string capturePath = Path.Combine(captureDirectory, requestedName);
        ScreenCapture.CaptureScreenshot(capturePath);
        AutomationCaptureResult result = new AutomationCaptureResult { path = capturePath };
        return AutomationResponse.Ok(request.id, "Screenshot queued", JsonUtility.ToJson(result));
    }

    private void WriteConnectionFile(int port)
    {
        AutomationConnectionInfo info = new AutomationConnectionInfo
        {
            host = "127.0.0.1",
            port = port,
            token = config.Token,
            processId = System.Diagnostics.Process.GetCurrentProcess().Id,
            product = Application.productName,
            identifier = Application.identifier,
            startedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };
        File.WriteAllText(connectionPath, JsonUtility.ToJson(info, true), new UTF8Encoding(false));
    }

    private void TryDeleteConnectionFile()
    {
        try
        {
            if (File.Exists(connectionPath))
            {
                File.Delete(connectionPath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static AutomationUiControl CreateUiControl(Selectable selectable)
    {
        RectTransform rect = selectable.transform as RectTransform;
        Vector3[] corners = new Vector3[4];
        rect?.GetWorldCorners(corners);
        Camera camera = ResolveCanvasCamera(selectable.transform);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int index = 0; index < corners.Length; index++)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corners[index]);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        TMP_Text tmpText = selectable.GetComponentInChildren<TMP_Text>(includeInactive: true);
        Text legacyText = tmpText == null
            ? selectable.GetComponentInChildren<Text>(includeInactive: true)
            : null;
        return new AutomationUiControl
        {
            name = selectable.gameObject.name,
            type = selectable.GetType().Name,
            text = tmpText != null ? tmpText.text : legacyText != null ? legacyText.text : string.Empty,
            interactable = selectable.IsInteractable(),
            x = float.IsInfinity(min.x) ? 0f : min.x,
            y = float.IsInfinity(min.y) ? 0f : min.y,
            width = float.IsInfinity(min.x) ? 0f : Mathf.Max(0f, max.x - min.x),
            height = float.IsInfinity(min.y) ? 0f : Mathf.Max(0f, max.y - min.y)
        };
    }

    private static Camera ResolveCanvasCamera(Transform transform)
    {
        Canvas canvas = transform != null ? transform.GetComponentInParent<Canvas>() : null;
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
    }

    private static int ValueOrDefault(Data<int> value)
    {
        return value != null ? value.Value : 0;
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left ?? string.Empty);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right ?? string.Empty);
        int difference = leftBytes.Length ^ rightBytes.Length;
        int length = Math.Max(leftBytes.Length, rightBytes.Length);
        for (int index = 0; index < length; index++)
        {
            byte leftByte = index < leftBytes.Length ? leftBytes[index] : (byte)0;
            byte rightByte = index < rightBytes.Length ? rightBytes[index] : (byte)0;
            difference |= leftByte ^ rightByte;
        }

        return difference == 0;
    }
}

[Serializable]
internal sealed class AutomationRequest
{
    public string id = string.Empty;
    public string token = string.Empty;
    public string command = string.Empty;
    public string target = string.Empty;
    public string key = string.Empty;
    public string path = string.Empty;
    public float x;
    public float y;
    public float duration;
    public int button;
}

[Serializable]
internal sealed class AutomationResponse
{
    public string id = string.Empty;
    public bool ok;
    public string message = string.Empty;
    public string error = string.Empty;
    public string data = string.Empty;

    public static AutomationResponse Ok(string id, string message, string data = "")
    {
        return new AutomationResponse
        {
            id = id ?? string.Empty,
            ok = true,
            message = message ?? string.Empty,
            data = data ?? string.Empty
        };
    }

    public static AutomationResponse Fail(string id, string error)
    {
        return new AutomationResponse
        {
            id = id ?? string.Empty,
            ok = false,
            error = error ?? string.Empty
        };
    }
}

internal sealed class PendingAutomationRequest
{
    public PendingAutomationRequest(AutomationRequest request)
    {
        Request = request;
    }

    public AutomationRequest Request { get; }
    public ManualResetEventSlim Completed { get; } = new ManualResetEventSlim(false);
    public AutomationResponse Response { get; set; }
}

[Serializable]
internal sealed class AutomationConnectionInfo
{
    public string host;
    public int port;
    public string token;
    public int processId;
    public string product;
    public string identifier;
    public string startedUtc;
}

[Serializable]
internal sealed class AutomationGameStatus
{
    public string product;
    public string identifier;
    public string version;
    public string scene;
    public bool focused;
    public int screenWidth;
    public int screenHeight;
    public string fullScreenMode;
    public float timeScale;
    public int frame;
    public int day;
    public string phase;
    public string outcome;
    public string objective;
    public int money;
    public int gameSpeed;
    public int hour;
    public float cameraX;
    public float cameraY;
    public float cameraZ;
}

[Serializable]
internal sealed class AutomationUiControlList
{
    public AutomationUiControl[] controls;
}

[Serializable]
internal sealed class AutomationUiControl
{
    public string name;
    public string type;
    public string text;
    public bool interactable;
    public float x;
    public float y;
    public float width;
    public float height;
}

[Serializable]
internal sealed class AutomationCaptureResult
{
    public string path;
}
