using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ShopFeatureSurfaceModel
{
    public bool IsAvailable { get; set; }
    public string UnavailableMessage { get; set; } = string.Empty;
    public int OfferDay { get; set; }
    public int CurrentMoney { get; set; } = -1;
    public IReadOnlyList<ShopFeatureOfferRow> DailyOffers { get; set; }
        = Array.Empty<ShopFeatureOfferRow>();
    public IReadOnlyList<ShopFeatureOfferRow> BasicOffers { get; set; }
        = Array.Empty<ShopFeatureOfferRow>();
    public IReadOnlyList<ShopFeatureRetailRow> RetailShops { get; set; }
        = Array.Empty<ShopFeatureRetailRow>();
    public int ProcurementDailyBudget { get; set; }
    public int ProcurementMinimumReserve { get; set; }
    public int ProcurementProtectedFunds { get; set; }
    public IReadOnlyList<ShopProcurementRuleRow> ProcurementRules { get; set; }
        = Array.Empty<ShopProcurementRuleRow>();
    public IReadOnlyList<ShopProcurementResultRow> ProcurementResults { get; set; }
        = Array.Empty<ShopProcurementResultRow>();
}

public sealed class ShopFeatureOfferRow
{
    public int Index { get; set; }
    public string ActionId { get; set; } = string.Empty;
    public string PurchaseKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string OfferTypeId { get; set; } = string.Empty;
    public int DataId { get; set; } = -1;
    public int Cost { get; set; }
    public bool IsPurchased { get; set; }
    public bool IsWishlisted { get; set; }
}

public sealed class ShopProcurementRuleRow
{
    public StockCategory Category { get; set; }
    public int TargetQuantity { get; set; }
    public int MaximumUnitPrice { get; set; }
    public int DailyMaximumQuantity { get; set; }
    public bool Enabled { get; set; }
}

public sealed class ShopProcurementResultRow
{
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool Purchased { get; set; }
}

public sealed class ShopFeatureRetailRow
{
    public int RuntimeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public IReadOnlyList<ShopFeatureProductRow> Products { get; set; }
        = Array.Empty<ShopFeatureProductRow>();
}

public sealed class ShopFeatureProductRow
{
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public readonly struct ShopFeatureCommandResult
{
    public ShopFeatureCommandResult(bool succeeded, string message)
    {
        Succeeded = succeeded;
        Message = message ?? string.Empty;
    }

    public bool Succeeded { get; }
    public string Message { get; }
}

public interface IShopFeatureQueryService
{
    ShopFeatureSurfaceModel Capture();
}

public interface IShopFeatureCommandService
{
    bool WasPurchased(string purchaseKey);
    ShopFeatureCommandResult PurchaseDaily(int index, string purchaseKey);
    ShopFeatureCommandResult PurchaseBasic(int index, string purchaseKey);
    ShopFeatureCommandResult AdjustProcurementBudget(
        int dailyBudgetDelta,
        int minimumReserveDelta);
    ShopFeatureCommandResult CycleStockTarget(StockCategory category);
    ShopFeatureCommandResult ToggleWishlist(
        string offerTypeId,
        int dataId,
        int maximumPrice,
        string displayName);
}

public sealed class ShopFeatureQueryService : IShopFeatureQueryService
{
    private readonly IDailyFacilityShopRuntimeProvider runtimeProvider;
    private readonly IGameDataProvider gameDataProvider;
    private readonly IShopFeatureCommandService commandService;
    private readonly IRetailWorldQuery retailWorld;
    private readonly IAutoProcurementRuntime autoProcurement;

    public ShopFeatureQueryService(
        IDailyFacilityShopRuntimeProvider runtimeProvider,
        IGameDataProvider gameDataProvider,
        IShopFeatureCommandService commandService,
        IRetailWorldQuery retailWorld,
        IAutoProcurementRuntime autoProcurement)
    {
        this.runtimeProvider = runtimeProvider
            ?? throw new ArgumentNullException(nameof(runtimeProvider));
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.commandService = commandService
            ?? throw new ArgumentNullException(nameof(commandService));
        this.retailWorld = retailWorld
            ?? throw new ArgumentNullException(nameof(retailWorld));
        this.autoProcurement = autoProcurement
            ?? throw new ArgumentNullException(nameof(autoProcurement));
    }

    public ShopFeatureSurfaceModel Capture()
    {
        if (!runtimeProvider.TryGetRuntime(out DailyFacilityShopRuntime runtime))
        {
            return new ShopFeatureSurfaceModel
            {
                UnavailableMessage = "시설 상점 런타임이 현재 씬에 없습니다."
            };
        }

        return new ShopFeatureSurfaceModel
        {
            IsAvailable = true,
            OfferDay = runtime.CurrentOfferDay,
            CurrentMoney = ResolveMoney(),
            DailyOffers = runtime.CurrentDailyOffers
                .Select((offer, index) => new { Offer = offer, Index = index })
                .OrderBy(entry => string.Equals(
                    entry.Offer?.OfferTypeId,
                    FacilityShopOfferTypeIds.Blueprint,
                    StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(entry => entry.Index)
                .Take(ShopFeatureSurfacePresenter.MaxVisibleCardsPerSection)
                .Select(entry => CreateRow(entry.Offer, entry.Index, runtime.CurrentOfferDay, false))
                .ToArray(),
            BasicOffers = runtime.CurrentBasicPurchaseOffers
                .Take(ShopFeatureSurfacePresenter.MaxVisibleCardsPerSection)
                .Select((offer, index) => CreateRow(offer, index, runtime.CurrentOfferDay, true))
                .ToArray(),
            RetailShops = retailWorld.RetailFacilities
                .Select((facility) => facility as BuildableObject)
                .Where((building) => building != null && !building.isDestroy)
                .OrderByDescending((building) => ((IRetailFacility)building).CurrentStock)
                .Take(ShopFeatureSurfacePresenter.MaxVisibleCardsPerSection)
                .Select(CreateRetailRow)
                .ToArray(),
            ProcurementDailyBudget = autoProcurement.DailyBudget,
            ProcurementMinimumReserve = autoProcurement.MinimumReserve,
            ProcurementProtectedFunds = autoProcurement.ProtectedFunds,
            ProcurementRules = CreateProcurementRows(),
            ProcurementResults = autoProcurement.LastResults
                .OrderByDescending(result => result.purchased)
                .ThenBy(result => result.ruleId, StringComparer.Ordinal)
                .Take(ShopFeatureSurfacePresenter.MaxVisibleCardsPerSection)
                .Select(result => new ShopProcurementResultRow
                {
                    Title = string.IsNullOrWhiteSpace(result.itemLabel)
                        ? result.ruleId
                        : result.itemLabel,
                    Detail = result.purchased
                        ? $"{result.quantity}개 구매 · {result.cost:N0}골드 · 하차장 배송"
                        : $"건너뜀 · {result.reason}",
                    Purchased = result.purchased
                })
                .ToArray()
        };
    }

    private ShopFeatureOfferRow CreateRow(
        FacilityShopOffer offer,
        int index,
        int offerDay,
        bool basic)
    {
        string scope = basic ? "Basic" : "Daily";
        string purchaseKey = basic
            ? $"shop:basic:{offer?.OfferTypeId}:{offer?.DataId}"
            : $"shop:daily:{offerDay}:{index}:{offer?.OfferTypeId}:{offer?.DataId}";
        string detail = offer == null
            ? "상품 정보 없음"
            : $"{offer.TypeDisplayName} / {offer.Rarity} / 비용 {offer.Cost} / {offer.Star}성" +
              (offer.IsBasicPurchase ? " / 기본 구매" : string.Empty);

        return new ShopFeatureOfferRow
        {
            Index = index,
            ActionId = $"P0Action_Shop{scope}_{index}",
            PurchaseKey = purchaseKey,
            Title = offer?.DisplayName ?? "알 수 없는 상품",
            Detail = detail,
            OfferTypeId = offer?.OfferTypeId ?? string.Empty,
            DataId = offer?.DataId ?? -1,
            Cost = Mathf.Max(0, offer?.Cost ?? 0),
            IsPurchased = commandService.WasPurchased(purchaseKey),
            IsWishlisted = autoProcurement.WishlistRules.Any(rule =>
                rule.enabled
                && string.Equals(
                    rule.offerTypeId,
                    offer?.OfferTypeId,
                    StringComparison.Ordinal)
                && rule.dataId == (offer?.DataId ?? -1))
        };
    }

    private IReadOnlyList<ShopProcurementRuleRow> CreateProcurementRows()
    {
        StockCategory[] categories =
        {
            StockCategory.Food,
            StockCategory.Water,
            StockCategory.General,
            StockCategory.Medicine,
            StockCategory.Ammunition,
            StockCategory.Fuel,
            StockCategory.Mana
        };
        return categories.Select(category =>
        {
            AutoProcurementRule rule = autoProcurement.StockRules
                .FirstOrDefault(candidate =>
                    candidate != null
                    && candidate.category == category);
            return new ShopProcurementRuleRow
            {
                Category = category,
                TargetQuantity = Mathf.Max(0, rule?.targetQuantity ?? 0),
                MaximumUnitPrice = Mathf.Max(
                    1,
                    rule?.maximumUnitPrice
                    ?? ResolveDefaultMaximumPrice(category)),
                DailyMaximumQuantity = Mathf.Max(
                    1,
                    rule?.dailyMaximumQuantity ?? 25),
                Enabled = rule?.enabled ?? false
            };
        }).ToArray();
    }

    private static int ResolveDefaultMaximumPrice(StockCategory category)
    {
        return StockCategoryCatalog.TryGet(
            category,
            out StockCategoryDefinition definition)
            ? Mathf.Max(1, definition.DailyUnitCost * 2)
            : 20;
    }

    private int ResolveMoney()
    {
        return gameDataProvider.TryGetGameData(out GameData gameData)
            && gameData != null
            && gameData.holdingMoney != null
            ? gameData.holdingMoney.Value
            : -1;
    }

    private static ShopFeatureRetailRow CreateRetailRow(BuildableObject building)
    {
        IRetailFacility shop = (IRetailFacility)building;
        string checkout = shop.RequiresStaffedCheckout
            ? shop.HasServingWorker ? "직원 계산대 운영" : "직원 필요"
            : "셀프 계산대";
        return new ShopFeatureRetailRow
        {
            RuntimeId = building.GetInstanceID(),
            Title = GetBuildingName(building),
            Detail =
                $"재고 {shop.CurrentStock}/{shop.MaxInternalStock} / 대기 {shop.WaitingCheckoutCount}명 / " +
                $"{checkout} / 가격 x{shop.CurrentPriceMultiplier:0.0} / " +
                $"절도 위험 {shop.GetCheckoutCrimeChance(1) * 100f:0.#}%",
            Products = shop.ProductSnapshots
                .Take(ShopFeatureSurfacePresenter.MaxVisibleCardsPerSection)
                .Select(product => new ShopFeatureProductRow
                {
                    Title = string.IsNullOrWhiteSpace(product.Name)
                        ? $"상품 {product.Id}"
                        : product.Name,
                    Detail = $"판매가 {product.Price} / 수량 {product.Quantity}"
                })
                .ToArray()
        };
    }

    private static string GetBuildingName(BuildableObject building)
    {
        return building.BuildingData != null
            && !string.IsNullOrWhiteSpace(building.BuildingData.objectName)
            ? building.BuildingData.objectName
            : building.name;
    }
}

public sealed class ShopFeatureCommandService : IShopFeatureCommandService
{
    private readonly HashSet<string> purchasedKeys =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly IDailyFacilityShopRuntimeProvider runtimeProvider;
    private readonly IGameDataProvider gameDataProvider;
    private readonly IAutoProcurementRuntime autoProcurement;

    public ShopFeatureCommandService(
        IDailyFacilityShopRuntimeProvider runtimeProvider,
        IGameDataProvider gameDataProvider,
        IAutoProcurementRuntime autoProcurement)
    {
        this.runtimeProvider = runtimeProvider
            ?? throw new ArgumentNullException(nameof(runtimeProvider));
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.autoProcurement = autoProcurement
            ?? throw new ArgumentNullException(nameof(autoProcurement));
    }

    public bool WasPurchased(string purchaseKey)
    {
        return !string.IsNullOrWhiteSpace(purchaseKey)
            && purchasedKeys.Contains(purchaseKey);
    }

    public ShopFeatureCommandResult PurchaseDaily(int index, string purchaseKey)
    {
        return Purchase(
            purchaseKey,
            (DailyFacilityShopRuntime runtime, GameData gameData, out FacilityShopPurchaseResult result) =>
                runtime.TryPurchaseDailyOffer(index, gameData, out result),
            "구매");
    }

    public ShopFeatureCommandResult PurchaseBasic(int index, string purchaseKey)
    {
        return Purchase(
            purchaseKey,
            (DailyFacilityShopRuntime runtime, GameData gameData, out FacilityShopPurchaseResult result) =>
                runtime.TryPurchaseBasicOffer(index, gameData, out result),
            "기본 구매");
    }

    public ShopFeatureCommandResult AdjustProcurementBudget(
        int dailyBudgetDelta,
        int minimumReserveDelta)
    {
        int daily = Mathf.Max(
            0,
            autoProcurement.DailyBudget + dailyBudgetDelta);
        int reserve = Mathf.Max(
            0,
            autoProcurement.MinimumReserve + minimumReserveDelta);
        autoProcurement.ConfigureBudget(daily, reserve);
        return new ShopFeatureCommandResult(
            true,
            $"자동 구매 예산 {daily:N0} / 최소 보호액 {reserve:N0}");
    }

    public ShopFeatureCommandResult CycleStockTarget(StockCategory category)
    {
        int[] targets = { 0, 10, 25, 50, 100 };
        AutoProcurementRule current = autoProcurement.StockRules
            .FirstOrDefault(rule =>
                rule != null && rule.category == category);
        int currentTarget = Mathf.Max(0, current?.targetQuantity ?? 0);
        int nextTarget = targets.FirstOrDefault(value => value > currentTarget);
        if (nextTarget <= currentTarget)
        {
            nextTarget = 0;
        }

        int defaultPrice = StockCategoryCatalog.TryGet(
            category,
            out StockCategoryDefinition definition)
            ? Mathf.Max(1, definition.DailyUnitCost * 2)
            : 20;
        autoProcurement.UpsertStockRule(new AutoProcurementRule
        {
            ruleId = $"auto-stock:{category}",
            category = category,
            targetQuantity = nextTarget,
            maximumUnitPrice = Mathf.Max(
                1,
                current?.maximumUnitPrice ?? defaultPrice),
            dailyMaximumQuantity = Mathf.Max(
                1,
                current?.dailyMaximumQuantity ?? 25),
            priority = current?.priority ?? 0,
            enabled = nextTarget > 0
        });
        return new ShopFeatureCommandResult(
            true,
            $"{StockCategoryCatalog.GetDisplayName(category)} 목표 재고 "
            + (nextTarget > 0 ? $"{nextTarget}개" : "사용 안 함"));
    }

    public ShopFeatureCommandResult ToggleWishlist(
        string offerTypeId,
        int dataId,
        int maximumPrice,
        string displayName)
    {
        ProcurementWishlistRule current =
            autoProcurement.WishlistRules.FirstOrDefault(rule =>
                rule != null
                && string.Equals(
                    rule.offerTypeId,
                    offerTypeId,
                    StringComparison.Ordinal)
                && rule.dataId == dataId);
        bool enabled = !(current?.enabled ?? false);
        autoProcurement.UpsertWishlistRule(new ProcurementWishlistRule
        {
            ruleId = $"wishlist:{offerTypeId}:{dataId}",
            offerTypeId = offerTypeId,
            dataId = dataId,
            maximumPrice = Mathf.Max(
                1,
                current?.maximumPrice ?? maximumPrice),
            maximumOwned = Mathf.Max(1, current?.maximumOwned ?? 1),
            priority = current?.priority ?? 0,
            enabled = enabled
        });
        return new ShopFeatureCommandResult(
            true,
            $"{displayName} 자동 구매 찜 {(enabled ? "등록" : "해제")}");
    }

    private ShopFeatureCommandResult Purchase(
        string purchaseKey,
        PurchaseOperation operation,
        string label)
    {
        if (string.IsNullOrWhiteSpace(purchaseKey))
        {
            return new ShopFeatureCommandResult(false, $"{label} 실패: 상품 식별자가 없습니다.");
        }

        if (purchasedKeys.Contains(purchaseKey))
        {
            return new ShopFeatureCommandResult(false, "이미 구매한 상품입니다.");
        }

        if (!runtimeProvider.TryGetRuntime(out DailyFacilityShopRuntime runtime))
        {
            return new ShopFeatureCommandResult(false, $"{label} 실패: 시설 상점이 준비되지 않았습니다.");
        }

        if (!gameDataProvider.TryGetGameData(out GameData gameData) || gameData == null)
        {
            return new ShopFeatureCommandResult(false, $"{label} 실패: 게임 자금 데이터가 없습니다.");
        }

        int beforeMoney = gameData.holdingMoney != null
            ? gameData.holdingMoney.Value
            : -1;
        bool succeeded = operation(runtime, gameData, out FacilityShopPurchaseResult result);
        int afterMoney = gameData.holdingMoney != null
            ? gameData.holdingMoney.Value
            : -1;
        if (succeeded)
        {
            purchasedKeys.Add(purchaseKey);
        }

        return new ShopFeatureCommandResult(
            succeeded,
            $"{(succeeded ? $"{label} 성공" : $"{label} 실패")}: {result.message} / " +
            $"자금 {FormatMoney(beforeMoney)} -> {FormatMoney(afterMoney)}");
    }

    private static string FormatMoney(int money)
    {
        return money >= 0 ? money.ToString() : "없음";
    }

    private delegate bool PurchaseOperation(
        DailyFacilityShopRuntime runtime,
        GameData gameData,
        out FacilityShopPurchaseResult result);
}

public sealed class ShopFeatureSurfacePresenter : IFeatureSurfaceTabPresenter
{
    internal const int MaxVisibleCardsPerSection = 8;
    private const float CardHeight = 86f;

    private readonly IShopFeatureQueryService queryService;
    private readonly IShopFeatureCommandService commandService;
    private int selectedShopId;

    public ShopFeatureSurfacePresenter(
        IShopFeatureQueryService queryService,
        IShopFeatureCommandService commandService)
    {
        this.queryService = queryService
            ?? throw new ArgumentNullException(nameof(queryService));
        this.commandService = commandService
            ?? throw new ArgumentNullException(nameof(commandService));
    }

    public TabId Id => TabId.Shop;

    public void Present(IFeatureSurfaceView view)
    {
        if (view == null)
        {
            throw new ArgumentNullException(nameof(view));
        }

        ShopFeatureSurfaceModel model = queryService.Capture();
        if (!model.IsAvailable)
        {
            view.AddLabel(model.UnavailableMessage, 20f, 44f);
            return;
        }

        view.AddSection(
            "자동 조달",
            $"일일 예산 {model.ProcurementDailyBudget:N0} / 최소 보호액 "
            + $"{model.ProcurementMinimumReserve:N0} / 적용 보호 자금 "
            + $"{model.ProcurementProtectedFunds:N0}");
        view.AddDataCard(
            "P3Action_ProcurementBudgetUp",
            "일일 구매 예산",
            $"현재 {model.ProcurementDailyBudget:N0}골드 · 상점 갱신 뒤 하루 한 번 처리",
            "+100",
            () => Execute(
                view,
                commandService.AdjustProcurementBudget(100, 0)),
            66f);
        view.AddDataCard(
            "P3Action_ProcurementBudgetDown",
            "일일 구매 예산 줄이기",
            "자동 구매가 임금과 계약비보다 먼저 자금을 쓰지 않습니다.",
            "-100",
            () => Execute(
                view,
                commandService.AdjustProcurementBudget(-100, 0)),
            66f);
        view.AddDataCard(
            "P3Action_ProcurementReserve",
            "최소 보호액",
            $"현재 {model.ProcurementMinimumReserve:N0}골드 · 3일 고정 지출보다 낮게 적용되지 않음",
            "+100",
            () => Execute(
                view,
                commandService.AdjustProcurementBudget(0, 100)),
            66f);
        view.AddDataCard(
            "P3Action_ProcurementReserveDown",
            "최소 보호액 줄이기",
            "보호액을 낮춰도 향후 3일 임금·용병·계약비는 자동 보호됩니다.",
            "-100",
            () => Execute(
                view,
                commandService.AdjustProcurementBudget(0, -100)),
            66f);

        foreach (ShopProcurementRuleRow rule in model.ProcurementRules)
        {
            ShopProcurementRuleRow capturedRule = rule;
            view.AddDataCard(
                $"P3Action_Procurement_{capturedRule.Category}",
                StockCategoryCatalog.GetDisplayName(capturedRule.Category),
                capturedRule.Enabled
                    ? $"목표 {capturedRule.TargetQuantity} / 최대 단가 "
                      + $"{capturedRule.MaximumUnitPrice} / 하루 최대 "
                      + $"{capturedRule.DailyMaximumQuantity}"
                    : "자동 구매 사용 안 함",
                "목표 변경",
                () => Execute(
                    view,
                    commandService.CycleStockTarget(
                        capturedRule.Category)),
                66f);
        }

        if (model.ProcurementResults.Count > 0)
        {
            view.AddSection("이번 갱신", "구매 상품은 창고가 아니라 하차장에 도착합니다.");
            foreach (ShopProcurementResultRow result in
                     model.ProcurementResults)
            {
                ShopProcurementResultRow capturedResult = result;
                view.AddDataCard(
                    $"P3Action_ProcurementResult_{capturedResult.Title}",
                    capturedResult.Title,
                    capturedResult.Detail,
                    capturedResult.Purchased ? "배송 중" : "사유",
                    () => view.ShowFeedback(capturedResult.Detail),
                    66f);
            }
        }

        view.AddSection(
            "일일 시설 상점",
            $"Day {model.OfferDay} 상품 {model.DailyOffers.Count}개 / 보유 자금 {FormatMoney(model.CurrentMoney)}");
        if (model.DailyOffers.Count == 0)
        {
            view.AddLabel("오늘 구매 가능한 상품이 없습니다.", 20f, 34f);
        }

        foreach (ShopFeatureOfferRow row in model.DailyOffers)
        {
            ShopFeatureOfferRow captured = row;
            view.AddDataCard(
                captured.ActionId,
                captured.Title,
                captured.Detail,
                captured.IsPurchased ? "구매됨" : "구매",
                () => Execute(
                    view,
                    captured,
                    commandService.PurchaseDaily),
                CardHeight);
        }

        view.AddSection("고유 상품 찜", "매일 갱신되는 정확한 상품을 자동 구매 목록에 등록합니다.");
        foreach (ShopFeatureOfferRow row in model.DailyOffers)
        {
            ShopFeatureOfferRow captured = row;
            view.AddDataCard(
                $"P3Action_Wishlist_{captured.Index}",
                captured.Title,
                captured.Detail,
                captured.IsWishlisted ? "찜 해제" : "찜 등록",
                () =>
                {
                    ShopFeatureCommandResult result =
                        commandService.ToggleWishlist(
                            captured.OfferTypeId,
                            captured.DataId,
                            captured.Cost,
                            captured.Title);
                    if (result.Succeeded)
                    {
                        captured.IsWishlisted = !captured.IsWishlisted;
                    }

                    view.ShowFeedback(result.Message);
                },
                66f);
        }

        view.AddSection("기본 구매", $"해금된 기본 구매 후보 {model.BasicOffers.Count}개");
        if (model.BasicOffers.Count == 0)
        {
            view.AddLabel(
                "기본 구매 후보가 아직 없습니다. 설계도 연구나 계승 강화로 해금됩니다.",
                19f,
                44f);
        }

        foreach (ShopFeatureOfferRow row in model.BasicOffers)
        {
            ShopFeatureOfferRow captured = row;
            view.AddDataCard(
                captured.ActionId,
                captured.Title,
                captured.Detail,
                captured.IsPurchased ? "구매됨" : "구매",
                () => Execute(
                    view,
                    captured,
                    commandService.PurchaseBasic),
                CardHeight);
        }

        view.AddSection("상점 상품/가격/계산대", $"운영 상점 {model.RetailShops.Count}개");
        for (int shopIndex = 0; shopIndex < model.RetailShops.Count; shopIndex++)
        {
            int capturedIndex = shopIndex;
            ShopFeatureRetailRow shop = model.RetailShops[shopIndex];
            bool selected = selectedShopId == shop.RuntimeId;
            view.AddDataCard(
                $"P2Action_ShopSelect_{capturedIndex}",
                shop.Title,
                shop.Detail,
                selected ? "선택됨" : "상품 보기",
                () =>
                {
                    selectedShopId = shop.RuntimeId;
                    view.ShowFeedback(
                        $"상점 선택: {shop.Title} / 상품 {shop.Products.Count}개");
                },
                CardHeight);

            if (!selected)
            {
                continue;
            }

            for (int productIndex = 0; productIndex < shop.Products.Count; productIndex++)
            {
                ShopFeatureProductRow product = shop.Products[productIndex];
                view.AddDataCard(
                    $"P2Action_ShopProduct_{capturedIndex}_{productIndex}",
                    product.Title,
                    product.Detail,
                    "가격 확인",
                    () => view.ShowFeedback($"{product.Title}: {product.Detail}"),
                    66f);
            }
        }
    }

    private static void Execute(
        IFeatureSurfaceView view,
        ShopFeatureOfferRow row,
        Func<int, string, ShopFeatureCommandResult> command)
    {
        if (row.IsPurchased)
        {
            view.ShowFeedback($"{row.Title}은 이미 구매했습니다.");
            return;
        }

        ShopFeatureCommandResult result = command(row.Index, row.PurchaseKey);
        if (result.Succeeded)
        {
            row.IsPurchased = true;
        }

        view.ShowFeedback(result.Message);
    }

    private static void Execute(
        IFeatureSurfaceView view,
        ShopFeatureCommandResult result)
    {
        view.ShowFeedback(result.Message);
    }

    private static string FormatMoney(int money)
    {
        return money >= 0 ? money.ToString() : "없음";
    }
}
