using System;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class UIBuildingSelectButton : MonoBehaviour
{
    private static readonly Vector2 DefaultButtonSize = new Vector2(100f, 100f);
    private static readonly Vector2 DefaultIconMaxSize = new Vector2(84f, 64f);

    [SerializeField] private Image iconImage;
    [SerializeField] private Vector2 buttonSize = DefaultButtonSize;
    [SerializeField] private Vector2 iconMaxSize = DefaultIconMaxSize;

    public int id;
    private IDungeonGridBuildingControllerProvider buildingControllerProvider;
    private IGameSessionStateProvider gameDataProvider;
    private BlueprintResearchRuntime research;
    private IGameEventBus gameEventBus;
    private IDungeonDebugRuleQuery debugRules;
    private IDisposable researchCompletedSubscription;
    private BuildingSO buildingData;
    private GameSessionState observedGameData;
    private Action collapseForPlacement;
    private bool isDisposed;

    [Inject]
    public void Construct(
        IDungeonGridBuildingControllerProvider buildingControllerProvider,
        IGameSessionStateProvider gameDataProvider,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IGameEventBus gameEventBus,
        IDungeonDebugRuleQuery debugRules)
    {
        this.buildingControllerProvider = buildingControllerProvider
            ?? throw new ArgumentNullException(nameof(buildingControllerProvider));
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        research = (progressionRuntimes
                ?? throw new ArgumentNullException(nameof(progressionRuntimes)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(UIBuildingSelectButton)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.debugRules = debugRules ?? throw new ArgumentNullException(nameof(debugRules));
        SubscribeToScopedEvents();
    }

    public void Initialization(BuildingSO so)
    {
        if (so == null) return;

        id = so.id;
        buildingData = so;
        ApplyButtonSize();
        SetIcon(so.icon);
        RefreshAvailability();
    }

    public void Initialization(BuildingSO so, IDungeonGridBuildingControllerProvider buildingControllerProvider)
    {
        this.buildingControllerProvider = buildingControllerProvider;
        Initialization(so);
    }

    public void BindPlacementCollapse(Action collapseAction)
    {
        collapseForPlacement = collapseAction;
    }

    public void OnClick()
    {
        if (!IsAvailable())
        {
            int phase = buildingData != null ? buildingData.GetUnlockPhase() : 1;
            gameEventBus.ShowNotice($"{phase}단계에 해금되는 시설입니다.", NoticeFeedEvent.Grade.DANGER);
            return;
        }

        DungeonStoryGridBuildingController controller = RequireBuildingController();
        controller.SetGridModeBuild();
        controller.SelectBuildingById(id);
        collapseForPlacement?.Invoke();
    }

    public void ActiveDestroyMode()
    {
        RequireBuildingController().SetDestroyMode();
    }

    private void OnEnable()
    {
        isDisposed = false;
        SubscribeToScopedEvents();
        ObserveGameData();
        RefreshAvailability();
    }

    private void OnDisable()
    {
        researchCompletedSubscription?.Dispose();
        researchCompletedSubscription = null;
        collapseForPlacement = null;
        StopObservingGameData();
    }

    private void OnDestroy()
    {
        isDisposed = true;
        researchCompletedSubscription?.Dispose();
        researchCompletedSubscription = null;
        StopObservingGameData();
    }

    private void StopObservingGameData()
    {
        if (observedGameData?.day != null)
        {
            observedGameData.day.OnValueChange -= OnDayChanged;
        }

        observedGameData = null;
    }

    private void ObserveGameData()
    {
        if (observedGameData != null
            || gameDataProvider == null
            || !gameDataProvider.TryGetSessionState(out observedGameData)
            || observedGameData?.day == null)
        {
            return;
        }

        observedGameData.day.OnValueChange -= OnDayChanged;
        observedGameData.day.OnValueChange += OnDayChanged;
    }

    private void OnDayChanged(int _)
    {
        if (isDisposed || this == null)
        {
            return;
        }

        RefreshAvailability();
    }

    private bool IsAvailable()
    {
        ObserveGameData();
        BlueprintResearchState unlockState = research.State;
        return buildingData == null
            || observedGameData == null
            || FacilityProgression.IsUnlocked(
                buildingData,
                observedGameData,
                unlockState,
                debugRules);
    }

    public void OnTriggerEvent(BlueprintResearchCompletedEvent eventType)
    {
        RefreshAvailability();
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || researchCompletedSubscription != null || gameEventBus == null)
        {
            return;
        }

        researchCompletedSubscription =
            gameEventBus.Subscribe<BlueprintResearchCompletedEvent>(OnTriggerEvent);
    }

    private void RefreshAvailability()
    {
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.interactable = IsAvailable();
        }
    }

    private void ApplyButtonSize()
    {
        if (transform is RectTransform rectTransform)
        {
            rectTransform.sizeDelta = buttonSize;
        }
    }

    private void SetIcon(Sprite sprite)
    {
        Image image = ResolveIconImage();
        if (image == null) return;

        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        if (image.transform is RectTransform iconRect)
        {
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = GetFittedIconSize(sprite);
        }
    }

    private Image ResolveIconImage()
    {
        if (iconImage != null) return iconImage;
        if (transform.childCount <= 0) return null;

        iconImage = transform.GetChild(0).GetComponent<Image>();
        return iconImage;
    }

    private Vector2 GetFittedIconSize(Sprite sprite)
    {
        if (sprite == null || sprite.rect.width <= 0f || sprite.rect.height <= 0f)
        {
            return iconMaxSize;
        }

        float scale = Mathf.Min(
            iconMaxSize.x / sprite.rect.width,
            iconMaxSize.y / sprite.rect.height);
        return new Vector2(sprite.rect.width * scale, sprite.rect.height * scale);
    }

    private DungeonStoryGridBuildingController RequireBuildingController()
    {
        return (buildingControllerProvider
                ?? throw new InvalidOperationException($"{nameof(UIBuildingSelectButton)} requires {nameof(IDungeonGridBuildingControllerProvider)} injection."))
            .Controller;
    }
}
