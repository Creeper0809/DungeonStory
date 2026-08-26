using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public class Shop : BuildableObject, IRetailFacility, IRestockableFacility, IRetailStockStateOwner, IRetailRestockOperationOwner, IWorkableFacility
{
    private const float WaitingCheckoutOperateUrgency = 160f;
    private const float WaitingCheckoutOperateUrgencyPerCustomer = 40f;
    private ShopInventoryRuntime inventoryRuntime;
    private ShopCrimeRuntime crimeRuntime;
    private ShopServiceCompletion serviceCompletion;
    private ShopCustomerInteractionService customerInteraction;
    private IBuildingVisitorPort worker;
    private CharacterId workerCharacterId;
    private IGameMoneyAccount moneyAccount;
    private IFloatingNumberFeedbackService floatingNumberFeedbackService;
    private IBuildingWorkforceReplanPort workforceReplanService;
    private IRoomEnvironmentExperienceService roomEnvironmentExperienceService;
    private IMealConsumptionRuntime mealConsumptionRuntime;
    private IServiceSessionRuntime serviceSessionRuntime;
    private IShopServiceSessionCompletionPort serviceSessionCompletionPort;
    private IRetailStockPhysicalRuntime retailStockPhysicalRuntime;
    private readonly HashSet<string> activeRestockOperations =
        new(StringComparer.Ordinal);
    private ShopInventoryRuntime Inventory =>
        inventoryRuntime ??= new ShopInventoryRuntime(this);
    private ShopCrimeRuntime Crime =>
        crimeRuntime ??= new ShopCrimeRuntime(this);
    private ShopServiceCompletion ServiceCompletion =>
        serviceCompletion ??= new ShopServiceCompletion(EndShopUse);
    private ShopCustomerInteractionService CustomerInteraction =>
        customerInteraction ??= new ShopCustomerInteractionService(
            this,
            () => transform.position);

    public int CurrentStock
    {
        get
        {
            SynchronizeAuthoredStock();
            return Inventory.CurrentCount;
        }
    }
    public bool HasAvailableStock => CurrentStock > 0;

    public bool HasMealAvailableFor(
        CharacterActor actor,
        out CharacterConsumablesFailure failure)
    {
        if (mealConsumptionRuntime == null)
        {
            failure = new CharacterConsumablesFailure(
                CharacterConsumablesFailureCode.InvalidCommand,
                "Meal consumption runtime is unavailable.");
            return false;
        }

        return mealConsumptionRuntime.HasMealAvailable(actor, this, out failure);
    }
    public int WaitingCheckoutCount => CustomerInteraction.WaitingCheckoutCount;
    public bool HasWaitingCheckout => CustomerInteraction.HasWaitingCheckout;
    public bool HasServingWorker
    {
        get
        {
            PruneInvalidWorker();
            return worker != null;
        }
    }

    public StockCategory ActiveStockCategory => Inventory.ActiveCategory;
    public int MaxInternalStock => Inventory.MaxInternalStock;
    public int MaxStock => MaxInternalStock;
    public int MissingStock => Mathf.Max(0, MaxInternalStock - CurrentStock);
    public bool NeedsRestock => MissingStock > 0;
    public bool RequiresStaffedCheckout => RequiresServingWorker();
    public int ActiveRestockOperationCount => activeRestockOperations.Count;
    public bool UsesSelfService => !RequiresStaffedCheckout;
    public float CurrentPriceMultiplier => GetPriceMultiplier();
    public IReadOnlyList<RetailProductSnapshot> ProductSnapshots
    {
        get
        {
            SynchronizeAuthoredStock();
            return Inventory.CreateProductSnapshots(GetPriceMultiplier());
        }
    }

    public override void Initialization(BuildingSO buildingSO, Vector2Int buildPos)
    {
        base.Initialization(buildingSO, buildPos);
        Inventory.Reset();
        TryInitializeStock(requireCatalog: false);
        RegisterStateModule(new ShopStockStateModule(this));
    }

    [Inject]
    public void ConstructShop(
        IGameMoneyAccount moneyAccount,
        IShopStockCatalog stockCatalog,
        IFloatingNumberFeedbackService floatingNumberFeedbackService,
        IBuildingWorkforceReplanPort workforceReplanService,
        IFacilityCrimeRiskEvaluator crimeRiskEvaluator,
        IRandomStreamProvider randomStreamProvider,
        IRoomEnvironmentExperienceService roomEnvironmentExperienceService,
        IMealConsumptionRuntime mealConsumptionRuntime,
        IServiceSessionRuntime serviceSessionRuntime,
        IRetailStockPhysicalRuntime retailStockPhysicalRuntime,
        IStockQuery stockQuery)
    {
        this.moneyAccount = moneyAccount
            ?? throw new ArgumentNullException(nameof(moneyAccount));
        Inventory.Configure(stockCatalog, stockQuery);
        this.floatingNumberFeedbackService = floatingNumberFeedbackService
            ?? throw new ArgumentNullException(nameof(floatingNumberFeedbackService));
        this.workforceReplanService = workforceReplanService
            ?? throw new ArgumentNullException(nameof(workforceReplanService));
        this.roomEnvironmentExperienceService = roomEnvironmentExperienceService;
        this.mealConsumptionRuntime = mealConsumptionRuntime;
        this.serviceSessionRuntime = serviceSessionRuntime;
        this.retailStockPhysicalRuntime = retailStockPhysicalRuntime;
        serviceSessionCompletionPort = serviceSessionRuntime != null
            ? new ShopServiceSessionCompletionRuntimeAdapter(
                serviceSessionRuntime)
            : null;
        IRandomStream random = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("shop-runtime");
        Crime.Configure(crimeRiskEvaluator, random);

        TryInitializeStock(requireCatalog: false);
        // Persistent identity injection is a base-class concern and VContainer
        // does not guarantee base/derived [Inject] method ordering. Exact
        // authored stock is therefore activated lazily at the first stock
        // query, after placement/restore has established BuildingInstanceId.
    }

    public IEnumerator Interact(IBuildingVisitorPort actor) =>
        CustomerInteraction.Interact(actor);

    public override void ReleaseTransientCharacterOwnership(
        IBuildingVisitorPort actor,
        string reason)
    {
        if (actor == null)
        {
            return;
        }

        string actorId = actor.BuildingCharacterId.Value;
        string hubId = PersistentInstanceId.IsValid
            ? PersistentInstanceId.Value
            : string.Empty;
        string[] sessions = serviceSessionRuntime?.ActiveSessions?
            .Where(session => session != null
                && session.IsActive
                && string.Equals(session.ActorId, actorId, StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(hubId)
                    || string.Equals(session.HubId, hubId, StringComparison.Ordinal)))
            .Select(session => session.SessionId)
            .ToArray() ?? Array.Empty<string>();

        base.ReleaseTransientCharacterOwnership(actor, reason);
        for (int index = 0; index < sessions.Length; index++)
        {
            serviceSessionRuntime.CancelSession(
                sessions[index],
                string.IsNullOrWhiteSpace(reason)
                    ? "character-lifecycle-ended"
                    : reason);
        }

        // CharacterActor owns actor-wide meal cancellation once per lifecycle
        // transition.  A shop only releases ownership scoped to itself.
    }

    public static bool CreatesRevenueFor(IBuildingVisitorPort actor)
    {
        return actor == null || !IsInternalStaffUse(actor);
    }

    public static bool IsInternalStaffUse(IBuildingVisitorPort actor)
    {
        return actor?.VisitorSnapshot.IsInternalStaff == true;
    }

    public bool CanServeCustomer(IBuildingVisitorPort actor, out string failureReason)
    {
        failureReason = string.Empty;
        if (!CreatesRevenueFor(actor))
        {
            return true;
        }

        if (!RequiresServingWorker())
        {
            return true;
        }

        if (HasServingWorker)
        {
            return true;
        }

        failureReason = "직원 없음";
        return false;
    }

    public float GetCheckoutCrimeChance(int cartItemCount)
    {
        return GetCheckoutCrimeChance(null, cartItemCount, 0);
    }

    public float GetCheckoutCrimeChance(IBuildingVisitorPort actor, int cartItemCount, int cartValue)
    {
        return Crime.GetCheckoutChance(actor, cartItemCount, cartValue);
    }

    internal bool TryResolveCheckoutCrime(IBuildingVisitorPort actor, IReadOnlyList<RemainStock> cart)
    {
        return Crime.TryResolve(actor, cart);
    }

    private float GetPriceMultiplier()
    {
        PruneInvalidWorker();
        return worker == null
            ? 1f
            : Mathf.Clamp(
                worker.VisitorSnapshot.RevenueMultiplier,
                1f,
                GoldEconomyBalanceRules.MaximumWorkerRevenuePremium);
    }

    public List<Stock> GetStock()
    {
        return Inventory.GetStock(GetPriceMultiplier());
    }

    public IReadOnlyList<Stock> GetPurchasableStock()
    {
        return GetPurchasableStock(null);
    }

    private IReadOnlyList<Stock> GetPurchasableStock(
        IReadOnlyDictionary<int, int> selectedCounts)
    {
        PruneInvalidWorker();
        return Inventory.GetPurchasableStock(
            selectedCounts,
            GetPriceMultiplier());
    }

    public int GetStockCount()
    {
        return CurrentStock;
    }

    public bool TryRequestRestock(out string resultMessage)
    {
        if (!NeedsRestock)
        {
            resultMessage = "재고가 이미 충분합니다";
            return false;
        }
        if (workforceReplanService == null)
        {
            resultMessage = "보충 작업 배정 서비스를 찾지 못했습니다";
            return false;
        }

        workforceReplanService.RequestIdleWorkersToReplan(clearFailures: true);
        resultMessage = "물리 재고 보충 작업을 요청했습니다";
        return true;
    }

    public bool TryBeginRestockOperation(string operationId)
    {
        string canonical = operationId?.Trim() ?? string.Empty;
        if (canonical.Length == 0
            || !string.Equals(canonical, operationId, StringComparison.Ordinal))
        {
            return false;
        }
        return activeRestockOperations.Add(canonical);
    }

    public void EndRestockOperation(string operationId)
    {
        if (!string.IsNullOrWhiteSpace(operationId))
        {
            activeRestockOperations.Remove(operationId);
        }
    }

    public bool TryFindRestockSource(
        IEnumerable<IWarehouseFacility> warehouses,
        int maxAmount,
        out IWarehouseFacility warehouse,
        out WarehouseRestockItem saleItem,
        out int amount,
        out string failureReason)
    {
        return Inventory.TryFindRestockSource(
            warehouses,
            maxAmount,
            out warehouse,
            out saleItem,
            out amount,
            out failureReason);
    }

    public bool TryFindRestockSource(
        IEnumerable<IWarehouseFacility> warehouses,
        int maxAmount,
        out IWarehouseFacility warehouse,
        out SaleItem saleItem,
        out int amount,
        out string failureReason)
    {
        return Inventory.TryFindRestockSource(
            warehouses,
            maxAmount,
            out warehouse,
            out saleItem,
            out amount,
            out failureReason);
    }

    public bool HasRestockSupply(
        IEnumerable<IWarehouseFacility> warehouses,
        out string failureReason)
    {
        return Inventory.HasRestockSupply(
            warehouses?.ToArray(),
            out failureReason);
    }

    public bool HasRestockSupply(
        IReadOnlyList<IWarehouseFacility> warehouses,
        out string failureReason)
    {
        return Inventory.HasRestockSupply(warehouses, out failureReason);
    }

#if UNITY_EDITOR
    public void DebugClearStock()
    {
        foreach (RetailStockLotSnapshot lot in Inventory.CreateSnapshot().lots)
        {
            if (lot == null || string.IsNullOrEmpty(lot.itemInstanceId))
            {
                continue;
            }
            string failureReason = "retail-terminal-physical-runtime-unavailable";
            if (retailStockPhysicalRuntime == null
                || !retailStockPhysicalRuntime.TryCommitExternalSink(
                    lot,
                    out failureReason))
            {
                throw new InvalidOperationException(
                    $"Could not clear unique retail lot '{lot?.sourceOperationId}': {failureReason}");
            }
        }
        Inventory.Clear();
    }
#endif

    public ShopStockStateSnapshot CreateStockSnapshot()
    {
        // Save capture is an authority boundary. It must not depend on whether
        // UI or AI happened to query CurrentStock first after placement.
        SynchronizeAuthoredStock();
        ShopStockStateSnapshot snapshot = Inventory.CreateSnapshot();
        snapshot.activeRestockOperationIds = activeRestockOperations
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        return snapshot;
    }

    public void ApplyStockSnapshot(ShopStockStateSnapshot snapshot)
    {
        string[] restoredOperations = (snapshot?.activeRestockOperationIds
                ?? new List<string>())
            .ToArray();
        if (restoredOperations.Any(id =>
                string.IsNullOrWhiteSpace(id)
                || !string.Equals(id, id.Trim(), StringComparison.Ordinal))
            || restoredOperations.Distinct(StringComparer.Ordinal).Count()
                != restoredOperations.Length)
        {
            throw new InvalidOperationException(
                "Shop stock snapshot contains invalid active restock operations.");
        }
        Inventory.ApplySnapshot(snapshot);
        activeRestockOperations.Clear();
        foreach (string operationId in restoredOperations)
        {
            activeRestockOperations.Add(operationId);
        }
    }

    public bool TryReceiveExactRetailLots(
        IReadOnlyList<RetailStockLotSnapshot> incoming,
        int requestedAmount,
        out int received,
        out string resultMessage)
    {
        return Inventory.TryReceiveExactRetailLots(
            incoming,
            requestedAmount,
            out received,
            out resultMessage);
    }

    internal bool TryTakeExactRetailLot(
        int saleItemId,
        out RetailStockLotSnapshot taken,
        out string failureReason)
    {
        return Inventory.TryTakeExactLot(
            saleItemId,
            out taken,
            out failureReason);
    }

    internal bool TryRestoreTakenExactRetailLot(
        RetailStockLotSnapshot taken,
        out string failureReason)
    {
        return Inventory.TryRestoreTakenExactLot(taken, out failureReason);
    }

    internal bool TryCommitExactRetailExternalSink(
        RetailStockLotSnapshot lot,
        out string failureReason)
    {
        if (lot == null)
        {
            failureReason = "retail-terminal-lot-missing";
            return false;
        }
        if (retailStockPhysicalRuntime == null)
        {
            if (string.IsNullOrEmpty(lot.itemInstanceId))
            {
                failureReason = string.Empty;
                return true;
            }
            failureReason = "retail-terminal-physical-runtime-unavailable";
            return false;
        }
        return retailStockPhysicalRuntime.TryCommitExternalSink(
            lot,
            out failureReason);
    }

    private bool TryInitializeStock(bool requireCatalog)
    {
        return Inventory.TryInitialize(requireCatalog);
    }

    private void SynchronizeAuthoredStock()
    {
        if (inventoryRuntime == null)
        {
            return;
        }
        Inventory.SynchronizeAuthoredStock(retailStockPhysicalRuntime);
    }

    private void EnsureStockInitialized()
    {
        Inventory.EnsureInitialized();
    }

    internal void NotifyStockChanged()
    {
        MarkFacilityDynamicStateDirty();
    }

    internal void EndShopUse(IBuildingVisitorPort actor, bool completed)
    {
        if (!completed)
        {
            EndUse(actor);
            return;
        }

        if (!CompleteUse(actor))
        {
            throw new InvalidOperationException(
                $"Shop completion lost its active occupancy: "
                + $"shop={RequirePersistentInstanceId().Value}; "
                + $"actor={actor?.BuildingCharacterId.Value ?? "<missing>"}.");
        }
    }

    internal string DisplayNameForActivity => objectNameOrDefault();
    internal int CurrentGameFrame => GameFrameCount;

    internal StockCategory GetStockCategoryForSaleItem(int saleItemId)
    {
        return Inventory.GetStockCategory(saleItemId);
    }

    internal bool TryGetSaleItem(int saleItemId, out SaleItem saleItem) =>
        Inventory.TryGetSaleItem(saleItemId, out saleItem);

    internal void PublishStockConsumed(
        IBuildingVisitorPort actor,
        StockCategory category)
    {
        PublishGameEvent(new FacilityStockConsumedEvent(actor, this, category, 1));
    }

    internal void PublishShopliftingCrime(
        IBuildingVisitorPort actor,
        string detail,
        int lossValue)
    {
        PublishGameEvent(new FacilityCrimeEvent(
            actor,
            this,
            FacilityCrimeKind.Shoplifting,
            detail,
            lossValue));
        GameEventBus.RaiseAlert(
            "도난 발생",
            detail,
            EventAlertImportance.Medium,
            "범죄");
    }

    internal void PublishRestockEvent(
        int requested,
        int received,
        string resultMessage)
    {
        PublishGameEvent(new FacilityRestockEvent(
            this,
            requested,
            received,
            resultMessage));
    }

    private Vector2 GetCheckoutWorldPosition()
    {
        int endX = buildPoses.Max((pos) => pos.x) - 1;
        return grid.GetWorldPos(new Vector2Int(endX, centerPos.y));
    }

    internal override float GetLegacyWorkUrgency(FacilityWorkType workType)
    {
        float urgency = base.GetLegacyWorkUrgency(workType);
        if (workType == FacilityWorkType.Operate
            && WaitingCheckoutCount > 0
            && !HasServingWorker)
        {
            urgency += WaitingCheckoutOperateUrgency
                + WaitingCheckoutCount * WaitingCheckoutOperateUrgencyPerCustomer;
        }

        return urgency;
    }

    public override bool isVisitable()
    {
        return CanVisit((IBuildingCharacterPort)null, out _);
    }

    public FacilityAssignmentStatus GetWorkerAssignmentStatus(IBuildingVisitorPort actor)
    {
        PruneInvalidWorker();
        FacilityAssignmentStatus workStatus = FacilityAssignmentStatus.Rejected(
            FacilityAssignmentFailureKind.UnsupportedWork,
            "지원하지 않는 작업");
        FacilityWorkType supported =
            FacilityEvolutionWorkUtility.AddFallbackWorkTypes(
                this,
                Facility != null
                    ? Facility.supportedWorkTypes
                    : FacilityWorkType.Operate);
        foreach (WorkTypeDefinition definition in FacilityWorkTypeMap.Enumerate(
                     supported))
        {
            workStatus = GetWorkAssignmentStatus(definition.WorkTypeId);
            if (workStatus.IsAllowed)
            {
                break;
            }
        }

        if (!workStatus.IsAllowed)
        {
            return workStatus;
        }

        if (worker != null && worker != actor)
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Occupied,
                "이미 근무자 있음");
        }

        if (HasWorkerReservationForOther(actor))
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Reserved,
                "이미 작업 예약됨");
        }

        return FacilityAssignmentStatus.Allowed();
    }

    public bool CanAssignWorker(IBuildingVisitorPort actor, out string failureReason)
    {
        FacilityAssignmentStatus status = GetWorkerAssignmentStatus(actor);
        failureReason = status.Reason;
        return status.IsAllowed;
    }

    public IEnumerator AllocateWorker(IBuildingVisitorPort actor)
    {
        PruneInvalidWorker();
        if (worker != null && worker != actor)
        {
            yield break;
        }
        worker = actor;
        workerCharacterId = actor?.BuildingCharacterId ?? default;
        TrackAllocatedWorkerOwnership(actor);
        MarkFacilityDynamicStateDirty();
        ReleaseWorkerReservation(actor);
        if (actor == null || !actor.VisitorSnapshot.CanMove) yield break;

        Vector2 endPos = GetFacilityAnchorWorldPosition(
            FacilityAnchorPurposeIds.Work,
            actor.VisitorSnapshot.Position);
        object currentAction = actor.CurrentActionToken;
        actor.SetActionPhase("\uC791\uC5C5\uB300 \uC811\uADFC", this);
        yield return actor.MoveTo(endPos, 1f, currentAction);
        actor.ChangeLayer("DungeonMiddleObject");
        yield return actor.MoveTo(
            endPos + new Vector2(0, 0.15f),
            3f,
            currentAction);
        actor.SetActionPhase("\uC791\uC5C5 \uC790\uC138", this);
        actor.FaceRight();
    }

    public void DeallocateWorker(IBuildingVisitorPort actor)
    {
        if (actor == null)
        {
            return;
        }

        PruneInvalidWorker();
        if (worker != actor) return;

        worker = null;
        workerCharacterId = default;
        UntrackAllocatedWorkerOwnership(actor);
        MarkFacilityDynamicStateDirty();
        actor.SetActionPhase("\uC2DC\uC124 \uD1F4\uC7A5", this);
        Vector3 actorPosition = actor.VisitorSnapshot.Position - new Vector3(0, 0.15f);
        actor.SetWorldPosition(actorPosition);
        Vector2Int actorGridPosition = grid != null
            ? grid.GetXY(actorPosition)
            : centerPos;
        if (!ContainsGridPosition(actorGridPosition)
            && TryGetFacilityOccupiedWorldPosition(actorPosition, out Vector3 exitPosition))
        {
            actor.SetWorldPosition(exitPosition);
        }
        actor.ChangeLayer("Default");
    }

    private void PruneInvalidWorker()
    {
        if (worker == null)
        {
            return;
        }

        try
        {
            if (!worker.VisitorSnapshot.IsRuntimeActive)
            {
                UntrackTransientOwnership(
                    workerCharacterId,
                    BuildingTransientOwnershipKind.AllocatedWorker);
                worker = null;
                workerCharacterId = default;
                MarkFacilityDynamicStateDirty();
            }
        }
        catch (MissingReferenceException)
        {
            UntrackTransientOwnership(
                workerCharacterId,
                BuildingTransientOwnershipKind.AllocatedWorker);
            worker = null;
            workerCharacterId = default;
            MarkFacilityDynamicStateDirty();
        }
    }

    private bool RequiresServingWorker()
    {
        return Facility != null
            && BuildingData.RequiresStaffedService()
            && Facility.SupportsWork(BuiltInWorkTypeIds.Operate);
    }

    private string objectNameOrDefault()
    {
        return BuildingData != null && !string.IsNullOrWhiteSpace(BuildingData.objectName)
            ? BuildingData.objectName
            : name;
    }

    private string ResolveFacilityPersistentId()
    {
        return RequirePersistentInstanceId().Value;
    }


    internal ShopInventoryRuntime CustomerInventory => Inventory;
    internal ShopServiceCompletion CustomerServiceCompletion => ServiceCompletion;
    internal IServiceSessionRuntime CustomerServiceSessionRuntime =>
        serviceSessionRuntime;
    internal IShopServiceSessionCompletionPort CustomerServiceSessionCompletion =>
        serviceSessionCompletionPort;
    internal IGameMoneyAccount CustomerMoneyAccount => moneyAccount;
    internal IRoomEnvironmentExperienceService CustomerRoomEnvironmentExperienceService =>
        roomEnvironmentExperienceService;
    internal IMealConsumptionRuntime CustomerMealConsumptionRuntime =>
        mealConsumptionRuntime;
    internal IBuildingVisitorPort ServingWorker
    {
        get
        {
            PruneInvalidWorker();
            return worker;
        }
    }
    internal IGameEventBus CustomerGameEventBus => GameEventBus;
    internal float CustomerGameTime => GameTime;
    internal float CustomerGameDeltaTime => GameDeltaTime;
    internal string CustomerDisplayName => objectNameOrDefault();

    internal void EnsureCustomerStockInitialized() => EnsureStockInitialized();

    internal IReadOnlyList<Stock> GetCustomerPurchasableStock(
        IReadOnlyDictionary<int, int> selectedCounts) =>
        GetPurchasableStock(selectedCounts);

    internal bool RequiresCustomerServingWorker() => RequiresServingWorker();

    internal void MarkCustomerFacilityStateDirty() =>
        MarkFacilityDynamicStateDirty();

    internal void PublishCustomerGameEvent<TEvent>(TEvent gameEvent) =>
        PublishGameEvent(gameEvent);

    internal IFloatingNumberFeedbackService RequireCustomerFloatingNumberFeedbackService() =>
        RequireFloatingNumberFeedbackService();

    internal IBuildingWorkforceReplanPort RequireCustomerWorkforceReplanService() =>
        RequireWorkforceReplanService();

    private IFloatingNumberFeedbackService RequireFloatingNumberFeedbackService()
    {
        return floatingNumberFeedbackService
            ?? throw new InvalidOperationException(
                $"{nameof(Shop)} requires "
                + $"{nameof(IFloatingNumberFeedbackService)} injection.");
    }

    private IBuildingWorkforceReplanPort RequireWorkforceReplanService()
    {
        return workforceReplanService
            ?? throw new InvalidOperationException(
                $"{nameof(Shop)} requires "
                + $"{nameof(IBuildingWorkforceReplanPort)} injection.");
    }

}

internal sealed class ShopServiceSessionCompletionRuntimeAdapter :
    IShopServiceSessionCompletionPort
{
    private readonly IServiceSessionRuntime runtime;

    public ShopServiceSessionCompletionRuntimeAdapter(
        IServiceSessionRuntime runtime)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
    }

    public bool TryCompleteSession(
        string sessionId,
        out string failureCode)
    {
        if (runtime.TryCompleteSession(
                sessionId,
                out _,
                out DomainFailure failure))
        {
            failureCode = string.Empty;
            return true;
        }

        failureCode = failure.Code.ToString();
        return false;
    }

    public void CancelSession(string sessionId, string reason)
    {
        runtime.CancelSession(sessionId, reason);
    }
}
