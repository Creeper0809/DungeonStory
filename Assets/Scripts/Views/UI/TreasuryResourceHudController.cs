using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;

public sealed class TreasuryHudPresentationContext
{
    public TreasuryHudPresentationContext(
        IDungeonUiCanvasProvider canvasProvider,
        ITmpKoreanFontService fonts,
        IUiClock uiClock,
        DungeonSceneRuntimeReferences sceneReferences,
        IStockCategoryDefinitionCatalog stockCategoryCatalog)
    {
        CanvasProvider = canvasProvider
            ?? throw new ArgumentNullException(nameof(canvasProvider));
        Fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
        UiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        SceneReferences = sceneReferences
            ?? throw new ArgumentNullException(nameof(sceneReferences));
        StockCategoryCatalog = stockCategoryCatalog
            ?? throw new ArgumentNullException(nameof(stockCategoryCatalog));
    }

    public IDungeonUiCanvasProvider CanvasProvider { get; }
    public ITmpKoreanFontService Fonts { get; }
    public IUiClock UiClock { get; }
    public DungeonSceneRuntimeReferences SceneReferences { get; }
    public IStockCategoryDefinitionCatalog StockCategoryCatalog { get; }
}

public sealed class TreasuryHudEconomyContext
{
    public TreasuryHudEconomyContext(
        IGameClock gameClock,
        IGameSessionStateProvider gameDataProvider,
        IWorldItemStackRuntime items,
        IEconomyTransactionLedger ledger)
    {
        GameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        GameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        Items = items ?? throw new ArgumentNullException(nameof(items));
        Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
    }

    public IGameClock GameClock { get; }
    public IGameSessionStateProvider GameDataProvider { get; }
    public IWorldItemStackRuntime Items { get; }
    public IEconomyTransactionLedger Ledger { get; }
}

public sealed class TreasuryHudContractContext
{
    public TreasuryHudContractContext(
        IEmploymentContractRuntime employment,
        IAutoProcurementRuntime procurement,
        IPaidFacilityContractRuntime paidContracts)
    {
        Employment = employment
            ?? throw new ArgumentNullException(nameof(employment));
        Procurement = procurement
            ?? throw new ArgumentNullException(nameof(procurement));
        PaidContracts = paidContracts
            ?? throw new ArgumentNullException(nameof(paidContracts));
    }

    public IEmploymentContractRuntime Employment { get; }
    public IAutoProcurementRuntime Procurement { get; }
    public IPaidFacilityContractRuntime PaidContracts { get; }
}

public sealed class TreasuryResourceHudController :
    IStartable,
    ITickable,
    IDisposable
{
    private static readonly StockCategory[] PrimaryCategories =
    {
        StockCategory.Food,
        StockCategory.Water,
        StockCategory.General,
        StockCategory.Medicine,
        StockCategory.Ammunition,
        StockCategory.Fuel,
        StockCategory.Mana,
        StockCategory.Biological,
        StockCategory.Knowledge,
        StockCategory.Blueprint
    };

    private readonly IDungeonUiCanvasProvider canvasProvider;
    private readonly ITmpKoreanFontService fonts;
    private readonly IUiClock uiClock;
    private readonly IGameClock gameClock;
    private readonly IGameSessionStateProvider gameDataProvider;
    private readonly IWorldItemStackRuntime items;
    private readonly IEconomyTransactionLedger ledger;
    private readonly IEmploymentContractRuntime employment;
    private readonly IAutoProcurementRuntime procurement;
    private readonly IPaidFacilityContractRuntime paidContracts;
    private readonly DungeonSceneRuntimeReferences sceneReferences;
    private readonly IStockCategoryDefinitionCatalog stockCategoryCatalog;
    private readonly Dictionary<StockCategory, TMP_Text> stockLabels =
        new Dictionary<StockCategory, TMP_Text>();

    private GameObject hudRoot;
    private GameObject financeRoot;
    private TMP_Text goldLabel;
    private TMP_Text financeDetails;
    private GameObject legacyMoneyPanel;
    private float nextRefreshAt;
    private int lastItemVersion = -1;
    private int lastMoney = int.MinValue;
    private int lastDay = -1;

    public TreasuryResourceHudController(
        TreasuryHudPresentationContext presentation,
        TreasuryHudEconomyContext economy,
        TreasuryHudContractContext contracts)
    {
        presentation = presentation
            ?? throw new ArgumentNullException(nameof(presentation));
        economy = economy ?? throw new ArgumentNullException(nameof(economy));
        contracts = contracts ?? throw new ArgumentNullException(nameof(contracts));
        canvasProvider = presentation.CanvasProvider;
        fonts = presentation.Fonts;
        uiClock = presentation.UiClock;
        sceneReferences = presentation.SceneReferences;
        stockCategoryCatalog = presentation.StockCategoryCatalog;
        gameClock = economy.GameClock;
        gameDataProvider = economy.GameDataProvider;
        items = economy.Items;
        ledger = economy.Ledger;
        employment = contracts.Employment;
        procurement = contracts.Procurement;
        paidContracts = contracts.PaidContracts;
    }

    public void Start()
    {
        HideLegacyMoneyPanel();
        Canvas canvas = canvasProvider.GetOrCreateCanvas();
        hudRoot = CreateHud(canvas.transform);
        financeRoot = CreateFinanceWindow(canvas.transform);
        financeRoot.SetActive(false);
        Refresh(force: true);
    }

    public void Tick()
    {
        if (uiClock.Time < nextRefreshAt)
        {
            return;
        }

        nextRefreshAt = uiClock.Time + 0.4f;
        Refresh(force: false);
    }

    public void Dispose()
    {
        if (legacyMoneyPanel != null)
        {
            legacyMoneyPanel.SetActive(true);
        }

        if (hudRoot != null)
        {
            UnityEngine.Object.Destroy(hudRoot);
        }

        if (financeRoot != null)
        {
            UnityEngine.Object.Destroy(financeRoot);
        }
    }

    private void HideLegacyMoneyPanel()
    {
        TMP_Text legacyMoneyText = sceneReferences.UIManager != null
            ? sceneReferences.UIManager.holdingMoneyText
            : null;
        legacyMoneyPanel = legacyMoneyText != null
            ? legacyMoneyText.transform.parent?.gameObject
            : null;
        legacyMoneyPanel?.SetActive(false);
    }

    private GameObject CreateHud(Transform parent)
    {
        GameObject root = CreateUiObject("TreasuryResourceHud", parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-18f, -92f);
        rect.sizeDelta = new Vector2(238f, 0f);

        Image background = root.AddComponent<Image>();
        background.color = DungeonUiTheme.Panel;
        VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 3f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        ContentSizeFitter fitter = root.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text heading = CreateText(root.transform, "Heading", 15f);
        heading.text = "물리 재고";
        heading.fontStyle = FontStyles.Bold;
        heading.color = DungeonUiTheme.TextSecondary;
        SetPreferredHeight(heading.gameObject, 22f);

        foreach (StockCategory category in PrimaryCategories)
        {
            TMP_Text label = CreateText(
                root.transform,
                $"Stock_{category}",
                15f);
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.text = $"{stockCategoryCatalog.GetShortName(category)}  0";
            SetPreferredHeight(label.gameObject, 21f);
            stockLabels[category] = label;
        }

        Button goldButton = CreateButton(
            root.transform,
            "TreasuryButton",
            "금고  0");
        SetPreferredHeight(goldButton.gameObject, 38f);
        goldLabel = goldButton.GetComponentInChildren<TMP_Text>(true);
        goldButton.onClick.AddListener(ToggleFinanceWindow);
        return root;
    }

    private GameObject CreateFinanceWindow(Transform parent)
    {
        GameObject root = CreateUiObject("TreasuryFinanceWindow", parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(720f, 620f);
        Image background = root.AddComponent<Image>();
        background.color = DungeonUiTheme.Panel;

        VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 18, 18);
        layout.spacing = 10f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        GameObject header = CreateUiObject("Header", root.transform);
        HorizontalLayoutGroup headerLayout =
            header.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 10f;
        headerLayout.childControlHeight = true;
        headerLayout.childControlWidth = true;
        headerLayout.childForceExpandHeight = true;
        headerLayout.childForceExpandWidth = false;
        SetPreferredHeight(header, 48f);

        TMP_Text title = CreateText(header.transform, "Title", 28f);
        title.text = "금고";
        title.fontStyle = FontStyles.Bold;
        LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
        titleLayout.flexibleWidth = 1f;

        Button close = CreateButton(header.transform, "Close", "X");
        SetPreferredWidth(close.gameObject, 48f);
        close.onClick.AddListener(() => root.SetActive(false));

        GameObject scroll = CreateUiObject("Scroll", root.transform);
        LayoutElement scrollLayout = scroll.AddComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1f;
        Image scrollImage = scroll.AddComponent<Image>();
        scrollImage.color = DungeonUiTheme.SurfaceMuted;
        ScrollRect scrollRect = scroll.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        GameObject viewport = CreateUiObject("Viewport", scroll.transform);
        Stretch(viewport.GetComponent<RectTransform>(), 8f);
        viewport.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        GameObject content = CreateUiObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        VerticalLayoutGroup contentLayout =
            content.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(12, 12, 12, 12);
        contentLayout.spacing = 6f;
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandHeight = false;
        content.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        financeDetails = CreateText(content.transform, "Details", 17f);
        financeDetails.alignment = TextAlignmentOptions.TopLeft;
        financeDetails.textWrappingMode = TextWrappingModes.Normal;
        financeDetails.color = DungeonUiTheme.TextPrimary;
        ContentSizeFitter detailsFitter =
            financeDetails.gameObject.AddComponent<ContentSizeFitter>();
        detailsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRect;
        return root;
    }

    private void ToggleFinanceWindow()
    {
        if (financeRoot == null)
        {
            return;
        }

        bool show = !financeRoot.activeSelf;
        financeRoot.SetActive(show);
        if (show)
        {
            financeRoot.transform.SetAsLastSibling();
            RefreshFinance();
        }
    }

    private void Refresh(bool force)
    {
        if (!gameDataProvider.TryGetSessionState(out GameSessionState gameData)
            || gameData?.holdingMoney == null)
        {
            return;
        }

        int money = gameData.holdingMoney.Value;
        int day = gameData.day?.Value ?? 1;
        int itemVersion = items.ItemStackVersion;
        if (!force
            && itemVersion == lastItemVersion
            && money == lastMoney
            && day == lastDay)
        {
            if (financeRoot != null && financeRoot.activeSelf)
            {
                RefreshFinance();
            }

            return;
        }

        Dictionary<StockCategory, int> amounts =
            PrimaryCategories.ToDictionary(category => category, _ => 0);
        foreach (WorldItemStackSnapshot stack in items.GetAllStacks())
        {
            if (stack != null
                && amounts.ContainsKey(stack.StockCategory))
            {
                amounts[stack.StockCategory] += Mathf.Max(0, stack.Quantity);
            }
        }

        foreach ((StockCategory category, TMP_Text label) in stockLabels)
        {
            label.text =
                $"{stockCategoryCatalog.GetShortName(category)}  {amounts[category]:N0}";
        }

        if (goldLabel != null)
        {
            goldLabel.text = $"금고  {money:N0}";
        }

        lastItemVersion = itemVersion;
        lastMoney = money;
        lastDay = day;
        if (financeRoot != null && financeRoot.activeSelf)
        {
            RefreshFinance();
        }
    }

    private void RefreshFinance()
    {
        if (financeDetails == null
            || !gameDataProvider.TryGetSessionState(out GameSessionState gameData)
            || gameData?.holdingMoney == null)
        {
            return;
        }

        int day = Mathf.Max(1, gameData.day?.Value ?? 1);
        float todayStart = Mathf.Max(0f, (day - 1) * 180f);
        int income = ledger.SumSince(todayStart, income: true);
        int expense = ledger.SumSince(todayStart, income: false);
        int employeeDaily = employment.WageStates
            .Where(state => state.active
                && state.contractKind == EmploymentContractKind.Employee)
            .Sum(state => employment.GetDailyCost(state.characterId));
        int mercenaryDaily = employment.MercenaryContracts
            .Where(contract => contract.active)
            .Sum(contract => employment.GetDailyCost(contract.characterId));
        int employmentForecast = employment.ForecastCost(3);
        int contractForecast = paidContracts.ForecastCost(3);
        int projected = Mathf.Max(
            0,
            gameData.holdingMoney.Value
            - employmentForecast
            - contractForecast
            - procurement.DailyBudget * 3);

        string recent = string.Join(
            "\n",
            ledger.Records
                .Where(record => record != null)
                .OrderByDescending(record => record.gameTime)
                .Take(8)
                .Select(record =>
                    $"{(record.amount >= 0 ? "+" : string.Empty)}{record.amount:N0}  "
                    + $"{FormatKind(record.kind)}"
                    + (record.succeeded
                        ? string.Empty
                        : $" · 실패: {record.failureReason}")));
        if (recent.Length == 0)
        {
            recent = "아직 거래 기록이 없습니다.";
        }

        financeDetails.text =
            $"보유 골드  {gameData.holdingMoney.Value:N0}\n"
            + $"오늘 수입  +{income:N0}\n"
            + $"오늘 지출  -{expense:N0}\n\n"
            + $"직원 임금  {employeeDaily:N0}/일\n"
            + $"용병 계약  {mercenaryDaily:N0}/일\n"
            + $"유료 시설  {paidContracts.ForecastCost(1):N0}/일\n\n"
            + $"자동 구매 예산  {procurement.DailyBudget:N0}/일\n"
            + $"플레이어 보호액  {procurement.MinimumReserve:N0}\n"
            + $"적용 보호 자금  {procurement.ProtectedFunds:N0}\n"
            + $"3일 고정 지출  {(employmentForecast + contractForecast):N0}\n"
            + $"3일 보수적 예상 잔액  {projected:N0}\n\n"
            + $"최근 거래\n{recent}";
    }

    private static string FormatKind(EconomyTransactionKind kind)
    {
        return kind switch
        {
            EconomyTransactionKind.EmployeeWage => "직원 임금",
            EconomyTransactionKind.MercenaryAdvance => "용병 선불",
            EconomyTransactionKind.MercenaryRenewal => "용병 갱신",
            EconomyTransactionKind.AutoProcurement => "자동 구매",
            EconomyTransactionKind.ShopPurchase => "상점 구매",
            EconomyTransactionKind.PaidFacilityContract => "시설 계약",
            EconomyTransactionKind.PaidFacilityUse => "시설 이용",
            EconomyTransactionKind.PaidFacilityOrder => "시설 주문",
            EconomyTransactionKind.ReforgePrecision => "정밀 재단조",
            EconomyTransactionKind.EquipmentOverclock => "장비 오버클럭",
            EconomyTransactionKind.FacilityOverclock => "시설 오버클럭",
            EconomyTransactionKind.TreasuryDefenseShot => "금고 방어",
            EconomyTransactionKind.Bribe => "뇌물",
            EconomyTransactionKind.ExpeditionFieldFundAllocation => "현장 자금",
            EconomyTransactionKind.ExpeditionFieldFundReturn => "현장 자금 반환",
            _ => kind.ToString()
        };
    }

    private Button CreateButton(
        Transform parent,
        string name,
        string label)
    {
        GameObject root = CreateUiObject(name, parent);
        Image image = root.AddComponent<Image>();
        image.color = DungeonUiTheme.SurfaceRaised;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text text = CreateText(root.transform, "Label", 16f);
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform, 4f);
        DungeonUiTheme.StyleButton(button);
        return button;
    }

    private TMP_Text CreateText(
        Transform parent,
        string name,
        float fontSize)
    {
        GameObject root = CreateUiObject(name, parent);
        TMP_Text text = root.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.characterSpacing = 0f;
        text.color = DungeonUiTheme.TextPrimary;
        text.raycastTarget = false;
        fonts.Apply(text);
        return text;
    }

    private static GameObject CreateUiObject(
        string name,
        Transform parent)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.layer = 5;
        root.transform.SetParent(parent, false);
        return root;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetPreferredHeight(GameObject target, float height)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>()
            ?? target.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.minHeight = height;
    }

    private static void SetPreferredWidth(GameObject target, float width)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>()
            ?? target.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minWidth = width;
    }
}
