using System;
using System.Collections.Generic;

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

    public ProductionBillSceneFacade(
        IProductionBillCoreQuery query,
        IProductionBillCoreOrderCommand orders,
        IProductionBillCoreWorkExecution work,
        IProductionAssemblyBridge bridge)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.orders = orders ?? throw new ArgumentNullException(nameof(orders));
        this.work = work ?? throw new ArgumentNullException(nameof(work));
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
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
        WorkTypeId workTypeId) => work.BeginWork(
            bridge.CaptureWorker(worker),
            bridge.CaptureFacility(facility),
            workTypeId);
    public ProductionWorkExecutionResult ExecuteWork(
        CharacterActor worker,
        BuildableObject facility,
        ProductionBillId billId,
        float amount) => work.ExecuteWork(
            bridge.CaptureWorker(worker),
            bridge.CaptureFacility(facility),
            billId,
            amount);
}

