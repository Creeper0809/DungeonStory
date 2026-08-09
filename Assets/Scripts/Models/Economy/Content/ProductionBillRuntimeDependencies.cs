using System;
using DungeonStory.Foundation;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionBillOrderDependencies
{
    public ProductionBillOrderDependencies(
        IResourceEconomyContentCatalog catalog,
        IProductionAssemblyBridge bridge,
        IProductionStockSensorRuntime stockSensors,
        ProductionAggregateStateStore stateStore,
        IRecipeBalanceWorkCalculator balanceWorkCalculator = null)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        StockSensors = stockSensors
            ?? throw new ArgumentNullException(nameof(stockSensors));
        StateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        BalanceWorkCalculator = balanceWorkCalculator;
    }

    public IResourceEconomyContentCatalog Catalog { get; }
    public IProductionAssemblyBridge Bridge { get; }
    public IProductionStockSensorRuntime StockSensors { get; }
    public ProductionAggregateStateStore StateStore { get; }
    public IRecipeBalanceWorkCalculator BalanceWorkCalculator { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionBillExecutionDependencies
{
    public ProductionBillExecutionDependencies(
        IProductionOutputPlanningService outputPlanning,
        IProductionOutputExecutionService outputExecution,
        IProductionBillSnapshotProjector snapshotProjector,
        IProductionAssemblyBridge bridge,
        IGameClock clock)
    {
        OutputPlanning = outputPlanning
            ?? throw new ArgumentNullException(nameof(outputPlanning));
        OutputExecution = outputExecution
            ?? throw new ArgumentNullException(nameof(outputExecution));
        SnapshotProjector = snapshotProjector
            ?? throw new ArgumentNullException(nameof(snapshotProjector));
        Bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public IProductionOutputPlanningService OutputPlanning { get; }
    public IProductionOutputExecutionService OutputExecution { get; }
    public IProductionBillSnapshotProjector SnapshotProjector { get; }
    public IProductionAssemblyBridge Bridge { get; }
    public IGameClock Clock { get; }
}
