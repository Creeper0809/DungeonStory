using System;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public sealed class WildlifeInfoPanel : UIPopUp
{
    private IUiPopupService popupService;
    private ITmpKoreanFontService fontService;
    private IDungeonItemCatalogProvider itemCatalogProvider;
    private IWildlifeHuntCommandService huntCommandService;
    private IWildlifeCaptureRuntime captureRuntime;
    private IAnimalHusbandryQuery husbandryQuery;
    private IAnimalHusbandryCommand husbandryCommands;
    private ICharacterAiWorldRegistry worldRegistry;
    private ISurgeryPlanningWindowService surgeryWindowService;
    private IWildlifeCompanionRoleQuery companionRoles;
    private IWildlifeCompanionRoleCommand companionCommands;
    private IWildlifeHaulRoleQuery haulRoles;
    private IWildlifeHaulRoleCommand haulCommands;
    private GameObject uiRoot;
    private TMP_Text titleText;
    private TMP_Text bodyText;
    private Image portraitImage;
    private WildlifeActor current;
    private IGameEventBus gameEventBus;
    private IDisposable infoFeedSubscription;
    private string actionMessage = string.Empty;
    private GameObject companionOwnerPanel;

    public WildlifeActor CurrentWildlife => current;
    public bool IsShowingWildlife => current != null
        && uiRoot != null
        && uiRoot.activeInHierarchy;

    [Inject]
    public void Construct(
        IUiPopupService popupService,
        ITmpKoreanFontService fontService,
        IDungeonItemCatalogProvider itemCatalogProvider,
        IWildlifeHuntCommandService huntCommandService,
        IWildlifeCaptureRuntime captureRuntime,
        IAnimalHusbandryQuery husbandryQuery,
        IAnimalHusbandryCommand husbandryCommands,
        ICharacterAiWorldRegistry worldRegistry,
        ISurgeryPlanningWindowService surgeryWindowService)
    {
        this.popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
        this.fontService = fontService ?? throw new ArgumentNullException(nameof(fontService));
        this.itemCatalogProvider = itemCatalogProvider
            ?? throw new ArgumentNullException(nameof(itemCatalogProvider));
        this.huntCommandService = huntCommandService
            ?? throw new ArgumentNullException(nameof(huntCommandService));
        this.captureRuntime = captureRuntime
            ?? throw new ArgumentNullException(nameof(captureRuntime));
        this.husbandryQuery = husbandryQuery
            ?? throw new ArgumentNullException(nameof(husbandryQuery));
        this.husbandryCommands = husbandryCommands
            ?? throw new ArgumentNullException(nameof(husbandryCommands));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.surgeryWindowService = surgeryWindowService
            ?? throw new ArgumentNullException(nameof(surgeryWindowService));
    }

    [Inject]
    public void ConstructWildlifeInfoEventBus(IGameEventBus gameEventBus)
    {
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        SubscribeToInfoFeed();
    }

    [Inject]
    public void ConstructWildlifeCompanionRoles(
        IWildlifeCompanionRoleQuery companionRoles,
        IWildlifeCompanionRoleCommand companionCommands)
    {
        this.companionRoles = companionRoles
            ?? throw new ArgumentNullException(nameof(companionRoles));
        this.companionCommands = companionCommands
            ?? throw new ArgumentNullException(nameof(companionCommands));
    }

    [Inject]
    public void ConstructWildlifeHaulRoles(
        IWildlifeHaulRoleQuery haulRoles,
        IWildlifeHaulRoleCommand haulCommands)
    {
        this.haulRoles = haulRoles
            ?? throw new ArgumentNullException(nameof(haulRoles));
        this.haulCommands = haulCommands
            ?? throw new ArgumentNullException(nameof(haulCommands));
    }

    private void Start()
    {
        EnsureView();
        uiRoot.SetActive(false);
    }

    private void Update()
    {
        if (uiRoot == null || !uiRoot.activeSelf)
        {
            return;
        }

        if (current == null || !current.IsAlive)
        {
            OnClose();
            return;
        }

        Render();
    }

    private void OnEnable()
    {
        SubscribeToInfoFeed();
    }

    private void OnDisable()
    {
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

    public void OnTriggerEvent(InfoFeedEvent eventType)
    {
        if (eventType.Target is not WildlifeActor wildlife || wildlife == null)
        {
            return;
        }

        EnsureView();
        popupService.CloseAll();
        current = wildlife;
        actionMessage = string.Empty;
        uiRoot.SetActive(true);
        popupService.Open(this);
        Render();
    }

    public override void OnClose()
    {
        if (uiRoot != null)
        {
            uiRoot.SetActive(false);
        }

        current = null;
        if (companionOwnerPanel != null)
        {
            Destroy(companionOwnerPanel);
        }
    }

    private void EnsureView()
    {
        if (uiRoot != null)
        {
            return;
        }

        uiRoot = RuntimePanelFactoryUtility.CreateOverlayCanvas(
            "WildlifeInfoCanvas",
            new Vector2(1920f, 1080f));
        uiRoot.transform.SetParent(transform, false);
        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 735;

        GameObject panel = RuntimePanelFactoryUtility.CreatePanel(
            uiRoot.transform,
            "WildlifeInfoPanel",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-28f, -40f),
            new Vector2(460f, 540f));

        RectTransform header = CreateRect("Header", panel.transform);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.offsetMin = new Vector2(0f, -68f);
        header.offsetMax = Vector2.zero;
        header.sizeDelta = new Vector2(0f, 68f);
        header.gameObject.AddComponent<Image>().color = DungeonUiTheme.SurfaceRaised;

        titleText = CreateText("Title", header, 24f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        Stretch(titleText.rectTransform, new Vector2(18f, 0f), new Vector2(-90f, 0f));

        Button close = CreateButton("Close", header, "닫기", OnClose);
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = Vector2.one;
        closeRect.pivot = Vector2.one;
        closeRect.anchoredPosition = new Vector2(-12f, -15f);
        closeRect.sizeDelta = new Vector2(68f, 36f);

        portraitImage = CreateImage("Portrait", panel.transform);
        RectTransform portraitRect = portraitImage.GetComponent<RectTransform>();
        portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0f, 1f);
        portraitRect.pivot = new Vector2(0f, 1f);
        portraitRect.anchoredPosition = new Vector2(22f, -92f);
        portraitRect.sizeDelta = new Vector2(86f, 86f);

        bodyText = CreateText("Body", panel.transform, 17f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        Stretch(bodyText.rectTransform, new Vector2(126f, 126f), new Vector2(-18f, -190f));

        CreateActionButtonRow(panel.transform);
    }

    private void CreateActionButtonRow(Transform parent)
    {
        CreateBottomButton(parent, 0, "사냥 지정", () =>
        {
            if (current == null)
            {
                return;
            }

            huntCommandService.DesignateHunt(current.WildlifeId, true, false);
            Render();
        });
        CreateBottomButton(parent, 1, "지정 해제", () =>
        {
            if (current == null)
            {
                return;
            }

            huntCommandService.DesignateHunt(current.WildlifeId, false, false);
            Render();
        });
        CreateBottomButton(parent, 2, "우선 사냥", () =>
        {
            if (current == null)
            {
                return;
            }

            huntCommandService.DesignateHunt(current.WildlifeId, true, true);
            Render();
        });
        CreateBottomButton(parent, 3, "생포·방생", ToggleCapture);
        CreateBottomButton(parent, 4, "도축 지정", ToggleSlaughter);
        CreateBottomButton(parent, 5, "수술 계획", OpenSurgery);
        CreateBottomButton(parent, 6, "동행 주인", ToggleCompanionOwnerPanel);
        CreateBottomButton(parent, 7, "독립 운반", ToggleHaulRole);
    }

    private void CreateBottomButton(Transform parent, int index, string label, Action action)
    {
        Button button = CreateButton("Action_" + index, parent, label, action);
        RectTransform rect = button.GetComponent<RectTransform>();
        const float buttonCount = 8f;
        rect.anchorMin = new Vector2(index / buttonCount, 0f);
        rect.anchorMax = new Vector2((index + 1) / buttonCount, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(index == 0 ? 18f : 8f, 22f);
        rect.offsetMax = new Vector2(index == 7 ? -18f : -8f, 66f);
    }

    private void Render()
    {
        if (current == null)
        {
            return;
        }

        titleText.text = "야생동물 정보";
        portraitImage.sprite = current.Sprite;
        portraitImage.color = current.Sprite != null
            ? Color.white
            : current.IsDangerous ? DungeonUiTheme.Danger : DungeonUiTheme.Accent;

        WildlifeSpeciesDefinition species = current.Species;
        string yields = species != null && species.ButcherYields.Count > 0
            ? string.Join(", ", species.ButcherYields.Select(yieldItem =>
            {
                DungeonItemDefinition definition = itemCatalogProvider.GetDefinition(yieldItem.itemId);
                string name = definition != null ? definition.DisplayName : yieldItem.itemId;
                return $"{name} x{yieldItem.amount}";
            }))
            : "없음";

        bodyText.text =
            $"{current.DisplayName}\n"
            + $"{current.Description}\n\n"
            + $"상태 {FormatState(current.State)} · {FormatIntent(current)}\n"
            + $"체력 {current.CurrentHealth}/{current.MaxHealth}\n"
            + $"허기 {FormatPercent(current.Hunger)} · 갈증 {FormatPercent(current.Thirst)}\n"
            + $"식성 {FormatDiet(species)} · 위험도 {(current.IsDangerous ? "높음" : "낮음")}\n"
            + $"도망 성향 {current.FearSensitivity:0.##} · 공격성 {current.Aggression:0.##}\n"
            + $"영역 중심 ({current.TerritoryCenter.x},{current.TerritoryCenter.y}) · 현재 위치 ({current.GridPosition.x},{current.GridPosition.y})\n"
            + $"예상 산출물 {yields}\n"
            + $"예약자 {FormatEmpty(current.ReservedByPersistentId)}\n"
            + $"사냥 지정 {(current.HuntDesignated ? (current.PriorityHunt ? "우선" : "지정됨") : "없음")}\n"
            + $"수용 상태 {FormatCaptureState(current.WildlifeId)}"
            + FormatHusbandryState(current.WildlifeId)
            + (string.IsNullOrWhiteSpace(actionMessage)
                ? string.Empty
                : $"\n{actionMessage}");
    }

    private void ToggleCapture()
    {
        if (current == null)
        {
            return;
        }

        if (captureRuntime.IsCaptured(current.WildlifeId))
        {
            actionMessage = captureRuntime.TryRelease(
                current.WildlifeId,
                out string releaseReason)
                ? "포획 동물을 방생했습니다."
                : releaseReason;
            Render();
            return;
        }

        BuildableObject pen = worldRegistry.Buildings
            .Where(building =>
                building != null
                && building.BuildingData.GetBeastPenAbility() != null)
            .OrderBy(building => Manhattan(
                building.centerPos,
                current.GridPosition))
            .FirstOrDefault();
        if (pen == null)
        {
            actionMessage = "먼저 닫힌 방 안에 야수 우리를 설치하고 문 권한을 제한해야 합니다.";
            Render();
            return;
        }

        actionMessage = captureRuntime.TryCapture(
            current,
            pen,
            out string captureReason)
            ? "가장 가까운 야수 우리로 생포·운반을 명령했습니다."
            : captureReason;
        Render();
    }

    private void ToggleSlaughter()
    {
        if (current == null
            || !husbandryQuery.TryGetAnimal(
                new WildlifeInstanceId(current.WildlifeId),
                out HusbandryAnimalState state))
        {
            actionMessage = "우리에서 관리 중인 동물만 도축 지정할 수 있습니다.";
            Render();
            return;
        }

        actionMessage = husbandryCommands.DesignateSlaughter(
            new WildlifeInstanceId(current.WildlifeId),
            !state.SlaughterDesignated,
            out AnimalHusbandryFailure failure)
                ? state.SlaughterDesignated
                    ? "도축 지정을 해제했습니다."
                    : "도축 작업으로 지정했습니다."
                : failure.Code.ToString();
        Render();
    }

    private void OpenSurgery()
    {
        if (current == null)
        {
            return;
        }

        if (!captureRuntime.IsCaptured(current.WildlifeId))
        {
            actionMessage = "수술하려면 먼저 동물을 생포해 우리에 수용해야 합니다.";
            Render();
            return;
        }

        surgeryWindowService.Open(current, transform);
    }

    private void ToggleCompanionOwnerPanel()
    {
        if (companionOwnerPanel != null)
        {
            Destroy(companionOwnerPanel);
            companionOwnerPanel = null;
            return;
        }
        if (current == null || companionRoles == null || companionCommands == null)
        {
            return;
        }

        companionOwnerPanel = new GameObject(
            "WildlifeCompanionOwnerPanel",
            typeof(RectTransform),
            typeof(Image));
        companionOwnerPanel.transform.SetParent(uiRoot.transform, false);
        RectTransform panel = companionOwnerPanel.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(390f, 420f);
        companionOwnerPanel.GetComponent<Image>().color = DungeonUiTheme.SurfaceRaised;

        TMP_Text heading = CreateText(
            "Heading",
            panel,
            21f,
            FontStyles.Bold,
            TextAlignmentOptions.Center);
        heading.rectTransform.anchorMin = new Vector2(0f, 1f);
        heading.rectTransform.anchorMax = new Vector2(1f, 1f);
        heading.rectTransform.pivot = new Vector2(0.5f, 1f);
        heading.rectTransform.offsetMin = new Vector2(16f, -58f);
        heading.rectTransform.offsetMax = new Vector2(-16f, -12f);
        heading.text = "동행 주인 선택";

        CharacterActor[] owners = worldRegistry.Characters
            .Where(actor => WildlifeCaptureRuntime.IsEligibleOwner(actor)
                && CharacterPersistentIdentity.TryGet(actor, out _))
            .OrderBy(actor => CharacterPersistentIdentity.Require(actor).Value,
                StringComparer.Ordinal)
            .ToArray();

        RectTransform viewport = CreateRect("OwnerViewport", panel);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(24f, 70f);
        viewport.offsetMax = new Vector2(-24f, -66f);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;

        RectTransform content = CreateRect("OwnerContent", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = content;

        for (int index = 0; index < owners.Length; index++)
        {
            CharacterActor owner = owners[index];
            Button select = CreateButton(
                "Owner_" + index,
                content,
                owner.Identity?.DisplayName ?? owner.name,
                () => SelectCompanionOwner(owner));
            LayoutElement row = select.gameObject.AddComponent<LayoutElement>();
            row.minHeight = 34f;
            row.preferredHeight = 34f;
        }

        Button clear = CreateButton(
            "ClearRole",
            panel,
            "동행 해제",
            ClearCompanionOwner);
        RectTransform clearRect = clear.GetComponent<RectTransform>();
        clearRect.anchorMin = clearRect.anchorMax = new Vector2(0f, 0f);
        clearRect.pivot = new Vector2(0f, 0f);
        clearRect.anchoredPosition = new Vector2(26f, 18f);
        clearRect.sizeDelta = new Vector2(150f, 38f);

        Button close = CreateButton(
            "Close",
            panel,
            "닫기",
            ToggleCompanionOwnerPanel);
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 0f);
        closeRect.pivot = new Vector2(1f, 0f);
        closeRect.anchoredPosition = new Vector2(-26f, 18f);
        closeRect.sizeDelta = new Vector2(150f, 38f);
    }

    private void SelectCompanionOwner(CharacterActor owner)
    {
        string failureReason = string.Empty;
        actionMessage = current != null
            && CharacterPersistentIdentity.TryGet(owner, out CharacterId ownerId)
            && companionCommands.TryAssignCompanion(
                current.WildlifeId,
                ownerId,
                out failureReason)
                    ? "동행 주인을 배정했습니다. 공격 준비 시간이 적용됩니다."
                    : failureReason ?? "동행 주인을 배정할 수 없습니다.";
        ToggleCompanionOwnerPanel();
        Render();
    }

    private void ClearCompanionOwner()
    {
        string failureReason = string.Empty;
        actionMessage = current != null
            && companionCommands.TryClearCompanion(
                current.WildlifeId,
                out failureReason)
                    ? "동행 역할을 해제했습니다."
                    : failureReason ?? "동행 역할을 해제할 수 없습니다.";
        ToggleCompanionOwnerPanel();
        Render();
    }

    private void ToggleHaulRole()
    {
        if (current == null || haulRoles == null || haulCommands == null)
        {
            return;
        }

        bool assigned = haulRoles.TryGetHaul(
            current.WildlifeId,
            out WildlifeHaulAssignmentSnapshot assignment);
        string failureReason = string.Empty;
        actionMessage = assigned
            ? haulCommands.TryClearHaul(current.WildlifeId, out failureReason)
                ? assignment.Phase is CapturedWildlifeHaulPhase.CargoOwned
                        or CapturedWildlifeHaulPhase.ReleasePending
                    ? "독립 운반 역할 해제를 요청했습니다. 보유 화물은 먼저 배송합니다."
                    : "독립 운반 역할을 해제했습니다."
                : failureReason
            : haulCommands.TryAssignHaul(current.WildlifeId, out failureReason)
                ? "독립 운반 역할을 배정했습니다."
                : failureReason;
        Render();
    }

    private string FormatHusbandryState(string wildlifeId)
    {
        if (!husbandryQuery.TryGetAnimal(
                new WildlifeInstanceId(wildlifeId),
                out HusbandryAnimalState state))
        {
            return string.Empty;
        }

        AnimalPenCompatibilityResult compatibility =
            husbandryQuery.EvaluatePen(state.PenId);
        string sex = state.Sex == AnimalSex.Female ? "암컷" : "수컷";
        string growth = current?.Species != null
            && state.AgeDays < current.Species.Husbandry.AdultAgeDays
                ? "새끼"
                : "성체";
        string pregnancy = state.Pregnant
            ? current?.Species?.Husbandry.LaysEggs == true
                ? " · 부화 준비 중"
                : " · 임신 중"
            : string.Empty;
        string products = state.Products != null
            ? string.Join(
                ", ",
                state.Products
                    .Where(product => product != null && product.ReadyCycles > 0)
                    .Select(product => $"{product.ItemId.Value} {product.ReadyCycles}회"))
            : string.Empty;
        return $"\n축산 {sex} · {growth} · {state.AgeDays:0.0}일"
            + $" · {(state.Tamed ? "길들임 완료" : $"길들임 {state.TamingProgress:P0}")}"
            + pregnancy
            + $"\n우리 위험 {compatibility.Risk:P0}"
            + (products.Length > 0 ? $" · 수거 대기 {products}" : string.Empty)
            + (state.SlaughterDesignated ? " · 도축 지정" : string.Empty);
    }

    private string FormatCaptureState(string wildlifeId)
    {
        if (!captureRuntime.TryGetCaptured(
                wildlifeId,
                out CapturedWildlifeState captured))
        {
            return "야생";
        }

        string state = captured.transportState switch
        {
            CapturedWildlifeTransportState.AwaitingTransport => "운반 대기",
            CapturedWildlifeTransportState.Transporting => "우리로 운반 중",
            CapturedWildlifeTransportState.Penned => "우리 수용",
            CapturedWildlifeTransportState.MovingToShow => "공연장 이동",
            CapturedWildlifeTransportState.Performing => "공연 중",
            CapturedWildlifeTransportState.ReturningToPen => "우리로 복귀",
            CapturedWildlifeTransportState.Released => "방생",
            CapturedWildlifeTransportState.Escaped => "탈출",
            _ => captured.transportState.ToString()
        };
        string role = companionRoles != null
            && companionRoles.TryGetCompanion(
                wildlifeId,
                out WildlifeCompanionAssignmentSnapshot assignment)
                ? $" · 동행 주인 {FormatOwnerName(assignment.OwnerId)}"
                : haulRoles != null
                    && haulRoles.TryGetHaul(
                        wildlifeId,
                        out WildlifeHaulAssignmentSnapshot haul)
                        ? $" · 독립 운반 {FormatHaulPhase(haul.Phase)}"
                        : string.Empty;
        return string.IsNullOrWhiteSpace(captured.lastCareStatus)
            ? state + role
            : $"{state}{role} · {captured.lastCareStatus}";
    }

    private string FormatOwnerName(CharacterId ownerId)
    {
        CharacterActor owner = worldRegistry.Characters.FirstOrDefault(actor =>
            actor != null
            && CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
            && id.Equals(ownerId));
        return owner?.Identity?.DisplayName ?? ownerId.Value;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private Button CreateButton(string name, Transform parent, string label, Action action)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(() => action?.Invoke());
        DungeonUiTheme.StyleButton(button);
        TMP_Text text = CreateText("Label", buttonObject.transform, 15f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
        text.text = label;
        return button;
    }

    private Image CreateImage(string name, Transform parent)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        return imageObject.GetComponent<Image>();
    }

    private TMP_Text CreateText(
        string name,
        Transform parent,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        fontService.Apply(text);
        text.fontSize = fontSize;
        text.fontSizeMin = Mathf.Max(11f, fontSize - 5f);
        text.fontSizeMax = fontSize;
        text.enableAutoSizing = true;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = DungeonUiTheme.TextPrimary;
        text.characterSpacing = 0f;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static string FormatState(WildlifeState state)
    {
        return state switch
        {
            WildlifeState.Idle => "배회",
            WildlifeState.Grazing => "먹이 활동",
            WildlifeState.Fleeing => "도망",
            WildlifeState.Hunted => "사냥 대상",
            WildlifeState.Retaliating => "반격",
            WildlifeState.PredatorStalking => "추적",
            WildlifeState.Dead => "죽음",
            WildlifeState.Leaving => "이탈",
            _ => state.ToString()
        };
    }

    private static string FormatIntent(WildlifeActor actor)
    {
        if (actor == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(actor.IntentReason))
        {
            return actor.IntentReason;
        }

        return actor.Intent switch
        {
            WildlifeIntent.Forage => "먹이를 찾는 중",
            WildlifeIntent.Drink => "물가로 이동",
            WildlifeIntent.Rest => "은신처에서 쉬는 중",
            WildlifeIntent.ReturnToTerritory => "영역으로 돌아가는 중",
            WildlifeIntent.HuntPrey => "먹잇감을 추적",
            WildlifeIntent.Flee => "위협을 피해 도망",
            WildlifeIntent.LeaveMap => "지역을 떠나는 중",
            _ => "영역 안을 배회"
        };
    }

    private static string FormatDiet(WildlifeSpeciesDefinition species)
    {
        return species == null
            ? "-"
            : species.Diet switch
            {
                WildlifeDietType.Herbivore => "초식",
                WildlifeDietType.Omnivore => "잡식",
                WildlifeDietType.Carnivore => "육식",
                WildlifeDietType.Scavenger => "청소동물",
                _ => species.Diet.ToString()
            };
    }

    private static string FormatPercent(float value)
    {
        return $"{Mathf.Clamp01(value) * 100f:0}%";
    }

    private static string FormatHaulPhase(CapturedWildlifeHaulPhase phase) =>
        phase switch
        {
            CapturedWildlifeHaulPhase.Idle => "대기",
            CapturedWildlifeHaulPhase.Reserved => "픽업 이동",
            CapturedWildlifeHaulPhase.CargoOwned => "배송 이동",
            CapturedWildlifeHaulPhase.ReleasePending => "해제 전 배송",
            _ => phase.ToString()
        };

    private static string FormatEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "없음" : value;
    }
}
