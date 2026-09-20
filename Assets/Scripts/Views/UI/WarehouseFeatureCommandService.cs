using System;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public sealed class WarehouseCommandSessionContext
{
    public WarehouseCommandSessionContext(
        IGameSessionStateProvider gameDataProvider,
        IGameMoneyAccount money,
        IGameEventBus gameEventBus,
        IDungeonDebugRuleQuery debugRules)
        : this(
            gameDataProvider,
            money,
            gameEventBus,
            debugRules,
            outcomeTransactions: null,
            gameSessionState: null,
            treasuryState: null)
    {
    }

    [Inject]
    public WarehouseCommandSessionContext(
        IGameSessionStateProvider gameDataProvider,
        IGameMoneyAccount money,
        IGameEventBus gameEventBus,
        IDungeonDebugRuleQuery debugRules,
        IMigratedProducerOutcomeTransaction outcomeTransactions,
        IGameSessionStateStore gameSessionState,
        TreasuryEconomyAggregateStateStore treasuryState)
    {
        GameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
        Money = money ?? throw new ArgumentNullException(nameof(money));
        GameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        DebugRules = debugRules
            ?? throw new ArgumentNullException(nameof(debugRules));
        OutcomeTransactions = outcomeTransactions;
        GameSessionState = gameSessionState;
        TreasuryState = treasuryState;
    }

    public IGameSessionStateProvider GameDataProvider { get; }
    public IGameMoneyAccount Money { get; }
    public IGameEventBus GameEventBus { get; }
    public IDungeonDebugRuleQuery DebugRules { get; }
    public IMigratedProducerOutcomeTransaction OutcomeTransactions { get; }
    public IGameSessionStateStore GameSessionState { get; }
    public TreasuryEconomyAggregateStateStore TreasuryState { get; }
}

public sealed class WarehouseCommandWorldContext
{
    public WarehouseCommandWorldContext(
        IWorldItemStackRuntime worldItemStackRuntime,
        IWarehouseWorldQuery warehouseWorld,
        IBuildingWorldQuery buildingWorld)
    {
        WorldItemStackRuntime = worldItemStackRuntime
            ?? throw new ArgumentNullException(nameof(worldItemStackRuntime));
        WarehouseWorld = warehouseWorld
            ?? throw new ArgumentNullException(nameof(warehouseWorld));
        BuildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
    }

    public IWorldItemStackRuntime WorldItemStackRuntime { get; }
    public IWarehouseWorldQuery WarehouseWorld { get; }
    public IBuildingWorldQuery BuildingWorld { get; }
}

public sealed class WarehouseCommandPlanningContext
{
    public WarehouseCommandPlanningContext(
        IResourceStockPolicyRuntime stockPolicies,
        IRegionalSupplyContractRuntime contracts,
        IGrandProjectRuntime grandProjects)
    {
        StockPolicies = stockPolicies
            ?? throw new ArgumentNullException(nameof(stockPolicies));
        Contracts = contracts ?? throw new ArgumentNullException(nameof(contracts));
        GrandProjects = grandProjects
            ?? throw new ArgumentNullException(nameof(grandProjects));
    }

    public IResourceStockPolicyRuntime StockPolicies { get; }
    public IRegionalSupplyContractRuntime Contracts { get; }
    public IGrandProjectRuntime GrandProjects { get; }
}

public sealed class WarehouseFeatureCommandService : IWarehouseFeatureCommandService
{
    private readonly IGameSessionStateProvider gameDataProvider;
    private readonly IGameMoneyAccount money;
    private readonly IWorldItemStackRuntime worldItemStackRuntime;
    private readonly IWarehouseWorldQuery warehouseWorld;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IGameEventBus gameEventBus;
    private readonly IResourceStockPolicyRuntime stockPolicies;
    private readonly IRegionalSupplyContractRuntime contracts;
    private readonly IGrandProjectRuntime grandProjects;
    private readonly IDungeonDebugRuleQuery debugRules;
    private readonly IMigratedProducerOutcomeTransaction outcomeTransactions;
    private readonly IGameSessionStateStore gameSessionState;
    private readonly TreasuryEconomyAggregateStateStore treasuryState;

    public WarehouseFeatureCommandService(
        WarehouseCommandSessionContext session,
        WarehouseCommandWorldContext world,
        WarehouseCommandPlanningContext planning)
    {
        session = session ?? throw new ArgumentNullException(nameof(session));
        world = world ?? throw new ArgumentNullException(nameof(world));
        planning = planning ?? throw new ArgumentNullException(nameof(planning));
        gameDataProvider = session.GameDataProvider;
        money = session.Money;
        gameEventBus = session.GameEventBus;
        debugRules = session.DebugRules;
        outcomeTransactions = session.OutcomeTransactions;
        gameSessionState = session.GameSessionState;
        treasuryState = session.TreasuryState;
        worldItemStackRuntime = world.WorldItemStackRuntime;
        warehouseWorld = world.WarehouseWorld;
        buildingWorld = world.BuildingWorld;
        stockPolicies = planning.StockPolicies;
        contracts = planning.Contracts;
        grandProjects = planning.GrandProjects;
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

        int beforeShop = facility.CurrentStock;
        string message = string.Empty;
        bool requested = amount > 0
            && facility.TryRequestRestock(out message);
        if (amount <= 0)
        {
            message = "보충 요청량은 1 이상이어야 합니다.";
        }
        string result =
            $"보충 {(requested ? "요청" : "실패")}: {GetBuildingName(target)} {message} / " +
            $"상점 현재 재고 {beforeShop}, 물리 운반 완료 후 반영";
        return new WarehouseFeatureCommandResult(requested, result);
    }

    public WarehouseFeatureCommandResult PurchaseDelivery(StockDeliveryOffer offer)
    {
        if (!gameDataProvider.TryGetSessionState(out GameSessionState gameData) || gameData == null)
        {
            return new WarehouseFeatureCommandResult(false, "게임 자금 정보를 찾지 못했습니다.");
        }
        if (outcomeTransactions == null
            || gameSessionState == null
            || treasuryState == null)
        {
            return new WarehouseFeatureCommandResult(
                false,
                "재고 납품 결과 원장 트랜잭션이 준비되지 않았습니다.");
        }

        IWarehouseFacility[] warehouses = FindWarehouses();
        int beforeMoney = GetHoldingMoney(gameData);
        int beforeStock = warehouses.Sum((warehouse) => warehouse.Inventory.TotalStock);
        int absoluteDay = Mathf.Max(0, gameData.day?.Value ?? 0);
        string identity = CreateSupplyOutcomeIdentity(absoluteDay, offer);
        if (!outcomeTransactions.TryReserveSingleSubject(
                MigratedProducerOutcomeKind.StockSupplyResult,
                identity,
                absoluteDay,
                GameplayOutcomeStatus.Succeeded,
                out PreparedMigratedProducerOutcome prepared,
                out string reserveFailure))
        {
            return new WarehouseFeatureCommandResult(
                false,
                "재고 납품 결과 예약 실패: " + reserveFailure);
        }

        GameSessionSnapshot sessionBefore;
        TreasuryEconomyAggregateState treasuryBefore;
        DungeonPhysicalItemSaveData physicalBefore;
        try
        {
            sessionBefore = gameData.Capture();
            treasuryBefore = treasuryState.Current.Copy();
            physicalBefore = worldItemStackRuntime.Capture();
        }
        catch
        {
            outcomeTransactions.Cancel(prepared);
            throw;
        }
        bool success;
        StockSupplyResult result;
        try
        {
            success = StockSupplyService.TryPurchaseDelivery(
                money,
                warehouses,
                worldItemStackRuntime,
                offer,
                debugRules,
                out result,
                resultCallback: null);
            MigratedProducerOutcomeCommitResult committed =
                outcomeTransactions.CommitSingleSubject(
                    prepared,
                    new MigratedProducerOutcomeSubject(
                        MigratedProducerOutcomeIds.OperationKind,
                        prepared.ResultKey.OperationId.Value,
                        string.IsNullOrWhiteSpace(result.sourceLabel)
                            ? result.category.ToString()
                            : result.sourceLabel,
                        MigratedProducerOutcomeIds.OperationRole),
                    CreateSupplyOutcomeSummary(result));
            if (!committed.DurablyCommitted)
            {
                RestoreSupplyOwners(
                    sessionBefore,
                    treasuryBefore,
                    physicalBefore);
                return new WarehouseFeatureCommandResult(
                    false,
                    "재고 납품 결과 확정 실패: " + committed.DetailCode);
            }
        }
        catch
        {
            outcomeTransactions.Cancel(prepared);
            RestoreSupplyOwners(sessionBefore, treasuryBefore, physicalBefore);
            throw;
        }

        PublishSupplyResultPostCommit(result);
        int afterStock = warehouses.Sum((warehouse) => warehouse.Inventory.TotalStock);
        string message =
            $"납품 {(success ? "성공" : "실패")}: {result.ToSummaryText()} / " +
            $"자금 {beforeMoney}->{GetHoldingMoney(gameData)}, 창고 {beforeStock}->{afterStock}";
        return new WarehouseFeatureCommandResult(success, message);
    }

    public WarehouseFeatureCommandResult CycleStockPolicy(string itemId)
    {
        ResourceStockPolicyData policy = stockPolicies.GetOrCreate(itemId);
        if (policy.surplusDisposition
                 < StockSurplusDisposition.Dismantle)
        {
            policy.surplusDisposition++;
        }
        else
        {
            policy.surplusDisposition = StockSurplusDisposition.Hold;
        }

        bool succeeded = stockPolicies.SetPolicy(
            policy,
            out string failureReason);
        return new WarehouseFeatureCommandResult(
            succeeded,
            succeeded
                ? $"초과 처리: {FormatDisposition(policy.surplusDisposition)}"
                : failureReason);
    }

    public WarehouseFeatureCommandResult ToggleStockPolicy(string itemId)
    {
        ResourceStockPolicyData policy = stockPolicies.GetOrCreate(itemId);
        policy.enabled = !policy.enabled;
        bool succeeded = stockPolicies.SetPolicy(policy, out string failureReason);
        return new WarehouseFeatureCommandResult(
            succeeded,
            succeeded
                ? $"재고 정책을 {(policy.enabled ? "켰습니다." : "껐습니다.")}"
                : failureReason);
    }

    public WarehouseFeatureCommandResult ToggleEmergencyReserve(string itemId)
    {
        ResourceStockPolicyData policy = stockPolicies.GetOrCreate(itemId);
        policy.isEmergencyReserve = !policy.isEmergencyReserve;
        if (policy.isEmergencyReserve)
            policy.enabled = true;
        bool succeeded = stockPolicies.SetPolicy(policy, out string failureReason);
        return new WarehouseFeatureCommandResult(
            succeeded,
            succeeded
                ? policy.isEmergencyReserve
                    ? $"비상 비축으로 지정했습니다. 충족 기준: 최소 {policy.minimumStock}개"
                    : "비상 비축 지정을 해제했습니다."
                : failureReason);
    }

    public WarehouseFeatureCommandResult AdjustStockPolicy(
        string itemId,
        ResourceStockThreshold threshold,
        int delta)
    {
        ResourceStockPolicyData policy = stockPolicies.GetOrCreate(itemId);
        int amount = Mathf.Clamp(delta, -100, 100);
        switch (threshold)
        {
            case ResourceStockThreshold.Minimum:
                policy.minimumStock = Mathf.Max(0, policy.minimumStock + amount);
                policy.targetStock = Mathf.Max(
                    policy.minimumStock,
                    policy.targetStock);
                policy.maximumStock = Mathf.Max(
                    policy.targetStock,
                    policy.maximumStock);
                break;
            case ResourceStockThreshold.Target:
                policy.targetStock = Mathf.Max(
                    policy.minimumStock,
                    policy.targetStock + amount);
                policy.maximumStock = Mathf.Max(
                    policy.targetStock,
                    policy.maximumStock);
                break;
            case ResourceStockThreshold.Maximum:
                policy.maximumStock = Mathf.Max(
                    policy.targetStock,
                    policy.maximumStock + amount);
                break;
            default:
                return new WarehouseFeatureCommandResult(
                    false,
                    "알 수 없는 재고 기준입니다.");
        }

        bool succeeded = stockPolicies.SetPolicy(policy, out string failureReason);
        return new WarehouseFeatureCommandResult(
            succeeded,
            succeeded
                ? $"재고 기준 {policy.minimumStock}/{policy.targetStock}/{policy.maximumStock}"
                : failureReason);
    }

    public WarehouseFeatureCommandResult AcceptContract(string contractId)
    {
        bool succeeded = contracts.Accept(contractId, out string message);
        return new WarehouseFeatureCommandResult(succeeded, message);
    }

    public WarehouseFeatureCommandResult DeclineContract(string contractId)
    {
        bool succeeded = contracts.Decline(contractId, out string message);
        return new WarehouseFeatureCommandResult(succeeded, message);
    }

    public WarehouseFeatureCommandResult StartGrandProject(string projectId)
    {
        bool succeeded = grandProjects.Start(projectId, out string message);
        return new WarehouseFeatureCommandResult(succeeded, message);
    }

    public WarehouseFeatureCommandResult CancelGrandProject()
    {
        bool succeeded = grandProjects.CancelActive(out string message);
        return new WarehouseFeatureCommandResult(succeeded, message);
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

    private void PublishSupplyResultPostCommit(StockSupplyResult result)
    {
        try
        {
            gameEventBus.Publish(new StockSupplyEvent(result));
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(
                "stock-supply-post-commit-event:"
                + exception.GetType().Name);
        }
    }

    private void RestoreSupplyOwners(
        GameSessionSnapshot sessionBefore,
        TreasuryEconomyAggregateState treasuryBefore,
        DungeonPhysicalItemSaveData physicalBefore)
    {
        gameSessionState.Restore(sessionBefore);
        treasuryState.Replace(treasuryBefore
            ?? throw new ArgumentNullException(nameof(treasuryBefore)));
        worldItemStackRuntime.Restore(physicalBefore
            ?? throw new ArgumentNullException(nameof(physicalBefore)));
    }

    private static string CreateSupplyOutcomeIdentity(
        int absoluteDay,
        StockDeliveryOffer offer) =>
        "stock-supply:warehouse:day=" + absoluteDay
        + ":category=" + offer.category
        + ":item=" + (offer.itemId?.Trim() ?? string.Empty)
        + ":amount=" + Mathf.Max(0, offer.amount)
        + ":source=" + (offer.sourceLabel?.Trim() ?? string.Empty);

    private static string CreateSupplyOutcomeSummary(StockSupplyResult result) =>
        "재고 납품 결과: success=" + (result.success ? "true" : "false")
        + "; category=" + result.category
        + "; requested=" + result.requestedAmount
        + "; delivered=" + result.deliveredAmount
        + "; cost=" + result.cost
        + "; source=" + result.sourceLabel
        + "; reason=" + result.reason;

    private IWarehouseFacility[] FindWarehouses()
    {
        return warehouseWorld.Warehouses
            .Where((warehouse) =>
                warehouse != null && warehouse.HasWarehouseInventory && warehouse.Inventory != null)
            .ToArray();
    }

    private static int GetHoldingMoney(GameSessionState gameData)
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
