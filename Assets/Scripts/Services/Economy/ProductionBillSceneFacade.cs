using System;
using System.Collections.Generic;
using DungeonStory.Foundation;

/// <summary>
/// Legacy scene API retained for existing UI/work handlers. All state changes
/// are delegated to the named Economy aggregate after actor capture.
/// </summary>
public sealed class ProductionBillSceneFacade :
    IProductionBillQuery,
    IProductionBillOrderCommand,
    IProductionBillWorkExecution
{
    private readonly IProductionBillCoreQuery query;
    private readonly IProductionBillCoreOrderCommand orders;
    private readonly IProductionBillCoreWorkExecution work;
    private readonly IProductionAssemblyBridge bridge;
    private readonly ExtremeTraitRuntime extremeTraits;
    private readonly IGameClock clock;
    private readonly CharacterIdentityEventPublisher identityEvents;

    public ProductionBillSceneFacade(
        IProductionBillCoreQuery query,
        IProductionBillCoreOrderCommand orders,
        IProductionBillCoreWorkExecution work,
        IProductionAssemblyBridge bridge,
        ExtremeTraitRuntime extremeTraits,
        IGameClock clock,
        CharacterIdentityEventPublisher identityEvents)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.orders = orders ?? throw new ArgumentNullException(nameof(orders));
        this.work = work ?? throw new ArgumentNullException(nameof(work));
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        this.extremeTraits = extremeTraits
            ?? throw new ArgumentNullException(nameof(extremeTraits));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.identityEvents = identityEvents
            ?? throw new ArgumentNullException(nameof(identityEvents));
    }

    public int Version => query.Version;
    public IReadOnlyList<ProductionBillSnapshot> GetBills(
        BuildableObject facility) => query.GetBills(
            bridge.CaptureFacility(facility));
    public bool HasStockSensor(BuildableObject facility) =>
        query.HasStockSensor(bridge.CaptureFacility(facility));
    public ProductionBillCommandResult AddBill(
        BuildableObject facility,
        string recipeId,
        ProductionOrderMode mode,
        int amount) => orders.AddBill(
            bridge.CaptureFacility(facility),
            recipeId,
            mode,
            amount);
    public ProductionBillCommandResult RemoveBill(
        ProductionBillId billId,
        bool returnMaterials) => orders.RemoveBill(billId, returnMaterials);
    public ProductionBillCommandResult MoveBill(
        ProductionBillId billId,
        int targetIndex) => orders.MoveBill(billId, targetIndex);
    public ProductionBillCommandResult SetSuspended(
        ProductionBillId billId,
        bool suspended) => orders.SetSuspended(billId, suspended);
    public ProductionBillCommandResult SetStockPolicy(
        ProductionBillId billId,
        int minimumReserve,
        int targetStock) => orders.SetStockPolicy(
            billId,
            minimumReserve,
            targetStock);
    public ProductionBillCommandResult SetOrderMode(
        ProductionBillId billId,
        ProductionOrderMode mode,
        int amount) => orders.SetOrderMode(billId, mode, amount);
    public ProductionBillCommandResult SetDistributionPolicy(
        ProductionBillId billId,
        ProductionDistributionMode mode,
        IReadOnlyList<ProductionConsumerRoutePolicy> routes) =>
        orders.SetDistributionPolicy(billId, mode, routes);
    public ProductionBillCommandResult SetWorkerPolicy(
        ProductionBillId billId,
        WorkerSelectionPolicySaveData policy) =>
        orders.SetWorkerPolicy(billId, policy);
    public ProductionBillCommandResult RequestStockSensorInstallation(
        BuildableObject facility) => orders.RequestStockSensorInstallation(
            bridge.CaptureFacility(facility));
    public ProductionBillCommandResult AcknowledgeStockSensorUnlock(
        BuildableObject facility) => orders.AcknowledgeStockSensorUnlock(
            bridge.CaptureFacility(facility));
    public ProductionBillCommandResult RemoveStockSensor(
        BuildableObject facility) => orders.RemoveStockSensor(
            bridge.CaptureFacility(facility));
    public ProductionWorkAvailabilityResult CheckWorkAvailability(
        BuildableObject facility,
        WorkTypeId workTypeId) => work.CheckWorkAvailability(
            bridge.CaptureFacility(facility),
            workTypeId);
    public ProductionWorkBeginResult BeginWork(
        CharacterActor worker,
        BuildableObject facility,
        WorkTypeId workTypeId)
    {
        ProductionFacilityHandle facilityHandle = bridge.CaptureFacility(facility);
        ProductionWorkAvailabilityResult preview = work.CheckWorkAvailability(
            facilityHandle,
            workTypeId);
        if (preview.Available
            && preview.Bill != null
            && !string.IsNullOrWhiteSpace(preview.Bill.EmergencyWorkerId)
            && string.Equals(
                preview.Bill.EmergencyWorkerId,
                worker?.Identity?.PersistentId,
                StringComparison.Ordinal)
            && !extremeTraits.CanBeginProductionLimitBreak(
                worker,
                preview.Bill.BillId.Value,
                clock.Time))
        {
            return new ProductionWorkBeginResult(
                null,
                new DomainFailure(
                    FailureCode.WorkOrderWorkerIneligible,
                    "production-limit-break-unavailable"));
        }

        ProductionWorkBeginResult result = work.BeginWork(
            bridge.CaptureWorker(worker),
            facilityHandle,
            workTypeId);
        if (!result.Succeeded
            || string.IsNullOrWhiteSpace(result.Bill.EmergencyWorkerId)
            || !string.Equals(
                result.Bill.EmergencyWorkerId,
                worker?.Identity?.PersistentId,
                StringComparison.Ordinal))
            return result;

        if (!extremeTraits.TryBeginProductionLimitBreak(
                worker,
                result.Bill.BillId.Value,
                clock.Time,
                out _))
        {
            throw new InvalidOperationException(
                "Production limit-break prevalidation diverged after the production "
                + "aggregate accepted work. This would leave consumed inputs without "
                + "the configured extreme-trait state.");
        }
        return result;
    }
    public ProductionWorkExecutionResult ExecuteWork(
        CharacterActor worker,
        BuildableObject facility,
        ProductionBillId billId,
        float amount)
    {
        ProductionWorkExecutionResult result = work.ExecuteWork(
            bridge.CaptureWorker(worker),
            bridge.CaptureFacility(facility),
            billId,
            amount);
        if (result.Succeeded)
        {
            extremeTraits.RefreshProductionLimitBreak(
                worker,
                billId.Value,
                clock.Time);
        }
        if (result.CycleCompleted)
        {
            extremeTraits.EndProductionLimitBreak(worker, billId.Value, clock.Time);
            PublishProductionCompleted(worker, billId, result.Outcome.ToString());
        }
        return result;
    }

    [GameplayEntryPoint(
        "ProductionBuildingPanelPresenter limit-break toggle; V26 extreme-trait focused audit")]
    public bool TrySetEmergencyProduction(
        CharacterActor worker,
        ProductionBillId billId,
        bool enabled,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (worker == null || !billId.IsValid)
        {
            failureReason = "작업자와 생산 주문이 필요합니다.";
            return false;
        }
        if (enabled)
        {
            if (!extremeTraits.CanConfigureProductionLimitBreak(worker, clock.Time))
            {
                failureReason = "한계 돌파를 시작할 수 없습니다.";
                return false;
            }
            ProductionBillCommandResult configured = orders.SetEmergencyWorker(
                billId,
                worker.Identity.PersistentId);
            if (!configured.Succeeded)
            {
                failureReason = configured.Failure.Code.ToString();
                return false;
            }
            return true;
        }
        ProductionBillCommandResult cleared = orders.SetEmergencyWorker(
            billId,
            string.Empty);
        if (!cleared.Succeeded)
        {
            failureReason = cleared.Failure.Code.ToString();
            return false;
        }
        extremeTraits.EndProductionLimitBreak(worker, billId.Value, clock.Time);
        return true;
    }

    private void PublishProductionCompleted(
        CharacterActor worker,
        ProductionBillId billId,
        string outcomeId)
    {
        if (worker == null
            || !CharacterPersistentIdentity.TryGet(worker, out CharacterId id))
            return;
        identityEvents.Publish(new WorkCompletedIdentityEvent(
            id,
            $"production:{billId.Value}",
            outcomeId,
            CharacterCommandOrigin.Autonomous,
            Math.Max(0, (int)(clock.Time / GameCalendarRules.SecondsPerDay))));
    }
}
