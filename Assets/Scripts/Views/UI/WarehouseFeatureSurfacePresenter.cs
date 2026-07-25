using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
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
}

public sealed class WarehouseFeatureQueryService : IWarehouseFeatureQueryService
{
    private readonly IBuildingManagementSummaryService summaryService;
    private readonly IGameDataProvider gameDataProvider;
    private readonly IRunVariableRuntimeReader runVariableReader;
    private readonly IWorldItemStackRuntime worldItemStackRuntime;
    private readonly IWarehouseWorldQuery warehouseWorld;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly ICharacterWorldQuery characterWorld;

    public WarehouseFeatureQueryService(
        IBuildingManagementSummaryService summaryService,
        IGameDataProvider gameDataProvider,
        IRunVariableRuntimeReader runVariableReader,
        IWorldItemStackRuntime worldItemStackRuntime,
        IWarehouseWorldQuery warehouseWorld,
        IBuildingWorldQuery buildingWorld,
        ICharacterWorldQuery characterWorld)
    {
        this.summaryService = summaryService ?? throw new ArgumentNullException(nameof(summaryService));
        this.gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.runVariableReader = runVariableReader ?? throw new ArgumentNullException(nameof(runVariableReader));
        this.worldItemStackRuntime = worldItemStackRuntime
            ?? throw new ArgumentNullException(nameof(worldItemStackRuntime));
        this.warehouseWorld = warehouseWorld
            ?? throw new ArgumentNullException(nameof(warehouseWorld));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
    }

    public WarehouseFeatureSurfaceModel Capture()
    {
        WarehouseManagementSummary summary = summaryService.CaptureWarehouses();
        IWarehouseFacility[] warehouses = FindWarehouses();
        BuildableObject[] restockTargets = FindRestockTargets();
        int money = ResolveMoney();
        IReadOnlyList<StockDeliveryOffer> offers =
            StockSupplyService.CreateDailyDeliveryOffers(ResolveCurrentDay(), runVariableReader);

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
                    Name = $"{StockCategoryCatalog.GetDisplayName(offer.category)} {offer.amount}개",
                    Detail = $"{offer.sourceLabel} / 비용 {offer.cost} / 현재 자금 {FormatMoney(money)}"
                })
                .ToArray()
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
        return gameDataProvider.TryGetGameData(out GameData gameData)
            && gameData != null
            && gameData.day != null
            ? Mathf.Max(1, gameData.day.Value)
            : 1;
    }

    private int ResolveMoney()
    {
        return gameDataProvider.TryGetGameData(out GameData gameData)
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

    private static string FormatStockAmounts(
        Func<StockCategory, int> getStock,
        bool useShortNames)
    {
        return getStock == null
            ? string.Empty
            : string.Join(
                " / ",
                StockCategoryCatalog.All.Select((definition) =>
                    $"{(useShortNames ? definition.ShortName : definition.DisplayName)} " +
                    $"{getStock(definition.Category)}"));
    }
}

public sealed class WarehouseFeatureCommandService : IWarehouseFeatureCommandService
{
    private readonly IGameDataProvider gameDataProvider;
    private readonly IWorldItemStackRuntime worldItemStackRuntime;
    private readonly IWarehouseWorldQuery warehouseWorld;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IGameEventBus gameEventBus;

    public WarehouseFeatureCommandService(
        IGameDataProvider gameDataProvider,
        IWorldItemStackRuntime worldItemStackRuntime,
        IWarehouseWorldQuery warehouseWorld,
        IBuildingWorldQuery buildingWorld,
        IGameEventBus gameEventBus)
    {
        this.gameDataProvider = gameDataProvider ?? throw new ArgumentNullException(nameof(gameDataProvider));
        this.worldItemStackRuntime = worldItemStackRuntime
            ?? throw new ArgumentNullException(nameof(worldItemStackRuntime));
        this.warehouseWorld = warehouseWorld
            ?? throw new ArgumentNullException(nameof(warehouseWorld));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    public WarehouseFeatureCommandResult Restock(int targetRuntimeId, int amount)
    {
        BuildableObject target = buildingWorld.Buildings
            .FirstOrDefault((building) =>
                building != null && building.GetInstanceID() == targetRuntimeId);
        if (!(target is IRestockableFacility facility))
        {
            return new WarehouseFeatureCommandResult(false, "보충할 시설을 찾지 못했습니다.");
        }

        IWarehouseFacility[] warehouses = FindWarehouses();
        int beforeShop = facility.CurrentStock;
        int beforeWarehouse = warehouses.Sum((warehouse) => warehouse.Inventory.TotalStock);
        int moved = facility.RestockFrom(
            warehouses,
            Mathf.Min(Mathf.Max(0, amount), facility.MissingStock),
            out string message);
        int afterWarehouse = warehouses.Sum((warehouse) => warehouse.Inventory.TotalStock);
        string result =
            $"보충 {(moved > 0 ? "성공" : "실패")}: {GetBuildingName(target)} {message} / " +
            $"상점 {beforeShop}->{facility.CurrentStock}, 창고 {beforeWarehouse}->{afterWarehouse}";
        return new WarehouseFeatureCommandResult(moved > 0, result);
    }

    public WarehouseFeatureCommandResult PurchaseDelivery(StockDeliveryOffer offer)
    {
        if (!gameDataProvider.TryGetGameData(out GameData gameData) || gameData == null)
        {
            return new WarehouseFeatureCommandResult(false, "게임 자금 정보를 찾지 못했습니다.");
        }

        IWarehouseFacility[] warehouses = FindWarehouses();
        int beforeMoney = GetHoldingMoney(gameData);
        int beforeStock = warehouses.Sum((warehouse) => warehouse.Inventory.TotalStock);
        bool success = StockSupplyService.TryPurchaseDelivery(
            gameData,
            warehouses,
            worldItemStackRuntime,
            offer,
            out StockSupplyResult result,
            PublishSupplyResult);
        int afterStock = warehouses.Sum((warehouse) => warehouse.Inventory.TotalStock);
        string message =
            $"납품 {(success ? "성공" : "실패")}: {result.ToSummaryText()} / " +
            $"자금 {beforeMoney}->{GetHoldingMoney(gameData)}, 창고 {beforeStock}->{afterStock}";
        return new WarehouseFeatureCommandResult(success, message);
    }

    private void PublishSupplyResult(StockSupplyResult result)
    {
        gameEventBus.Publish(new StockSupplyEvent(result));
    }

    private IWarehouseFacility[] FindWarehouses()
    {
        return warehouseWorld.Warehouses
            .Where((warehouse) =>
                warehouse != null && warehouse.HasWarehouseInventory && warehouse.Inventory != null)
            .ToArray();
    }

    private static int GetHoldingMoney(GameData gameData)
    {
        return gameData != null && gameData.holdingMoney != null
            ? gameData.holdingMoney.Value
            : -1;
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
    }
}
