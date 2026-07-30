using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public sealed class ItemPileInfoPanel : UIPopUp
{
    private IWorldItemStackRuntime itemStackRuntime;
    private ISurvivalFoodRuntime survivalFoodRuntime;
    private IResourceEconomyContentCatalog resourceCatalog;
    private IUiPopupService popupService;
    private ITmpKoreanFontService fontService;
    private ISurgeryPlanningWindowService surgeryWindowService;
    private ISurgicalCorpseFreshnessRuntime corpseFreshness;

    private GameObject uiRoot;
    private RectTransform contentRoot;
    private TMP_Text titleText;
    private TMP_Text statusText;
    private Vector2Int currentPosition;
    private string selectedStackId = string.Empty;
    private IGameEventBus gameEventBus;
    private IDisposable infoFeedSubscription;

    [Inject]
    public void Construct(
        IWorldItemStackRuntime itemStackRuntime,
        ISurvivalFoodRuntime survivalFoodRuntime,
        IResourceEconomyContentCatalog resourceCatalog,
        IUiPopupService popupService,
        ITmpKoreanFontService fontService,
        ISurgeryPlanningWindowService surgeryWindowService,
        ISurgicalCorpseFreshnessRuntime corpseFreshness)
    {
        this.itemStackRuntime = itemStackRuntime ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.survivalFoodRuntime = survivalFoodRuntime ?? throw new ArgumentNullException(nameof(survivalFoodRuntime));
        this.resourceCatalog = resourceCatalog
            ?? throw new ArgumentNullException(nameof(resourceCatalog));
        this.popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
        this.fontService = fontService ?? throw new ArgumentNullException(nameof(fontService));
        this.surgeryWindowService = surgeryWindowService
            ?? throw new ArgumentNullException(nameof(surgeryWindowService));
        this.corpseFreshness = corpseFreshness
            ?? throw new ArgumentNullException(nameof(corpseFreshness));
    }

    [Inject]
    public void ConstructItemPileInfoEventBus(IGameEventBus gameEventBus)
    {
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        SubscribeToInfoFeed();
    }

    private void Start()
    {
        EnsureView();
        uiRoot.SetActive(false);
    }

    private void Update()
    {
        if (uiRoot == null || !uiRoot.activeSelf || string.IsNullOrWhiteSpace(selectedStackId))
        {
            return;
        }

        if (!TryFindSelectedStack(out _))
        {
            selectedStackId = string.Empty;
            RenderList("선택한 스택이 이동되었거나 합쳐졌습니다.");
        }
    }

    public void OnTriggerEvent(InfoFeedEvent eventType)
    {
        if (eventType.Target is not ItemPileInfoTarget target)
        {
            return;
        }

        currentPosition = target.Position;
        selectedStackId = string.Empty;
        EnsureView();
        popupService.CloseAll();
        uiRoot.SetActive(true);
        popupService.Open(this);
        RenderList();
    }

    public override void OnClose()
    {
        if (uiRoot != null)
        {
            uiRoot.SetActive(false);
        }
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

    private void EnsureView()
    {
        if (uiRoot != null)
        {
            return;
        }

        uiRoot = RuntimePanelFactoryUtility.CreateOverlayCanvas(
            "ItemPileInfoCanvas",
            new Vector2(1920f, 1080f));
        uiRoot.transform.SetParent(transform, false);
        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 730;

        GameObject panel = RuntimePanelFactoryUtility.CreatePanel(
            uiRoot.transform,
            "ItemPileInfoPanel",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(24f, -40f),
            new Vector2(520f, 620f));

        RectTransform header = CreateRect("Header", panel.transform);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.sizeDelta = new Vector2(0f, 66f);
        header.anchoredPosition = Vector2.zero;
        header.gameObject.AddComponent<Image>().color = DungeonUiTheme.SurfaceRaised;

        titleText = CreateText("Title", header, 24f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        Stretch(titleText.rectTransform, new Vector2(18f, 0f), new Vector2(-92f, 0f));

        Button close = CreateButton("Close", header, "닫기", OnClose);
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = Vector2.one;
        closeRect.pivot = Vector2.one;
        closeRect.anchoredPosition = new Vector2(-12f, -15f);
        closeRect.sizeDelta = new Vector2(68f, 36f);

        contentRoot = CreateRect("Content", panel.transform);
        contentRoot.anchorMin = Vector2.zero;
        contentRoot.anchorMax = Vector2.one;
        Stretch(contentRoot, new Vector2(16f, 58f), new Vector2(-16f, -82f));

        statusText = CreateText("Status", panel.transform, 16f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        Stretch(statusText.rectTransform, new Vector2(18f, 14f), new Vector2(-18f, -548f));
    }

    private void RenderList(string status = "")
    {
        ClearContent();
        statusText.text = status;
        if (!itemStackRuntime.TryGetPileAt(currentPosition, out WorldItemPileSnapshot pile))
        {
            titleText.text = "아이템 더미";
            statusText.text = string.IsNullOrWhiteSpace(status) ? "이 칸에는 더 이상 표시할 아이템이 없습니다." : status;
            return;
        }

        titleText.text = $"아이템 더미 ({currentPosition.x}, {currentPosition.y})";
        statusText.text = string.IsNullOrWhiteSpace(status)
            ? $"{pile.TotalQuantity}개 · {pile.KindCount}종 · {pile.TotalWeight:0.#}kg"
            : status;

        float top = 0f;
        foreach (WorldItemStackSnapshot stack in pile.Stacks)
        {
            CreateStackRow(stack, top);
            top += 58f;
        }
    }

    private void RenderDetail(string stackId)
    {
        selectedStackId = stackId;
        ClearContent();
        if (!TryFindSelectedStack(out WorldItemStackSnapshot stack))
        {
            selectedStackId = string.Empty;
            RenderList("선택한 스택이 이동되었거나 합쳐졌습니다.");
            return;
        }

        titleText.text = stack.DisplayName;
        statusText.text = $"{stack.Quantity}개 · {stack.TotalWeight:0.#}kg · {FormatState(stack)}";

        Button back = CreateButton("Back", contentRoot, "뒤로", () =>
        {
            selectedStackId = string.Empty;
            RenderList();
        });
        RectTransform backRect = back.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 1f);
        backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0f, 1f);
        backRect.anchoredPosition = new Vector2(0f, 0f);
        backRect.sizeDelta = new Vector2(88f, 38f);

        Image detailIcon = CreateImage("DetailIcon", contentRoot);
        RectTransform detailIconRect = detailIcon.GetComponent<RectTransform>();
        detailIconRect.anchorMin = detailIconRect.anchorMax = new Vector2(1f, 1f);
        detailIconRect.pivot = new Vector2(1f, 1f);
        detailIconRect.anchoredPosition = new Vector2(0f, 0f);
        detailIconRect.sizeDelta = new Vector2(58f, 58f);
        detailIcon.sprite = stack.Sprite;
        detailIcon.color = stack.Sprite != null ? Color.white : DungeonUiTheme.Accent;

        TMP_Text detail = CreateText(
            "DetailText",
            contentRoot,
            18f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft);
        Stretch(detail.rectTransform, new Vector2(0f, 64f), new Vector2(0f, -150f));
        detail.text =
            $"{stack.DisplayName}\n"
            + $"{(string.IsNullOrWhiteSpace(stack.Description) ? "설명 없음" : stack.Description)}\n\n"
            + $"수량 {stack.Quantity}\n"
            + $"단위 무게 {stack.UnitWeight:0.##}kg\n"
            + $"총 무게 {stack.TotalWeight:0.#}kg\n"
            + $"단가 {stack.UnitPrice}\n"
            + $"총 가치 {stack.TotalValue}\n"
            + $"상태 {FormatState(stack)}\n"
            + FormatResourceConsumableLine(stack)
            + FormatSurvivalStatusLine(stack)
            + FormatWasteMetadata(stack)
            + FormatCorpseFreshnessLine(stack)
            + FormatCorpseMetadata(stack)
            + $"위치 ({stack.Position.x}, {stack.Position.y})\n"
            + $"예약자 {FormatEmpty(stack.ReservedByPersistentId)}\n"
            + $"목적지 {FormatEmpty(stack.DestinationId)}\n"
            + $"운반 {(!stack.Forbidden && stack.State == WorldItemStackState.Loose ? "가능" : "불가")}";

        CreateDetailActionRow(stack);
        CreateEmergencyButcheryAction(stack);
        CreateCorpseSurgeryAction(stack);
    }

    private string FormatCorpseMetadata(WorldItemStackSnapshot stack)
    {
        if (stack == null
            || !string.Equals(
                    stack.ItemId,
                    DarkSurvivalItemDefinitions.HumanoidCorpseItemId,
                    StringComparison.Ordinal)
                && !WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(
                    stack.ItemId,
                    out _))
        {
            return string.Empty;
        }

        if (!string.Equals(
                stack.ItemId,
                DarkSurvivalItemDefinitions.HumanoidCorpseItemId,
                StringComparison.Ordinal))
        {
            return string.Empty;
        }

        string sourceName = string.IsNullOrWhiteSpace(stack.SourceDisplayName) ? "신원 불명" : stack.SourceDisplayName;
        string species = string.IsNullOrWhiteSpace(stack.SourceSpeciesTag) ? "종족 불명" : stack.SourceSpeciesTag;
        string deathReason = string.IsNullOrWhiteSpace(stack.SourceDeathReason) ? "사인 불명" : stack.SourceDeathReason;
        return $"원래 이름 {sourceName}\n종족 {species}\n사망 원인 {deathReason}\n"
            + $"비상 도축 {(stack.EmergencyButcheryAllowed ? "허용" : "금지")}\n";
    }

    private string FormatCorpseFreshnessLine(WorldItemStackSnapshot stack)
    {
        if (stack == null
            || !string.Equals(
                    stack.ItemId,
                    DarkSurvivalItemDefinitions.HumanoidCorpseItemId,
                    StringComparison.Ordinal)
                && !WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(
                    stack.ItemId,
                    out _))
        {
            return string.Empty;
        }

        return corpseFreshness.TryGetFreshness(
            stack.StackId,
            out float remaining,
            out bool isFresh)
                ? isFresh
                    ? $"신선도 {remaining / 180f:0.0}일 남음\n"
                    : "신선도 부패함 · 장기 적출 불가\n"
                : "신선도 기록 없음 · 장기 적출 불가\n";
    }

    private static string FormatWasteMetadata(WorldItemStackSnapshot stack)
    {
        if (stack == null || !stack.IsWaste)
        {
            return string.Empty;
        }

        string origin = stack.WasteOrigin switch
        {
            WasteOriginKind.Plant => "식물성",
            WasteOriginKind.Animal => "동물성",
            WasteOriginKind.Mixed => "혼합",
            WasteOriginKind.Forbidden => "금기",
            _ => "원산지 불명"
        };
        string feeding = stack.Contamination >= 80f
            ? "직접 급여 불가"
            : "식성에 맞으면 사료 사용 가능";
        return $"원산지 {origin}\n오염도 {stack.Contamination:0}/100\n{feeding}\n";
    }

    private void CreateEmergencyButcheryAction(WorldItemStackSnapshot stack)
    {
        if (stack == null || !string.Equals(stack.ItemId, DarkSurvivalItemDefinitions.HumanoidCorpseItemId, StringComparison.Ordinal))
        {
            return;
        }

        string label = stack.EmergencyButcheryAllowed ? "비상 도축 해제" : "비상 도축 허용";
        Button button = CreateButton("EmergencyButcheryAction", contentRoot, label, () =>
        {
            itemStackRuntime.SetEmergencyButcheryAllowed(stack.StackId, !stack.EmergencyButcheryAllowed);
            RenderDetail(stack.StackId);
        });
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(0f, 52f);
        rect.offsetMax = new Vector2(0f, 96f);
    }

    private void CreateCorpseSurgeryAction(WorldItemStackSnapshot stack)
    {
        if (stack == null
            || !string.Equals(
                    stack.ItemId,
                    DarkSurvivalItemDefinitions.HumanoidCorpseItemId,
                    StringComparison.Ordinal)
                && !WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(
                    stack.ItemId,
                    out _))
        {
            return;
        }

        Button button = CreateButton(
            "CorpseSurgeryAction",
            contentRoot,
            "해부·적출 계획",
            () => surgeryWindowService.Open(stack, transform));
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(0f, 100f);
        rect.offsetMax = new Vector2(0f, 144f);
    }

    private void CreateStackRow(WorldItemStackSnapshot stack, float top)
    {
        Button row = CreateButton(
            "StackRow_" + stack.StackId,
            contentRoot,
            string.Empty,
            () => RenderDetail(stack.StackId));
        RectTransform rect = row.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = new Vector2(0f, 50f);

        Image icon = CreateImage("Icon", rect);
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(10f, 0f);
        iconRect.sizeDelta = new Vector2(34f, 34f);
        icon.sprite = stack.Sprite;
        icon.color = stack.Sprite != null ? Color.white : DungeonUiTheme.Accent;

        TMP_Text name = CreateText("Name", rect, 17f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        Stretch(name.rectTransform, new Vector2(54f, 2f), new Vector2(-225f, -2f));
        name.text = $"{stack.DisplayName} x{stack.Quantity}";

        TMP_Text meta = CreateText("Meta", rect, 15f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
        Stretch(meta.rectTransform, new Vector2(265f, 2f), new Vector2(-12f, -2f));
        string reservation = string.IsNullOrWhiteSpace(stack.ReservedByPersistentId)
            ? "예약 없음"
            : "예약 " + stack.ReservedByPersistentId;
        string destination = string.IsNullOrWhiteSpace(stack.DestinationId)
            ? "목적지 -"
            : "목적지 " + stack.DestinationId;
        meta.text = $"{stack.TotalWeight:0.#}kg · {FormatState(stack)} · {reservation} · {destination}";
    }

    private void CreateDetailActionRow(WorldItemStackSnapshot stack)
    {
        string[] labels =
        {
            "운반 우선",
            "예약 해제",
            stack.Forbidden ? "허용" : "금지",
            "버리기"
        };
        Action[] actions =
        {
            () =>
            {
                itemStackRuntime.PrioritizeHaul(stack.StackId);
                RenderList("운반 우선 작업으로 올렸습니다.");
            },
            () =>
            {
                itemStackRuntime.TryClearReservation(stack.StackId);
                RenderDetail(stack.StackId);
            },
            () =>
            {
                itemStackRuntime.SetForbidden(stack.StackId, !stack.Forbidden);
                RenderDetail(stack.StackId);
            },
            () =>
            {
                itemStackRuntime.DeleteStack(stack.StackId);
                selectedStackId = string.Empty;
                RenderList("스택을 버렸습니다.");
            }
        };

        for (int i = 0; i < labels.Length; i++)
        {
            Button button = CreateButton("DetailAction_" + i, contentRoot, labels[i], actions[i].Invoke);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(i * 0.25f, 0f);
            rect.anchorMax = new Vector2((i + 1) * 0.25f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(i == 0 ? 0f : 4f, 0f);
            rect.offsetMax = new Vector2(i == labels.Length - 1 ? 0f : -4f, 44f);
        }
    }

    private bool TryFindSelectedStack(out WorldItemStackSnapshot stack)
    {
        stack = itemStackRuntime.GetStacksAt(currentPosition, includeStored: true)
            .FirstOrDefault(candidate => string.Equals(
                candidate.StackId,
                selectedStackId,
                StringComparison.Ordinal));
        return stack != null;
    }

    private void ClearContent()
    {
        if (contentRoot == null)
        {
            return;
        }

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(contentRoot.GetChild(i).gameObject);
        }
    }

    private Button CreateButton(string name, Transform parent, string label, Action action)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(() => action?.Invoke());
        DungeonUiTheme.StyleButton(button, false);
        if (!string.IsNullOrWhiteSpace(label))
        {
            TMP_Text text = CreateText("Label", buttonObject.transform, 16f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
            text.text = label;
        }

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

    private static string FormatState(WorldItemStackSnapshot stack)
    {
        if (stack.Forbidden)
        {
            return "금지";
        }

        return stack.State switch
        {
            WorldItemStackState.Loose => stack.IsReserved ? "바닥/예약" : "바닥",
            WorldItemStackState.Stored => "저장됨",
            WorldItemStackState.FacilityBuffer => "시설 버퍼",
            WorldItemStackState.Carried => "운반 중",
            WorldItemStackState.ExpeditionPacked => "원정 포장",
            _ => stack.State.ToString()
        };
    }

    private string FormatSurvivalStatusLine(WorldItemStackSnapshot stack)
    {
        if (stack == null
            || !survivalFoodRuntime.TryGetItemStatus(
                stack.StackId,
                stack.ItemId,
                out SurvivalItemStatus status))
        {
            return string.Empty;
        }

        string preservation = status.Preserved ? "보존됨" : "일반";
        string contamination = status.Contaminated ? "오염됨" : "오염 없음";
        int freshnessPercent = Mathf.RoundToInt(status.Freshness01 * 100f);
        return $"신선도 {freshnessPercent}% · {status.Label} · {preservation} · {contamination}\n";
    }

    private string FormatResourceConsumableLine(WorldItemStackSnapshot stack)
    {
        if (stack == null
            || resourceCatalog == null
            || !resourceCatalog.TryGetItem(
                stack.ItemId,
                out ResourceItemDefinitionSO item))
        {
            return string.Empty;
        }

        if (item.IsMeal)
        {
            return $"식단 {FormatMealDiet(item.MealDietClass)}"
                + $" · {FormatMealQuality(item.MealQuality)}"
                + $" · 영양 {item.Nutrition:0.#}"
                + $" · 기분 {(item.MealMood >= 0f ? "+" : string.Empty)}{item.MealMood:0.#}\n";
        }

        if (item.Kind == ResourceItemKind.Medicine)
        {
            string treatment = item.SupportsInjuryTreatment
                ? $"치료력 x{item.TreatmentPotency:0.##}"
                : "보조 약품";
            return $"{treatment}"
                + $" · 감염 -{item.InfectionReduction:0.#}"
                + $" · 해독 -{item.DetoxReduction:0.#}"
                + $" · 진정 {item.PainReduction:0.#}\n";
        }

        SubstanceDefinitionSO substance = resourceCatalog.Substances
            .FirstOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.ItemId,
                    item.ItemId,
                    StringComparison.Ordinal));
        if (substance == null)
        {
            return string.Empty;
        }

        return $"약물 {FormatSubstanceClass(substance.UseClass)}"
            + $" · 중독 {substance.AddictionChance * 100f:0.#}%"
            + $" · 과다 복용 {substance.OverdoseChance * 100f:0.#}%"
            + $" · 지속 {substance.DurationSeconds:0}s\n";
    }

    private static string FormatMealDiet(MealDietClass dietClass)
    {
        return dietClass switch
        {
            MealDietClass.Vegetarian => "채식",
            MealDietClass.Mixed => "혼합식",
            MealDietClass.Carnivore => "육식",
            _ => "비건"
        };
    }

    private static string FormatMealQuality(MealQualityTier quality)
    {
        return quality switch
        {
            MealQualityTier.Fine => "고급식",
            MealQualityTier.Lavish => "호화식",
            MealQualityTier.Preserved => "보존식",
            _ => "단순식"
        };
    }

    private static string FormatSubstanceClass(SubstanceUseClass useClass)
    {
        return useClass switch
        {
            SubstanceUseClass.NonAddictive => "비중독성",
            SubstanceUseClass.Addictive => "중독성",
            SubstanceUseClass.Recreational => "유흥성",
            _ => "의료용"
        };
    }

    private static string FormatEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }
}
