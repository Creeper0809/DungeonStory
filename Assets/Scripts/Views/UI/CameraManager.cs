using System;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;
using PixelPerfectCamera = UnityEngine.Rendering.Universal.PixelPerfectCamera;

public class CameraManager : MonoBehaviour
{
    public float speed = 5.0f;
    public float edgeThreshold = 10.0f;
    public bool enableEdgeScroll = false;
    public bool enableZoom = true;
    public Vector2 movementPadding = new Vector2(4f, 1f);
    public Vector2 zoomBounds = new Vector2(3.25f, 10.5f);
    public float wheelZoomStep = 0.85f;
    public float keyboardZoomSpeed = 4.5f;
    public bool blockWheelZoomOverUi = true;
    public bool centerDungeonOnStart = true;
    public Vector2 maxBound;
    public Vector2 minBound;

    private IGridSystemProvider gridSystemProvider;
    private IMainCameraProvider mainCameraProvider;
    private IPlayerInputReader inputReader;
    private IUiPointerBlocker uiPointerBlocker;
    private IUiClock uiClock;
    private GridSystemManager gridSystemManager;
    private Camera targetCamera;
    private PixelPerfectCamera pixelPerfectCamera;
    private float height;
    private float width;
    private float userSpeedMultiplier = 1f;
    private DungeonCameraControlScheme controlScheme = DungeonCameraControlScheme.WasdAndArrows;

    [Inject]
    public void Construct(
        IGridSystemProvider gridSystemProvider,
        IMainCameraProvider mainCameraProvider,
        IPlayerInputReader inputReader,
        IUiPointerBlocker uiPointerBlocker,
        IUiClock uiClock)
    {
        this.gridSystemProvider = gridSystemProvider ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.mainCameraProvider = mainCameraProvider ?? throw new ArgumentNullException(nameof(mainCameraProvider));
        this.inputReader = inputReader ?? throw new ArgumentNullException(nameof(inputReader));
        this.uiPointerBlocker = uiPointerBlocker;
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        SubscribeToGridExpansionIfInjected();
    }

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        pixelPerfectCamera = GetComponent<PixelPerfectCamera>();
    }

    private void OnEnable()
    {
        SubscribeToGridExpansionIfInjected();
    }

    private void Start()
    {
        SubscribeToGridExpansionIfInjected();
        UpdateViewportSize();
        if (gridSystemProvider != null)
        {
            if (centerDungeonOnStart)
            {
                CenterOnDungeonInterior();
            }

            ClampToCurrentBounds();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromGridExpansion();
    }

    private void OnDestroy()
    {
        UnsubscribeFromGridExpansion();
    }

    private void Update()
    {
        UpdateViewportSize();

        if (inputReader == null)
        {
            return;
        }

        IPlayerInputReader input = inputReader;
        HandleZoomInput(input);
        UpdateViewportSize();

        bool useWasd = controlScheme != DungeonCameraControlScheme.ArrowsOnly;
        bool useArrows = controlScheme != DungeonCameraControlScheme.WasdOnly;
        float effectiveSpeed = speed * userSpeedMultiplier;
        float moveX = GetKeyboardAxis(
            useWasd && input.GetKey(KeyCode.A) || useArrows && input.GetKey(KeyCode.LeftArrow),
            useWasd && input.GetKey(KeyCode.D) || useArrows && input.GetKey(KeyCode.RightArrow)) * effectiveSpeed;
        float moveY = GetKeyboardAxis(
            useWasd && input.GetKey(KeyCode.S) || useArrows && input.GetKey(KeyCode.DownArrow),
            useWasd && input.GetKey(KeyCode.W) || useArrows && input.GetKey(KeyCode.UpArrow)) * effectiveSpeed;
        Vector3 mousePosition = input.MousePosition;

        if (enableEdgeScroll && Mathf.Approximately(moveX, 0f))
        {
            moveX = GetHorizontalEdgeAxis(input, mousePosition) * effectiveSpeed;
        }

        if (enableEdgeScroll && Mathf.Approximately(moveY, 0f))
        {
            moveY = GetVerticalEdgeAxis(input, mousePosition) * effectiveSpeed;
        }

        transform.Translate(new Vector3(moveX * uiClock.DeltaTime, moveY * uiClock.DeltaTime, 0f), Space.World);
        ClampToCurrentBounds();
    }

    public void ApplyZoomDelta(float zoomInAmount)
    {
        Camera camera = targetCamera != null ? targetCamera : ResolveMainCamera();
        if (camera == null || !camera.orthographic || Mathf.Approximately(zoomInAmount, 0f))
        {
            return;
        }

        float min = Mathf.Min(zoomBounds.x, zoomBounds.y);
        float max = Mathf.Max(zoomBounds.x, zoomBounds.y);
        float nextSize = Mathf.Clamp(camera.orthographicSize - zoomInAmount, min, max);
        PreparePixelPerfectCameraForZoom(nextSize);
        camera.orthographicSize = nextSize;
        UpdateViewportSize();
        if (gridSystemProvider != null)
        {
            ClampToCurrentBounds();
        }
    }

    private void PreparePixelPerfectCameraForZoom(float targetSize)
    {
        if (pixelPerfectCamera == null)
        {
            pixelPerfectCamera = GetComponent<PixelPerfectCamera>();
        }

        if (pixelPerfectCamera != null && pixelPerfectCamera.isActiveAndEnabled)
        {
            // Compatibility mode keeps pixel snapping active without restoring the
            // camera's orthographic size during PixelPerfectCamera.OnPreCull().
            pixelPerfectCamera.CorrectCinemachineOrthoSize(targetSize);
        }
    }

    public void ClampToCurrentBounds()
    {
        UpdateViewportSize();
        GetGridBounds(out Vector2 lowerBound, out Vector2 upperBound);

        float clampX = ClampAxis(transform.position.x, lowerBound.x, upperBound.x, width);
        float clampY = ClampAxis(transform.position.y, lowerBound.y, upperBound.y, height);
        transform.position = new Vector3(clampX, clampY, -10f);
    }

    public bool CenterOnDungeonInterior()
    {
        Grid grid = RequireGrid();
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        foreach (GridCell cell in grid.GetCells())
        {
            if (cell == null || cell.AreaType != GridCellAreaType.DungeonInterior)
            {
                continue;
            }

            float worldX = grid.GetWorldPos(cell.Position).x;
            minX = Mathf.Min(minX, worldX);
            maxX = Mathf.Max(maxX, worldX);
        }

        if (float.IsInfinity(minX) || float.IsInfinity(maxX))
        {
            return false;
        }

        transform.position = new Vector3((minX + maxX) * 0.5f, transform.position.y, -10f);
        return true;
    }

    public void ApplyUserPreferences(
        float speedMultiplier,
        bool edgeScrollEnabled,
        DungeonCameraControlScheme cameraControlScheme)
    {
        userSpeedMultiplier = Mathf.Clamp(speedMultiplier, 0.5f, 2f);
        enableEdgeScroll = edgeScrollEnabled;
        controlScheme = Enum.IsDefined(typeof(DungeonCameraControlScheme), cameraControlScheme)
            ? cameraControlScheme
            : DungeonCameraControlScheme.WasdAndArrows;
    }

    public void OnGridExpand()
    {
        ClampToCurrentBounds();
    }

    private void SubscribeToGridExpansionIfInjected()
    {
        if (gridSystemProvider != null && isActiveAndEnabled)
        {
            SubscribeToGridExpansion();
        }
    }

    private void SubscribeToGridExpansion()
    {
        if (!TryResolveGridSystemManager(out GridSystemManager manager))
        {
            return;
        }

        if (gridSystemManager == manager)
        {
            return;
        }

        UnsubscribeFromGridExpansion();
        gridSystemManager = manager;
        gridSystemManager.OnGridExpand += OnGridExpand;
    }

    private void UnsubscribeFromGridExpansion()
    {
        if (gridSystemManager == null)
        {
            return;
        }

        gridSystemManager.OnGridExpand -= OnGridExpand;
        gridSystemManager = null;
    }

    private IGridSystemProvider RequireGridSystemProvider()
    {
        return gridSystemProvider
            ?? throw new InvalidOperationException($"{nameof(CameraManager)} requires {nameof(IGridSystemProvider)} injection.");
    }

    private void UpdateViewportSize()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        Camera camera = targetCamera != null ? targetCamera : ResolveMainCamera();
        if (camera == null) return;

        height = camera.orthographicSize;
        width = camera.aspect * height;
    }

    private void GetGridBounds(out Vector2 lowerBound, out Vector2 upperBound)
    {
        Grid grid = RequireGrid();

        Vector3 firstCellCenter = grid.GetWorldPos(Vector2Int.zero);
        Vector3 lastCellCenter = grid.GetWorldPos(new Vector2Int(grid.width - 1, grid.height - 1));
        float minX = Mathf.Min(firstCellCenter.x, lastCellCenter.x) - 0.5f;
        float maxX = Mathf.Max(firstCellCenter.x, lastCellCenter.x) + 0.5f;
        float minY = Mathf.Min(firstCellCenter.y, lastCellCenter.y);
        float maxY = Mathf.Max(firstCellCenter.y, lastCellCenter.y) + 3f;

        lowerBound = new Vector2(minX - movementPadding.x, minY - movementPadding.y);
        upperBound = new Vector2(maxX + movementPadding.x, maxY + movementPadding.y);
    }

    private static float ClampAxis(float value, float min, float max, float halfViewSize)
    {
        float lower = Mathf.Min(min, max);
        float upper = Mathf.Max(min, max);
        if (upper - lower <= halfViewSize * 2f)
        {
            return (lower + upper) * 0.5f;
        }

        return Mathf.Clamp(value, lower + halfViewSize, upper - halfViewSize);
    }

    private static float GetKeyboardAxis(bool negativePressed, bool positivePressed)
    {
        if (negativePressed == positivePressed)
        {
            return 0f;
        }

        return negativePressed ? -1f : 1f;
    }

    private void HandleZoomInput(IPlayerInputReader input)
    {
        if (!enableZoom || input == null)
        {
            return;
        }

        float zoomInAmount = 0f;
        float rawScrollDelta = input.ScrollDeltaY;
        if (!Mathf.Approximately(rawScrollDelta, 0f) && !IsWheelZoomBlocked())
        {
            zoomInAmount += NormalizeScrollDelta(rawScrollDelta) * wheelZoomStep;
        }

        float keyboardAxis = GetKeyboardAxis(
            input.GetKey(KeyCode.Minus)
                || input.GetKey(KeyCode.KeypadMinus)
                || input.GetKey(KeyCode.PageDown),
            input.GetKey(KeyCode.Equals)
                || input.GetKey(KeyCode.KeypadPlus)
                || input.GetKey(KeyCode.PageUp));
        zoomInAmount += keyboardAxis * keyboardZoomSpeed * uiClock.DeltaTime;
        ApplyZoomDelta(zoomInAmount);
    }

    private bool IsWheelZoomBlocked()
    {
        if (!blockWheelZoomOverUi || uiPointerBlocker == null)
        {
            return false;
        }

        return uiPointerBlocker is EventSystemUiPointerBlocker eventSystemBlocker
            ? eventSystemBlocker.IsPointerOverScrollableUi()
            : uiPointerBlocker.IsPointerOverUi();
    }

    private static float NormalizeScrollDelta(float rawDelta)
    {
        if (Mathf.Approximately(rawDelta, 0f))
        {
            return 0f;
        }

        return Mathf.Abs(rawDelta) > 10f
            ? rawDelta / 120f
            : rawDelta;
    }

    private float GetHorizontalEdgeAxis(IPlayerInputReader input, Vector3 mousePosition)
    {
        if (!IsUsableEdgeScrollPointer(input, mousePosition))
        {
            return 0f;
        }

        if (mousePosition.x < edgeThreshold)
        {
            return -1f;
        }

        return mousePosition.x > input.ScreenWidth - edgeThreshold ? 1f : 0f;
    }

    private float GetVerticalEdgeAxis(IPlayerInputReader input, Vector3 mousePosition)
    {
        if (!IsUsableEdgeScrollPointer(input, mousePosition))
        {
            return 0f;
        }

        if (mousePosition.y < edgeThreshold)
        {
            return -1f;
        }

        return mousePosition.y > input.ScreenHeight - edgeThreshold ? 1f : 0f;
    }

    private static bool IsUsableEdgeScrollPointer(IPlayerInputReader input, Vector3 mousePosition)
    {
        return Application.isFocused
            && mousePosition.x > 0f
            && mousePosition.y > 0f
            && mousePosition.x < input.ScreenWidth - 1f
            && mousePosition.y < input.ScreenHeight - 1f;
    }

    private IMainCameraProvider RequireMainCameraProvider()
    {
        return mainCameraProvider
            ?? throw new InvalidOperationException($"{nameof(CameraManager)} requires {nameof(IMainCameraProvider)} injection.");
    }

    private IPlayerInputReader RequireInputReader()
    {
        return inputReader
            ?? throw new InvalidOperationException($"{nameof(CameraManager)} requires {nameof(IPlayerInputReader)} injection.");
    }

    private Camera ResolveMainCamera()
    {
        if (mainCameraProvider == null)
        {
            return targetCamera;
        }

        Camera camera = mainCameraProvider.Camera;
        return camera != null
            ? camera
            : throw new InvalidOperationException($"{nameof(IMainCameraProvider)} returned no camera.");
    }

    private Grid RequireGrid()
    {
        return RequireGridSystemProvider().Grid;
    }

    private bool TryResolveGridSystemManager(out GridSystemManager manager)
    {
        manager = null;
        return gridSystemProvider != null
            && gridSystemProvider.TryGetManager(out manager);
    }
}
