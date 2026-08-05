using System;
using System.Collections;
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

internal static class DungeonPlayerAutomationSceneBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void LoadRequestedAutomationScene()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        if (!arguments.Contains("-automation", StringComparer.Ordinal))
        {
            return;
        }

        int sceneArgument = Array.FindIndex(arguments, value =>
            string.Equals(
                value,
                "-automation-scene",
                StringComparison.Ordinal));
        if (sceneArgument < 0 || sceneArgument + 1 >= arguments.Length)
        {
            return;
        }

        string target = arguments[sceneArgument + 1]?.Trim()
            ?? string.Empty;
        if (!IsAllowedScene(target))
        {
            Debug.LogError(
                $"Player automation rejected unknown scene '{target}'.");
            return;
        }

        if (!string.Equals(
                SceneManager.GetActiveScene().name,
                target,
                StringComparison.Ordinal))
        {
            SceneManager.LoadScene(target);
        }
    }

    private static bool IsAllowedScene(string sceneName)
    {
        return sceneName is "TitleScene"
            or "StartPreparationScene"
            or "GameplayScene";
    }
}

public interface IDungeonAutomationInputReader
{
    int FrameCount { get; }
    bool TryGetPointerPosition(out Vector3 position);
    bool TryConsumeScrollDeltaY(out float deltaY);
    bool GetMouseButtonDown(int button);
    bool GetMouseButton(int button);
    bool GetKey(KeyCode key);
    bool GetKeyDown(KeyCode key);
}

public interface IDungeonAutomationInputControl : IDungeonAutomationInputReader
{
    void Enable();
    void Disable();
    void MovePointer(Vector2 position);
    int ClickPointer(int button);
    void Scroll(float deltaY);
    bool HoldKey(KeyCode key, float durationSeconds);
    void ReleaseKey(KeyCode key);
}

public sealed class DungeonAutomationInputState :
    IDungeonAutomationInputControl,
    IDisposable
{
    private readonly Dictionary<KeyCode, double> heldKeys =
        new Dictionary<KeyCode, double>();
    private readonly Dictionary<KeyCode, int> keyDownFrames =
        new Dictionary<KeyCode, int>();
    private readonly int[] mouseDownFrames = { -1, -1, -1 };
    private readonly int[] mouseHeldUntilFrames = { -1, -1, -1 };
    private readonly IGameClock gameClock;
    private readonly IUiClock uiClock;

    private bool enabled;
    private bool pointerOverridden;
    private Vector3 pointerPosition;
    private float scrollDeltaY;

    public DungeonAutomationInputState(
        IGameClock gameClock,
        IUiClock uiClock)
    {
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        Disable();
    }

    public void Enable()
    {
        enabled = true;
    }

    public void Disable()
    {
        enabled = false;
        pointerOverridden = false;
        pointerPosition = Vector3.zero;
        scrollDeltaY = 0f;
        heldKeys.Clear();
        keyDownFrames.Clear();
        for (int index = 0; index < mouseDownFrames.Length; index++)
        {
            mouseDownFrames[index] = -1;
            mouseHeldUntilFrames[index] = -1;
        }
    }

    public bool TryGetPointerPosition(out Vector3 position)
    {
        position = pointerPosition;
        return enabled && pointerOverridden;
    }

    public void MovePointer(Vector2 position)
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

    public int ClickPointer(int button)
    {
        if (!enabled || button < 0 || button >= mouseDownFrames.Length)
        {
            return -1;
        }

        int frame = CurrentFrame;
        if (frame < 0)
        {
            return -1;
        }

        int downFrame = frame + 1;
        mouseDownFrames[button] = downFrame;
        mouseHeldUntilFrames[button] = downFrame + 1;
        return downFrame;
    }

    public void Scroll(float deltaY)
    {
        if (!enabled || Mathf.Approximately(deltaY, 0f))
        {
            return;
        }

        scrollDeltaY += deltaY;
    }

    public bool TryConsumeScrollDeltaY(out float deltaY)
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

    public bool GetMouseButtonDown(int button)
    {
        int frame = CurrentFrame;
        return enabled
            && frame >= 0
            && button >= 0
            && button < mouseDownFrames.Length
            && mouseDownFrames[button] == frame;
    }

    public bool GetMouseButton(int button)
    {
        int frame = CurrentFrame;
        return enabled
            && frame >= 0
            && button >= 0
            && button < mouseHeldUntilFrames.Length
            && frame <= mouseHeldUntilFrames[button];
    }

    public bool HoldKey(KeyCode key, float durationSeconds)
    {
        int frame = CurrentFrame;
        if (!enabled || key == KeyCode.None || frame < 0)
        {
            return false;
        }

        heldKeys[key] = CurrentRealtime + Mathf.Clamp(durationSeconds, 0.05f, 30f);
        keyDownFrames[key] = frame + 1;
        return true;
    }

    public void ReleaseKey(KeyCode key)
    {
        heldKeys.Remove(key);
        keyDownFrames.Remove(key);
    }

    public bool GetKey(KeyCode key)
    {
        if (!enabled || !heldKeys.TryGetValue(key, out double expiresAt))
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

    public bool GetKeyDown(KeyCode key)
    {
        int currentFrame = CurrentFrame;
        return enabled
            && currentFrame >= 0
            && keyDownFrames.TryGetValue(key, out int frame)
            && frame == currentFrame;
    }

    public void Dispose() => Disable();

    public int FrameCount => CurrentFrame;
    private int CurrentFrame => gameClock.FrameCount;
    private double CurrentRealtime => uiClock.Time;
}

public sealed class DungeonPlayerAutomationBridge : IStartable, IDisposable
{
    private readonly IDungeonRunFlowRuntime runFlow;
    private readonly IFirstRunObjectiveRuntime firstRunObjective;
    private readonly IGameSessionStateProvider gameDataProvider;
    private readonly DungeonUserSettingsRuntimeTargets userSettingsTargets;
    private readonly IDungeonUiCanvasProvider canvasProvider;
    private readonly IMainCameraProvider mainCameraProvider;
    private readonly IDungeonAutomationInputControl automationInput;
    private readonly IGameTimeScaleController timeScaleController;

    private DungeonPlayerAutomationHost host;

    public DungeonPlayerAutomationBridge(
        IDungeonRunFlowRuntime runFlow,
        IFirstRunObjectiveRuntime firstRunObjective,
        IGameSessionStateProvider gameDataProvider,
        DungeonUserSettingsRuntimeTargets userSettingsTargets,
        IDungeonUiCanvasProvider canvasProvider,
        IMainCameraProvider mainCameraProvider,
        IDungeonAutomationInputControl automationInput,
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
        this.automationInput = automationInput
            ?? throw new ArgumentNullException(nameof(automationInput));
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
            automationInput,
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
                continue;
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
    private IGameSessionStateProvider gameDataProvider;
    private DungeonUserSettingsRuntimeTargets userSettingsTargets;
    private IDungeonUiCanvasProvider canvasProvider;
    private IMainCameraProvider mainCameraProvider;
    private IDungeonAutomationInputControl automationInput;
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
        IGameSessionStateProvider dataProvider,
        DungeonUserSettingsRuntimeTargets settingsTargets,
        IDungeonUiCanvasProvider uiCanvasProvider,
        IMainCameraProvider cameraProvider,
        IDungeonAutomationInputControl inputControl,
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
        automationInput = inputControl
            ?? throw new ArgumentNullException(nameof(inputControl));
        timeScaleController = scaleController
            ?? throw new ArgumentNullException(nameof(scaleController));

        automationDirectory = Path.Combine(Application.persistentDataPath, "Automation");
        connectionPath = Path.Combine(automationDirectory, "bridge.json");
        Directory.CreateDirectory(automationDirectory);
        automationInput.Enable();
        StartServer();
    }

    private void Update()
    {
        for (int index = 0; index < MaxRequestsPerFrame && requests.TryDequeue(out PendingAutomationRequest pending); index++)
        {
            if (IsScreenCaptureRequest(pending.Request))
            {
                StartCoroutine(CompleteScreenCaptureAtEndOfFrame(pending));
                continue;
            }

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

    private IEnumerator CompleteScreenCaptureAtEndOfFrame(
        PendingAutomationRequest pending)
    {
        yield return new WaitForEndOfFrame();
        try
        {
            pending.Response = CaptureScreen(pending.Request);
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

    private void OnDestroy()
    {
        Shutdown();
    }

    public void Shutdown()
    {
        if (!running && listener == null)
        {
            automationInput?.Disable();
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
        automationInput?.Disable();
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
            automationInput?.Disable();
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
        GameSessionState gameData = null;
        gameDataProvider.TryGetSessionState(out gameData);
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
            frame = automationInput.FrameCount,
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
        IReadOnlyDictionary<Selectable, string> labels = canvas != null
            ? CollectSelectableLabels(canvas)
            : new Dictionary<Selectable, string>();
        List<AutomationUiControl> controls = new List<AutomationUiControl>();
        foreach (Selectable selectable in selectables.OrderBy(item => item.gameObject.name, StringComparer.Ordinal))
        {
            if (selectable == null || !selectable.gameObject.activeInHierarchy)
            {
                continue;
            }

            labels.TryGetValue(selectable, out string label);
            controls.Add(CreateUiControl(selectable, label));
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
        automationInput.MovePointer(center);
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
        automationInput.MovePointer(new Vector2(request.x, request.y));
        return AutomationResponse.Ok(request.id, $"Pointer moved to {request.x:0.##},{request.y:0.##}");
    }

    private AutomationResponse ClickPointer(AutomationRequest request)
    {
        automationInput.MovePointer(new Vector2(request.x, request.y));
        int frame = automationInput.ClickPointer(Mathf.Clamp(request.button, 0, 2));
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
        return automationInput.HoldKey(key, duration)
            ? AutomationResponse.Ok(request.id, $"Holding {key} for {duration:0.##}s")
            : AutomationResponse.Fail(request.id, "Key could not be held: " + key);
    }

    private AutomationResponse ReleaseKey(AutomationRequest request)
    {
        if (!Enum.TryParse(request.key, true, out KeyCode key) || key == KeyCode.None)
        {
            return AutomationResponse.Fail(request.id, "Unknown KeyCode: " + request.key);
        }

        automationInput.ReleaseKey(key);
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
        Texture2D capturedFrame = CaptureRenderedFrame();
        if (capturedFrame == null)
        {
            return AutomationResponse.Fail(
                request.id,
                "Rendered frame capture returned no texture.");
        }

        try
        {
            File.WriteAllBytes(capturePath, capturedFrame.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.Destroy(capturedFrame);
        }

        AutomationCaptureResult result = new AutomationCaptureResult { path = capturePath };
        return AutomationResponse.Ok(request.id, "Screenshot captured", JsonUtility.ToJson(result));
    }

    private Texture2D CaptureRenderedFrame()
    {
        Camera captureCamera = mainCameraProvider.Camera;
        if (captureCamera == null)
        {
            return null;
        }

        int width = Mathf.Max(1, Screen.width);
        int height = Mathf.Max(1, Screen.height);
        RenderTexture target = RenderTexture.GetTemporary(
            width,
            height,
            24,
            RenderTextureFormat.ARGB32);
        RenderTexture previousTarget = captureCamera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        int previousCullingMask = captureCamera.cullingMask;
        GameObject uiCameraObject = new GameObject("DungeonAutomationCaptureUiCamera")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        Camera uiCamera = uiCameraObject.AddComponent<Camera>();
        uiCamera.enabled = false;
        uiCamera.orthographic = true;
        uiCamera.orthographicSize = height * 0.5f;
        uiCamera.nearClipPlane = 0.01f;
        uiCamera.farClipPlane = 100f;
        uiCamera.clearFlags = CameraClearFlags.Depth;
        uiCamera.cullingMask = 1 << 5;
        uiCamera.targetTexture = target;
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        RenderMode[] renderModes = new RenderMode[canvases.Length];
        Camera[] worldCameras = new Camera[canvases.Length];
        float[] planeDistances = new float[canvases.Length];
        Texture2D captured = null;
        try
        {
            for (int index = 0; index < canvases.Length; index++)
            {
                Canvas canvas = canvases[index];
                renderModes[index] = canvas.renderMode;
                worldCameras[index] = canvas.worldCamera;
                planeDistances[index] = canvas.planeDistance;
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = uiCamera;
                    canvas.planeDistance = 1f;
                }
            }

            Canvas.ForceUpdateCanvases();
            captureCamera.cullingMask = previousCullingMask & ~(1 << 5);
            captureCamera.targetTexture = target;
            captureCamera.Render();
            uiCamera.Render();
            RenderTexture.active = target;
            captured = new Texture2D(width, height, TextureFormat.RGB24, false);
            captured.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
            captured.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return captured;
        }
        catch
        {
            if (captured != null)
            {
                UnityEngine.Object.Destroy(captured);
            }

            throw;
        }
        finally
        {
            captureCamera.targetTexture = previousTarget;
            captureCamera.cullingMask = previousCullingMask;
            RenderTexture.active = previousActive;
            for (int index = 0; index < canvases.Length; index++)
            {
                Canvas canvas = canvases[index];
                if (canvas == null)
                {
                    continue;
                }

                canvas.renderMode = renderModes[index];
                canvas.worldCamera = worldCameras[index];
                canvas.planeDistance = planeDistances[index];
            }

            Canvas.ForceUpdateCanvases();
            UnityEngine.Object.Destroy(uiCameraObject);
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static bool IsScreenCaptureRequest(AutomationRequest request)
    {
        return request != null
            && string.Equals(
                request.command?.Trim(),
                "capture.screen",
                StringComparison.OrdinalIgnoreCase);
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

    private static IReadOnlyDictionary<Selectable, string> CollectSelectableLabels(
        Canvas canvas)
    {
        Dictionary<Selectable, string> labels = new Dictionary<Selectable, string>();
        foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(includeInactive: false))
        {
            AddSelectableLabel(labels, text, text != null ? text.text : string.Empty);
        }

        foreach (Text text in canvas.GetComponentsInChildren<Text>(includeInactive: false))
        {
            AddSelectableLabel(labels, text, text != null ? text.text : string.Empty);
        }

        return labels;
    }

    private static void AddSelectableLabel(
        IDictionary<Selectable, string> labels,
        Component labelComponent,
        string label)
    {
        if (labelComponent == null || string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        Selectable owner = labelComponent.GetComponentInParent<Selectable>();
        if (owner != null && !labels.ContainsKey(owner))
        {
            labels.Add(owner, label);
        }
    }

    private static AutomationUiControl CreateUiControl(
        Selectable selectable,
        string label)
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

        return new AutomationUiControl
        {
            name = selectable.gameObject.name,
            type = selectable.GetType().Name,
            text = label ?? string.Empty,
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
