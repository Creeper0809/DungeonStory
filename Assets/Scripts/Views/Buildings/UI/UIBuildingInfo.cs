using DG.Tweening;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using static BuildingInfoActionViewFactory;

public class UIBuildingInfo : SerializedMonoBehaviour
{
    private BuildableObject selectedBuilding;
    private CanvasGroup canvasGroup;
    private Image buildingImage;
    private RectTransform buildingImageSize;
    private IBuildingDefinitionLookup buildingDefinitionLookup;
    private IBuildingSummaryFormatter summaryFormatter;
    private IUiPopupService popupService;
    private ICombatEquipmentMaintenanceRuntime equipmentMaintenanceRuntime;
    private ICombatEquipmentRuntime combatEquipmentRuntime;
    private ICombatEquipmentCatalog combatEquipmentCatalog;
    private IWorkOrderRuntime workOrderRuntime;
    private IGameEventBus gameEventBus;
    private IDungeonUserSettingsService userSettings;
    private IDungeonDebugModeService debugMode;
    private IDoorAccessPanelPresenter doorAccessPanelPresenter;
    private ICircusBuildingPanelPresenter circusBuildingPanelPresenter;
    private IEnvironmentalBuildingPanelPresenter
        environmentalBuildingPanelPresenter;
    private IEquipmentCraftingPanelPresenter equipmentCraftingPanelPresenter;
    private IInstanceEvolutionPanelPresenter instanceEvolutionPanelPresenter;
    private IProductionBuildingPanelPresenter productionBuildingPanelPresenter;
    private ICropPlotBuildingPanelPresenter cropPlotBuildingPanelPresenter;
    private IAnimalHusbandryBuildingPanelPresenter animalHusbandryPanelPresenter;
    private IApparelBuildingPanelPresenter apparelBuildingPanelPresenter;
    private ISurgeryBuildingPanelPresenter surgeryBuildingPanelPresenter;
    private IPaidFacilityBuildingPanelPresenter
        paidFacilityBuildingPanelPresenter;
    private IServiceRoomBuildingPanelPresenter serviceRoomBuildingPanelPresenter;
    private ITreasuryDefenseBuildingPanelPresenter
        treasuryDefenseBuildingPanelPresenter;
    private IDisposable infoFeedSubscription;
    private bool initialized;
    private readonly List<GameObject> craftActionObjects = new List<GameObject>();
    private string craftStatusMessage = string.Empty;
    private GameObject contextActionsPanel;
    private RectTransform contextActionsContent;
    private BuildingInfoResponsiveLayout responsiveLayout;

    public GameObject buildingImageObject;

    public List<UIConfig<TMP_Text>> simpleInfoText;
    public TMP_Text nameText;

    public GameObject textPrefab;
    public GameObject simpleInfoPanel;

    private bool hidden = true;

    [Inject]
    public void ConstructUIBuildingInfo(
        IBuildingDefinitionLookup buildingDefinitionLookup,
        IBuildingSummaryFormatter summaryFormatter,
        IUiPopupService popupService,
        ICombatEquipmentMaintenanceRuntime equipmentMaintenanceRuntime,
        ICombatEquipmentRuntime combatEquipmentRuntime,
        ICombatEquipmentCatalog combatEquipmentCatalog,
        IWorkOrderRuntime workOrderRuntime,
        IDoorAccessPanelPresenter doorAccessPanelPresenter,
        ICircusBuildingPanelPresenter circusBuildingPanelPresenter,
        IEnvironmentalBuildingPanelPresenter
            environmentalBuildingPanelPresenter,
        IEquipmentCraftingPanelPresenter equipmentCraftingPanelPresenter,
        IInstanceEvolutionPanelPresenter instanceEvolutionPanelPresenter,
        IProductionBuildingPanelPresenter productionBuildingPanelPresenter,
        ICropPlotBuildingPanelPresenter cropPlotBuildingPanelPresenter,
        IAnimalHusbandryBuildingPanelPresenter animalHusbandryPanelPresenter,
        IApparelBuildingPanelPresenter apparelBuildingPanelPresenter,
        ISurgeryBuildingPanelPresenter surgeryBuildingPanelPresenter,
        IPaidFacilityBuildingPanelPresenter
            paidFacilityBuildingPanelPresenter,
        IServiceRoomBuildingPanelPresenter serviceRoomBuildingPanelPresenter,
        ITreasuryDefenseBuildingPanelPresenter
            treasuryDefenseBuildingPanelPresenter,
        IDungeonUserSettingsService userSettings,
        IDungeonDebugModeService debugMode)
    {
        this.buildingDefinitionLookup = buildingDefinitionLookup
            ?? throw new ArgumentNullException(nameof(buildingDefinitionLookup));
        this.summaryFormatter = summaryFormatter
            ?? throw new ArgumentNullException(nameof(summaryFormatter));
        this.popupService = popupService
            ?? throw new ArgumentNullException(nameof(popupService));
        this.equipmentMaintenanceRuntime = equipmentMaintenanceRuntime
            ?? throw new ArgumentNullException(nameof(equipmentMaintenanceRuntime));
        this.combatEquipmentRuntime = combatEquipmentRuntime
            ?? throw new ArgumentNullException(nameof(combatEquipmentRuntime));
        this.combatEquipmentCatalog = combatEquipmentCatalog
            ?? throw new ArgumentNullException(nameof(combatEquipmentCatalog));
        this.workOrderRuntime = workOrderRuntime
            ?? throw new ArgumentNullException(nameof(workOrderRuntime));
        this.doorAccessPanelPresenter = doorAccessPanelPresenter
            ?? throw new ArgumentNullException(nameof(doorAccessPanelPresenter));
        this.circusBuildingPanelPresenter = circusBuildingPanelPresenter
            ?? throw new ArgumentNullException(nameof(circusBuildingPanelPresenter));
        this.environmentalBuildingPanelPresenter =
            environmentalBuildingPanelPresenter
            ?? throw new ArgumentNullException(
                nameof(environmentalBuildingPanelPresenter));
        this.equipmentCraftingPanelPresenter = equipmentCraftingPanelPresenter
            ?? throw new ArgumentNullException(
                nameof(equipmentCraftingPanelPresenter));
        this.instanceEvolutionPanelPresenter = instanceEvolutionPanelPresenter
            ?? throw new ArgumentNullException(
                nameof(instanceEvolutionPanelPresenter));
        this.productionBuildingPanelPresenter = productionBuildingPanelPresenter
            ?? throw new ArgumentNullException(
                nameof(productionBuildingPanelPresenter));
        this.cropPlotBuildingPanelPresenter = cropPlotBuildingPanelPresenter
            ?? throw new ArgumentNullException(
                nameof(cropPlotBuildingPanelPresenter));
        this.animalHusbandryPanelPresenter = animalHusbandryPanelPresenter
            ?? throw new ArgumentNullException(
                nameof(animalHusbandryPanelPresenter));
        this.apparelBuildingPanelPresenter = apparelBuildingPanelPresenter
            ?? throw new ArgumentNullException(
                nameof(apparelBuildingPanelPresenter));
        this.surgeryBuildingPanelPresenter = surgeryBuildingPanelPresenter
            ?? throw new ArgumentNullException(
                nameof(surgeryBuildingPanelPresenter));
        this.paidFacilityBuildingPanelPresenter =
            paidFacilityBuildingPanelPresenter
            ?? throw new ArgumentNullException(
                nameof(paidFacilityBuildingPanelPresenter));
        this.serviceRoomBuildingPanelPresenter =
            serviceRoomBuildingPanelPresenter
            ?? throw new ArgumentNullException(
                nameof(serviceRoomBuildingPanelPresenter));
        this.treasuryDefenseBuildingPanelPresenter =
            treasuryDefenseBuildingPanelPresenter
            ?? throw new ArgumentNullException(
                nameof(treasuryDefenseBuildingPanelPresenter));
        this.userSettings = userSettings
            ?? throw new ArgumentNullException(nameof(userSettings));
        this.debugMode = debugMode
            ?? throw new ArgumentNullException(nameof(debugMode));
    }

    [Inject]
    public void ConstructUIBuildingInfoEventBus(IGameEventBus gameEventBus)
    {
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        SubscribeToInfoFeed();
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void Start()
    {
        EnsureInitialized();
        if (hidden)
        {
            SetHiddenImmediate();
        }
    }

    private void EnsureInitialized()
    {
        responsiveLayout ??=
            new BuildingInfoResponsiveLayout(transform as RectTransform);
        if (initialized)
        {
            return;
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (buildingImageObject != null)
        {
            buildingImage = buildingImageObject.GetComponent<Image>();
            buildingImageSize = buildingImageObject.GetComponent<RectTransform>();
        }
        initialized = true;
    }

    public void DisplayBuildingInfo(BuildableObject building)
    {
        EnsureInitialized();

        if (building == null)
        {
            throw new ArgumentNullException(nameof(building));
        }

        if (building != selectedBuilding && !hidden) return;
        if (building != selectedBuilding)
        {
            craftStatusMessage = string.Empty;
        }
        selectedBuilding = building;
        productionBuildingPanelPresenter?.ShowWorldLinks(building);
        BuildingSO buildingData = ResolveBuildingLookup().GetBuilding(building.id) ?? building.BuildingData;
        if (buildingData == null)
        {
            return;
        }

        ResolvePopupService().CloseAll();
        OpenDispaly();
        if (buildingImageObject != null)
        {
            Vector2 size = new Vector2(Mathf.Max(40f, (buildingData.width / 3f) * 160f), 160f);
            if (buildingImageSize != null)
            {
                buildingImageSize.sizeDelta = size;
            }
            if (buildingImage != null)
            {
                ApplyBuildingPreview(buildingData.icon);
            }
        }
        if (nameText != null)
        {
            nameText.text = buildingData.objectName;
        }

        BuildingSummaryPresentation presentation = ResolveSummaryFormatter().Format(building);
        IReadOnlyList<string> details = presentation.DetailLines;
        List<UIConfig<TMP_Text>> detailViews = simpleInfoText ?? new List<UIConfig<TMP_Text>>();
        for (int index = 0; index < detailViews.Count; index++)
        {
            UIConfig<TMP_Text> ui = detailViews[index];
            if (ui?.uiObject == null) continue;

            bool visible = index < details.Count;
            ui.uiObject.gameObject.SetActive(visible);
            if (visible)
            {
                ui.uiObject.text = details[index];
                ui.uiObject.color = DungeonUiTheme.TextPrimary;
                ui.uiObject.fontSize = 24f;
                ui.uiObject.enableAutoSizing = true;
                ui.uiObject.fontSizeMin = 14f;
                ui.uiObject.fontSizeMax = 24f;
            }
        }

        RenderContextActions(buildingData, building);
    }

    private void ApplyBuildingPreview(Sprite sprite)
    {
        buildingImage.sprite = sprite;
        buildingImage.color = Color.white;
        buildingImage.material = null;
        buildingImage.type = Image.Type.Simple;
        buildingImage.preserveAspect = true;
        buildingImage.raycastTarget = false;
    }

    public void OpenDispaly()
    {
        EnsureInitialized();
        hidden = false;
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        responsiveLayout?.BringToFront();

        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        ResolvePopupService().BlockTouch();
        canvasGroup.DOKill();
        transform.DOKill();
        if (userSettings?.Current?.reducedMotion == true)
        {
            canvasGroup.alpha = 1f;
            transform.localScale = Vector3.one;
        }
        else
        {
            canvasGroup.alpha = 0f;
            transform.localScale = new Vector3(0.985f, 0.985f, 1f);
            canvasGroup.DOFade(1.0f, 0.14f)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
            transform.DOScale(Vector3.one, 0.18f)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }
    }
    public void CloseDispaly()
    {
        EnsureInitialized();
        hidden = true;
        selectedBuilding = null;
        productionBuildingPanelPresenter?.ClearWorldLinks();
        ClearCraftActions();

        if (!gameObject.activeInHierarchy)
        {
            SetHiddenImmediate();
            ResolvePopupService().ReleaseTouch();
            return;
        }

        canvasGroup.DOKill();
        transform.DOKill();
        if (userSettings?.Current?.reducedMotion == true)
        {
            FinishCloseDisplay();
            return;
        }
        transform.DOScale(new Vector3(0.99f, 0.99f, 1f), 0.1f)
            .SetEase(Ease.InCubic)
            .SetUpdate(true);
        canvasGroup.DOFade(0f, 0.1f).SetEase(Ease.InCubic).SetUpdate(true).OnComplete(() =>
        {
            FinishCloseDisplay();
        });
    }

    private void FinishCloseDisplay()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        transform.localScale = Vector3.one;
        gameObject.SetActive(false);
        ResolvePopupService().ReleaseTouch();
    }

    public void OnTriggerEvent(InfoFeedEvent eventType)
    {
        if (eventType.Target is not CharacterActor actor || actor == null)
        {
            return;
        }

        CloseDisplayImmediate();
    }

    private void OnEnable()
    {
        SubscribeToInfoFeed();
    }

    private void OnDisable()
    {
        productionBuildingPanelPresenter?.ClearWorldLinks();
        infoFeedSubscription?.Dispose();
        infoFeedSubscription = null;
    }

    private void SubscribeToInfoFeed()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        infoFeedSubscription ??=
            gameEventBus.Subscribe<InfoFeedEvent>(OnTriggerEvent);
    }

    private void CloseDisplayImmediate()
    {
        EnsureInitialized();
        hidden = true;
        selectedBuilding = null;
        productionBuildingPanelPresenter?.ClearWorldLinks();
        ClearCraftActions();
        SetHiddenImmediate();
        ResolvePopupService().ReleaseTouch();
        gameObject.SetActive(false);
    }

    private void RenderContextActions(BuildingSO buildingData, BuildableObject building)
    {
        ClearCraftActions();
        if (!string.IsNullOrWhiteSpace(craftStatusMessage))
        {
            GameObject feedback = CreateCraftStatus(
                RequireContextActionsRoot(),
                craftStatusMessage,
                nameText?.font);
            feedback.name = "BuildingActionFeedback";
            craftActionObjects.Add(feedback);
        }
        if (building is ConstructionSite constructionSite)
        {
            RenderConstructionActions(constructionSite);
            RenderDebugIdentity(buildingData, building);
            return;
        }

        IReadOnlyList<GameObject> equipmentCraftingObjects =
            equipmentCraftingPanelPresenter.Render(
                RequireContextActionsRoot(),
                building,
                nameText != null ? nameText.font : null,
                message => craftStatusMessage = message,
                () => DisplayBuildingInfo(building));
        craftActionObjects.AddRange(equipmentCraftingObjects);
        IReadOnlyList<GameObject> evolutionObjects =
            instanceEvolutionPanelPresenter.Render(
                RequireContextActionsRoot(),
                building,
                nameText != null ? nameText.font : null,
                message => craftStatusMessage = message,
                () => DisplayBuildingInfo(building));
        craftActionObjects.AddRange(evolutionObjects);
        IReadOnlyList<GameObject> environmentalObjects =
            environmentalBuildingPanelPresenter.Render(
                RequireContextActionsRoot(),
                building,
                nameText != null ? nameText.font : null,
                message => craftStatusMessage = message,
                () => DisplayBuildingInfo(building));
        craftActionObjects.AddRange(environmentalObjects);
        RenderMaintenanceActions(buildingData, building);
        IReadOnlyList<GameObject> productionObjects =
            productionBuildingPanelPresenter.Render(
                RequireContextActionsRoot(),
                building,
                nameText != null ? nameText.font : null,
                message => craftStatusMessage = message,
                () => DisplayBuildingInfo(building));
        craftActionObjects.AddRange(productionObjects);
        IReadOnlyList<GameObject> cropObjects =
            cropPlotBuildingPanelPresenter.Render(
                RequireContextActionsRoot(),
                building,
                nameText != null ? nameText.font : null,
                message => craftStatusMessage = message,
                () => DisplayBuildingInfo(building));
        craftActionObjects.AddRange(cropObjects);
        IReadOnlyList<GameObject> husbandryObjects =
            animalHusbandryPanelPresenter.Render(
                RequireContextActionsRoot(),
                building,
                nameText != null ? nameText.font : null,
                message => craftStatusMessage = message,
                () => DisplayBuildingInfo(building));
        craftActionObjects.AddRange(husbandryObjects);
        IReadOnlyList<GameObject> apparelObjects =
            apparelBuildingPanelPresenter.Render(
                RequireContextActionsRoot(),
                building,
                nameText != null ? nameText.font : null,
                message => craftStatusMessage = message,
                () => DisplayBuildingInfo(building));
        craftActionObjects.AddRange(apparelObjects);
        IReadOnlyList<GameObject> surgeryObjects =
            surgeryBuildingPanelPresenter.Render(
                RequireContextActionsRoot(),
                building,
                nameText != null ? nameText.font : null,
                message => craftStatusMessage = message,
                () => DisplayBuildingInfo(building));
        craftActionObjects.AddRange(surgeryObjects);
        IReadOnlyList<GameObject> paidFacilityObjects =
            paidFacilityBuildingPanelPresenter.Render(
                RequireContextActionsRoot(),
                building,
                nameText != null ? nameText.font : null,
                message => craftStatusMessage = message,
                () => DisplayBuildingInfo(building));
        craftActionObjects.AddRange(paidFacilityObjects);
        IReadOnlyList<GameObject> serviceRoomObjects =
            serviceRoomBuildingPanelPresenter.Render(
                RequireContextActionsRoot(),
                building,
                nameText != null ? nameText.font : null,
                message => craftStatusMessage = message,
                () => DisplayBuildingInfo(building));
        craftActionObjects.AddRange(serviceRoomObjects);
        IReadOnlyList<GameObject> treasuryDefenseObjects =
            treasuryDefenseBuildingPanelPresenter.Render(
                RequireContextActionsRoot(),
                building,
                nameText != null ? nameText.font : null,
                message => craftStatusMessage = message,
                () => DisplayBuildingInfo(building));
        craftActionObjects.AddRange(treasuryDefenseObjects);
        if (building is Door door)
        {
            IReadOnlyList<GameObject> doorObjects = doorAccessPanelPresenter.Render(
                RequireContextActionsRoot(),
                door,
                nameText != null ? nameText.font : null,
                () => DisplayBuildingInfo(door));
            craftActionObjects.AddRange(doorObjects);
        }
        if (buildingData.GetCircusStageAbility() != null)
        {
            IReadOnlyList<GameObject> circusObjects =
                circusBuildingPanelPresenter.Render(
                    RequireContextActionsRoot(),
                    building,
                    nameText != null ? nameText.font : null,
                    () => DisplayBuildingInfo(building));
            craftActionObjects.AddRange(circusObjects);
        }
        RenderDebugIdentity(buildingData, building);
    }

    private void RenderDebugIdentity(BuildingSO definition, BuildableObject building)
    {
        if (debugMode?.IsDeveloperModeEnabled != true
            || definition == null
            || building == null)
        {
            return;
        }

        string instanceId = building.RequirePersistentInstanceId().Value;
        GameObject diagnostic = CreateCraftStatus(
            RequireContextActionsRoot(),
            $"DEBUG · definition={definition.ContentDefinitionId}\n"
            + $"instance={instanceId} · cell={building.centerPos.x},{building.centerPos.y}",
            nameText?.font);
        diagnostic.name = "BuildingDebugIdentity";
        craftActionObjects.Add(diagnostic);
    }

    private void RenderConstructionActions(ConstructionSite site)
    {
        if (site == null)
        {
            return;
        }

        Transform actionsRoot = RequireContextActionsRoot();
        if (workOrderRuntime.TryGetOrderFor(site, BuiltInWorkTypeIds.Construct, out WorkOrderProgressState order))
        {
            craftActionObjects.Add(CreateConstructionProgressBar(actionsRoot, order, nameText?.font));
            if (workOrderRuntime is IConstructionProjectWorkforceRuntime workforce
                && workforce.TryCaptureConstructionProject(
                    site,
                    out ProjectWorkforceSnapshot project))
            {
                float remainingWu = Mathf.Max(
                    0f,
                    order.RequiredWork - order.CompletedWork);
                float currentSeconds = project.EffectiveWuPerSecond > 0f
                    ? remainingWu / project.EffectiveWuPerSecond
                    : float.PositiveInfinity;
                float nextRate = project.EffectiveWuPerSecond
                    + project.ReferenceWorkerWuPerSecond
                    * project.NextWorkerContribution;
                float nextSeconds = nextRate > 0f
                    ? remainingWu / nextRate
                    : float.PositiveInfinity;
                string marginal = project.ActiveWorkers >= project.MaximumWorkers
                    ? "추가 투입 불가"
                    : float.IsInfinity(currentSeconds)
                        ? $"다음 작업자 기여 {project.NextWorkerContribution * 100f:0}%"
                        : $"다음 작업자 투입 시 약 {Mathf.Max(0f, currentSeconds - nextSeconds):0.0}초 단축";
                GameObject workforceStatus = CreateCraftStatus(
                    actionsRoot,
                    $"시공 인원 {project.ActiveWorkers}/{project.MaximumWorkers}명"
                    + $" · 유효 {project.EffectiveWorkerCount:0.00}명\n{marginal}",
                    nameText?.font);
                workforceStatus.name = "BuildingConstructionWorkforceStatus";
                craftActionObjects.Add(workforceStatus);
            }
            if (workOrderRuntime is IWorkOrderWorkerPolicyCommand workerCommands)
            {
                GameObject workerButton = CreateCraftButton(
                    actionsRoot,
                    FormatConstructionWorkerPolicy(order.WorkerPolicy),
                    () =>
                    {
                        workerCommands.SetWorkerPolicy(
                            order.WorkOrderId,
                            NextConstructionWorkerPolicy(order.WorkerPolicy),
                            out DomainFailure failure);
                        craftStatusMessage = failure.IsFailure
                            ? GameplayUiPresentationText.FailureFallback(
                                failure,
                                debugMode?.IsDeveloperModeEnabled == true)
                            : "건설 작업자 정책을 변경했습니다.";
                        DisplayBuildingInfo(site);
                    },
                    nameText?.font);
                workerButton.name = "BuildingConstructionWorkerPolicy";
                craftActionObjects.Add(workerButton);
            }

            if (workOrderRuntime is IQualityTargetPipelineCommand qualityCommands
                && workOrderRuntime is IQualityTargetPipelineQuery qualityQuery)
            {
                bool hasPipeline = qualityQuery.TryGetQualityPipeline(
                    order.QualityPipelineId,
                    out QualityTargetPipelineSaveData pipeline);
                GameObject qualityButton = CreateCraftButton(
                    actionsRoot,
                    hasPipeline
                        ? $"품질 반복: {GameplayUiPresentationText.Quality(pipeline.minimumQuality)}"
                            + $" · {GameplayUiPresentationText.QualityStage(pipeline.stage)}"
                        : "품질 반복: 양호 이상",
                    () =>
                    {
                        if (!hasPipeline)
                        {
                            qualityCommands.CreateForWorkOrder(
                                order.WorkOrderId,
                                new QualityTargetPipelineSaveData
                                {
                                    definitionId = site.BuildingData?
                                        .ContentDefinitionId ?? string.Empty,
                                    minimumQuality = CraftsmanshipQualityTier.Good,
                                    requiredAcceptedCount = 1,
                                    workerPolicy = order.WorkerPolicy?
                                        .CloneNormalized(),
                                    rejectedDisposition =
                                        RejectedOutputDisposition
                                            .DismantleFacilityAndRetry,
                                    limitMode = QualityRepeatLimitMode.SafeLimits,
                                    maximumAttempts = 10,
                                    footprintWidth = Mathf.Max(
                                        1,
                                        site.BuildingData?.width ?? 1),
                                    footprintHeight = Mathf.Max(
                                        1,
                                        site.BuildingData?.height ?? 1)
                                },
                                out _,
                                out DomainFailure failure);
                            craftStatusMessage = failure.IsFailure
                                ? GameplayUiPresentationText.FailureFallback(
                                    failure,
                                    debugMode?.IsDeveloperModeEnabled == true)
                                : "시설 품질 반복을 설정했습니다.";
                        }
                        else if (pipeline.stage == QualityTargetPipelineStage.Paused)
                        {
                            qualityCommands.ResumeQualityPipeline(
                                pipeline.pipelineId,
                                out _);
                        }
                        else
                        {
                            qualityCommands.PauseQualityPipeline(
                                pipeline.pipelineId,
                                out _);
                        }
                        DisplayBuildingInfo(site);
                    },
                    nameText?.font);
                qualityButton.name = "BuildingConstructionQualityPipeline";
                craftActionObjects.Add(qualityButton);
            }
        }

        GameObject cancelButton = CreateCraftButton(
            actionsRoot,
            "공사 취소",
            () =>
            {
                site.CancelConstruction();
                CloseDispaly();
            },
            nameText?.font);
        cancelButton.name = "BuildingConstructionCancel";
        craftActionObjects.Add(cancelButton);
    }

    private static string FormatConstructionWorkerPolicy(
        WorkerSelectionPolicySaveData policy)
    {
        return $"건설 작업자 · {GameplayUiPresentationText.WorkerPolicy(policy)}";
    }

    private static WorkerSelectionPolicySaveData NextConstructionWorkerPolicy(
        WorkerSelectionPolicySaveData policy)
    {
        WorkerSelectionPolicySaveData normalized = policy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone();
        if (normalized.mode == WorkerSelectionMode.Anyone
            && normalized.sortMode != WorkerCandidateSortMode.BestExpectedQuality)
        {
            return WorkerSelectionPolicySaveData.Anyone(
                WorkerCandidateSortMode.BestExpectedQuality);
        }
        if (normalized.mode == WorkerSelectionMode.Anyone)
        {
            return new WorkerSelectionPolicySaveData
            {
                mode = WorkerSelectionMode.RuleSet,
                matchMode = WorkerRequirementMatchMode.All,
                sortMode = WorkerCandidateSortMode.BestExpectedQuality,
                minimumSkillId =
                    BuiltInCharacterProficiencyIds.ConstructionEngineering.Value,
                minimumSkillExperience = 400
            };
        }
        return WorkerSelectionPolicySaveData.Anyone(
            WorkerCandidateSortMode.SpecificThenBestExpectedQuality);
    }

    private void RenderMaintenanceActions(
        BuildingSO buildingData,
        BuildableObject building)
    {
        if (buildingData?.GetAbility<BuildingEquipmentMaintenanceAbility>() == null
            || building == null)
        {
            return;
        }

        IReadOnlyList<CombatEquipmentRepairOrder> orders =
            equipmentMaintenanceRuntime.Orders
                .Where(order =>
                    order != null
                    && string.Equals(
                        order.facilityBuildingId,
                        building.PersistentInstanceId.Value,
                        StringComparison.Ordinal))
                .OrderBy(order => order.orderId, StringComparer.Ordinal)
                .ToArray();
        Transform actionsRoot = RequireContextActionsRoot();
        GameObject header = CreateCraftStatus(
            actionsRoot,
            orders.Count == 0
                ? "장비 수리 대기열이 비어 있습니다."
                : $"장비 수리 대기열 {orders.Count}건",
            nameText?.font);
        header.name = "BuildingMaintenanceHeader";
        craftActionObjects.Add(header);

        ICombatEquipmentCatalog catalog = combatEquipmentCatalog;
        ICombatEquipmentRuntime equipment = combatEquipmentRuntime;
        for (int i = 0; i < orders.Count; i++)
        {
            CombatEquipmentRepairOrder order = orders[i];
            string equipmentName = order.equipmentInstanceId;
            if (equipment != null
                && equipment.TryGetInstance(
                    order.equipmentInstanceId,
                    out CombatEquipmentInstance instance)
                && catalog.TryGet(
                    instance.definitionId,
                    out CombatEquipmentDefinitionSO definition))
            {
                equipmentName = definition.DisplayName;
            }

            GameObject progress = CreateMaintenanceProgressBar(
                actionsRoot,
                equipmentName,
                order,
                nameText?.font);
            progress.name = $"BuildingMaintenance_{i}";
            craftActionObjects.Add(progress);
        }
    }

    private void ClearCraftActions()
    {
        foreach (GameObject item in craftActionObjects)
        {
            if (item == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                item.SetActive(false);
                Destroy(item);
            }
            else
            {
                DestroyImmediate(item);
            }
        }

        craftActionObjects.Clear();
        if (contextActionsPanel != null)
        {
            contextActionsPanel.SetActive(false);
        }
        responsiveLayout?.SetLegacyChromeVisible(true);
    }

    private Transform RequireContextActionsRoot()
    {
        EnsureContextActionsPanel();
        ApplyResponsiveContextActionsLayout();
        responsiveLayout?.SetLegacyChromeVisible(false);
        contextActionsPanel.SetActive(true);
        contextActionsPanel.transform.SetAsLastSibling();
        return contextActionsContent;
    }

    private void ApplyResponsiveContextActionsLayout()
    {
        if (contextActionsPanel == null)
        {
            return;
        }

        RectTransform panelRect =
            contextActionsPanel.GetComponent<RectTransform>();
        if (panelRect == null)
        {
            return;
        }

        bool portrait = Screen.height > Screen.width;
        responsiveLayout?.ApplyWidth(portrait);
        panelRect.anchorMin = portrait
            ? new Vector2(0.04f, 0.05f)
            : new Vector2(0.38f, 0.08f);
        panelRect.anchorMax = portrait
            ? new Vector2(0.96f, 0.72f)
            : new Vector2(0.98f, 0.72f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
    }

    private void EnsureContextActionsPanel()
    {
        if (contextActionsPanel != null && contextActionsContent != null)
        {
            return;
        }

        Transform existing = transform.Find("BuildingContextActions");
        if (existing != null)
        {
            contextActionsPanel = existing.gameObject;
            contextActionsContent = existing
                .Find("Viewport/Content") as RectTransform;
            if (contextActionsContent != null)
            {
                return;
            }

            Destroy(existing.gameObject);
        }

        contextActionsPanel = new GameObject(
            "BuildingContextActions",
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect));
        contextActionsPanel.transform.SetParent(transform, false);
        RectTransform panelRect = contextActionsPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.38f, 0.08f);
        panelRect.anchorMax = new Vector2(0.98f, 0.72f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        contextActionsPanel.GetComponent<Image>().color = DungeonUiTheme.Surface;

        GameObject viewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(RectMask2D));
        viewportObject.transform.SetParent(contextActionsPanel.transform, false);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(8f, 8f);
        viewport.offsetMax = new Vector2(-24f, -8f);
        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.01f);

        GameObject contentObject = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        contextActionsContent = contentObject.GetComponent<RectTransform>();
        contextActionsContent.anchorMin = new Vector2(0f, 1f);
        contextActionsContent.anchorMax = new Vector2(1f, 1f);
        contextActionsContent.pivot = new Vector2(0.5f, 1f);
        contextActionsContent.anchoredPosition = Vector2.zero;
        contextActionsContent.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar scrollbar = CreateContextScrollbar(contextActionsPanel.transform);
        ScrollRect scroll = contextActionsPanel.GetComponent<ScrollRect>();
        scroll.content = contextActionsContent;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 36f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility =
            ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = 4f;
        contextActionsPanel.SetActive(false);
    }

    private static Scrollbar CreateContextScrollbar(Transform parent)
    {
        GameObject root = new GameObject(
            "Scrollbar",
            typeof(RectTransform),
            typeof(Image),
            typeof(Scrollbar));
        root.transform.SetParent(parent, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 0f);
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = Vector2.one;
        rootRect.offsetMin = new Vector2(-16f, 8f);
        rootRect.offsetMax = new Vector2(-4f, -8f);
        root.GetComponent<Image>().color = DungeonUiTheme.SurfaceMuted;

        GameObject slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
        slidingArea.transform.SetParent(root.transform, false);
        RectTransform slidingRect = slidingArea.GetComponent<RectTransform>();
        slidingRect.anchorMin = Vector2.zero;
        slidingRect.anchorMax = Vector2.one;
        slidingRect.offsetMin = new Vector2(2f, 2f);
        slidingRect.offsetMax = new Vector2(-2f, -2f);

        GameObject handleObject = new GameObject(
            "Handle",
            typeof(RectTransform),
            typeof(Image));
        handleObject.transform.SetParent(slidingArea.transform, false);
        RectTransform handle = handleObject.GetComponent<RectTransform>();
        handle.anchorMin = Vector2.zero;
        handle.anchorMax = Vector2.one;
        handle.offsetMin = Vector2.zero;
        handle.offsetMax = Vector2.zero;
        Image handleImage = handleObject.GetComponent<Image>();
        handleImage.color = DungeonUiTheme.Accent;

        Scrollbar scrollbar = root.GetComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        return scrollbar;
    }

    private static string FormatCraftMessage(string message)
    {
        return message switch
        {
            "craft-cost-not-available" => "재료 부족",
            "craft-cost-withdraw-failed" => "재료 출고 실패",
            "equipment-not-found" => "장비 정의 없음",
            _ => string.IsNullOrWhiteSpace(message) ? "제작 실패" : message
        };
    }

    private static string SanitizeObjectName(string value)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        char[] chars = normalized
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray();
        return new string(chars);
    }

    private void SetHiddenImmediate()
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.DOKill();
        transform.DOKill();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        transform.localScale = Vector3.one;
    }

    private IBuildingDefinitionLookup ResolveBuildingLookup()
    {
        return buildingDefinitionLookup
            ?? throw new InvalidOperationException($"{nameof(UIBuildingInfo)} requires {nameof(IBuildingDefinitionLookup)} injection.");
    }

    private IUiPopupService ResolvePopupService()
    {
        return popupService
            ?? throw new InvalidOperationException($"{nameof(UIBuildingInfo)} requires {nameof(IUiPopupService)} injection.");
    }

    private IBuildingSummaryFormatter ResolveSummaryFormatter()
    {
        return summaryFormatter
            ?? throw new InvalidOperationException($"{nameof(UIBuildingInfo)} requires {nameof(IBuildingSummaryFormatter)} injection.");
    }
}
[Serializable]
public class UIConfig<T>
{
    public string name;
    public T uiObject;
}
