using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer.Unity;

public interface IFacilityRelocationTargetingService
{
    bool IsTargeting { get; }
    void Begin(
        BuildableObject facility,
        Action<string> showFeedback,
        Action refresh);
    void Cancel();
}

public sealed class FacilityRelocationTargetSurface :
    MonoBehaviour,
    IPointerClickHandler,
    IPointerMoveHandler
{
    private Action<Vector2, PointerEventData.InputButton> click;
    private Action<Vector2> move;

    public void Initialize(
        Action<Vector2, PointerEventData.InputButton> click,
        Action<Vector2> move)
    {
        this.click = click;
        this.move = move;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        click?.Invoke(
            eventData?.position ?? Vector2.zero,
            eventData?.button ?? PointerEventData.InputButton.Left);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        move?.Invoke(eventData?.position ?? Vector2.zero);
    }
}

public sealed class FacilityRelocationTargetingService :
    IFacilityRelocationTargetingService,
    IStartable,
    ITickable,
    IDisposable
{
    private const int SortingOrder = 946;

    private readonly IDungeonUiCanvasProvider canvasProvider;
    private readonly IGridSystemProvider gridProvider;
    private readonly IMainCameraProvider cameraProvider;
    private readonly IFacilityEvolutionRuntime evolution;
    private readonly IFacilityRelocationWorldService relocationWorld;
    private readonly IPlayerInputReader input;

    private GameObject root;
    private GameObject shield;
    private SpriteRenderer preview;
    private Sprite previewSprite;
    private BuildableObject facility;
    private Action<string> showFeedback;
    private Action refresh;

    public FacilityRelocationTargetingService(
        IDungeonUiCanvasProvider canvasProvider,
        IGridSystemProvider gridProvider,
        IMainCameraProvider cameraProvider,
        IFacilityEvolutionRuntime evolution,
        IFacilityRelocationWorldService relocationWorld,
        IPlayerInputReader input)
    {
        this.canvasProvider = canvasProvider
            ?? throw new ArgumentNullException(nameof(canvasProvider));
        this.gridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        this.cameraProvider = cameraProvider
            ?? throw new ArgumentNullException(nameof(cameraProvider));
        this.evolution = evolution
            ?? throw new ArgumentNullException(nameof(evolution));
        this.relocationWorld = relocationWorld
            ?? throw new ArgumentNullException(nameof(relocationWorld));
        this.input = input
            ?? throw new ArgumentNullException(nameof(input));
    }

    public bool IsTargeting => facility != null && shield != null && shield.activeSelf;

    public void Start()
    {
        Canvas canvas = canvasProvider.GetOrCreateCanvas();
        root = new GameObject(
            "FacilityRelocationTargeting",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster));
        root.transform.SetParent(canvas.transform, false);
        Stretch(root.GetComponent<RectTransform>());
        Canvas overlay = root.GetComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingOrder = SortingOrder;

        shield = new GameObject(
            "FacilityRelocationTargetSurface",
            typeof(RectTransform),
            typeof(Image),
            typeof(FacilityRelocationTargetSurface));
        shield.transform.SetParent(root.transform, false);
        Stretch(shield.GetComponent<RectTransform>());
        Image image = shield.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.001f);
        image.raycastTarget = true;
        shield.GetComponent<FacilityRelocationTargetSurface>().Initialize(
            HandleClick,
            UpdatePreview);
        shield.SetActive(false);
    }

    public void Tick()
    {
        if (IsTargeting && input.GetKeyDown(KeyCode.Escape))
        {
            showFeedback?.Invoke("시설 이전을 취소했습니다.");
            Cancel();
        }
    }

    public void Begin(
        BuildableObject targetFacility,
        Action<string> feedback,
        Action refreshView)
    {
        if (targetFacility == null
            || targetFacility.isDestroy
            || targetFacility.Grid == null)
        {
            feedback?.Invoke("이전할 시설을 찾을 수 없습니다.");
            return;
        }

        facility = targetFacility;
        showFeedback = feedback;
        refresh = refreshView;
        shield.SetActive(true);
        shield.transform.SetAsLastSibling();
        EnsurePreview();
        UpdatePreview(input.MousePosition);
        showFeedback?.Invoke(
            "이전할 정확한 칸을 좌클릭하세요. 우클릭 또는 Esc로 취소합니다.");
    }

    public void Cancel()
    {
        facility = null;
        showFeedback = null;
        refresh = null;
        if (shield != null)
        {
            shield.SetActive(false);
        }

        if (preview != null)
        {
            preview.gameObject.SetActive(false);
        }
    }

    public void Dispose()
    {
        if (previewSprite != null)
        {
            UnityEngine.Object.Destroy(previewSprite.texture);
            UnityEngine.Object.Destroy(previewSprite);
        }

        if (preview != null)
        {
            UnityEngine.Object.Destroy(preview.gameObject);
        }

        if (root != null)
        {
            UnityEngine.Object.Destroy(root);
        }
    }

    private void HandleClick(
        Vector2 screenPosition,
        PointerEventData.InputButton button)
    {
        if (!IsTargeting)
        {
            return;
        }

        if (button == PointerEventData.InputButton.Right)
        {
            Action<string> feedback = showFeedback;
            Cancel();
            feedback?.Invoke("시설 이전을 취소했습니다.");
            return;
        }

        if (button != PointerEventData.InputButton.Left
            || !TryGetGridPosition(screenPosition, out Vector2Int destination))
        {
            return;
        }

        if (!evolution.TryQueueRelocation(
                facility,
                destination,
                out _,
                out string failureReason))
        {
            showFeedback?.Invoke(failureReason);
            UpdatePreview(screenPosition);
            return;
        }

        Action<string> feedbackCallback = showFeedback;
        Action refreshCallback = refresh;
        Cancel();
        feedbackCallback?.Invoke(
            $"시설 이전을 예약했습니다. 목적지 ({destination.x}, {destination.y})");
        refreshCallback?.Invoke();
    }

    private void UpdatePreview(Vector2 screenPosition)
    {
        if (!IsTargeting
            || !TryGetGridPosition(screenPosition, out Vector2Int destination))
        {
            if (preview != null)
            {
                preview.gameObject.SetActive(false);
            }

            return;
        }

        EnsurePreview();
        Grid grid = facility.Grid;
        IReadOnlyList<Vector2Int> cells = facility.BuildingData
            .GetGridPosList(destination);
        Vector3 center = cells
            .Select(grid.GetWorldPos)
            .Aggregate(Vector3.zero, (sum, value) => sum + value)
            / Mathf.Max(1, cells.Count);
        preview.transform.position = new Vector3(center.x, center.y + 0.5f, -1f);
        int width = Mathf.Max(1, cells.Max(cell => cell.x) - cells.Min(cell => cell.x) + 1);
        int height = Mathf.Max(1, cells.Max(cell => cell.y) - cells.Min(cell => cell.y) + 1);
        preview.transform.localScale = new Vector3(
            width,
            height * grid.CellWorldHeight,
            1f);
        bool valid = relocationWorld.CanRelocate(
            facility,
            destination,
            out _);
        preview.color = valid
            ? new Color(0.25f, 0.9f, 0.56f, 0.28f)
            : new Color(0.95f, 0.24f, 0.25f, 0.3f);
        preview.gameObject.SetActive(true);
    }

    private bool TryGetGridPosition(
        Vector2 screenPosition,
        out Vector2Int position)
    {
        position = default;
        Grid grid = facility?.Grid;
        Camera camera = cameraProvider.Camera;
        if (grid == null || camera == null)
        {
            return false;
        }

        Vector3 screen = new Vector3(
            screenPosition.x,
            screenPosition.y,
            Mathf.Abs(camera.transform.position.z - grid.OriginPosition.z));
        Vector3 world = camera.ScreenToWorldPoint(screen);
        position = grid.GetXY(world);
        return grid.IsValidGridPos(position);
    }

    private void EnsurePreview()
    {
        if (preview != null)
        {
            return;
        }

        GameObject previewObject = new GameObject(
            "FacilityRelocationPreview",
            typeof(SpriteRenderer));
        preview = previewObject.GetComponent<SpriteRenderer>();
        previewSprite = CreateWhiteSprite();
        preview.sprite = previewSprite;
        preview.sortingLayerName = "DungeonMiddleObject";
        preview.sortingOrder = 85;
        preview.gameObject.SetActive(false);
    }

    private static Sprite CreateWhiteSprite()
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "FacilityRelocationPreviewTexture";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        sprite.name = "FacilityRelocationPreviewSprite";
        return sprite;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
