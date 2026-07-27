using TMPro;
using DungeonStory.Foundation;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldCharacterNameplate : MonoBehaviour
{
    private const string RootName = "WorldNameplate";
    private const string DefaultSortingLayerName = "Default";
    private const string OverlaySortingLayerName = "UI";

    [SerializeField] private Vector2 plateSize = new Vector2(2.05f, 0.14f);
    [SerializeField] private float belowFeetOffset = 0.18f;
    [SerializeField] private float textFontSize = 2.8f;
    [SerializeField] private int sortingOrderOffset = 36;
    [SerializeField] private Color fullHealthColor = new Color(0.31f, 0.86f, 0.55f, 0.78f);
    [SerializeField] private Color woundedHealthColor = new Color(1f, 0.75f, 0.25f, 0.82f);
    [SerializeField] private Color criticalHealthColor = new Color(1f, 0.28f, 0.25f, 0.88f);

    private static Material sharedLineMaterial;

    private CharacterActor actor;
    private CharacterIdentity identity;
    private CharacterStats stats;
    private Transform root;
    private LineRenderer backgroundLine;
    private LineRenderer healthLine;
    private TextMeshPro nameText;
    private TextMeshPro warningText;
    private string cachedDisplayName;
    private float cachedHealth01 = -1f;
    private float cachedCurrentHealth = -1f;
    private float cachedMaxHealth = -1f;
    private string cachedSortingLayerName;
    private int cachedSortingOrder = int.MinValue;
    private bool combatActive;
    private bool commandSelected;
    private float combatHealthVisibleUntil;
    private float combatHorizontalOffset;
    private IMainCameraProvider mainCameraProvider;
    private IGameClock gameClock;
    private IDynamicFrameWorkBudget frameWorkBudget;
    private ITmpKoreanFontService tmpKoreanFontService;

    public static WorldCharacterNameplate Ensure(
        CharacterActor owner,
        ITmpKoreanFontService tmpKoreanFontService = null)
    {
        if (owner == null)
        {
            return null;
        }

        if (!owner.TryGetComponent(out WorldCharacterNameplate nameplate))
        {
            nameplate = owner.gameObject.AddComponent<WorldCharacterNameplate>();
        }

        nameplate.Bind(
            owner,
            owner.MainCameraProvider,
            tmpKoreanFontService ?? owner.TmpKoreanFontService);
        return nameplate;
    }

    private void Awake()
    {
        Bind(GetComponent<CharacterActor>());
    }

    private void OnEnable()
    {
        Refresh(force: true);
    }

    internal void TickFromScheduler(bool isVisible, bool force)
    {
        if (!isVisible)
        {
            SetRootActive(false);
            return;
        }

        Refresh(force, visibilityOverride: true);
    }

    internal void HideFromScheduler()
    {
        SetRootActive(false);
    }

    public void Bind(CharacterActor owner)
    {
        Bind(
            owner,
            owner != null ? owner.MainCameraProvider : null,
            owner != null ? owner.TmpKoreanFontService : null);
    }

    public void Bind(CharacterActor owner, IMainCameraProvider cameraProvider)
    {
        Bind(
            owner,
            cameraProvider,
            owner != null ? owner.TmpKoreanFontService : null);
    }

    public void Bind(
        CharacterActor owner,
        IMainCameraProvider cameraProvider,
        ITmpKoreanFontService fontService)
    {
        actor = owner;
        identity = owner != null ? owner.Identity : null;
        stats = owner != null ? owner.Stats : null;
        mainCameraProvider = cameraProvider;
        gameClock = owner != null ? owner.GameClock : null;
        frameWorkBudget = owner != null ? owner.FrameWorkBudget : null;
        tmpKoreanFontService = fontService;
        Refresh(force: true);
    }

    public void SetCombatActive(bool active)
    {
        combatActive = active;
        if (!active)
        {
            combatHorizontalOffset = 0f;
        }
        if (active)
        {
            combatHealthVisibleUntil = Mathf.Max(combatHealthVisibleUntil, GameTime + 0.5f);
        }

        Refresh(force: true);
    }

    public void SetCommandSelected(bool selected)
    {
        commandSelected = selected;
        RefreshName(force: true);
    }

    public void SetCombatHorizontalOffset(float localX)
    {
        combatHorizontalOffset = Mathf.Clamp(localX, -0.4f, 0.4f);
        UpdatePosition();
    }

    public void RevealHealthForCombat(float seconds)
    {
        combatHealthVisibleUntil = Mathf.Max(
            combatHealthVisibleUntil,
            GameTime + Mathf.Max(0f, seconds));
        Refresh(force: true);
    }

#if UNITY_EDITOR
    public void RefreshNowForDebug()
    {
        Refresh(force: true, visibilityOverride: true);
    }
#endif

    private void Refresh(bool force)
    {
        Refresh(force, visibilityOverride: null);
    }

    private void Refresh(bool force, bool? visibilityOverride)
    {
        if (actor == null)
        {
            actor = GetComponent<CharacterActor>();
        }

        if (actor == null)
        {
            SetRootActive(false);
            return;
        }

        if (identity == null)
        {
            identity = actor.Identity;
        }

        if (stats == null)
        {
            stats = actor.Stats;
        }

        bool shouldShow = actor.CurrentLifecycleState != CharacterLifecycleState.OnExpedition
            && actor.CurrentLifecycleState != CharacterLifecycleState.Despawned
            && !actor.IsDead
            && (visibilityOverride ?? IsWithinCameraViewport());
        SetRootActive(shouldShow);
        if (!shouldShow)
        {
            return;
        }

        EnsureView();
        SetRootActive(true);
        UpdatePosition();
        SyncSorting(force);
        RefreshName(force);
        RefreshHealth(force);
        RefreshDeprivationWarning();
        ClampWarningToCamera();
    }

    private void EnsureView()
    {
        if (root != null && nameText != null && warningText != null && backgroundLine != null && healthLine != null)
        {
            return;
        }

        root = transform.Find(RootName);
        if (root == null)
        {
            GameObject rootObject = new GameObject(RootName);
            root = rootObject.transform;
            root.SetParent(transform, false);
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
        }

        backgroundLine = EnsureLine("HealthBackground", new Color(0.02f, 0.04f, 0.05f, 0.76f));
        healthLine = EnsureLine("HealthFill", fullHealthColor);
        nameText = EnsureText();
        warningText = EnsureWarningText();
    }

    private LineRenderer EnsureLine(string objectName, Color color)
    {
        Transform child = root.Find(objectName);
        if (child == null)
        {
            child = new GameObject(objectName).transform;
            child.SetParent(root, false);
        }

        if (!child.TryGetComponent(out LineRenderer line))
        {
            line = child.gameObject.AddComponent<LineRenderer>();
        }

        line.useWorldSpace = false;
        line.positionCount = 2;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 0;
        line.numCornerVertices = 0;
        line.widthMultiplier = Mathf.Max(0.02f, plateSize.y);
        Material material = ResolveLineMaterial();
        if (material != null)
        {
            line.sharedMaterial = material;
        }

        line.startColor = color;
        line.endColor = color;
        return line;
    }

    private TextMeshPro EnsureText()
    {
        Transform child = root.Find("Name");
        if (child == null)
        {
            child = new GameObject("Name").transform;
            child.SetParent(root, false);
        }

        if (!child.TryGetComponent(out TextMeshPro text))
        {
            text = child.gameObject.AddComponent<TextMeshPro>();
        }

        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = textFontSize;
        text.color = new Color(0.94f, 0.98f, 0.95f, 1f);
        ApplyKoreanFont(text);
        if (Application.isPlaying && text.fontSharedMaterial != null)
        {
            text.outlineColor = new Color(0.01f, 0.02f, 0.025f, 0.92f);
            text.outlineWidth = 0.18f;
        }
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = false;
        text.raycastTarget = false;
        text.transform.localPosition = new Vector3(0f, 0.005f, 0f);
        return text;
    }

    private TextMeshPro EnsureWarningText()
    {
        Transform child = root.Find("DeprivationWarning");
        if (child == null)
        {
            child = new GameObject("DeprivationWarning").transform;
            child.SetParent(root, false);
        }

        if (!child.TryGetComponent(out TextMeshPro text))
        {
            text = child.gameObject.AddComponent<TextMeshPro>();
        }

        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = Mathf.Max(2.6f, textFontSize * 0.95f);
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(1f, 0.38f, 0.24f, 1f);
        ApplyKoreanFont(text);
        if (Application.isPlaying && text.fontSharedMaterial != null)
        {
            text.outlineColor = new Color(0.03f, 0.01f, 0.01f, 0.96f);
            text.outlineWidth = 0.2f;
        }
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.gameObject.SetActive(false);
        return text;
    }

    private void UpdatePosition()
    {
        if (root == null)
        {
            return;
        }

        float bottomLocalY = 0f;
        SpriteRenderer renderer = actor != null ? actor.VisualRenderer : null;
        if (renderer != null)
        {
            Bounds bounds = renderer.bounds;
            Vector3 bottomWorld = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bottomLocalY = transform.InverseTransformPoint(bottomWorld).y;
        }

        root.localPosition = new Vector3(combatHorizontalOffset, bottomLocalY - belowFeetOffset, 0f);
        if (warningText != null)
        {
            float visualHeight = renderer != null
                ? Mathf.Max(0.6f, renderer.bounds.size.y / Mathf.Max(0.01f, transform.lossyScale.y))
                : 1.2f;
            warningText.transform.localPosition = new Vector3(0f, visualHeight + belowFeetOffset * 2.2f, 0f);
        }
    }

    private void RefreshName(bool force)
    {
        if (nameText == null)
        {
            return;
        }

        nameText.color = commandSelected
            ? new Color(0.35f, 1f, 0.72f, 1f)
            : new Color(0.94f, 0.98f, 0.95f, 1f);
        string displayName = actor != null && actor.TryGetComponent(out InvasionIntruderRuntime _)
            ? "침입자"
            : identity != null
                ? identity.DisplayName
                : actor != null
                    ? actor.name
                    : name;
        if (!force && string.Equals(cachedDisplayName, displayName, System.StringComparison.Ordinal))
        {
            return;
        }

        cachedDisplayName = displayName;
        nameText.text = string.IsNullOrWhiteSpace(displayName) ? "Unknown" : displayName;
    }

    private void RefreshHealth(bool force)
    {
        if (healthLine == null || backgroundLine == null)
        {
            return;
        }

        float maximum = Mathf.Max(1f, stats != null ? stats.MaxHealth : actor != null ? actor.MaxHealth : 1f);
        float current = Mathf.Clamp(stats != null ? stats.CurrentHealth : actor != null ? actor.CurrentHealth : maximum, 0f, maximum);
        float health01 = Mathf.Clamp01(current / maximum);
        bool visible = current < maximum - 0.01f
            || combatActive
            || GameTime < combatHealthVisibleUntil;
        SetHealthLinesActive(visible);
        if (!visible)
        {
            cachedHealth01 = health01;
            cachedCurrentHealth = current;
            cachedMaxHealth = maximum;
            return;
        }

        if (!force
            && Mathf.Approximately(cachedHealth01, health01)
            && Mathf.Approximately(cachedCurrentHealth, current)
            && Mathf.Approximately(cachedMaxHealth, maximum))
        {
            return;
        }

        cachedHealth01 = health01;
        cachedCurrentHealth = current;
        cachedMaxHealth = maximum;

        SetLineSpan(backgroundLine, -plateSize.x * 0.5f, plateSize.x * 0.5f);
        float fillRight = Mathf.Lerp(-plateSize.x * 0.5f, plateSize.x * 0.5f, health01);
        SetLineSpan(healthLine, -plateSize.x * 0.5f, fillRight);

        Color color = health01 > 0.6f
            ? fullHealthColor
            : health01 > 0.3f ? woundedHealthColor : criticalHealthColor;
        healthLine.startColor = color;
        healthLine.endColor = color;
    }

    private float GameTime => gameClock?.Time ?? actor?.GameClock?.Time ?? 0f;

    private void SetHealthLinesActive(bool visible)
    {
        if (backgroundLine != null && backgroundLine.gameObject.activeSelf != visible)
        {
            backgroundLine.gameObject.SetActive(visible);
        }

        if (healthLine != null && healthLine.gameObject.activeSelf != visible)
        {
            healthLine.gameObject.SetActive(visible);
        }
    }

    private void RefreshDeprivationWarning()
    {
        if (warningText == null)
        {
            return;
        }

        if (actor?.DeprivationRuntime == null
            || !actor.DeprivationRuntime.TryGetDisplayState(
                actor,
                out CharacterDeprivationDisplayState displayState)
            || (displayState.HighestBurden < 40f && !displayState.BreakdownActive))
        {
            warningText.gameObject.SetActive(false);
            return;
        }

        warningText.gameObject.SetActive(true);
        if (displayState.BreakdownActive)
        {
            warningText.text = displayState.BreakdownKind switch
            {
                CharacterBreakdownKind.DesperateRelief => "배변 통제 상실",
                CharacterBreakdownKind.DesperateDrink => "절박한 갈증",
                CharacterBreakdownKind.DesperateEat => "금기 포식",
                CharacterBreakdownKind.Collapse => "실신",
                _ => "폭력 충동"
            };
            warningText.color = new Color(1f, 0.16f, 0.12f, 1f);
        }
        else if (displayState.HighestBurden >= 70f)
        {
            warningText.text = "붕괴 위험";
            warningText.color = new Color(1f, 0.28f, 0.18f, 1f);
        }
        else
        {
            warningText.text = "건강 악화";
            warningText.color = new Color(1f, 0.72f, 0.2f, 1f);
        }
    }

    private void ClampWarningToCamera()
    {
        if (warningText == null || !warningText.gameObject.activeInHierarchy)
        {
            return;
        }

        Camera camera = null;
        try
        {
            camera = mainCameraProvider != null ? mainCameraProvider.Camera : null;
        }
        catch (System.InvalidOperationException)
        {
            // Scene services can release the registered camera before actors finish disabling.
        }

        if (camera == null || !camera.orthographic || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        float pixelsPerWorldUnit = Screen.height / Mathf.Max(0.01f, camera.orthographicSize * 2f);
        float worldWidth = Mathf.Max(0.5f, warningText.preferredWidth * warningText.transform.lossyScale.x);
        float halfPixels = worldWidth * pixelsPerWorldUnit * 0.5f;
        float margin = 8f;
        Vector3 screen = camera.WorldToScreenPoint(warningText.transform.position);
        float minimum = margin + halfPixels;
        float maximum = Screen.width - margin - halfPixels;
        if (maximum <= minimum)
        {
            return;
        }

        float clamped = Mathf.Clamp(screen.x, minimum, maximum);
        float worldDelta = (clamped - screen.x) / pixelsPerWorldUnit;
        float parentScaleX = warningText.transform.parent != null
            ? Mathf.Max(0.01f, Mathf.Abs(warningText.transform.parent.lossyScale.x))
            : 1f;
        Vector3 local = warningText.transform.localPosition;
        local.x += worldDelta / parentScaleX;
        warningText.transform.localPosition = local;
    }

    private bool IsWithinCameraViewport()
    {
        Camera camera = null;
        try
        {
            camera = mainCameraProvider != null ? mainCameraProvider.Camera : null;
        }
        catch (System.InvalidOperationException)
        {
            return false;
        }

        if (camera == null)
        {
            return false;
        }

        Vector3 viewport = camera.WorldToViewportPoint(transform.position);
        const float margin = 0.08f;
        return viewport.z >= 0f
            && viewport.x >= -margin
            && viewport.x <= 1f + margin
            && viewport.y >= -margin
            && viewport.y <= 1f + margin;
    }

    private void SyncSorting(bool force)
    {
        string layerName = OverlaySortingLayerName;
        int order = sortingOrderOffset;
        SpriteRenderer renderer = actor != null ? actor.VisualRenderer : null;
        if (renderer != null)
        {
            order = renderer.sortingOrder + sortingOrderOffset;
        }

        if (!force
            && cachedSortingOrder == order
            && string.Equals(cachedSortingLayerName, layerName, System.StringComparison.Ordinal))
        {
            return;
        }

        cachedSortingLayerName = layerName;
        cachedSortingOrder = order;
        ApplySorting(backgroundLine, layerName, order);
        ApplySorting(healthLine, layerName, order + 1);
        MeshRenderer textRenderer = nameText != null ? nameText.GetComponent<MeshRenderer>() : null;
        if (textRenderer != null)
        {
            textRenderer.sortingLayerName = layerName;
            textRenderer.sortingOrder = order + 2;
        }
        MeshRenderer warningRenderer = warningText != null ? warningText.GetComponent<MeshRenderer>() : null;
        if (warningRenderer != null)
        {
            warningRenderer.sortingLayerName = layerName;
            warningRenderer.sortingOrder = order + 3;
        }
    }

    private void SetRootActive(bool value)
    {
        if (root != null && root.gameObject.activeSelf != value)
        {
            root.gameObject.SetActive(value);
        }
    }

    private static void SetLineSpan(LineRenderer line, float left, float right)
    {
        line.SetPosition(0, new Vector3(left, 0f, 0f));
        line.SetPosition(1, new Vector3(right, 0f, 0f));
    }

    private static void ApplySorting(LineRenderer line, string layerName, int order)
    {
        if (line == null)
        {
            return;
        }

        line.sortingLayerName = string.IsNullOrWhiteSpace(layerName)
            ? DefaultSortingLayerName
            : layerName;
        line.sortingOrder = order;
    }

    private void ApplyKoreanFont(TextMeshPro text)
    {
        if (text == null || tmpKoreanFontService == null)
        {
            return;
        }

        tmpKoreanFontService.Apply(text);
    }

    private static Material ResolveLineMaterial()
    {
        if (sharedLineMaterial != null)
        {
            return sharedLineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            return null;
        }

        sharedLineMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        return sharedLineMaterial;
    }
}
