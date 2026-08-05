using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class WarehouseFeatureSurfaceModel
{
    public string WarehouseSummary { get; set; } = string.Empty;
    public string StockSummary { get; set; } = string.Empty;
    public string PhysicalStockSummary { get; set; } = string.Empty;
    public int CurrentMoney { get; set; } = -1;
    public IReadOnlyList<WarehouseFeatureWarehouseRow> Warehouses { get; set; }
        = Array.Empty<WarehouseFeatureWarehouseRow>();
    public IReadOnlyList<WarehouseFeatureRestockRow> RestockTargets { get; set; }
        = Array.Empty<WarehouseFeatureRestockRow>();
    public IReadOnlyList<WarehouseFeatureDeliveryRow> DeliveryOffers { get; set; }
        = Array.Empty<WarehouseFeatureDeliveryRow>();
    public IReadOnlyList<WarehouseFeatureForecastRow> ForecastRows { get; set; }
        = Array.Empty<WarehouseFeatureForecastRow>();
    public IReadOnlyList<WarehouseFeatureContractRow> Contracts { get; set; }
        = Array.Empty<WarehouseFeatureContractRow>();
    public IReadOnlyList<WarehouseFeatureGrandProjectRow> GrandProjects { get; set; }
        = Array.Empty<WarehouseFeatureGrandProjectRow>();
    public string ForecastSummary { get; set; } = string.Empty;
}

public sealed class WarehouseFeatureWarehouseRow
{
    public int RuntimeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class WarehouseFeatureRestockRow
{
    public int RuntimeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool NeedsRestock { get; set; }
}

public sealed class WarehouseFeatureDeliveryRow
{
    public int Index { get; set; }
    public StockDeliveryOffer Offer { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class WarehouseFeatureForecastRow
{
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool PolicyEnabled { get; set; }
    public int MinimumStock { get; set; }
    public int TargetStock { get; set; }
    public int MaximumStock { get; set; }
    public StockSurplusDisposition SurplusDisposition { get; set; }
}

public sealed class WarehouseFeatureContractRow
{
    public string ContractId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public RegionalSupplyContractStatus Status { get; set; }
}

public sealed class WarehouseFeatureGrandProjectRow
{
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public GrandProjectStatus Status { get; set; }
}

public readonly struct WarehouseFeatureCommandResult
{
    public WarehouseFeatureCommandResult(bool succeeded, string message)
    {
        Succeeded = succeeded;
        Message = message ?? string.Empty;
    }

    public bool Succeeded { get; }
    public string Message { get; }
}

public interface IWarehouseFeatureQueryService
{
    WarehouseFeatureSurfaceModel Capture();
}

public interface IWarehouseFeatureCommandService
{
    WarehouseFeatureCommandResult Restock(int targetRuntimeId, int amount);
    WarehouseFeatureCommandResult PurchaseDelivery(StockDeliveryOffer offer);
    WarehouseFeatureCommandResult CycleStockPolicy(string itemId);
    WarehouseFeatureCommandResult ToggleStockPolicy(string itemId);
    WarehouseFeatureCommandResult AdjustStockPolicy(
        string itemId,
        ResourceStockThreshold threshold,
        int delta);
    WarehouseFeatureCommandResult AcceptContract(string contractId);
    WarehouseFeatureCommandResult DeclineContract(string contractId);
    WarehouseFeatureCommandResult StartGrandProject(string projectId);
    WarehouseFeatureCommandResult CancelGrandProject();
}

public sealed class WarehouseFeatureSessionContext
{
    public WarehouseFeatureSessionContext(
        IBuildingManagementSummaryService summaryService,
        IGameSessionStateProvider gameDataProvider,
        IRunVariableRuntimeReader runVariableReader)
    {
        SummaryService = summaryService
            ?? throw new ArgumentNullException(nameof(summaryService));
        GameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        RunVariableReader = runVariableReader
            ?? throw new ArgumentNullException(nameof(runVariableReader));
    }

    public IBuildingManagementSummaryService SummaryService { get; }
    public IGameSessionStateProvider GameDataProvider { get; }
    public IRunVariableRuntimeReader RunVariableReader { get; }
}

public sealed class WarehouseFeatureWorldContext
{
    public WarehouseFeatureWorldContext(
        IWorldItemStackRuntime worldItemStackRuntime,
        IWarehouseWorldQuery warehouseWorld,
        IBuildingWorldQuery buildingWorld,
        ICharacterWorldQuery characterWorld)
    {
        WorldItemStackRuntime = worldItemStackRuntime
            ?? throw new ArgumentNullException(nameof(worldItemStackRuntime));
        WarehouseWorld = warehouseWorld
            ?? throw new ArgumentNullException(nameof(warehouseWorld));
        BuildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        CharacterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
    }

    public IWorldItemStackRuntime WorldItemStackRuntime { get; }
    public IWarehouseWorldQuery WarehouseWorld { get; }
    public IBuildingWorldQuery BuildingWorld { get; }
    public ICharacterWorldQuery CharacterWorld { get; }
}

public sealed class WarehouseFeatureEconomyContext
{
    public WarehouseFeatureEconomyContext(
        IResourceEconomyForecastService forecastService,
        IResourceStockPolicyRuntime stockPolicies,
        IRegionalSupplyContractRuntime contracts,
        IGrandProjectRuntime grandProjects,
        IResourceEconomyContentCatalog economyCatalog,
        IItemDefinitionCatalog itemDefinitions,
        IStockCategoryDefinitionCatalog stockCategoryCatalog)
    {
        ForecastService = forecastService
            ?? throw new ArgumentNullException(nameof(forecastService));
        StockPolicies = stockPolicies
            ?? throw new ArgumentNullException(nameof(stockPolicies));
        Contracts = contracts ?? throw new ArgumentNullException(nameof(contracts));
        GrandProjects = grandProjects
            ?? throw new ArgumentNullException(nameof(grandProjects));
        EconomyCatalog = economyCatalog
            ?? throw new ArgumentNullException(nameof(economyCatalog));
        ItemDefinitions = itemDefinitions
            ?? throw new ArgumentNullException(nameof(itemDefinitions));
        StockCategoryCatalog = stockCategoryCatalog
            ?? throw new ArgumentNullException(nameof(stockCategoryCatalog));
    }

    public IResourceEconomyForecastService ForecastService { get; }
    public IResourceStockPolicyRuntime StockPolicies { get; }
    public IRegionalSupplyContractRuntime Contracts { get; }
    public IGrandProjectRuntime GrandProjects { get; }
    public IResourceEconomyContentCatalog EconomyCatalog { get; }
    public IItemDefinitionCatalog ItemDefinitions { get; }
    public IStockCategoryDefinitionCatalog StockCategoryCatalog { get; }
}

public sealed class WarehouseFeatureQueryService : IWarehouseFeatureQueryService
{
    private readonly IBuildingManagementSummaryService summaryService;
    private readonly IGameSessionStateProvider gameDataProvider;
    private readonly IRunVariableRuntimeReader runVariableReader;
    private readonly IWorldItemStackRuntime worldItemStackRuntime;
    private readonly IWarehouseWorldQuery warehouseWorld;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IResourceEconomyForecastService forecastService;
    private readonly IResourceStockPolicyRuntime stockPolicies;
    private readonly IRegionalSupplyContractRuntime contracts;
    private readonly IGrandProjectRuntime grandProjects;
    private readonly IResourceEconomyContentCatalog economyCatalog;
    private readonly IItemDefinitionCatalog itemDefinitions;
    private readonly IStockCategoryDefinitionCatalog stockCategoryCatalog;

    public WarehouseFeatureQueryService(
        WarehouseFeatureSessionContext session,
        WarehouseFeatureWorldContext world,
        WarehouseFeatureEconomyContext economy)
    {
        session = session ?? throw new ArgumentNullException(nameof(session));
        world = world ?? throw new ArgumentNullException(nameof(world));
        economy = economy ?? throw new ArgumentNullException(nameof(economy));
        summaryService = session.SummaryService;
        gameDataProvider = session.GameDataProvider;
        runVariableReader = session.RunVariableReader;
        worldItemStackRuntime = world.WorldItemStackRuntime;
        warehouseWorld = world.WarehouseWorld;
        buildingWorld = world.BuildingWorld;
        characterWorld = world.CharacterWorld;
        forecastService = economy.ForecastService;
        stockPolicies = economy.StockPolicies;
        contracts = economy.Contracts;
        grandProjects = economy.GrandProjects;
        economyCatalog = economy.EconomyCatalog;
        itemDefinitions = economy.ItemDefinitions;
        stockCategoryCatalog = economy.StockCategoryCatalog;
    }

    public WarehouseFeatureSurfaceModel Capture()
    {
        WarehouseManagementSummary summary = summaryService.CaptureWarehouses();
        IWarehouseFacility[] warehouses = FindWarehouses();
        BuildableObject[] restockTargets = FindRestockTargets();
        int money = ResolveMoney();
        IReadOnlyList<StockDeliveryOffer> offers =
            StockSupplyService.CreateDailyDeliveryOffers(
                ResolveCurrentDay(),
                runVariableReader,
                stockCategoryCatalog);
        ResourceEconomyForecast forecast = forecastService.Capture(3);

        return new WarehouseFeatureSurfaceModel
        {
            WarehouseSummary = summary.HasCapacityLimit
                ? $"창고 {summary.WarehouseCount}개 / 총 재고 {summary.TotalStock}/{summary.TotalCapacity}"
                : $"창고 {summary.WarehouseCount}개 / 총 재고 {summary.TotalStock}",
            StockSummary = FormatStockAmounts(summary.GetStock, useShortNames: false),
            PhysicalStockSummary = BuildPhysicalStockStateText(summary.TotalStock),
            CurrentMoney = money,
            Warehouses = warehouses
                .Take(WarehouseFeatureSurfacePresenter.MaxVisibleCardsPerSection)
                .Select(CreateWarehouseRow)
                .ToArray(),
            RestockTargets = restockTargets.Select(CreateRestockRow).ToArray(),
            DeliveryOffers = offers
                .Take(WarehouseFeatureSurfacePresenter.MaxVisibleCardsPerSection)
                .Select((offer, index) => new WarehouseFeatureDeliveryRow
                {
                    Index = index,
                    Offer = offer,
                    Name = $"{stockCategoryCatalog.GetDisplayName(offer.category)} {offer.amount}개",
                    Detail = $"{offer.sourceLabel} / 비용 {offer.cost} / 현재 자금 {FormatMoney(money)}"
                })
                .ToArray(),
            ForecastSummary =
                $"3일 전망 / 부족 {forecast.Shortages.Count}종 / 초과 {forecast.Surpluses.Count}종",
            ForecastRows = CreateForecastRows(forecast),
            Contracts = contracts.Contracts
                .Where(contract => contract != null
                    && contract.status is RegionalSupplyContractStatus.Offered
                        or RegionalSupplyContractStatus.Accepted
                        or RegionalSupplyContractStatus.Delivering)
                .Take(WarehouseFeatureSurfacePresenter.MaxVisibleCardsPerSection)
                .Select(CreateContractRow)
                .ToArray(),
            GrandProjects = grandProjects.Definitions
                .Select(CreateGrandProjectRow)
                .ToArray()
        };
    }

    private IReadOnlyList<WarehouseFeatureForecastRow> CreateForecastRows(
        ResourceEconomyForecast forecast)
    {
        HashSet<string> enabledPolicyIds = new HashSet<string>(
            stockPolicies.Policies
                .Where(policy => policy != null && policy.enabled)
                .Select(policy => policy.itemId),
            StringComparer.Ordinal);
        IEnumerable<ResourceEconomyForecastRow> priorityRows =
            forecast.Shortages
                .Concat(forecast.Surpluses)
                .Where(row => economyCatalog.TryGetItem(row.ItemId, out _)
                    || TryGetAuthoredStockCategory(row.ItemId, out _))
                .GroupBy(row => row.ItemId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderByDescending(row =>
                    enabledPolicyIds.Contains(row.ItemId))
                .ThenBy(row => row.ProjectedBalance)
                .ThenBy(row => row.DisplayName, StringComparer.Ordinal)
                .Take(WarehouseFeatureSurfacePresenter.MaxVisibleCardsPerSection);
        return priorityRows
            .Select(row =>
            {
                ResourceStockPolicyData policy =
                    stockPolicies.GetOrCreate(row.ItemId);
                string direction = row.ProjectedBalance < 0
                    ? "부족"
                    : "초과";
                return new WarehouseFeatureForecastRow
                {
                    ItemId = row.ItemId,
                    Name = $"{row.DisplayName} {direction}",
                    Detail =
                        $"보유 {row.Available} / 예약 {row.Reserved} / 생산 +{row.ExpectedProduction} / "
                        + $"수요 -{row.ExpectedDemand} / 예상 {row.ProjectedBalance}\n"
                        + FormatPolicy(policy),
                    PolicyEnabled = policy.enabled,
                    MinimumStock = policy.minimumStock,
                    TargetStock = policy.targetStock,
                    MaximumStock = policy.maximumStock,
                    SurplusDisposition = policy.surplusDisposition
                };
            })
            .ToArray();
    }

    private bool TryGetAuthoredStockCategory(
        string itemId,
        out StockCategory category)
    {
        if (itemDefinitions.TryGet(
                (ItemDefinitionId)itemId,
                out ItemDefinitionSO definition))
        {
            category = definition.StockCategory;
            return true;
        }

        category = default;
        return false;
    }

    private WarehouseFeatureContractRow CreateContractRow(
        RegionalSupplyContractState contract)
    {
        string requirements = string.Join(
            ", ",
            (contract.requirements
                ?? new List<RegionalSupplyContractRequirement>())
            .Select(requirement =>
                $"{ResolveItemName(requirement.itemId)} {requirement.amount}"));
        return new WarehouseFeatureContractRow
        {
            ContractId = contract.contractId,
            Name = contract.title,
            Status = contract.status,
            Detail =
                $"{requirements} / 보상 {contract.rewardGold} 골드 / 기한 {contract.deadlineDay}일\n"
                + contract.lastStatus
        };
    }

    private WarehouseFeatureGrandProjectRow CreateGrandProjectRow(
        GrandProjectDefinition definition)
    {
        GrandProjectStatus status = grandProjects.GetStatus(
            definition.ProjectId,
            out string reason);
        string requirements = string.Join(
            ", ",
            definition.Requirements.Select(requirement =>
                $"{ResolveItemName(requirement.ItemId)} {requirement.Amount}"));
        return new WarehouseFeatureGrandProjectRow
        {
            ProjectId = definition.ProjectId,
            Name = definition.DisplayName,
            Status = status,
            Detail =
                $"{definition.Description}\n재료 {requirements} / 작업량 {definition.RequiredWork:0}\n"
                + reason
        };
    }

    private string ResolveItemName(string itemId)
    {
        return economyCatalog.TryGetItem(
            itemId,
            out ResourceItemDefinitionSO item)
            ? item.DisplayName
            : itemId;
    }

    private static string FormatPolicy(ResourceStockPolicyData policy)
    {
        if (policy == null || !policy.enabled)
        {
            return "재고 정책 꺼짐";
        }

        return $"정책 {policy.minimumStock}/{policy.targetStock}/{policy.maximumStock}"
            + $" · 초과 시 {FormatDisposition(policy.surplusDisposition)}"
            + (string.IsNullOrWhiteSpace(policy.lastStatus)
                ? string.Empty
                : $" · {policy.lastStatus}");
    }

    private static string FormatDisposition(
        StockSurplusDisposition disposition)
    {
        return disposition switch
        {
            StockSurplusDisposition.Sell => "판매",
            StockSurplusDisposition.Process => "가공",
            StockSurplusDisposition.Compost => "퇴비화",
            StockSurplusDisposition.Dismantle => "해체",
            _ => "보관"
        };
    }

    private IWarehouseFacility[] FindWarehouses()
    {
        return warehouseWorld.Warehouses
            .Where((warehouse) =>
                warehouse != null && warehouse.HasWarehouseInventory && warehouse.Inventory != null)
            .ToArray();
    }

    private BuildableObject[] FindRestockTargets()
    {
        return buildingWorld.Buildings
            .Where((building) =>
                building != null && !building.isDestroy && building is IRestockableFacility)
            .OrderByDescending((building) => ((IRestockableFacility)building).MissingStock)
            .ToArray();
    }

    private WarehouseFeatureWarehouseRow CreateWarehouseRow(IWarehouseFacility warehouse)
    {
        WarehouseInventory inventory = warehouse.Inventory;
        return new WarehouseFeatureWarehouseRow
        {
            RuntimeId = GetUnityObjectId(warehouse),
            Name = GetWarehouseName(warehouse),
            Detail =
                $"총 {inventory.TotalStock}/{(inventory.HasCapacityLimit ? inventory.MaxCapacity.ToString() : "무제한")} / " +
                FormatStockAmounts(inventory.GetStock, useShortNames: true)
        };
    }

    private static WarehouseFeatureRestockRow CreateRestockRow(BuildableObject building)
    {
        IRestockableFacility facility = (IRestockableFacility)building;
        return new WarehouseFeatureRestockRow
        {
            RuntimeId = building.GetInstanceID(),
            Name = GetBuildingName(building),
            Detail = $"재고 {facility.CurrentStock}/{facility.MaxStock} / 부족 {facility.MissingStock}",
            NeedsRestock = facility.NeedsRestock
        };
    }

    private string BuildPhysicalStockStateText(int availableStock)
    {
        int loose = 0;
        int reserved = 0;
        int facilityBuffer = 0;
        foreach (WorldItemStackSnapshot stack in worldItemStackRuntime.GetAllStacks())
        {
            if (stack == null || stack.Quantity <= 0)
            {
                continue;
            }

            bool reservedLike = stack.IsReserved
                || stack.HasDestinationPosition
                || stack.State == WorldItemStackState.ExpeditionPacked;
            if (reservedLike)
            {
                reserved += stack.Quantity;
            }
            else if (stack.State == WorldItemStackState.Loose)
            {
                loose += stack.Quantity;
            }
            else if (stack.State == WorldItemStackState.FacilityBuffer)
            {
                facilityBuffer += stack.Quantity;
            }
            else if (stack.State == WorldItemStackState.FacilityOutputBuffer)
            {
                facilityBuffer += stack.Quantity;
            }
        }

        int carried = characterWorld.Characters
            .Where((actor) => actor != null)
            .Select((actor) => actor.GetComponent<CharacterCarryInventory>())
            .Where((inventory) => inventory != null)
            .Sum((inventory) => inventory.Items.Sum(
                (item) => item != null ? Mathf.Max(0, item.quantity) : 0));
        return $"재고 상태  사용 가능 {availableStock} / 예약됨 {reserved} / 바닥 {loose} / " +
               $"시설 버퍼 {facilityBuffer} / 운반 중 {carried}";
    }

    private int ResolveCurrentDay()
    {
        return gameDataProvider.TryGetSessionState(out GameSessionState gameData)
            && gameData != null
            && gameData.day != null
            ? Mathf.Max(1, gameData.day.Value)
            : 1;
    }

    private int ResolveMoney()
    {
        return gameDataProvider.TryGetSessionState(out GameSessionState gameData)
            && gameData != null
            && gameData.holdingMoney != null
            ? gameData.holdingMoney.Value
            : -1;
    }

    private static string GetWarehouseName(IWarehouseFacility warehouse)
    {
        Component component = warehouse as Component;
        return component != null
            ? GetBuildingName(component.GetComponent<BuildableObject>())
            : "창고";
    }

    private static string GetBuildingName(BuildableObject building)
    {
        if (building == null)
        {
            return "시설";
        }

        return building.BuildingData != null
            && !string.IsNullOrWhiteSpace(building.BuildingData.objectName)
            ? building.BuildingData.objectName
            : building.name;
    }

    private static int GetUnityObjectId(object value)
    {
        return value is Component component
            ? component.GetInstanceID()
            : value != null
                ? value.GetHashCode()
                : 0;
    }

    private static string FormatMoney(int money)
    {
        return money >= 0 ? money.ToString() : "없음";
    }

    private string FormatStockAmounts(
        Func<StockCategory, int> getStock,
        bool useShortNames)
    {
        return getStock == null
            ? string.Empty
            : string.Join(
                " / ",
                stockCategoryCatalog.All.Select((definition) =>
                    $"{(useShortNames ? definition.ShortName : definition.DisplayName)} " +
                    $"{getStock(definition.Category)}"));
    }
}


public sealed class WarehouseFeatureSurfacePresenter : IFeatureSurfaceTabPresenter
{
    internal const int MaxVisibleCardsPerSection = 8;
    private const float CompactCardHeight = 66f;

    private readonly IWarehouseFeatureQueryService queryService;
    private readonly IWarehouseFeatureCommandService commandService;

    public WarehouseFeatureSurfacePresenter(
        IWarehouseFeatureQueryService queryService,
        IWarehouseFeatureCommandService commandService)
    {
        this.queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        this.commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
    }

    public TabId Id => TabId.Warehouse;

    public void Present(IFeatureSurfaceView view)
    {
        if (view == null)
        {
            throw new ArgumentNullException(nameof(view));
        }

        WarehouseFeatureSurfaceModel model = queryService.Capture();
        view.AddSection("창고 재고", model.WarehouseSummary);
        view.AddLabel(model.StockSummary, 20f, 36f);
        view.AddLabel(model.PhysicalStockSummary, 18f, 32f);

        foreach (WarehouseFeatureWarehouseRow warehouse in model.Warehouses)
        {
            WarehouseFeatureWarehouseRow captured = warehouse;
            view.AddDataCard(
                "P0State_Warehouse_" + captured.RuntimeId,
                captured.Name,
                captured.Detail,
                "상태",
                () => view.ShowFeedback($"{captured.Name} 재고를 확인했습니다."),
                CompactCardHeight);
        }

        int needsRestock = model.RestockTargets.Count((row) => row.NeedsRestock);
        view.AddSection(
            "상점 수동 보충",
            $"상점 {model.RestockTargets.Count}개 / 보충 필요 {needsRestock}개");
        foreach (WarehouseFeatureRestockRow target in model.RestockTargets
                     .Where((row) => row.NeedsRestock)
                     .Take(MaxVisibleCardsPerSection))
        {
            WarehouseFeatureRestockRow captured = target;
            view.AddDataCard(
                "P0Action_WarehouseRestock_" + captured.RuntimeId,
                captured.Name,
                captured.Detail,
                "보충",
                () => view.ShowFeedback(commandService.Restock(captured.RuntimeId, 5).Message),
                CompactCardHeight);
        }

        view.AddSection("일일 납품", "자금을 지불하면 하차장에 물품이 도착합니다.");
        foreach (WarehouseFeatureDeliveryRow delivery in model.DeliveryOffers)
        {
            WarehouseFeatureDeliveryRow captured = delivery;
            view.AddDataCard(
                $"P0Action_WarehouseDelivery_{captured.Index}",
                captured.Name,
                captured.Detail,
                "납품 구매",
                () => view.ShowFeedback(commandService.PurchaseDelivery(captured.Offer).Message),
                CompactCardHeight);
        }

        view.AddSection("자원 전망", model.ForecastSummary);
        if (model.ForecastRows.Count == 0)
        {
            view.AddLabel("3일 안에 예상되는 부족 또는 초과 품목이 없습니다.", 18f, 36f);
        }

        foreach (WarehouseFeatureForecastRow forecast in model.ForecastRows)
        {
            WarehouseFeatureForecastRow captured = forecast;
            view.AddControlCard(
                "EconomyForecast_" + captured.ItemId,
                captured.Name,
                captured.Detail,
                new[]
                {
                    CreateStockStepper(
                        view,
                        captured,
                        ResourceStockThreshold.Minimum,
                        "최소",
                        captured.MinimumStock),
                    CreateStockStepper(
                        view,
                        captured,
                        ResourceStockThreshold.Target,
                        "목표",
                        captured.TargetStock),
                    CreateStockStepper(
                        view,
                        captured,
                        ResourceStockThreshold.Maximum,
                        "최대",
                        captured.MaximumStock)
                },
                new[]
                {
                    new FeatureSurfaceAction(
                        "Toggle",
                        captured.PolicyEnabled ? "정책 끄기" : "정책 켜기",
                        () => ShowAndRefresh(
                            view,
                            commandService.ToggleStockPolicy(
                                captured.ItemId))),
                    new FeatureSurfaceAction(
                        "Disposition",
                        "초과: " + FormatDisposition(
                            captured.SurplusDisposition),
                        () => ShowAndRefresh(
                            view,
                            commandService.CycleStockPolicy(
                                captured.ItemId)))
                },
                144f);
        }

        view.AddSection(
            "지역 공급 계약",
            model.Contracts.Count > 0
                ? "계약 물품은 창고에서 하차장 집결점으로 실제 운반됩니다."
                : "현재 제안되거나 진행 중인 계약이 없습니다.");
        foreach (WarehouseFeatureContractRow contract in model.Contracts)
        {
            WarehouseFeatureContractRow captured = contract;
            bool offered =
                captured.Status == RegionalSupplyContractStatus.Offered;
            if (offered)
            {
                view.AddControlCard(
                    "RegionalContract_" + captured.ContractId,
                    captured.Name,
                    captured.Detail,
                    Array.Empty<FeatureSurfaceStepper>(),
                    new[]
                    {
                        new FeatureSurfaceAction(
                            "Accept",
                            "수락",
                            () => ShowAndRefresh(
                                view,
                                commandService.AcceptContract(
                                    captured.ContractId))),
                        new FeatureSurfaceAction(
                            "Decline",
                            "거절",
                            () => ShowAndRefresh(
                                view,
                                commandService.DeclineContract(
                                    captured.ContractId)))
                    },
                    116f);
            }
            else
            {
                view.AddDataCard(
                    "RegionalContract_" + captured.ContractId,
                    captured.Name,
                    captured.Detail,
                    "운반 현황",
                    () => view.ShowFeedback(captured.Detail),
                    98f);
            }
        }

        view.AddSection(
            "대형 사업",
            "대규모 재료를 사무실로 운반한 뒤 누적 작업량을 채워 완성합니다.");
        foreach (WarehouseFeatureGrandProjectRow project in
                 model.GrandProjects)
        {
            WarehouseFeatureGrandProjectRow captured = project;
            bool active = captured.Status is
                GrandProjectStatus.WaitingForMaterials
                or GrandProjectStatus.InProgress;
            bool canStart = captured.Status == GrandProjectStatus.Available;
            view.AddDataCard(
                "GrandProject_" + captured.ProjectId,
                captured.Name,
                captured.Detail,
                active ? "사업 취소" : canStart ? "사업 시작" : "상태",
                () =>
                {
                    WarehouseFeatureCommandResult result = active
                        ? commandService.CancelGrandProject()
                        : canStart
                            ? commandService.StartGrandProject(
                                captured.ProjectId)
                            : new WarehouseFeatureCommandResult(
                                false,
                                captured.Detail);
                    view.ShowFeedback(result.Message);
                    view.RequestRefresh();
                },
                112f);
        }
    }

    private FeatureSurfaceStepper CreateStockStepper(
        IFeatureSurfaceView view,
        WarehouseFeatureForecastRow row,
        ResourceStockThreshold threshold,
        string label,
        int value)
    {
        return new FeatureSurfaceStepper(
            threshold.ToString(),
            label,
            value.ToString(),
            () => ShowAndRefresh(
                view,
                commandService.AdjustStockPolicy(
                    row.ItemId,
                    threshold,
                    -5)),
            () => ShowAndRefresh(
                view,
                commandService.AdjustStockPolicy(
                    row.ItemId,
                    threshold,
                    5)));
    }

    private static void ShowAndRefresh(
        IFeatureSurfaceView view,
        WarehouseFeatureCommandResult result)
    {
        view.ShowFeedback(result.Message);
        view.RequestRefresh();
    }

    private static string FormatDisposition(
        StockSurplusDisposition disposition)
    {
        return disposition switch
        {
            StockSurplusDisposition.Sell => "판매",
            StockSurplusDisposition.Process => "가공",
            StockSurplusDisposition.Compost => "퇴비화",
            StockSurplusDisposition.Dismantle => "해체",
            _ => "보관"
        };
    }
}
