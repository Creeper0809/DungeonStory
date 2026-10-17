using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public class Shop : BuildableObject,
    IRetailFacility,
    IRestockableFacility,
    IRetailStockStateOwner,
    IRetailRestockOperationOwner,
    IWorkableFacility,
    IAllocatedWorkerOccupancyQuery
{
    private const float WaitingCheckoutOperateUrgency = 160f;
    private const float WaitingCheckoutOperateUrgencyPerCustomer = 40f;
    private ShopInventoryRuntime inventoryRuntime;
    private ShopCrimeRuntime crimeRuntime;
    private ShopServiceCompletion serviceCompletion;
    private ShopCustomerInteractionService customerInteraction;
    private ShopInteractionState interactionState = new();
    internal ShopInteractionState InteractionState => interactionState;
    private BuildingWorkerAllocation workerAllocation;
    private IBuildingVisitorPort worker => workerAllocation?.Actor;
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
        interactionState = new ShopInteractionState();
        RegisterStateModule(new ShopUseExecutionStateModule(this));
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

    [Inject]
    public void ConstructShopCrimeFacts(
        ShopliftingFactJournal facts,
        IGameSessionStateProvider gameDataProvider)
    {
        Crime.ConfigureFacts(
            facts ?? throw new ArgumentNullException(nameof(facts)),
            gameDataProvider
                ?? throw new ArgumentNullException(nameof(gameDataProvider)));
    }
#if UNITY_EDITOR
    internal void ConfigureCrimeForDiagnostics(
        IFacilityCrimeRiskEvaluator crimeRiskEvaluator,
        IRandomStream randomStream)
    {
        Crime.Configure(
            crimeRiskEvaluator
                ?? throw new ArgumentNullException(nameof(crimeRiskEvaluator)),
            randomStream
                ?? throw new ArgumentNullException(nameof(randomStream)));
    }
#endif
    public bool HasAllocatedWorker => HasServingWorker;

    public IEnumerator Interact(IBuildingVisitorPort actor) =>
        CustomerInteraction.Interact(actor);

    internal ShopUseExecutionState FindActiveShopUse(string actorId) =>
        interactionState.activeUses.SingleOrDefault(value => value.actorId == actorId);

    internal ShopUseExecutionState BeginShopExecution(IBuildingVisitorPort actor, ServiceSessionSnapshot session)
    {
        if (FindActiveShopUse(actor.BuildingCharacterId.Value) != null)
            throw new InvalidOperationException("A shop execution already owns this actor.");
        long sequence = interactionState.nextSequence;
        long next = checked(sequence + 1);
        AIAction action = actor.CurrentActionToken as AIAction;
        var state = new ShopUseExecutionState
        {
            sequence = sequence,
            operationId = ShopUseExecutionState.OperationFor(RequirePersistentInstanceId().Value, sequence),
            actorId = actor.BuildingCharacterId.Value,
            actionType = action?.actionset?.GetType().FullName ?? string.Empty,
            actionBranch = (int)(action?.actionset?.Branch ?? CharacterAiBranch.None),
            serviceSessionId = session?.SessionId ?? string.Empty,
            selectionCount = actor.Shopping.GetShoppingCount(),
            createsRevenue = CreatesRevenueFor(actor),
            phase = ShopUseExecutionPhase.Selection
        };
        interactionState.nextSequence = next;
        interactionState.activeUses.Add(state);
        MarkFacilityDynamicStateDirty();
        return state;
    }

    internal ShopUseCartLine RequirePurchaseLine(string operationId, int cartIndex, string actorId)
    {
        ShopUseExecutionState use = FindActiveShopUse(actorId);
        if (use == null || use.operationId != operationId || use.phase != ShopUseExecutionPhase.Purchase
            || use.purchaseIndex != cartIndex || cartIndex < 0 || cartIndex >= use.cart.Count)
            throw new InvalidOperationException("Retail purchase execution/actor/slot mismatch.");
        return use.cart[cartIndex];
    }

    internal void ValidatePurchaseBinding(RetailPurchaseRecoverySaveData recovery)
    {
        if (string.IsNullOrEmpty(recovery.shopUseOperationId)) return;
        ShopUseExecutionState use = FindActiveShopUse(recovery.buyerId);
        if (use == null) throw new InvalidOperationException("Retail purchase execution owner missing.");
        use.ValidatePurchaseBinding(RequirePersistentInstanceId().Value, recovery);
    }

    internal void RecordPurchaseBinding(RetailPurchaseRecoverySaveData recovery)
    {
        ValidatePurchaseBinding(recovery);
        if (string.IsNullOrEmpty(recovery.shopUseOperationId)) return;
        ShopUseCartLine line = RequirePurchaseLine(recovery.shopUseOperationId,
            recovery.shopUseCartIndex, recovery.buyerId);
        line.purchaseOperationId = recovery.operationId;
        line.pendingPurchaseOperationId = string.Empty;
        line.committedLot = recovery.lot.Clone();
        MarkFacilityDynamicStateDirty();
    }

    internal ShopInteractionState CaptureInteractionState()
    {
        ShopInteractionState snapshot = interactionState.Clone();
        if (!snapshot.IsValid(RequirePersistentInstanceId().Value, out string error))
            throw new InvalidOperationException(error);
        return snapshot;
    }

    internal void RestoreInteractionState(ShopInteractionState snapshot)
    {
        if (snapshot == null || !snapshot.IsValid(RequirePersistentInstanceId().Value, out string error))
            throw new InvalidOperationException("Invalid shop interaction restore.");
        interactionState = snapshot.Clone();
        MarkFacilityDynamicStateDirty();
    }

    public override void ReleaseTransientCharacterOwnership(
        IBuildingVisitorPort actor,
        string reason)
    {
        if (actor == null)
        {
            return;
        }

        if (serviceSessionRuntime?.IsReplacedFacility(this) == true)
        {
            base.ReleaseTransientCharacterOwnership(actor, reason);
            return;
        }

        string actorId = actor.BuildingCharacterId.Value;
        if (FindActiveShopUse(actorId)?.cart.Any(value => !string.IsNullOrEmpty(value.pendingPurchaseOperationId)) == true)
            throw new InvalidOperationException("Shop release is deferred until its purchase receipt is settled.");
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

        for (int index = 0; index < sessions.Length; index++)
        {
            if (!serviceSessionRuntime.CancelSession(
                sessions[index],
                string.IsNullOrWhiteSpace(reason)
                    ? "character-lifecycle-ended"
                    : reason))
                throw new InvalidOperationException("Shop release cancellation remains pending.");
        }
        base.ReleaseTransientCharacterOwnership(actor, reason);

        // CharacterActor owns actor-wide meal cancellation once per lifecycle
        // transition.  A shop only releases ownership scoped to itself.
        interactionState.activeUses.RemoveAll(value => value.actorId == actorId);
        MarkFacilityDynamicStateDirty();
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
        return TryTakeExactRetailLot(
            saleItemId,
            out taken,
            out _,
            out failureReason);
    }

    internal RetailRevenueModifierSnapshot CaptureRevenueModifierSnapshot()
    {
        PruneInvalidWorker();
        if (worker == null)
            return RetailRevenueModifierSnapshot.Unmodified;

        BuildingVisitorSnapshot visitor = worker.VisitorSnapshot;
        CharacterActor workerActor =
            CharacterBuildingVisitorAdapter.GetActorOrNull(worker);
        string provenance = CharacterSkillRuntimeEffects
            .CaptureManagementModifierProvenance(
                workerActor,
                CharacterSkillTrigger.WorkCompleted,
                new[] { "revenue" });
        return new RetailRevenueModifierSnapshot(
            visitor.PersistentId,
            Mathf.Clamp(
                visitor.RevenueMultiplier,
                1f,
                GoldEconomyBalanceRules.MaximumWorkerRevenuePremium),
            provenance);
    }

    internal bool TryTakeExactRetailLot(
        int saleItemId,
        out RetailStockLotSnapshot taken,
        out string unitOperationId,
        out string failureReason)
    {
        return Inventory.TryTakeExactLot(
            saleItemId,
            out taken,
            out unitOperationId,
            out failureReason);
    }

    internal bool TryPreviewExactRetailLotOperationId(
        int saleItemId,
        out string unitOperationId)
        => TryPreviewExactRetailLot(saleItemId, out _, out unitOperationId);

    internal bool TryPreviewExactRetailLot(int saleItemId,
        out RetailStockLotSnapshot source, out string unitOperationId)
    {
        SynchronizeAuthoredStock();
        source = Inventory.CreateSnapshot().lots
            .Where(lot => lot != null
                && lot.saleItemId == saleItemId
                && lot.quantity > 0)
            .OrderBy(lot => lot.sourceOperationId, StringComparer.Ordinal)
            .ThenBy(lot => lot.itemInstanceId, StringComparer.Ordinal)
            .FirstOrDefault();
        unitOperationId = source == null
            ? string.Empty
            : $"retail-unit:{source.sourceOperationId}:ordinal:{source.quantity:D10}";
        return unitOperationId.Length > 0;
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

    internal bool TryCommitRetailSinkReversible(RetailStockLotSnapshot lot,
        out IRetailStockSinkTransaction transaction, out string failure)
    {
        transaction = null; failure = string.Empty;
        if (lot == null) { failure = "retail-terminal-lot-missing"; return false; }
        if (string.IsNullOrEmpty(lot.itemInstanceId))
        { transaction = RetailEquipmentSinkTransaction.NoSecondaryOwner(); return true; }
        if (retailStockPhysicalRuntime is IReversibleRetailStockPhysicalRuntime reversible)
            return reversible.TryCommitExternalSinkReversible(lot, out transaction, out failure);
        failure = "retail-reversible-physical-runtime-unavailable"; return false;
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
            interactionState.activeUses.RemoveAll(value => value.actorId == actor?.BuildingCharacterId.Value);
            MarkFacilityDynamicStateDirty();
            return;
        }

        if (!CompleteUse(actor))
        {
            throw new InvalidOperationException(
                $"Shop completion lost its active occupancy: "
                + $"shop={RequirePersistentInstanceId().Value}; "
                + $"actor={actor?.BuildingCharacterId.Value ?? "<missing>"}.");
        }
        interactionState.activeUses.RemoveAll(value => value.actorId == actor?.BuildingCharacterId.Value);
        MarkFacilityDynamicStateDirty();
    }

    internal void ResumeFailedServiceCleanup(IBuildingVisitorPort actor, ShopUseExecutionState state) =>
        CustomerInteraction.ResumeFailedServiceCleanup(actor, state);

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

    internal void PublishShopliftingCompleted(IBuildingVisitorPort actor, ShopliftingFact fact)
    {
        try { PublishGameEvent(new ShopliftingCompletedEvent(this, actor, fact)); }
        catch (Exception error) when (error is not OutOfMemoryException
            && error is not StackOverflowException && error is not AccessViolationException)
        {
            // Observation cannot turn an already-applied stock transaction into a failed checkout.
            Debug.LogError("shoplifting-completed-observation:" + error.GetType().Name);
        }
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
        if (TryAllocateWorker(actor, actor?.CurrentActionToken, null,
                out IBuildingWorkerAllocation allocation, out _))
            yield return allocation.Approach();
    }

    public bool TryAllocateWorker(IBuildingVisitorPort actor, object expectedAction,
        Func<bool> isRequestCurrent, out IBuildingWorkerAllocation allocation,
        out string failureReason)
    {
        allocation = null;
        PruneInvalidWorker();
        if (actor == null || !actor.VisitorSnapshot.IsRuntimeActive)
        {
            failureReason = "worker-unavailable";
            return false;
        }
        if (workerAllocation != null)
        {
            failureReason = "worker-already-allocated";
            return false;
        }
        if (!CanAssignWorker(actor, out failureReason))
            return false;
        if (expectedAction != null && (!actor.IsCurrentAction(expectedAction)
                || actor.IsCurrentActionEnded)
            || isRequestCurrent != null && !isRequestCurrent())
        {
            failureReason = "worker-allocation-request-replaced";
            return false;
        }
        var admitted = new BuildingWorkerAllocation(this, actor, expectedAction,
            isRequestCurrent, value => ReferenceEquals(workerAllocation, value),
            ReleaseWorkerAllocation,
            GetFacilityAnchorWorldPosition(FacilityAnchorPurposeIds.Work,
                actor.VisitorSnapshot.Position), new Vector3(0f, 0.15f),
            ResolveWorkerExitPosition, "작업대 접근", "작업 자세");
        workerAllocation = admitted;
        allocation = admitted;
        try
        {
            TrackAllocatedWorkerOwnership(actor);
            ReleaseWorkerReservation(actor);
            MarkFacilityDynamicStateDirty();
            failureReason = string.Empty;
            return true;
        }
        catch
        {
            admitted.Dispose();
            throw;
        }
    }

    public void DeallocateWorker(IBuildingVisitorPort actor)
    {
        if (actor != null && ReferenceEquals(worker, actor))
            workerAllocation.Dispose();
    }

    private void ReleaseWorkerAllocation(BuildingWorkerAllocation allocation)
    {
        if (!ReferenceEquals(workerAllocation, allocation))
            return;
        workerAllocation = null;
        UntrackTransientOwnership(allocation.CharacterId,
            BuildingTransientOwnershipKind.AllocatedWorker);
        MarkFacilityDynamicStateDirty();
    }

    private Vector3 ResolveWorkerExitPosition(Vector3 position)
    {
        Vector2Int actorGridPosition = grid != null ? grid.GetXY(position) : centerPos;
        return !ContainsGridPosition(actorGridPosition)
            && TryGetFacilityOccupiedWorldPosition(position, out Vector3 exit)
                ? exit : position;
    }

    private void PruneInvalidWorker()
    {
        if (worker == null)
            return;
        try
        {
            if (!worker.VisitorSnapshot.IsRuntimeActive)
                workerAllocation.Dispose();
        }
        catch (MissingReferenceException)
        {
            workerAllocation.Dispose();
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
    internal bool TryCommitExactRetailRevenue(
        string unitOperationId,
        string buyerId,
        int amount,
        out string failureReason)
    {
        int revenue = Mathf.Max(0, amount);
        if (revenue == 0)
        {
            failureReason = string.Empty;
            return true;
        }
        if (moneyAccount is not IIdempotentGameMoneyAccount idempotent)
        {
            failureReason = "retail-revenue-account-not-idempotent";
            return false;
        }
        string operation = unitOperationId?.Trim() ?? string.Empty;
        if (operation.Length == 0)
        {
            failureReason = "retail-revenue-operation-id-missing";
            return false;
        }
        return idempotent.TryCreditOnce(
            revenue,
            new EconomyTransactionContext(
                EconomyTransactionKind.GuestServiceIncome,
                "retail-revenue:" + operation,
                buyerId?.Trim() ?? string.Empty,
                "손님 물품 판매"),
            out failureReason);
    }
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
